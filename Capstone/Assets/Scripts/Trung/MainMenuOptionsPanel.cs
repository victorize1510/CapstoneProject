using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using Capstone.Game.SettingsSystem;

// Cài đặt game, lưu bằng PlayerPrefs. Tự áp dụng lại mỗi khi mở game (cả khi vào thẳng DemoScene).
public static class GameOptions
{
    // Âm lượng tổng, toàn màn hình, độ phân giải: dùng chung GameSettingsService của game
    // (cùng chỗ lưu với màn Cài đặt Esc trong gameplay). Phần còn lại lưu PlayerPrefs.
    const string KeyMusic = "opt.musicVolume";
    const string KeySfx = "opt.sfxVolume";
    const string KeyQuality = "opt.quality";
    const string KeyVSync = "opt.vsync";
    const string KeyFps = "opt.fps";
    static readonly string[] LocalKeys = { KeyMusic, KeySfx, KeyQuality, KeyVSync, KeyFps };

    public static readonly int[] FpsChoices = { 30, 60, 120, 144, -1 };   // -1 = không giới hạn

    // AudioMixer để chỉnh riêng Nhạc nền / Hiệu ứng. Tự lấy Resources/GameAudioMixer nếu chưa gán.
    public static AudioMixer Mixer;
    public static string MusicParameter = "MusicVolume";
    public static string SfxParameter = "SfxVolume";
    public const string MixerResourceName = "GameAudioMixer";

    static SettingsState Shared => GameSettingsService.Instance != null ? GameSettingsService.Instance.Model : null;

    public static float MasterVolume
    {
        get => Shared != null ? Shared.Data.masterVolume : 1f;
        set { if (Shared != null) Shared.Change(SettingsArea.Audio, d => { d.masterVolume = value; d.muteAll = false; }); }
    }
    public static float MusicVolume { get => PlayerPrefs.GetFloat(KeyMusic, 0.8f); set { PlayerPrefs.SetFloat(KeyMusic, value); ApplyAudio(); } }
    public static float SfxVolume { get => PlayerPrefs.GetFloat(KeySfx, 0.8f); set { PlayerPrefs.SetFloat(KeySfx, value); ApplyAudio(); } }

