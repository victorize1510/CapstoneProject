#if UNITY_EDITOR
using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Opt-in verification storage. Never points a verification run at the player's save.</summary>
    public static class ReadabilityQaIsolation {
        public const string Root = QaArtifactPaths.Root + "ui-readability-interaction-2026-10-26/";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Isolate() {
            if (!File.Exists(Root + "isolate.flag")) return;
            string target = Root + "test-saves", source = Path.Combine(Application.persistentDataPath, "Saves");
            Directory.CreateDirectory(target);
            if (Directory.Exists(source)) foreach (string path in Directory.GetFiles(source, "*.json*"))
                File.Copy(path, Path.Combine(target, Path.GetFileName(path)), true);
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                save.SetSaveStore(new PlayerJsonFileSaveStore(target));
            foreach (var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                save.SetSaveStore(new QuestJsonFileSaveStore(target));
        }
    }
}
#endif
