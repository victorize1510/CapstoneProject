using System;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed partial class PetCaptureCoordinator : MonoBehaviour {
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
            if(IsCapturing||Capstone.Game.CaptureSystem.CaptureEventBatch.IsActive){error=Capstone.Game.CaptureSystem.CaptureMessages.For(Capstone.Game.CaptureSystem.CaptureFailure.Busy);return false;}
            var metadata=pet!=null?pet.GetComponentInChildren<PetCollectionMetadata>(true):null;
            if(string.IsNullOrWhiteSpace(creatureTypeId))creatureTypeId=metadata?.DefinitionId;
            processing=true;
            try { return CommitAttempt(pet,creatureId,creatureTypeId,true,null,null,null,out error); }
            finally { processing=false; }
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
