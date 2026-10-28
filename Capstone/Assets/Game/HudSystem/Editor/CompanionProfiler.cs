using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using Capstone.Game.UISystem;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.Profiling;
using Object = UnityEngine.Object;

namespace Capstone.Game.HudSystem.Editor {
    /// <summary>Explicit QA request only. Real Unity Profiler counters + binary capture, no saved scene changes.</summary>
    public static class CompanionProfiler {
        const string Request = "Temp/ProfileCompanion.request", OwnPlay = "CompanionProfiler.OwnPlay";
        const string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "companion-ui/profiler/";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly List<ProfilerRecorder> recorders = new List<ProfilerRecorder>();
        static readonly List<string> metrics = new List<string>();
        static readonly List<string> units = new List<string>();
        static readonly List<string> log = new List<string>();
        static readonly ProfilerMarker OpenMarker = new ProfilerMarker("Companion.QA.Open");
        static readonly ProfilerMarker CloseMarker = new ProfilerMarker("Companion.QA.Close");
        static long[,] samples;
        static int phase, phaseOffset, frame, lastFrame, resolution;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920,1080), new Vector2Int(1366,768) };
        static readonly string[] Phases = { "warmup_closed", "closed", "warmup_2d", "2d", "warmup_3d", "3d", "cycles" };
        static readonly int[] Lengths = { 90, 180, 90, 180, 90, 180, 120 };
        static bool running, oldEnabled, oldBinary, oldProfileEditor, wasOpen;
        static string oldLog;
        static EditorWindow gameView;
        static PropertyInfo selected, profileEditor;
        static int previousSize;
        static GameMenuController menu;
        static MonsterInventoryController inventory;
        static PetPartyPanelController party;
        static Vector3 cameraPosition;
        static Quaternion cameraRotation;
        static int lastWrittenFrame;

        [MenuItem("Game Tools/Companion UI/Profile closed, 2D and 3D")]
        static void RequestProfile() {
            if(running||File.Exists(Request)||File.Exists("Temp/VerifyCompanion.request")){Debug.LogWarning("Companion QA is already queued or running.");return;}
            Directory.CreateDirectory("Temp");File.WriteAllText(Request,"profile");
        }

        [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Tick; EditorApplication.update += Tick; }
        static void Tick() {
            try {
                if (!running) {
                    if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                    if (!EditorApplication.isPlaying) {
                        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                        SessionState.SetBool(OwnPlay,true); EditorApplication.EnterPlaymode(); return;
                    }
                    menu = Object.FindFirstObjectByType<GameMenuController>();
                    if (menu == null || Time.frameCount < 15) return;
                    File.Delete(Request); Begin(); return;
                }
                if (!EditorApplication.isPlaying) { End("ERROR Play Mode ended during collection"); return; }
                if (lastFrame == Time.frameCount) return;
                lastFrame = Time.frameCount;
                // LastValue belongs to the completed prior frame. Skip the first frame after each state change.
                if (frame > 0 && frame <= Lengths[phase]) {
                    int offset = phaseOffset+frame-1;
                    for (int i=0;i<recorders.Count;i++) samples[offset,i] = recorders[i].Count > 0 ? recorders[i].LastValue : -1;
                    samples[offset,recorders.Count] = Time.frameCount-1;
                }
                frame++;
                if (phase == 6 && frame % 12 == 1) { CycleModes(); }
                if (frame <= Lengths[phase]) return;
                if (phase == 6) {
                    SaveResolution(); resolution++;
                    if (resolution == Sizes.Length) { End("COMPLETE real Editor Play Mode profiling"); return; }
                    BeginResolution(); return;
                }
                phaseOffset+=Lengths[phase]; phase++; frame=0;
                if (phase == 2) OpenMenu();
                if (phase == 4) party.SetPreview3D(true);
                if (phase == 6) CloseMenu();
                LogPhase();
            } catch (Exception e) { End("ERROR "+e); Debug.LogException(e); }
        }
        static void Begin() {
            inventory=Object.FindFirstObjectByType<MonsterInventoryController>(); party=inventory.GetComponent<PetPartyPanelController>();
            Directory.CreateDirectory(Output); running=true; log.Clear(); metrics.Clear(); units.Clear(); recorders.Clear();
            oldEnabled=Profiler.enabled; oldBinary=Profiler.enableBinaryLog; oldLog=Profiler.logFile; wasOpen=inventory.IsPetsSectionOpen;
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var driver=assembly.GetType("UnityEditorInternal.ProfilerDriver");
            profileEditor=driver?.GetProperty("profileEditor",Any);
            if (profileEditor != null) { oldProfileEditor=(bool)profileEditor.GetValue(null); profileEditor.SetValue(null,false); }
            gameView=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            selected=gameView.GetType().GetProperty("selectedSizeIndex",Any); previousSize=(int)selected.GetValue(gameView);
            gameView.Show(); gameView.Focus();
            Profiler.enabled=false; Profiler.logFile=Path.GetFullPath(Output+"editor-play.raw"); Profiler.enableBinaryLog=true; Profiler.enabled=true;
            var handles=new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions=handles.Select(h=>new { handle=h, desc=ProfilerRecorderHandle.GetDescription(h) }).ToArray();
            File.WriteAllLines(Output+"available-counters.txt",descriptions.Select(x=>x.desc.Category.Name+" | "+x.desc.Name+" | "+x.desc.UnitType));
            string[] wanted={ "Main Thread", "CPU Total Frame Time", "GC Allocated In Frame", "GC.Alloc", "Draw Calls Count", "Batches Count", "SetPass Calls Count", "Triangles Count", "UIElements.UpdateLayout", "UIElements.UpdateStyle", "UIElements.UpdateRenderData", "UIR.DrawChain", "Canvas.BuildBatch", "Canvas.RenderOverlays", "UI Layout::Layout", "UI Render::Render", "Companion.QA.Open", "Companion.QA.Close" };
            foreach (string name in wanted) {
                string[] parts=name.Split(new[]{"::"},StringSplitOptions.None);
                var item=descriptions.FirstOrDefault(x=>x.desc.Name==parts[parts.Length-1] && (parts.Length==1 || x.desc.Category.Name==parts[0]));
                if (item==null) { log.Add("UNAVAILABLE "+name); continue; }
                var recorder=new ProfilerRecorder(item.handle,1,ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame);
                if (!recorder.Valid) { recorder.Dispose(); log.Add("INVALID "+name); continue; }
                recorders.Add(recorder); metrics.Add(name); units.Add(item.desc.UnitType.ToString());
            }
            log.Add("Mode=Unity Editor Play Mode; binary Profiler enabled; profileEditor=false; Editor/Profiler overhead applies. Not Release performance.");
            log.Add("Scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path+"; Unity="+Application.unityVersion);
            log.Add("CPU="+SystemInfo.processorType+"; cores="+SystemInfo.processorCount+"; RAM_MB="+SystemInfo.systemMemorySize);
            log.Add("GPU="+SystemInfo.graphicsDeviceName+"; API="+SystemInfo.graphicsDeviceType+"; VRAM_MB="+SystemInfo.graphicsMemorySize+"; OS="+SystemInfo.operatingSystem);
            log.Add("No external camera/input movement; live scene simulation continues. Menu suppresses HUD via existing presentation. Counters include whole frame, not Menu-only attribution.");
            resolution=0; BeginResolution();
        }
        static void BeginResolution() {
            CloseMenu(); Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture.SelectSize(gameView,selected,Sizes[resolution]); gameView.Focus();
            samples=new long[Lengths.Sum(),recorders.Count+1]; phase=0; phaseOffset=0; frame=0; lastFrame=Time.frameCount;
            if (Camera.main != null) { cameraPosition=Camera.main.transform.position; cameraRotation=Camera.main.transform.rotation; }
            log.Add("Resolution requested="+Sizes[resolution]+"; 90 warmup and 180 samples for each of closed/2D/3D; 120 transition-cycle frames. Full CSV includes warmup.");
            LogPhase();
        }
        static void OpenMenu() { using (OpenMarker.Auto()) { menu.CloseMenu(); menu.OpenPetsDirect(); party.SetPreview3D(false); } }
        static void CloseMenu() { using (CloseMarker.Auto()) { inventory.Close(); menu.CloseMenu(); } }
        static void CycleModes() { if(!inventory.IsPetsSectionOpen) OpenMenu(); else if(!party.IsPreview3D) party.SetPreview3D(true); else CloseMenu(); }
        static void LogPhase() {
            log.Add(DateTime.UtcNow.ToString("O")+" resolution="+Sizes[resolution]+" actual="+Screen.width+"x"+Screen.height+" preview3D="+party.IsPreview3D+" texture="+party.HasPreviewTexture+" phase="+Phases[phase]+" frame="+Time.frameCount);
            File.WriteAllLines(Output+"progress.txt",log);
        }
        static void SaveResolution() {
            string name=Sizes[resolution].x+"x"+Sizes[resolution].y;
            var csv=new List<string>(samples.GetLength(0)+1) { "phase,unity_frame,"+string.Join(",",metrics.Select((m,i)=>m+" ["+units[i]+"]")) };
            int start=0;
            for(int p=0;p<Phases.Length;p++) {
                for(int row=start;row<start+Lengths[p];row++) {
                    var values=new string[recorders.Count]; for(int m=0;m<values.Length;m++) values[m]=samples[row,m].ToString(CultureInfo.InvariantCulture);
                    csv.Add(Phases[p]+","+samples[row,recorders.Count]+","+string.Join(",",values));
                }
                if (p==1 || p==3 || p==5 || p==6) for(int m=0;m<recorders.Count;m++) {
                    var values=Enumerable.Range(start,Lengths[p]).Select(row=>samples[row,m]).Where(v=>v>=0).OrderBy(v=>v).ToArray();
                    if(values.Length==0) {log.Add(name+" "+Phases[p]+" "+metrics[m]+" NO SAMPLES"); continue;}
                    log.Add(name+" "+Phases[p]+" "+metrics[m]+" ["+units[m]+"] n="+values.Length+" median="+values[values.Length/2]+" p95="+values[(int)Math.Ceiling(values.Length*.95)-1]);
                    if (metrics[m].StartsWith("Companion.QA.")) {
                        var events=values.Where(v=>v>0).ToArray();
                        if(events.Length>0) log.Add(name+" "+Phases[p]+" "+metrics[m]+" EVENT samples="+events.Length+" median="+events[events.Length/2]+" p95="+events[(int)Math.Ceiling(events.Length*.95)-1]+" max="+events[events.Length-1]);
                    }
                }
                start+=Lengths[p];
            }
            File.WriteAllLines(Output+"frames-"+name+".csv",csv);
            log.Add("Camera start="+cameraPosition+" / "+cameraRotation+"; end="+(Camera.main!=null ? Camera.main.transform.position.ToString():"missing")+" / "+(Camera.main!=null ? Camera.main.transform.rotation.ToString():"missing"));
            lastWrittenFrame=Time.frameCount;
        }
        static void End(string status) {
            if (!running) return;
            try {
                Profiler.enabled=false; Profiler.enableBinaryLog=false;
                foreach(var recorder in recorders) recorder.Dispose(); recorders.Clear();
                Profiler.logFile=oldLog; Profiler.enableBinaryLog=oldBinary; Profiler.enabled=oldEnabled;
                profileEditor?.SetValue(null,oldProfileEditor);
                if(gameView!=null && selected!=null) selected.SetValue(gameView,previousSize);
                if(menu!=null) { if(wasOpen) OpenMenu(); else CloseMenu(); }
                log.Add(status+"; final recorded Unity frame="+lastWrittenFrame);
                File.WriteAllLines(Output+"report.txt",log);
            } finally {
                running=false;
                if(SessionState.GetBool(OwnPlay,false)) { SessionState.SetBool(OwnPlay,false); EditorApplication.ExitPlaymode(); }
            }
        }
    }
}
