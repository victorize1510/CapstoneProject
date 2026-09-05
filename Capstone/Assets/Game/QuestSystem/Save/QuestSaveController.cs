using System;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.QuestSystem.Save {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(QuestManager))]
    public sealed class QuestSaveController : MonoBehaviour {
        [SerializeField] QuestManager questManager;
        [SerializeField] string slotId = "main";
        [SerializeField] bool loadOnStart = true;
        [SerializeField] bool saveOnApplicationQuit = true;
        [SerializeField] bool saveOnDisable;

        IQuestSaveStore saveStore;
        bool applicationIsQuitting;

        public event Action Saved;
        public event Action Loaded;
        public event Action NewGameStarted;
        public event Action<string> SaveOperationFailed;

        public string SlotId => slotId;

        void Awake() {
            ResolveReferences();
            saveStore ??= new QuestJsonFileSaveStore();
        }

        void Start() {
            if (loadOnStart && UnifiedSave() == null) Load();
        }

        void OnDisable() {
            if (!Application.isPlaying || applicationIsQuitting || !saveOnDisable || UnifiedSave() != null) return;
            Save();
        }

        void OnApplicationQuit() {
            applicationIsQuitting = true;
            if (saveOnApplicationQuit && UnifiedSave() == null) Save();
        }

        public void SetSaveStore(IQuestSaveStore store) {
            saveStore = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool HasSave() {
            var unified = UnifiedSave();
            if (unified != null && unified.HasSave) return true;
            EnsureSaveStore();
            return saveStore.Exists(slotId);
        }

        public bool Save() {
            var unified = UnifiedSave();
            if (unified != null) return unified.SaveNow();
            ResolveReferences();
            EnsureSaveStore();
            if (questManager == null) return Fail("QuestManager is missing.");

            if (!saveStore.TrySave(slotId, questManager.CreateSaveData(), out string error)) {
                return Fail("Could not save quest progress: " + error);
            }

            Saved?.Invoke();
            return true;
        }

        public bool Load() {
            var unified = UnifiedSave();
            if (unified != null) return unified.LoadNow();
            ResolveReferences();
            EnsureSaveStore();
            if (questManager == null) return Fail("QuestManager is missing.");

            if (!saveStore.TryLoad(slotId, out QuestSaveData data, out string error)) {
                if (string.IsNullOrWhiteSpace(error)) return false;
                return Fail("Could not load quest progress: " + error);
            }

            if (!questManager.CanRestoreSaveData(data, out error)) return Fail(error);
            questManager.RestoreFromSaveData(data);
            Loaded?.Invoke();
            return true;
        }

        public bool StartNewGame(bool deleteStoredSave = true) {
            if (UnifiedSave() != null) return Fail("Quest-only reset is unavailable while unified player save owns this slot.");
            ResolveReferences();
            EnsureSaveStore();
            if (questManager == null) return Fail("QuestManager is missing.");

            if (deleteStoredSave && !saveStore.TryDelete(slotId, out string error)) {
                return Fail("Could not delete quest save: " + error);
            }

            questManager.ResetForNewGame();
            NewGameStarted?.Invoke();
            return true;
        }

        void ResolveReferences() {
            if (questManager == null) questManager = GetComponent<QuestManager>();
        }

        PlayerSaveController UnifiedSave() {
            ResolveReferences();
            return PlayerSaveController.FindForQuest(questManager, slotId);
        }

        void EnsureSaveStore() {
            saveStore ??= new QuestJsonFileSaveStore();
        }

        bool Fail(string message) {
            Debug.LogWarning(message, this);
            SaveOperationFailed?.Invoke(message);
            return false;
        }
    }
}
