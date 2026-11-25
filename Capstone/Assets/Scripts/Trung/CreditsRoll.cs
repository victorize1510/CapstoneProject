using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using PhysicalKeys = Capstone.Game.UISystem.GameplayKeyboard;

// Scene Ending: chữ credit chạy từ dưới lên trên nền ảnh / video, có nhạc.
// Tự dựng giao diện, không cần tạo Canvas. Gắn lên 1 object rỗng trong scene "EndingScene".
//   - Giữ Space / chuột trái: chạy nhanh x4
//   - Esc: bỏ qua, về Main Menu
//   - Chạy hết: hiện "Cảm ơn đã chơi!" rồi về Main Menu
public class CreditsRoll : MonoBehaviour
{
    [Serializable]
    public class Section
    {
        [Tooltip("Tiêu đề mục (vd. LẬP TRÌNH GAMEPLAY). Để trống = không có tiêu đề")]
        public string title;
        [Tooltip("Mỗi dòng 1 người / 1 việc")]
        [TextArea(2, 8)] public string lines;
        [Tooltip("Ảnh minh họa cho mục này (ảnh chụp game, logo...) - tùy chọn")]
        public Sprite image;
    }

    [Header("Nội dung")]
    [SerializeField] string gameTitle = "BEAST OF FANTASY";
    [SerializeField] string subtitle = "Capstone Project - VTC Academy";
    [SerializeField] Sprite logo;
    [SerializeField] List<Section> sections = new List<Section>
    {
        new Section { title = "NHÓM PHÁT TRIỂN", lines = "Lưu Quốc Trung\nThắng" },
        new Section { title = "THIẾT KẾ GAME", lines = "Lưu Quốc Trung\nThắng" },
        new Section { title = "LẬP TRÌNH HỆ THỐNG GAMEPLAY", lines = "Thắng\nChiến đấu thời gian thực, bắt thú, đội hình, kỹ năng\nNhiệm vụ, bản đồ, cửa hàng, lưu game" },
        new Section { title = "LẬP TRÌNH CỐT TRUYỆN & GIAO DIỆN", lines = "Lưu Quốc Trung\nMain Menu, tạo nhân vật, đoạn mở đầu\nCutscene trưởng làng, chọn thú khởi đầu\nTiến hóa, ô lưu game, âm thanh & nhạc" },
        new Section { title = "BOSS", lines = "Lưu Quốc Trung\nDetale Cổ Đại - AI, kỹ năng, hiệu ứng" },
        new Section { title = "ĐỒ HỌA & HIỆU ỨNG", lines = "(Điền tên gói asset / nguồn đã dùng)" },
        new Section { title = "ÂM NHẠC & ÂM THANH", lines = "(Điền tên bài nhạc / nguồn đã dùng)" },
        new Section { title = "CÔNG CỤ", lines = "Unity 6.3 LTS (URP)\nUnity Version Control" },
        new Section { title = "GIẢNG VIÊN HƯỚNG DẪN", lines = "(Điền tên thầy / cô)" },
        new Section { title = "CẢM ƠN ĐẶC BIỆT", lines = "VTC Academy\nGia đình và bạn bè đã chơi thử" },
    };
    [SerializeField] string thankYouText = "Cảm ơn bạn đã chơi!";

    [Header("Giao diện")]
    [Tooltip("Font TextMeshPro có dấu tiếng Việt")]
    [SerializeField] TMP_FontAsset font;
    [Tooltip("Ảnh nền (để trống = nền đen)")]
    [SerializeField] Sprite background;
    [SerializeField, Range(0f, 1f)] float backgroundDim = 0.55f;
    [SerializeField] Color titleColor = new Color(1f, 0.82f, 0.35f);
    [SerializeField] Color textColor = Color.white;

    [Header("Tốc độ")]
    [Tooltip("Tốc độ chạy chữ (pixel / giây, theo màn 1920x1080)")]
    [SerializeField, Min(10f)] float scrollSpeed = 90f;
    [SerializeField, Min(1f)] float fastForward = 4f;
    [SerializeField, Min(0f)] float thankYouTime = 4f;

    [Header("Nhạc")]
    [SerializeField] AudioClip music;
    [SerializeField, Range(0f, 1f)] float musicVolume = 0.7f;

    [Header("Xong")]
    [SerializeField] string nextScene = "MainMenu";

    RectTransform content;
    CanvasGroup fader;
    TextMeshProUGUI thankYou;
    bool leaving;

    /// <summary>Gọi từ chỗ khác để chuyển sang scene Ending (vd. On Defeated của boss).</summary>
    public static void PlayEnding(string endingScene = "EndingScene") => SceneManager.LoadScene(endingScene);

