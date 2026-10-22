using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.UI;
using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.SettingsSystem.Editor {
    public sealed class SettingsPreviewWindow : EditorWindow {
        internal VisualElement Surface;
        internal SettingsScreenController Controller;
        internal SettingsState Model;
        GameObject owner;
        const string UI = "Assets/ThangTest/UIThang/Inventory/UI/SettingsScreen.uxml";
        [MenuItem("Game Tools/GameToolThang/HUD/Preview Settings (Editor fixture)")]
        public static SettingsPreviewWindow OpenPreview() {
            var window = GetWindow<SettingsPreviewWindow>("Cài đặt — xem thử");
            window.position = new Rect(40, 70, 1366, 768); window.Show(); return window;
        }
        public void CreateGUI() {
            var scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal); scroll.style.flexGrow = 1; rootVisualElement.Add(scroll);
            Surface = new VisualElement(); Surface.style.width = 1920; Surface.style.height = 1080; Surface.style.flexShrink = 0; scroll.Add(Surface);
            AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UI).CloneTree(Surface);
            owner = new GameObject("SettingsEditorFixture") { hideFlags = HideFlags.HideAndDontSave };
            Controller = owner.AddComponent<SettingsScreenController>(); Model = new SettingsState(new GameSettingsData());
            Controller.BindView(Surface);
            Controller.Open(Model, new SettingsCapabilities { Camera = true, Minimap = true, QuestTracker = true, SaveIndicator = true, Display = true, MainMenu = true },
                () => { }, () => "WASD — Di chuyển\nSpace — Nhảy\nE — Tương tác", () => "Không chuyển scene trong bản xem thử.");
        }
        void OnDisable() { if (owner != null) DestroyImmediate(owner); }
    }

    public static class SettingsValidation {
        sealed class MemoryStorage : ISettingsStorage {
            public string Json = ""; public int Writes;
            public string Read() => Json;
            public void Write(string json) { Json = json; Writes++; }
        }
        static readonly List<string> results = new List<string>();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Output = "../artifacts/settings-ui/";
        static bool running;
        static double nextPoll;
        static void Check(bool condition, string label) { if (!condition) throw new InvalidOperationException(label); results.Add("PASS " + label); }
        static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Private).GetValue(value);
        static void SetField(object value, string name, object data) => value.GetType().GetField(name, Private).SetValue(value, data);
        static void Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Private).Invoke(value, args);
        static void Submit(Button button) { using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); } }
        static Toggle Toggle(VisualElement root, string row) => root.Q(row).Q<Toggle>("setting-toggle");
        static Slider Slider(VisualElement root, string row) => root.Q(row).Q<Slider>("setting-slider");
        static Button Button(VisualElement root, string row) => root.Q(row).Q<Button>("setting-button");
        static void Write(string file) { Directory.CreateDirectory(Output); File.WriteAllLines(Output + file, results); }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Settings")]
        public static async void Run() {
            if (running || EditorApplication.isPlaying) return;
            running = true; results.Clear(); SettingsPreviewWindow window = null;
            try {
                var storage = new MemoryStorage(); var repository = new SettingsRepository(storage); var defaults = new GameSettingsData();
                Check(repository.Load(defaults, out _).masterVolume == 1 && storage.Writes == 0, "missing preferences preserve defaults without writing");
                storage.Json = "{\"masterVolume\":0.37}";
                var loaded = repository.Load(defaults, out _);
                Check(Mathf.Approximately(loaded.masterVolume, .37f) && loaded.cameraSmoothing && loaded.showMinimap, "partial JSON preserves missing defaults");
                storage.Json = "{\"version\":99}";
                Check(repository.Load(defaults, out string warning).version == 1 && warning.Length > 0, "unknown preference version falls back safely");
                storage.Json = "broken json";
                Check(repository.Load(defaults, out warning).masterVolume == 1 && warning.Length > 0, "corrupt preferences do not prevent opening screen");
                loaded = new GameSettingsData { masterVolume = 8, mouseSensitivity = -.4f, languageCode = "fake", resolutionWidth = -1, resolutionHeight = 1080 };
                repository.Save(loaded); loaded = repository.Load(defaults, out _);
                Check(loaded.masterVolume == 1 && loaded.mouseSensitivity == 0 && loaded.languageCode == "vi" && loaded.resolutionWidth == 0, "normalization protects ranges and supported language");
                loaded.masterVolume = float.NaN; loaded.mouseSensitivity = float.PositiveInfinity; loaded.Normalize();
                Check(loaded.masterVolume == 1 && loaded.mouseSensitivity == .5f, "non-finite preferences recover");
                loaded.muteAll = true; loaded.masterVolume = .37f; loaded.fullscreen = true; loaded.resolutionWidth = 1600; loaded.resolutionHeight = 900;
                repository.Save(loaded); loaded = repository.Load(defaults, out _);
                Check(loaded.muteAll && Mathf.Approximately(loaded.masterVolume, .37f) && loaded.fullscreen && loaded.resolutionWidth == 1600, "volume, mute and display survive repository reload");
                var available = DisplaySettings.Available(new[] { new Resolution { width = 1920, height = 1080 }, new Resolution { width = 1920, height = 1080 }, new Resolution { width = 1600, height = 900 }, new Resolution { width = 1024, height = 768 } }, new Vector2Int(1920,1080));
                Check(available.Count == 3 && available[0].x == 1920 && available[1].x == 1600, "resolutions deduplicate refresh rates and prefer matching aspect");
                Check(Mathf.Approximately(BasicCameraFollow.PreferenceSensitivity(2.2f,.5f),2.2f) && BasicCameraFollow.PreferenceSensitivity(2.2f,0) > 0, "50 percent preserves Inspector sensitivity and zero stays usable");

                window = SettingsPreviewWindow.OpenPreview(); await Task.Delay(350);
                var root = window.Surface.Q("settings-panel"); var controller = window.Controller; var model = window.Model;
                Check(root.Q<Label>(className: "screen-title").text == "CÀI ĐẶT", "Vietnamese title uses shared header");
                int changes = 0; model.Changed += _ => changes++;
                var master = Slider(root, "settings-master-row"); master.value = 37;
                Check(Mathf.Approximately(model.Data.masterVolume,.37f) && root.Q("settings-master-row").Q<Label>("setting-value").text == "37%", "slider applies instantly and displays percent");
                Toggle(root, "settings-mute-row").value = true;
                Check(model.Data.muteAll && Mathf.Approximately(master.value,37), "mute preserves slider value");
                Toggle(root, "settings-mute-row").value = false;
                Toggle(root, "settings-invert-row").value = true;
                Check(model.Data.invertY, "invert switch reaches model");
                Toggle(root, "settings-fullscreen-row").value = true;
                Check(model.Data.fullscreen && (Screen.resolutions.Length == 0 || model.Data.resolutionWidth > 0), "fullscreen preference includes valid resolution for next launch");
                var field = root.Q("settings-language-row").Q<DropdownField>("setting-dropdown");
                Check(field.choices.Count == 1 && field.value == "Tiếng Việt" && !field.enabledSelf, "only supported language is shown");
                foreach (var size in new[] { new Vector2(1920,1080),new Vector2(1600,900),new Vector2(2560,1440),new Vector2(1366,768),new Vector2(1920,1200),new Vector2(2560,1080) }) {
                    window.Surface.style.width = size.x; window.Surface.style.height = size.y; await Task.Delay(220);
                    var audio = root.Q("settings-audio-group"); var camera = root.Q("settings-camera-group");
                    var gameplay = root.Q("settings-interface-group"); var other = root.Q("settings-other-group");
                    Check(audio.worldBound.width > 0 && audio.worldBound.xMax < camera.worldBound.xMin && audio.worldBound.yMax < gameplay.worldBound.yMin, "two columns and two rows " + size);
                    Check(other.worldBound.yMax <= root.worldBound.yMax + 1 && camera.worldBound.xMax <= root.worldBound.xMax + 1, "four cards fit viewport " + size);
                    Check(root.Q<Button>("settings-back").worldBound.xMax <= root.worldBound.xMax + 1 && root.Q<Label>(className: "screen-title").resolvedStyle.fontSize == 38, "shared title and Back geometry " + size);
                    Check(master.worldBound.xMax <= root.Q("settings-master-row").Q<Label>("setting-value").worldBound.xMin + 1, "slider and percent do not overlap " + size);
                    Check(Toggle(root,"settings-invert-row").Q("settings-switch-thumb").resolvedStyle.marginLeft > 20 && Toggle(root,"settings-mute-row").Q("settings-switch-thumb").resolvedStyle.marginLeft < 10, "pill switches represent actual state " + size);
                    Check(Toggle(root,"settings-invert-row").Q(className:"unity-toggle__input").resolvedStyle.width >= 63 && Toggle(root,"settings-mute-row").Q(className:"unity-toggle__input").resolvedStyle.width >= 63, "pill background never collapses to native checkbox width " + size);
                    if (size.y >= 1080) Check(Button(root,"settings-quit-row").worldBound.yMax <= other.Q<ScrollView>().contentViewport.worldBound.yMax+1, "exit action visible without scrolling " + size);
                }
                Submit(Button(root,"settings-reset-row")); await Task.Delay(180);
                Check(root.Q("settings-modal").resolvedStyle.display == DisplayStyle.Flex, "reset requires confirmation");
                root.Q<Button>("settings-modal-confirm").Focus();
                using (var navigation = NavigationMoveEvent.GetPooled(NavigationMoveEvent.Direction.Next)) {
                    navigation.target = root.Q<Button>("settings-modal-confirm"); root.Q<Button>("settings-modal-confirm").SendEvent(navigation);
                }
                Check(root.Q("settings-modal").Contains(root.focusController.focusedElement as VisualElement), "Tab stays inside modal rather than background controls");
                Check(controller.TryCloseModal() && Mathf.Approximately(model.Data.masterVolume,.37f), "Esc cancels reset without changing settings");
                await Task.Delay(180); Submit(Button(root,"settings-reset-row")); await Task.Delay(180); Submit(root.Q<Button>("settings-modal-confirm")); await Task.Delay(180);
                Check(model.Data.masterVolume == 1 && !model.Data.muteAll && model.Data.cameraSmoothing && !model.Data.showQuestTracker, "confirmed reset restores project defaults");
                Submit(Button(root,"settings-guide-row")); await Task.Delay(180);
                Check(root.Q<Label>("settings-modal-text").text.Contains("WASD") && root.Q<Button>("settings-modal-confirm").resolvedStyle.display == DisplayStyle.None, "controls guide is read-only");
                controller.TryCloseModal(); await Task.Delay(180);
                Submit(Button(root,"settings-quit-row")); await Task.Delay(180); Submit(root.Q<Button>("settings-modal-confirm")); await Task.Delay(180);
                Check(root.Q<Label>("settings-modal-error").text.Contains("Không chuyển scene"), "failed scene exit stays on confirmation with explanation"); controller.TryCloseModal();
                int callbacks = Field<IList>(controller,"unbind").Count;
                for (int i=0;i<3;i++) {
                    controller.Close(); controller.BindView(window.Surface);
                    controller.Open(model,new SettingsCapabilities { Camera=true,Minimap=true,QuestTracker=true,SaveIndicator=true,Display=true,MainMenu=true },()=>{},()=>"Hướng dẫn",()=>"");
                    int before = changes; master = Slider(root,"settings-master-row"); master.value = 20+i;
                    Check(changes == before+1 && Field<IList>(controller,"unbind").Count == callbacks, "rebind has exactly one callback " + i);
                }
                controller.Close(); controller.Open(model,new SettingsCapabilities(),()=>{},()=>"",()=>""); await Task.Delay(180);
                Check(root.Q("settings-mouse-row").resolvedStyle.display == DisplayStyle.None && root.Q("settings-minimap-row").resolvedStyle.display == DisplayStyle.None && !root.Q("settings-fullscreen-row").enabledSelf, "unsupported controls hide and Editor display disables");
                int exits=0;
                controller.Close(); controller.Open(model,new SettingsCapabilities { MainMenu=true },()=>{},()=>"",()=> { exits++; return ""; });
                await Task.Delay(180); Submit(Button(root,"settings-quit-row")); await Task.Delay(180);
                Submit(root.Q<Button>("settings-modal-confirm")); Invoke(controller,"ConfirmModal");
                Check(exits==1 && controller.TryCloseModal() && !root.Q<Button>("settings-modal-confirm").enabledSelf, "confirmed exit cannot run twice or close while loading");
                Write("validation.txt"); Debug.Log("Settings validation: " + results.Count + " checks passed.");
            } catch (Exception e) { results.Add("FAIL " + e); Write("validation.txt"); Debug.LogException(e); }
            finally { window?.Close(); running=false; }
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Settings in Play Mode")]
        public static async void RunPlay() {
            if (running || !Application.isPlaying) return;
            running=true; results.Clear();
            var service=GameSettingsService.Instance; var inventory=Object.FindFirstObjectByType<MonsterInventoryController>(); var menu=Object.FindFirstObjectByType<GameMenuController>();
            SettingsRepository originalRepository=null; string snapshot=null;
            try {
                Check(service != null && inventory != null && menu != null, "actual TestChar components exist");
                service.Flush(); originalRepository=Field<SettingsRepository>(service,"repository"); snapshot=JsonUtility.ToJson(service.Model.Data);
                var memory=new MemoryStorage(); SetField(service,"repository",new SettingsRepository(memory));
                menu.CloseMenu(); inventory.Close(); await Task.Delay(220); menu.OpenMenu(); await Task.Delay(220); menu.OpenSettingsDirect(); await Task.Delay(300);
                var root=inventory.GetComponent<UIDocument>().rootVisualElement; var controller=inventory.GetComponent<SettingsScreenController>();
                Check(inventory.IsSettingsOpen && controller.IsOpen && InventoryInputController.GameplayInputBlocked && !menu.IsMenuOpen, "Trainer Settings opens fullscreen and locks gameplay");
                Check(Toggle(root,"settings-invert-row").Q(className:"unity-toggle__input").resolvedStyle.width >= 63, "actual composed UI keeps pill background width");
                var master=Slider(root,"settings-master-row"); master.value=42; master.value=43; master.value=44;
                Check(Mathf.Approximately(AudioListener.volume,.44f), "actual AudioListener updates immediately");
                await Task.Delay(700); Check(memory.Writes==1, "rapid slider changes persist once after debounce");
                master.value=0; Check(AudioListener.volume==0, "zero master volume silences listener");
                master.value=100; Check(AudioListener.volume==1, "maximum master volume restores listener"); master.value=44;
                Toggle(root,"settings-mute-row").value=true;
                Check(AudioListener.volume==0 && Mathf.Approximately(master.value,44), "actual mute keeps volume");
                Toggle(root,"settings-mute-row").value=false; Check(Mathf.Approximately(AudioListener.volume,.44f), "unmute restores previous volume");
                var camera=Object.FindFirstObjectByType<BasicCameraFollow>();
                float vertical=camera.ScaleVerticalInput(1); Toggle(root,"settings-invert-row").value=!service.Model.Data.invertY;
                Check(Mathf.Approximately(vertical,-camera.ScaleVerticalInput(1)), "actual vertical input reverses without changing sensitivity");
                Toggle(root,"settings-smoothing-row").value=false;
                Check(!camera.UserSmoothingEnabled, "camera smoothing changes existing controller");
                Check(camera.freeLookRig.orbit.TrackerSettings.PositionDamping==Vector3.zero && camera.freeLookRig.lockFollow.TrackerSettings.PositionDamping==Vector3.zero, "both Cinemachine cameras remove follow damping");
                Toggle(root,"settings-smoothing-row").value=true;
                Check(camera.freeLookRig.orbit.TrackerSettings.PositionDamping==Field<Vector3>(camera.freeLookRig,"orbitDamping"), "smoothing restores scene-authored damping");
                Toggle(root,"settings-minimap-row").value=false;
                var hud=Object.FindFirstObjectByType<GameplayHudController>();
                Check(service.HasMinimap && !Field<RectTransform>(hud,"minimapPanel").gameObject.activeSelf, "minimap toggle hides its presentation");
                Toggle(root,"settings-minimap-row").value=true;
                Check(Field<RectTransform>(hud,"minimapPanel").gameObject.activeSelf, "minimap reappears without recreation");
                Toggle(root,"settings-prompts-row").value=false; Check(!GameSettingsService.InteractionPromptsVisible, "interaction prompt preference reaches shared presentation gate");
                var quests=Object.FindFirstObjectByType<QuestManager>(); var tracked=quests.GetTrackedQuest();
                Toggle(root,"settings-quest-row").value=true;
                Check(Object.FindFirstObjectByType<QuestTrackerHudController>()!=null && quests.GetTrackedQuest()==tracked, "show tracker does not change tracked quest");
                Toggle(root,"settings-quest-row").value=false;
                Check(Object.FindFirstObjectByType<QuestTrackerHudController>()==null && quests.GetTrackedQuest()==tracked, "hide tracker keeps quest tracking active");
                Toggle(root,"settings-save-row").value=true; Invoke(service,"Saved",new PlayerSaveData()); await Task.Delay(100);
                Check(root.Q<Label>("game-save-indicator").resolvedStyle.display==DisplayStyle.Flex, "save event displays feedback outside hidden inventory root");
                Toggle(root,"settings-save-row").value=false;
                Check(root.Q<Label>("game-save-indicator").style.display==DisplayStyle.None, "save indicator can hide without disabling saves");
                Check(!root.Q("settings-fullscreen-row").enabledSelf && service.CanReturnToMainMenu, "Editor display controls explain limitation and real MainMenu is available");
                Check(menu.GetControlsGuide().Contains("Đổi nhân vật") && menu.GetControlsGuide().Contains("Ra lệnh thú"), "guide uses actual visual and pet controls");
                Submit(Button(root,"settings-guide-row")); await Task.Delay(180);
                Check(inventory.TryCloseTopmostPanel() && inventory.IsSettingsOpen, "Esc closes guide before Settings");
                await Task.Delay(180); Submit(root.Q<Button>("settings-back")); await Task.Delay(220);
                Check(!inventory.IsOpen && menu.IsMenuOpen, "Back returns to originating Trainer Menu");
                menu.CloseMenu(); await Task.Delay(220); menu.OpenSettingsDirect(); await Task.Delay(220); inventory.RequestClose(); await Task.Delay(220);
                Check(!inventory.IsOpen && !menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked, "direct Settings closes back to gameplay");
                service.Model.Change(SettingsArea.All,d=>JsonUtility.FromJsonOverwrite(snapshot,d)); service.Flush();
                SetField(service,"repository",originalRepository); originalRepository=null;
                menu.OpenSettingsDirect(); await Task.Delay(300);
                if (Screen.height >= 1080) Check(Button(root,"settings-quit-row").worldBound.yMax <= root.Q("settings-other-group").Q<ScrollView>().contentViewport.worldBound.yMax+1, "actual baseline exit action stays fully visible");
                Directory.CreateDirectory(Output+"screenshots"); ScreenCapture.CaptureScreenshot(Output+"screenshots/Settings-actual.png"); await Task.Delay(350);
                Submit(Button(root,"settings-guide-row")); await Task.Delay(220);
                ScreenCapture.CaptureScreenshot(Output+"screenshots/Settings-controls.png"); await Task.Delay(300); controller.TryCloseModal();
                Write("runtime-validation.txt"); Debug.Log("Settings runtime: "+results.Count+" checks passed.");
            } catch(Exception e) { results.Add("FAIL "+e); Write("runtime-validation.txt"); Debug.LogException(e); }
            finally {
                if (service!=null && originalRepository!=null) { service.Model.Change(SettingsArea.All,d=>JsonUtility.FromJsonOverwrite(snapshot,d)); service.Flush(); SetField(service,"repository",originalRepository); }
                inventory?.Close(); running=false;
            }
        }
        [InitializeOnLoadMethod] static void Resume() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static bool Ready(string path) => File.Exists(path) && (DateTime.UtcNow-File.GetLastWriteTimeUtc(path)).TotalSeconds>1;
        static void Poll() {
            if(running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<nextPoll) return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            const string edit="Temp/RunSettings.request",play="Temp/RunSettingsPlay.request";
            if(Ready(edit) && !EditorApplication.isPlaying) { File.Delete(edit); Run(); }
            if(Ready(play) && EditorApplication.isPlaying) { File.Delete(play); RunPlay(); }
        }
    }
}
