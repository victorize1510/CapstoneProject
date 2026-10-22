using System;
using System.Collections;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.QuestSystem.UI;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Capstone.Game.SettingsSystem {
    /// <summary>User preferences only; player progression remains in PlayerSaveController.</summary>
    public sealed class GameSettingsService : MonoBehaviour {
        public static GameSettingsService Instance { get; private set; }
        public static bool InteractionPromptsVisible => Instance == null || Instance.Model == null || Instance.Model.Data.showInteractionPrompts;
        public SettingsState Model { get; private set; }
        SettingsRepository repository;
        Coroutine pendingSave;
        bool dirty;
        BasicCameraFollow cameraController;
        GameplayHudController hud;
        Capstone.Game.MapSystem.MapSystemController mapSystem;
        QuestTrackerHudController[] trackers = Array.Empty<QuestTrackerHudController>();
        PlayerSaveController save;
        Label saveIndicator;
        IVisualElementScheduledItem hideIndicator;
        public bool HasCamera => cameraController != null;
        public bool HasMinimap => hud != null && hud.HasMinimap;
        public bool HasQuestTracker => trackers.Length > 0;
        public bool HasSaveIndicator => save != null && saveIndicator != null;
        public bool CanReturnToMainMenu => SceneManager.GetActiveScene().name != "MainMenu" && Application.CanStreamedLevelBeLoaded("MainMenu");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap() { if (Instance == null) new GameObject("Game User Settings").AddComponent<GameSettingsService>(); }
        void Awake() {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            repository = new SettingsRepository(new PlayerPrefsSettingsStorage());
            var defaults = new GameSettingsData { fullscreen = Screen.fullScreen };
            Model = new SettingsState(repository.Load(defaults, out string warning), defaults);
            if (warning.Length > 0) Debug.LogWarning(warning);
            Model.Changed += OnChanged; SceneManager.sceneLoaded += SceneLoaded;
            AudioListener.volume = Model.Data.muteAll ? 0 : Model.Data.masterVolume;
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(BindAfterStartup());
        IEnumerator BindAfterStartup() {
            yield return null; ResolveScene();
            if (Model.Data.resolutionWidth > 0) Apply(SettingsArea.Display);
        }
        public void ResolveScene() {
            cameraController = FindFirstObjectByType<BasicCameraFollow>();
            hud = FindFirstObjectByType<GameplayHudController>();
            mapSystem = FindFirstObjectByType<Capstone.Game.MapSystem.MapSystemController>();
            trackers = FindObjectsByType<QuestTrackerHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var nextSave = FindFirstObjectByType<PlayerSaveController>();
            if (save != nextSave) {
                if (save != null) save.SaveCompleted -= Saved;
                save = nextSave; if (save != null) save.SaveCompleted += Saved;
            }
            var inventory = FindFirstObjectByType<MonsterInventoryController>();
            VisualElement docRoot = inventory != null ? inventory.GetComponent<UIDocument>()?.rootVisualElement : null;
            if (saveIndicator?.parent != docRoot) {
                hideIndicator?.Pause(); saveIndicator?.RemoveFromHierarchy(); saveIndicator = null;
            }
            if (docRoot != null && saveIndicator == null) {
                var feedbackStyle = Resources.Load<StyleSheet>("UI/SettingsFeedback");
                if (feedbackStyle != null && !docRoot.styleSheets.Contains(feedbackStyle)) docRoot.styleSheets.Add(feedbackStyle);
                saveIndicator = new Label { name = "game-save-indicator", pickingMode = PickingMode.Ignore, focusable = false };
                saveIndicator.AddToClassList("menu-save-indicator"); docRoot.Add(saveIndicator); saveIndicator.style.display = DisplayStyle.None;
            }
            Apply(SettingsArea.Audio | SettingsArea.Camera | SettingsArea.Interface);
        }
        void Apply(SettingsArea area) {
            var data = Model.Data;
            if ((area & SettingsArea.Audio) != 0) AudioListener.volume = data.muteAll ? 0 : data.masterVolume;
            if ((area & SettingsArea.Camera) != 0 && cameraController != null)
                cameraController.ApplyUserPreferences(data.mouseSensitivity, data.invertY, data.cameraSmoothing);
            if ((area & SettingsArea.Interface) != 0) {
                hud?.SetMinimapVisible(data.showMinimap);
                mapSystem?.SetUserMinimapVisible(data.showMinimap);
                foreach (var tracker in trackers) if (tracker != null && tracker.gameObject.activeSelf != data.showQuestTracker) tracker.gameObject.SetActive(data.showQuestTracker);
                if (!data.showSaveIndicator && saveIndicator != null) saveIndicator.style.display = DisplayStyle.None;
            }
            if ((area & SettingsArea.Display) != 0) DisplaySettings.Apply(data);
        }
        void OnChanged(SettingsArea area) {
            Apply(area); dirty = true;
            if (pendingSave != null) StopCoroutine(pendingSave);
            pendingSave = StartCoroutine(SaveLater());
        }
        IEnumerator SaveLater() { yield return new WaitForSecondsRealtime(.6f); pendingSave = null; Flush(); }
        public void Flush() {
            if (!dirty) return;
            if (pendingSave != null) { StopCoroutine(pendingSave); pendingSave = null; }
            repository.Save(Model.Data); dirty = false;
        }
        void Saved(PlayerSaveData _) {
            if (!Model.Data.showSaveIndicator || saveIndicator == null) return;
            hideIndicator?.Pause(); saveIndicator.text = "Đã lưu trò chơi."; saveIndicator.style.display = DisplayStyle.Flex;
            hideIndicator = saveIndicator.schedule.Execute(() => saveIndicator.style.display = DisplayStyle.None);
            hideIndicator.ExecuteLater(1800);
        }
        public bool TryReturnToMainMenu(out string error) {
            error = "";
            if (!CanReturnToMainMenu) { error = "Màn hình chính chưa sẵn sàng."; return false; }
            if (save != null && !save.SaveNow()) { error = "Chưa thể lưu tiến trình. Vui lòng thử lại. " + save.SaveBlockedReason; return false; }
            Flush(); SceneManager.LoadSceneAsync("MainMenu"); return true;
        }
        void OnApplicationPause(bool pause) { if (pause) Flush(); }
        void OnApplicationFocus(bool focus) { if (!focus) Flush(); }
        void OnApplicationQuit() => Flush();
        void OnDestroy() {
            if (Instance != this) return;
            Flush(); Model.Changed -= OnChanged; SceneManager.sceneLoaded -= SceneLoaded;
            if (save != null) save.SaveCompleted -= Saved;
            hideIndicator?.Pause(); saveIndicator?.RemoveFromHierarchy(); Instance = null;
        }
    }
}
