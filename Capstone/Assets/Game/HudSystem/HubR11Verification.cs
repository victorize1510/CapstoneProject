#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unity.Profiling;
using Object=UnityEngine.Object;
namespace Capstone.Game.HudSystem.Editor {
    public sealed class HubR11Verification:MonoBehaviour,ICaptureOpportunitySource {
        public bool HasCaptureOpportunity {get;private set;}
        sealed class FixedRandom:ICaptureRandom {public double NextFloat01()=>.999999;}
        const string Root=QaArtifactPaths.Root+"hub-r11-2026-11-04/",Session="Capstone.HubR11.QA";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static readonly List<string> checks=new List<string>(),geometry=new List<string>();static bool running;
        static void Write(string file,string text){Directory.CreateDirectory(Root);File.WriteAllText(Root+file,text);File.AppendAllText(Root+"owned-paths.txt",Root+file+"\n");}
        static void Check(bool pass,string message){checks.Add((pass?"PASS ":"FAIL ")+message);Write("checks.txt",string.Join("\n",checks));if(!pass)throw new Exception(message);}
        static void Set(object o,string field,object value)=>o.GetType().GetField(field,Any).SetValue(o,value);
        static object Call(object o,string method,params object[] args)=>o.GetType().GetMethod(method,Any).Invoke(o,args);
        [MenuItem("Game Tools/GameToolThang/HUD/R11/Verify isolated runtime")]
        public static void Run()=>Begin(false);
        [MenuItem("Game Tools/GameToolThang/HUD/R11/Manual isolated keyboard check")]
        public static void Manual()=>Begin(true);
        static void Begin(bool manual){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling)return;SessionState.SetBool(Session+"manual",manual);AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.HudSystem.Editor.HubR11AssetImporter")).First(t=>t!=null).GetMethod("Reimport").Invoke(null,null);SessionState.SetString(Session+"save",Root+"test-saves/"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));SessionState.SetBool(Session,true);SessionState.SetBool(Session+"enabled",EditorSettings.enterPlayModeOptionsEnabled);SessionState.SetInt(Session+"options",(int)EditorSettings.enterPlayModeOptions);EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;EditorApplication.EnterPlaymode();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Boot(){if(!SessionState.GetBool(Session,false))return;
            foreach(var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new PlayerJsonFileSaveStore(SessionState.GetString(Session+"save","")));
            foreach(var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))save.SetSaveStore(new QuestJsonFileSaveStore(SessionState.GetString(Session+"save","")));
            new GameObject("Isolated HUB R11 verification",typeof(HubR11Verification)){hideFlags=HideFlags.DontSave};
        }
        static async Task Gap(int ms=220)=>await Task.Delay(ms);
        static async Task Shot(string name){var path=Root+name+".png";File.AppendAllText(Root+"owned-paths.txt",path+"\n");ScreenCapture.CaptureScreenshot(path);await Gap(300);}
        static Vector2 Position(RectTransform r){var corners=new Vector3[4];r.GetWorldCorners(corners);return new Vector2(corners[0].x,Screen.height-corners[1].y);}
        static void Bounds(RectTransform r,float x,float y,float width,float height){Canvas.ForceUpdateCanvases();float scale=Screen.height/1080f;var corners=new Vector3[4];r.GetWorldCorners(corners);var p=Position(r);var size=(Vector2)(corners[2]-corners[0]);float delta=Mathf.Max(Mathf.Abs(p.x-x*scale),Mathf.Abs(p.y-y*scale),Mathf.Abs(size.x-width*scale),Mathf.Abs(size.y-height*scale));geometry.Add(FormattableString.Invariant($"{Screen.width},{r.name},{p.x},{p.y},{size.x},{size.y},{delta}"));Check(delta<1.2f,"geometry <1.2 px "+r.name+" at "+Screen.width+" max="+delta);}
        readonly List<string> physicalEvents=new List<string>();
        void LateUpdate(){if(!SessionState.GetBool(Session+"manual",false))return;foreach(var key in new[]{KeyCode.P,KeyCode.G,KeyCode.Escape})if(Input.GetKeyDown(key)||Input.GetKeyUp(key)){var hud=GameplayHudController.Live;var visual=Object.FindFirstObjectByType<PlayerVisualSwitcher>();physicalEvents.Add($"frame={Time.frameCount} key={key} down={Input.GetKeyDown(key)} up={Input.GetKeyUp(key)} shift={HubCaptureHud.ModelSwitchModifier} party={hud?.IsPartyOpen} picker={hud?.GetComponent<HubCaptureHud>()?.IsPickerOpen} intents={hud?.GetComponent<HubCaptureHud>()?.ThrowIntents} model={visual?.ActiveVisualIndex}");if(physicalEvents.Count>40)physicalEvents.RemoveAt(0);Write("physical-events.txt",string.Join("\n",physicalEvents));}}
        async void Start(){running=true;checks.Clear();geometry.Clear();geometry.Add("resolution,name,x,y,width,height,maxErrorPx");Write("status.txt","RUNNING "+DateTime.Now);EditorWindow view=null;PropertyInfo size=null;int oldSize=-1;bool wasMaximized=false;
            try {
                await Gap(1800);var hud=Object.FindFirstObjectByType<GameplayHudController>();var input=Object.FindFirstObjectByType<PetCommandInput>();var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryAdapter>();var save=Object.FindFirstObjectByType<PlayerSaveController>();var provider=Object.FindFirstObjectByType<PetCommandHudProvider>();
                Check(hud!=null&&input!=null&&menu!=null,"existing authoritative HUD/input/menu");Check(save.SavePath.StartsWith(Root),"QA save isolated on E");
                if(Capstone.Game.SettingsSystem.GameSettingsService.Instance!=null)Capstone.Game.SettingsSystem.GameSettingsService.Instance.Model.Data.showQuestTracker=true; // In-memory QA preference; no settings storage write.
                var quests=Object.FindFirstObjectByType<QuestManager>();var tracked=quests?.GetActiveQuests().FirstOrDefault();if(tracked==null){var available=quests?.GetAvailableQuestDefinitions().FirstOrDefault();if(available!=null){quests.AcceptQuest(available.QuestId);tracked=quests.GetActiveQuests().FirstOrDefault();}}if(tracked!=null)quests.TrackQuest(tracked.QuestId);
                hud.RebuildHud();await Gap();var capture=hud.GetComponent<HubCaptureHud>();Check(capture!=null&&hud.Radial!=null,"all R11 modules constructed");
                var gesture=new HubPressGesture();Check(gesture.Step(true,true,false,0,.28f)==HubPressGesture.Intent.None,"G press pending");Check(gesture.Step(false,false,true,.1f,.28f)==HubPressGesture.Intent.Tap,"short G emits tap");Check(gesture.Step(false,false,true,.2f,.28f)==HubPressGesture.Intent.None,"duplicate G release no throw");gesture.Step(true,true,false,1,.28f);Check(gesture.Step(false,true,false,1.3f,.28f)==HubPressGesture.Intent.Hold,"hold emits picker");Check(gesture.Step(false,false,true,1.4f,.28f)==HubPressGesture.Intent.ReleaseHold,"release after hold never throws");gesture.Step(true,true,false,2,.28f);gesture.Cancel();Check(gesture.Step(false,false,true,2.1f,.28f)==HubPressGesture.Intent.None,"focus/modal cancel suppresses pending throw");
                var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");view=EditorWindow.GetWindow(type);size=type.GetProperty("selectedSizeIndex",Any);oldSize=(int)size.GetValue(view);view.Show();view.Focus();
                Set(hud,"skillBarDisplayMode",SkillBarDisplayMode.AlwaysVisible);
                if(SessionState.GetBool(Session+"manual",false)){
                    var select=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null);select.GetMethod("SelectSize",Any).Invoke(null,new object[]{view,size,new Vector2Int(1366,768)});wasMaximized=view.maximized;view.maximized=true;view.Focus();inventory.TryAddItemAtomic(CaptureAssets.LoadBall(),3);capture.Refresh();Write("manual-state.txt","READY: isolated save; remains in Play until explicit manual-stop.request or user stops Play. No automatic timeout input session.");
                    while(Application.isPlaying&&!File.Exists(Root+"manual-stop.request"))await Gap(100);
                    if(File.Exists(Root+"manual-stop.request"))File.Delete(Root+"manual-stop.request");Write("manual-state.txt","COMPLETE explicit stop");return;
                }
                foreach(var resolution in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}) {
                    var select=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null);select.GetMethod("SelectSize",Any).Invoke(null,new object[]{view,size,resolution});await Gap(400);
                    Check(Screen.width==resolution.x&&Screen.height==resolution.y,"actual engine resolution "+resolution);var root=hud.PresentationRoot;
                    Bounds((RectTransform)root.Find("MiniMap"),24,24,350,350);Bounds((RectTransform)root.Find("SkillBar"),782,952,356,106);Bounds((RectTransform)root.Find("QuickItems"),1716,952,176,106);Bounds((RectTransform)root.Find("CaptureG"),1764,836,80,80);
                    var center=hud.Radial.IdentityRoot;Vector3 before=center.position;
                    hud.Radial.ResetState();await Gap();await Shot("collapsed-"+resolution.x);
                    Check(menu.HandleShortcut(KeyCode.P)&&hud.IsPartyOpen,"P router opens radial instead of fullscreen Companion");await Gap(400);Check(center.position==before,"identity remains stationary when radial opens");
                    Check(hud.Radial.GetComponentsInChildren<HubStateButton>().Length==6,"exactly six radial slots, no seventh carousel view");
                    for(int i=0;i<6;i++){var slot=(RectTransform)center.Find("PartySlot"+(i+1));float a=PetRadialHudView.Angles[i]*Mathf.Deg2Rad;var wanted=center.position+(Vector3)(new Vector2(Mathf.Cos(a),Mathf.Sin(a))*201*Screen.height/1080f);float error=Vector2.Distance(slot.position,wanted);Check(error<1,"slot center R201 "+i);Check(Mathf.Abs(Mathf.DeltaAngle(slot.Find("ArtworkMask/PetPortrait").eulerAngles.z,0))<.01f,"portrait stays upright "+i);var hit=slot.GetComponent<HubHitContour>();Check(hit.IsRaycastLocationValid(slot.position,null)&&!hit.IsRaycastLocationValid(slot.TransformPoint(new Vector3(-47,47)),null),"sector hit excludes rectangular corners "+i);}
                    await Shot("toggle-open-"+resolution.x);Check(menu.HandleShortcut(KeyCode.Escape)&&!hud.IsPartyOpen&&!InventoryInputController.GameplayInputBlocked,"Esc closes radial without opening Settings");
                    Set(hud,"partyPickerMode",PartyPickerMode.Hold);await Gap();Call(hud,"HandlePartyKey",KeyCode.P);Check(hud.IsPartyOpen,"Hold mode opens via same P owner");hud.ProcessPartyRelease(false,true,Time.unscaledTime+.4f);Check(!hud.IsPartyOpen,"Hold release closes P");hud.Radial.SetOpen(true);Call(hud,"RefreshHud");await Gap(400);await Shot("hold-open-"+resolution.x);Call(hud,"OnApplicationFocus",false);Check(!hud.IsPartyOpen,"focus loss closes Hold radial");Set(hud,"partyPickerMode",PartyPickerMode.Toggle);await Gap();
                    int intents=capture.ThrowIntents;capture.OpenPicker();await Gap();Check(capture.IsPickerOpen&&InventoryInputController.GameplayInputBlocked,"G picker acquires own input lease");int qty=inventory.GetQuantity(CaptureAssets.LoadBall());capture.ClosePicker();await Gap(100);Check(capture.ThrowIntents==intents&&inventory.GetQuantity(CaptureAssets.LoadBall())==qty&&!InventoryInputController.GameplayInputBlocked,"closing ball picker neither throws nor consumes and releases input");
                }
                var ball=CaptureAssets.LoadBall();inventory.TryAddItemAtomic(ball,3);capture.Refresh();int countBefore=inventory.GetQuantity(ball);capture.OpenPicker();await Gap();await Shot("ball-picker-1366");Check(capture.SelectBall(ball.StableId)&&inventory.GetQuantity(ball)==countBefore,"ball selection is presentation-only");
                await Gap(150);await VerifyAuthoritativeFlows(hud,capture,input,provider,inventory);
                var camera=Object.FindFirstObjectByType<Capstone.Game.MapSystem.MinimapController>();Check(camera!=null&&camera.Camera!=null&&camera.Camera.enabled&&camera.Camera.targetTexture!=null,"minimap uses existing live camera render texture");
                int views=hud.Radial.GetComponentsInChildren<Transform>(true).Length;for(int i=0;i<30;i++){hud.Radial.SetOpen(true);hud.Radial.SetOpen(false);}await Gap(150);Check(views==hud.Radial.GetComponentsInChildren<Transform>(true).Length,"30 open/close cycles retain pooled view count");
                using(var cpu=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",128))using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",128)){
                    await Gap(2200);var cpuSamples=new List<ProfilerRecorderSample>();var gcSamples=new List<ProfilerRecorderSample>();cpu.CopyTo(cpuSamples);gc.CopyTo(gcSamples);Write("profiler.json",JsonUtility.ToJson(new Profile{editor=true,cpuAvailable=cpu.Valid,gcAvailable=gc.Valid,seconds=2.2,cpuMeanMilliseconds=cpuSamples.Count>0?cpuSamples.Average(x=>x.Value)/1000000d:0,gcMeanBytes=gcSamples.Count>0?gcSamples.Average(x=>x.Value):0,viewCount=views},true));
                }
                Write("geometry.csv",string.Join("\n",geometry));Write("status.txt","COMPLETE "+checks.Count+" engine checks. Synthetic engine callback/input-state checks; physical OS key edges, standalone and true opportunity source remain unverified.");
            } catch(Exception e){Write("status.txt","FAILED "+e);Write("geometry.csv",string.Join("\n",geometry));Debug.LogException(e);}
            finally{GameplayHudController.Live?.TryCloseTransientHud();if(view!=null&&oldSize>=0){size.SetValue(view,oldSize);if(SessionState.GetBool(Session+"manual",false))view.maximized=wasMaximized;}SessionState.SetBool(Session,false);SessionState.SetBool(Session+"manual",false);running=false;EditorApplication.ExitPlaymode();EditorApplication.delayCall+=()=>{EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"enabled",false);EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"options",0);};}
        }
        async Task VerifyAuthoritativeFlows(GameplayHudController hud,HubCaptureHud capture,PetCommandInput input,PetCommandHudProvider provider,MonsterInventoryAdapter inventory){
            var camera=Object.FindFirstObjectByType<BasicCameraFollow>();var coordinator=Object.FindFirstObjectByType<PetCaptureCoordinator>();var ball=CaptureAssets.LoadBall();
            Set(camera,"lockedEnemy",null);int count=inventory.GetQuantity(ball);Check(!capture.TryThrow()&&inventory.GetQuantity(ball)==count,"G invalid target cannot consume");await Gap();
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/CaptureSystem/Prefabs/WildDragon01.prefab");var target=Object.Instantiate(prefab,input.transform.position+input.transform.forward*4,Quaternion.identity).GetComponent<WildPetCaptureTarget>();await Gap();
            Set(camera,"lockedEnemy",target.Enemy);Set(camera,"enemyLockActive",true);coordinator.RandomSource=new FixedRandom();count=inventory.GetQuantity(ball);
            Check(capture.TryThrow()&&inventory.GetQuantity(ball)==count-1,"G calls authoritative capture service and consumes exactly one");Check(!capture.TryThrow()&&inventory.GetQuantity(ball)==count-1,"busy/duplicate G cannot double throw");
            var presentation=coordinator.GetComponent<CapturePresentation>();Check(presentation!=null&&presentation.IsPlaying,"G starts real capture presentation");presentation.Cancel();await Gap();Check(!InventoryInputController.GameplayInputBlocked&&target.IsWild&&!target.Enemy.CaptureLocked,"cancel capture presentation restores HUD/input and target");
            Set(camera,"lockedEnemy",null);Set(camera,"enemyLockActive",false);Object.Destroy(target.gameObject);
            Set(capture,"opportunitySource",this);HasCaptureOpportunity=true;await Gap(200);var g=hud.PresentationRoot.Find("CaptureG");Check(g.Find("Opportunity").gameObject.activeSelf&&g.GetComponent<CanvasGroup>().alpha>=.55f&&g.GetComponent<CanvasGroup>().alpha<=1,"injected Editor opportunity flag pulses whole G and child label together");await Shot("opportunity-1366");HasCaptureOpportunity=false;await Gap();Check(!g.Find("Opportunity").gameObject.activeSelf&&Mathf.Approximately(g.GetComponent<CanvasGroup>().alpha,1),"opportunity off hides alert and restores alpha");Set(capture,"opportunitySource",null);
            var pet=provider.GetSelectedPetController();var stats=pet.GetComponentInChildren<PetHudRuntimeStats>(true);float hp=stats.Health,max=stats.MaxHealth,mp=stats.Energy,maxMp=stats.MaxEnergy;
            stats.SetStatus(0,max,0,0);await Gap();var label=hud.Radial.GetComponentsInChildren<HubCurvedText>().First(t=>t.name=="HP");var energy=hud.Radial.GetComponentsInChildren<HubCurvedText>().First(t=>t.name=="MP");Check(label.text.StartsWith("HP 0 /")&&energy.text=="MP — / —","HP zero and missing MP use real bound labels without NaN");stats.SetStatus(Mathf.Max(1,max-1),max,mp,maxMp);
            var heal=AssetDatabase.FindAssets("t:MonsterItemDefinition").Select(id=>AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(AssetDatabase.GUIDToAssetPath(id))).FirstOrDefault(d=>d!=null&&d.HealAmount>0&&d.UsableFromInventory);
            if(heal!=null){inventory.TryAddItemAtomic(heal,2);var item=inventory.GetItems().First(i=>i.ItemBase==heal);var quick=hud.GetComponent<GameplayHudQuickItemsView>();Set(quick,"pendingAssignment",item);Check(quick.Assign(0),"R assignment uses existing immediate save flow");await Gap();int before=inventory.GetQuantity(heal);Check(quick.Use(0)&&stats.Health>max-1&&inventory.GetQuantity(heal)==before-1,"R uses existing heal service once");await Gap();Check(!quick.Use(0)&&inventory.GetQuantity(heal)==before-1,"full HP cannot consume another quick item");int left=inventory.GetQuantity(heal);if(left>0)inventory.TryRemoveItemAtomic(heal,left);quick.Refresh();var q=hud.PresentationRoot.Find("QuickItems/QuickItem1/Item/Quantity").GetComponent<Text>();Check(q.text=="0"&&hud.PresentationRoot.Find("QuickItems/QuickItem1/Item/ArtworkMask/ItemIcon").GetComponent<Image>().enabled,"assigned quantity zero retains item icon and zero count");}
            stats.SetStatus(hp,max,mp,maxMp);
            string identity=hud.Radial.IdentityRoot.Find("PetName").GetComponent<Text>().text;Call(hud,"ConfirmPetSlot",5);await Gap();Check(hud.Radial.IdentityRoot.Find("PetName").GetComponent<Text>().text==identity,"empty/rejected slot never changes displayed identity");
            var longState=provider.GetSelectedPetStatus();longState.displayName="Thú đồng hành có tên rất dài để kiểm tra";hud.Radial.Refresh(longState,provider.GetPetSlots(),PartyPickerMode.Toggle);Check(hud.Radial.IdentityRoot.Find("PetName").GetComponent<Text>().text.EndsWith("…"),"long pet name elides without changing font or layout");
        }
        [Serializable]sealed class Profile{public bool editor,cpuAvailable,gcAvailable;public double seconds,cpuMeanMilliseconds,gcMeanBytes;public int viewCount;}
    }
}
#endif





