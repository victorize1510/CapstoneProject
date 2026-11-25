using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.CaptureSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.PetSummon;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.SettingsSystem.Editor
{
    static class CaptureInputSettingsVerification
    {
        const string Session = "Capstone.CaptureInputSettings.Isolated";
        static string Report => Path.Combine(QaArtifactPaths.Root, "CaptureInputSettings", "playmode.txt");

        [MenuItem("Tools/Capstone/Audio/Verify Capture, Input and Settings (isolated)")]
        static void Start()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1
                || scene.isDirty || string.IsNullOrEmpty(scene.path) || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Mở một scene đã lưu trong Edit Mode trước khi chạy.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Entering isolated Play Mode.");
            SessionState.SetString(Session, scene.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        static void Install() => EditorApplication.playModeStateChanged += StateChanged;

        static void StateChanged(PlayModeStateChange state)
        {
            string scene = SessionState.GetString(Session, "");
            if (string.IsNullOrEmpty(scene)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) Run();
            if (state != PlayModeStateChange.EnteredEditMode) return;
            SessionState.EraseString(Session);
            EditorApplication.delayCall += () => EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
        }

        static async void Run()
        {
            var report = new List<string>();
            var controls = BindingFlags.Static | BindingFlags.NonPublic;
            var settings = GameSettingsService.Instance.Model.Data.Copy();
            var applyAudio = typeof(GameAudioRouting).GetMethod("Apply", controls);
            Keyboard keyboard = null;
            PanelSettings panel = null;
            VisualElement root = null;
            void Check(bool pass, string message)
            {
                report.Add((pass ? "PASS " : "FAIL ") + message);
                File.WriteAllLines(Report, report);
            }

            try
            {
                new GameObject("QA listener").AddComponent<AudioListener>();
                keyboard = InputSystem.AddDevice<Keyboard>();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.A, Key.Q, Key.E, Key.G, Key.P, Key.Digit1));
                await Task.Delay(120);
                Check(GameplayKeyboard.Movement == new Vector2(-1, 1) && GameplayKeyboard.GetKey(KeyCode.Q)
                    && GameplayKeyboard.GetKey(KeyCode.E) && GameplayKeyboard.GetKey(KeyCode.G)
                    && GameplayKeyboard.GetKey(KeyCode.P) && GameplayKeyboard.GetKey(KeyCode.Alpha1),
                    "Input System physical keys drive movement and gameplay shortcuts");

                panel = ScriptableObject.CreateInstance<PanelSettings>();
                var doc = new GameObject("QA input document").AddComponent<UIDocument>();
                doc.panelSettings = panel;
                root = doc.rootVisualElement;
                typeof(GameplayKeyboard).GetMethod("Track", controls).Invoke(null, new object[] { root });
                var entry = new VisualElement();
                var field = new TextField { value = "Tiếng Việt có dấu" };
                root.Add(entry);
                entry.Add(field);
                PanelFocusUtility.Open(entry);
                field.Focus();
                await Task.Delay(100);
                Check(GameplayKeyboard.IsEditingText && InventoryInputController.GameplayInputBlocked
                    && !GameplayKeyboard.GetKey(KeyCode.W) && !GameplayKeyboard.GetKey(KeyCode.G)
                    && field.value == "Tiếng Việt có dấu", "A focused text field retains Unicode and gates movement/capture");
                entry.style.display = DisplayStyle.None;
                PanelFocusUtility.Close(entry);
                await Task.Delay(100);
                Check(!GameplayKeyboard.IsEditingText && !GameplayKeyboard.GetKey(KeyCode.W),
                    "Closing the text panel returns focus and held keys wait for physical release");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                await WaitFor(() => !keyboard.wKey.isPressed);
                await Task.Delay(120);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                await WaitFor(() => keyboard.wKey.isPressed);
                keyboard.MakeCurrent();
                bool resumed = await WaitFor(() => GameplayKeyboard.GetKey(KeyCode.W));
                Check(resumed, "Movement resumes on a fresh key press after text focus"
                    + $" (focused={Application.isFocused}, edit={GameplayKeyboard.IsEditingText}, key={keyboard.wKey.isPressed})");
                InputSystem.RemoveDevice(keyboard);
                keyboard = null;

                var mixer = Resources.Load<GameAudioAssets>("CapstoneAudio");
                Check(mixer != null && mixer.mixer != null && mixer.menuMusic != null && mixer.gameplayMusic != null
                    && mixer.evolutionMusic != null && mixer.click != null && mixer.hover != null,
                    "Mixer and authored music/UI clips load through Resources");
                var sources = Enum.GetValues(typeof(GameAudioBus)).Cast<GameAudioBus>().ToDictionary(bus => bus, bus =>
                {
                    var source = new GameObject("QA " + bus).AddComponent<AudioSource>();
                    GameAudioRouting.Route(source, bus);
                    return source;
                });
                Check(sources.All(entry => entry.Value.outputAudioMixerGroup != null), "Six source buses resolve to mixer groups");
                var local = new GameSettingsData { masterVolume = .7f, musicVolume = .5f, skillSfxVolume = 0, petCryVolume = .35f };
                applyAudio.Invoke(null, new object[] { local });
                Check(Mathf.Approximately(AudioListener.volume, .7f)
                    && mixer.mixer.GetFloat("MusicVolume", out float db) && Mathf.Abs(db + 6.0206f) < .01f,
                    "Master has one global gain and music slider sets exposed dB");
                Check(sources[GameAudioBus.Skill].mute && !sources[GameAudioBus.PetCry].mute,
                    "Skill zero is silent while Pet Cry remains independent");
                local.sfxVolume = 0;
                applyAudio.Invoke(null, new object[] { local });
                Check(sources[GameAudioBus.Skill].mute && sources[GameAudioBus.PetCry].mute && !sources[GameAudioBus.UI].mute,
                    "Parent SFX zero silences Skill and Pet Cry, with UI independent");
                local.muteAll = true;
                applyAudio.Invoke(null, new object[] { local });
                Check(AudioListener.volume == 0 && local.musicVolume == .5f, "Mute All preserves the saved individual slider values");
                Check(PetVFXProfile.ParticleBudget(PetVFXQuality.Low) > 0
                    && PetVFXProfile.ParticleBudget(PetVFXQuality.Low) < PetVFXProfile.ParticleBudget(PetVFXQuality.Medium)
                    && PetVFXProfile.ParticleBudget(PetVFXQuality.Medium) < PetVFXProfile.ParticleBudget(PetVFXQuality.High),
                    "The existing three VFX budgets retain the core effect at Low");

                var gesture = new HubPressGesture();
                gesture.Step(true, true, false, 0, .18f);
                Check(gesture.Step(false, false, true, .1f, .18f) == HubPressGesture.Intent.Tap, "Short M3 is a pin tap");
                gesture.Step(true, true, false, 1, .18f);
                Check(gesture.Step(false, true, false, 1.22f, .18f) == HubPressGesture.Intent.Hold
                    && gesture.Step(false, false, true, 1.3f, .18f) == HubPressGesture.Intent.ReleaseHold,
                    "Holding/releasing M3 emits Aim open/close without a pin tap");

                var library = AssetDatabase.LoadAssetAtPath<CreatureLibrary>("Assets/Game/CreatureSystem/Data/CreatureLibrary.asset");
                var bats = library.families.First(family => family.definitionId == "bat");
                var player = new GameObject("QA capture player").AddComponent<PetCommandInput>();
                var view = new GameObject("QA capture camera").AddComponent<Camera>();
                view.transform.position = new Vector3(0, 1.4f, -4);
                player.commandCamera = view;
                var first = Object.Instantiate(bats.forms[0].wildPrefab, new Vector3(0, 0, 6), Quaternion.identity).GetComponent<DummyEnemy>();
                var second = Object.Instantiate(bats.forms[1].wildPrefab, new Vector3(1.75f, 0, 7), Quaternion.identity).GetComponent<DummyEnemy>();
                if (first.TryGetComponent<BasicCreatureEnemy>(out var firstBrain)) firstBrain.enabled = false;
                if (second.TryGetComponent<BasicCreatureEnemy>(out var secondBrain)) secondBrain.enabled = false;
                view.transform.LookAt(first.TargetPosition);
                Physics.SyncTransforms();
                await Task.Delay(100);
                Check(WildCaptureTargeting.Preview(player, null, false, null).Enemy == first,
                    "Normal preview chooses the visible centered Wild Pet");
                player.SendMessage("SelectPinnedEnemy", second);
                Check(WildCaptureTargeting.Preview(player, null, false, first).Enemy == second
                    && WildCaptureTargeting.Preview(player, null, true, second).Enemy == first,
                    "Normal preserves the explicit target while Aim uses the reticle target");
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.transform.position = Vector3.Lerp(player.transform.position + Vector3.up, first.TargetPosition, .5f);
                blocker.transform.localScale = new Vector3(.8f, 2, .4f);
                Physics.SyncTransforms();
                var check = typeof(WildCaptureTargeting).GetMethod("Check", controls);
                WildCaptureTargeting.Selection Recheck(DummyEnemy target) =>
                    (WildCaptureTargeting.Selection)check.Invoke(null, new object[] { player, view, target, true });
                Check(Recheck(first).Failure == CaptureFailure.Obstructed,
                    "Revalidating a blocked preview rejects that individual instead of changing targets");
                blocker.SetActive(false);
                first.transform.position = new Vector3(0, 0, 50);
                view.transform.LookAt(first.TargetPosition);
                Physics.SyncTransforms();
                Check(Recheck(first).Failure == CaptureFailure.OutOfRange, "A visible Wild Pet outside ball range is rejected");

                first.transform.position = new Vector3(0, 0, 6);
                view.transform.LookAt(first.TargetPosition);
                first.TakeDamage(first.maxHealth * 100);
                Physics.SyncTransforms();
                Check(first.IsDownedAvailable && Recheck(first).Failure == CaptureFailure.None,
                    "An eligible downed individual preserves the existing real-time capture window");
                report.Insert(0, report.Any(line => line.StartsWith("FAIL ", StringComparison.Ordinal))
                    ? "FAILED isolated Play Mode." : "COMPLETE isolated Play Mode. No native Telex input was injected.");
            }
            catch (Exception error)
            {
                report.Insert(0, "FAILED isolated Play Mode.");
                report.Add(error.ToString());
                Debug.LogException(error);
            }
            finally
            {
                if (root != null) typeof(GameplayKeyboard).GetMethod("Untrack", controls).Invoke(null, new object[] { root });
                if (panel != null) Object.Destroy(panel);
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                applyAudio.Invoke(null, new object[] { settings });
                File.WriteAllLines(Report, report);
                if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            }
        }

        static async Task<bool> WaitFor(Func<bool> condition)
        {
            double until = EditorApplication.timeSinceStartup + 2;
            while (EditorApplication.isPlaying && EditorApplication.timeSinceStartup < until && !condition())
                await Task.Delay(25);
            return condition();
        }
    }
}
