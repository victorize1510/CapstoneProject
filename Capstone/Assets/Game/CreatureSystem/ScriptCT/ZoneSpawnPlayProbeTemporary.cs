#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.CreatureSystem {
    static class ZoneSpawnPlayProbeTemporary {
        const string Key = "Capstone.ZoneSpawnProbe.20261121";
        static string Root => Path.Combine(UISystem.QaArtifactPaths.Root, "zone-spawn-terrain-2026-11-21");
        static string Report => Path.Combine(Root, "unity-verification.txt");

        [MenuItem("Tools/Zone Spawn QA (DemoScene 2)")]
        static void Begin() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != "DemoScene 2" || SceneManager.sceneCount != 1) {
                Debug.LogWarning("Zone QA requires DemoScene 2 only, in Edit Mode.");
                return;
            }
            Directory.CreateDirectory(Root);
            var zones = Object.FindObjectsByType<CreatureEncounterZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Physics.SyncTransforms();
            File.WriteAllLines(Report, zones.Select(zone => $"EDITOR {zone.name} valid={zone.ValidateConfiguration(out string reason)} {reason}\n{zone.ProbeNavMeshCoverage()}"));
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void WatchPlay() {
            EditorApplication.playModeStateChanged += state => {
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(Key);
            };
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Isolate() {
            if (!SessionState.GetBool(Key, false)) return;
            foreach (var controller in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                controller.SetSaveStore(new PlayerJsonFileSaveStore(Path.Combine(Root, "qa-saves")));
            foreach (var controller in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                controller.SetSaveStore(new QuestJsonFileSaveStore(Path.Combine(Root, "qa-saves")));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static async void Measure() {
            if (!SessionState.GetBool(Key, false)) return;
            bool background = Application.runInBackground;
            Application.runInBackground = true;
            try {
                var zones = Object.FindObjectsByType<CreatureEncounterZone>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                var mid = zones.First(zone => zone.name.StartsWith("Zone_Grass_Mid", StringComparison.Ordinal));
                double start = Time.realtimeSinceStartupAsDouble;
                foreach (double seconds in new[] { 3d, 9d, 20d }) {
                    while (Application.isPlaying && Time.realtimeSinceStartupAsDouble - start < seconds) await Awaitable.NextFrameAsync();
                    if (!Application.isPlaying) return;
                    foreach (var zone in zones) File.AppendAllText(Report, $"PLAY +{seconds}s {zone.name} position={zone.transform.position:F2} occupied={zone.OccupiedSlots}/{zone.TargetPopulation} eligible={zone.EligibleSpeciesCount} total={zone.TotalSpawned} {zone.LastPlacementReport}\n");
                }
                var actors = DummyEnemy.ActiveEnemies.Where(actor => actor != null && actor.name.StartsWith("Wild_", StringComparison.Ordinal)).ToArray();
                File.AppendAllText(Report, "PLAY Wild actors: " + string.Join(", ", actors.GroupBy(actor => actor.name).Select(group => $"{group.Key} x{group.Count()}")) + "\n");
            } catch (Exception exception) {
                File.AppendAllText(Report, "QA ERROR " + exception.Message + "\n");
            } finally {
                Application.runInBackground = background;
                if (Application.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
    }
}
#endif
