#if UNITY_EDITOR
using System;
using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.UISystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Opt-in QA only. A task-owned flag on E redirects stores before scene Start.</summary>
    static class PetSkillQaSaveIsolation {
        const string Root = QaArtifactPaths.Root + "pet-skill-library-2026-10-22/";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Redirect() {
            if (!File.Exists(Root + "isolate.flag")) return;
            string target = Root + "test-saves";
            Directory.CreateDirectory(target);
            string live = Path.Combine(Application.persistentDataPath, "Saves");
            if (Directory.Exists(live)) foreach (var file in Directory.GetFiles(live, "*.json*")) {
                string copy = Path.Combine(target, Path.GetFileName(file)); if (!File.Exists(copy)) File.Copy(file, copy);
            }
            foreach (var save in UnityEngine.Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new PlayerJsonFileSaveStore(target));
            foreach (var save in UnityEngine.Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new QuestJsonFileSaveStore(target));
            File.WriteAllText(Root + "isolation.txt", DateTime.UtcNow.ToString("O") + " stores redirected before Start to " + target);
        }
    }
}
#endif
