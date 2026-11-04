using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.SettingsSystem {
    public struct SettingsCapabilities {
        public bool Camera, Minimap, QuestTracker, SaveIndicator, Display, MainMenu;
    }

    [DisallowMultipleComponent]
    public sealed class SettingsScreenController : MonoBehaviour {
        sealed class SliderView { public Slider Slider; public Label Value; public SettingsSliderFace Face; public Func<GameSettingsData, float> Read; }
        sealed class ToggleView { public Toggle Toggle; public Label Value; public Func<GameSettingsData, bool> Read; }
        readonly List<SliderView> sliders = new List<SliderView>();
        readonly List<ToggleView> toggles = new List<ToggleView>();
        readonly List<Action> unbind = new List<Action>();
        VisualElement root, modal;
        Label modalTitle, modalText, modalError;
        Button confirm, cancel;
        DropdownField resolution, language;
        SettingsState model;
        GameSettingsService service;
        Action flush;
        Func<string> controlsGuide;
        Func<string> quit;
        readonly List<Vector2Int> sizes = new List<Vector2Int>();
        bool open, subscribed;
        SettingsCapabilities capabilities;
        bool leavingScene;
        SettingsFinalView presentation;
        enum Modal { None, Guide, Reset, Quit }
        Modal currentModal;
        public bool IsOpen => open;
        public event Action BackRequested;

        public void Bind(UIDocument document) => BindView(document?.rootVisualElement);
        public void BindView(VisualElement documentRoot) {
            Unsubscribe(); CloseModal(); PanelFocusUtility.Close(root); Unbind();
            root = documentRoot?.Q("settings-panel");
            if (root == null) return;
            UiReadability.Bind(documentRoot);
            presentation=SettingsFinalView.Attach(root);
            modal = root.Q("settings-modal"); modalTitle = root.Q<Label>("settings-modal-title");
            modalText = root.Q<Label>("settings-modal-text"); modalError = root.Q<Label>("settings-modal-error");
            confirm = root.Q<Button>("settings-modal-confirm"); cancel = root.Q<Button>("settings-modal-cancel");
            BindSlider("settings-master-row", "Tổng âm lượng", SettingsArea.Audio, d => d.masterVolume, (d, v) => d.masterVolume = v);
            BindSlider("settings-mouse-row", "Độ nhạy chuột", SettingsArea.Camera, d => d.mouseSensitivity, (d, v) => d.mouseSensitivity = v);
            BindToggle("settings-mute-row", "Tắt tất cả âm thanh", SettingsArea.Audio, d => d.muteAll, (d, v) => d.muteAll = v);
            BindToggle("settings-invert-row", "Đảo ngược trục Y (camera)", SettingsArea.Camera, d => d.invertY, (d, v) => d.invertY = v);
            BindToggle("settings-smoothing-row", "Làm mượt chuyển động camera", SettingsArea.Camera, d => d.cameraSmoothing, (d, v) => d.cameraSmoothing = v);
            BindToggle("settings-prompts-row", "Hiển thị gợi ý tương tác", SettingsArea.Interface, d => d.showInteractionPrompts, (d, v) => d.showInteractionPrompts = v);
            BindToggle("settings-minimap-row", "Hiển thị bản đồ mini", SettingsArea.Interface, d => d.showMinimap, (d, v) => d.showMinimap = v);
            BindToggle("settings-quest-row", "Hiển thị nhiệm vụ đang theo dõi", SettingsArea.Interface, d => d.showQuestTracker, (d, v) => d.showQuestTracker = v);
            BindToggle("settings-save-row", "Hiển thị thông báo lưu game", SettingsArea.Interface, d => d.showSaveIndicator, (d, v) => d.showSaveIndicator = v);
            BindToggle("settings-fullscreen-row", "Chế độ toàn màn hình", SettingsArea.Display, d => d.fullscreen, (d, v) => {
                d.fullscreen = v;
                if (d.resolutionWidth > 0 || sizes.Count == 0) return;
                int index = Mathf.Clamp(resolution.index, 0, sizes.Count - 1);
                d.resolutionWidth = sizes[index].x; d.resolutionHeight = sizes[index].y;
            });
            language = Dropdown("settings-language-row", "Ngôn ngữ");
            language.choices = new List<string> { "Tiếng Việt" }; language.SetValueWithoutNotify("Tiếng Việt"); language.SetEnabled(false);
            language.tooltip = "Trò chơi hiện hỗ trợ tiếng Việt.";
            resolution = Dropdown("settings-resolution-row", "Độ phân giải");
            EventCallback<ChangeEvent<string>> changeResolution = _ => {
                int index = resolution.index;
                if (model == null || index < 0 || index >= sizes.Count) return;
                model.Change(SettingsArea.Display, d => { d.resolutionWidth = sizes[index].x; d.resolutionHeight = sizes[index].y; });
            };
            resolution.RegisterValueChangedCallback(changeResolution); unbind.Add(() => resolution.UnregisterValueChangedCallback(changeResolution));
            BindButton("settings-guide-row", "Hướng dẫn phím điều khiển", "Hướng dẫn phím", () => OpenModal(Modal.Guide));
            BindButton("settings-reset-row", "Đặt lại cài đặt mặc định", "Đặt lại mặc định", () => OpenModal(Modal.Reset));
            BindButton("settings-quit-row", "Thoát về màn hình chính", "Về màn hình chính", () => OpenModal(Modal.Quit));
            Click(root.Q<Button>("settings-back"), () => BackRequested?.Invoke());
            Click(cancel, CloseModal); Click(confirm, ConfirmModal);
            Click(root.Q<Button>("settings-modal-close"),()=>{if(!leavingScene)CloseModal();});
            BindSubmitKeys();
            if (model != null) { SetCapabilities(capabilities); Refresh(); }
            Show(root, open); if (open) { Subscribe(); PanelFocusUtility.Open(root); }
        }
        public void Open(GameSettingsService settings, Func<string> guide) {
            service = settings;
            if (service == null || root == null) { Debug.LogError("Settings screen is missing its service or document.", this); return; }
            service.ResolveScene();
            var caps = new SettingsCapabilities { Camera = service.HasCamera, Minimap = service.HasMinimap,
                QuestTracker = service.HasQuestTracker, SaveIndicator = service.HasSaveIndicator,
                Display = !Application.isEditor, MainMenu = service.CanReturnToMainMenu };
            Open(service.Model, caps, service.Flush, guide, () => service.TryReturnToMainMenu(out string error) ? "" : error);
        }
        // Editor and tests supply an isolated model; no production preferences are touched.
        public void Open(SettingsState state, SettingsCapabilities caps, Action save, Func<string> guide, Func<string> returnToMenu) {
            Unsubscribe(); model = state; flush = save; controlsGuide = guide; quit = returnToMenu; open = true; leavingScene = false;
            Show(root, true); SetCapabilities(caps); Subscribe(); Refresh(); PanelFocusUtility.Open(root);
        }
        void SetCapabilities(SettingsCapabilities caps) {
            capabilities = caps;
            foreach (string name in new[] { "settings-mouse-row", "settings-invert-row", "settings-smoothing-row" }) Show(root.Q(name), caps.Camera);
            Show(root.Q("settings-camera-unavailable"), !caps.Camera);
            Show(root.Q("settings-minimap-row"), caps.Minimap); Show(root.Q("settings-quest-row"), caps.QuestTracker);
            Show(root.Q("settings-save-row"), caps.SaveIndicator); Show(root.Q("settings-quit-row"), caps.MainMenu);
            root.Q("settings-fullscreen-row").SetEnabled(caps.Display);
            sizes.Clear(); sizes.AddRange(DisplaySettings.Available(Screen.resolutions, new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height)));
            var choices = new List<string>(); foreach (var size in sizes) choices.Add(size.x + " × " + size.y);
            if (choices.Count == 0) choices.Add("Chưa có độ phân giải khả dụng"); resolution.choices = choices;
            resolution.SetEnabled(caps.Display && sizes.Count > 0);
            string displayHelp = caps.Display ? "Áp dụng tức thời cho cửa sổ trò chơi." : "Thiết lập cửa sổ khả dụng trong bản game PC.";
            root.Q("settings-fullscreen-row").tooltip = displayHelp; resolution.tooltip = displayHelp;
            root.Q<Label>("settings-display-note").text = caps.Display ? "" : displayHelp;
            presentation?.Refresh();
        }
        public void Close() {
            bool wasOpen = open; open = false; CloseModal(); Unsubscribe(); Show(root, false); PanelFocusUtility.Close(root);
            if (wasOpen) flush?.Invoke();
        }
        public bool TryCloseModal() { if (leavingScene) return true; if (currentModal == Modal.None) return false; CloseModal(); return true; }
        void OnDisable() => Close();
        void OnDestroy() { Close(); Unbind(); }
        void Subscribe() { if (model != null && !subscribed && open) { model.Changed += Changed; subscribed = true; } }
        void Unsubscribe() { if (model != null && subscribed) model.Changed -= Changed; subscribed = false; }
        void Changed(SettingsArea _) { if (open) Refresh(); }
        public void Refresh() {
            if (model == null || root == null) return;
            foreach (var view in sliders) {
                float value = view.Read(model.Data); view.Slider.SetValueWithoutNotify(value * 100);
                view.Value.text = Mathf.RoundToInt(value * 100) + "%"; view.Face?.Refresh();
            }
            foreach (var view in toggles) {
                bool value = view.Read(model.Data); view.Toggle.SetValueWithoutNotify(value); view.Value.text = value ? "Bật" : "Tắt";
                view.Value.EnableInClassList("is-on", value);
            }
            var wanted = new Vector2Int(model.Data.resolutionWidth, model.Data.resolutionHeight);
            int selected = sizes.IndexOf(wanted);
            if (selected < 0) selected = sizes.IndexOf(new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height));
            resolution.SetValueWithoutNotify(resolution.choices[Mathf.Clamp(selected, 0, resolution.choices.Count - 1)]);
            presentation?.Refresh();
        }
        void BindSlider(string name, string label, SettingsArea area, Func<GameSettingsData, float> read, Action<GameSettingsData, float> write) {
            var row = root.Q(name); row.Q<Label>("setting-label").text = label;
            var slider = row.Q<Slider>("setting-slider");
            sliders.Add(new SliderView { Slider = slider, Value = row.Q<Label>("setting-value"), Face = slider.Q<SettingsSliderFace>(), Read = read });
            EventCallback<ChangeEvent<float>> callback = evt => model?.Change(area, d => write(d, evt.newValue / 100));
            slider.RegisterValueChangedCallback(callback); unbind.Add(() => slider.UnregisterValueChangedCallback(callback));
            // This project's legacy gameplay input can suspend the UI move action.
            // Forward focused arrow keys through Slider's native navigation path,
            // and consume a second move from the input module in the same frame.
            int keyFrame = -1, moveFrame = -1;
            NavigationMoveEvent forwardedMove = null;
            EventCallback<NavigationMoveEvent> move = evt => {
                if (!(evt.target is VisualElement target) || target != slider && !slider.Contains(target)) return;
                if (evt.direction != NavigationMoveEvent.Direction.Left && evt.direction != NavigationMoveEvent.Direction.Right) return;
                if (ReferenceEquals(evt, forwardedMove)) forwardedMove = null;
                else if (keyFrame == Time.frameCount) { evt.StopImmediatePropagation(); slider.focusController?.IgnoreEvent(evt); return; }
                moveFrame = Time.frameCount;
            };
            EventCallback<KeyDownEvent> key = evt => {
                if (!(evt.target is VisualElement target) || target != slider && !slider.Contains(target)) return;
                if (!open || !slider.enabledInHierarchy || evt.altKey || evt.ctrlKey || evt.commandKey) return;
                if (evt.keyCode != KeyCode.LeftArrow && evt.keyCode != KeyCode.RightArrow) return;
                if (moveFrame != Time.frameCount) {
                    keyFrame = Time.frameCount;
                    // Nested Toolkit dispatch may be queued until this key event
                    // finishes. Identify that event rather than a temporary flag.
                    using (var navigation = NavigationMoveEvent.GetPooled(evt.keyCode == KeyCode.LeftArrow ? NavigationMoveEvent.Direction.Left : NavigationMoveEvent.Direction.Right)) { forwardedMove = navigation; navigation.target = slider; slider.SendEvent(navigation); }
                }
                evt.StopPropagation(); slider.focusController?.IgnoreEvent(evt);
            };
            // Capture above BaseSlider's internal key handler, which consumes
            // legacy arrow events before user callbacks on the slider itself.
            var inputRoot = root;
            inputRoot.RegisterCallback(move, TrickleDown.TrickleDown); inputRoot.RegisterCallback(key, TrickleDown.TrickleDown);
            unbind.Add(() => { inputRoot.UnregisterCallback(move, TrickleDown.TrickleDown); inputRoot.UnregisterCallback(key, TrickleDown.TrickleDown); });
        }
        void BindToggle(string name, string label, SettingsArea area, Func<GameSettingsData, bool> read, Action<GameSettingsData, bool> write) {
            var row = root.Q(name); row.Q<Label>("setting-label").text = label;
            var toggle = row.Q<Toggle>("setting-toggle");
            toggles.Add(new ToggleView { Toggle = toggle, Value = row.Q<Label>("setting-value"), Read = read });
            EventCallback<ChangeEvent<bool>> callback = evt => model?.Change(area, d => write(d, evt.newValue));
            toggle.RegisterValueChangedCallback(callback); unbind.Add(() => toggle.UnregisterValueChangedCallback(callback));
        }
        DropdownField Dropdown(string name, string label) {
            var row = root.Q(name); row.Q<Label>("setting-label").text = label;
            var field = row.Q<DropdownField>("setting-dropdown"); VerticalDropdownPopup.Bind(field);
            unbind.Add(() => VerticalDropdownPopup.Unbind(field)); return field;
        }
        void BindButton(string name, string label, string text, Action callback) {
            var row = root.Q(name); row.Q<Label>("setting-label").text = label;
            var button = row.Q<Button>("setting-button"); button.text = text;
            Action guarded = PlayerActionGate.Guard(this, name, callback); button.clicked += guarded; unbind.Add(() => button.clicked -= guarded);
        }
        void Click(Button button, Action callback) {
            Action guarded = PlayerActionGate.Guard(this, "settings:" + button.name, callback);
            button.clicked += guarded; unbind.Add(() => button.clicked -= guarded);
        }
        void BindSubmitKeys() {
            var inputRoot = root;
            int keyFrame = -1, submitFrame = -1;
            VisualElement keyControl = null, submitControl = null;
            NavigationSubmitEvent forwarded = null;
            VisualElement Control(object target) {
                for (var element = target as VisualElement; element != null && element != inputRoot; element = element.parent)
                    if (element is Button || element is Toggle) return element;
                return null;
            }
            EventCallback<NavigationSubmitEvent> submit = evt => {
                var control = Control(evt.target); if (control == null) return;
                if (ReferenceEquals(evt, forwarded)) forwarded = null;
                else if (keyControl == control && keyFrame == Time.frameCount) { evt.StopImmediatePropagation(); inputRoot.focusController?.IgnoreEvent(evt); return; }
                submitControl = control; submitFrame = Time.frameCount;
            };
            EventCallback<KeyDownEvent> key = evt => {
                if (!open || evt.altKey || evt.ctrlKey || evt.commandKey) return;
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter && evt.keyCode != KeyCode.Space) return;
                var control = Control(evt.target); if (control == null || !control.enabledInHierarchy) return;
                if (submitControl != control || submitFrame != Time.frameCount) {
                    keyControl = control; keyFrame = Time.frameCount;
                    using (var navigation = NavigationSubmitEvent.GetPooled()) { forwarded = navigation; navigation.target = control; control.SendEvent(navigation); }
                }
                evt.StopPropagation(); inputRoot.focusController?.IgnoreEvent(evt);
            };
            inputRoot.RegisterCallback(submit, TrickleDown.TrickleDown); inputRoot.RegisterCallback(key, TrickleDown.TrickleDown);
            unbind.Add(() => { inputRoot.UnregisterCallback(submit, TrickleDown.TrickleDown); inputRoot.UnregisterCallback(key, TrickleDown.TrickleDown); });
        }
        void Unbind() { foreach (var action in unbind) action(); unbind.Clear(); sliders.Clear(); toggles.Clear(); }
        void OpenModal(Modal type) {
            if (leavingScene) return;
            confirm.SetEnabled(true); cancel.SetEnabled(true);
            currentModal = type; modalError.text = "";
            presentation?.Modal(type==Modal.Guide);
            modalTitle.text = type == Modal.Guide ? "PHÍM ĐIỀU KHIỂN" : type == Modal.Reset ? "ĐẶT LẠI CÀI ĐẶT" : "VỀ MÀN HÌNH CHÍNH";
            modalText.text = type == Modal.Guide ? controlsGuide?.Invoke() ?? "Chưa có thông tin phím điều khiển."
                : type == Modal.Reset ? "Bạn có muốn đặt lại toàn bộ cài đặt? Tiến trình trò chơi của bạn được giữ nguyên."
                : "Bạn có muốn trở về màn hình chính? Trò chơi sẽ lưu tiến trình trước khi chuyển màn hình.";
            confirm.text = type == Modal.Reset ? "ĐẶT LẠI" : "VỀ MÀN HÌNH CHÍNH";
            Show(confirm, type != Modal.Guide); cancel.text = type == Modal.Guide ? "ĐÓNG" : "HỦY";
            Show(modal, true); PanelFocusUtility.Open(modal);
        }
        void ConfirmModal() {
            if (leavingScene) return;
            if (currentModal == Modal.Reset) { model.Reset(); flush?.Invoke(); CloseModal(); }
            else if (currentModal == Modal.Quit) {
                string error = quit?.Invoke() ?? "Màn hình chính chưa sẵn sàng.";
                if (error.Length > 0) modalError.text = error;
                else { leavingScene = true; confirm.SetEnabled(false); cancel.SetEnabled(false); modalText.text = "Đang mở màn hình chính…"; }
            }
        }
        void CloseModal() { currentModal = Modal.None; Show(modal, false); PanelFocusUtility.Close(modal); }
        static void Show(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }
    }
}
