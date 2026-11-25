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
    [Tooltip("Scene gameplay mặc định nếu save chưa ghi scene (vd: DemoScene)")]
    public string loadGameSceneName = "DemoScene";
    [Tooltip("Nút Load Game: tự làm mờ khi chưa có save (để trống cũng được)")]
    public Button loadGameButton;
    [Tooltip("Bảng danh sách ô save (object có SaveSlotPanel)")]
    public SaveSlotPanel saveSlotPanel;
    public string noSaveMessage = "Chưa có dữ liệu lưu. Hãy chọn New Game.";

    [Header("Options")]
    public GameObject optionsPanel;

    bool newGameInProgress;
    string pendingNewGameSlot;
    bool confirmPressed;

    void Awake()
    {
        if (notificationText == null && notificationPanel != null)
            notificationText = notificationPanel.GetComponentInChildren<TextMeshProUGUI>(true);

        if (newGameConfirmPopup != null) newGameConfirmPopup.SetActive(false);
        if (notificationPanel != null) notificationPanel.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(false);
        RefreshLoadButton();
    }

    void RefreshLoadButton()
    {
        if (loadGameButton != null) loadGameButton.interactable = AnySave();
    }

    static bool AnySave()
    {
        foreach (var slot in SaveSlotSession.Slots) if (SaveSlotSession.Exists(slot)) return true;
        return false;
    }

    /// <summary>Gắn vào On Click() của nút NEW GAME: chỉ hiện popup hỏi lại.</summary>
    public void NewGame()
    {
        if (newGameInProgress) return;

        // Còn ô trống -> dùng ô đó. Hết ô -> mở bảng cho chọn ô để ghi đè.
        string empty = SaveSlotSession.FirstEmptySlot();
        if (empty == null && saveSlotPanel != null)
        {
            saveSlotPanel.Show(SaveSlotPanel.Mode.NewGame, slot => { pendingNewGameSlot = slot; ConfirmNewGame(); });
            return;
        }
        pendingNewGameSlot = empty ?? SaveSlotSession.Slots[0];

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

        // Nạp sẵn scene tạo nhân vật ngầm trong lúc đọc thông báo -> bấm Xác Nhận là chuyển ngay,
        // không lộ màn hình Main Menu ở giữa.
        AsyncOperation preload = null;
        if (!string.IsNullOrWhiteSpace(characterCreationSceneName)
            && Application.CanStreamedLevelBeLoaded(characterCreationSceneName))
        {
            preload = SceneManager.LoadSceneAsync(characterCreationSceneName);
            preload.allowSceneActivation = false;
        }

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

        if (preload == null)
        {
            if (notificationPanel != null) notificationPanel.SetActive(false);
            if (loadingPanel != null) loadingPanel.SetActive(false);
            Debug.LogWarning($"{name}: scene '{characterCreationSceneName}' chưa gán hoặc chưa có trong Build Profiles / Scene List.");
            newGameInProgress = false;
            yield break;
        }
        // New Game: chọn ô và đánh dấu xóa save cũ của ô đó khi vào game.
        SaveSlotSession.Current = string.IsNullOrEmpty(pendingNewGameSlot) ? SaveSlotSession.Slots[0] : pendingNewGameSlot;
        SaveSlotSession.RequestReset(SaveSlotSession.Current);
        // Giữ nguyên Loading + Thông báo trên màn hình cho tới khi scene mới hiện ra.
        preload.allowSceneActivation = true;
    }

    public void LoadGame()
    {
        bool anySave = AnySave();
        Debug.Log($"[MainMenu] Bấm Load Game. Có save: {anySave}, SaveSlotPanel: {(saveSlotPanel != null ? saveSlotPanel.name : "CHƯA GÁN")}, " +
                  $"thư mục save: {System.IO.Path.Combine(Application.persistentDataPath, "Saves")}");
        if (newGameInProgress) { Debug.LogWarning("[MainMenu] Đang chạy New Game nên bỏ qua Load Game."); return; }
        if (!anySave) { ShowNoSave(); return; }

        if (saveSlotPanel != null) saveSlotPanel.Show(SaveSlotPanel.Mode.Load, ContinueFromSlot);
        else ContinueFromSlot(SaveSlotSession.Exists(SaveSlotSession.Current) ? SaveSlotSession.Current : FirstUsedSlot());
    }

    static string FirstUsedSlot()
    {
        foreach (var slot in SaveSlotSession.Slots) if (SaveSlotSession.Exists(slot)) return slot;
        return SaveSlotSession.Slots[0];
    }

    void ContinueFromSlot(string slot)
    {
        RefreshLoadButton();
        if (!SaveSlotSession.Exists(slot)) { ShowNoSave(); return; }

        // Vào bằng Load Game thì không phải chơi mới: bỏ dấu xóa save (nếu còn sót) và dữ liệu tạo nhân vật.
        SaveSlotSession.Current = slot;
        SaveSlotSession.CancelReset(slot);
        CharacterSelectionData.Clear();
        CharacterSelectionData.PlayNewGameIntro = false;

        string scene = PlayerContinueState.HasSavedScene(slot, out string saved) ? saved : loadGameSceneName;
        if (!Application.CanStreamedLevelBeLoaded(scene)) scene = loadGameSceneName;
        if (string.IsNullOrWhiteSpace(scene) || !Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogWarning($"{name}: scene '{scene}' chưa có trong Build Profiles / Scene List.");
            return;
        }
        StartCoroutine(LoadGameFlow(scene));
    }

    IEnumerator LoadGameFlow(string scene)
    {
        newGameInProgress = true;
        if (buttonGroup != null) buttonGroup.SetActive(false);
        if (loadingPanel != null) loadingPanel.SetActive(true);
        if (loadingBar != null) { loadingBar.gameObject.SetActive(true); loadingBar.value = 0f; }
        if (loadingText != null) loadingText.gameObject.SetActive(true);

        var op = SceneManager.LoadSceneAsync(scene);
        while (!op.isDone)
        {
            float progress = Mathf.Clamp01(op.progress / 0.9f);
            if (loadingBar != null) loadingBar.value = progress;
            if (loadingText != null) loadingText.text = $"Loading... {Mathf.RoundToInt(progress * 100f)}%";
            yield return null;
        }
    }

    void ShowNoSave()
    {
        Debug.Log(noSaveMessage);
        if (notificationPanel == null || notificationText == null) return;
        StopCoroutine(nameof(HideNoSave));
        notificationText.text = noSaveMessage;
        if (notificationCanvasGroup != null) notificationCanvasGroup.alpha = 1f;
        notificationPanel.SetActive(true);
        StartCoroutine(nameof(HideNoSave));
    }

    IEnumerator HideNoSave()
    {
        yield return new WaitForSecondsRealtime(2f);
        if (!newGameInProgress && notificationPanel != null) notificationPanel.SetActive(false);
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