using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEngine;

namespace Capstone.Game.SaveSystem {
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class PlayerSaveController : MonoBehaviour {
        const int PartySize = 6;

        [Header("Data Sources")]
        [SerializeField] QuestManager questManager = null;
        [SerializeField] MonsterInventoryAdapter inventoryAdapter = null;
        [SerializeField] PlayerCurrencyWallet currencyWallet = null;
        [SerializeField] PetCommandInput petCommandInput = null;
        [SerializeField] PetBoxRuntimeProvider petBoxProvider = null;
        [SerializeField] PlayerProfileRuntimeProvider profileProvider = null;
        [SerializeField] AchievementManager achievementManager = null;
        [SerializeField] QuestRewardService questRewardService = null;
        [SerializeField] PetCaptureCoordinator petCaptureCoordinator = null;
        [SerializeField] PetPrefabCatalog petPrefabCatalog = null;
        [SerializeField] Transform restoredPetRoot = null;
        [SerializeField] bool autoFindReferences = true;
        [SerializeField] bool autoCreateCurrencyWallet = true;
        [SerializeField] bool autoCreateQuestRewardService = true;
        [SerializeField] bool autoCreatePetCaptureCoordinator = true;

        [Header("Save Slot")]
        [SerializeField] string slotId = "main";
        [SerializeField] bool loadOnStart = true;
        [SerializeField] bool saveOnApplicationQuit = true;
        [SerializeField] bool saveOnApplicationPause = true;
        [SerializeField] bool saveOnDisable;
        [SerializeField] bool migrateLegacyQuestSave = true;
        [SerializeField, Min(0f)] float autoSaveDelay = 0.35f;

        PlayerJsonFileSaveStore saveStore;
        bool startupCompleted;
        bool applicationIsQuitting;
        bool isApplyingSave;
        bool saveQueued;
        float saveDueAt;
        string loadedSlotPath;
        string saveBlockedReason = string.Empty;
        PlayerSaveData retainedData;
        readonly List<PetCollectionMetadata> watchedMetadata = new List<PetCollectionMetadata>();
        readonly List<PetHudRuntimeStats> watchedStats = new List<PetHudRuntimeStats>();

        public bool IsApplyingSave => isApplyingSave;
        public string SaveBlockedReason => saveBlockedReason;
        public bool IsSaveBlocked => !string.IsNullOrEmpty(saveBlockedReason);

        public void SetSaveStore(PlayerJsonFileSaveStore store) {
            saveStore = store ?? throw new ArgumentNullException(nameof(store));
            loadedSlotPath = null;
            retainedData = null;
            saveBlockedReason = string.Empty;
            saveQueued = false;
        }

        public static PlayerSaveController FindForQuest(QuestManager manager, string slot) {
            foreach (var controller in FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (controller.isActiveAndEnabled && controller.questManager == manager
                    && string.Equals(controller.SlotId, AtomicSaveFile.Slot(slot), StringComparison.Ordinal)) return controller;
            }
            return null;
        }

        public string SlotId => string.IsNullOrWhiteSpace(slotId) ? "main" : slotId.Trim();
        public string SavePath => GetSaveStore().GetPath(SlotId);
        public bool HasSave => GetSaveStore().Exists(SlotId);
        public PetCaptureCoordinator CaptureCoordinator => petCaptureCoordinator;

        public event Action<PlayerSaveData> SaveCompleted;
        public event Action<PlayerSaveData> LoadCompleted;
        public event Action<string> SaveFailed;
        public event Action<string> LoadFailed;

        IEnumerator Start() {
            yield return null;
            ResolveReferences();
            SubscribeDataSources();
            if (loadOnStart) LoadNow();
            startupCompleted = true;
        }

        void OnEnable() {
            ResolveReferences();
            SubscribeDataSources();
        }

        void Update() {
            if (!startupCompleted || isApplyingSave || !saveQueued || Time.unscaledTime < saveDueAt) return;
            SaveNow();
        }

        void OnApplicationPause(bool paused) {
            if (paused && startupCompleted && saveOnApplicationPause) SaveNow();
        }

        void OnApplicationQuit() {
            applicationIsQuitting = true;
            if (startupCompleted && saveOnApplicationQuit) SaveNow();
        }

        void OnDisable() {
            UnsubscribeDataSources();
            if (!applicationIsQuitting && startupCompleted && saveOnDisable) SaveNow();
        }

        public void RequestSave() {
            if (isApplyingSave || IsSaveBlocked) return;
            saveQueued = true;
            saveDueAt = Time.unscaledTime + Mathf.Max(0f, autoSaveDelay);
        }

        public bool SaveNow() {
            if (isApplyingSave) return false;
            saveQueued = false;
            if (IsSaveBlocked) return false;
            if (HasSave && !string.Equals(loadedSlotPath, SavePath, StringComparison.Ordinal)) {
                return BlockSave("Existing save has not been loaded successfully. Load it before saving.", false);
            }
            ResolveReferences();
            try {
                PlayerSaveData saveData = CaptureSaveData();
                if (GetSaveStore().TrySave(SlotId, saveData, out string error)) {
                    retainedData = saveData;
                    loadedSlotPath = SavePath;
                    SaveCompleted?.Invoke(saveData);
                    return true;
                }
                return BlockSave(error, false);
            } catch (Exception exception) {
                return BlockSave(exception.Message, false);
            }
        }

        public bool LoadNow() {
            if (isApplyingSave) return false;
            saveQueued = false;
            ResolveReferences();
            if (GetSaveStore().TryLoad(SlotId, out PlayerSaveData saveData, out string error)) {
                try {
                    ValidateSources(saveData);
                    ApplySaveData(saveData);
                    retainedData = saveData;
                    loadedSlotPath = SavePath;
                    saveBlockedReason = string.Empty;
                    LoadCompleted?.Invoke(saveData);
                    return true;
                } catch (Exception exception) { return BlockSave(exception.Message, true); }
            }

            if (!string.IsNullOrWhiteSpace(error)) {
                return BlockSave(error, true);
            }
            bool migrated = migrateLegacyQuestSave && TryMigrateLegacyQuestSave();
            if (!migrated && !IsSaveBlocked) {
                retainedData = null;
                loadedSlotPath = SavePath;
            }
            return migrated;
        }

        bool BlockSave(string reason, bool loading) {
            saveBlockedReason = string.IsNullOrWhiteSpace(reason) ? "Save/load did not complete." : reason;
            saveQueued = false;
            Debug.LogWarning("Player save protected: " + saveBlockedReason, this);
            if (loading) LoadFailed?.Invoke(saveBlockedReason);
            else SaveFailed?.Invoke(saveBlockedReason);
            return false;
        }

        public bool DeleteSave() {
            if (GetSaveStore().TryDelete(SlotId, out string error)) {
                saveQueued = false;
                saveBlockedReason = string.Empty;
                loadedSlotPath = null;
                retainedData = null;
                return true;
            }

            Debug.LogWarning($"Could not delete player save: {error}", this);
            return false;
        }

        PlayerSaveData CaptureSaveData() {
            return new PlayerSaveData {
                version = PlayerSaveData.CurrentVersion,
                savedAtUtc = DateTime.UtcNow.ToString("O"),
                quest = questManager != null ? questManager.CreateSaveData() : retainedData?.quest,
                inventory = inventoryAdapter != null ? inventoryAdapter.CreateSaveData() : retainedData?.inventory,
                currency = currencyWallet != null ? currencyWallet.CreateSaveData() : retainedData?.currency,
                pets = petCommandInput != null ? CapturePetRoster() : retainedData?.pets,
                profile = profileProvider != null ? profileProvider.CreateSaveData() : retainedData?.profile,
                achievements = achievementManager != null ? achievementManager.CreateSaveData() : retainedData?.achievements
            };
        }

        PetRosterSaveData CapturePetRoster() {
            var saveData = new PetRosterSaveData {
                captured = petCommandInput != null,
                activePetSummoned = petCommandInput != null
                    && petCommandInput.activePet != null
                    && petCommandInput.activePet.IsSummoned,
                boxCapacity = petBoxProvider != null ? petBoxProvider.Capacity : 60,
                releaseCountDateUtc = petBoxProvider != null ? petBoxProvider.ReleaseCountDateUtc : string.Empty,
                releasedToday = petBoxProvider != null ? petBoxProvider.ReleasedToday : 0
            };

            var petIds = new Dictionary<PetController, string>();
            var usedPetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (retainedData?.pets?.releasedPetIds != null) saveData.releasedPetIds.AddRange(retainedData.pets.releasedPetIds);
            foreach (PetController pet in FindScenePets()) {
                PetCollectionMetadata metadata = ResolveMetadata(pet, true);
                if (metadata == null) continue;

                string petId = EnsureUniquePetId(metadata, usedPetIds, pet);
                if (metadata.IsReleased) {
                    if (!string.IsNullOrWhiteSpace(petId) && !saveData.releasedPetIds.Contains(petId)) saveData.releasedPetIds.Add(petId);
                    continue;
                }

                petIds.Add(pet, petId);
                var instanceData = new PetInstanceSaveData {
                    petId = petId,
                    definitionId = metadata.DefinitionId,
                    customization = metadata.CreateSaveData()
                };

                PetHudRuntimeStats runtimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                if (runtimeStats != null) instanceData.runtimeStats = runtimeStats.CreateSaveData();
                saveData.petStates.Add(instanceData);
            }

            if (petCommandInput != null) {
                saveData.activePetId = ResolvePetId(petCommandInput.activePet, petIds);
                for (int i = 0; i < PartySize; i++) {
                    PetController pet = petCommandInput.petSlots != null && i < petCommandInput.petSlots.Length
                        ? petCommandInput.petSlots[i]
                        : null;
                    saveData.partyPetIds.Add(ResolvePetId(pet, petIds));
                }
            }

            if (petBoxProvider != null) {
                foreach (PetController pet in petBoxProvider.StoredPets) {
                    string petId = ResolvePetId(pet, petIds);
                    if (!string.IsNullOrWhiteSpace(petId)) saveData.boxPetIds.Add(petId);
                }
            }

            return saveData;
        }

        void ApplySaveData(PlayerSaveData saveData) {
            if (saveData == null) return;
            isApplyingSave = true;
            try {
                if (inventoryAdapter != null && saveData.inventory != null && saveData.inventory.captured) {
                    if (!inventoryAdapter.RestoreFromSaveData(saveData.inventory, out string inventoryError)) {
                        throw new InvalidOperationException(inventoryError);
                    }
                }

                if (saveData.currency?.captured == true) currencyWallet?.RestoreFromSaveData(saveData.currency);
                RestorePetRoster(saveData.pets);
                if (saveData.achievements?.captured == true) achievementManager?.RestoreFromSaveData(saveData.achievements);
                if (saveData.profile?.captured == true) profileProvider?.RestoreFromSaveData(saveData.profile);
                if (questManager != null && saveData.quest != null) questManager.RestoreFromSaveData(saveData.quest);
                RefreshPetSubscriptions();
            } finally {
                isApplyingSave = false;
                saveQueued = false;
            }
        }

        PetController RestoreMissingPet(PetInstanceSaveData petState) {
            if (petState == null || petPrefabCatalog == null
                || !petPrefabCatalog.TryInstantiate(petState.definitionId, restoredPetRoot, out PetController pet)) {
                return null;
            }

            PetCollectionMetadata metadata = ResolveMetadata(pet, true);
            metadata?.AssignPersistentId(petState.petId);
            metadata?.AssignDefinitionId(petState.definitionId);
            if (pet.owner == null && petCommandInput != null) pet.AssignOwner(petCommandInput.transform);
            pet.Withdraw();
            return pet;
        }

        void RestorePetRoster(PetRosterSaveData saveData) {
            if (saveData == null || !saveData.captured || petCommandInput == null) return;

            var petsById = new Dictionary<string, PetController>(StringComparer.OrdinalIgnoreCase);
            var usedPetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (PetController pet in FindScenePets()) {
                PetCollectionMetadata metadata = ResolveMetadata(pet, true);
                if (metadata == null) continue;

                string petId = EnsureUniquePetId(metadata, usedPetIds, pet);
                petsById.Add(petId, pet);
            }

            if (saveData.releasedPetIds != null) {
                foreach (string releasedPetId in saveData.releasedPetIds) {
                    if (string.IsNullOrWhiteSpace(releasedPetId)
                        || !petsById.TryGetValue(releasedPetId, out PetController releasedPet)) continue;

                    PetCollectionMetadata releasedMetadata = ResolveMetadata(releasedPet, true);
                    releasedMetadata?.SetReleased(true);
                    releasedPet.Withdraw();
                    releasedPet.gameObject.SetActive(false);
                    petsById.Remove(releasedPetId);
                }
            }

            if (saveData.petStates != null) {
                foreach (PetInstanceSaveData petState in saveData.petStates) {
                    if (petState == null || string.IsNullOrWhiteSpace(petState.petId)) continue;
                    if (!petsById.TryGetValue(petState.petId, out PetController pet)) {
                        pet = RestoreMissingPet(petState);
                        if (pet == null) {
                            Debug.LogWarning(
                                $"Saved pet '{petState.petId}' is missing and definition '{petState.definitionId}' is not configured in the Pet Prefab Catalog.",
                                this);
                            throw new InvalidOperationException("Cannot restore pet " + petState.petId + "; original save is protected.");
                        }
                        petsById.Add(petState.petId, pet);
                    }

                    PetCollectionMetadata metadata = ResolveMetadata(pet, true);
                    metadata?.RestoreFromSaveData(petState.customization);
                    PetHudRuntimeStats runtimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                    runtimeStats?.RestoreFromSaveData(petState.runtimeStats);
                    metadata?.RestoreEvolutionPresentation(runtimeStats);
                }
            }

            petCommandInput.petSlots = new PetController[PartySize];
            if (saveData.partyPetIds != null) {
                for (int i = 0; i < PartySize && i < saveData.partyPetIds.Count; i++) {
                    petCommandInput.petSlots[i] = ResolvePet(saveData.partyPetIds[i], petsById);
                    if (petCommandInput.petSlots[i] != null && petCommandInput.petSlots[i].owner == null) {
                        petCommandInput.petSlots[i].AssignOwner(petCommandInput.transform);
                    }
                }
            }

            PetController activePet = ResolvePet(saveData.activePetId, petsById);
            if (activePet == null) activePet = FindFirstPartyPet();
            petCommandInput.SetActivePet(activePet);

            for (int i = 0; i < petCommandInput.petSlots.Length; i++) {
                PetController partyPet = petCommandInput.petSlots[i];
                if (partyPet != null && partyPet != activePet) partyPet.Withdraw();
            }

            if (activePet != null) {
                if (saveData.activePetSummoned) activePet.Summon();
                else activePet.Withdraw();
            }

            if (petBoxProvider != null) {
                var restoredBoxPets = new List<PetController>();
                if (saveData.boxPetIds != null) {
                    foreach (string petId in saveData.boxPetIds) {
                        PetController pet = ResolvePet(petId, petsById);
                        if (pet != null) restoredBoxPets.Add(pet);
                    }
                }
                petBoxProvider.RestoreState(
                    restoredBoxPets,
                    saveData.boxCapacity,
                    saveData.releaseCountDateUtc,
                    saveData.releasedToday);
            }
        }

        bool TryMigrateLegacyQuestSave() {
            if (questManager == null) return false;

            var legacyStore = new QuestJsonFileSaveStore(GetSaveStore().RootDirectory);
            if (!legacyStore.TryLoad(SlotId, out QuestSaveData questData, out string error)) {
                if (!string.IsNullOrWhiteSpace(error)) {
                    BlockSave("Legacy quest migration: " + error, true);
                }
                return false;
            }

            try {
                if (!questManager.CanRestoreSaveData(questData, out error)) return BlockSave(error, true);
                isApplyingSave = true;
                questManager.RestoreFromSaveData(questData);
                PlayerSaveData migratedData = CaptureSaveData();
                if (!GetSaveStore().TrySave(SlotId, migratedData, out error)) return BlockSave(error, true);
                retainedData = migratedData;
                loadedSlotPath = SavePath;
                saveBlockedReason = string.Empty;
                isApplyingSave = false;
                LoadCompleted?.Invoke(migratedData);
                return true;
            } catch (Exception exception) { return BlockSave(exception.Message, true); }
            finally {
                isApplyingSave = false;
                saveQueued = false;
            }
        }

        void ValidateSources(PlayerSaveData data) {
            PlayerSaveMigration.Validate(data);
            if (data.quest != null) {
                if (questManager == null) throw new InvalidOperationException("QuestManager is missing; save is protected.");
                if (!questManager.CanRestoreSaveData(data.quest, out string error)) throw new InvalidOperationException(error);
            }
            if (data.inventory?.captured == true && inventoryAdapter == null) throw new InvalidOperationException("Inventory source is missing.");
            if (data.currency?.captured == true && currencyWallet == null) throw new InvalidOperationException("Currency source is missing.");
            if (data.profile?.captured == true && profileProvider == null) throw new InvalidOperationException("Profile source is missing.");
            if (data.achievements?.captured == true && achievementManager == null) throw new InvalidOperationException("Achievement source is missing.");
            if (data.pets?.captured != true) return;
            if (petCommandInput == null || petBoxProvider == null) throw new InvalidOperationException("Party/Box source is missing.");
            var pets = new Dictionary<string, PetController>(StringComparer.OrdinalIgnoreCase);
            foreach (PetController pet in FindScenePets()) {
                var metadata = ResolveMetadata(pet, true);
                if (pets.ContainsKey(metadata.PersistentId)) throw new InvalidOperationException("Duplicate scene pet ID: " + metadata.PersistentId);
                pets.Add(metadata.PersistentId, pet);
            }
            foreach (var state in data.pets.petStates) {
                PetController pet;
                if (!pets.TryGetValue(state.petId, out pet)) {
                    if (petPrefabCatalog == null || !petPrefabCatalog.TryGetPrefab(state.definitionId, out pet))
                        throw new InvalidOperationException("Missing pet definition " + state.definitionId + " for " + state.petId + "; save is protected.");
                }
                if (state.runtimeStats?.captured == true) {
                    var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                    if (stats == null) throw new InvalidOperationException("Pet stats source is missing: " + state.petId);
                    if (!stats.CanRestoreSaveData(state.runtimeStats, out string error)) throw new InvalidOperationException(error);
                }
            }
        }

        void ResolveReferences() {
            if (!autoFindReferences) return;

            if (questManager == null) questManager = FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include);
            if (inventoryAdapter == null) inventoryAdapter = FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            if (currencyWallet == null) currencyWallet = FindFirstObjectByType<PlayerCurrencyWallet>(FindObjectsInactive.Include);
            if (currencyWallet == null && autoCreateCurrencyWallet && Application.isPlaying) {
                currencyWallet = gameObject.AddComponent<PlayerCurrencyWallet>();
            }
            if (petCommandInput == null) petCommandInput = FindFirstObjectByType<PetCommandInput>(FindObjectsInactive.Include);
            if (petBoxProvider == null) petBoxProvider = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
            if (profileProvider == null) profileProvider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
            if (achievementManager == null) achievementManager = FindFirstObjectByType<AchievementManager>(FindObjectsInactive.Include);
            if (questRewardService == null) questRewardService = FindFirstObjectByType<QuestRewardService>(FindObjectsInactive.Include);
            if (questRewardService == null && autoCreateQuestRewardService && Application.isPlaying) {
                questRewardService = gameObject.AddComponent<QuestRewardService>();
            }
            questRewardService?.Bind(questManager, inventoryAdapter, currencyWallet, profileProvider, this);
            if (petCaptureCoordinator == null) {
                petCaptureCoordinator = FindFirstObjectByType<PetCaptureCoordinator>(FindObjectsInactive.Include);
            }
            if (petCaptureCoordinator == null && autoCreatePetCaptureCoordinator && Application.isPlaying) {
                petCaptureCoordinator = gameObject.AddComponent<PetCaptureCoordinator>();
            }
            petCaptureCoordinator?.Bind(petBoxProvider, profileProvider, achievementManager, questManager, this);
        }

