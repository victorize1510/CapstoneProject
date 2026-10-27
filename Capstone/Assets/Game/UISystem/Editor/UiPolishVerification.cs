using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Object = UnityEngine.Object;
using Button = UnityEngine.UIElements.Button;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Explicit, bounded real-engine capture. Transactions use the existing isolated-save hook.</summary>
    public static class UiPolishVerification {
        public const string Root = QaArtifactPaths.Root + "ui-polish-2026-10-22/";
        const string Request = "Temp/UiPolish.request";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static bool running;
        static string stage;
        static readonly List<string> checks = new List<string>();
        [Serializable] sealed class Box { public string id; public float x,y,width,height; }
        [Serializable] sealed class Bounds { public int width,height; public Box[] boxes; }
        [InitializeOnLoadMethod] static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;}
        static void Poll(){
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||!File.Exists(Request))return;
            if(!EditorApplication.isPlaying){if(!EditorApplication.isPlayingOrWillChangePlaymode){
                foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{"Assets/Game/UISystem/Resources/UI/Polish20261022"})){var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer!=null&&importer.textureType!=TextureImporterType.Sprite){importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.SaveAndReimport();}}
                File.WriteAllText(InteractionR3SaveIsolation.Flag,"isolated");EditorApplication.EnterPlaymode();}return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;
            stage=File.ReadAllText(Request).Trim();File.Delete(Request);if(stage.StartsWith("native"))RunNative();else Run();
        }
        static void Check(bool valid,string message){checks.Add((valid?"PASS ":"FAIL ")+message);File.WriteAllLines(Root+stage+"/checks.txt",checks);}
        static void Click(VisualElement root,string id){var b=root.Q<Button>(id);if(b==null||!b.enabledInHierarchy)throw new Exception("Unavailable "+id);typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        static async Task Shot(string name,GameMenuController menu,VisualElement ui){
            await Task.Delay(220);Canvas.ForceUpdateCanvases();var boxes=new List<Box>();
            var menuRoot=typeof(GameMenuController).GetField("root",Any).GetValue(menu) as RectTransform;
            if(menu.IsMenuOpen&&menuRoot!=null)foreach(var rect in menuRoot.GetComponentsInChildren<RectTransform>()){
                var corners=new Vector3[4];rect.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(null,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(null,corners[2]);
                boxes.Add(new Box{id=rect.name,x=a.x,y=Screen.height-b.y,width=b.x-a.x,height=b.y-a.y});
            }
            else {float scale=Screen.width/ui.worldBound.width;foreach(var e in ui.Query<VisualElement>().ToList()){
                if(e.resolvedStyle.display==DisplayStyle.None||e.worldBound.width<=0||e.worldBound.height<=0)continue;
                if(string.IsNullOrEmpty(e.name)&&!(e is Label))continue;var r=e.worldBound;
                boxes.Add(new Box{id=string.IsNullOrEmpty(e.name)?"label:"+(e as Label)?.text:e.name,x=r.x*scale,y=r.y*scale,width=r.width*scale,height=r.height*scale});
            }}
            string stem=Root+stage+"/"+name+"-"+Screen.width+"x"+Screen.height;
            File.WriteAllText(stem+".json",JsonUtility.ToJson(new Bounds{width=Screen.width,height=Screen.height,boxes=boxes.ToArray()},true));
            ScreenCapture.CaptureScreenshot(stem+".png");await Task.Delay(250);
        }
        static async Task Measure(string phase,GameMenuController menu,PetPartyPanelController party,MonsterInventoryController inventory){
            menu.CloseMenu();inventory.Close();if(phase=="menu")menu.OpenMenu();if(phase=="companion")menu.OpenPetsDirect();await Task.Delay(300);
            using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1))
            using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1)){
                var rows=new List<string>{"frame,cpu_ns,gc_bytes"};int previous=-1,warmup=0,samples=0;double deadline=EditorApplication.timeSinceStartup+15;
                while(samples<180&&EditorApplication.isPlaying&&EditorApplication.timeSinceStartup<deadline){await Task.Delay(8);if(previous==Time.frameCount)continue;previous=Time.frameCount;if(warmup++<60)continue;rows.Add(Time.frameCount+","+(cpu.Valid?cpu.LastValue:-1)+","+(gc.Valid?gc.LastValue:-1));samples++;}
                File.WriteAllLines(Root+stage+"/profile-"+Screen.width+"-"+phase+".csv",rows);Check(samples==180,"ProfilerRecorder actual frames "+Screen.width+" "+phase+" cpu="+cpu.Valid+" gc="+gc.Valid);
            }
        }
        static async void RunNative(){
            running=true;string folder=Root+stage;Directory.CreateDirectory(folder);
            var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();var ui=inventory.GetComponent<UIDocument>().rootVisualElement;
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);bool max=view.maximized;int shot=0;
            var events=new List<string>{"utc,event,target"};var boxes=new List<string>{"sample,id,part,x,y,width,height"};int sample=0;
            bool Visible(VisualElement e){for(var p=e;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None)return false;return e.worldBound.width>0;}
            EventCallback<PointerDownEvent> down=e=>{events.Add(DateTime.UtcNow.ToString("O")+",PointerDown,"+((e.target as VisualElement)?.GetFirstAncestorOfType<Button>()?.name??(e.target as VisualElement)?.name));ui.schedule.Execute(()=>ScreenCapture.CaptureScreenshot(folder+"/frame-"+(shot++%QaArtifactPaths.MaxRetainedFrames).ToString("D2")+".png")).StartingIn(180);};
            EventCallback<KeyDownEvent> key=e=>events.Add(DateTime.UtcNow.ToString("O")+",KeyDown,"+e.keyCode);
            ui.RegisterCallback(down,TrickleDown.TrickleDown);ui.RegisterCallback(key,TrickleDown.TrickleDown);
            try{
                view.maximized=true;TrainerMenuPolishCapture.SelectSize(view,selected,stage.EndsWith("1920")?new Vector2Int(1920,1080):new Vector2Int(1366,768));view.Focus();await Task.Delay(800);inventory.Close();menu.CloseMenu();menu.OpenMenu();
                var menuRoot=typeof(GameMenuController).GetField("root",Any).GetValue(menu) as RectTransform;
                var menuButtons=menuRoot.GetComponentsInChildren<GuiKitButton>(true);
                foreach(var b in menuButtons)b.onClick.AddListener(()=>events.Add(DateTime.UtcNow.ToString("O")+",uGUI-Click,"+b.name));
                double deadline=EditorApplication.timeSinceStartup+1800;
                while(EditorApplication.isPlaying&&EditorApplication.timeSinceStartup<deadline&&!File.Exists("Temp/UiPolishNative.stop")){
                    foreach(var b in ui.Query<AngularButton>().ToList()){
                        if(!Visible(b))continue;
                        foreach(var pair in new[]{("button",(VisualElement)b),("icon",b.Q(className:"companion-icon")),("label",(VisualElement)b.Query<Label>().ToList().FirstOrDefault(l=>!string.IsNullOrEmpty(l.text)&&l.resolvedStyle.display!=DisplayStyle.None))}){var e=pair.Item2;if(e==null)continue;var r=e.worldBound;boxes.Add(FormattableString.Invariant($"{sample},{b.name},{pair.Item1},{r.x},{r.y},{r.width},{r.height}"));}
                    }
                    if(menu.IsMenuOpen)foreach(var b in menuButtons){var label=b.GetComponentInChildren<UnityEngine.UI.Text>();if(label==null)continue;var corners=new Vector3[4];label.rectTransform.GetWorldCorners(corners);boxes.Add(FormattableString.Invariant($"{sample},{b.name},label,{corners[0].x},{corners[0].y},{corners[2].x-corners[0].x},{corners[2].y-corners[0].y}"));}
                    var mouse=Mouse.current;if(mouse!=null&&mouse.leftButton.isPressed)events.Add(DateTime.UtcNow.ToString("O")+",MouseHeld,"+mouse.position.ReadValue());
                    if(boxes.Count>12000)boxes.RemoveRange(1,boxes.Count-12000);File.WriteAllLines(folder+"/native-events.csv",events);File.WriteAllLines(folder+"/native-bounds.csv",boxes);sample++;await Task.Delay(250);
                }
                File.WriteAllText(folder+"/complete.txt","Physical input observer; no handler invocation by this observer. See native-events.csv for actual input coverage. Bounded 1800 seconds; maximum 10 rolling captures.");
            }catch(Exception e){File.WriteAllText(folder+"/error.txt",e.ToString());}finally{
                ui.UnregisterCallback(down,TrickleDown.TrickleDown);ui.UnregisterCallback(key,TrickleDown.TrickleDown);inventory.Close();menu.CloseMenu();selected.SetValue(view,previous);view.maximized=max;running=false;EditorApplication.ExitPlaymode();
                if(File.Exists("Temp/UiPolishNative.stop"))File.Delete("Temp/UiPolishNative.stop");if(File.Exists(InteractionR3SaveIsolation.Flag))File.Delete(InteractionR3SaveIsolation.Flag);
            }
        }
        static async void Run(){
            running=true;checks.Clear();Directory.CreateDirectory(Root+stage);
            var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();var party=inventory.GetComponent<PetPartyPanelController>();var ui=inventory.GetComponent<UIDocument>().rootVisualElement;
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);bool maximized=view.maximized;
            try{
                await Task.Delay(1200);view.maximized=true;view.Focus();
                File.WriteAllText(Root+stage+"/environment.txt","Actual Unity Editor Play; programmatic navigation, physical input separate.\nUnity="+Application.unityVersion+"\nGPU="+SystemInfo.graphicsDeviceName+"\nScene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"\n");
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    TrainerMenuPolishCapture.SelectSize(view,selected,size);await Task.Delay(700);Check(Screen.width==size.x&&Screen.height==size.y,"actual Game view "+size);
                    inventory.Close();menu.CloseMenu();menu.OpenMenu();EventSystem.current?.SetSelectedGameObject(null);await Shot("01-menu",menu,ui);menu.CloseMenu();menu.OpenPetsDirect();
                    foreach(int tab in new[]{0,1,2}){party.ShowCompanionTab(tab);await Task.Delay(250);await Shot("0"+(tab+2)+"-tab",menu,ui);}
                    party.ShowCompanionTab(0);await Task.Delay(250);
                    foreach(var action in new[]{new[]{"pets-details-button","05-details"},new[]{"pets-level-up-button","06-level"},new[]{"pets-heal-button","07-heal"},new[]{"pets-rename-button","08-rename"}}){Click(ui,action[0]);await Shot(action[1],menu,ui);menu.HandleShortcut(KeyCode.Escape);await Task.Delay(180);}
                    if(stage=="after"){
                        foreach(string id in new[]{"pets-tab-overview","pets-tab-skills","pets-tab-growth","pets-rename-button","pets-favorite-button","pets-level-up-button","pets-heal-button"}){
                            var button=ui.Q<AngularButton>(id);if(button==null)continue;
                            Check(!button.ContainsPoint(new Vector2(1,1)),"polygon excludes transparent corner "+id);
                            var picked=button.panel.Pick(button.LocalToWorld(new Vector2(1,1)));
                            Check(picked!=button&&picked?.GetFirstAncestorOfType<Button>()!=button,"panel picking excludes transparent corner "+id);
                            var label=button.Query<Label>().ToList().FirstOrDefault(l=>!string.IsNullOrEmpty(l.text));var icon=button.Q(className:"companion-icon");var r=button.worldBound;var textBounds=label?.worldBound;var iconBounds=icon?.worldBound;
                            button.Surface.Hovered=true;button.Surface.AnimateState();await Task.Delay(180);button.Surface.Pressed=true;button.Surface.AnimateState();await Task.Delay(110);button.Surface.IsFocused=true;button.Surface.InvalidateFeedback();
                            Check(button.worldBound==r&&(label==null||label.worldBound==textBounds)&&(icon==null||icon.worldBound==iconBounds),"paint hover/press/focus keeps geometry "+id);
                            button.Surface.StopFeedback();button.Surface.InvalidateFeedback();
                        }
                        var actions=ui.Q("companion-actions");var details=ui.Q("pets-details-button");var inspector=ui.Q("companion-inspector");
                        Check(!actions.worldBound.Overlaps(details.worldBound),"overview footer actions do not overlap details");
                        Check(details.worldBound.yMax<=inspector.worldBound.yMax-8,"details button has bottom clearance");
                        for(int i=0;i<20;i++)party.ShowCompanionTab(i%3);await Task.Delay(300);Check(party.CompanionTab==1,"rapid tab last request wins");party.ShowCompanionTab(0);
                        Click(ui,"pets-rename-button");var field=ui.Q<TextField>("pets-rename-input");await Task.Delay(100);var inputHit=field.panel.Pick(field.worldBound.center);Check(inputHit==field||field.Contains(inputHit),"rename center hits TextField instead of placeholder");field.value=" ";Check(!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"blank name disabled");field.value="Đặng Ánh Trăng";Check(ui.Q<Button>("pets-rename-save").enabledInHierarchy,"Vietnamese name enabled");menu.HandleShortcut(KeyCode.Escape);
                        party.SetPreview3D(true);await Task.Delay(300);Check(party.HasPreviewTexture,"3D explicitly allocates preview");party.SetPreview3D(false);Check(!party.HasPreviewTexture,"2D releases preview");
                    }
                    foreach(string phase in new[]{"closed","menu","companion"})await Measure(phase,menu,party,inventory);
                }
                inventory.Close();menu.CloseMenu();Check(!party.HasPreviewTexture,"final close owns no preview texture");checks.Add("COMPLETE engine capture + ProfilerRecorder; no positive transactions or native input implied.");
            }catch(Exception e){checks.Add("ERROR "+e);Debug.LogException(e);}finally{
                inventory.Close();menu.CloseMenu();selected.SetValue(view,previous);view.maximized=maximized;File.WriteAllLines(Root+stage+"/checks.txt",checks);running=false;
                EditorApplication.ExitPlaymode();if(File.Exists(InteractionR3SaveIsolation.Flag))File.Delete(InteractionR3SaveIsolation.Flag);
            }
        }
    }
}
