using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Bounded, opt-in runtime captures of the five background surfaces. No item transactions.</summary>
    public static class BackgroundVerification {
        const string Root = BackgroundQaIsolation.Root, Session = "Capstone.BackgroundQA";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static bool running;
        static double nextPoll;
        [InitializeOnLoadMethod] static void Init() {
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
        }
        static void Poll() {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Root + "request.txt")) return;
            string stage = File.ReadAllText(Root + "request.txt").Trim();
            if (stage != "before" && stage != "after") return;
            File.Delete(Root + "request.txt"); SessionState.SetString(Session, stage);
            File.WriteAllText(Root + "isolate.flag", "explicit background QA");
            if(stage=="before")File.WriteAllText(Root+"baseline.flag","Render original materials for actual runtime baseline");
            else if(File.Exists(Root+"baseline.flag"))File.Delete(Root+"baseline.flag");
            SessionState.SetBool(Session+"OptionsEnabled",EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Session+"Options",(int)EditorSettings.enterPlayModeOptions);
            // The installed MCP package stalls BeforeAssemblyReload. Skip domain reload only during this bounded capture.
            EditorSettings.enterPlayModeOptionsEnabled=true;
            EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload;
            EditorApplication.EnterPlaymode();
        }
        static void OnPlay(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode && !string.IsNullOrEmpty(SessionState.GetString(Session, "")) && !running) Run();
            if(state==PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Session+"RestorePending",false)) {
                SessionState.SetBool(Session+"RestorePending",false);
                EditorSettings.enterPlayModeOptionsEnabled=SessionState.GetBool(Session+"OptionsEnabled",false);
                EditorSettings.enterPlayModeOptions=(EnterPlayModeOptions)SessionState.GetInt(Session+"Options",0);
            }
        }
        [Serializable] sealed class Box { public string path, text; public float x,y,w,h,opacity; }
        [Serializable] sealed class Capture { public int width,height; public Box[] elements; }
        static bool Visible(VisualElement e) { for (var p=e;p!=null;p=p.parent) if(p.resolvedStyle.display==DisplayStyle.None || p.resolvedStyle.visibility==Visibility.Hidden)return false; return true; }
        static string ElementPath(VisualElement e) { string s="";for(var p=e;p!=null;p=p.parent)s="/"+(string.IsNullOrEmpty(p.name)?p.GetType().Name+":"+(p.parent?.IndexOf(p)??0):p.name)+s;return s; }
        static async Task Shot(VisualElement ui, string folder, string name) {
            await Task.Delay(350); var boxes = new List<Box>(); float scale = Screen.width / ui.worldBound.width;
            foreach(var e in ui.Query<VisualElement>().ToList()) {
                if (!Visible(e) || e.worldBound.width<=0 || e.worldBound.height<=0) continue;
                var r=e.worldBound; boxes.Add(new Box{path=ElementPath(e),text=(e as TextElement)?.text,x=r.x*scale,y=r.y*scale,w=r.width*scale,h=r.height*scale,opacity=e.resolvedStyle.opacity});
            }
            string path=folder+"/"+name+"-"+Screen.width+"x"+Screen.height;
            File.WriteAllText(path+".json",JsonUtility.ToJson(new Capture{width=Screen.width,height=Screen.height,elements=boxes.ToArray()},true));
            ScreenCapture.CaptureScreenshot(path+".png"); await Task.Delay(350);
        }
        static async void Run() {
            running=true;string stage=SessionState.GetString(Session,"");string folder=Root+stage;Directory.CreateDirectory(folder);
            var log=new List<string>{DateTime.UtcNow.ToString("O"),"Actual Editor Play Mode; background-only capture; original game data from isolated save copy."};
            var view=EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var index=view.GetType().GetProperty("selectedSizeIndex",Any);int previous=(int)index.GetValue(view);bool maximized=view.maximized;
            MonsterInventoryController bag=null; GameMenuController menu=null;
            Application.LogCallback errors=(message,trace,type)=>{if(type==LogType.Exception||type==LogType.Error)log.Add("ENGINE ERROR "+message+"\n"+trace);};
            Application.logMessageReceived+=errors;
            try {
                await Task.Delay(1400);bag=Object.FindFirstObjectByType<MonsterInventoryController>();menu=Object.FindFirstObjectByType<GameMenuController>();
                var ui=bag.GetComponent<UIDocument>().rootVisualElement;
                if(!Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include,FindObjectsSortMode.None).All(s=>Path.GetFullPath(s.SavePath).StartsWith(Path.GetFullPath(Root),StringComparison.OrdinalIgnoreCase)))throw new Exception("Save isolation failed");
                log.Add("PASS All player stores isolated on E"); view.maximized=true;
                foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1366,768)}) {
                    TrainerMenuPolishCapture.SelectSize(view,index,size);view.Focus();await Task.Delay(500);
                    menu.CloseMenu();bag.Close();bag.OpenInventoryPanel(); if(bag.SelectedItem==null)bag.SelectNextItem();
                    await Shot(ui,folder,"01-bag-grid");BagVerification.Click(ui,"inventory-view-button");await Shot(ui,folder,"02-bag-list");BagVerification.Click(ui,"inventory-view-button");
                    bag.Close();menu.OpenPetsDirect();await Task.Delay(400);
                    foreach(var tab in new[]{"overview","skills","growth"}) {
                        BagVerification.Click(ui,"pets-tab-"+tab);await Shot(ui,folder,"03-pet-"+tab);
                        if(!Visible(ui.Q("companion-dock")) || ui.Q("pets-party-row").childCount!=6)throw new Exception("PET six-slot dock unavailable");
                    }
                    log.Add("PASS 5 actual runtime screens at "+size+"; PET dock has six slots in all tabs");
                    BagVerification.Click(ui,"pets-back-button");menu.CloseMenu();await Task.Delay(200);
                }
                log.Add("COMPLETE "+stage+"; standalone/Profiler not run by this capture.");
            } catch(Exception e) {log.Add("FAILED "+e);Debug.LogException(e);}
            finally {
                bag?.Close();menu?.CloseMenu(); index.SetValue(view,previous);view.maximized=maximized;
                Application.logMessageReceived-=errors;File.WriteAllLines(Root+stage+"-runtime.log",log);
                File.WriteAllText(Root+"status.txt",log.Any(s=>s.StartsWith("FAILED")||s.StartsWith("ENGINE ERROR"))?"FAILED "+stage:"COMPLETE "+stage);
                SessionState.SetString(Session,"");running=false; if(File.Exists(Root+"isolate.flag"))File.Delete(Root+"isolate.flag");
                if(File.Exists(Root+"baseline.flag"))File.Delete(Root+"baseline.flag");
                SessionState.SetBool(Session+"RestorePending",true);EditorApplication.ExitPlaymode();
            }
        }
    }
}
