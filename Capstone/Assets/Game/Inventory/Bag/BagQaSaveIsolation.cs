#if UNITY_EDITOR
using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.UISystem;
using UnityEngine;
namespace Capstone.Game.Inventory.Bag {
    static class BagQaSaveIsolation {
        const string Root = QaArtifactPaths.Root + "bag-implementation-2026-10-22/";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Redirect() {
            if (!File.Exists(Root + "isolate.flag")) return;
            string target = Root + "test-saves"; Directory.CreateDirectory(target);
            string source = Path.Combine(Application.persistentDataPath,"Saves");
            if (Directory.Exists(source)) foreach (string file in Directory.GetFiles(source,"*.json*")) {
                string copy=Path.Combine(target,Path.GetFileName(file));if(!File.Exists(copy))File.Copy(file,copy);
            }
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(target));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(target));
            File.WriteAllText(Root+"isolation.txt","Stores redirected before Start: "+target);
        }
    }
}
#endif
