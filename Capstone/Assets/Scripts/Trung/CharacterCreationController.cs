using System.Collections;
using Capstone.Game.SettingsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class CharacterCreationController : MonoBehaviour
{
    // Khớp với quy định của PlayerProfileRuntimeProvider.TrySetDisplayName.
    const int MinNameLength = 2;
    const int MaxNameLength = 20;

    [Header("Khung đặt tên (ẩn tới khi chọn giới tính)")]
    public TMP_InputField nameInputField;

    [Header("Chọn giới tính")]
    public Button maleButton;
    public Button femaleButton;
    [Range(0f, 1f)] public float dimmedAlpha = 0.4f;

    [Header("Nút tạo nhân vật")]
    public Button createCharaterButton;
    public TextMeshProUGUI errorText;

    [Header("Popup xác nhận")]
    public GameObject confirmPopup;
    public TextMeshProUGUI confirmText;
    public Button acceptButton;
    public Button cancelButton;

    [Header("Scene tiếp theo")]
    public string gameplaySceneName = "DemoScene";

    [Header("Loading khi bấm Đồng ý")]
    [Tooltip("Object LoadingPanel (nền + LoadingBar + Loading Text)")]
    public GameObject loadingPanel;
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;
    [Tooltip("Thời gian hiện Loading tối thiểu (giây), để thanh chạy kịp nhìn thấy")]
    public float minLoadingTime = 1.5f;

    [Header("Âm thanh nút")]
    [Tooltip("Audio Source phát tiếng. Để trống = tự tạo, đi qua kênh UI của game")]
    public AudioSource uiSource;
    [Tooltip("Để trống = tiếng hover chung của game")]
    public AudioClip hoverClip;
    [Range(0f, 1f)] public float hoverVolume = 0.6f;
    [Tooltip("Để trống = tiếng bấm chung của game")]
    public AudioClip clickClip;
    [Range(0f, 1f)] public float clickVolume = 1f;

    string selectedGender;
    Image maleImage;
    Image femaleImage;
    bool loading;
    float nextHoverAllowed;

    void Awake()
    {
        if (maleButton != null) maleImage = maleButton.GetComponent<Image>();
        if (femaleButton != null) femaleImage = femaleButton.GetComponent<Image>();

        if (nameInputField != null)
        {
            nameInputField.characterLimit = MaxNameLength;
            nameInputField.gameObject.SetActive(false);
        }
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (errorText != null) errorText.gameObject.SetActive(false);

        if (maleButton != null) maleButton.onClick.AddListener(() => SelectGender(CharacterSelectionData.Male));
        if (femaleButton != null) femaleButton.onClick.AddListener(() => SelectGender(CharacterSelectionData.Female));
        if (createCharaterButton != null) createCharaterButton.onClick.AddListener(OnCreateClicked);
        if (acceptButton != null) acceptButton.onClick.AddListener(OnAcceptConfirm);
        if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelConfirm);

        if (loadingPanel != null) loadingPanel.SetActive(false);
        SetupSounds();
    }

    // ================= Âm thanh =================
    // Hình Nam / Nữ: chỉ tiếng bấm. Nút Tạo + Đồng ý / Hủy: tiếng bấm + tiếng rê chuột.
    void SetupSounds()
    {
        if (uiSource == null)
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
        }
        GameAudioRouting.Route(uiSource, GameAudioBus.UI);
        if (GameAudioRouting.Assets != null)
        {
            if (hoverClip == null) hoverClip = GameAudioRouting.Assets.hover;
            if (clickClip == null) clickClip = GameAudioRouting.Assets.click;
        }

        AddClick(maleButton);
        AddClick(femaleButton);
        foreach (var b in new[] { createCharaterButton, acceptButton, cancelButton })
        {
            AddClick(b);
            AddHover(b);
        }
    }

    void AddClick(Button button)
    {
        if (button != null) button.onClick.AddListener(() => PlaySound(clickClip, clickVolume));
    }

    void AddHover(Button button)
    {
        if (button == null) return;
        var trigger = button.GetComponent<EventTrigger>() ?? button.gameObject.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        entry.callback.AddListener(_ =>
        {
            if (!button.IsInteractable() || Time.unscaledTime < nextHoverAllowed) return;
            nextHoverAllowed = Time.unscaledTime + 0.06f;
            PlaySound(hoverClip, hoverVolume);
        });
        trigger.triggers.Add(entry);
    }

    void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null && uiSource != null && uiSource.isActiveAndEnabled) uiSource.PlayOneShot(clip, volume);
    }

    void SelectGender(string gender)
    {
        selectedGender = gender;

        SetImageAlpha(maleImage, gender == CharacterSelectionData.Male ? 1f : dimmedAlpha);
        SetImageAlpha(femaleImage, gender == CharacterSelectionData.Female ? 1f : dimmedAlpha);

        if (nameInputField != null) nameInputField.gameObject.SetActive(true);
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    void SetImageAlpha(Image img, float alpha)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = alpha;
        img.color = c;
    }

    void OnCreateClicked()
    {
        if (string.IsNullOrEmpty(selectedGender))
        {
            ShowError("Vui lòng chọn giới tính trước.");
            return;
        }

        string typedName = nameInputField != null ? nameInputField.text.Trim() : "";
        if (string.IsNullOrEmpty(typedName))
        {
            ShowError("Vui lòng nhập tên nhân vật.");
            return;
        }
        if (typedName.Length < MinNameLength || typedName.Length > MaxNameLength)
        {
            ShowError($"Tên phải có từ {MinNameLength} đến {MaxNameLength} ký tự.");
            return;
        }

        string genderLabel = selectedGender == CharacterSelectionData.Male ? "Nam" : "Nữ";
        if (confirmText != null)
            confirmText.text = $"Bạn muốn chọn nhân vật {genderLabel} tên \"{typedName}\"?";

        if (errorText != null) errorText.gameObject.SetActive(false);
        if (confirmPopup != null) confirmPopup.SetActive(true);
    }

    void ShowError(string msg)
    {
        if (errorText == null) return;
        errorText.text = msg;
        errorText.gameObject.SetActive(true);
    }

    void OnCancelConfirm()
    {
        if (confirmPopup != null) confirmPopup.SetActive(false);
        if (nameInputField != null) nameInputField.gameObject.SetActive(false);

        selectedGender = null;
        SetImageAlpha(maleImage, 1f);
        SetImageAlpha(femaleImage, 1f);
    }

    void OnAcceptConfirm()
    {
        if (loading) return;
        if (string.IsNullOrWhiteSpace(gameplaySceneName) || !Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogWarning($"{name}: scene '{gameplaySceneName}' chưa gán hoặc chưa có trong Build Profiles / Scene List.");
            return;
        }

        CharacterSelectionData.Gender = selectedGender;
        CharacterSelectionData.PlayerName = nameInputField != null ? nameInputField.text.Trim() : "";
        CharacterSelectionData.PlayNewGameIntro = true;   // DemoScene sẽ chạy đoạn mở đầu.
        StartCoroutine(LoadGameplay());
    }

    // ================= Loading =================
    IEnumerator LoadGameplay()
    {
        loading = true;
        if (confirmPopup != null) confirmPopup.SetActive(false);
        ShowLoadingPanel();

        // Đợi 1 khung hình cho Loading hiện lên rồi mới bắt đầu nạp scene.
        yield return null;
        var op = SceneManager.LoadSceneAsync(gameplaySceneName);
        op.allowSceneActivation = false;

        float start = Time.unscaledTime;
        float shown = 0f;
        while (true)
        {
            float real = Mathf.Clamp01(op.progress / 0.9f);
            float timed = minLoadingTime > 0f ? Mathf.Clamp01((Time.unscaledTime - start) / minLoadingTime) : 1f;
            shown = Mathf.Max(shown, Mathf.Min(real, timed));
            if (loadingBar != null) loadingBar.value = shown;
            if (loadingText != null) loadingText.text = $"Loading... {Mathf.RoundToInt(shown * 100f)}%";
            if (shown >= 1f) break;
            yield return null;
        }
        op.allowSceneActivation = true;   // Loading vẫn hiện tới khi DemoScene hiện ra.
    }

    void ShowLoadingPanel()
    {
        if (loadingPanel == null) return;
        // LoadingPanel nằm ngoài Canvas thì không hiện được -> đưa vào Canvas của màn tạo nhân vật.
        if (loadingPanel.GetComponentInParent<Canvas>(true) == null && createCharaterButton != null)
        {
            var canvas = createCharaterButton.GetComponentInParent<Canvas>(true);
            if (canvas != null) loadingPanel.transform.SetParent(canvas.rootCanvas.transform, false);
        }
        loadingPanel.transform.SetAsLastSibling();   // Đè lên mọi thứ.
        if (loadingBar != null) { loadingBar.gameObject.SetActive(true); loadingBar.value = 0f; loadingBar.interactable = false; }
        if (loadingText != null) { loadingText.gameObject.SetActive(true); loadingText.text = "Loading... 0%"; }
        loadingPanel.SetActive(true);
    }
}

public static class CharacterSelectionData
{
    public const string Male = "Male";
    public const string Female = "Female";

    public static string Gender;
    public static string PlayerName;

    // Bật khi vừa tạo nhân vật xong. NewGameIntroSequence đọc rồi tự tắt.
    // Không bị Clear() xóa, để Load Game không chạy lại đoạn mở đầu.
    public static bool PlayNewGameIntro;

    public static bool HasPendingSelection => !string.IsNullOrEmpty(Gender);

    public static void Clear()
    {
        Gender = null;
        PlayerName = null;
    }
}