        void SubscribeDataSources() {
            UnsubscribeDataSources();
            if (questManager != null) questManager.QuestsChanged += RequestSave;
            if (inventoryAdapter != null) inventoryAdapter.InventoryChanged += RequestSave;
            if (currencyWallet != null) currencyWallet.GoldChanged += HandleGoldChanged;
            if (petBoxProvider != null) petBoxProvider.Changed += HandleRosterChanged;
            if (profileProvider != null) profileProvider.ProfileChanged += RequestSave;
            if (achievementManager != null) achievementManager.AchievementsChanged += RequestSave;
            RefreshPetSubscriptions();
        }

        void UnsubscribeDataSources() {
            if (questManager != null) questManager.QuestsChanged -= RequestSave;
            if (inventoryAdapter != null) inventoryAdapter.InventoryChanged -= RequestSave;
            if (currencyWallet != null) currencyWallet.GoldChanged -= HandleGoldChanged;
            if (petBoxProvider != null) petBoxProvider.Changed -= HandleRosterChanged;
            if (profileProvider != null) profileProvider.ProfileChanged -= RequestSave;
            if (achievementManager != null) achievementManager.AchievementsChanged -= RequestSave;
            ClearPetSubscriptions();
        }

        void HandleRosterChanged() {
            RefreshPetSubscriptions();
            RequestSave();
        }