    // 0 = Toàn màn hình, 1 = Cửa sổ.
    public static int ScreenModeIndex
    {
        get => Shared != null && !Shared.Data.fullscreen ? 1 : 0;
        set { if (Shared != null) Shared.Change(SettingsArea.Display, d => d.fullscreen = value == 0); }
    }
    public static int QualityLevel
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyQuality, QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
        set { PlayerPrefs.SetInt(KeyQuality, value); QualitySettings.SetQualityLevel(value, true); ApplyFrameRate(); }
    }
    public static bool VSync { get => PlayerPrefs.GetInt(KeyVSync, 1) == 1; set { PlayerPrefs.SetInt(KeyVSync, value ? 1 : 0); ApplyFrameRate(); } }
    public static int FpsIndex
    {
        get => Mathf.Clamp(PlayerPrefs.GetInt(KeyFps, 1), 0, FpsChoices.Length - 1);
        set { PlayerPrefs.SetInt(KeyFps, value); ApplyFrameRate(); }
    }

    // Cùng danh sách với màn Cài đặt trong gameplay.
    public static List<Vector2Int> Resolutions()
    {
        var current = new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);
        List<Vector2Int> list = DisplaySettings.Available(Screen.resolutions, current);
        if (list.Count == 0) list.Add(new Vector2Int(Screen.width, Screen.height));
        return list;
    }

    public static int ResolutionIndex
    {
        get
        {
            List<Vector2Int> list = Resolutions();
            var saved = Shared != null ? new Vector2Int(Shared.Data.resolutionWidth, Shared.Data.resolutionHeight) : Vector2Int.zero;
            int index = list.IndexOf(saved);
            return index >= 0 ? index : 0;
        }
        set
        {
            List<Vector2Int> list = Resolutions();
            Vector2Int size = list[Mathf.Clamp(value, 0, list.Count - 1)];
            if (Shared != null) Shared.Change(SettingsArea.Display, d => { d.resolutionWidth = size.x; d.resolutionHeight = size.y; });
        }
    }

    public static void ResetToDefault()
    {
        foreach (string key in LocalKeys) PlayerPrefs.DeleteKey(key);
        Shared?.Reset();
        ApplyAll();
    }

    // Chụp lại cài đặt lúc mở bảng để bấm Hủy thì trả về như cũ.
    public sealed class Snapshot
    {
        public GameSettingsData shared;
        public readonly Dictionary<string, object> local = new Dictionary<string, object>();
    }

    public static Snapshot Capture()
    {
        var snapshot = new Snapshot { shared = Shared?.Data.Copy() };
        foreach (string key in LocalKeys)
        {
            if (!PlayerPrefs.HasKey(key)) continue;
            snapshot.local[key] = key == KeyMusic || key == KeySfx ? PlayerPrefs.GetFloat(key) : (object)PlayerPrefs.GetInt(key);
        }
        return snapshot;
    }

    public static void Restore(Snapshot snapshot)
    {
        if (snapshot == null) return;
        foreach (string key in LocalKeys)
        {
            if (!snapshot.local.TryGetValue(key, out object value)) PlayerPrefs.DeleteKey(key);
            else if (value is float number) PlayerPrefs.SetFloat(key, number);
            else PlayerPrefs.SetInt(key, (int)value);
        }
        if (Shared != null && snapshot.shared != null)
        {
            GameSettingsData old = snapshot.shared;
            Shared.Change(SettingsArea.Audio | SettingsArea.Display, d =>
            {
                d.masterVolume = old.masterVolume;
                d.muteAll = old.muteAll;
                d.fullscreen = old.fullscreen;
                d.resolutionWidth = old.resolutionWidth;
                d.resolutionHeight = old.resolutionHeight;
            });
        }
        ApplyAll();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void ApplyAll()
    {
        ApplyAudio();
        QualitySettings.SetQualityLevel(QualityLevel, true);
        ApplyFrameRate();
    }

    public static void Save()
    {
        PlayerPrefs.Save();
        GameSettingsService.Instance?.Flush();
    }

    public static AudioMixer LoadMixer()
    {
        if (Mixer == null) Mixer = Resources.Load<AudioMixer>(MixerResourceName);
        return Mixer;
    }

    // Âm lượng tổng do GameSettingsService đặt vào AudioListener. Ở đây chỉ chỉnh Nhạc nền / Hiệu ứng.
    static void ApplyAudio()
    {
        AudioMixer mixer = LoadMixer();
        if (mixer == null) return;
        if (!string.IsNullOrEmpty(MusicParameter)) mixer.SetFloat(MusicParameter, ToDecibel(MusicVolume));
        if (!string.IsNullOrEmpty(SfxParameter)) mixer.SetFloat(SfxParameter, ToDecibel(SfxVolume));
    }

    static float ToDecibel(float volume) => Mathf.Log10(Mathf.Max(volume, 0.0001f)) * 20f;

    static void ApplyFrameRate()
    {
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = VSync ? -1 : FpsChoices[FpsIndex];
    }
}

// Bảng Cài đặt cho MainMenu. Gắn lên object Canvas của MainMenu, kéo OptionsButton vào ô Open Button.
// Bảng tự dựng khi chạy game. Muốn chỉnh màu/hình: gán Panel Sprite, Button Sprite, Font.
[DisallowMultipleComponent]
public class MainMenuOptionsPanel : MonoBehaviour
{
    [Header("Nối với Main Menu")]
    [Tooltip("Nút Options trong ButtonGroup")]
    [SerializeField] Button openButton;
    [Tooltip("(Tùy chọn) Ẩn object này khi mở Cài đặt, vd. ButtonGroup")]
    [SerializeField] GameObject hideWhileOpen;

