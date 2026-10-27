using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.ProfileSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Captures the real Game view; restores its size and any Play session it starts.</summary>
    public static class TrainerMenuPolishCapture {
        public const string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "trainer-menu-v4/";
        const string Request = "Temp/CaptureTrainerMenu.request";
        const string OwnPlay = "TrainerMenuPolishCapture.OwnPlay";
        static bool running;
        static double nextPoll;
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        [InitializeOnLoadMethod] static void Initialize() { EditorApplication.update -= Poll; EditorApplication.update += Poll; }
        static void Poll() {
            if (running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(Request)) return;
            if (!EditorApplication.isPlaying) {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(OwnPlay, true); EditorApplication.EnterPlaymode(); return;
            }
            if (Object.FindFirstObjectByType<GameMenuController>() == null) return;
            string stage = File.ReadAllText(Request).Trim() == "before" ? "before" : "after";
            File.Delete(Request); Capture(stage);
        }
        static async void Capture(string stage) {
            running = true; Directory.CreateDirectory(Output);
            var log = new List<string>();
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var inventory = Object.FindFirstObjectByType<MonsterInventoryController>();
            var profile = Object.FindFirstObjectByType<ProfilePanelController>();
            var map = Object.FindFirstObjectByType<MapInputController>();
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            EditorWindow view = null; PropertyInfo selected = null; int previous = -1;
            bool wasOpen = menu != null && menu.IsMenuOpen;
            try {
                await Task.Delay(1400);
                view = EditorWindow.GetWindow(type); selected = type.GetProperty("selectedSizeIndex", Any);
                previous = (int)selected.GetValue(view);
                inventory?.Close(); profile?.Close(); map?.CloseMap(); menu.CloseMenu();
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) }) {
                    SelectSize(view, selected, size);
                    view.Show(); view.Focus(); await Task.Delay(750);
                    if (Screen.width != size.x || Screen.height != size.y)
                        throw new InvalidOperationException("Game view actual " + Screen.width + "x" + Screen.height + "; requested " + size);
                    menu.OpenMenu(); await Task.Delay(400);
                    // Reference shows normal cards. Remove the default keyboard-focus ring for image comparison only.
                    // The runtime Menu still selects Pets normally; state fixtures cover its focus ring separately.
                    if (stage == "after") { EventSystem.current?.SetSelectedGameObject(null); await Task.Delay(200); }
                    RecordBounds(menu, stage, size);
                    string path = Output + stage + "-game-" + size.x + "x" + size.y + ".png";
                    ScreenCapture.CaptureScreenshot(path); await Task.Delay(500);
                    log.Add("PASS real Game view " + size + ": " + path);
                    menu.CloseMenu(); await Task.Delay(200);
                }
            } catch (Exception e) { log.Add("ERROR " + e); Debug.LogException(e); }
            finally {
                if (view != null && selected != null && previous >= 0) selected.SetValue(view, previous);
                if (wasOpen) menu?.OpenMenu(); else menu?.CloseMenu();
                File.WriteAllLines(Output + stage + "-capture.txt", log); running = false;
                if (SessionState.GetBool(OwnPlay, false)) { SessionState.SetBool(OwnPlay, false); EditorApplication.ExitPlaymode(); }
            }
        }
        [Serializable] sealed class BoundsRecord { public string id; public float x,y,width,height; }
        [Serializable] sealed class BoundsFile { public int width,height; public BoundsRecord[] boxes; }
        static void RecordBounds(GameMenuController menu, string stage, Vector2Int size) {
            Canvas.ForceUpdateCanvases();
            var fields = new Dictionary<string,string> {
                {"MenuShell","shell"}, {"TrainerSummary","profile_inset"}, {"AvatarFrame","avatar_ring"},
                {"PetsButton","pet_primary"}, {"InventoryButton","bag_primary"},
                {"ProfileButton","profile_secondary"}, {"QuestButton","quest_secondary"},
                {"MapButton","map_secondary"}, {"CodexButton","codex_secondary"},
                {"QuickInfo","info"}, {"SettingsButton","settings"}, {"BackButton","back"}
            };
            var root = (RectTransform)typeof(GameMenuController).GetField("root",Any).GetValue(menu);
            var records = new List<BoundsRecord>();
            foreach (var rect in root.GetComponentsInChildren<RectTransform>()) {
                if (!fields.TryGetValue(rect.name,out var id)) continue;
                var canvas = rect.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var a = RectTransformUtility.WorldToScreenPoint(camera,corners[1]);
                var b = RectTransformUtility.WorldToScreenPoint(camera,corners[3]);
                records.Add(new BoundsRecord { id=id,x=a.x,y=Screen.height-a.y,width=b.x-a.x,height=a.y-b.y });
            }
            File.WriteAllText(Output+stage+"-bounds-"+size.x+"x"+size.y+".json",JsonUtility.ToJson(new BoundsFile {width=size.x,height=size.y,boxes=records.ToArray()},true));
        }
        public static void SelectSize(EditorWindow view, PropertyInfo selected, Vector2Int size) {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance", Any).GetValue(null);
            var group = sizesType.GetProperty("currentGroup", Any).GetValue(sizes);
            var groupType = group.GetType(); int total = (int)groupType.GetMethod("GetTotalCount", Any).Invoke(group, null);
            for (int i = 0; i < total; i++) {
                var entry = groupType.GetMethod("GetGameViewSize", Any).Invoke(group, new object[] { i });
                int width = (int)entry.GetType().GetProperty("width", Any).GetValue(entry);
                int height = (int)entry.GetType().GetProperty("height", Any).GetValue(entry);
                if (width == size.x && height == size.y) { selected.SetValue(view, i); return; }
            }
            var mode = assembly.GetType("UnityEditor.GameViewSizeType");
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var custom = Activator.CreateInstance(sizeType, Any, null, new[] { Enum.Parse(mode, "FixedResolution"), (object)size.x, size.y, "Menu QA " + size.x + "x" + size.y }, null);
            groupType.GetMethod("AddCustomSize", Any).Invoke(group, new[] { custom });
            selected.SetValue(view, total);
        }
    }
}

