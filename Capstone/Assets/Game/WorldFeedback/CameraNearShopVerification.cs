#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CreatureSystem;
using Capstone.Game.QuestSystem.Save;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Capstone.Game.WorldFeedback
{
    public static class CameraNearShopVerification
    {
        public const string Root = QaArtifactPaths.Root + "camera-near-shop-2026-11-14/";
        const string Session = "Capstone.CameraNearShopQA";
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static readonly List<string> checks = new List<string>();
        static bool running;
        static void Write(string name, string data)
        {
            Directory.CreateDirectory(Root); File.WriteAllText(Root + name, data);
            File.AppendAllText(Root + "created-paths.txt", Root + name + "\n");
        }
        [MenuItem("Tools/Capstone/Advanced/Camera/Verify near-shop player visibility")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "DemoScene") return;
            SessionState.SetBool(Session, true); checks.Clear();
            SessionState.SetBool(Session + "reload", EditorSettings.enterPlayModeOptionsEnabled);
            SessionState.SetInt(Session + "options", (int)EditorSettings.enterPlayModeOptions);
            SessionState.SetString(Session + "save", Root + "test-saves/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            EditorSettings.enterPlayModeOptionsEnabled = true; EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            Write("status.txt", "ENTERING_PLAY"); EditorApplication.EnterPlaymode();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot()
        {
            if (!SessionState.GetBool(Session, false) || running) return;
            string path = SessionState.GetString(Session + "save", "");
            foreach (var save in Object.FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new PlayerJsonFileSaveStore(path));
            foreach (var save in Object.FindObjectsByType<QuestSaveController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) save.SetSaveStore(new QuestJsonFileSaveStore(path));
            Run();
        }
        static void Check(bool success, string name) { checks.Add((success ? "PASS " : "FAIL ") + name); Write("checks.txt", string.Join("\n", checks)); }
        static async Task Wait(int ms = 150) { await Task.Delay(ms); if (!EditorApplication.isPlaying) throw new OperationCanceledException("Play ended"); }
        static async Task Shot(string name)
        {
            File.AppendAllText(Root + "created-paths.txt", Root + name + ".png\n");
            ScreenCapture.CaptureScreenshot(Root + name + ".png"); await Wait(200);
        }
        static async void Run()
        {
            running = true; GameObject driver = null; BasicPlayerMovement movement = null; bool movementEnabled = false;
            CameraVisibility visibility = null; object near = null; EditorWindow view = null; PropertyInfo size = null; int previousSize = -1;
            BasicCameraFollow source = null; bool previousCameraGate = false;
            var enemies = new List<(BasicCreatureEnemy ai, bool enabled)>();
            try
            {
                await Wait(1000); Write("status.txt", "RUNNING");
                var save = Object.FindFirstObjectByType<PlayerSaveController>();
                Check(save != null && save.SavePath.StartsWith(Root, StringComparison.OrdinalIgnoreCase), "save isolated on E before Start");
                source = Object.FindFirstObjectByType<BasicCameraFollow>(); var rig = source.freeLookRig;
                previousCameraGate = (bool)typeof(BasicCameraFollow).GetField("gameplayInputLocked", Any).GetValue(source);
                // Keep visibility enabled, but let only the pose driver update the
                // manual brain. Two updates per frame invalidate collision evidence.
                source.SetGameplayInputLocked(true);
                visibility = source.GetComponent<CameraVisibility>(); var camera = source.GetComponent<Camera>();
                var visuals = Object.FindFirstObjectByType<PlayerVisualSwitcher>();
                movement = visuals.GetComponent<BasicPlayerMovement>(); movementEnabled = movement.enabled; movement.enabled = false;
                foreach (var ai in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)) { enemies.Add((ai, ai.enabled)); ai.enabled = false; }
                foreach (var family in new[] { "Lit", "SimpleLit", "Unlit" })
                {
                    var shader = Resources.Load<Shader>("WorldFeedback/CameraDither" + family);
                    Check(shader != null && !ShaderUtil.ShaderHasError(shader), "shader imports without error: " + family);
                }
                var body = new Bounds(Vector3.zero, new Vector3(.6f, 1.8f, .5f));
                Check(CameraNearPlayerVisibility.DesiredFade(body, Vector3.back * 4, .3f) == 0, "normal distance stays opaque");
                Check(CameraNearPlayerVisibility.DesiredFade(body, Vector3.zero, .3f) == 1, "inside body fully hidden before near-plane slicing");
                float middle = CameraNearPlayerVisibility.DesiredFade(body, Vector3.back * 1, .3f);
                Check(middle > 0 && middle < 1, "soft transition exists before close-up safety zone");
                var shop = GameObject.Find("ShopNPC"); var collider = shop.GetComponent<Collider>(); var shopRenderer = shop.GetComponent<Renderer>();
                Write("shop-inspection.txt", "collider=" + collider.GetType().Name + " bounds=" + collider.bounds + "\nmesh=" + shop.GetComponent<MeshFilter>().sharedMesh.name + " bounds=" + shopRenderer.bounds + "\nnearClip=" + camera.nearClipPlane + "\n");
                Vector3 initial = source.target.position;
                Vector3 position = new Vector3(collider.bounds.center.x, initial.y, collider.bounds.max.z + 1.2f);
                source.target.position = position; source.target.rotation = Quaternion.identity; Physics.SyncTransforms();
                rig.freeCamera.PreviousStateIsValid = false; rig.lockCamera.PreviousStateIsValid = false;
                driver = new GameObject("Near shop QA pose driver"); var playback = driver.AddComponent<CameraBoomQaPlayback>();
                playback.rig = rig; playback.position = position; playback.yaw = 0; playback.pitch = 18; playback.distance = 5;
                var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"); view = EditorWindow.GetWindow(viewType);
                size = viewType.GetProperty("selectedSizeIndex", Any); previousSize = (int)size.GetValue(view);
                var select = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t => t != null).GetMethod("SelectSize", Any);
                var nearField = typeof(CameraVisibility).GetField("nearPlayer", Any);
                near = nearField.GetValue(visibility);
                foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) })
                {
                    select.Invoke(null, new object[] { view, size, resolution }); view.Focus();
                    for (int form = 0; form < 2; form++)
                    {
                        visuals.SwitchTo(form, true); await Wait(350);
                        Check(visibility.NearPlayerFade >= .999f, "close shop hides complete avatar " + visuals.ActiveVisualName + " " + resolution);
                        Check(visibility.NearPlayerMaterialCount > 0, "close-up adapters cached for " + visuals.ActiveVisualName);
                        Vector3 raw = rig.freeCamera.State.RawPosition - rig.orbitTarget.position;
                        Vector3 requested = rig.orbitTarget.position + raw.normalized * rig.ZoomDistance;
                        float angle = Vector3.Angle(source.transform.position - rig.CollisionOrigin, requested - rig.CollisionOrigin);
                        Check(angle < 1, "shop obstruction preserves requested boom " + form + " " + resolution + " angle=" + angle.ToString("F3"));
                        Check(Screen.width == resolution.x && Screen.height == resolution.y, "actual resolution " + resolution);
                        if (form == 1)
                        {
                            ((CameraNearPlayerVisibility)near).Reset(); nearField.SetValue(visibility, null); await Wait();
                            await Shot("before-close-" + resolution.x); nearField.SetValue(visibility, near); await Wait(250);
                        }
                        await Shot("after-close-" + form + "-" + resolution.x);
                    }
                }
                var active = visuals.ActiveVisualRoot.GetComponentsInChildren<Renderer>().Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                var originals = active.Select(r => r.sharedMaterials).ToArray();
                var begin = typeof(CameraVisibility).GetMethod("BeginCamera", Any); var end = typeof(CameraVisibility).GetMethod("EndCamera", Any);
                var context = default(UnityEngine.Rendering.ScriptableRenderContext);
                begin.Invoke(visibility, new object[] { context, camera });
                Check(active.Any(r => r.sharedMaterials.Any(m => m != null && m.HasProperty("_CapstoneActorFade") && m.GetFloat("_CapstoneActorFade") >= .999f)), "gameplay render applies whole-avatar fade");
                Check(visuals.ActiveVisualRoot.GetComponentsInChildren<AAMAP.MapIcon>(true)
                    .SelectMany(icon => icon.GetComponentsInChildren<Renderer>(true))
                    .All(r => !r.forceRenderingOff && !r.sharedMaterials.Any(m => m != null && m.HasProperty("_CapstoneActorFade") && m.GetFloat("_CapstoneActorFade") > 0)),
                    "player map icons are excluded from close-up actor materials");
                var secondaryObject = new GameObject("QA secondary camera"); var secondary = secondaryObject.AddComponent<Camera>(); secondary.enabled = false;
                begin.Invoke(visibility, new object[] { context, secondary });
                Check(active.Select((r, i) => r.sharedMaterials.SequenceEqual(originals[i])).All(v => v), "nested minimap/other camera restores authored player materials");
                end.Invoke(visibility, new object[] { context, secondary });
                Check(Shader.GetGlobalFloat("_CapstoneOcclusionView") == 1, "nested camera restores outer gameplay scope");
                end.Invoke(visibility, new object[] { context, camera }); Object.Destroy(secondaryObject);
                Check(active.Select((r, i) => r.sharedMaterials.SequenceEqual(originals[i])).All(v => v), "render end restores original materials");
                playback.position = position + Vector3.forward * 6; source.target.position = playback.position; Physics.SyncTransforms(); await Wait(600);
                Check(visibility.NearPlayerFade == 0, "leaving shop restores opaque avatar"); await Shot("after-clear-1366");
                bool transitionSeen = false;
                for (float offset = .4f; offset <= 1.9f; offset += .2f)
                {
                    playback.position = position + Vector3.forward * offset; source.target.position = playback.position;
                    Physics.SyncTransforms(); await Wait(350);
                    if (visibility.NearPlayerFade > .05f && visibility.NearPlayerFade < .95f)
                    {
                        transitionSeen = true; Write("transition.txt", "actual soft fade=" + visibility.NearPlayerFade);
                        await Shot("after-transition-1366"); break;
                    }
                }
                Check(transitionSeen, "actual shop approach has intermediate whole-avatar fade");
                int cacheCount = visibility.NearPlayerMaterialCount;
                for (int i = 0; i < 4; i++) { playback.position = position; source.target.position = position; await Wait(150); playback.position = position + Vector3.forward * 6; source.target.position = playback.position; await Wait(250); }
                Check(visibility.NearPlayerMaterialCount == cacheCount, "repeated wall approach does not grow material cache");
                playback.position = position; source.target.position = position; Physics.SyncTransforms(); await Wait(350);
                Check(visibility.NearPlayerFade >= .999f, "allocation measurement runs with active close-up material scopes");
                var helper = (CameraNearPlayerVisibility)near;
                helper.Update(visuals.ActiveVisualRoot.transform, camera, 1f/60); helper.Apply(); helper.Restore();
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 500; i++) { helper.Update(visuals.ActiveVisualRoot.transform, camera, 1f/60); helper.Apply(); helper.Restore(); }
                long allocation = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Check(allocation == 0, "500 warmed close-up update/scopes allocate zero managed bytes: " + allocation);
                visibility.enabled = false;
                Check(visibility.NearPlayerFade == 0 && visibility.NearPlayerMaterialCount == 0, "disable restores avatar and releases clones"); visibility.enabled = true;
                Write("status.txt", (checks.Any(c => c.StartsWith("FAIL")) ? "FINDINGS " : "COMPLETE ") + checks.Count + " checks; actual rig and ShopNPC; engine-driven pose, not manual traversal");
            }
            catch (Exception exception) { Write("status.txt", "FAILED\n" + exception); Debug.LogException(exception); }
            finally
            {
                if (visibility != null && near != null) typeof(CameraVisibility).GetField("nearPlayer", Any).SetValue(visibility, near);
                if (driver != null) Object.Destroy(driver);
                if (source != null) source.SetGameplayInputLocked(previousCameraGate);
                if (movement != null) movement.enabled = movementEnabled;
                foreach (var entry in enemies) if (entry.ai != null) entry.ai.enabled = entry.enabled;
                if (view != null && previousSize >= 0) size.SetValue(view, previousSize);
                SessionState.SetBool(Session, false); running = false;
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Session + "reload", false);
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Session + "options", 0);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
    }
}
#endif
