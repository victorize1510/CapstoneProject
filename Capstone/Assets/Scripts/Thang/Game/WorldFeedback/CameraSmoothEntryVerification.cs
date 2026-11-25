#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    public static class CameraSmoothEntryVerification
    {
        public static string Root => QaArtifactPaths.Root + "camera-smooth-entry-2026-11-14/";
        const string Session = "Capstone.CameraSmoothEntry";
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly List<string> checks = new List<string>();
        static bool running;
        internal static void Write(string name, string text)
        {
            Directory.CreateDirectory(Root); File.WriteAllText(Root + name, text);
            File.AppendAllText(Root + "created-paths.txt", Root + name + "\n");
        }
        static void Check(bool pass, string text) { checks.Add((pass ? "PASS " : "FAIL ") + text); Write("checks.txt", string.Join("\n", checks)); }
        [MenuItem("Tools/Capstone/Advanced/Camera/Verify smooth obstruction entry")]
        public static void Verify()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "DemoScene") return;
            checks.Clear(); SessionState.SetBool(Session, true);
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
        static async Task Wait(int ms = 100) { await Task.Delay(ms); if (!EditorApplication.isPlaying) throw new OperationCanceledException(); }
        static void PureChecks()
        {
            var rigObject = new GameObject("QA collision source", typeof(Camera)); var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lens = rigObject.GetComponent<Camera>(); lens.enabled = false; lens.aspect = 16f / 9; lens.fieldOfView = 60; lens.nearClipPlane = .3f;
                var source = rigObject.AddComponent<BasicCameraFollow>(); source.enabled = false; source.target = rigObject.transform;
                Vector3 origin = new Vector3(3000, 100, 3000);
                wall.transform.position = origin + Vector3.back * 1.6f; wall.transform.localScale = new Vector3(.8f, 4, .7f); Physics.SyncTransforms();
                var records = new StringBuilder("fps,mode,time,requested,safe,rendered,inwardStep\n");
                foreach (int fps in new[] { 30, 60, 120 })
                {
                    float baseline = 0, revised = 0; bool safePath = true;
                    for (int mode = 0; mode < 2; mode++)
                    {
                        source.cameraCollisionEnterTime = mode == 0 ? 0 : .16f;
                        source.cameraCollisionLookAhead = mode == 0 ? 0 : .28f;
                        var recovery = new CameraBoomRecovery(); float previous = 5, maximum = 0;
                        for (int frame = 0; frame < fps * 3; frame++)
                        {
                            float t = frame / (float)fps;
                            float yaw = Mathf.Lerp(-100, 100, Mathf.Clamp01((t - .3f) / 2.4f));
                            Vector3 desired = origin + Quaternion.Euler(0, yaw, 0) * Vector3.back * 5;
                            Vector3 actual = source.ConstrainRenderedCamera(origin, desired, ref recovery, 1f / fps);
                            float distance = Vector3.Distance(actual, origin), safe = Vector3.Distance(source.ConstrainCameraPosition(origin, desired), origin);
                            float step = Mathf.Max(0, previous - distance); if (frame > 1) maximum = Mathf.Max(maximum, step); previous = distance;
                            safePath &= distance <= safe + .001f && Vector3.Cross(actual - origin, desired - origin).magnitude < .001f;
                            records.AppendLine(FormattableString.Invariant($"{fps},{mode},{t},5,{safe},{distance},{step}"));
                        }
                        if (mode == 0) baseline = maximum; else revised = maximum;
                    }
                    Check(safePath, "sweep stays on requested ray and outside wall at " + fps + "Hz");
                    Check(revised < baseline * .5f, "inward step reduced >50% at " + fps + "Hz: " + baseline.ToString("F3") + " -> " + revised.ToString("F3"));
                }
                Write("deterministic-motion.csv", records.ToString());
                var safety = new CameraBoomRecovery(); safety.Approach(5, 5, 5, origin, .016f, .16f, .24f);
                Check(safety.Approach(.3f, .3f, 5, origin, .016f, .16f, .24f) <= .3f, "sudden unpredicted obstacle keeps immediate safety clamp");
                wall.SetActive(true); wall.transform.position = origin + Vector3.forward;
                wall.transform.localScale = new Vector3(8, 4, .2f); Physics.SyncTransforms();
                var forwardWall = new CameraBoomRecovery(); float smallest = 5;
                for (int frame = 0; frame <= 24; frame++)
                {
                    Vector3 pivot = origin + Vector3.forward * (frame * .025f);
                    smallest = Mathf.Min(smallest, Vector3.Distance(pivot,
                        source.ConstrainRenderedCamera(pivot, pivot + Vector3.back * 5, ref forwardWall, 1f / 60)));
                }
                Check(smallest > 4.99f, "approaching a front wall cannot retract a clear camera behind the player");
                wall.SetActive(false); Physics.SyncTransforms();
                var clear = new CameraBoomRecovery(); Vector3 clearDesired = origin + Vector3.back * 5;
                Check(Vector3.Distance(source.ConstrainRenderedCamera(origin, clearDesired, ref clear, .016f), clearDesired) < .001f, "clear idle camera keeps authored distance");
                source.ConstrainRenderedCamera(origin, clearDesired, ref clear, .016f);
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 500; i++) source.ConstrainRenderedCamera(origin, clearDesired, ref clear, .016f);
                long used = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Check(used == 0, "500 warm predictive solves allocate zero managed bytes: " + used);
            }
            finally { Object.DestroyImmediate(wall); Object.DestroyImmediate(rigObject); }
        }
        static async void Run()
        {
            running = true; BasicCameraFollow source = null; BasicPlayerMovement movement = null; CameraSmoothEntryPlayback playback = null;
            bool oldGate = false, oldMovement = false, oldFocused = true; float oldEnter = 0, oldAhead = 0;
            EditorWindow view = null; PropertyInfo size = null; int oldSize = -1;
            var enemies = new List<(BasicCreatureEnemy ai, bool enabled)>();
            try
            {
                await Wait(1000); Write("status.txt", "RUNNING");
                Check(Object.FindFirstObjectByType<PlayerSaveController>().SavePath.StartsWith(Root, StringComparison.OrdinalIgnoreCase), "isolated save on E before Start");
                PureChecks();
                source = Object.FindFirstObjectByType<BasicCameraFollow>(); oldEnter = source.cameraCollisionEnterTime; oldAhead = source.cameraCollisionLookAhead;
                oldGate = (bool)typeof(BasicCameraFollow).GetField("gameplayInputLocked", Any).GetValue(source); source.SetGameplayInputLocked(true);
                oldFocused = (bool)typeof(BasicCameraFollow).GetField("applicationFocused", Any).GetValue(source);
                movement = source.target.GetComponent<BasicPlayerMovement>(); oldMovement = movement.enabled; movement.enabled = false;
                foreach (var ai in Object.FindObjectsByType<BasicCreatureEnemy>(FindObjectsSortMode.None)) { enemies.Add((ai, ai.enabled)); ai.enabled = false; }
                var shop = GameObject.Find("ShopNPC").GetComponent<Collider>();
                Vector3 position = new Vector3(shop.bounds.center.x, source.target.position.y, shop.bounds.max.z + 1.2f);
                // Loading the scene can leave only a few simulated gravity frames
                // before this test starts. Place the test actor on real ground;
                // an airborne pivot would sweep over the shop without hitting it.
                var groundHits = Physics.RaycastAll(position + Vector3.up * 8, Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore);
                var ground = groundHits.Where(hit => hit.normal.y > .7f && hit.collider != shop
                    && !hit.collider.transform.IsChildOf(source.target)
                    && hit.collider.GetComponentInParent<DummyEnemy>() == null)
                    .OrderBy(hit => hit.distance).FirstOrDefault();
                Check(ground.collider != null, "native sweep fixture grounded on actual scene surface");
                if (ground.collider != null) position.y = ground.point.y + .02f;
                Write("scene-pose.txt", "actor=" + position + " ground=" + ground.collider?.name + " shop=" + shop.bounds);
                source.target.position = position; Physics.SyncTransforms();
                playback = new GameObject("QA smooth-entry pose driver").AddComponent<CameraSmoothEntryPlayback>(); playback.source = source; playback.position = position;
                var viewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"); view = EditorWindow.GetWindow(viewType);
                size = viewType.GetProperty("selectedSizeIndex", Any); oldSize = (int)size.GetValue(view);
                var select = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Capstone.Game.UISystem.Editor.TrainerMenuPolishCapture")).First(t => t != null).GetMethod("SelectSize", Any);
                foreach (var res in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) })
                {
                    select.Invoke(null, new object[] { view, size, res }); view.Focus();
                    float before = 0;
                    for (int mode = 0; mode < 2; mode++)
                    {
                        source.cameraCollisionEnterTime = mode == 0 ? 0 : .16f; source.cameraCollisionLookAhead = mode == 0 ? 0 : .28f;
                        source.freeLookRig.freeCamera.PreviousStateIsValid = false; source.freeLookRig.lockCamera.PreviousStateIsValid = false;
                        var extension = source.freeLookRig.freeCamera.GetComponent<PlayerCameraCollision>(); extension.enabled = false; extension.enabled = true;
                        playback.StartSweep((mode == 0 ? "before" : "after") + "-" + res.x);
                        while (!playback.done) await Wait();
                        Write(playback.label + ".csv", playback.trace.ToString());
                        var cameraRig = source.freeLookRig;
                        Write(playback.label + "-diagnostic.txt", "timescale=" + Time.timeScale + " dt=" + Time.deltaTime
                            + " axis=" + cameraRig.orbit.HorizontalAxis.Value + " range=" + cameraRig.orbit.HorizontalAxis.Range
                            + " live=" + cameraRig.brain.ActiveVirtualCamera?.Name + " solo=" + Unity.Cinemachine.CinemachineCore.SoloCamera?.Name
                            + " brain=" + cameraRig.brain.UpdateMethod + " enabled=" + cameraRig.brain.enabled
                            + " focus=" + typeof(BasicCameraFollow).GetField("applicationFocused", Any).GetValue(source)
                            + " raw=" + cameraRig.freeCamera.State.RawPosition + " output=" + source.transform.position);
                        Check(playback.safe, "actual rig remains collision-safe " + playback.label);
                        Check(Screen.width == res.x && Screen.height == res.y, "actual resolution " + playback.label);
                        if (mode == 0) { before = playback.maxSpeed; Check(before > 1, "baseline actually crosses shop obstruction " + res); }
                        else Check(playback.maxSpeed < before * .65f, "actual inward peak speed reduced >35% " + res + ": " + before.ToString("F2") + " -> " + playback.maxSpeed.ToString("F2") + " m/s");
                    }
                }
                Write("status.txt", (checks.Any(c => c.StartsWith("FAIL")) ? "FINDINGS " : "COMPLETE ") + checks.Count + " checks; native rig motion, not full manual traversal");
            }
            catch (Exception e) { Write("status.txt", "FAILED\n" + e); Debug.LogException(e); }
            finally
            {
                if (playback != null) Object.Destroy(playback.gameObject);
                if (source != null) { source.cameraCollisionEnterTime = oldEnter; source.cameraCollisionLookAhead = oldAhead; source.SetGameplayInputLocked(oldGate); typeof(BasicCameraFollow).GetField("applicationFocused", Any).SetValue(source, oldFocused); }
                if (movement != null) movement.enabled = oldMovement;
                foreach (var entry in enemies) if (entry.ai != null) entry.ai.enabled = entry.enabled;
                if (view != null && oldSize >= 0) size.SetValue(view, oldSize);
                running = false; SessionState.SetBool(Session, false);
                EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Session + "reload", false);
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Session + "options", 0);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }
    }
    [DefaultExecutionOrder(100)]
    internal sealed class CameraSmoothEntryPlayback : MonoBehaviour
    {
        internal BasicCameraFollow source; internal Vector3 position; internal bool done = true, safe; internal string label;
        internal float maxSpeed; internal StringBuilder trace;
        float started, motionStarted, previousDistance; int warmFrames; bool shot;
        static readonly FieldInfo Focused = typeof(BasicCameraFollow).GetField("applicationFocused", BindingFlags.Instance | BindingFlags.NonPublic);
        // The trainer menu intentionally allows passive camera tracking even when
        // gameplay input is blocked. Freeze that source during this pose replay so
        // the brain receives exactly one update, without disabling visibility.
        void Update() { if (source != null && !done) Focused.SetValue(source, false); }
        internal void StartSweep(string name)
        {
            label = name; started = Time.unscaledTime; motionStarted = -1; warmFrames = 0;
            maxSpeed = 0; previousDistance = 5; safe = true; done = false; shot = false;
            trace = new StringBuilder("frame,motionTime,dt,yaw,distance,inwardStep,inwardSpeed\n");
        }
        void LateUpdate()
        {
            if (source == null || done) return;
            // Warm the initial view before replaying input. The first Editor
            // render can spend seconds importing/JITing; it must not skip the
            // entire obstruction crossing in one cold frame.
            if (motionStarted < 0)
            {
                warmFrames = Time.unscaledDeltaTime < .075f ? warmFrames + 1 : 0;
                if (warmFrames >= 24) motionStarted = Time.unscaledTime;
            }
            float t = motionStarted < 0 ? 0 : Time.unscaledTime - motionStarted;
            float yaw = Mathf.Lerp(-100, 100, Mathf.Clamp01((t - .5f) / 2.4f));
            var rig = source.freeLookRig;
            rig.Render(position, yaw, 18, 5, false, Quaternion.identity, source.lockCameraOffset, position + Vector3.up * source.lookHeight);
            float distance = Vector3.Distance(source.transform.position, rig.CollisionOrigin);
            float step = Mathf.Max(0, previousDistance - distance), speed = step / Mathf.Max(.001f, Time.unscaledDeltaTime);
            if (t > .5f) maxSpeed = Mathf.Max(maxSpeed, speed); previousDistance = distance;
            Vector3 raw = rig.freeCamera.State.RawPosition - rig.orbitTarget.position;
            Vector3 requested = rig.orbitTarget.position + raw.normalized * rig.ZoomDistance;
            float clearance = Vector3.Distance(source.ConstrainCameraPosition(rig.CollisionOrigin, requested), rig.CollisionOrigin);
            safe &= distance <= clearance + .002f;
            trace.AppendLine(FormattableString.Invariant($"{Time.frameCount},{t},{Time.unscaledDeltaTime},{yaw},{distance},{step},{speed}"));
            if (!shot && t > 1.65f)
            {
                shot = true; string path = CameraSmoothEntryVerification.Root + label + ".png";
                File.AppendAllText(CameraSmoothEntryVerification.Root + "created-paths.txt", path + "\n"); ScreenCapture.CaptureScreenshot(path);
            }
            done = t > 3.2f;
            if (!done && Time.unscaledTime - started > 20) { safe = false; done = true; }
        }
    }
}
#endif
