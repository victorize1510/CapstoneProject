using System.Collections;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

// Gắn vào Player trong scene gameplay (DemoScene...).
// Save của game chưa lưu vị trí, scene và nhân vật nam/nữ. Script này lưu thêm mấy thứ đó
// mỗi khi game lưu xong (SaveCompleted), và trả lại khi Load Game (LoadCompleted).
[DefaultExecutionOrder(-5000)]   // Chạy trước PlayerSaveController để kịp gán ô save.
[DisallowMultipleComponent]
public class PlayerContinueState : MonoBehaviour
{
    [Tooltip("Chỉ dùng khi bấm Play thẳng ở scene này. Vào từ Main Menu thì lấy ô đã chọn.")]
    [SerializeField] bool useLastChosenSlot = true;
    [SerializeField] string slotId = "main";
    [Tooltip("Để trống = tự tìm BasicPlayerMovement")]
    [SerializeField] Transform player;
    [SerializeField] PlayerVisualSwitcher visualSwitcher;

    [Header("Tự động lưu")]
    [Tooltip("Lưu khi thoát game / Alt-Tab ra ngoài")]
    [SerializeField] bool saveOnQuitAndPause = true;
    [Tooltip("Tự lưu mỗi X giây. 0 = tắt")]
    [SerializeField] float autoSaveInterval = 60f;

    [SerializeField] bool logSteps = true;

    const string Tag = "[Continue]";
    PlayerSaveController save;
    bool applied;
    float nextAutoSave;

    // ===== Dùng chung cho Main Menu =====
    static string Key(string slot, string field) => $"continue.{slot}.{field}";

    public static bool HasSavedScene(string slot, out string scene)
    {
        scene = PlayerPrefs.GetString(Key(slot, "scene"), "");
        return !string.IsNullOrEmpty(scene);
    }

    public static void Clear(string slot = "main")
    {
        foreach (var f in new[] { "scene", "px", "py", "pz", "ry", "visual" })
            PlayerPrefs.DeleteKey(Key(slot, f));
        PlayerPrefs.Save();
    }

    // ===== Gắn vào scene =====
    void Awake()
    {
        if (useLastChosenSlot) slotId = SaveSlotSession.Current;
        SaveSlotSession.ApplyToScene(slotId);
        Log($"Đang dùng {SaveSlotSession.DisplayName(slotId)} ({slotId}).");

        if (player == null)
        {
            var move = FindFirstObjectByType<BasicPlayerMovement>();
            player = move != null ? move.transform : transform;
        }
        if (visualSwitcher == null) visualSwitcher = player.GetComponentInChildren<PlayerVisualSwitcher>(true);
        if (visualSwitcher == null) visualSwitcher = FindFirstObjectByType<PlayerVisualSwitcher>();
    }

    void OnEnable()
    {
        save = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
        if (save == null) { Debug.LogWarning($"{Tag} Không thấy PlayerSaveController trong scene."); return; }
        save.SaveCompleted += OnSaved;
        save.LoadCompleted += OnLoaded;
    }

    void OnDisable()
    {
        if (save == null) return;
        save.SaveCompleted -= OnSaved;
        save.LoadCompleted -= OnLoaded;
    }

    void Start()
    {
        nextAutoSave = Time.unscaledTime + autoSaveInterval;
        // Phòng khi save đã nạp trước khi script này kịp đăng ký.
        if (save != null && save.IsCurrentSlotLoaded) StartCoroutine(ApplyNextFrame());
        else StartCoroutine(AllowSaveAfterDelay());
    }

    // New Game / chưa có save: không có LoadCompleted -> sau 3 giây vẫn cho tự lưu.
    IEnumerator AllowSaveAfterDelay()
    {
        yield return new WaitForSecondsRealtime(3f);
        applied = true;
    }

    void Update()
    {
        if (autoSaveInterval <= 0f || Time.unscaledTime < nextAutoSave) return;
        nextAutoSave = Time.unscaledTime + autoSaveInterval;
        SaveGame();
    }

    void OnApplicationPause(bool paused) { if (paused && saveOnQuitAndPause) SaveGame(); }
    void OnApplicationQuit() { if (saveOnQuitAndPause) SaveGame(); }

    public string SlotId => slotId;
    public void SetSlot(string slot) { if (!string.IsNullOrWhiteSpace(slot)) slotId = slot; }

