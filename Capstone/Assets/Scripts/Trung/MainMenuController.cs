using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Xử lý các nút ở màn Main Menu: NEW GAME, LOAD GAME, OPTIONS, QUIT.
///
/// Luồng NEW GAME:
/// 0) Bấm NEW GAME -> hiện popup hỏi "Bạn muốn bắt đầu hành trình mới không?" (Xác Nhận / Hủy)
///    - Hủy: tắt popup, quay lại menu
///    - Xác Nhận: chạy tiếp các bước dưới
/// 1) Ẩn ButtonGroup + ClickAnywhereText
/// 2) Hiện LoadingPanel (nền), hiện LoadingBar + Loading Text, chạy thanh loading GIẢ LẬP
/// 3) Chạy đầy xong -> ẨN riêng LoadingBar + Loading Text (KHÔNG tắt LoadingPanel)
/// 4) Hiện NotificationPanel ĐÈ LÊN nền LoadingPanel (fade-in), chờ bấm nút "Xác Nhận"
/// 5) Tắt NotificationPanel + LoadingPanel, load sang Scene tạo nhân vật
///
/// Gắn vào 1 GameObject trong scene MainMenu (VD: "MainMenuSystem").
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Tham chiếu UI có sẵn trong Main Menu")]
    public GameObject buttonGroup;
    public GameObject clickAnywhereText;

    [Header("Popup hỏi lại khi bấm NEW GAME")]
    [Tooltip("Panel popup 'Bạn muốn bắt đầu hành trình mới không?' có 2 nút Xác Nhận / Hủy. Để sẵn SetActive = false.")]
    public GameObject newGameConfirmPopup;

    [Header("Panel Loading (giả lập, chạy trước Notification)")]
    public GameObject loadingPanel;
    public Slider loadingBar;
    public TextMeshProUGUI loadingText;
    public float fakeLoadingDuration = 3f;

    [Header("Panel Thông Báo (dẫn nhập)")]
    public GameObject notificationPanel;
    public CanvasGroup notificationCanvasGroup;
    public TextMeshProUGUI notificationText;
    [TextArea(2, 5)]
    public string notificationMessage =
        "Vùng đất Fantasy rộng lớn, nơi sinh vật huyền bí vẫn còn ẩn mình chờ được khám phá.\n" +
        "Hành trình của một nhà thuần hóa mới... bắt đầu từ đây.";
    public float notificationFadeInDuration = 0.6f;
    public float notificationMinDisplayTime = 1f;

    [Header("New Game -> Scene tạo nhân vật")]
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

        if (newGameConfirmPopup != null) newGameConfirmPopup.SetActive(false);
        if (notificationPanel != null) notificationPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
    }

    /// <summary>Gắn vào On Click() của nút NEW GAME: chỉ hiện popup hỏi lại.</summary>
    public void NewGame()
    {
        if (newGameInProgress) return;

        if (newGameConfirmPopup != null)
            newGameConfirmPopup.SetActive(true);
        else
            ConfirmNewGame(); // chưa gán popup thì chạy thẳng như cũ
    }

    /// <summary>Gắn vào On Click() của nút "Xác Nhận" trong popup hỏi lại.</summary>
    public void ConfirmNewGame()
    {
        if (newGameInProgress) return;
        newGameInProgress = true;

        if (newGameConfirmPopup != null) newGameConfirmPopup.SetActive(false);
        StartCoroutine(NewGameFlow());
    }

    /// <summary>Gắn vào On Click() của nút "Hủy" trong popup hỏi lại.</summary>
    public void CancelNewGame()
    {
        if (newGameConfirmPopup != null) newGameConfirmPopup.SetActive(false);
    }

    /// <summary>Gắn vào On Click() của nút "Xác Nhận" (ConfirmButton) trong NotificationPanel.</summary>
    public void ConfirmNotification()
    {
        confirmPressed = true;
    }

    IEnumerator NewGameFlow()
    {
        if (buttonGroup != null) buttonGroup.SetActive(false);
        if (clickAnywhereText != null) clickAnywhereText.SetActive(false);

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

        if (loadingBar != null) loadingBar.gameObject.SetActive(false);
        if (loadingText != null) loadingText.gameObject.SetActive(false);

        confirmPressed = false;
        if (notificationPanel != null)
        {
            if (notificationText != null) notificationText.text = notificationMessage;
            if (notificationCanvasGroup != null) notificationCanvasGroup.alpha = 0f;
            notificationPanel.SetActive(true);
        }

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

        if (string.IsNullOrWhiteSpace(characterCreationSceneName))
        {
            Debug.LogWarning($"{name}: chưa gán Character Creation Scene Name.");
            newGameInProgress = false;
            yield break;
        }
        SceneManager.LoadScene(characterCreationSceneName);
    }

    public void LoadGame()
    {
        if (string.IsNullOrWhiteSpace(loadGameSceneName))
        {
            Debug.LogWarning($"{name}: chưa gán Load Game Scene Name.");
            return;
        }
        SceneManager.LoadScene(loadGameSceneName);
    }

    public void Options()
    {
        if (optionsPanel != null) optionsPanel.SetActive(true);
        else Debug.LogWarning($"{name}: chưa gán Options Panel, chưa làm màn Options.");
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}