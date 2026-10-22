#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Opt-in Editor QA only. Redirects stores before scene Start, never edits a live save.</summary>
    public static class InteractionR3SaveIsolation {
        public const string Flag="Temp/InteractionR3.isolated";
        public static readonly string Output=Path.GetFullPath("../artifacts/ui-r3-fixes-2026-10-20/transaction-run/");
        public static string SaveRoot=>Path.Combine(Output,"test-saves");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Redirect(){
            if(!File.Exists(Flag))return;
            Directory.CreateDirectory(SaveRoot);
            string live=Path.Combine(Application.persistentDataPath,"Saves");
            string evidence=Path.Combine(Output,"save-isolation.txt");
            File.AppendAllText(evidence,DateTime.UtcNow.ToString("O")+" BEFORE START live="+live+" isolated="+SaveRoot+"\n");
            if(Directory.Exists(live))foreach(var file in Directory.GetFiles(live,"*.json*")){
                var target=Path.Combine(SaveRoot,Path.GetFileName(file));
                if(!File.Exists(target))File.Copy(file,target);
                using(var sha=SHA256.Create())File.AppendAllText(evidence,"LIVE SHA256 "+Path.GetFileName(file)+" "+BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))).Replace("-","")+"\n");
            }
            foreach(var save in UnityEngine.Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
                save.SetSaveStore(new PlayerJsonFileSaveStore(SaveRoot));
                File.AppendAllText(evidence,"REDIRECT player "+save.name+" "+save.SavePath+"\n");
            }
            foreach(var save in UnityEngine.Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(SaveRoot));
        }
    }
}
#endif
