#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.ProfileSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Capstone.Game.UISystem {
    /// <summary>Explicit opt-in development player check. All output and stores must be outside the worktree.</summary>
    sealed class ReadabilityStandaloneQa:MonoBehaviour {
        static string root;const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Init(){
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--readability-qa");if(Application.isEditor||i<0||i+1>=args.Length)return;
            root=Path.GetFullPath(args[i+1]);if(!root.StartsWith(Path.GetFullPath(QaArtifactPaths.Root),StringComparison.OrdinalIgnoreCase)){Application.Quit(9);return;}Directory.CreateDirectory(root);
            string saves=Path.Combine(root,"test-saves");if(!File.Exists(Path.Combine(saves,"player_main.json"))){File.WriteAllText(root+"/status.txt","FAILED missing isolated snapshot");Application.Quit(9);return;}
            foreach(var s in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))s.SetSaveStore(new PlayerJsonFileSaveStore(saves));
            foreach(var s in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))s.SetSaveStore(new QuestJsonFileSaveStore(saves));
            Application.runInBackground=true;new GameObject("Readability standalone verification").AddComponent<ReadabilityStandaloneQa>();
        }
        static void Click(VisualElement ui,string id){var b=ui.Q<Button>(id);if(b==null||!b.enabledInHierarchy)throw new Exception("Unavailable "+id);typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        static async Task Shot(string label){await Task.Delay(280);ScreenCapture.CaptureScreenshot(Path.Combine(root,label+"-"+Screen.width+"x"+Screen.height+".png"));await Task.Delay(280);}
        async void Start(){
            try{
                File.WriteAllText(root+"/status.txt","RUNNING actual Windows player");await Task.Delay(2000);var menu=Object.FindFirstObjectByType<GameMenuController>();var bag=Object.FindFirstObjectByType<MonsterInventoryController>();var party=Object.FindFirstObjectByType<PetPartyPanelController>();var profile=Object.FindFirstObjectByType<ProfilePanelController>();var ui=bag.GetComponent<UIDocument>().rootVisualElement;var log=new System.Collections.Generic.List<string>();
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);await Task.Delay(1000);if(Screen.width!=size.x||Screen.height!=size.y)throw new Exception("Actual resolution mismatch "+Screen.width+"x"+Screen.height);log.Add("PASS actual standalone resolution "+size);
                    menu.OpenPetsDirect();await Shot("pet-overview");Click(ui,"pets-tab-skills");await Shot("pet-skills");Click(ui,"pets-tab-growth");await Shot("pet-growth");Click(ui,"pets-tab-overview");await Task.Delay(180);Click(ui,"pets-details-button");ui.Q<ScrollView>(className:"ga-details-scroll").scrollOffset=Vector2.zero;await Shot("pet-details");party.CloseDetails();
                    party.SetPreview3D(true);await Task.Delay(350);if(!party.HasPreviewTexture)throw new Exception("3D texture missing");await Shot("pet-preview3d");party.SetPreview3D(false);if(party.HasPreviewTexture)throw new Exception("3D preview leaked");log.Add("PASS explicit 3D created and released");
                    bag.Close();menu.OpenInventoryDirect();await Shot("bag-grid");Click(ui,"inventory-view-button");await Shot("bag-list");Click(ui,"inventory-view-button");bag.Close();menu.OpenQuestDirect();await Shot("quest");bag.Close();menu.OpenProfileDirect();await Shot("profile");profile.Close();
                }
                menu.CloseMenu();menu.OpenPetsDirect();await Task.Delay(350);var csv=new System.Collections.Generic.List<string>{"frame,main_thread_ns,gc_bytes"};
                using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1)){
                    double end=Time.realtimeSinceStartupAsDouble+8;int last=-1;while(csv.Count<121&&Time.realtimeSinceStartupAsDouble<end){await Task.Delay(16);if(last==Time.frameCount)continue;last=Time.frameCount;csv.Add(last+","+cpu.LastValue+","+gc.LastValue);}
                }
                File.WriteAllLines(root+"/profiler.csv",csv);File.WriteAllLines(root+"/checks.txt",log);File.WriteAllText(root+"/status.txt","COMPLETE actual Windows player; callback-driven smoke checks; mouse/keyboard tested separately in Editor");
            }catch(Exception e){File.WriteAllText(root+"/status.txt","FAILED "+e);Debug.LogException(e);}finally{Application.Quit();}
        }
    }
}
#endif
