using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Xử lý các nút ở màn Main Menu: NEW GAME, LOAD GAME, OPTIONS, QUIT.
///
/// Luồng NEW GAME:
/// 1) Ẩn ButtonGroup + ClickAnywhereText
/// 2) Hiện LoadingPanel (nền), hiện LoadingBar + Loading Text, chạy thanh loading GIẢ LẬP
///    từ 0 -> 100% trong fakeLoadingDuration giây (tăng số này trong Inspector nếu muốn chạy chậm hơn)
/// 3) Chạy đầy xong -> ẨN riêng LoadingBar + Loading Text (KHÔNG tắt LoadingPanel)
/// 4) Hiện NotificationPanel ĐÈ LÊN TRÊN nền LoadingPanel (đoạn chữ dẫn nhập), chờ bấm nút
///    "Xác Nhận" (ConfirmButton)
/// 5) Tắt NotificationPanel + LoadingPanel, load THẬT SỰ sang Scene tạo nhân vật
///
/// Gắn vào 1 GameObject trong scene MainMenu (VD: "MainMenuSystem").
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Tham chiếu UI có sẵn trong Main Menu")]
    [Tooltip("Nhóm nút NEW GAME/LOAD GAME/OPTIONS/QUIT — sẽ bị ẩn đi khi bấm NEW GAME.")]
    public GameObject buttonGroup;
    [Tooltip("Chữ '~CLICK ANYWHERE TO START~' — sẽ bị ẩn cùng lúc với buttonGroup. Để trống nếu không cần ẩn.")]
    public GameObject clickAnywhereText;

    [Header("Panel Loading (giả lập, chạy trước Notification)")]
    [Tooltip("Panel nền — sẽ KHÔNG bị tắt khi NotificationPanel hiện lên, chỉ tắt hẳn ở bước cuối trước khi chuyển scene.")]
    public GameObject loadingPanel;
    [Tooltip("Slider thanh loading (đã bỏ Interactable) bên trong loadingPanel.")]
    public Slider loadingBar;
    [Tooltip("Text (TMP) hiện % loading, VD 'Progress Text'.")]
    public TextMeshProUGUI loadingText;
    [Tooltip("Thời gian (giây) để thanh loading giả lập chạy từ 0 lên 100%. Tăng số này nếu muốn chạy chậm hơn.")]
    public float fakeLoadingDuration = 3f;

    [Header("Panel Thông Báo (dẫn nhập)")]
    [Tooltip("Panel chứa đoạn chữ dẫn nhập + nút Xác Nhận. Để sẵn SetActive = false trong scene.")]
    public GameObject notificationPanel;
    [Tooltip("BẮT BUỘC có Canvas Group trên notificationPanel để fade-in mượt được.")]
    public CanvasGroup notificationCanvasGroup;
    [Tooltip("Text (TMP) hiện nội dung bên dưới. Để trống sẽ tự tìm trong con của notificationPanel.")]
    public TextMeshProUGUI notificationText;
    [TextArea(2, 5)]
    public string notificationMessage =
        "Vùng đất Fantasy rộng lớn, nơi sinh vật huyền bí vẫn còn ẩn mình chờ được khám phá.\n" +
        "Hành trình của một nhà thuần hóa mới... bắt đầu từ đây.";
    [Tooltip("Thời gian (giây) để NotificationPanel mờ dần hiện lên (fade-in).")]
    public float notificationFadeInDuration = 0.6f;
    [Tooltip("Thời gian tối thiểu (giây) giữ Panel thông báo trước khi cho phép bấm Xác Nhận (tính từ lúc BẮT ĐẦU fade-in).")]
    public float notificationMinDisplayTime = 1f;

    [Header("New Game -> Scene tạo nhân vật")]
    [Tooltip("Tên Scene tạo nhân vật (chọn giới tính + đặt tên). PHẢI có trong Build Settings > Scene List.")]
    public string characterCreationSceneName = "CharacterCreationScene";

    [Header("Load Game")]
    public string loadGameSceneName = "GameplayScene";

    [Header("Options")]
    public GameObject optionsPanel;

    bool newGameInProgress;
    bool confirmPressed;

    void Awake()
    {
        if (notificationText == null && notificationPanel != null)
            notificationText = notificationPanel.GetComponentInChildren<TextMeshProUGUI>(true);

        if (notificationPanel != null) notificationPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    /// <summary>Gọi khi bấm nút NEW GAME.</summary>
    public void NewGame()
    {
        if (newGameInProgress) return;
        newGameInProgress = true;
        StartCoroutine(NewGameFlow());
    }

    /// <summary>Gắn vào On Click() của nút "Xác Nhận" (ConfirmButton) trong NotificationPanel.</summary>
    public void ConfirmNotification()
    {
        confirmPressed = true;
    }

    IEnumerator NewGameFlow()
    {
        // 1) Ẩn nhóm nút + chữ click anywhere
        if (buttonGroup != null) buttonGroup.SetActive(false);
        if (clickAnywhereText != null) clickAnywhereText.SetActive(false);

        // 2) Hiện LoadingPanel (nền) + LoadingBar + Loading Text, chạy thanh loading giả lập 0 -> 100%
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingBar != null) { loadingBar.gameObject.SetActive(true); loadingBar.value = 0f; }
        if (loadingText != null) loadingText.gameObject.SetActive(true);

        float t = 0f;
        while (t < fakeLoadingDuration)
        {
            t += Time.deltaTime;
            float progress = Mathf.Clamp01(t / fakeLoadingDuration);
            if (loadingBar != null) loadingBar.value = progress;
            if (loadingText != null) loadingText.text = $"Loading... {Mathf.RoundToInt(progress * 100f)}%";
            yield return null;
        }
        if (loadingBar != null) loadingBar.value = 1f;
        if (loadingText != null) loadingText.text = "Loading... 100%";

        // 3) Chạy đầy xong -> chỉ ẩn riêng LoadingBar + Loading Text, GIỮ NGUYÊN nền LoadingPanel
        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        if (loadingText != null) loadingText.gameObject.SetActive(false);

        // 4) Hiện NotificationPanel ĐÈ LÊN nền LoadingPanel, chờ bấm nút "Xác Nhận"
        confirmPressed = false;
        if (notificationPanel != null)
        {
            if (notificationText != null) notificationText.text = notificationMessage;
            if (notificationCanvasGroup != null) notificationCanvasGroup.alpha = 0f;
            notificationPanel.SetActive(true);
        }

        // Fade-in mượt từ alpha 0 -> 1
        if (notificationCanvasGroup != null)
        {
            float fadeT = 0f;
            while (fadeT < notificationFadeInDuration)
            {
                fadeT += Time.deltaTime;
                notificationCanvasGroup.alpha = Mathf.Clamp01(fadeT / notificationFadeInDuration);
                yield return null;
            }
            notificationCanvasGroup.alpha = 1f;
        }

        float shownAt = Time.unscaledTime;
        while (Time.unscaledTime - shownAt < notificationMinDisplayTime)
            yield return null;

        while (!confirmPressed)
            yield return null;

        if (notificationPanel != null) notificationPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);

        // 5) Load THẬT SỰ sang Scene tạo nhân vật
        if (string.IsNullOrWhiteSpace(characterCreationSceneName))
        {
            Debug.LogWarning($"{name}: chưa gán Character Creation Scene Name.");
            newGameInProgress = false;
            yield break;
        }
        SceneManager.LoadScene(characterCreationSceneName);
    }

    /// <summary>Gọi khi bấm nút LOAD GAME.</summary>
    public void LoadGame()
    {
        if (string.IsNullOrWhiteSpace(loadGameSceneName))
        {
            Debug.LogWarning($"{name}: chưa gán Load Game Scene Name.");
            return;
        }
        SceneManager.LoadScene(loadGameSceneName);
    }

    /// <summary>Gọi khi bấm nút OPTIONS.</summary>
    public void Options()
    {
        if (optionsPanel != null) optionsPanel.SetActive(true);
        else Debug.LogWarning($"{name}: chưa gán Options Panel, chưa làm màn Options.");
    }

    /// <summary>Gọi khi bấm nút QUIT.</summary>
    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}