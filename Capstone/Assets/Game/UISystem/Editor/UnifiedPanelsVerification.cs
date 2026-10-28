using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.HudSystem;
using Capstone.Game.ProfileSystem;
using Capstone.Game.QuestSystem.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;
namespace Capstone.Game.UISystem.Editor {
    public static partial class UnifiedPanelsVerification {
        const string Root=UnifiedPanelsQaIsolation.Root,Session="Capstone.UnifiedPanelsQA";
        static readonly BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static bool running;static double nextPoll;static readonly List<string> log=new List<string>();
        static object Get(object o,string f)=>o.GetType().GetField(f,Any).GetValue(o);
        static void Set(object o,string f,object v)=>o.GetType().GetField(f,Any).SetValue(o,v);
        static object Call(object o,string m,params object[] args)=>o.GetType().GetMethod(m,Any).Invoke(o,args);
        static void Check(bool pass,string message){log.Add((pass?"PASS ":"FAIL ")+message);File.WriteAllLines(Root+(SessionState.GetString(Session+"phase","")=="capture"?"capture-checks.txt":"checks.txt"),log);if(!pass)throw new Exception(message);}
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
        static void Poll(){if(EditorApplication.timeSinceStartup<nextPoll)return;nextPoll=EditorApplication.timeSinceStartup+.4;
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists(Root+"refresh.request")){File.Delete(Root+"refresh.request");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);return;}
            if(!File.Exists(Root+"request.txt"))return;string phase=File.ReadAllText(Root+"request.txt").Trim();File.Delete(Root+"request.txt");
            UnifiedPanelsAssetImporter.Configure();SessionState.SetString(Session+"phase",phase);SessionState.SetBool(Session,true);File.WriteAllText(Root+"isolate.flag","Unified panels isolated QA");
            SessionState.SetBool(Session+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;EditorApplication.EnterPlaymode();}
        static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Session,false)&&!running)Run();if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Session+"restore",false)){SessionState.SetBool(Session+"restore",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);}}
        static async void Run(){running=true;log.Clear();string phase=SessionState.GetString(Session+"phase","");
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var size=view.GetType().GetProperty("selectedSizeIndex",Any);int old=(int)size.GetValue(view);bool max=view.maximized;
            GameMenuController menu=null;MonsterInventoryController bag=null;ProfilePanelController profile=null;
            try{await Task.Delay(1500);menu=Object.FindFirstObjectByType<GameMenuController>();bag=Object.FindFirstObjectByType<MonsterInventoryController>();profile=Object.FindFirstObjectByType<ProfilePanelController>();var ui=bag.GetComponent<UIDocument>().rootVisualElement;view.maximized=true;view.Focus();
                if(phase.StartsWith("native")){TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1366,768));await Native(menu,bag,ui,phase);return;}
                bool verify=phase=="verify";string prefix=verify||phase=="capture"?"final":phase;
                foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    TrainerMenuPolishCapture.SelectSize(view,size,resolution);await Task.Delay(350);menu.OpenPetsDirect();await Shot(ui,prefix+"-pet");if(verify){Header(ui,"pets-panel","pets-back-button");await PetChecks(menu,ui);}bag.Close();await Task.Delay(200);
                    menu.OpenMenu();await Task.Delay(200);menu.OpenInventoryDirect();await Task.Delay(250);if(bag.SelectedItem==null)bag.SelectNextItem();await BagMode(bag,ui,false);PickBack(bag,ui,prefix+"-"+resolution.x);await Shot(ui,prefix+"-bag-grid");await BagMode(bag,ui,true);await Shot(ui,prefix+"-bag-list");await BagMode(bag,ui,false);if(verify){Header(ui,"inventory-panel","cancel-button");await BagChecks(menu,bag,ui);}bag.Close();await Task.Delay(200);
                    menu.OpenQuestDirect();var quest=Object.FindFirstObjectByType<QuestPanelController>();Set(quest,"completedSectionExpanded",true);Call(quest,"SelectTab",Enum.Parse(Get(quest,"currentTab").GetType(),"Main"));await Shot(ui,prefix+"-quest");if(verify){Header(ui,"quest-panel","quest-back-button");await QuestChecks(menu,quest,ui);}bag.Close();await Task.Delay(200);
                    menu.OpenProfileDirect();await ProfileShot(profile,prefix+"-profile");if(verify)await ProfileChecks(profile,ui);profile.Close();await Task.Delay(200);
                }
                if(verify){TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1600,1000));menu.OpenPetsDirect();await Task.Delay(250);await OpaquePet(ui);bag.Close();TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1366,768));await ProfileAll(menu,bag,profile);}
                File.WriteAllText(Root+"status.txt","COMPLETE "+phase);
            }catch(Exception e){File.WriteAllText(Root+"status.txt","FAILED "+e);Debug.LogException(e);}finally{profile?.Close();bag?.Close();menu?.CloseMenu();size.SetValue(view,old);view.maximized=max;if(!phase.StartsWith("native"))File.WriteAllLines(Root+(phase=="capture"?"capture-checks.txt":"checks.txt"),log);SessionState.SetBool(Session,false);SessionState.SetBool(Session+"restore",true);running=false;if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");EditorApplication.ExitPlaymode();}}
        static string PathOf(VisualElement e){var result=new List<string>();for(;e!=null;e=e.parent)result.Add(string.IsNullOrEmpty(e.name)?e.GetType().Name:e.name);return string.Join(" <- ",result);}
        static void PickBack(MonsterInventoryController bag,VisualElement ui,string phase){var button=ui.Q<Button>("cancel-button");var picked=button.panel.Pick(button.worldBound.center);var callbacks=(System.Collections.IDictionary)Get(bag,"controlCallbacks");
            File.WriteAllText(Root+phase+"-back-hit.txt","center="+button.worldBound.center+"\nbounds="+button.worldBound+"\npick="+PathOf(picked)+"\nsameDisplayedCached="+ReferenceEquals(button,Get(bag,"cancelButton"))+"\ncontrolsRootCurrent="+ReferenceEquals(ui,Get(bag,"controlsRoot"))+"\nregistered="+Get(bag,"controlsRegistered")+"\ncallbackExists="+callbacks.Contains(button)+"\nenabled="+button.enabledInHierarchy+"\nduplicates="+ui.Query<Button>("cancel-button").ToList().Count);}
        [Serializable]sealed class Box {public string name,text;public float x,y,w,h,font;}
        [Serializable]sealed class Frame {public int width,height;public Box[] boxes;}
        static async Task Shot(VisualElement ui,string name){await Task.Delay(250);typeof(Capstone.Game.Inventory.Bag.BagPresentation).Assembly.GetType("Capstone.Game.Inventory.Bag.BagTooltip").GetMethod("HideAll",BindingFlags.Static|BindingFlags.Public).Invoke(null,new object[]{ui});float s=Screen.width/ui.worldBound.width;var boxes=ui.Query<VisualElement>().ToList().Where(e=>!string.IsNullOrEmpty(e.name)&&e.worldBound.width>0&&Visible(e)).Select(e=>new Box{name=e.name,text=e is Label l?l.text:"",x=e.worldBound.x*s,y=e.worldBound.y*s,w=e.worldBound.width*s,h=e.worldBound.height*s,font=e.resolvedStyle.fontSize}).ToArray();WriteFrame(name,boxes);await Task.Delay(280);}
        static bool Visible(VisualElement e){for(var p=e;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None)return false;return true;}
        static void WriteFrame(string name,Box[] boxes){string path=Root+name+"-"+Screen.width+"x"+Screen.height;File.WriteAllText(path+".json",JsonUtility.ToJson(new Frame{width=Screen.width,height=Screen.height,boxes=boxes},true));ScreenCapture.CaptureScreenshot(path+".png");}
        static async Task ProfileShot(ProfilePanelController profile,string name){await Task.Delay(250);Canvas.ForceUpdateCanvases();var root=(RectTransform)Get(profile,"root");var camera=root.GetComponentInParent<Canvas>().worldCamera;var boxes=new List<Box>();foreach(var r in root.GetComponentsInChildren<RectTransform>(false)){var corners=new Vector3[4];r.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);var t=r.GetComponent<UnityEngine.UI.Text>();boxes.Add(new Box{name=r.name,text=t?.text,font=t!=null?t.fontSize:0,x=a.x,y=Screen.height-b.y,w=b.x-a.x,h=b.y-a.y});}WriteFrame(name,boxes.ToArray());await Task.Delay(280);}
    }
}