        void ClearPetSubscriptions() {
            foreach (var metadata in watchedMetadata) if (metadata != null) metadata.Changed -= RequestSave;
            foreach (var stats in watchedStats) if (stats != null) stats.PersistentDataChanged -= RequestSave;
            watchedMetadata.Clear();
            watchedStats.Clear();
        }

        void RefreshPetSubscriptions() {
            ClearPetSubscriptions();
            foreach (var pet in FindScenePets()) {
                var metadata = ResolveMetadata(pet, false);
                if (metadata != null) { metadata.Changed += RequestSave; watchedMetadata.Add(metadata); }
                var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                if (stats != null) { stats.PersistentDataChanged += RequestSave; watchedStats.Add(stats); }
            }
        }

        void HandleGoldChanged(int _) {
            RequestSave();
        }

        PlayerJsonFileSaveStore GetSaveStore() {
            return saveStore ??= new PlayerJsonFileSaveStore();
        }

        PetController FindFirstPartyPet() {
            if (petCommandInput == null || petCommandInput.petSlots == null) return null;
            for (int i = 0; i < petCommandInput.petSlots.Length; i++) {
                if (petCommandInput.petSlots[i] != null) return petCommandInput.petSlots[i];
            }
            return null;
        }

        static PetController[] FindScenePets() {
            return FindObjectsByType<PetController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        static PetCollectionMetadata ResolveMetadata(PetController pet, bool createIfMissing) {
            if (pet == null) return null;

            PetCollectionMetadata metadata = pet.GetComponent<PetCollectionMetadata>();
            if (metadata == null) metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null && createIfMissing) metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            return metadata;
        }

        static string ResolvePetId(PetController pet, IReadOnlyDictionary<PetController, string> petIds) {
            return pet != null && petIds.TryGetValue(pet, out string petId) ? petId : string.Empty;
        }

        static string EnsureUniquePetId(
            PetCollectionMetadata metadata,
            ISet<string> usedPetIds,
            UnityEngine.Object context) {
            string petId = metadata.PersistentId;
            if (usedPetIds.Add(petId)) return petId;

            string replacement;
            do {
                replacement = "pet-" + Guid.NewGuid().ToString("N");
            } while (!usedPetIds.Add(replacement));

            metadata.AssignPersistentId(replacement);
            Debug.LogWarning($"Duplicate pet persistent ID '{petId}' was replaced with '{replacement}'.", context);
            return replacement;
        }

        static PetController ResolvePet(string petId, IReadOnlyDictionary<string, PetController> petsById) {
            return !string.IsNullOrWhiteSpace(petId) && petsById.TryGetValue(petId, out PetController pet)
                ? pet
                : null;
        }
    }
}
