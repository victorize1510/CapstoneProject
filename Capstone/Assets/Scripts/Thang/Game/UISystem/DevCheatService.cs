#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Capstone.Game.UISystem {
    internal static class DevCheatService {
        internal const int Level = 100;
        const int MaximumVitals = 9999;
        const int MaximumStat = 999;
        internal static PlayerJsonFileSaveStore Store { get; private set; }
        internal static bool Active => Store != null;
        internal static bool PlayerLevelCapped => playerLevelBefore.HasValue;
        internal static bool NoCooldown, NoMana, GodMode;
        static PlayerSaveData baseline;
        static WorldFeedback.ExperiencePlayback.Point? playerLevelBefore;
        static readonly Dictionary<string, PetBefore> petsBefore = new(StringComparer.Ordinal);
        static readonly Dictionary<DummyEnemy, PetBefore> enemiesBefore = new();
        static readonly Dictionary<BossController, int> bossesBefore = new();

        sealed class PetBefore {
            internal int level, experience, required, speed;
            internal float hp, maxHp, mp, maxMp;
            internal bool changedLevel, maximumStats;

            internal static PetBefore Capture(PetHudRuntimeStats stats, PetCollectionMetadata metadata) => new() {
                level = stats.Level, hp = stats.Health, maxHp = stats.MaxHealth, mp = stats.Energy, maxMp = stats.MaxEnergy,
                experience = metadata != null ? metadata.Experience : 0,
                required = metadata != null ? metadata.ExperienceToNextLevel : 0,
                speed = metadata != null ? metadata.Speed : 0
            };

            internal void Restore(PetHudRuntimeStats stats, PetCollectionMetadata metadata) {
                if (changedLevel) {
                    stats.SetIdentity(stats.DisplayName, level);
                    metadata?.SetProgress(experience, required);
                }
                stats.SetStatus(hp, maxHp, mp, maxMp);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ClearSession() {
            Store = null;
            baseline = null;
            ClearOverrides();
        }

        static void ClearOverrides() {
            NoCooldown = NoMana = GodMode = false;
            playerLevelBefore = null;
            petsBefore.Clear();
            enemiesBefore.Clear();
            bossesBefore.Clear();
        }

        internal static void StartSession(PlayerJsonFileSaveStore store, PlayerSaveData snapshot) {
            Store = store;
            baseline = snapshot;
        }

        internal static bool EnsureSession(PlayerSaveController saves, out string error) {
            error = "Không tìm thấy Player Save Controller.";
            if (saves == null) return false;
            if (!Active) return saves.TryBeginDeveloperSave(out error);
            if (!saves.HasCompletedStartup || !saves.IsCurrentSlotLoaded || saves.IsSaveBlocked) {
                error = "Save phiên thử chưa sẵn sàng. " + saves.SaveBlockedReason;
                return false;
            }
            error = string.Empty;
            return true;
        }

        internal static PetController[] Party(PetCommandInput player) => player != null && player.petSlots != null
            ? player.petSlots.Where(pet => pet != null).Distinct().ToArray() : Array.Empty<PetController>();

        internal static bool Controls(GameObject actor) => Active && actor.TryGetComponent<PetController>(out var pet) &&
            !pet.IsWildEncounter && pet.owner != null && pet.owner.TryGetComponent<PetCommandInput>(out var player) &&
            (player.activePet == pet || Array.IndexOf(player.petSlots ?? Array.Empty<PetController>(), pet) >= 0);

        internal static PetStatSnapshot OverrideStats(PetCollectionMetadata metadata, PetStatSnapshot normal) {
            if (!Active || !petsBefore.TryGetValue(metadata.PersistentId, out var saved) || !saved.maximumStats) return normal;
            return new PetStatSnapshot(MaximumVitals, MaximumStat, MaximumStat, MaximumStat, MaximumStat, saved.speed,
                MaximumVitals, normal.CriticalRate, normal.CriticalDamage);
        }

        internal static string MaxPlayer(PlayerProfileRuntimeProvider profile) {
            if (profile == null) return "Scene này thiếu hồ sơ cấp nhân vật.";
            playerLevelBefore ??= profile.ExperiencePoint;
            profile.SetLevelProgress(Level, 0, 100);
            return "Nhân vật đã đạt cấp 100.";
        }

        internal static int MaxPets(IEnumerable<PetController> pets, bool level, bool stats) {
            int count = 0;
            foreach (var pet in pets.Distinct()) {
                if (pet == null || pet.IsWildEncounter || !pet.TryGetComponent<PetHudRuntimeStats>(out var runtime) ||
                    !pet.TryGetComponent<PetCollectionMetadata>(out var metadata)) continue;
                if (!petsBefore.TryGetValue(metadata.PersistentId, out var before)) {
                    before = PetBefore.Capture(runtime, metadata);
                    petsBefore.Add(metadata.PersistentId, before);
                }
                if (level) {
                    before.changedLevel = true;
                    runtime.SetIdentity(runtime.DisplayName, Level);
                    metadata.SetProgress(0, 0);
                    runtime.UnlockSkillsUpToLevel(Level, out _);
                }
                if (stats) before.maximumStats = true;
                if (before.maximumStats) {
                    runtime.SetStatus(MaximumVitals, MaximumVitals, MaximumVitals, MaximumVitals);
                    if (pet.IsDefeatPresentation) pet.FinishDefeatRecall();
                }
                count++;
            }
            return count;
        }

        internal static string MaxSelected(DummyEnemy enemy, bool creatureOnly) {
            if (enemy == null || enemy.CaptureLocked || !enemy.IsAlive)
                return "Hãy chọn một mục tiêu sống có dữ liệu cấp độ.";
            if (!creatureOnly && enemy.TryGetComponent<BossController>(out var boss)) {
                bossesBefore.TryAdd(boss, boss.DeveloperLevel);
                boss.DeveloperLevel = Level;
                return "Boss đang chọn đã đạt cấp 100.";
            }
            if (!enemy.TryGetComponent<PetHudRuntimeStats>(out var stats) ||
                (creatureOnly && !enemy.TryGetComponent<PetCollectionMetadata>(out _)))
                return "Mục tiêu đang chọn chưa có dữ liệu cấp độ.";
            enemy.TryGetComponent<PetCollectionMetadata>(out var metadata);
            if (!enemiesBefore.ContainsKey(enemy)) enemiesBefore.Add(enemy, PetBefore.Capture(stats, metadata));
            enemiesBefore[enemy].changedLevel = true;
            stats.SetIdentity(stats.DisplayName, Level);
            stats.UnlockSkillsUpToLevel(Level, out _);
            metadata?.SetProgress(0, 0);
            enemy.InitializeCaptureHealth(stats.Health, stats.MaxHealth);
            return "Mục tiêu đã đạt cấp 100.";
        }

        internal static void SetNoCooldown(PetCommandInput player, bool value) {
            NoCooldown = value;
            if (!value) return;
            foreach (var pet in Party(player)) pet.GetComponent<PetHudRuntimeStats>()?.RestoreCooldowns(null);
        }

        internal static int Heal(IEnumerable<PetController> pets) {
            int count = 0;
            foreach (var pet in pets) {
                if (!pet.TryGetComponent<PetHudRuntimeStats>(out var stats)) continue;
                pet.GetComponent<CreatureCombatant>()?.ClearTransientEffects();
                stats.SetStatus(stats.MaxHealth, stats.MaxHealth, stats.MaxEnergy, stats.MaxEnergy);
                if (pet.IsDefeatPresentation) pet.FinishDefeatRecall();
                count++;
            }
            return count;
        }

        internal static void SetGold(PlayerCurrencyWallet wallet) =>
            wallet.RestoreFromSaveData(new CurrencySaveData { captured = true, gold = 9999 });

        internal static QuestDefinition[] Chapter(QuestManager quests) {
            if (quests == null) return Array.Empty<QuestDefinition>();
            var definitions = quests.GetQuestDefinitions().Where(def => def.ChapterId == ChapterQuestFlow.ChapterId).ToList();
            var ordered = new List<QuestDefinition>(definitions.Count);
            while (definitions.Count > 0) {
                var next = definitions.FirstOrDefault(def => def.PrerequisiteQuestIds.All(id =>
                    definitions.All(other => other.QuestId != id)));
                if (next == null) return Array.Empty<QuestDefinition>();
                definitions.Remove(next);
                ordered.Add(next);
            }
            return ordered.ToArray();
        }

        internal static bool CompleteChapter(QuestManager quests, int count, PetElement starter, out string error) {
            error = "Luồng nhiệm vụ Chương I chưa sẵn sàng trong scene này.";
            var chapter = Chapter(quests);
            if (quests?.Chapter == null || chapter.Length != 9 || count < 1 || count > 8) return false;
            if (quests.StarterElement == PetElement.Unknown) quests.StarterElement = starter;
            quests.MarkTutorialComplete();
            for (int i = 0; i < count; i++) {
                if (quests.Chapter.TryDeveloperComplete(chapter[i], quests.StarterElement, out error)) continue;
                error = $"NV{i + 1} · {chapter[i].Title}. {error}";
                return false;
            }
            quests.RefreshChapterTracking();
            error = string.Empty;
            return true;
        }

        internal static bool TryTeleport(ChapterWorldLayout world, PetCommandInput player, int index, out string error) {
            error = "Điểm đến chưa có mặt đất đi được hoặc đang bị vật cản che.";
            if (world == null || player == null) return false;
            var waypoint = world.DeveloperWaypoint(index);
            Vector3 origin;
            if (waypoint != null) origin = waypoint.position;
            else {
                string zoneName = index switch {
                    1 => "Zone_Grass_Easy", 2 => "Zone_Grass_Mid", 3 => "Zone_Grass_Hard", 6 => "Zone_Cave_Deep", _ => null
                };
                if (zoneName == null) return false;
                var zone = UnityEngine.Object.FindObjectsByType<CreatureEncounterZone>(FindObjectsSortMode.None)
                    .Where(zone => zone.gameObject.scene == world.gameObject.scene && zone.zoneBounds != null)
                    .OrderBy(zone => zone.name, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault(zone => zone.name.Equals(zoneName, StringComparison.OrdinalIgnoreCase) ||
                        zone.name.StartsWith(zoneName + " (", StringComparison.OrdinalIgnoreCase));
                if (zone == null) return false;
                origin = zone.zoneBounds.bounds.center;
            }
            var boss = index == 7 && world.DeveloperBossEntrance == null ? world.Boss : null;
            float bossClearance = 0;
            if (boss != null) {
                bossClearance = boss.TryGetComponent<BossController>(out var ai) ? ai.DeveloperApproachDistance : 14f;
                var approachPoint = world.DeveloperWaypoint(5);
                var approach = Vector3.ProjectOnPlane(approachPoint != null ? approachPoint.position - boss.transform.position
                    : -boss.transform.forward, Vector3.up);
                if (approach.sqrMagnitude <= .1f) approach = -boss.transform.forward;
                origin = boss.transform.position + approach.normalized * bossClearance;
            }

            var body = player.GetComponent<CharacterController>();
            if (body == null) return false;
            for (int i = 0; i <= 16; i++) {
                Vector3 candidate = i == 0 ? origin : origin + Quaternion.AngleAxis(i * 45f, Vector3.up) *
                    Vector3.forward * (i < 9 ? 3f : 7f);
                if (!NavMesh.SamplePosition(candidate, out var nav, 6f, NavMesh.AllAreas) ||
                    Mathf.Abs(nav.position.y - origin.y) > 5f || (boss != null && Vector3.ProjectOnPlane(nav.position - boss.transform.position,
                        Vector3.up).magnitude < bossClearance - 1f))
                    continue;
                if (!Physics.Raycast(nav.position + Vector3.up, Vector3.down, out var floor, 1.6f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                    Mathf.Abs(floor.point.y - nav.position.y) > .45f || floor.normal.y < .6f) continue;
                Vector3 point = floor.point + Vector3.up * .08f;
                float radius = body.radius * .9f;
                Vector3 center = point + player.transform.TransformVector(body.center);
                float halfAxis = Mathf.Max(0, body.height * .5f - radius);
                if (Physics.OverlapCapsule(center - Vector3.up * halfAxis, center + Vector3.up * halfAxis, radius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore).Any(collider => !collider.transform.IsChildOf(player.transform)))
                    continue;
                world.DeveloperTeleport(point, waypoint != null ? Quaternion.Euler(0, waypoint.eulerAngles.y, 0) : player.transform.rotation);
                error = string.Empty;
                return true;
            }
            return false;
        }

        internal static void Reset(PlayerProfileRuntimeProvider profile, PetCommandInput player) {
            var owned = player != null ? UnityEngine.Object.FindObjectsByType<PetCollectionMetadata>(FindObjectsInactive.Include,
                FindObjectsSortMode.None).Where(info => info.gameObject.scene == player.gameObject.scene) : Array.Empty<PetCollectionMetadata>();
            var modifiedPets = owned.Where(info => petsBefore.ContainsKey(info.PersistentId))
                .Select(info => (info, before: petsBefore[info.PersistentId])).ToArray();
            var modifiedEnemies = enemiesBefore.ToArray();
            var modifiedBosses = bossesBefore.ToArray();
            var profileLevel = playerLevelBefore;
            ClearOverrides();
            if (profileLevel.HasValue) profile?.SetLevelProgress(profileLevel.Value.Level,
                profileLevel.Value.Experience, profileLevel.Value.Required);
            foreach (var item in modifiedPets)
                if (item.info.TryGetComponent<PetHudRuntimeStats>(out var stats)) item.before.Restore(stats, item.info);
            foreach (var item in modifiedEnemies)
                if (item.Key != null && item.Key.IsAlive && item.Key.TryGetComponent<PetHudRuntimeStats>(out var stats)) {
                    item.Value.Restore(stats, item.Key.GetComponent<PetCollectionMetadata>());
                    item.Key.InitializeCaptureHealth(item.Value.hp, item.Value.maxHp);
                }
            foreach (var item in modifiedBosses)
                if (item.Key != null) item.Key.DeveloperLevel = item.Value;
        }

        internal static bool RestoreBaseline(PlayerSaveController saves, out string error) {
            ClearOverrides();
            return saves.TryRestoreDeveloperSave(baseline, out error);
        }
    }
}
#endif
