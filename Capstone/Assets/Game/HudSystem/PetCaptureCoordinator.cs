using System;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetCaptureCoordinator : MonoBehaviour {
        [SerializeField] PetBoxRuntimeProvider roster = null;
        [SerializeField] PlayerProfileRuntimeProvider profileProvider = null;
        [SerializeField] AchievementManager achievementManager = null;
        [SerializeField] QuestManager questManager = null;
        [SerializeField] PlayerSaveController saveController = null;

        public event Action<PetController> CaptureCommitted;

        public void Bind(
            PetBoxRuntimeProvider targetRoster,
            PlayerProfileRuntimeProvider targetProfile,
            AchievementManager targetAchievements,
            QuestManager targetQuestManager,
            PlayerSaveController targetSaveController) {
            roster = targetRoster != null ? targetRoster : roster;
            profileProvider = targetProfile != null ? targetProfile : profileProvider;
            achievementManager = targetAchievements != null ? targetAchievements : achievementManager;
            questManager = targetQuestManager != null ? targetQuestManager : questManager;
            saveController = targetSaveController != null ? targetSaveController : saveController;
            ResolveReferences();
        }

        public bool TryCommitCapture(
            PetController pet,
            string creatureId,
            string creatureTypeId,
            out string error) {
            ResolveReferences();
            if (pet == null) return Fail("Pet vừa bắt không hợp lệ.", out error);
            if (roster == null) return Fail("Không tìm thấy Box để lưu pet vừa bắt.", out error);
            if (profileProvider == null) return Fail("Không tìm thấy Profile để cập nhật thống kê.", out error);
            if (questManager == null) return Fail("Không tìm thấy QuestManager để cập nhật nhiệm vụ.", out error);
            if (saveController == null) return Fail("Không tìm thấy PlayerSaveController.", out error);

            PetCollectionMetadata metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null) metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            string speciesId = FirstNonEmpty(creatureTypeId, metadata.DefinitionId, metadata.Species);
            if (string.IsNullOrWhiteSpace(speciesId)) return Fail("Pet vừa bắt thiếu ID loài.", out error);

            string instanceId = FirstNonEmpty(creatureId);
            if (string.IsNullOrWhiteSpace(instanceId)) {
                instanceId = "captured-" + Guid.NewGuid().ToString("N");
            }
            metadata.AssignPersistentId(instanceId);
            if (string.IsNullOrWhiteSpace(metadata.DefinitionId)) metadata.AssignDefinitionId(speciesId);

            bool wasSummoned = pet.IsSummoned;
            PlayerProfileSaveData profileSnapshot = profileProvider.CreateSaveData();
            AchievementSaveData achievementSnapshot = achievementManager != null
                ? achievementManager.CreateSaveData()
                : null;
            QuestSaveData questSnapshot = questManager.CreateSaveData();

            if (!roster.TryStoreCapturedPet(pet, out error)) return false;

            profileProvider.RecordCreatureSeen();
            profileProvider.RecordSpeciesCaptured(speciesId);
            questManager.ReportProgress(QuestProgressEvent.CreatureCaptured(instanceId, speciesId));

            if (!saveController.SaveNow()) {
                questManager.RestoreFromSaveData(questSnapshot);
                profileProvider.RestoreFromSaveData(profileSnapshot);
                if (achievementSnapshot != null) achievementManager?.RestoreFromSaveData(achievementSnapshot);
                roster.TryRemoveCapturedPetForRollback(pet, wasSummoned, out _);
                roster.RestorePendingCapturedPet(pet);
                return Fail("Không thể lưu lần bắt pet. Mọi thay đổi đã được hoàn tác.", out error);
            }

            CaptureCommitted?.Invoke(pet);
            error = string.Empty;
            return true;
        }

        public bool TryCommitPendingCapture(out string error) {
            ResolveReferences();
            PetController pet = roster != null ? roster.PendingCapturedPet : null;
            if (pet == null) return Fail("Không có pet đang chờ đưa vào Box.", out error);

            PetCollectionMetadata metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            string instanceId = metadata != null ? metadata.PersistentId : string.Empty;
            string speciesId = metadata != null ? metadata.DefinitionId : string.Empty;
            return TryCommitCapture(pet, instanceId, speciesId, out error);
        }

        void ResolveReferences() {
            if (roster == null) roster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
            if (profileProvider == null) profileProvider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
            if (achievementManager == null) achievementManager = FindFirstObjectByType<AchievementManager>(FindObjectsInactive.Include);
            if (questManager == null) questManager = FindFirstObjectByType<QuestManager>(FindObjectsInactive.Include);
            if (saveController == null) saveController = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
        }

        static string FirstNonEmpty(params string[] values) {
            if (values == null) return string.Empty;
            for (int i = 0; i < values.Length; i++) {
                if (!string.IsNullOrWhiteSpace(values[i])) return values[i].Trim();
            }
            return string.Empty;
        }

        static bool Fail(string message, out string error) {
            error = message;
            return false;
        }
    }
}
