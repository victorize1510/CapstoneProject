using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button=UnityEngine.UI.Button;
using Object=UnityEngine.Object;

namespace Capstone.Game.ProfileSystem.Editor {
    public static class ProfileR2Verification {
        const string Root=ProfileR2QaIsolation.Root,Session="Capstone.ProfileR2.QA";
        const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static bool running;static readonly List<string> log=new List<string>();
        static object Get(object owner,string field)=>owner.GetType().GetField(field,Any).GetValue(owner);
        static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,Any).SetValue(owner,value);
        static object Call(object owner,string method,params object[] args)=>owner.GetType().GetMethod(method,Any).Invoke(owner,args);
        static void Check(bool pass,string text){log.Add((pass?"PASS ":"FAIL ")+text);File.WriteAllLines(Root+"checks.txt",log);if(!pass)throw new Exception(text);}
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Poll;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged-=State;EditorApplication.playModeStateChanged+=State;}
        static void Poll(){if(SessionState.GetBool(Session+"manual",false)&&EditorApplication.isPlaying){
                if(EditorApplication.timeSinceStartup>SessionState.GetFloat(Session+"manualEnd",0)||File.Exists(Root+"manual-stop.request")){if(File.Exists(Root+"manual-stop.request"))File.Delete(Root+"manual-stop.request");EditorApplication.ExitPlaymode();}return;}
            if(running||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(File.Exists(Root+"refresh.request")){File.Delete(Root+"refresh.request");AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);return;}
            // A revision-specific request prevents the previously loaded verifier from consuming it before this assembly imports.
            bool manual=File.Exists(Root+"manual.request");if(!manual&&!File.Exists(Root+"verify-r2.request"))return;
            File.Delete(Root+(manual?"manual.request":"verify-r2.request"));File.WriteAllText(Root+"isolate.flag","isolated Profile QA");SessionState.SetBool(Session,true);SessionState.SetBool(Session+"manual",manual);
            SessionState.SetBool(Session+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Session,false)&&!running){if(SessionState.GetBool(Session+"manual",false))OpenManual();else Run();}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Session+"manual",false)){SessionState.SetBool(Session+"manual",false);SessionState.SetBool(Session,false);SessionState.SetBool(Session+"restore",true);running=false;if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");File.WriteAllText(Root+"manual-status.txt","STOPPED; isolated native-input session ended");}
            if(state==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Session+"restore",false)){SessionState.SetBool(Session+"restore",false);EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);}}
        static async void OpenManual(){running=true;SessionState.SetFloat(Session+"manualEnd",(float)EditorApplication.timeSinceStartup+300);await Task.Delay(1500);if(!EditorApplication.isPlaying)return;Object.FindFirstObjectByType<GameMenuController>().OpenProfileDirect();File.WriteAllText(Root+"manual-status.txt","READY; isolated native-input session, maximum 5 minutes");}
        [Serializable]sealed class Box{public string name;public float x,y,w,h;public string text;}
        [Serializable]sealed class Frame{public int width,height;public Box[] bounds;}
        static async Task Shot(ProfilePanelController profile,string name){
            await Task.Delay(260);Canvas.ForceUpdateCanvases();var root=(RectTransform)Get(profile,"root");var camera=root.GetComponentInParent<Canvas>().worldCamera;var boxes=new List<Box>();
            foreach(var r in root.GetComponentsInChildren<RectTransform>(false)){var c=new Vector3[4];r.GetWorldCorners(c);var a=RectTransformUtility.WorldToScreenPoint(camera,c[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,c[2]);boxes.Add(new Box{name=r.name,x=a.x,y=Screen.height-b.y,w=b.x-a.x,h=b.y-a.y,text=r.GetComponent<Text>()?.text});}
            foreach(var title in root.GetComponentsInChildren<Text>(false).Where(t=>t.name=="Title"))Check(title.cachedTextGenerator.vertexCount>0,"visible title mesh: "+title.text+" at "+Screen.width+"x"+Screen.height);
            string path=Root+name+"-"+Screen.width+"x"+Screen.height;File.WriteAllText(path+".json",JsonUtility.ToJson(new Frame{width=Screen.width,height=Screen.height,bounds=boxes.ToArray()},true));ScreenCapture.CaptureScreenshot(path+".png");await Task.Delay(280);
        }
        static void Click(ProfilePanelController profile,string name){var root=(RectTransform)Get(profile,"root");var button=root.GetComponentsInChildren<Button>(false).First(b=>b.name==name);EventSystem.current.SetSelectedGameObject(button.gameObject);button.onClick.Invoke();}
        static async Task ProfileCounters(){
            var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions=handles.Select(h=>new{handle=h,desc=ProfilerRecorderHandle.GetDescription(h)}).ToArray();
            string[] wanted={"Main Thread","GC Allocated In Frame","Canvas.BuildBatch","Canvas.RenderOverlays","UI Layout::Layout","UI Render::Render"};
            var recorders=new List<ProfilerRecorder>();var names=new List<string>();var csv=new List<string>();
            try{foreach(string name in wanted){var found=descriptions.FirstOrDefault(x=>x.desc.Name==name);if(found==null){log.Add("COUNTER UNAVAILABLE "+name);continue;}var recorder=new ProfilerRecorder(found.handle,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame);if(!recorder.Valid){recorder.Dispose();continue;}recorders.Add(recorder);names.Add(name+" ["+found.desc.UnitType+"]");}
                csv.Add("frame,"+string.Join(",",names));int last=-1;double end=Time.realtimeSinceStartupAsDouble+10;for(int i=0;i<120&&Time.realtimeSinceStartupAsDouble<end;){await Task.Delay(16);if(Time.frameCount==last)continue;last=Time.frameCount;csv.Add(last+","+string.Join(",",recorders.Select(r=>r.LastValue)));i++;}
                File.WriteAllLines(Root+"profiler-editor.csv",csv);log.Add("PROFILER: "+(csv.Count-1)+" actual Editor Play Mode samples (10 second cap), whole-frame counters; not standalone and not UI-only attribution.");
            }finally{foreach(var recorder in recorders)recorder.Dispose();}
        }
        static async void Run(){
            running=true;log.Clear();log.Add(DateTime.UtcNow.ToString("O")+" Actual Unity Editor Play Mode; normal screens use live provider snapshots, scenarios are explicitly labelled.");
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var index=view.GetType().GetProperty("selectedSizeIndex",Any);int old=(int)index.GetValue(view);bool maximized=view.maximized;
            ProfilePanelController profile=null;GameMenuController menu=null;PlayerProfileRuntimeProvider provider=null;GameObject scenarioObject=null;
            try{
                await Task.Delay(1500);menu=Object.FindFirstObjectByType<GameMenuController>();profile=Object.FindFirstObjectByType<ProfilePanelController>();provider=Object.FindFirstObjectByType<PlayerProfileRuntimeProvider>();
                Check(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(s=>Path.GetFullPath(s.SavePath).StartsWith(Path.GetFullPath(Root),StringComparison.OrdinalIgnoreCase)),"all real save stores redirected to task E folder");
                var original=provider.GetSnapshot();int avatarIndex=provider.SelectedAvatarIndex;var originalTeam=(MonoBehaviour)Get(profile,"teamProviderSource");view.maximized=true;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,index,size);view.Focus();await Task.Delay(400);menu.OpenProfileDirect();await Shot(profile,"01-profile");
                    var beforeName=provider.GetSnapshot().DisplayName;Click(profile,"ChangeNameButton");await Task.Delay(180);var input=(InputField)Get(profile,"nameInput");input.text="A";Click(profile,"ConfirmButton");Check(((RectTransform)Get(profile,"nameDialog")).gameObject.activeSelf&&provider.GetSnapshot().DisplayName==beforeName,"invalid one-character name keeps popup and data at "+size);
                    await Shot(profile,"02-rename-invalid");await Task.Delay(180);input.text="WWWWWWWWWWWWWWWWWWWW";await Shot(profile,"03-rename");Click(profile,"ConfirmButton");Check(provider.GetSnapshot().DisplayName=="WWWWWWWWWWWWWWWWWWWW"&&!((RectTransform)Get(profile,"nameDialog")).gameObject.activeSelf,"20 character rename commits through live provider at "+size);await Shot(profile,"04-long-name");
                    provider.TrySetDisplayName(original.DisplayName,out _);await Task.Delay(180);Click(profile,"AvatarCameraButton");await Shot(profile,"05-avatar-picker");Check(((RectTransform)Get(profile,"avatarDialog")).gameObject.activeSelf,"camera opens original avatar picker at "+size);
                    var root=(RectTransform)Get(profile,"root");var options=root.GetComponentsInChildren<Button>(false).Where(b=>b.name.StartsWith("AvatarOption_")).ToArray();Check(options.Length==provider.AvatarOptions.Count(s=>s!=null),"picker contains exactly valid scene sprites");
                    var picked=options.First(b=>b.name!="AvatarOption_"+provider.SelectedAvatarIndex);int chosen=int.Parse(picked.name.Substring("AvatarOption_".Length));await Task.Delay(180);picked.onClick.Invoke();Check(provider.SelectedAvatarIndex==chosen&&!((RectTransform)Get(profile,"avatarDialog")).gameObject.activeSelf,"avatar applies immediately and closes without extra confirmation");provider.TrySelectAvatar(avatarIndex);
                    await Task.Delay(180);Click(profile,"ChangeAvatarButton");Check(profile.TryClosePopup()&&profile.IsOpen,"Back closes popup before profile");Check(EventSystem.current.currentSelectedGameObject.name=="ChangeAvatarButton","popup restores focus to avatar trigger");
                    var team=(IPetHudProvider)Get(profile,"teamProvider");var active=team.GetPetSlots().Select(s=>s.selected).ToArray();await Task.Delay(180);Click(profile,"TeamPetSlot_01");Check(active.SequenceEqual(team.GetPetSlots().Select(s=>s.selected)),"profile pet click never changes active pet");Check(root.GetComponentsInChildren<Button>(false).First(b=>b.name=="TeamPetSlot_01").GetComponent<ProfileR2Feedback>().Selected,"pet selection contour updates immediately without waiting for periodic refresh");
                    scenarioObject=new GameObject("ProfileR2 controlled UI scenario");var scenario=scenarioObject.AddComponent<ProfileR2Scenario>();scenario.Source=provider;scenario.Portrait=team.GetPetSlots().First(s=>s.icon!=null).icon;
                    profile.Configure(root.GetComponentInParent<Canvas>(),scenario);Set(profile,"teamProviderSource",scenario);Call(profile,"ResolveTeamProvider");Call(profile,"Refresh");await Shot(profile,"06-scenario-missing-empty");
                    Check(!((Button)Get(profile,"avatarCameraButton")).interactable,"empty avatar options disable picker action");scenario.Six=true;profile.Configure(root.GetComponentInParent<Canvas>(),provider);Call(profile,"Refresh");await Shot(profile,"07-scenario-six-pets");
                    Set(profile,"teamProviderSource",originalTeam);Call(profile,"ResolveTeamProvider");Call(profile,"Refresh");Object.Destroy(scenarioObject);scenarioObject=null;
                    var button=root.GetComponentsInChildren<Button>(true).First(b=>b.name=="ChangeAvatarButton");var feedback=button.GetComponent<ProfileR2Feedback>();var before=button.GetComponent<RectTransform>().rect;
                    feedback.OnPointerEnter(new PointerEventData(EventSystem.current));await Task.Delay(180);feedback.OnSelect(new BaseEventData(EventSystem.current));await Task.Delay(180);Check(button.GetComponent<RectTransform>().rect==before,"hover/focus preserve button geometry");feedback.OnPointerExit(new PointerEventData(EventSystem.current));feedback.OnDeselect(new BaseEventData(EventSystem.current));
                    profile.Close();
                }
                menu.OpenProfileDirect();await Task.Delay(300);await ProfileCounters();profile.Close();
                var bag=Object.FindFirstObjectByType<MonsterInventoryController>();foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,index,size);await Task.Delay(400);bag.OpenInventoryPanel();var ui=bag.GetComponent<UIDocument>().rootVisualElement;
                    var listField=bag.GetType().GetField("listView",Any);if((bool)listField.GetValue(bag))BagVerification.Click(ui,"inventory-view-button");await BagVerification.Shot(ui,Root,"08-bag-grid-gap");BagVerification.Click(ui,"inventory-view-button");await BagVerification.Shot(ui,Root,"09-bag-list-gap");bag.Close();}
                Check(!provider.TrySetDisplayName("X",out _)&&!provider.TrySetDisplayName(new string('W',21),out _),"provider retains 2–20 validation rule");provider.TrySetDisplayName("  AB  ",out _);Check(provider.GetSnapshot().DisplayName=="AB","provider trims before validation");provider.TrySetDisplayName(original.DisplayName,out _);provider.TrySelectAvatar(avatarIndex);
                log.Add("COMPLETE runtime captures + interactions + actual Profiler counters. No standalone run.");
            }catch(Exception e){log.Add("FAILED "+e);Debug.LogException(e);}finally{
                if(scenarioObject!=null)Object.Destroy(scenarioObject);profile?.Close();menu?.CloseMenu();index.SetValue(view,old);view.maximized=maximized;
                File.WriteAllLines(Root+"checks.txt",log);File.WriteAllText(Root+"status.txt",log.Any(s=>s.StartsWith("FAIL"))?"FAILED":"COMPLETE");SessionState.SetBool(Session,false);SessionState.SetBool(Session+"restore",true);running=false;if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");EditorApplication.ExitPlaymode();
            }
        }
    }
    public sealed class ProfileR2Scenario:MonoBehaviour,IPlayerProfileProvider,IPetHudProvider {
        public IPlayerProfileProvider Source;public bool Six;public Sprite Portrait;public event Action ProfileChanged{add{}remove{}}public event Action HudDataChanged{add{}remove{}}
        public bool CanChangeAvatar=>false;public IReadOnlyList<Sprite> AvatarOptions=>Array.Empty<Sprite>();public int SelectedAvatarIndex=>-1;
        public PlayerProfileSnapshot GetSnapshot(){var s=Source.GetSnapshot();return new PlayerProfileSnapshot(s.DisplayName,"",0,0,0,null,-1,"","",-1,-1,-1,0,-1,0,-1,-1,null,0);}
        public bool TrySetDisplayName(string value,out string error){error="QA scenario is read-only";return false;}public bool TrySelectAvatar(int index)=>false;public bool TrySelectNextAvatar()=>false;
        public IReadOnlyList<PetSlotHudData> GetPetSlots()=>Enumerable.Range(0,6).Select(i=>new PetSlotHudData{occupied=Six,icon=Six?Portrait:null}).ToArray();public PetStatusHudData GetSelectedPetStatus()=>default;public IReadOnlyList<SkillHudData> GetSkills()=>Array.Empty<SkillHudData>();public void SelectPetSlot(int index){throw new Exception("Profile must not summon");}public void RequestSkill(int index){}
    }
}
