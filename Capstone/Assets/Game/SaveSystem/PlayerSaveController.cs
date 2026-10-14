using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
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
        [SerializeField] bool seedDragonPreviewPartyOnFreshSave;

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
        PetController pendingSummonedRestore;
        Coroutine summonedRestoreRoutine;

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
        public bool IsCurrentSlotLoaded => string.Equals(loadedSlotPath, SavePath, StringComparison.Ordinal);
        public PetCaptureCoordinator CaptureCoordinator => petCaptureCoordinator;

        public event Action<PlayerSaveData> SaveCompleted;
        public event Action<PlayerSaveData> LoadCompleted;
        public event Action<string> SaveFailed;
        public event Action<string> LoadFailed;

        IEnumerator Start() {
            yield return null;
            ResolveReferences();
            SubscribeDataSources();
            if (!ApplyPendingReset()) yield break;
            if (loadOnStart) LoadNow();
            if (seedDragonPreviewPartyOnFreshSave && !HasSave && !IsSaveBlocked
                && TrySeedDragonPreviewParty()) SaveNow();
            startupCompleted = true;
        }

        bool TrySeedDragonPreviewParty() {
            if (petCommandInput == null || petPrefabCatalog == null) return false;
            PetController dusk = FindScenePetTemplate("Dragon Dusk");
            PetCollectionMetadata duskMetadata = ResolveMetadata(dusk, false);
            if (dusk == null || duskMetadata == null || duskMetadata.EvolutionStage != 0
                || dusk.GetComponentInChildren<PetHudRuntimeStats>(true) == null) {
                Debug.LogError("Cannot seed Dragon preview party: the base Dusk pet is missing.", this);
                return false;
            }

            var created = new PetController[2];
            var configured = new bool[2];
            for (int stage = 1; stage <= 2; stage++) {
                if (!petPrefabCatalog.TryInstantiate("Dragon Dusk", "01", restoredPetRoot,
                    out PetController pet) || pet == null) break;
                created[stage - 1] = pet;
                PetCollectionMetadata metadata = ResolveMetadata(pet, false);
                PetHudRuntimeStats stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                PetEvolutionVisualController visual = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
                if (metadata == null || stats == null || visual == null) break;

                metadata.AssignPersistentId("preview-dragon-" + (stage + 1) + "-" + Guid.NewGuid().ToString("N"));
                metadata.AssignDefinitionId("Dragon Dusk");
                pet.AssignOwner(petCommandInput.transform);
                pet.Withdraw();
                PetEvolutionRule finalRule = null;
                bool valid = true;
                for (int step = 0; step < stage; step++) {
                    if (!metadata.TryGetNextEvolutionRule(out PetEvolutionRule rule)
                        || !metadata.ApplyEvolution(rule)) { valid = false; break; }
                    finalRule = rule;
                }
                string visualError = string.Empty;
                if (!valid || finalRule?.TargetVisualPrefab == null
                    || !visual.TryApply(finalRule.TargetVisualPrefab, out visualError)) {
                    Debug.LogError("Cannot seed Dragon preview form " + (stage + 1)
                        + ": " + (valid ? visualError : "evolution rule missing"), this);
                    break;
                }
                pet.gameObject.name = finalRule.TargetDisplayName + " (Preview Slot " + (stage + 1) + ")";
                SetPreviewPetLevel(pet, finalRule.TargetDisplayName, stage + 1, finalRule.TargetIcon);
                configured[stage - 1] = true;
            }

            if (!configured[0] || !configured[1]
                || ResolveMetadata(created[0], false)?.EvolutionStage != 1
                || ResolveMetadata(created[1], false)?.EvolutionStage != 2) {
                foreach (PetController pet in created)
                    if (pet != null) Destroy(pet.gameObject);
                Debug.LogError("Dragon preview party setup failed; starter party was kept.", this);
                return false;
            }

            dusk.Withdraw();
            SetPreviewPetLevel(dusk, "Dragon Dusk", 1,
                dusk.GetComponentInChildren<PetHudRuntimeStats>(true)?.Icon);
            petCommandInput.petSlots = new PetController[PartySize];
            petCommandInput.petSlots[0] = dusk;
            petCommandInput.petSlots[1] = created[0];
            petCommandInput.petSlots[2] = created[1];
            petCommandInput.SetActivePet(dusk);
            Debug.Log("Fresh game seeded Dragon Dusk, Nightfall and Darkness in PETS slots 1-3.", this);
            return true;
        }

        static void SetPreviewPetLevel(PetController pet, string displayName, int level, Sprite icon) {
            PetCollectionMetadata metadata = ResolveMetadata(pet, false);
            PetHudRuntimeStats stats = pet != null
                ? pet.GetComponentInChildren<PetHudRuntimeStats>(true) : null;
            if (metadata == null || stats == null) return;
            stats.SetIdentity(displayName, level, icon);
            float maxHealth = metadata.GetStatsAtLevel(level).MaxHealth;
            stats.SetStatus(maxHealth, maxHealth, stats.MaxEnergy, stats.MaxEnergy);
            metadata.SetProgress(0, PetLevelUpService.CalculateDefaultExperienceRequirement(level));
        }

        bool ApplyPendingReset() {
            string marker = SavePath + ".reset-pending";
            if (!File.Exists(marker)) return true;
            if (!DeleteSave()) return BlockSave("Could not reset current player save slot.", true);
            var quests = new QuestJsonFileSaveStore(GetSaveStore().RootDirectory);
            if (!quests.TryDelete(SlotId, out string error)) return BlockSave(error, true);
            try {
                File.Delete(marker);
                Debug.Log("Player save slot " + SlotId + " reset to scene defaults.", this);
                return true;
            }
            catch (Exception exception) { return BlockSave(exception.Message, true); }
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
            CancelSummonedRestore();
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
            CancelSummonedRestore();
            saveQueued = false;
            ResolveReferences();
            if (GetSaveStore().TryLoad(SlotId, out PlayerSaveData saveData, out string error)) {
                try {
                    ValidateSources(saveData);
                    ApplySaveData(saveData, GetSaveStore().LoadedVersion < PlayerSaveData.CurrentVersion);
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
                    && (petCommandInput.activePet.IsSummoned
                        || petCommandInput.activePet == pendingSummonedRestore),
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
                for (int slotIndex = 0; slotIndex < petBoxProvider.Capacity; slotIndex++) {
                    PetController pet = petBoxProvider.GetStoredPet(slotIndex);
                    string petId = ResolvePetId(pet, petIds);
                    if (!string.IsNullOrWhiteSpace(petId)) {
                        saveData.boxSlots.Add(new PetBoxSlotSaveData {
                            slotIndex = slotIndex,
                            petId = petId
                        });
                    }
                }
            }

            return saveData;
        }

        void ApplySaveData(PlayerSaveData saveData, bool fromLegacyVersion) {
            if (saveData == null) return;
            isApplyingSave = true;
            try {
                if (inventoryAdapter != null && saveData.inventory != null && saveData.inventory.captured) {
                    if (!inventoryAdapter.RestoreFromSaveData(saveData.inventory, out string inventoryError)) {
                        throw new InvalidOperationException(inventoryError);
                    }
                }

                if (saveData.currency?.captured == true) currencyWallet?.RestoreFromSaveData(saveData.currency);
                RestorePetRoster(saveData.pets, fromLegacyVersion);
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
            if (petState == null) return null;
            PetController pet = null;
            bool fromCatalog = petPrefabCatalog != null && petPrefabCatalog.TryInstantiate(
                petState.definitionId, petState.customization?.currentFormId, restoredPetRoot, out pet);
            if (!fromCatalog) {
                PetController template = FindScenePetTemplate(petState.definitionId);
                if (template == null) return null;
                Debug.LogWarning("Pet prefab catalog is unavailable; restoring " + petState.definitionId
                    + " from the scene pet template.", this);
                pet = Instantiate(template, restoredPetRoot);
            }

            PetCollectionMetadata metadata = ResolveMetadata(pet, true);
            metadata?.AssignPersistentId(petState.petId);
            metadata?.AssignDefinitionId(petState.definitionId);
            if (pet.owner == null && petCommandInput != null) pet.AssignOwner(petCommandInput.transform);
            pet.Withdraw();
            return pet;
        }

        PetController FindScenePetTemplate(string definitionId) {
            foreach (PetController candidate in FindScenePets()) {
                PetCollectionMetadata metadata = ResolveMetadata(candidate, false);
                if (metadata != null && string.Equals(metadata.DefinitionId, definitionId,
                    StringComparison.OrdinalIgnoreCase)) return candidate;
            }
            return null;
        }

        void RestorePetRoster(PetRosterSaveData saveData, bool fromLegacyVersion) {
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
                    if (!string.IsNullOrWhiteSpace(petState.definitionId))
                        metadata?.AssignDefinitionId(petState.definitionId);
                    metadata?.RestoreFromSaveData(petState.customization);
                    PetHudRuntimeStats runtimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                    runtimeStats?.RestoreFromSaveData(petState.runtimeStats);
                    metadata?.RestoreEvolutionPresentation(runtimeStats);
                    if (metadata != null && runtimeStats != null) {
                        metadata.SyncRuntimeHealth(runtimeStats);
                        if (fromLegacyVersion) {
                            int requirement = runtimeStats.Level >= metadata.MaxLevel ? 0
                                : PetLevelUpService.CalculateDefaultExperienceRequirement(runtimeStats.Level);
                            metadata.SetProgress(metadata.Experience, requirement);
                        }
                    }
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
                if (saveData.activePetSummoned) {
                    // Scene physics and the player can still be settling during the
                    // first load frame. Keep the saved intent until a safe spot exists.
                    activePet.Withdraw();
                    pendingSummonedRestore = activePet;
                    summonedRestoreRoutine = StartCoroutine(RestoreSummonedWhenReady(activePet));
                }
                else activePet.Withdraw();
            }

            if (petBoxProvider != null) {
                int restoredCapacity = Mathf.Max(1, saveData.boxCapacity);
                var restoredBoxPets = new PetController[restoredCapacity];
                if (saveData.boxSlots != null) {
                    foreach (PetBoxSlotSaveData slot in saveData.boxSlots) {
                        if (slot == null || slot.slotIndex < 0 || slot.slotIndex >= restoredCapacity) continue;
                        restoredBoxPets[slot.slotIndex] = ResolvePet(slot.petId, petsById);
                    }
                }
                petBoxProvider.RestoreState(
                    restoredBoxPets,
                    restoredCapacity,
                    saveData.releaseCountDateUtc,
                    saveData.releasedToday);
            }
        }

        IEnumerator RestoreSummonedWhenReady(PetController pet) {
            yield return null;
            var retryDelay = new WaitForSecondsRealtime(0.2f);
            PetSummonDirector director = FindFirstObjectByType<PetSummonDirector>();
            while (pet != null && pet == pendingSummonedRestore) {
                if (petCommandInput == null || petCommandInput.activePet != pet
                    || pet.IsSummoned || (director != null && director.IsSequenceRunning)) {
                    break;
                }
                Physics.SyncTransforms();
                if (pet.TryRestoreSummonedNearOwner()) {
                    pendingSummonedRestore = null;
                    summonedRestoreRoutine = null;
                    yield break;
                }
                yield return retryDelay;
            }
            pendingSummonedRestore = null;
            summonedRestoreRoutine = null;
        }

        void CancelSummonedRestore() {
            if (summonedRestoreRoutine != null) StopCoroutine(summonedRestoreRoutine);
            summonedRestoreRoutine = null;
            pendingSummonedRestore = null;
        }

        public bool CancelPendingSummonedRestore(PetController pet) {
            if (pet == null || pet != pendingSummonedRestore) return false;
            CancelSummonedRestore();
            RequestSave();
            return true;
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
                    if ((petPrefabCatalog == null || !petPrefabCatalog.TryGetPrefab(
                            state.definitionId, state.customization?.currentFormId, out pet))
                        && (pet = FindScenePetTemplate(state.definitionId)) == null)
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
