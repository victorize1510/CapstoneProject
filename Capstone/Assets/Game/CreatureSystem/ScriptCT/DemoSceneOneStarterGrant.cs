using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Opt-in development-scene starter receipt. Never matches pets by display name.</summary>
    [DisallowMultipleComponent]
    public sealed class DemoSceneOneStarterGrant : MonoBehaviour {
        public const string GrantKey = "demoscene1.starter.v1";
        public PetController baseStarter;
        public string legacyBasePetId;
        public PetPrefabCatalog catalog;
        static readonly int[] Levels = { 5, 18, 36 };
        public string LastError { get; private set; }

        public bool TryApply(PlayerSaveController save, out string error) {
            error = string.Empty;
            if (gameObject.scene.name != "DemoScene 1" || save == null) {
                error = "Gói khởi đầu chỉ dành cho DemoScene 1."; return false;
            }
            if (save.HasStarterGrant(GrantKey)) return true;
            var input = FindFirstObjectByType<PetCommandInput>();
            var box = FindFirstObjectByType<PetBoxRuntimeProvider>();
            var bag = FindFirstObjectByType<MonsterInventoryAdapter>();
            var ball = CaptureAssets.LoadBall();
            if (input?.petSlots == null || input.petSlots.Length != 6 || box == null || bag == null || ball == null || catalog == null) {
                error = "Thiếu Party, BOX, túi hoặc cấu hình gói khởi đầu."; LastError = error; return false;
            }
            var created = new List<PetController>();
            var placements = new List<PetBoxRuntimeProvider.CapturePlacement>();
            var snapshots = new List<(PetHudRuntimeStats stats, PetRuntimeStatsSaveData runtime, PetCollectionMetadata meta, PetCustomizationSaveData data)>();
            var slots = bag.CaptureSlots();
            bool success = save.TryCommitStarterGrant(GrantKey, () => {
                var owned = input.petSlots.Concat(box.StoredPets).Where(p => p != null).Distinct().ToArray();
                for (int form = 0; form < Levels.Length; form++) {
                    int index = form;
                    var pet = owned.FirstOrDefault(p => MatchesLegacy(p, index));
                    if (pet == null) {
                        if (!catalog.TryInstantiate("Dragon Dusk", (form + 1).ToString("00"), null, out pet) || pet == null) return false;
                        created.Add(pet);
                        pet.GetComponent<PetCollectionMetadata>().AssignPersistentId("starter-demoscene1-" + (form + 1) + "-" + Guid.NewGuid().ToString("N"));
                        pet.AssignOwner(input.transform); pet.Withdraw();
                        if (!box.TryStageOwnedPet(pet, out var placement)) return false;
                        placements.Add(placement);
                    }
                    var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
                    var metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
                    if (stats == null || metadata == null) return false;
                    snapshots.Add((stats, stats.CreateSaveData(), metadata, metadata.CreateSaveData()));
                    int level = Mathf.Max(stats.Level, Levels[form]);
                    if (level != stats.Level) {
                        stats.SetIdentity(stats.DisplayName, level, stats.Icon);
                        metadata.SetProgress(metadata.Experience, PetLevelUpService.CalculateDefaultExperienceRequirement(level));
                    }
                    metadata.SyncRuntimeHealth(stats);
                    stats.SetStatus(stats.MaxHealth, stats.MaxHealth, 0, 0);
                }
                return bag.TryAddItemAtomic(ball, 5, false);
            }, () => {
                bag.RestoreCaptureSlots(slots);
                for (int i = placements.Count - 1; i >= 0; i--) box.RollbackCapture(placements[i]);
                foreach (var item in snapshots) { item.meta.RestoreFromSaveData(item.data); item.stats.RestoreFromSaveData(item.runtime); }
                foreach (var pet in created) if (pet != null) { pet.gameObject.SetActive(false); Destroy(pet.gameObject); }
            }, out error);
            LastError = error;
            if (success) { box.PublishCapture(); bag.NotifyAtomicAddition(); }
            else Debug.LogWarning("Gói khởi đầu chưa được lưu; đã hoàn tác: " + error, this);
            return success;
        }

        bool MatchesLegacy(PetController pet, int form) {
            var metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null || metadata.IsReleased || !string.IsNullOrEmpty(metadata.CaptureBallId)
                || !string.IsNullOrEmpty(metadata.CaptureDate) || metadata.EvolutionStage != form
                || !string.Equals(metadata.DefinitionId, "Dragon Dusk", StringComparison.Ordinal)) return false;
            string id = metadata.PetId;
            if (id.StartsWith("starter-demoscene1-" + (form + 1) + "-", StringComparison.Ordinal)) return true;
            return form == 0 ? !string.IsNullOrEmpty(legacyBasePetId) && id == legacyBasePetId
                : id.StartsWith("preview-dragon-" + (form + 1) + "-", StringComparison.Ordinal);
        }
    }
}