    [Header("Âm thanh (tùy chọn)")]
    [Tooltip("AudioMixer có tham số exposed để chỉnh Nhạc nền / Hiệu ứng riêng")]
    [SerializeField] AudioMixer audioMixer;
    [SerializeField] string musicParameter = "MusicVolume";
    [SerializeField] string sfxParameter = "SfxVolume";

    [Header("Giao diện")]
    [Tooltip("Khung bảng. Để trống = khung tối viền vàng")]
    [SerializeField] Sprite panelSprite;
    [Tooltip("Hình nút (Mặc định / Đóng / mũi tên). Để trống = nút màu")]
    [SerializeField] Sprite buttonSprite;
    [Tooltip("Font chữ TextMeshPro. Để trống = font TMP mặc định")]
    [SerializeField] TMP_FontAsset font;
    [SerializeField] string title = "CÀI ĐẶT";
    [SerializeField] Color accent = new Color(1f, 0.84f, 0.35f);
    [SerializeField] Color textColor = new Color(0.97f, 0.93f, 0.84f);
    [SerializeField] Vector2 panelSize = new Vector2(1000f, 760f);

    RectTransform root;
    RectTransform window;
    CanvasGroup group;
    readonly List<Action> refreshers = new List<Action>();
    Coroutine fadeRoutine;
    bool isOpen;
    GameOptions.Snapshot openedWith;

    public bool IsOpen => isOpen;

    void Awake()
    {
        if (audioMixer != null) GameOptions.Mixer = audioMixer;
        GameOptions.MusicParameter = musicParameter;
        GameOptions.SfxParameter = sfxParameter;
        if (audioMixer != null) GameOptions.ApplyAll();
    }

    void Start()
    {
        Build();
        root.gameObject.SetActive(false);
        if (openButton != null) openButton.onClick.AddListener(Open);
        else Debug.LogWarning($"{name}: chưa gán Open Button (OptionsButton).", this);
    }

