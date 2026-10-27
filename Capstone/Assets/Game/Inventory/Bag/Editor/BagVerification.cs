using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Capstone.Game.Inventory.Bag.Editor {
    public static class BagVerification {
        public const string Root=QaArtifactPaths.Root+"bag-implementation-2026-10-22/";
        const string Session="Capstone.BagQA";
        const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static bool running;static double nextPoll;static readonly List<string> checks=new List<string>();
        public static object Get(object owner,string name)=>owner.GetType().GetField(name,Any).GetValue(owner);
        public static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Any).SetValue(owner,value);
        public static object Call(object owner,string name,params object[] args)=>owner.GetType().GetMethod(name,Any,null,args.Select(a=>a.GetType()).ToArray(),null).Invoke(owner,args);
        public static void Click(VisualElement root,string id){var b=root.Q<Button>(id);if(b==null||!b.enabledInHierarchy)throw new Exception("Unavailable "+id);typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        public static void Check(bool ok,string label){checks.Add((ok?"PASS ":"FAIL ")+label);File.WriteAllLines(Root+"checks.txt",checks);if(!ok)throw new Exception(label);}
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged-=OnPlay;EditorApplication.playModeStateChanged+=OnPlay;}
        static void Poll(){
            if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+.5;
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Root+"request.txt"))return;
            string stage;try{stage=File.ReadAllText(Root+"request.txt").Trim();File.Delete(Root+"request.txt");}catch(IOException){return;}
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Game/Inventory/Bag/Resources"})){var t=AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;if(t!=null&&t.npotScale!=TextureImporterNPOTScale.None){t.npotScale=TextureImporterNPOTScale.None;t.SaveAndReimport();}}
            SessionState.SetString(Session,stage);File.WriteAllText(Root+"isolate.flag","isolated");EditorApplication.EnterPlaymode();
        }
        static void OnPlay(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&!string.IsNullOrEmpty(SessionState.GetString(Session,""))&&!running)Run();}
        [Serializable]sealed class Box {public string id;public float x,y,w,h;}
        [Serializable]sealed class Boxes {public int width,height;public Box[] controls;}
        public static async Task Shot(VisualElement ui,string folder,string name){
            await Task.Delay(220);var boxes=new List<Box>();float scale=Screen.width/ui.worldBound.width;
            foreach(var e in ui.Query<VisualElement>().ToList()){
                if(string.IsNullOrEmpty(e.name)||e.worldBound.width<=0||e.worldBound.height<=0)continue;
                var b=e.worldBound;boxes.Add(new Box{id=e.name,x=b.x*scale,y=b.y*scale,w=b.width*scale,h=b.height*scale});
            }
            string path=folder+"/"+name+"-"+Screen.width+"x"+Screen.height;File.WriteAllText(path+".json",JsonUtility.ToJson(new Boxes{width=Screen.width,height=Screen.height,controls=boxes.ToArray()},true));
            ScreenCapture.CaptureScreenshot(path+".png");await Task.Delay(300);
        }
        static async void Run(){
            running=true;string stage=SessionState.GetString(Session,"");string folder=Root+stage;Directory.CreateDirectory(folder);checks.Clear();
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var index=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)index.GetValue(view);bool maximized=view.maximized;
            MonsterInventoryController bag=null;
            try{
                await Task.Delay(1200);bag=Object.FindFirstObjectByType<MonsterInventoryController>();var ui=bag.GetComponent<UIDocument>().rootVisualElement;
                Object.FindFirstObjectByType<GameMenuController>()?.CloseMenu();bag.Close();
                Check(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(s=>Path.GetFullPath(s.SavePath).StartsWith(Path.GetFullPath(Root),StringComparison.OrdinalIgnoreCase)),"all player stores isolated on E");
                view.maximized=true;
                if(stage.StartsWith("native")){Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,index,new Vector2Int(1366,768));view.Focus();bag.OpenInventoryPanel();await Native(ui,folder);return;}
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,index,size);view.Focus();await Task.Delay(500);bag.OpenInventoryPanel();
                    if(bag.SelectedItem==null)bag.SelectNextItem();await Shot(ui,folder,"01-grid");
                    Click(ui,"inventory-view-button");await Shot(ui,folder,"02-list");
                    if(stage=="after")foreach(var card in ui.Q("item-grid").Children().OfType<BagButton>()){var key=card.Q<Label>("card-key");var quantity=card.Q<Label>("card-quantity");if(!string.IsNullOrEmpty(key?.text))Check(key.worldBound.xMax<=quantity.worldBound.xMin,"List quick-key bounds do not overlap quantity at "+Screen.width);}
                    Click(ui,"inventory-view-button");
                    if(stage=="after")await BagRuntimeChecks.Screens(bag,ui,folder);
                }
                if(stage=="after"){await BagRuntimeChecks.Transactions(bag,ui,folder);await BagRuntimeChecks.Profile(bag,folder);}
                checks.Add("COMPLETE "+stage+" runtime captures; mockups were not used as runtime evidence.");
            }catch(Exception e){checks.Add("ERROR "+e);Debug.LogException(e);}finally{
                bag?.Close();index.SetValue(view,previous);view.maximized=maximized;File.WriteAllLines(Root+"checks.txt",checks);File.WriteAllText(Root+"status.txt",checks.Any(s=>s.StartsWith("ERROR")||s.StartsWith("FAIL"))?"FAILED":"COMPLETE "+stage);
                SessionState.SetString(Session,"");running=false;if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");EditorApplication.ExitPlaymode();
            }
        }
        static async Task Native(VisualElement ui,string folder){
            var events=new List<string>{"utc,event,target"};var bounds=new List<string>{"sample,state,button,part,x,y,w,h"};int captures=0,sample=0;
            EventCallback<PointerDownEvent> down=e=>{var b=(e.target as VisualElement)?.GetFirstAncestorOfType<Button>()??e.target as Button;events.Add(DateTime.UtcNow.ToString("O")+",PointerDown,"+(b?.name??(e.target as VisualElement)?.name));ui.schedule.Execute(()=>ScreenCapture.CaptureScreenshot(folder+"/frame-"+(captures++%QaArtifactPaths.MaxRetainedFrames).ToString("D2")+".png")).StartingIn(180);};
            EventCallback<KeyDownEvent> key=e=>events.Add(DateTime.UtcNow.ToString("O")+",KeyDown,"+e.keyCode);
            ui.RegisterCallback(down,TrickleDown.TrickleDown);ui.RegisterCallback(key,TrickleDown.TrickleDown);
            File.WriteAllText(folder+"/ready.txt","Native mouse/keyboard observer; maximum 300 seconds and 10 rolling captures.");
            try{double end=Time.realtimeSinceStartupAsDouble+300;while(Time.realtimeSinceStartupAsDouble<end&&!File.Exists(Root+"native.stop")){
                foreach(var b in ui.Query<BagButton>().ToList()){if(b.resolvedStyle.display==DisplayStyle.None)continue;bool visible=true;for(var p=b.parent;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None){visible=false;break;}if(!visible)continue;
                    string state=b.Surface.Pressed?"pressed":b.Surface.Hovered?"hover":b.Surface.Focused?"focus":"normal";
                    foreach(var e in b.Query<Label>().ToList()){if(string.IsNullOrEmpty(e.text))continue;var r=e.worldBound;bounds.Add(FormattableString.Invariant($"{sample},{state},{b.name},{e.name},{r.x},{r.y},{r.width},{r.height}"));}
                }
                if(bounds.Count>10000)bounds.RemoveRange(1,bounds.Count-10000);File.WriteAllLines(folder+"/native-events.csv",events);File.WriteAllLines(folder+"/native-bounds.csv",bounds);sample++;await Task.Delay(180);
            }}finally{ui.UnregisterCallback(down,TrickleDown.TrickleDown);ui.UnregisterCallback(key,TrickleDown.TrickleDown);if(File.Exists(Root+"native.stop"))File.Delete(Root+"native.stop");}
        }
    }
}
