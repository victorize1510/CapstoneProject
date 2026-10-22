using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using Object=UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>On-demand real Editor Play Profiler capture. Whole-scene counters, not Release benchmarks.</summary>
    public static class GreenAngularProfiler {
        const string Request="Temp/ProfileGreenAngular.request",OwnPlay="GreenAngularProfiler.OwnPlay",Output=GreenAngularVerification.Output+"profiler/";
        const BindingFlags Any=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
        static bool running,sampling;
        static int priorFrame;
        static string phase;
        static readonly List<ProfilerRecorder> recorders=new List<ProfilerRecorder>();
        static readonly List<string> names=new List<string>(),rows=new List<string>(),log=new List<string>();
        [InitializeOnLoadMethod]static void Init(){EditorApplication.update-=Tick;EditorApplication.update+=Tick;}
        static void Tick(){
            if(running){if(sampling&&priorFrame!=Time.frameCount){priorFrame=Time.frameCount;rows.Add(phase+","+Screen.width+","+Screen.height+","+Time.frameCount+","+string.Join(",",recorders.Select(r=>r.Count>0?r.LastValue.ToString():"-1"))+","+Resources.FindObjectsOfTypeAll<RenderTexture>().Length);}return;}
            if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlaying){if(EditorApplication.isPlayingOrWillChangePlaymode)return;SessionState.SetBool(OwnPlay,true);EditorApplication.EnterPlaymode();return;}
            if(Object.FindFirstObjectByType<GameMenuController>()==null)return;File.Delete(Request);Run();
        }
        static async Task Measure(string title,Action action){sampling=false;phase=title;action();await Task.Delay(600);sampling=true;int start=rows.Count;await Task.Delay(1500);sampling=false;log.Add(DateTime.UtcNow.ToString("O")+" phase="+title+" actual="+Screen.width+"x"+Screen.height+" samples="+(rows.Count-start)+" warmup_ms=600 sample_ms=1500");File.WriteAllLines(Output+"progress.txt",log);}
        static void Click(VisualElement root,string name){var b=root.Q<Button>(name);if(b==null||!b.enabledInHierarchy)throw new Exception("Unavailable profiler control "+name);typeof(Clickable).GetMethod("Invoke",Any).Invoke(b.clickable,new object[]{null});}
        static async void Run(){running=true;Directory.CreateDirectory(Output);rows.Clear();log.Clear();names.Clear();recorders.Clear();bool oldEnabled=Profiler.enabled,oldBinary=Profiler.enableBinaryLog;string oldLog=Profiler.logFile;
            var driver=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditorInternal.ProfilerDriver");var profileEditor=driver?.GetProperty("profileEditor",Any);bool oldEditor=profileEditor!=null&&(bool)profileEditor.GetValue(null);
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));var selected=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)selected.GetValue(view);
            var menu=Object.FindFirstObjectByType<GameMenuController>();var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();var party=inventory.GetComponent<PetPartyPanelController>();var root=inventory.GetComponent<UIDocument>().rootVisualElement;
            try{
                var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);var available=handles.Select(h=>new{h,d=ProfilerRecorderHandle.GetDescription(h)}).ToArray();
                foreach(string name in new[]{"Main Thread","CPU Total Frame Time","GC Allocated In Frame","Draw Calls Count","Batches Count","SetPass Calls Count","Total Used Memory","UIElements.UpdateLayout","UIElements.UpdateStyle","UIElements.UpdateRenderData","UIR.DrawChain","Canvas.BuildBatch","Canvas.RenderOverlays"}){var hit=available.FirstOrDefault(x=>x.d.Name==name);if(hit==null){log.Add("UNAVAILABLE "+name);continue;}var r=new ProfilerRecorder(hit.h,1,ProfilerRecorderOptions.StartImmediately|ProfilerRecorderOptions.WrapAroundWhenCapacityReached|ProfilerRecorderOptions.SumAllSamplesInFrame);recorders.Add(r);names.Add(name+" ["+hit.d.UnitType+"]");}
                Profiler.enabled=false;if(profileEditor!=null)profileEditor.SetValue(null,false);Profiler.logFile=Path.GetFullPath(Output+"editor-play.raw");Profiler.enableBinaryLog=true;Profiler.enabled=true;
                rows.Add("phase,width,height,unity_frame,"+string.Join(",",names)+",live_RenderTexture_count");
                log.Add("Unity="+Application.unityVersion+"; scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"; mode=Editor Play; profileEditor=false; whole frame including gameplay and Editor overhead; not a standalone performance claim.");
                log.Add("CPU="+SystemInfo.processorType+"; RAM_MB="+SystemInfo.systemMemorySize+"; GPU="+SystemInfo.graphicsDeviceName+"; API="+SystemInfo.graphicsDeviceType+"; OS="+SystemInfo.operatingSystem);
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}){
                    inventory.Close();menu.CloseMenu();TrainerMenuPolishCapture.SelectSize(view,selected,size);view.Show();view.Focus();await Task.Delay(600);
                    await Measure("closed",()=>{});await Measure("trainer_menu",()=>menu.OpenMenu());await Measure("overview_2d",()=>{menu.CloseMenu();menu.OpenPetsDirect();});await Measure("skills",()=>party.ShowCompanionTab(1));await Measure("growth",()=>party.ShowCompanionTab(2));
                    await Measure("level_popup",()=>Click(root,"pets-growth-level"));menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);
                    await Measure("heal_popup",()=>Click(root,"pets-growth-heal"));menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);
                    await Measure("details_popup",()=>{party.ShowCompanionTab(0);Click(root,"pets-details-button");});menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);
                    await Measure("rename_popup",()=>Click(root,"pets-rename-button"));menu.HandleShortcut(KeyCode.Escape);await Task.Delay(200);
                    await Measure("explicit_3d",()=>party.SetPreview3D(true));await Measure("return_2d",()=>party.SetPreview3D(false));
                    for(int i=0;i<3;i++){inventory.Close();menu.OpenMenu();await Task.Delay(100);menu.CloseMenu();menu.OpenPetsDirect();party.ShowCompanionTab(i%3);await Task.Delay(100);}
                    await Measure("closed_after_cycles",()=>{inventory.Close();menu.CloseMenu();});log.Add("preview RT released="+!party.HasPreviewTexture+" size="+size);
                }
                log.Add("COMPLETE real Unity Editor Play Profiler; no consumable transactions or save edits issued by QA.");
            }catch(Exception e){log.Add("ERROR "+e);Debug.LogException(e);}finally{
                sampling=false;inventory.Close();menu.CloseMenu();Profiler.enabled=false;foreach(var r in recorders)r.Dispose();recorders.Clear();Profiler.enableBinaryLog=oldBinary;Profiler.logFile=oldLog;if(profileEditor!=null)profileEditor.SetValue(null,oldEditor);Profiler.enabled=oldEnabled;selected.SetValue(view,previous);File.WriteAllLines(Output+"samples.csv",rows);File.WriteAllLines(Output+"capture-log.txt",log);running=false;if(SessionState.GetBool(OwnPlay,false)){SessionState.SetBool(OwnPlay,false);EditorApplication.ExitPlaymode();}
            }
        }
    }
}
