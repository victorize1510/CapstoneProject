#if UNITY_EDITOR
using System.IO;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.UISystem;
using UnityEngine;
namespace Capstone.Game.QuestSystem.UI {
    public static class QuestSkinQaIsolation {
        public const string Root=QaArtifactPaths.Root+"quest-original-layout-unity-2026-10-23/";
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Redirect(){
            if(!File.Exists(Root+"isolate.flag"))return;
            var target=Root+"test-saves";Directory.CreateDirectory(target);var source=Path.Combine(Application.persistentDataPath,"Saves");
            if(Directory.Exists(source))foreach(var file in Directory.GetFiles(source,"*.json*"))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(target));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(target));
        }
    }
}
#endif
