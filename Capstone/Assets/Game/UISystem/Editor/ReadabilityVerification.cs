using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Inventory.Bag.Editor;
using Capstone.Game.ProfileSystem;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    public static partial class ReadabilityVerification {
        const string Root = ReadabilityQaIsolation.Root, Session = "Capstone.ReadabilityQA";
        static readonly BindingFlags Any = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly List<string> checks = new List<string>();
        static bool running;
        static double nextPoll;
        static object Get(object target, string field) => target.GetType().GetField(field, Any).GetValue(target);
        static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Any).Invoke(target, args);
        static void Check(bool value, string label) { checks.Add((value ? "PASS " : "FAIL ") + label); if (!value) throw new Exception(label); }
        [InitializeOnLoadMethod] static void Init() { EditorApplication.update -= Poll; EditorApplication.update += Poll; EditorApplication.playModeStateChanged -= State; EditorApplication.playModeStateChanged += State; }
        static void Poll() {
            if (EditorApplication.timeSinceStartup < nextPoll) return; nextPoll = EditorApplication.timeSinceStartup + .5;
            if (running || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!File.Exists(Root + "request.txt")) return;
            string phase = File.ReadAllText(Root + "request.txt").Trim(); File.Delete(Root + "request.txt");
            SessionState.SetString(Session + "phase", phase); SessionState.SetBool(Session, true);
            SessionState.SetBool(Session + "enabled", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Session + "options", (int)EditorSettings.enterPlayModeOptions);
            File.WriteAllText(Root + "isolate.flag", "isolated verification");
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            File.WriteAllText(Root + "status.txt", "STARTING " + phase);
            EditorApplication.EnterPlaymode();
        }
        static void State(PlayModeStateChange state) {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Session, false) && !running) Run();
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Session + "restore", false)) {
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Session + "enabled", false);
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Session + "options", 0);
                SessionState.SetBool(Session + "restore", false);
            }
        }
        static string PathOf(VisualElement e) { var names = new List<string>(); for (; e != null; e = e.parent) names.Add(string.IsNullOrEmpty(e.name) ? e.GetType().Name : e.name); return string.Join(" <- ", names); }
        static async Task HitGrid(VisualElement ui, string phase) {
            var csv = new List<string> { "button,fx,fy,inside,picks_button,path" };
            float[] xs = { .2f, .5f, .8f }, ys = { .2f, .5f, .8f };
            foreach (string id in new[] { "pets-growth-level", "pets-growth-heal", "pets-growth-evolution" }) {
                var button = ui.Q<Button>(id);
                ui.Q<ScrollView>("pets-tab-body-growth").ScrollTo(button);await Task.Delay(180);
                foreach (float y in ys) foreach (float x in xs) {
                    var local = new Vector2(button.layout.width * x, button.layout.height * y);
                    bool inside = button.ContainsPoint(local); var picked = button.panel.Pick(button.LocalToWorld(local));
                    bool correct = picked == button || button.Contains(picked);
                    csv.Add(FormattableString.Invariant($"{id},{x},{y},{inside},{correct},\"{PathOf(picked)}\""));
                    if (phase == "after" && inside) Check(correct, id + " hit " + x + "/" + y);
                }
            }
            File.WriteAllLines(Root + phase + "-growth-hit.csv", csv);
            ui.Q<ScrollView>("pets-tab-body-growth").scrollOffset=Vector2.zero;await Task.Delay(150);
        }
        static async Task Shot(string phase, string name) {
            await Task.Delay(250);
            ScreenCapture.CaptureScreenshot(Root + phase + "-" + name + "-" + Screen.width + "x" + Screen.height + ".png");
            await Task.Delay(250);
        }
        static async void Run() {
            running = true; checks.Clear(); string phase = SessionState.GetString(Session + "phase", "");
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            var size = view.GetType().GetProperty("selectedSizeIndex", Any); int oldSize = (int)size.GetValue(view); bool oldMax = view.maximized;
            GameMenuController menu = null; MonsterInventoryController bag = null; ProfilePanelController profile = null;
            try {
                await Task.Delay(1500); menu = Object.FindFirstObjectByType<GameMenuController>(); bag = Object.FindFirstObjectByType<MonsterInventoryController>(); profile = Object.FindFirstObjectByType<ProfilePanelController>();
                var ui = bag.GetComponent<UIDocument>().rootVisualElement; view.maximized = true; view.Focus();
                if(phase=="native") { TrainerMenuPolishCapture.SelectSize(view,size,new Vector2Int(1366,768));await NativeChecks(menu,bag,profile,ui);File.WriteAllText(Root+"status.txt","COMPLETE native");return; }
                if(phase=="contrast"){await ContrastChecks(menu,bag,ui,view,size);File.WriteAllText(Root+"status.txt","COMPLETE contrast");return;}
                foreach (var res in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) }) {
                    TrainerMenuPolishCapture.SelectSize(view, size, res); await Task.Delay(300);
                    Check(Screen.width == res.x && Screen.height == res.y, "actual GameView " + res);
                    menu.OpenPetsDirect(); await Shot(phase, "pet-overview"); BagVerification.Click(ui, "pets-tab-growth"); await Task.Delay(220); await HitGrid(ui, phase); await Shot(phase, "pet-growth");
                    BagVerification.Click(ui, "pets-tab-skills"); await Shot(phase, "pet-skills"); BagVerification.Click(ui, "pets-tab-overview"); await Task.Delay(200);
                    BagVerification.Click(ui, "pets-details-button");var detail = ui.Q<ScrollView>(className: "ga-details-scroll");detail.scrollOffset=Vector2.zero; await Shot(phase, "pet-details-top"); detail.scrollOffset = new Vector2(0, 2000); await Shot(phase, "pet-details-bottom");
                    Object.FindFirstObjectByType<PetPartyPanelController>().CloseDetails(); bag.Close(); menu.OpenInventoryDirect(); await Shot(phase, "bag-empty-inspector");
                    if(phase=="after"){bag.SelectNextItem();await Shot(phase,"bag-grid");BagVerification.Click(ui,"inventory-view-button");await Shot(phase,"bag-list");BagVerification.Click(ui,"inventory-view-button");}
                    bag.Close();
                    menu.OpenQuestDirect(); await Shot(phase, "quest"); bag.Close(); menu.OpenProfileDirect(); await Shot(phase, "profile"); profile.Close();
                }
                if (phase == "after") await AfterChecks(menu, bag, profile, ui);
                TrainerMenuPolishCapture.SelectSize(view, size, new Vector2Int(1366, 768)); await Counters(menu, bag, profile, phase);
                File.WriteAllText(Root + "status.txt", "COMPLETE " + phase);
            } catch (Exception e) { checks.Add("ERROR " + e); File.WriteAllText(Root + "status.txt", "FAILED " + e); Debug.LogException(e); }
            finally {
                profile?.Close(); bag?.Close(); menu?.CloseMenu(); view.maximized = oldMax; size.SetValue(view, oldSize);
                File.WriteAllLines(Root + phase + "-checks.txt", checks); SessionState.SetBool(Session, false); SessionState.SetBool(Session + "restore", true);
                running = false; if (File.Exists(Root + "isolate.flag")) File.Delete(Root + "isolate.flag"); EditorApplication.ExitPlaymode();
            }
        }
        static async Task Counters(GameMenuController menu, MonsterInventoryController bag, ProfilePanelController profile, string phase) {
            var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
            var available = handles.Select(h => new { h, d = ProfilerRecorderHandle.GetDescription(h) }).ToArray();
            var recorders = new List<ProfilerRecorder>(); var names = new List<string>(); var csv = new List<string>();
            try {
                foreach (string marker in new[] { "Main Thread", "GC Allocated In Frame", "UIElements.UpdateLayout", "UIElements.UpdateRenderData", "UIR.DrawChain", "Canvas.BuildBatch" }) {
                    var found = available.FirstOrDefault(x => x.d.Name == marker); if (found == null) { checks.Add("UNAVAILABLE " + marker); continue; }
                    recorders.Add(new ProfilerRecorder(found.h, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame)); names.Add(marker + " [" + found.d.UnitType + "]");
                }
                csv.Add("panel,frame," + string.Join(",", names)); double end = Time.realtimeSinceStartupAsDouble + 16;
                foreach (string panel in new[] { "Pet", "Bag", "Quest", "Profile" }) {
                    bag.Close(); profile.Close(); await Task.Delay(200);
                    if (panel == "Pet") menu.OpenPetsDirect(); else if (panel == "Bag") menu.OpenInventoryDirect(); else if (panel == "Quest") menu.OpenQuestDirect(); else menu.OpenProfileDirect();
                    await Task.Delay(250); int previous = -1;
                    for (int sample = 0; sample < 30 && Time.realtimeSinceStartupAsDouble < end;) { await Task.Delay(16); if (Time.frameCount == previous) continue; previous = Time.frameCount; csv.Add(panel + "," + previous + "," + string.Join(",", recorders.Select(r => r.LastValue))); sample++; }
                }
                File.WriteAllLines(Root + phase + "-profiler.csv", csv);
            } finally { foreach (var recorder in recorders) recorder.Dispose(); }
        }
    }
}
