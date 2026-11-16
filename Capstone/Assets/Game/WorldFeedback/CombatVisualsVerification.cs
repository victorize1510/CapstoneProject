#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Capstone.Game.SaveSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.UISystem;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Opt-in QA transport. Never runs, redirects saves, or captures without an external request.</summary>
    public static class CombatVisualsVerification {
        public const string Root = QaArtifactPaths.Root + "combat-visuals-camera-rework-2026-11-12/";
        const string Session = "Capstone.CombatVisuals.Qa";
        static double nextPoll;
        static bool busy;
        static int captures;
        static string Stage => SessionState.GetString(Session + ".stage", "baseline");
        public static void Write(string name, string content) {
            Directory.CreateDirectory(Root);
            File.WriteAllText(Root + name, content);
            File.AppendAllText(Root + "created-paths.txt", Root + name + "\n");
        }
        [InitializeOnLoadMethod] static void Install() {
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
            EditorApplication.playModeStateChanged -= OnPlay; EditorApplication.playModeStateChanged += OnPlay;
            Write("bridge-ready.txt", "combat-visuals-r1-v3 " + DateTime.UtcNow.ToString("o"));
        }
        static void OnPlay(PlayModeStateChange state) {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Session, false)) return;
            SessionState.SetBool(Session, false); busy = false;
            Write("session-ended.txt", DateTime.UtcNow.ToString("o"));
        }
        static async void Poll() {
            if (busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            string path = Root + "command.request";
            if (!File.Exists(path)) return;
            string[] args = File.ReadAllText(path).Trim().Split(' '); File.Delete(path);
            busy = true;
            try {
                if ((args[0] == "arm" || args[0] == "begin") && !EditorApplication.isPlayingOrWillChangePlaymode) {
                    if (SceneManager.GetActiveScene().name != "DemoScene") throw new InvalidOperationException("DemoScene must already be open.");
                    SessionState.SetString(Session + ".stage", args.Length > 1 ? args[1] : "baseline");
                    string save = Root + "test-saves/" + Stage;
                    Directory.CreateDirectory(save);
                    string real = Path.Combine(Application.persistentDataPath, "Saves");
                    var hashes = new StringBuilder();
                    if (Directory.Exists(real)) foreach (string file in Directory.GetFiles(real)) {
                        string dest = Path.Combine(save, Path.GetFileName(file));
                        if (!File.Exists(dest)) { File.Copy(file, dest); File.AppendAllText(Root + "created-paths.txt", dest + "\n"); }
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                            hashes.AppendLine(file + " " + BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(file))));
                    }
                    Write(Stage + "-real-save-hashes.txt", hashes.ToString());
                    SessionState.SetString(Session + ".save", save); SessionState.SetBool(Session, true);
                    Write("session-status.txt", "ARMED " + Stage + "\n" + save + "\nNative Play required.");
                    if (args[0] == "begin") EditorApplication.EnterPlaymode();
                } else if (args[0] == "snapshot" && EditorApplication.isPlaying && SessionState.GetBool(Session, false)) {
                    if (captures >= 10) throw new InvalidOperationException("10 capture budget exhausted; remove task-owned duplicates before rearming.");
                    await Task.Delay(200); string name = Stage + "-" + (args.Length > 1 ? args[1] : "frame") + ".png";
                    string capture = Root + name; ScreenCapture.CaptureScreenshot(capture); captures++;
                    File.AppendAllText(Root + "created-paths.txt", capture + "\n"); Write(name + ".txt", Describe());
                } else if (args[0] == "size") {
                    int width = int.Parse(args[1]), height = int.Parse(args[2]);
                    Type helper = null;
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                        helper = assembly.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture");
                        if (helper != null) break;
                    }
                    if (helper == null) throw new InvalidOperationException("Existing GameView size helper is unavailable.");
                    var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
                    var view = EditorWindow.GetWindow(viewType);
                    var selected = viewType.GetProperty("selectedSizeIndex", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    helper.GetMethod("SelectSize", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic).Invoke(null, new object[] { view, selected, new Vector2Int(width, height) });
                    Write("size-status.txt", width + "x" + height);
                } else if (args[0] == "describe" && EditorApplication.isPlaying) Write(Stage + "-runtime.txt", Describe());
                else if (args[0] == "assets" && !EditorApplication.isPlayingOrWillChangePlaymode) {
                    Type author = null; foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) { author = assembly.GetType("Capstone.Game.WorldFeedback.Editor.CombatVisualsAssetSetup"); if (author != null) break; }
                    if (author == null) throw new InvalidOperationException("Asset authoring helper unavailable.");
                    author.GetMethod("Apply").Invoke(null, null);
                }
                else if (args[0] == "verify" && EditorApplication.isPlaying && SessionState.GetBool(Session, false)) await CombatVisualsEngineChecks.Run();
                else if (args[0] == "load-copy" && EditorApplication.isPlaying && SessionState.GetBool(Session, false)) {
                    var save = Object.FindFirstObjectByType<PlayerSaveController>();
                    if (save == null || !save.SavePath.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Save not isolated");
                    Write("copy-load.txt", "loaded=" + save.LoadNow() + " reason=" + save.SaveBlockedReason + "\n" + Describe());
                }
                else if ((args[0] == "profile" || args[0] == "benchmark") && EditorApplication.isPlaying && SessionState.GetBool(Session, false)) {
                    var brains = new List<Capstone.Game.CreatureSystem.BasicCreatureEnemy>();
                    if (args[0] == "benchmark") foreach (var brain in Object.FindObjectsByType<Capstone.Game.CreatureSystem.BasicCreatureEnemy>(FindObjectsSortMode.None))
                        if (brain.enabled) { brains.Add(brain); brain.enabled = false; }
                    try { await Profile(args.Length > 1 ? args[1] : "idle"); }
                    finally { foreach (var brain in brains) if (brain != null) brain.enabled = true; }
                }
                else if (args[0] == "stop" && EditorApplication.isPlaying && SessionState.GetBool(Session, false)) EditorApplication.ExitPlaymode();
                else if (args[0] == "disarm") SessionState.SetBool(Session, false);
                else Write("command-error.txt", "Rejected command/state: " + string.Join(" ", args));
            } catch (Exception e) { Write("command-error.txt", e.ToString()); Debug.LogException(e); }
            finally { busy = false; }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Isolate() {
            if (!SessionState.GetBool(Session, false)) return;
            string path = SessionState.GetString(Session + ".save", "");
            if (!path.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe QA save path");
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new PlayerJsonFileSaveStore(path));
            foreach (var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new QuestJsonFileSaveStore(path));
            Write("session-status.txt", "ISOLATED " + Stage + "\n" + path); captures = 0;
        }
        static string Describe() {
            var text = new StringBuilder(); text.AppendLine("scene=" + SceneManager.GetActiveScene().name + " screen=" + Screen.width + "x" + Screen.height);
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) text.AppendLine("camera=" + camera.name + " enabled=" + camera.enabled + " pos=" + camera.transform.position.ToString("F3") + " rot=" + camera.transform.eulerAngles.ToString("F3"));
            var input = Object.FindFirstObjectByType<PetCommandInput>();
            if (input != null) text.AppendLine("player=" + input.transform.position.ToString("F3") + " pet=" + (input.activePet == null ? "none" : input.activePet.name));
            var fade = Object.FindFirstObjectByType<CameraVisibility>();
            if (fade != null) text.AppendLine("fade cached=" + fade.CachedRendererCount + " faded=" + fade.FadedRendererCount + " unsupported=" + fade.UnsupportedRendererCount);
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsSortMode.None)) text.AppendLine("save=" + save.SavePath);
            text.AppendLine("materials=" + Resources.FindObjectsOfTypeAll<Material>().Length);
            return text.ToString();
        }
        static async Task Profile(string label) {
            // 2 s warmup + 240 *distinct* frames, bounded to 20 s. No recorder binary output.
            await Task.Delay(2000);
            using (var cpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1))
            using (var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1))
            using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1)) {
                var csv = new StringBuilder("frame,cpu_ms,gc_bytes,batches\n");
                int last = -1, samples = 0; double deadline = EditorApplication.timeSinceStartup + 20;
                while (EditorApplication.isPlaying && samples < 240 && EditorApplication.timeSinceStartup < deadline) {
                    await Task.Delay(1); if (Time.frameCount == last) continue; last = Time.frameCount;
                    csv.AppendLine(last + "," + (cpu.LastValue * .000001).ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + "," + gc.LastValue + "," + batches.LastValue); samples++;
                }
                Write(Stage + "-profile-" + label + ".csv", csv.ToString());
                Write(Stage + "-profile-" + label + ".txt", "samples=" + samples + " cpuValid=" + cpu.Valid + " gcValid=" + gc.Valid + " batchesValid=" + batches.Valid + "\n" + Describe());
            }
            using (var collision = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Capstone.Camera.Collision", 1))
            using (var scan = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Capstone.Camera.OcclusionScan", 1))
            using (var materials = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Capstone.Camera.MaterialScope", 1)) {
                var csv = new StringBuilder("frame,collision_us,occlusion_us,material_scope_us\n");
                int frame = -1, count = 0; double limit = EditorApplication.timeSinceStartup + 10;
                while (EditorApplication.isPlaying && count < 120 && EditorApplication.timeSinceStartup < limit) {
                    await Task.Delay(1); if (frame == Time.frameCount) continue; frame = Time.frameCount; count++;
                    csv.AppendLine(FormattableString.Invariant($"{frame},{collision.LastValue*.001},{scan.LastValue*.001},{materials.LastValue*.001}"));
                }
                Write(Stage + "-markers-" + label + ".csv", csv.ToString());
                Write(Stage + "-markers-" + label + ".txt", "collision=" + collision.Valid + " scan=" + scan.Valid + " materials=" + materials.Valid + " samples=" + count);
            }
        }
    }
}
#endif