    /// <summary>Gắn vào nút "Lưu game" nếu muốn có nút lưu tay.</summary>
    public void SaveGame()
    {
        if (save == null || !applied) return;          // Chưa trả vị trí xong thì đừng lưu đè.
        if (save.IsSaveBlocked) { Log($"Không lưu được: {save.SaveBlockedReason}"); return; }
        if (CutscenePlaying()) return;                  // Đang cutscene thì vị trí Player chưa chuẩn.
        save.SaveNow();                                 // Xong sẽ gọi OnSaved bên dưới.
    }

    void OnSaved(PlayerSaveData _)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Capstone.Game.UISystem.DevCheatService.Active) return;
#endif
        if (player == null) return;
        Vector3 p = player.position;
        PlayerPrefs.SetString(Key(slotId, "scene"), SceneManager.GetActiveScene().name);
        PlayerPrefs.SetFloat(Key(slotId, "px"), p.x);
        PlayerPrefs.SetFloat(Key(slotId, "py"), p.y);
        PlayerPrefs.SetFloat(Key(slotId, "pz"), p.z);
        PlayerPrefs.SetFloat(Key(slotId, "ry"), player.eulerAngles.y);
        if (visualSwitcher != null) PlayerPrefs.SetInt(Key(slotId, "visual"), visualSwitcher.ActiveVisualIndex);
        PlayerPrefs.Save();
        Log($"Đã lưu vị trí {p} ở scene '{SceneManager.GetActiveScene().name}'.");
    }

    bool restored;
    void OnLoaded(PlayerSaveData _)
    {
        if (!restored) StartCoroutine(ApplyNextFrame());
    }

    IEnumerator ApplyNextFrame()
    {
        if (restored) yield break;
        restored = true;
        yield return null;   // Đợi các hệ thống khác áp save xong.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (Capstone.Game.UISystem.DevCheatService.Active) { ApplyVisual(); applied = true; yield break; }
#endif

        // Vừa tạo nhân vật (New Game) -> PlayerSelectionApplier lo, mình không đụng vào.
        if (CharacterSelectionData.HasPendingSelection) { Log("New Game -> giữ vị trí bắt đầu."); applied = true; yield break; }

        ApplyVisual();
        ApplyPosition();
        applied = true;
    }

    void ApplyVisual()
    {
        int index = PlayerPrefs.GetInt(Key(slotId, "visual"), -1);
        if (index < 0 || visualSwitcher == null) return;
        visualSwitcher.SetRuntimeSwitchLocked(false);
        visualSwitcher.SwitchTo(index, true);
        visualSwitcher.SetRuntimeSwitchLocked(true);
        Log($"Trả lại nhân vật: {visualSwitcher.ActiveVisualName}");
    }

    void ApplyPosition()
    {
        if (!HasSavedScene(slotId, out string scene)) return;
        if (scene != SceneManager.GetActiveScene().name) return;   // Lưu ở scene khác thì để mặc định.

        var pos = new Vector3(PlayerPrefs.GetFloat(Key(slotId, "px")),
                              PlayerPrefs.GetFloat(Key(slotId, "py")),
                              PlayerPrefs.GetFloat(Key(slotId, "pz")));
        var rot = Quaternion.Euler(0f, PlayerPrefs.GetFloat(Key(slotId, "ry")), 0f);

        // CharacterController sẽ kéo Player về chỗ cũ nếu không tắt trước khi dịch chuyển.
        var cc = player.GetComponent<CharacterController>();
        bool ccWasOn = cc != null && cc.enabled;
        if (ccWasOn) cc.enabled = false;
        player.SetPositionAndRotation(pos + Vector3.up * 0.05f, rot);
        if (ccWasOn) cc.enabled = true;
        Physics.SyncTransforms();
        Log($"Load Game -> đưa Player về {pos}.");
    }

    static bool CutscenePlaying()
    {
        foreach (var c in FindObjectsByType<VillageChiefCutscene>(FindObjectsSortMode.None))
            if (c.IsPlaying) return true;
        var intro = FindFirstObjectByType<NewGameIntroSequence>();
        return intro != null && intro.IsPlaying;
    }

    void Log(string message) { if (logSteps) Debug.Log($"{Tag} {message}"); }
}
