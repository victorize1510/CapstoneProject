using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Video;
using PhysicalKeys = Capstone.Game.UISystem.GameplayKeyboard;

// Phát clip intro, hết clip -> sang scene tiếp theo.
// Có nút "Bỏ qua" ở góc dưới bên phải (tự dựng, không cần tạo trong Hierarchy) + phím Esc / Space / Enter để bỏ qua.
public class IntroScene : MonoBehaviour
{
    public VideoPlayer videoPlayer;
    public string nextScene = "LoadingSceneToMainMenu";

    [Header("Nút Bỏ qua")]
    [SerializeField] bool showSkipButton = true;
    [SerializeField] string skipText = "Bỏ qua  ▶▶";
    [Tooltip("Font TextMeshPro có dấu tiếng Việt. Để trống = font mặc định")]
    [SerializeField] TMP_FontAsset font;
    [Tooltip("Sau bao nhiêu giây thì hiện nút (tránh bấm nhầm ngay lúc vừa vào)")]
    [SerializeField, Min(0f)] float showButtonAfter = 1f;
    [Tooltip("Esc / Space / Enter cũng bỏ qua được")]
    [SerializeField] bool allowKeySkip = true;
    [SerializeField] Color buttonColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] Color textColor = new Color(1f, 1f, 1f, 0.95f);

    bool leaving;
    float startTime;
    CanvasGroup buttonGroup;
    AudioSource uiAudio;

    void Start()
    {
        startTime = Time.unscaledTime;
        if (videoPlayer != null) videoPlayer.loopPointReached += OnVideoEnd;   // Hết clip -> sang scene tiếp.
        else Debug.LogWarning($"{name}: chưa gắn Video Player.", this);
        if (showSkipButton) BuildSkipButton();
    }

    void OnDestroy()
    {
        if (videoPlayer != null) videoPlayer.loopPointReached -= OnVideoEnd;
    }

    void Update()
    {
        if (leaving) return;
        float shownFor = Time.unscaledTime - startTime - showButtonAfter;
        if (buttonGroup != null && shownFor > 0f)
        {
            buttonGroup.alpha = Mathf.Clamp01(shownFor / 0.4f);
            buttonGroup.interactable = buttonGroup.blocksRaycasts = true;
        }
        if (allowKeySkip && shownFor > 0f &&
            (PhysicalKeys.GetKeyDown(KeyCode.Escape) || PhysicalKeys.GetKeyDown(KeyCode.Space) || PhysicalKeys.GetKeyDown(KeyCode.Return)))
            SkipIntro();
    }

    void OnVideoEnd(VideoPlayer vp) => GoNext();

    /// <summary>Gắn được vào On Click của nút tự làm.</summary>
    public void SkipIntro()
    {
        PlayUi(false);
        GoNext();
    }

    void GoNext()
    {
        if (leaving) return;
        leaving = true;
        if (videoPlayer != null) videoPlayer.Stop();
        if (string.IsNullOrWhiteSpace(nextScene) || !Application.CanStreamedLevelBeLoaded(nextScene))
        {
            Debug.LogError($"{name}: scene '{nextScene}' chưa gắn hoặc chưa có trong Build Profiles / Scene List.", this);
            leaving = false;
            return;
        }
        SceneManager.LoadScene(nextScene);
    }

    // ---------- Nút Bỏ qua (tự dựng) ----------

    void BuildSkipButton()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

        var canvasGo = new GameObject("SkipCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var buttonGo = new GameObject("SkipButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(CanvasGroup));
        buttonGo.transform.SetParent(canvasGo.transform, false);
        var rect = (RectTransform)buttonGo.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-50f, 50f);
        rect.sizeDelta = new Vector2(260f, 70f);
        buttonGo.GetComponent<Image>().color = buttonColor;
        var outline = buttonGo.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 1f, 1f, 0.5f);
        outline.effectDistance = new Vector2(2f, -2f);

        var button = buttonGo.GetComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.85f, 0.4f, 1f);
        colors.pressedColor = new Color(0.8f, 0.7f, 0.4f, 1f);
        button.colors = colors;
        button.onClick.AddListener(SkipIntro);

        var hover = buttonGo.AddComponent<EventTrigger>();
        var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ => PlayUi(true));
        hover.triggers.Add(enter);

        var textGo = new GameObject("Text", typeof(RectTransform));
        textGo.transform.SetParent(buttonGo.transform, false);
        var textRect = (RectTransform)textGo.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var label = textGo.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.text = skipText;
        label.fontSize = 32;
        label.fontStyle = FontStyles.Bold;
        label.color = textColor;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;

        buttonGroup = buttonGo.GetComponent<CanvasGroup>();
        buttonGroup.alpha = 0f;
        buttonGroup.interactable = buttonGroup.blocksRaycasts = false;
    }

    void PlayUi(bool hover)
    {
        var assets = Capstone.Game.SettingsSystem.GameAudioRouting.Assets;
        AudioClip clip = assets == null ? null : hover ? assets.hover : assets.click;
        if (clip == null) return;
        if (uiAudio == null)
        {
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            Capstone.Game.SettingsSystem.GameAudioRouting.Route(uiAudio, Capstone.Game.SettingsSystem.GameAudioBus.UI);
        }
        uiAudio.PlayOneShot(clip);
    }
}