    void Update()
    {
        if (isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace))) Cancel();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        openedWith = GameOptions.Capture();
        foreach (Action refresh in refreshers) refresh();
        root.SetAsLastSibling();
        root.gameObject.SetActive(true);
        if (hideWhileOpen != null) hideWhileOpen.SetActive(false);
        Animate(true);
    }

    // Lưu: giữ thay đổi, lưu xuống máy rồi về Main Menu.
    public void SaveAndClose()
    {
        if (!isOpen) return;
        GameOptions.Save();
        Hide();
    }

    // Hủy (hoặc Esc): trả cài đặt về như lúc mở bảng rồi về Main Menu.
    public void Cancel()
    {
        if (!isOpen) return;
        GameOptions.Restore(openedWith);
        GameOptions.Save();
        Hide();
    }

    void Hide()
    {
        isOpen = false;
        openedWith = null;
        if (hideWhileOpen != null) hideWhileOpen.SetActive(true);
        if (openButton != null) openButton.Select();
        Animate(false);
    }

    void Animate(bool show)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(AnimateRoutine(show));
    }

    IEnumerator AnimateRoutine(bool show)
    {
        float from = group.alpha, to = show ? 1f : 0f;
        float t = 0f;
        const float time = 0.2f;
        group.interactable = show;
        group.blocksRaycasts = true;
        while (t < time)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / time);
            group.alpha = Mathf.Lerp(from, to, k);
            window.localScale = Vector3.one * Mathf.Lerp(show ? 0.92f : 1f, show ? 1f : 0.92f, k);
            yield return null;
        }
        group.alpha = to;
        window.localScale = Vector3.one;
        if (!show) root.gameObject.SetActive(false);
        fadeRoutine = null;
    }

    // ---------- Dựng giao diện ----------

    void Build()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;
        if (canvas == null)
        {
            var canvasObject = new GameObject("OptionsCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var newCanvas = canvasObject.GetComponent<Canvas>();
            newCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            newCanvas.sortingOrder = 500;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            parent = canvasObject.transform;
        }

        // Nền tối phủ toàn màn hình (bấm ra ngoài không làm gì, chặn click xuống menu).
        root = NewRect("OptionsPanel", parent);
        Stretch(root);
        var dim = root.gameObject.AddComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.65f);
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;

        window = NewRect("Window", root);
        window.sizeDelta = panelSize;
        var windowImage = window.gameObject.AddComponent<Image>();
        if (panelSprite != null)
        {
            windowImage.sprite = panelSprite;
            windowImage.type = panelSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else
        {
            windowImage.color = new Color(0.16f, 0.11f, 0.08f, 0.97f);
            var outline = window.gameObject.AddComponent<Outline>();
            outline.effectColor = accent;
            outline.effectDistance = new Vector2(4f, -4f);
        }

        TextMeshProUGUI heading = NewText("Title", window, title, 56, accent, TextAlignmentOptions.Center);
        heading.fontStyle = FontStyles.Bold;
        var headingRect = heading.rectTransform;
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(1f, 1f);
        headingRect.pivot = new Vector2(0.5f, 1f);
        headingRect.sizeDelta = new Vector2(0f, 90f);
        headingRect.anchoredPosition = new Vector2(0f, -30f);

        var list = NewRect("Rows", window);
        list.anchorMin = new Vector2(0f, 0f);
        list.anchorMax = new Vector2(1f, 1f);
        list.offsetMin = new Vector2(70f, 130f);
        list.offsetMax = new Vector2(-70f, -135f);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        AddSlider(list, "Âm lượng tổng", () => GameOptions.MasterVolume, v => GameOptions.MasterVolume = v);
        AddSlider(list, "Nhạc nền", () => GameOptions.MusicVolume, v => GameOptions.MusicVolume = v);
        AddSlider(list, "Hiệu ứng âm thanh", () => GameOptions.SfxVolume, v => GameOptions.SfxVolume = v);

        List<Vector2Int> resolutions = GameOptions.Resolutions();
        var resolutionNames = new string[resolutions.Count];
        for (int i = 0; i < resolutions.Count; i++) resolutionNames[i] = $"{resolutions[i].x} x {resolutions[i].y}";
        AddSelector(list, "Độ phân giải", resolutionNames, () => GameOptions.ResolutionIndex, i => GameOptions.ResolutionIndex = i);
        AddSelector(list, "Chế độ màn hình", new[] { "Toàn màn hình", "Cửa sổ" },
            () => GameOptions.ScreenModeIndex, i => GameOptions.ScreenModeIndex = i);
        AddSelector(list, "Chất lượng đồ họa", QualitySettings.names, () => GameOptions.QualityLevel, i => GameOptions.QualityLevel = i);
        AddSelector(list, "V-Sync", new[] { "Tắt", "Bật" }, () => GameOptions.VSync ? 1 : 0, i => GameOptions.VSync = i == 1);
        AddSelector(list, "Giới hạn FPS", new[] { "30", "60", "120", "144", "Không giới hạn" },
            () => GameOptions.FpsIndex, i => GameOptions.FpsIndex = i);

        // Nút dưới cùng: Mặc định | Hủy | Lưu.
        Button reset = NewButton(window, "Mặc định", new Vector2(230f, 70f), () =>
        {
            GameOptions.ResetToDefault();
            foreach (Action refresh in refreshers) refresh();
        });
        Place(reset.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(-260f, 55f));
        Button cancel = NewButton(window, "Hủy", new Vector2(230f, 70f), Cancel);
        Place(cancel.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0f, 55f));
        Button save = NewButton(window, "Lưu", new Vector2(230f, 70f), SaveAndClose);
        Place(save.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(260f, 55f));
    }

    RectTransform NewRow(RectTransform parent, string label)
    {
        RectTransform row = NewRect(label, parent);
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = 56f;

        TextMeshProUGUI text = NewText("Label", row, label, 32, textColor, TextAlignmentOptions.MidlineLeft);
        var rect = text.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0.45f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return row;
    }

    RectTransform ControlArea(RectTransform row)
    {
        RectTransform area = NewRect("Control", row);
        area.anchorMin = new Vector2(0.47f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.offsetMin = area.offsetMax = Vector2.zero;
        return area;
    }

    void AddSlider(RectTransform parent, string label, Func<float> get, Action<float> set)
    {
        RectTransform area = ControlArea(NewRow(parent, label));

        var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
        sliderObject.transform.SetParent(area, false);
        var sliderRect = (RectTransform)sliderObject.transform;
        sliderRect.anchorMin = new Vector2(0f, 0.5f);
        sliderRect.anchorMax = new Vector2(0.8f, 0.5f);
        sliderRect.sizeDelta = new Vector2(0f, 22f);
        sliderRect.anchoredPosition = Vector2.zero;

        var slider = sliderObject.GetComponent<Slider>();
        foreach (Image image in sliderObject.GetComponentsInChildren<Image>())
        {
            if (image.name == "Background") image.color = new Color(0f, 0f, 0f, 0.55f);
            else if (image.name == "Fill") image.color = accent;
            else if (image.name == "Handle") { image.color = Color.white; image.rectTransform.sizeDelta = new Vector2(30f, 0f); }
        }

        TextMeshProUGUI value = NewText("Value", area, "", 30, accent, TextAlignmentOptions.MidlineRight);
        var valueRect = value.rectTransform;
        valueRect.anchorMin = new Vector2(0.82f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.offsetMin = valueRect.offsetMax = Vector2.zero;

        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.onValueChanged.AddListener(v => { set(v); value.text = $"{Mathf.RoundToInt(v * 100f)}%"; });
        refreshers.Add(() =>
        {
            float current = get();
            slider.SetValueWithoutNotify(current);
            value.text = $"{Mathf.RoundToInt(current * 100f)}%";
        });
    }

    void AddSelector(RectTransform parent, string label, string[] options, Func<int> get, Action<int> set)
    {
        RectTransform area = ControlArea(NewRow(parent, label));
        if (options == null || options.Length == 0) options = new[] { "-" };

        TextMeshProUGUI value = NewText("Value", area, "", 30, accent, TextAlignmentOptions.Center);
        var valueRect = value.rectTransform;
        valueRect.anchorMin = new Vector2(0f, 0f);
        valueRect.anchorMax = new Vector2(1f, 1f);
        valueRect.offsetMin = new Vector2(64f, 0f);
        valueRect.offsetMax = new Vector2(-64f, 0f);

        int index = 0;
        void Show() => value.text = options[index];
        void Step(int direction)
        {
            index = (index + direction + options.Length) % options.Length;
            set(index);
            Show();
        }

        Button left = NewButton(area, "<", new Vector2(54f, 50f), () => Step(-1));
        Place(left.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(27f, 0f));
        Button right = NewButton(area, ">", new Vector2(54f, 50f), () => Step(1));
        Place(right.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(-27f, 0f));

        refreshers.Add(() => { index = Mathf.Clamp(get(), 0, options.Length - 1); Show(); });
    }

    Button NewButton(RectTransform parent, string text, Vector2 size, Action onClick)
    {
        RectTransform rect = NewRect(text, parent);
        rect.sizeDelta = size;
        var image = rect.gameObject.AddComponent<Image>();
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = buttonSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }
        else image.color = new Color(0.36f, 0.24f, 0.13f, 1f);

        var button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.9f, 0.65f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(() => onClick());

        TextMeshProUGUI label = NewText("Text", rect, text, size.y > 60f ? 32 : 30, textColor, TextAlignmentOptions.Center);
        label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);
        return button;
    }

    TextMeshProUGUI NewText(string objectName, RectTransform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        RectTransform rect = NewRect(objectName, parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = alignment;
        label.raycastTarget = false;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
    }
}