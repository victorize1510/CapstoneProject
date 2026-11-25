using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.QuestSystem.UI;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.QuestSystem.Editor {
    public static partial class QuestSkinVerification {
        static readonly List<string> checks=new List<string>();
        static object Get(object owner,string field)=>owner.GetType().GetField(field,Any).GetValue(owner);
        static void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Root+"checks.txt",checks);if(!ok)throw new Exception(message);}
        static bool Visible(VisualElement e){if(e==null)return false;for(var p=e;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None)return false;return true;}
        static void Tab(QuestPanelController controller,string tab){var type=controller.GetType().GetField("currentTab",Any).FieldType;Call(controller,"SelectTab",Enum.Parse(type,tab));}
        static void Click(VisualElement ui,string id)=>Capstone.Game.Inventory.Bag.Editor.BagVerification.Click(ui,id);
        static void ClickElement(VisualElement target){using(var e=ClickEvent.GetPooled()){e.target=target;target.SendEvent(e);}}
        [Serializable]sealed class ElementBox{public string name,classes,text;public float x,y,w,h;}
        [Serializable]sealed class FrameBoxes{public int width,height;public ElementBox[] elements;}
        static async Task Capture(VisualElement ui,string name){await Task.Delay(260);float scale=Screen.width/ui.worldBound.width;
            var boxes=ui.Q("quest-panel").Query<VisualElement>().ToList().Where(Visible).Select(e=>new ElementBox{name=e.name,classes=string.Join(" ",e.GetClasses()),text=e is Label l?l.text:"",x=e.worldBound.x*scale,y=e.worldBound.y*scale,w=e.worldBound.width*scale,h=e.worldBound.height*scale}).ToArray();
            string path=Root+name+"-"+Screen.width+"x"+Screen.height;File.WriteAllText(path+".json",JsonUtility.ToJson(new FrameBoxes{width=Screen.width,height=Screen.height,elements=boxes},true));ScreenCapture.CaptureScreenshot(path+".png");await Task.Delay(320);
        }
        static void ReplaceState(QuestPanelController controller,List<QuestRuntimeState> states,int index,QuestRuntimeState state){states[index]=state;Call(controller,"RefreshAll");Call(controller,"SelectQuest",state);}
        static QuestRuntimeState Active(QuestDefinition definition){var state=new QuestRuntimeState(definition,Time.time);state.SetRewardsClaimed(true);return state;}
        static async Task VerifyFinal(QuestPanelController controller,GameMenuController menu,EditorWindow view,PropertyInfo size,VisualElement ui){
            checks.Clear();checks.Add(DateTime.UtcNow.ToString("O")+" Unity Editor Play Mode. Official snapshots and explicitly labelled transient scenarios; no new quest assets.");
            var manager=Object.FindFirstObjectByType<QuestManager>();var states=(List<QuestRuntimeState>)Get(manager,"activeQuests");var originals=states.ToArray();var clones=new List<QuestDefinition>();
            var original=states.FirstOrDefault(q=>q?.Definition!=null);Check(original!=null,"existing real quest runtime is present");int index=states.IndexOf(original);var definition=original.Definition;
            Check(Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(s=>Path.GetFullPath(s.SavePath).StartsWith(Path.GetFullPath(Root),StringComparison.OrdinalIgnoreCase)),"all player save stores isolated on E");
            try{
                foreach(var resolution in new[]{new Vector2Int(1600,900),new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,size,resolution);await Task.Delay(400);menu.OpenQuestDirect();Set(controller,"completedSectionExpanded",true);
                    var completed=new QuestRuntimeState(definition,new QuestRuntimeSaveData{questId=original.QuestId,status=QuestStatus.Completed,rewardsClaimed=true});ReplaceState(controller,states,index,completed);Tab(controller,"Main");Call(controller,"SelectQuest",completed);await Capture(ui,"01-completed");
                    Check(!Visible(ui.Q("track-quest-button"))&&!Visible(ui.Q("show-map-button"))&&!Visible(ui.Q("abandon-quest-button")),"completed quest hides all actions at "+resolution);
                    Check(!Visible(ui.Q("quest-progress-bar")),"overall progress remains hidden at "+resolution);
                    Check(ui.Q("quest-reward-list").childCount==definition.Rewards.Count(r=>r!=null),"reward rows come from runtime definition at "+resolution);
                    if(resolution.x==1600)continue;
                    var group=ui.Q("quest-list-scroll").Query(className:"quest-list-section-header").ToList().Last();var groupContent=group.parent.Q(className:"quest-list-section-content");bool expanded=Visible(groupContent);ClickElement(group);await Task.Delay(100);Check(Visible(groupContent)!=expanded&&controller.SelectedQuest==completed,"group collapse preserves detail selection");ClickElement(group);
                    var active=Active(definition);ReplaceState(controller,states,index,active);Tab(controller,"Main");Call(controller,"SelectQuest",active);await Capture(ui,"02-active-untracked");
                    Check(!active.IsTracked,"selecting active quest does not track it");Check(!Visible(ui.Q("abandon-quest-button")),"sample CanAbandon=false hides abandon");
                    var list=ui.Q<ScrollView>("quest-list-scroll");var before=list.scrollOffset;var selected=controller.SelectedQuest;float y=ui.Q("quest-list-column").worldBound.y;
                    await Task.Delay(180);Click(ui,"track-quest-button");await Task.Delay(180);Check(active.IsTracked&&controller.SelectedQuest==selected&&Vector2.Distance(before,list.scrollOffset)<1,"track preserves selection and list scroll");
                    float rootScale=ui.Q("quest-canvas").worldBound.width/1600;Check(Mathf.Abs((ui.Q("quest-list-column").worldBound.y-y)/rootScale-34)<2,"tracked strip shifts body by 34 design pixels");
                    Check(ui.Q("show-map-button").worldBound.yMax<=ui.Q<ScrollView>("quest-detail-scroll").contentViewport.worldBound.yMax+1,"short tracked quest keeps bottom action fully visible without scrolling: button="+ui.Q("show-map-button").worldBound.yMax+", viewport="+ui.Q<ScrollView>("quest-detail-scroll").contentViewport.worldBound.yMax);
                    Tab(controller,"InProgress");await Capture(ui,"03-active-tracked");Check(ui.Q("quest-list-scroll").Query(className:"quest-list-section-header").ToList().Count==0,"InProgress uses flat Active list");
                    foreach(string tab in new[]{"Main","Side","Event","Other","InProgress"}){Tab(controller,tab);Check(ui.Q<Button>("quest-tab-"+(tab=="InProgress"?"progress":tab.ToLowerInvariant())).ClassListContains("is-selected"),"tab selection "+tab+" at "+resolution);}
                    Tab(controller,"Other");await Capture(ui,"04-empty");Check(!Visible(ui.Q("quest-detail-scroll"))&&Visible(ui.Q("quest-detail-empty")),"empty category hides detail content and shows empty state");
                    var button=ui.Q<QuestButton>("quest-tab-other");var rect=button.worldBound;button.Surface.Hovered=true;button.Surface.Animate();await Task.Delay(180);button.Surface.Focused=true;button.Surface.MarkDirtyRepaint();Check(button.worldBound==rect,"hover/focus preserve tab bounds");button.Surface.Stop();
                    Check(!button.ContainsPoint(new Vector2(1,1))&&button.ContainsPoint(new Vector2(button.layout.width/2,button.layout.height/2)),"tab picking rejects transparent corner and accepts center");
                    Check(ui.Q<QuestFrame>("quest-detail-column").ContainsPoint(new Vector2(1,1))==false,"panel picking rejects beveled corner");
                    menu.CloseMenu();Object.FindFirstObjectByType<Capstone.Game.Inventory.MonsterInventoryController>()?.Close();
                }
                Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1366,768));menu.OpenQuestDirect();var state=Active(definition);ReplaceState(controller,states,index,state);Tab(controller,"Main");
                var map=ui.Q<Button>("show-map-button");var receivers=(Action<Vector3>)Get(controller,"showOnMapRequested");Check(receivers!=null,"real Quest map receiver exists");Set(controller,"showOnMapRequested",null);Call(controller,"RefreshActionButtons");Check(Visible(map)&&!map.enabledInHierarchy,"target with no map receiver is visible but disabled");Set(controller,"showOnMapRequested",receivers);Call(controller,"RefreshActionButtons");
                bool delivered=false;Vector3 received=default;Action<Vector3> observer=p=>{delivered=true;received=p;};controller.ShowOnMapRequested+=observer;await Task.Delay(180);Click(ui,"show-map-button");await Task.Delay(200);Check(delivered,"map callback delivered actual target "+received);checks.Add(menu.CurrentMode.ToString()=="WorldMap"?"PASS actual map UI opened through existing connector":"UNVERIFIED map visual open; callback was delivered");controller.ShowOnMapRequested-=observer;menu.OpenQuestDirect();
                var stress=Object.Instantiate(definition);clones.Add(stress);stress.name=definition.name+" temporary layout stress";Set(stress,"title",string.Join(" ",Enumerable.Repeat(definition.Title,80)));Set(stress,"description",string.Join("\n",Enumerable.Repeat(definition.Description,7)));
                var objectives=new List<QuestObjectiveDefinition>();for(int i=0;i<6;i++){var objective=new QuestObjectiveDefinition();Set(objective,"objectiveId","qa_layout_"+i);Set(objective,"title",definition.Objectives.First().Title);Set(objective,"objectiveType",i==0?QuestObjectiveType.ReachLocation:QuestObjectiveType.DefeatEnemy);Set(objective,"requiredAmount",definition.Objectives.First().RequiredAmount);objectives.Add(objective);}Set(stress,"objectives",objectives);Set(stress,"rewards",Enumerable.Range(0,4).SelectMany(_=>definition.Rewards).ToList());Set(stress,"canAbandon",true);
                var longState=Active(stress);ReplaceState(controller,states,index,longState);Tab(controller,"Main");Call(controller,"SelectQuest",longState);await Task.Delay(300);
                var longList=ui.Q<ScrollView>("quest-list-scroll");longList.scrollOffset=new Vector2(0,80);await Task.Delay(150);var scroll=longList.scrollOffset;var longSelection=controller.SelectedQuest;Click(ui,"track-quest-button");await Task.Delay(200);Check(scroll.y>0&&Vector2.Distance(scroll,longList.scrollOffset)<1&&longSelection==controller.SelectedQuest,"tracking retains a genuinely nonzero list scroll with long title");
                Check(ui.Q("quest-objective-list").childCount==6&&ui.Q("quest-objective-list").Query<ProgressBar>().ToList().Count==5,"many objectives render and ReachLocation has no quantity bar");Check(ui.Q("quest-reward-list").childCount==stress.Rewards.Count,"many rewards render from runtime data");
                var detail=ui.Q<ScrollView>("quest-detail-scroll");detail.ScrollTo(ui.Q("quest-actions"));await Task.Delay(200);await Capture(ui,"05-scenario-long-scroll-bottom");Check(ui.Q("quest-actions").worldBound.yMax<=detail.contentViewport.worldBound.yMax+2,"bottom actions remain reachable in scroll viewport");
                await Task.Delay(180);Click(ui,"abandon-quest-button");Check(longState.Status==QuestStatus.Abandoned,"existing abandon callback works only for CanAbandon=true test clone");
                var missing=Object.Instantiate(definition);clones.Add(missing);Set(missing,"description","");Set(missing,"locationName","");Set(missing,"questGiverName","");Set(missing,"extraInfo","");Set(missing,"objectives",new List<QuestObjectiveDefinition>());Set(missing,"rewards",new List<QuestRewardDefinition>());Set(missing,"hasWorldPosition",false);Set(missing,"timeLimit",0f);
                var missingState=Active(missing);ReplaceState(controller,states,index,missingState);Tab(controller,"Main");detail.scrollOffset=Vector2.zero;await Capture(ui,"06-scenario-missing-data");Check(new[]{"quest-description-section","quest-objectives-section","quest-rewards-section","quest-extra-info-section","quest-location-row","quest-giver-row","quest-time-row","show-map-button"}.All(id=>!Visible(ui.Q(id))),"missing data hides optional sections and target action");
                var failedReward=new QuestRuntimeState(definition,new QuestRuntimeSaveData{questId=original.QuestId,status=QuestStatus.Completed,rewardsClaimed=false});ReplaceState(controller,states,index,failedReward);Tab(controller,"Main");
                var service=Object.FindFirstObjectByType<QuestRewardService>();var saveController=Get(service,"saveController");try{Set(service,"saveController",null);Call(service,"HandleRewardsReady",failedReward,definition.Rewards);Check(!string.IsNullOrEmpty(service.LastFailureMessage)&&ui.Q<Label>("quest-reward-feedback").text==service.LastFailureMessage&&Visible(ui.Q("quest-reward-feedback")),"reward feedback displays real service failure message for matching quest");await Capture(ui,"07-scenario-reward-failure");
                    string failureId=service.LastFailedQuestId;Set(service,"<LastFailedQuestId>k__BackingField","qa_unselected");Call(controller,"RefreshRewardFeedback");Check(!Visible(ui.Q("quest-reward-feedback")),"reward feedback hidden for another quest ID");Set(service,"<LastFailedQuestId>k__BackingField",failureId);
                }finally{Set(service,"saveController",saveController);failedReward.SetRewardsClaimed(true);}
                states.Clear();states.AddRange(originals);menu.OpenQuestDirect();Set(controller,"completedSectionExpanded",true);Tab(controller,"Main");await Task.Delay(100);int count=ui.Q("quest-panel").Query<VisualElement>().ToList().Count;
                for(int i=0;i<3;i++){Object.FindFirstObjectByType<Capstone.Game.Inventory.MonsterInventoryController>().Close();await Task.Delay(220);menu.OpenQuestDirect();Set(controller,"completedSectionExpanded",true);Tab(controller,"Main");await Task.Delay(220);Check(Visible(ui.Q("quest-panel")),"quest is actually visible after reopen "+(i+1));Check(ui.Q("quest-panel").Query<VisualElement>().ToList().Count==count,"visual element count stable after reopen "+(i+1));}
                await Counters();await ContourBoard(ui,view,size);checks.Add("COMPLETE actual runtime/callback/geometry checks; native input is recorded separately; no standalone run.");File.WriteAllLines(Root+"checks.txt",checks);
            }finally{states.Clear();states.AddRange(originals);foreach(var clone in clones)if(clone!=null)Object.Destroy(clone);}
        }
        static async Task Counters(){var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);var available=handles.Select(h=>new{h,d=ProfilerRecorderHandle.GetDescription(h)}).ToArray();var recorders=new List<ProfilerRecorder>();var names=new List<string>();var csv=new List<string>();
            try{foreach(string name in new[]{"Main Thread","GC Allocated In Frame","UIElements.UpdateLayout","UIElements.UpdateStyle","UIElements.UpdateRenderData","UIR.DrawChain"}){var found=available.FirstOrDefault(a=>a.d.Name==name);if(found==null){checks.Add("COUNTER UNAVAILABLE "+name);continue;}var r=new ProfilerRecorder(found.h,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame);if(!r.Valid){r.Dispose();continue;}recorders.Add(r);names.Add(name+" ["+found.d.UnitType+"]");}
                csv.Add("frame,"+string.Join(",",names));double end=Time.realtimeSinceStartupAsDouble+10;int last=-1;for(int i=0;i<120&&Time.realtimeSinceStartupAsDouble<end;){await Task.Delay(16);if(Time.frameCount==last)continue;last=Time.frameCount;csv.Add(last+","+string.Join(",",recorders.Select(r=>r.LastValue)));i++;}File.WriteAllLines(Root+"profiler-editor.csv",csv);checks.Add("PROFILER "+(csv.Count-1)+" actual Editor Play Mode samples; <=10 seconds; whole frame, not standalone or UI-only attribution.");
            }finally{foreach(var r in recorders)r.Dispose();}}
        [Serializable]sealed class Corner{public string name;public float x,y;}
        [Serializable]sealed class CornerList{public Corner[] samples;}
        static async Task ContourBoard(VisualElement ui,EditorWindow view,PropertyInfo size){
            Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1600,900));await Task.Delay(300);var canvas=ui.Q("quest-canvas");var board=new VisualElement{name="quest-contour-runtime-board",pickingMode=PickingMode.Ignore};Place(board,0,0,1600,900);board.style.backgroundColor=GreenAngularGeometry.Shell;canvas.Add(board);var cells=new List<(VisualElement,float)>();
            for(int c=0;c<3;c++){var cell=new VisualElement{pickingMode=PickingMode.Ignore};Place(cell,c*530+5,5,520,890);cell.style.backgroundColor=c==0?GreenAngularGeometry.Light:GreenAngularGeometry.Ink;if(c==2)cell.style.backgroundImage=new StyleBackground(QuestSkinArt.Texture("Art/quest-material"));board.Add(cell);for(int row=0;row<3;row++){var label=new Label((c==0?"LIGHT":c==1?"MID-DARK":"DETAILED")+" / "+(100+row*25)+"% / actual Unity mesh"){pickingMode=PickingMode.Ignore};Place(label,12,12+row*292,490,24);label.style.color=c==0?GreenAngularGeometry.Ink:GreenAngularGeometry.Light;cell.Add(label);}cells.Add((cell,c));}
            await Capture(ui,"08-contour-background-before");var corners=new List<Corner>();int number=0;
            foreach(var pair in cells)for(int row=0;row<3;row++){float scale=1+row*.25f;var button=new QuestButton{text="Theo dõi",Kind="track",pickingMode=PickingMode.Ignore};button.AddToClassList("quest-header-track-button");Place(button,18,52+row*292,288,48);button.style.transformOrigin=new TransformOrigin(0,0,0);button.style.scale=new Scale(new Vector3(scale,scale,1));button.Surface.Focused=true;button.Surface.HoverAmount=.5f;pair.Item1.Add(button);
                var card=new QuestFrame{Kind="card",pickingMode=PickingMode.Ignore};Place(card,18,145+row*292,288,80);card.style.transformOrigin=new TransformOrigin(0,0,0);card.style.scale=new Scale(new Vector3(scale,scale,1));card.AddToClassList("is-selected");pair.Item1.Add(card);var text=new Label("Contour / selected + focus"){pickingMode=PickingMode.Ignore};Place(text,16,20,256,32);card.Add(text);card.Focused=true;
                await Task.Delay(160);Check(button.Surface.layout.width>2&&card.layout.width>2,"contour layout resolved before hit-test");foreach(var element in new VisualElement[]{button,card}){var p=element.LocalToWorld(new Vector2(.2f,.2f));float screenScale=Screen.width/ui.worldBound.width;corners.Add(new Corner{name="corner-"+(number++),x=p.x*screenScale,y=p.y*screenScale});Check(!element.ContainsPoint(new Vector2(.2f,.2f)),"transparent corner rejected at scale "+scale+" backdrop "+pair.Item2);}}
            await Capture(ui,"09-contour-board");File.WriteAllText(Root+"contour-samples.json",JsonUtility.ToJson(new CornerList{samples=corners.ToArray()},true));board.RemoveFromHierarchy();
        }
        static void Place(VisualElement e,float x,float y,float w,float h){e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=w;e.style.height=h;}
        static async Task Native(QuestPanelController controller,VisualElement ui){
            var manager=Object.FindFirstObjectByType<QuestManager>();var states=(List<QuestRuntimeState>)Get(manager,"activeQuests");var originals=states.ToArray();var original=originals.First(q=>q?.Definition!=null);int index=states.IndexOf(original);ReplaceState(controller,states,index,Active(original.Definition));Tab(controller,"Main");
            var events=new List<string>{"utc,event,target"};int captures=0;void Frame(){ui.schedule.Execute(()=>ScreenCapture.CaptureScreenshot(Root+"native-frame-"+(captures++%QaArtifactPaths.MaxRetainedFrames).ToString("D2")+".png")).StartingIn(180);}EventCallback<PointerDownEvent> pointer=e=>{var target=e.target as VisualElement;events.Add(DateTime.UtcNow.ToString("O")+",PointerDown,"+(target?.GetFirstAncestorOfType<QuestButton>()?.name??target?.name));Frame();};EventCallback<KeyDownEvent> key=e=>{events.Add(DateTime.UtcNow.ToString("O")+",KeyDown,"+e.keyCode);Frame();};ui.RegisterCallback(pointer,TrickleDown.TrickleDown);ui.RegisterCallback(key,TrickleDown.TrickleDown);
            File.WriteAllText(Root+"native-status.txt","READY; isolated native input observer, maximum 600 seconds /10 rolling captures");
            try{double end=Time.realtimeSinceStartupAsDouble+600;while(Time.realtimeSinceStartupAsDouble<end&&!File.Exists(Root+"native.stop")){File.WriteAllLines(Root+"native-events.csv",events.Take(1000));await Task.Delay(200);}}
            finally{ui.UnregisterCallback(pointer,TrickleDown.TrickleDown);ui.UnregisterCallback(key,TrickleDown.TrickleDown);if(File.Exists(Root+"native.stop"))File.Delete(Root+"native.stop");states.Clear();states.AddRange(originals);}
        }
    }
}
