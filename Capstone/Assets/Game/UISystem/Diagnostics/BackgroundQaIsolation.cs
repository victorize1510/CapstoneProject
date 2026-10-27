#if UNITY_EDITOR
using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEngine;

namespace Capstone.Game.UISystem {
    // Explicit opt-in for the background-only capture session. Real saves are never written.
    public static class BackgroundQaIsolation {
        public const string Root = QaArtifactPaths.Root + "bag-pet-backgrounds-unity-2026-10-23/";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void MaterialMode() { PanelMaterialField.UseUpdatedMaterials = !File.Exists(Root + "baseline.flag"); }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Redirect() {
            if (!File.Exists(Root + "isolate.flag")) return;
            string seed = Root + "save-baseline", target = Root + "test-saves";
            Directory.CreateDirectory(seed); Directory.CreateDirectory(target);
            string source = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(source)) foreach (string file in Directory.GetFiles(source, "*.json*")) {
                string copy = Path.Combine(seed, Path.GetFileName(file));
                if (!File.Exists(copy)) File.Copy(file, copy);
                File.Copy(copy, Path.Combine(target, Path.GetFileName(file)), true);
            }
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                save.SetSaveStore(new PlayerJsonFileSaveStore(target));
            foreach (var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                save.SetSaveStore(new QuestJsonFileSaveStore(target));
            File.WriteAllText(Root + "isolation.txt", "Player/Quest stores redirected before Start: " + target);
        }
    }
}
#endif