    void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;
        Build();
        if (music != null) MusicManager.Push(this, music, musicVolume);
        StartCoroutine(Run());
    }

    void OnDisable() => MusicManager.Release(this);

    IEnumerator Run()
    {
        yield return Fade(1f, 0f, 1.2f);   // Sáng dần.
        Canvas.ForceUpdateCanvases();
        float endY = content.rect.height + 1080f;
        while (!leaving && content.anchoredPosition.y < endY)
        {
            bool fast = PhysicalKeys.GetKey(KeyCode.Space) || Input.GetMouseButton(0);
            content.anchoredPosition += Vector2.up * scrollSpeed * (fast ? fastForward : 1f) * Time.unscaledDeltaTime;
            if (PhysicalKeys.GetKeyDown(KeyCode.Escape)) break;
            yield return null;
        }

        // Cảm ơn đã chơi.
        if (!leaving && thankYouTime > 0f)
        {
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime) { thankYou.alpha = t; yield return null; }
            thankYou.alpha = 1f;
            float until = Time.unscaledTime + thankYouTime;
            while (Time.unscaledTime < until && !PhysicalKeys.GetKeyDown(KeyCode.Escape)) yield return null;
        }
        yield return Leave();
    }

    IEnumerator Leave()
    {
        if (leaving) yield break;
        leaving = true;
        yield return Fade(0f, 1f, 1.2f);
        MusicManager.Release(this);
        if (!string.IsNullOrWhiteSpace(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene)) SceneManager.LoadScene(nextScene);
        else Debug.LogError($"{name}: scene '{nextScene}' chưa có trong Build Profiles / Scene List.", this);
    }

    IEnumerator Fade(float from, float to, float time)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime) { fader.alpha = Mathf.Lerp(from, to, t / time); yield return null; }
        fader.alpha = to;
    }

    // ---------- Dựng giao diện ----------

    void Build()
    {
        var canvasGo = new GameObject("CreditsCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        // Nền
        var bg = NewImage("Background", root, background, background != null ? Color.white : Color.black);
        Stretch(bg.rectTransform);
        bg.preserveAspect = false;
        if (background != null) { var dim = NewImage("Dim", root, null, new Color(0f, 0f, 0f, backgroundDim)); Stretch(dim.rectTransform); }

        // Khung chạy chữ (bắt đầu dưới mép màn hình)
        var viewport = NewRect("Viewport", root);
        Stretch(viewport);
        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0.5f, 0f);
        content.anchorMax = new Vector2(0.5f, 0f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(1400f, 0f);
        content.anchoredPosition = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 26f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (logo != null) AddImage(logo, 260f);
        AddText(gameTitle, 96, titleColor, FontStyles.Bold);
        if (!string.IsNullOrWhiteSpace(subtitle)) AddText(subtitle, 36, textColor, FontStyles.Italic);
        AddSpace(160f);
        foreach (var section in sections)
        {
            if (section == null) continue;
            if (!string.IsNullOrWhiteSpace(section.title)) AddText(section.title, 44, titleColor, FontStyles.Bold);
            if (section.image != null) AddImage(section.image, 360f);
            if (!string.IsNullOrWhiteSpace(section.lines)) AddText(section.lines, 36, textColor, FontStyles.Normal);
            AddSpace(110f);
        }

        // "Cảm ơn đã chơi" ở giữa màn hình, hiện sau cùng
        thankYou = NewText("ThankYou", root, thankYouText, 72, titleColor, FontStyles.Bold);
        Stretch(thankYou.rectTransform);
        thankYou.alignment = TextAlignmentOptions.Center;
        thankYou.alpha = 0f;

        var hint = NewText("Hint", root, "Giữ Space: tua nhanh     Esc: bỏ qua", 24, new Color(1f, 1f, 1f, 0.5f), FontStyles.Normal);
        hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 0f);
        hint.rectTransform.pivot = new Vector2(1f, 0f);
        hint.rectTransform.anchoredPosition = new Vector2(-40f, 30f);
        hint.rectTransform.sizeDelta = new Vector2(700f, 40f);
        hint.alignment = TextAlignmentOptions.BottomRight;

        // Màn đen để sáng dần / tối dần
        var black = NewImage("Fader", root, null, Color.black);
        Stretch(black.rectTransform);
        fader = black.gameObject.AddComponent<CanvasGroup>();
        fader.alpha = 1f;
        fader.blocksRaycasts = false;
    }

    void AddText(string text, float size, Color color, FontStyles style)
    {
        var label = NewText("Line", content, text, size, color, style);
        label.alignment = TextAlignmentOptions.Top;
        label.textWrappingMode = TextWrappingModes.Normal;
    }

    void AddImage(Sprite sprite, float height)
    {
        var image = NewImage("Image", content, sprite, Color.white);
        image.preserveAspect = true;
        var element = image.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
    }

    void AddSpace(float height)
    {
        var space = NewRect("Space", content);
        space.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
    }

    TextMeshProUGUI NewText(string objectName, RectTransform parent, string text, float size, Color color, FontStyles style)
    {
        var label = NewRect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.fontStyle = style;
        label.raycastTarget = false;
        return label;
    }

    static Image NewImage(string objectName, RectTransform parent, Sprite sprite, Color color)
    {
        var image = NewRect(objectName, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}