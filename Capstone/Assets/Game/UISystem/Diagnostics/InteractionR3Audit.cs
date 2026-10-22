#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem {
    // Development-only observer/fixtures. Never active without the explicit audit argument/flag.
    public sealed class InteractionR3Audit : MonoBehaviour {
        const BindingFlags Any=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
        static string output;static bool manual;
        readonly List<string> results=new List<string>();
        UIDocument doc;VisualElement ui,panel;MonsterInventoryController inventory;PetPartyPanelController party;PetSkillsPanelController skills;PetCommandHudProvider provider;GameMenuController menu;
        StreamWriter motion;int frame;float end;bool measuring;string lastFocus;
        static string Arg(string[] args,string key){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:null;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]static void Init(){
            var args=Environment.GetCommandLineArgs();string root=Arg(args,"--r3-audit");
#if UNITY_EDITOR
            if(root==null&&File.Exists("Temp/R3Audit.run"))root=Path.GetFullPath("../artifacts/ui-r3-fixes-2026-10-20/editor");
#endif
            if(root==null)return;output=Path.GetFullPath(root);Directory.CreateDirectory(output);manual=args.Contains("--r3-manual");
#if UNITY_EDITOR
            manual|=File.Exists("Temp/R3Audit.manual");
#endif
            string saveRoot=Path.Combine(output,"test-saves");Directory.CreateDirectory(saveRoot);
            // The runner must have a test snapshot explicitly provisioned by the operator.
            if(!File.Exists(Path.Combine(saveRoot,"player_main.json"))){Debug.LogError("R3 audit refuses to run without an isolated test snapshot");Application.Quit(9);return;}
            foreach(var s in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))s.SetSaveStore(new PlayerJsonFileSaveStore(saveRoot));
            foreach(var s in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None))s.SetSaveStore(new QuestJsonFileSaveStore(saveRoot));
            File.WriteAllText(output+"/isolation.txt",DateTime.UtcNow.ToString("O")+" before Start; saveRoot="+saveRoot+"; mode="+(Application.isEditor?"Editor":"Development standalone"));
            var host=new GameObject("R3 audit observer");host.AddComponent<InteractionR3Audit>();
        }
        void Check(bool pass,string text){results.Add((pass?"PASS ":"FAIL ")+text);File.WriteAllLines(output+"/checks.txt",results);}
        object Field(object o,string name)=>o.GetType().GetField(name,Any)?.GetValue(o);
        void Set(object o,string name,object value)=>o.GetType().GetField(name,Any).SetValue(o,value);
        object Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Any).Invoke(o,args);
        void Click(string name){var b=ui.Q<Button>(name);if(b==null||!b.enabledInHierarchy){Check(false,"unavailable control "+name);return;}typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        IEnumerator Shot(string name){yield return new WaitForSecondsRealtime(.2f);ScreenCapture.CaptureScreenshot(output+"/"+name+"-"+Screen.width+"x"+Screen.height+".png");yield return new WaitForSecondsRealtime(.2f);}
        IEnumerator Start(){
            yield return new WaitForSecondsRealtime(2);
            menu=Object.FindFirstObjectByType<GameMenuController>();inventory=Object.FindFirstObjectByType<MonsterInventoryController>();provider=Object.FindFirstObjectByType<PetCommandHudProvider>();
            Check(menu!=null&&inventory!=null&&provider!=null,"scene contains current Menu, Companion and provider");if(menu==null||inventory==null||provider==null)yield break;
            party=inventory.GetComponent<PetPartyPanelController>();skills=inventory.GetComponent<PetSkillsPanelController>();doc=inventory.GetComponent<UIDocument>();ui=doc.rootVisualElement;panel=ui.Q("pets-panel");
            Check(Object.FindFirstObjectByType<PlayerSaveController>().SavePath.StartsWith(Path.Combine(output,"test-saves"),StringComparison.OrdinalIgnoreCase),"actual save path is isolated before any input");
            if(manual){menu.CloseMenu();menu.OpenPetsDirect();yield return new WaitForSecondsRealtime(.5f);BeginMotion();yield break;}
#if UNITY_EDITOR
            if(File.Exists("Temp/R3Audit.allocations")){yield return AllocationProbe();yield return FollowupEdges();inventory.Close();menu.CloseMenu();results.Add("COMPLETE allocation calibration and targeted followup edges");File.WriteAllLines(output+"/allocation-checks.txt",results);File.WriteAllText(output+"/stop","probe complete");yield break;}
#endif
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                Screen.SetResolution(size.x,size.y,FullScreenMode.Windowed);
#if UNITY_EDITOR
                var view=UnityEditor.EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);var helper=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t=>t!=null);helper.GetMethod("SelectSize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{view,selected,size});
#endif
                yield return new WaitForSecondsRealtime(1);Check(Screen.width==size.x&&Screen.height==size.y,"actual resolution "+size);
                menu.CloseMenu();inventory.Close();menu.OpenMenu();yield return Shot("01-menu");menu.CloseMenu();menu.OpenPetsDirect();yield return new WaitForSecondsRealtime(.3f);
                foreach(int tab in new[]{0,1,2}){party.ShowCompanionTab(tab);yield return Shot("0"+(tab+2)+"-tab");}
                foreach(var action in new[]{new[]{"pets-details-button","05-details"},new[]{"pets-level-up-button","06-level"},new[]{"pets-heal-button","07-heal"},new[]{"pets-rename-button","08-rename"}}){party.ShowCompanionTab(0);yield return new WaitForSecondsRealtime(.25f);Click(action[0]);yield return Shot(action[1]);menu.HandleShortcut(KeyCode.Escape);}
                for(int i=0;i<20;i++)party.ShowCompanionTab(i%3);yield return new WaitForSecondsRealtime(.3f);Check(party.CompanionTab==1,"rapid 20 navigation requests retain latest tab");
                Check(panel.Q("pets-tab-body-skills").resolvedStyle.display==DisplayStyle.Flex&&panel.Q("pets-tab-body-overview").resolvedStyle.display==DisplayStyle.None&&panel.Q("pets-tab-body-growth").resolvedStyle.display==DisplayStyle.None,"only latest tab body displayed");
                party.ShowCompanionTab(0);yield return new WaitForSecondsRealtime(.3f);
                foreach(string id in new[]{"pets-rename-button","pets-favorite-button","pets-level-up-button","pets-tab-overview","pets-back-button"}){
                    var anchor=ui.Q(id);if(anchor==null)continue;
                    Call(party,"ShowTooltip",anchor,"Đặng Thú — tên rất dài để kiểm tooltip","Nội dung kiểm: tiếng Việt có dấu, thông tin đầy đủ và không che nút bên cạnh.");yield return new WaitForSecondsRealtime(.5f);
                    var tip=Field(party,"tooltip") as VisualElement;var r=tip.worldBound;var bounds=ui.worldBound;
                    Check(tip.resolvedStyle.display==DisplayStyle.Flex&&r.xMin>=bounds.xMin&&r.yMin>=bounds.yMin&&r.xMax<=bounds.xMax&&r.yMax<=bounds.yMax,"tooltip within viewport "+id);
                    Check(!r.Overlaps(anchor.worldBound),"tooltip avoids its anchor "+id);
                    foreach(var label in tip.Query<Label>().ToList()) {
                        var textBounds=label.worldBound;
                        Check(textBounds.xMin>=r.xMin-.5f&&textBounds.xMax<=r.xMax+.5f&&textBounds.yMin>=r.yMin-.5f&&textBounds.yMax<=r.yMax+.5f,"tooltip text layout stays inside panel "+id+" "+label.name);
                    }
                    yield return Shot("tooltip-"+id);Call(party,"HideTooltip");
                }
                Click("pets-rename-button");var input=ui.Q<TextField>("pets-rename-input");input.value=" ";Check(!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"blank rename disabled");input.value="Đặng Ánh Trăng";Check(ui.Q<Button>("pets-rename-save").enabledInHierarchy,"Vietnamese name accepted");input.value="ABCDEFGHIJKLMNOPQRSTUVWXYZ";yield return Shot("rename-overlength");Check(input.value.Length<=16||!ui.Q<Button>("pets-rename-save").enabledInHierarchy,"overlength rename is truncated or rejected");menu.HandleShortcut(KeyCode.Escape);
                Click("pets-swap-button");Check((int)Field(party,"pendingSwapSourceIndex")>=0,"swap armed");Call(party,"OnApplicationFocus",false);Check((int)Field(party,"pendingSwapSourceIndex")==-1&&Field(party,"dragGhost")==null,"focus-loss callback cancels pending swap and ghost (programmatic)");
                // Intentionally stale expected source, with no roster mutation and no save command.
                Click("pets-swap-button");var source=provider.GetPetControllerAt((int)Field(party,"pendingSwapSourceIndex"));int dest=Enumerable.Range(0,6).First(i=>provider.GetPetControllerAt(i)!=source);Call(party,"HandlePartyCardClick",dest,1);Set(party,"swapSourcePet",null);Call(party,"ConfirmPartySwap");Check((int)Field(party,"pendingSwapSourceIndex")==-1,"stale expected source rejects confirm");
                yield return Fixtures();
                yield return Profile();
            }
            inventory.Close();menu.CloseMenu();Check(!party.HasPreviewTexture,"final close releases owned preview RT");results.Add("COMPLETE actual "+(Application.isEditor?"Editor":"Development standalone")+"; programmatic edge cases, physical input recorded separately");File.WriteAllLines(output+"/checks.txt",results);
            if(!Application.isEditor)Application.Quit();
        }
        IEnumerator Fixtures(){
            var field=typeof(PetPartyPanelController).GetField("petHudProvider",Any);var auto=typeof(PetPartyPanelController).GetField("autoFindProvider",Any);object saved=field.GetValue(party),savedAuto=auto.GetValue(party);
            var host=new GameObject("Audit readonly provider fixture");var fixture=host.AddComponent<InteractionR3AuditReadonlyProvider>();fixture.enabled=false;
            var slots=new List<PetSlotHudData>(provider.GetPetSlots());string longName=string.Concat(Enumerable.Repeat("Đặng Ánh Trăng ",8));var status=provider.GetSelectedPetStatus();status.displayName=longName;status.icon=null;status.health=100000000;status.maxHealth=100000001;fixture.Status=status;fixture.Skills=provider.GetSkills();
            for(int i=0;i<slots.Count;i++){var data=slots[i];data.icon=null;if(data.occupied){data.displayName=longName;data.health=100000000;data.maxHealth=100000001;}slots[i]=data;}fixture.Slots=slots;
            auto.SetValue(party,false);field.SetValue(party,fixture);party.Refresh();yield return new WaitForSecondsRealtime(.2f);yield return Shot("fixture-long-vietnamese-big-hp-no-icons");
            Check(panel.Q<Label>("pets-selected-name").resolvedStyle.textOverflow==TextOverflow.Ellipsis,"long pet name has ellipsis");
            fixture.Status=default;fixture.Skills=Array.Empty<SkillHudData>();fixture.Slots=Enumerable.Range(0,6).Select(_=>new PetSlotHudData()).ToArray();party.Refresh();yield return Shot("fixture-empty-roster");Check(panel.Query<Button>(className:"pet-party-card").ToList().Count==6,"empty roster retains six slots");Check(!ui.Q<Button>("pets-level-up-button").enabledInHierarchy,"empty readonly provider disables pet actions");
            field.SetValue(party,null);party.Refresh();yield return Shot("fixture-missing-provider");
            field.SetValue(party,saved);auto.SetValue(party,savedAuto);party.Refresh();Object.Destroy(host);
        }
        IEnumerator Profile(){
            var recorders=new List<(string name,ProfilerRecorder r)>();var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
            foreach(string name in new[]{"Main Thread","GC Allocated In Frame","UIElements.UpdateLayout","UIElements.UpdateStyle","UIElements.UpdateRenderData","Draw Calls Count","Total Used Memory"}){var h=handles.FirstOrDefault(x=>ProfilerRecorderHandle.GetDescription(x).Name==name);if(h.Equals(default(ProfilerRecorderHandle)))continue;recorders.Add((name,new ProfilerRecorder(h,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame)));}
            string path=output+"/profile-"+Screen.width+"x"+Screen.height+".csv";
            using(var writer=new StreamWriter(path)){writer.WriteLine("phase,frame,"+string.Join(",",recorders.Select(x=>x.name)));
                foreach(string phase in new[]{"closed","menu","overview","skills","growth","3d","return2d","closed_end"}){
                    if(phase.StartsWith("closed")){inventory.Close();menu.CloseMenu();}else if(phase=="menu")menu.OpenMenu();else if(phase=="overview"){menu.CloseMenu();menu.OpenPetsDirect();}else if(phase=="skills")party.ShowCompanionTab(1);else if(phase=="growth")party.ShowCompanionTab(2);else party.SetPreview3D(phase=="3d");
                    yield return new WaitForSecondsRealtime(2);float until=Time.realtimeSinceStartup+3;while(Time.realtimeSinceStartup<until){yield return null;writer.WriteLine(phase+","+Time.frameCount+","+string.Join(",",recorders.Select(x=>x.r.LastValue.ToString(CultureInfo.InvariantCulture))));}
                }
            }foreach(var rec in recorders)rec.r.Dispose();
            yield return AllocationProbe();
        }
        IEnumerator AllocationProbe(){
            menu.CloseMenu();menu.OpenPetsDirect();yield return new WaitForSecondsRealtime(.3f);party.ShowCompanionTab(1);yield return new WaitForSecondsRealtime(.3f);
            long calibrationBefore=GC.GetAllocatedBytesForCurrentThread();long heapBefore=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();var calibration=new byte[1024*1024];GC.KeepAlive(calibration);long calibrationDelta=GC.GetAllocatedBytesForCurrentThread()-calibrationBefore;long calibrationHeap=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()-heapBefore;
            File.WriteAllText(output+"/allocation-calibration.txt",$"Known allocation byte[1048576]; thread_counter_delta={calibrationDelta}; mono_heap_delta={calibrationHeap}; thread counter usable={calibrationDelta>=1048576}. Mono heap deltas are approximate retained growth, not cumulative allocation.\n");
            var design=panel.Q("pets-design");var samples=new List<string>{"operation,iterations,bytes_total,bytes_per_call"};
            samples.Clear();samples.Add("operation,iterations,thread_counter_supported,thread_counter_delta,mono_heap_delta,mono_heap_delta_per_call");
            foreach(var op in new (string,Action)[]{("provider status",()=>provider.GetSelectedPetStatus()),("provider slots",()=>provider.GetPetSlots()),("party Refresh",()=>party.Refresh()),("skin Update",()=>GreenAngularCompanionSkin.Update(design,1))}){
                for(int i=0;i<5;i++)op.Item2();GC.Collect();GC.WaitForPendingFinalizers();long before=GC.GetAllocatedBytesForCurrentThread(),heap=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();for(int i=0;i<30;i++)op.Item2();long bytes=GC.GetAllocatedBytesForCurrentThread()-before,growth=UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()-heap;samples.Add(op.Item1+",30,"+(calibrationDelta>=1048576)+","+bytes+","+growth+","+(growth/30.0).ToString(CultureInfo.InvariantCulture));
            }File.WriteAllLines(output+"/operation-allocations-"+Screen.width+".csv",samples);
        }
        IEnumerator FollowupEdges(){
            party.ShowCompanionTab(1);yield return new WaitForSecondsRealtime(.3f);skills.CancelEquipPreview();
            var save=Object.FindFirstObjectByType<PlayerSaveController>();string disk=File.ReadAllText(save.SavePath);
            Call(skills,"SelectLearnedSkill",0);Call(skills,"EquipSelectedInto",0);Set(skills,"selectedSkillId","audit-id-that-is-not-learned");Call(skills,"ConfirmEquipPreview");
            Check(!skills.HasEquipPreview&&File.ReadAllText(save.SavePath)==disk,"stale learned skill ID clears preview without save");
            skills.CancelEquipPreview();Call(skills,"EquipSelectedInto",99);Check(!skills.HasEquipPreview&&File.ReadAllText(save.SavePath)==disk,"out-of-range skill destination rejected without save");
            Call(skills,"SelectLearnedSkill",0);Call(skills,"EquipSelectedInto",0);Set(skills,"previewPet",provider.GetPetControllerAt(1));Call(skills,"ConfirmEquipPreview");Check(File.ReadAllText(save.SavePath)==disk,"different pet identity rejects equip without save");skills.CancelEquipPreview();
            party.ShowCompanionTab(0);yield return new WaitForSecondsRealtime(.3f);
            var command=Field(provider,"petCommandInput");object slots=Field(command,"petSlots"),active=Field(command,"activePet");
            Set(command,"petSlots",new PetController[6]);Set(command,"activePet",null);party.Refresh();
            Check(!provider.GetSelectedPetStatus().hasPet&&party.ViewedPet==null,"real command provider empty roster clears viewed context");Check(!ui.Q<Button>("pets-level-up-button").enabledInHierarchy,"real empty roster disables pet actions");yield return Shot("fixture-real-command-empty-roster");
            Set(command,"petSlots",slots);Set(command,"activePet",active);party.Refresh();
        }
        void BeginMotion(){
            motion=new StreamWriter(output+"/native-motion.csv");motion.WriteLine("frame,time,id,part,x,y,w,h,marginLeft,paddingLeft,borderLeft,focused,keyboard");end=Time.realtimeSinceStartup+900;
            ui.RegisterCallback<PointerDownEvent>(e=>File.AppendAllText(output+"/native-events.txt",DateTime.UtcNow.ToString("O")+" down "+(e.target as VisualElement)?.name+" "+e.position+"\n"),TrickleDown.TrickleDown);
        }
        void LateUpdate(){
            if(motion==null||ui==null)return;
            var focused=ui.focusController.focusedElement as VisualElement;
            string focus=(focused?.name??"none")+"; keyboard="+InteractionFeedback.KeyboardNavigation+"; bracket="+(focused is AngularButton angular&&angular.Surface.IsFocused);
            if(focus!=lastFocus){lastFocus=focus;File.AppendAllText(output+"/native-focus.txt",DateTime.UtcNow.ToString("O")+" "+focus+"\n");}
            foreach(string id in new[]{"pets-rename-button","pets-favorite-button","pets-tab-overview","pets-tab-skills","pets-tab-growth"}){
                var b=ui.Q<Button>(id);if(b==null)continue;foreach(var pair in new[]{("button",(VisualElement)b),("icon",b.Q(className:"companion-icon"))}){var e=pair.Item2;if(e==null)continue;var r=e.worldBound;var s=e.resolvedStyle;motion.WriteLine(FormattableString.Invariant($"{Time.frameCount},{Time.realtimeSinceStartup},{id},{pair.Item1},{r.x},{r.y},{r.width},{r.height},{s.marginLeft},{s.paddingLeft},{s.borderLeftWidth},{b==ui.focusController.focusedElement},{InteractionFeedback.KeyboardNavigation}"));}
            }
            if(frame++%60==0){motion.Flush();ScreenCapture.CaptureScreenshot(output+"/native-"+frame.ToString("D6")+".png");}
            if(Time.realtimeSinceStartup>end||File.Exists(output+"/stop")){motion.Dispose();motion=null;if(!Application.isEditor)Application.Quit();}
        }
        void OnDestroy(){motion?.Dispose();}
    }
    public sealed class InteractionR3AuditReadonlyProvider : MonoBehaviour,IPetHudProvider {
        public IReadOnlyList<PetSlotHudData> Slots;public IReadOnlyList<SkillHudData> Skills;public PetStatusHudData Status;
        public event Action HudDataChanged {add{}remove{}}
        public PetStatusHudData GetSelectedPetStatus()=>Status;public IReadOnlyList<PetSlotHudData> GetPetSlots()=>Slots;public IReadOnlyList<SkillHudData> GetSkills()=>Skills;
        public void SelectPetSlot(int index){}public void RequestSkill(int index){}
    }
}
#endif
