using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.ProfileSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    public enum GameInputMode {
        Gameplay,
        Inventory,
        Menu,
        WorldMap,
        Dialogue
    }

    enum UiReturnTarget {
        Gameplay,
        Menu
    }

    [DisallowMultipleComponent]
    public sealed class GameMenuController : MonoBehaviour {
        const string RootName = "GameMenuRoot";

        static readonly Color Paper = MenuPalette.Surface;
        static readonly Color Forest = new Color(0.075f, 0.28f, 0.13f, 1f);
        static readonly Color AccentGreen = new Color(0.25f, 0.55f, 0.24f, 1f);
        static readonly Color PaleGreen = new Color(0.88f, 0.94f, 0.83f, 1f);
        static readonly Color BorderGreen = new Color(0.35f, 0.53f, 0.25f, 0.95f);
        static readonly Color MutedGreen = new Color(0.30f, 0.39f, 0.30f, 1f);

        static Sprite solidSprite;
        static Sprite roundedSprite;

        [Header("References")]
        [SerializeField] Canvas targetCanvas = null;
        [SerializeField] MonsterInventoryController inventory = null;
        [SerializeField] InventoryInputController inventoryInput = null;
        [SerializeField] MapInputController mapInput = null;
        [SerializeField] MapSystemController mapSystem = null;
        [SerializeField] ProfilePanelController profile = null;
        [SerializeField] LocalPlayerControlLock controlLock = null;
        [SerializeField] MonoBehaviour profileProviderSource = null;
        [SerializeField] PetBoxRuntimeProvider petRoster = null;

        [Header("Keys")]
        [SerializeField] KeyCode menuKey = KeyCode.Tab;
        [SerializeField] KeyCode inventoryKey = KeyCode.I;
        [SerializeField] KeyCode mapKey = KeyCode.M;
        [SerializeField] KeyCode questKey = KeyCode.Q;
        [SerializeField] KeyCode petsKey = KeyCode.P;
        [SerializeField] KeyCode closeKey = KeyCode.Escape;

        [Header("Behaviour")]
        [SerializeField] bool buildOnAwake = true;
        [SerializeField] bool closeOnStart = true;
        [SerializeField] bool disableStandaloneUiHotkeys = true;

        RectTransform root;
        RectTransform panel;
        RectTransform menuContent;
        RectTransform placeholderPanel;
        Text placeholderTitle;
        Text placeholderBody;
        Text descriptionText;
        Image trainerAvatarImage;
        Text trainerAvatarPlaceholder;
        Text trainerNameText;
        Text trainerLevelText;
        Image trainerExperienceFill;
        Text trainerExperienceValueText;
        Text trainerAreaValueText;
        Text trainerPlayTimeValueText;
        Text trainerPartyValueText;
        IPlayerProfileProvider profileProvider;
        bool cursorStateSaved;
        bool previousCursorVisible;
        CursorLockMode previousLockState;
        readonly HashSet<object> dialogueOwners = new HashSet<object>();
        InventoryInputController ownedInventoryInput;
        MapInputController ownedMapInput;
        ProfilePanelController configuredProfile;
        Canvas configuredProfileCanvas;
        WorldMapController subscribedWorldMap;
        UiReturnTarget mapReturnTarget = UiReturnTarget.Gameplay;
        UiReturnTarget placeholderReturnTarget = UiReturnTarget.Menu;
        UiReturnTarget profileReturnTarget = UiReturnTarget.Gameplay;
        float nextReferenceResolveTime;
        float nextTrainerSummaryRefreshTime;
        int handledShortcutFrame = -1;
        KeyCode handledShortcutKey;
        bool inputModeApplied;
        bool minimapPresentationApplied;
        GameObject trainerReturnFocus;

        const float ReferenceRetryInterval = 1f;

        public bool IsMenuOpen => root != null && root.gameObject.activeSelf;
        public GameInputMode CurrentMode { get; private set; } = GameInputMode.Gameplay;
        public bool IsUiOpen => CurrentMode != GameInputMode.Gameplay;
        public bool AllowsCameraTracking => IsMenuOpen && !IsInventoryOpen() && !IsMapOpen()
            && !IsProfileOpen() && dialogueOwners.Count == 0;

        void Awake() {
            ResolveReferences();
            if (buildOnAwake) RebuildMenu();
        }

        void OnEnable() {
            ResolveReferences();
            SubscribeProfileProvider();
            SubscribeProfile();
            SubscribeMapBack();
            ApplyInputOwnership();
            if (root == null && buildOnAwake) RebuildMenu();
        }

        void Start() {
            ResolveReferences();
            ApplyInputOwnership();
            if (closeOnStart) CloseMenu();
            RefreshInputModeFromOpenUi();
        }

        void OnDisable() {
            if (inventory != null) inventory.CodexBackRequested -= HandleCodexBack;
            if (subscribedInventoryBack != null) subscribedInventoryBack.BackRequested -= HandleInventoryBack;
            subscribedInventoryBack = null;
            UnsubscribeProfileProvider();
            UnsubscribeProfile();
            UnsubscribeMapBack();
            CloseProfile(false);
            CloseMenu();
            dialogueOwners.Clear();
            controlLock?.UnlockControls(this);
            SetRouterGameplayBlock(false);
            ReleaseInputOwnership();
            if (!IsInventoryOpen() && !IsMapOpen()) RestoreCursorState();
        }

        void Update() {
            ResolveRuntimeReferences(false);
            RefreshInputModeFromOpenUi();

            if (IsMenuOpen && Time.unscaledTime >= nextTrainerSummaryRefreshTime) {
                nextTrainerSummaryRefreshTime = Time.unscaledTime + 1f;
                RefreshTrainerSummary();
            }

            bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Pressed(closeKey)) HandleShortcut(closeKey, reverse);
            else if (Pressed(menuKey)) HandleShortcut(menuKey, reverse);
            else if (Pressed(inventoryKey)) HandleShortcut(inventoryKey, reverse);
            else if (Pressed(mapKey)) HandleShortcut(mapKey, reverse);
            else if (Pressed(questKey)) HandleShortcut(questKey, reverse);
            else if (Pressed(petsKey)) HandleShortcut(petsKey, reverse);
            else if (menuKey != KeyCode.Tab && Pressed(KeyCode.Tab)) HandleShortcut(KeyCode.Tab, reverse);
        }

        // UI Toolkit and legacy Input share this route; the same key edge runs once.
        public bool HandleShortcut(KeyCode key, bool reverse = false) {
            if (key == KeyCode.None) return false;
            if (handledShortcutFrame == Time.frameCount && handledShortcutKey == key) return true;
            RefreshInputModeFromOpenUi();
            if (key != closeKey && IsEditingText()) return false;
            bool handled = RouteShortcut(key, reverse);
            if (handled) MarkShortcutHandled(key);
            return handled;
        }
        public void MarkShortcutHandled(KeyCode key) { handledShortcutFrame = Time.frameCount; handledShortcutKey = key; }

        bool RouteShortcut(KeyCode key, bool reverse) {
            if (key == closeKey) {
                if (inventory != null && inventory.IsEvolutionScreenOpen) { inventory.DismissEvolutionScreen(); return true; }
                if (CurrentMode == GameInputMode.Gameplay) OpenSettingsFromGameplay(); else CloseTopUi();
                return true;
            }
            if (CurrentMode == GameInputMode.Dialogue) return false;
            if (key == menuKey && (CurrentMode == GameInputMode.Gameplay || IsMenuOpen) && !(key == KeyCode.Tab && reverse && IsMenuOpen)) {
                ToggleMenuFromInput(); return true;
            }
            if (key == KeyCode.Tab && (IsProfileOpen() || CurrentMode == GameInputMode.WorldMap || IsMenuOpen)) {
                Transform navigationRoot = IsProfileOpen() ? profile.NavigationRoot
                    : CurrentMode == GameInputMode.WorldMap ? mapSystem?.GetComponent<Capstone.Game.MapSystem.UI.WorldMapUIController>()?.NavigationRoot : panel;
                MenuKeyboardNavigation.Advance(navigationRoot, reverse); return true;
            }
            if (key == inventoryKey && inventory != null && inventory.IsBagSectionOpen
                || key == questKey && inventory != null && inventory.IsQuestSectionOpen
                || key == petsKey && inventory != null && inventory.IsPetsSectionOpen
                || key == mapKey && IsMapOpen()) { CloseTopUi(); return true; }
            if (CurrentMode != GameInputMode.Gameplay && !IsMenuOpen) return false;
            if (key == inventoryKey) { OpenInventoryDirect(); return true; }
            if (key == mapKey) { OpenMapDirect(); return true; }
            if (key == questKey) { OpenQuestDirect(); return true; }
            if (key == petsKey) { OpenPetsDirect(); return true; }
            return false;
        }

        bool IsEditingText() {
            if (inventoryInput != null && inventory != null && inventory.IsOpen && inventoryInput.IsEditingUiText()) return true;
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null) return false;
            var input = selected.GetComponentInParent<InputField>();
            if (input != null && input.isFocused) return true;
            var tmpInput = selected.GetComponentInParent<TMPro.TMP_InputField>();
            return tmpInput != null && tmpInput.isFocused;
        }

        [ContextMenu("Rebuild Game Menu")]
        public void RebuildMenu() {
            ResolveReferences();
            EnsureCanvas();

            root = EnsureRoot(targetCanvas.transform);
            ClearChildren(root);

            BuildDimmer(root);
            panel = CreateRoundedPanel(root, "TrainerMenuPanel", MenuPalette.Background, true);
            SetMenuPanelBounds(panel);
            AddOutline(panel.gameObject, BorderGreen, 2f);
            menuContent = CreatePanel(panel, "MenuContent", Color.clear, false);
            menuContent.anchorMin = menuContent.anchorMax = menuContent.pivot = new Vector2(0.5f, 0.5f);
            menuContent.anchoredPosition = Vector2.zero;
            menuContent.sizeDelta = new Vector2(620f, 1080f);
            BuildHeader(menuContent);
            BuildMenuGrid(menuContent);
            BuildFooter(menuContent);
            RefreshMenuLayout();
            BuildFeaturePlaceholder(root);
            root.gameObject.SetActive(false);
        }

        public void OpenMenu() {
            ResolveReferences();
            if (root == null) RebuildMenu();
            if (root == null || IsMenuOpen) return;

            CloseProfile(false);
            CloseInventory();
            CloseMap();
            SaveCursorState();
            controlLock?.LockControls(this);
            SetRouterGameplayBlock(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            HideFeaturePlaceholder();
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            RefreshTrainerSummary();
            SetInputMode(GameInputMode.Menu);
            if (trainerReturnFocus != null && trainerReturnFocus.activeInHierarchy)
                MenuKeyboardNavigation.Restore(trainerReturnFocus);
            else MenuKeyboardNavigation.FocusFirst(panel);
        }

        public void CloseMenu() {
            RememberTrainerFocus();
            if (root != null) root.gameObject.SetActive(false);
            HideFeaturePlaceholder();

            if (!IsInventoryOpen() && !IsMapOpen() && !IsProfileOpen() && dialogueOwners.Count == 0) {
                controlLock?.UnlockControls(this);
                SetRouterGameplayBlock(false);
                RestoreCursorState();
            }

            RefreshInputModeFromOpenUi();
        }
        void RememberTrainerFocus() {
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (root != null && selected != null && selected.transform.IsChildOf(root)) trainerReturnFocus = selected;
        }

        void LateUpdate() {
            if (IsMenuOpen) RefreshMenuLayout();
        }

        void RefreshMenuLayout() {
            if (panel == null || menuContent == null) return;
            // Preserve the design's proportions inside a panel spanning 35% of the viewport.
            float scale = Mathf.Min(panel.rect.width / 620f, panel.rect.height / 1080f);
            if (scale > 0f && !Mathf.Approximately(menuContent.localScale.x, scale))
                menuContent.localScale = Vector3.one * scale;
        }

        static void SetMenuPanelBounds(RectTransform rect) {
            rect.anchorMin = new Vector2(.65f, 0f);
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        public void ToggleMenu() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "menu-visibility")) return;
            if (IsMenuOpen) CloseMenu();
            else OpenMenu();
        }

        void ToggleMenuFromInput() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "menu-visibility")) return;
            if (IsProfileOpen()) {
                CloseProfile(true);
                return;
            }

            if (IsMenuOpen) {
                CloseMenu();
                return;
            }

            CloseInventory();
            CloseMap();
            OpenMenu();
        }

        public void OpenInventoryDirect() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "bag")) return;
            ResolveReferences();

            if (IsInventoryOpen()) {
                inventory.RequestClose();
                return;
            }

            SetInventoryReturnTarget(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
            CloseProfile(false);
            CloseMenu();
            CloseMap();

            if (inventory == null) {
                OpenFeaturePlaceholder("Inventory");
                return;
            }

            inventory.OpenInventoryPanel();
            SetInputMode(GameInputMode.Inventory);
        }

        UiReturnTarget codexReturnTarget;
        UiReturnTarget inventoryReturnTarget;
        MonsterInventoryController subscribedInventoryBack;
        void SetInventoryReturnTarget(UiReturnTarget target) {
            inventoryReturnTarget = target;
            if (subscribedInventoryBack == inventory) return;
            if (subscribedInventoryBack != null) subscribedInventoryBack.BackRequested -= HandleInventoryBack;
            subscribedInventoryBack = inventory;
            if (subscribedInventoryBack != null) subscribedInventoryBack.BackRequested += HandleInventoryBack;
        }
        void HandleInventoryBack() {
            bool returnToMenu = inventoryReturnTarget == UiReturnTarget.Menu;
            inventoryReturnTarget = UiReturnTarget.Gameplay;
            if (returnToMenu) OpenMenu(); else RefreshInputModeFromOpenUi();
        }
        public void OpenCodexDirect() {
            if (!PlayerActionGate.TryAccept(this, "codex")) return;
            ResolveReferences();
            codexReturnTarget = IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay;
            CloseProfile(false); CloseMenu(); CloseMap();
            if (inventory == null) { OpenFeaturePlaceholder("Codex", codexReturnTarget); return; }
            inventory.CodexBackRequested -= HandleCodexBack;
            inventory.CodexBackRequested += HandleCodexBack;
            inventory.OpenCodexPanel();
            SetInputMode(GameInputMode.Inventory);
        }
        void HandleCodexBack() {
            if (codexReturnTarget == UiReturnTarget.Menu) OpenMenu();
            else RefreshInputModeFromOpenUi();
        }

        public void OpenQuestDirect() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "quest")) return;
            ResolveReferences();
            SetInventoryReturnTarget(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
            CloseProfile(false);
            CloseMenu();
            CloseMap();

            if (inventory == null) {
                OpenFeaturePlaceholder("Quest");
                return;
            }

            inventory.OpenQuestJournalPanel();
            SetInputMode(GameInputMode.Inventory);
        }

        public void OpenPetsDirect() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "pets")) return;
            ResolveReferences();
            SetInventoryReturnTarget(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
            CloseProfile(false);
            CloseMenu();
            CloseMap();

            if (inventory == null) {
                OpenFeaturePlaceholder("Pets");
                return;
            }

            inventory.OpenPetPartyPanel();
            SetInputMode(GameInputMode.Inventory);
        }

        public void OpenBoxDirect() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "box")) return;
            ResolveReferences();
            SetInventoryReturnTarget(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
            CloseProfile(false);
            CloseMenu();
            CloseMap();

            if (inventory == null) {
                OpenFeaturePlaceholder("Box");
                return;
            }

            inventory.OpenPetBoxPanel(false);
            SetInputMode(GameInputMode.Inventory);
        }

        public void OpenBoxFromWorld() {
            ResolveReferences();
            SetInventoryReturnTarget(UiReturnTarget.Gameplay);
            CloseProfile(false);
            CloseMenu();
            CloseMap();

            if (inventory == null) {
                OpenFeaturePlaceholder("Box", UiReturnTarget.Gameplay);
                return;
            }

            inventory.OpenPetBoxPanel(PetBoxReturnTarget.Gameplay);
            SetInputMode(GameInputMode.Inventory);
        }

        public void OpenShopFromWorld(Capstone.Game.ShopSystem.ShopSource source = null) {
            if (!PlayerActionGate.TryAccept(this, "shop")) return;
            ResolveReferences();
            SetInventoryReturnTarget(UiReturnTarget.Gameplay);
            CloseProfile(false);
            CloseMenu();
            CloseMap();
            if (inventory == null) return;
            inventory.OpenShopPanel(source);
            SetInputMode(GameInputMode.Inventory);
        }

        public void OpenMapDirect() {
            OpenMap(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
        }

        void OpenMap(UiReturnTarget returnTarget) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "map")) return;
            ResolveReferences();

            if (mapInput != null && mapInput.IsOpen) {
                HandleMapBack();
                return;
            }

            mapReturnTarget = returnTarget;

            CloseProfile(false);
            CloseMenu();
            CloseInventory();

            if (mapInput == null) {
                OpenFeaturePlaceholder("Map", returnTarget);
                return;
            }

            mapInput.OpenMap();
            SetInputMode(GameInputMode.WorldMap);
        }

        void OpenSettingsFromGameplay() {
            OpenSettings(UiReturnTarget.Gameplay);
        }
        public void OpenSettingsDirect() => OpenSettings(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
        void OpenSettings(UiReturnTarget target) {
            if (!PlayerActionGate.TryAccept(this, "settings")) return;
            ResolveReferences(); SetInventoryReturnTarget(target);
            CloseProfile(false); CloseMenu(); CloseMap();
            if (inventory == null) return;
            inventory.OpenSettingsPanel(GetControlsGuide);
            SetInputMode(GameInputMode.Inventory);
        }

        public string GetControlsGuide() {
            var text = new System.Text.StringBuilder();
            text.AppendLine("WASD / phím mũi tên — Di chuyển\nShift — Chạy nhanh");
            var movement = FindFirstObjectByType<BasicPlayerMovement>();
            if (movement != null) {
                if (movement.enableJump && movement.jumpKey != KeyCode.None) text.AppendLine(movement.jumpKey + " — Nhảy");
                if (movement.pickUpKey != KeyCode.None) text.AppendLine(movement.pickUpKey + " — Nhặt đồ khi nhìn thấy và ở đủ gần");
                if (movement.enableRoll && movement.rollKey != KeyCode.None) text.AppendLine(movement.rollKey + " — Lăn");
            }
            text.AppendLine(menuKey + " — Mở / đóng Menu nhà huấn luyện");
            text.AppendLine(inventoryKey + " — Mở / đóng Túi đồ"); text.AppendLine(mapKey + " — Mở / đóng Bản đồ");
            text.AppendLine(questKey + " — Mở / đóng Nhiệm vụ"); text.AppendLine(petsKey + " — Mở / đóng Thú đồng hành");
            text.AppendLine(closeKey + " — Cài đặt / Trở lại; đóng hộp thoại trước");
            text.AppendLine("Tab / Shift+Tab — Chuyển focus trong các panel; Shift+Tab trong Trainer Menu\nEnter — Chọn nút đang focus");
            foreach (var npc in FindObjectsByType<NpcPanelInteractable>(FindObjectsSortMode.None)) {
                text.AppendLine(npc.InteractKey + " — Tương tác khi nhìn thấy và ở đủ gần"); break;
            }
            var camera = FindFirstObjectByType<BasicCameraFollow>();
            if (camera != null) {
                text.AppendLine("Di chuyển chuột — Xoay camera");
                if (camera.enableEnemyLock) text.AppendLine("Chuột " + (camera.lockMouseButton + 1) + " — Ghim / bỏ ghim mục tiêu");
                if (camera.enableScrollZoom) text.AppendLine("Con lăn — Thu phóng; đổi mục tiêu khi đang ghim");
            }
            var command = FindFirstObjectByType<PetCommandInput>();
            if (command != null) {
                text.AppendLine("1–6 — Gọi thú ở ô tương ứng");
                if (command.withdrawKey != KeyCode.None) text.AppendLine(command.withdrawKey + " — Thu thú");
                text.AppendLine("Chuột " + (command.commandMouseButton + 1) + " — Ra lệnh thú tấn công mục tiêu");
            }
            var visuals = FindFirstObjectByType<PlayerVisualSwitcher>();
            if (visuals != null && visuals.SwitchKey != KeyCode.None) text.AppendLine(visuals.SwitchKey + " — Đổi nhân vật nam / nữ");
            var hud = FindFirstObjectByType<GameplayHudController>();
            if (hud != null) for (int i = 0; i < 4; i++) if (hud.GetSkillHotkey(i) != KeyCode.None) text.AppendLine(hud.GetSkillHotkey(i) + " — Kỹ năng thú " + (i + 1));
            var items = FindFirstObjectByType<GameplayHudQuickItemsView>();
            if (items != null) for (int i = 0; i < 2; i++) if (items.GetHotkey(i) != KeyCode.None) text.AppendLine(items.GetHotkey(i) + " — Vật phẩm nhanh " + (i + 1));
            return text.ToString();
        }

        public void OpenProfileDirect() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "profile")) return;
            ResolveReferences();
            profileReturnTarget = IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay;

            if (profile == null) {
                OpenFeaturePlaceholder("Profile");
                return;
            }

            CloseInventory();
            CloseMap();
            HideFeaturePlaceholder();
            RememberTrainerFocus();
            if (root != null) root.gameObject.SetActive(false);

            SaveCursorState();
            controlLock?.LockControls(this);
            SetRouterGameplayBlock(true);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            profile.Open();
            SetInputMode(GameInputMode.Menu);
        }

        public void SetDialogueOpen(object owner, bool open) {
            if (owner == null) return;

            if (open) {
                dialogueOwners.Add(owner);
                CloseProfile(false);
                CloseMenu();
                CloseInventory();
                CloseMap();
                SaveCursorState();
                controlLock?.LockControls(this);
                SetRouterGameplayBlock(true);
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
                SetInputMode(GameInputMode.Dialogue);
                return;
            }

            dialogueOwners.Remove(owner);
            if (dialogueOwners.Count == 0 && !IsMenuOpen && !IsInventoryOpen() && !IsMapOpen() && !IsProfileOpen()) {
                controlLock?.UnlockControls(this);
                SetRouterGameplayBlock(false);
                RestoreCursorState();
                SetInputMode(GameInputMode.Gameplay);
            }
        }

        void CloseTopUi() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, IsMenuOpen ? "menu-visibility" : "ui-back")) return;
            if (inventory != null && inventory.IsCodexOpen) {
                if (inventory.TryCloseTopmostPanel()) return;
                inventory.Close(); HandleCodexBack(); return;
            }
            RefreshInputModeFromOpenUi();

            if (IsProfileOpen()) {
                if (profile.TryClosePopup()) return;
                HandleProfileBack();
                return;
            }

            if (placeholderPanel != null && placeholderPanel.gameObject.activeSelf) {
                HandlePlaceholderBack();
                return;
            }

            switch (CurrentMode) {
                case GameInputMode.Dialogue:
                    return;
                case GameInputMode.WorldMap:
                    if (mapSystem != null && mapSystem.GetComponent<Capstone.Game.MapSystem.UI.WorldMapUIController>()?.TryCloseMarkerGroup() == true) return;
                    HandleMapBack();
                    return;
                case GameInputMode.Inventory:
                    if (inventory != null && !inventory.TryCloseTopmostPanel()) inventory.RequestClose();
                    return;
                case GameInputMode.Menu:
                    CloseMenu();
                    return;
            }
        }

        void ResolveReferences() {
            EnsureCanvas();
            ResolveRuntimeReferences(true);
        }

        void ResolveProfileProvider() {
            IPlayerProfileProvider nextProvider = profileProviderSource as IPlayerProfileProvider;
            if (nextProvider == null) {
                MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (MonoBehaviour behaviour in behaviours) {
                    if (!(behaviour is IPlayerProfileProvider candidate)) continue;
                    profileProviderSource = behaviour;
                    nextProvider = candidate;
                    break;
                }
            }

            if (nextProvider == null && Application.isPlaying) {
                PlayerProfileRuntimeProvider runtimeProvider = GetComponent<PlayerProfileRuntimeProvider>();
                if (runtimeProvider == null) runtimeProvider = gameObject.AddComponent<PlayerProfileRuntimeProvider>();
                profileProviderSource = runtimeProvider;
                nextProvider = runtimeProvider;
            }

            if (ReferenceEquals(profileProvider, nextProvider)) return;
            UnsubscribeProfileProvider();
            profileProvider = nextProvider;
            SubscribeProfileProvider();
        }

        void SubscribeProfileProvider() {
            if (profileProvider == null) return;
            profileProvider.ProfileChanged -= RefreshTrainerSummary;
            profileProvider.ProfileChanged += RefreshTrainerSummary;
        }

        void UnsubscribeProfileProvider() {
            if (profileProvider == null) return;
            profileProvider.ProfileChanged -= RefreshTrainerSummary;
        }

        void ResolveRuntimeReferences(bool force) {
            bool needsReferences = inventory == null
                || inventoryInput == null
                || mapInput == null
                || mapSystem == null
                || profile == null
                || controlLock == null
                || profileProvider == null
                || petRoster == null;
            if (!force && !needsReferences) return;
            if (!force && Time.unscaledTime < nextReferenceResolveTime) return;
            nextReferenceResolveTime = Time.unscaledTime + ReferenceRetryInterval;

            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryController>();
            if (inventoryInput == null) inventoryInput = FindFirstObjectByType<InventoryInputController>();
            if (mapInput == null) mapInput = FindFirstObjectByType<MapInputController>();
            if (mapSystem == null) mapSystem = FindFirstObjectByType<MapSystemController>();
            if (profile == null) profile = FindFirstObjectByType<ProfilePanelController>();
            if (profile == null && Application.isPlaying) profile = gameObject.AddComponent<ProfilePanelController>();
            if (controlLock == null) controlLock = FindFirstObjectByType<LocalPlayerControlLock>();
            if (petRoster == null) petRoster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);
            ResolveProfileProvider();

            if (inventoryInput == null && inventory != null) inventoryInput = inventory.GetComponent<InventoryInputController>();
            if (profile != null && (configuredProfile != profile || configuredProfileCanvas != targetCanvas)) {
                profile.Configure(targetCanvas);
                configuredProfile = profile;
                configuredProfileCanvas = targetCanvas;
                SubscribeProfile();
            }

            SubscribeMapBack();
            ApplyInputOwnership();
        }

        void ApplyInputOwnership() {
            if (!disableStandaloneUiHotkeys) {
                ReleaseInputOwnership();
                return;
            }

            if (ownedInventoryInput != inventoryInput) {
                if (ownedInventoryInput != null) { ownedInventoryInput.SetShortcutRouter(null); ownedInventoryInput.SetOpenCloseHotkeysEnabled(true); }
                ownedInventoryInput = inventoryInput;
                if (ownedInventoryInput != null) { ownedInventoryInput.SetOpenCloseHotkeysEnabled(false); ownedInventoryInput.SetShortcutRouter(this); }
            }

            if (ownedMapInput != mapInput) {
                if (ownedMapInput != null) ownedMapInput.SetOpenCloseHotkeysEnabled(true);
                ownedMapInput = mapInput;
                if (ownedMapInput != null) ownedMapInput.SetOpenCloseHotkeysEnabled(false);
            }
        }

        void ReleaseInputOwnership() {
            if (ownedInventoryInput != null) { ownedInventoryInput.SetShortcutRouter(null); ownedInventoryInput.SetOpenCloseHotkeysEnabled(true); }
            if (ownedMapInput != null) ownedMapInput.SetOpenCloseHotkeysEnabled(true);
            ownedInventoryInput = null;
            ownedMapInput = null;
        }

        void RefreshInputModeFromOpenUi() {
            GameInputMode nextMode;
            if (dialogueOwners.Count > 0) {
                nextMode = GameInputMode.Dialogue;
            }
            else if (IsMapOpen()) {
                nextMode = GameInputMode.WorldMap;
            }
            else if (IsInventoryOpen()) {
                nextMode = GameInputMode.Inventory;
            }
            else if (IsProfileOpen()) {
                nextMode = GameInputMode.Menu;
            }
            else if (IsMenuOpen || (placeholderPanel != null && placeholderPanel.gameObject.activeSelf)) {
                nextMode = GameInputMode.Menu;
            }
            else {
                nextMode = GameInputMode.Gameplay;
            }

            SetInputMode(nextMode);
        }

        void SetInputMode(GameInputMode mode) {
            bool showMinimap = mode == GameInputMode.Gameplay || IsMenuOpen;
            if (inputModeApplied && CurrentMode == mode && minimapPresentationApplied == showMinimap) return;
            inputModeApplied = true;
            minimapPresentationApplied = showMinimap;
            CurrentMode = mode;
            if (mode == GameInputMode.Gameplay) {
                // Recover if a panel is hidden/disabled directly instead of using its Back button.
                controlLock?.UnlockControls(this);
                SetRouterGameplayBlock(false);
                RestoreCursorState();
            }
            mapSystem?.SetMinimapVisible(showMinimap);
        }

        void EnsureCanvas() {
            if (targetCanvas != null) {
                ConfigureCanvas(targetCanvas);
                return;
            }

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            targetCanvas = canvases.FirstOrDefault(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay);
            if (targetCanvas != null) {
                ConfigureCanvas(targetCanvas);
                return;
            }

            GameObject canvasObject = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvas(targetCanvas);
        }

        void BuildDimmer(RectTransform parent) {
            RectTransform dimmer = CreatePanel(parent, "Dimmer", new Color(0.01f, 0.035f, 0.02f, 0.38f), true);
            Stretch(dimmer);
        }

        void BuildHeader(RectTransform parent) {
            RectTransform summary = CreatePanel(parent, "TrainerSummary", Color.clear, false);
            SetTopStretch(summary, 18f, 12f, 18f, 216f);

            RectTransform avatarFrame = CreateRoundedPanel(summary, "AvatarFrame", Paper, false);
            SetTopLeft(avatarFrame, 16f, 20f, 100f, 100f);
            AddOutline(avatarFrame.gameObject, BorderGreen, 1f);

            trainerAvatarImage = CreatePanel(avatarFrame, "Avatar", PaleGreen, false).GetComponent<Image>();
            SetTopLeft(trainerAvatarImage.rectTransform, 4f, 4f, 92f, 92f);
            trainerAvatarImage.preserveAspect = true;
            trainerAvatarPlaceholder = CreateText(avatarFrame, "AvatarPlaceholder", "BẠN", 15, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(trainerAvatarPlaceholder.rectTransform);

            Text title = CreateText(summary, "Title", "HUẤN LUYỆN VIÊN", 30, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 138f, 10f, 420f, 44f);

            trainerNameText = CreateText(summary, "PlayerName", "Người huấn luyện", 22, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(trainerNameText.rectTransform, 140f, 50f, 410f, 34f);

            RectTransform levelBadge = CreateRoundedPanel(summary, "LevelBadge", Forest, false);
            SetTopLeft(levelBadge, 138f, 88f, 88f, 40f);
            AddOutline(levelBadge.gameObject, AccentGreen, 1f);
            trainerLevelText = CreateText(levelBadge, "Cấp độ", "Lv. -", 17, FontStyle.Bold, Paper, TextAnchor.MiddleCenter);
            Stretch(trainerLevelText.rectTransform);

            Text experienceCaption = CreateText(summary, "ExperienceCaption", "EXP", 13, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(experienceCaption.rectTransform, 242f, 83f, 58f, 24f);
            trainerExperienceValueText = CreateText(summary, "ExperienceValue", "- / -", 13, FontStyle.Normal, Forest, TextAnchor.MiddleRight);
            SetTopLeft(trainerExperienceValueText.rectTransform, 382f, 83f, 174f, 24f);

            RectTransform experienceTrack = CreateRoundedPanel(summary, "ExperienceTrack", new Color(0.90f, 0.92f, 0.84f, 1f), false);
            SetTopLeft(experienceTrack, 242f, 111f, 314f, 17f);
            AddOutline(experienceTrack.gameObject, new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.75f), 1f);
            trainerExperienceFill = CreatePanel(experienceTrack, "Fill", AccentGreen, false).GetComponent<Image>();
            trainerExperienceFill.type = Image.Type.Filled;
            trainerExperienceFill.fillMethod = Image.FillMethod.Horizontal;
            trainerExperienceFill.fillOrigin = 0;
            trainerExperienceFill.fillAmount = 0f;
            SetInset(trainerExperienceFill.rectTransform, 2f);

            RectTransform separator = CreatePanel(summary, "SummarySeparator", new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.55f), false);
            SetTopStretch(separator, 8f, 143f, 8f, 1f);

            trainerAreaValueText = BuildQuickInfo(summary, "Area", "A", "Khu vực", 16f);
            trainerPlayTimeValueText = BuildQuickInfo(summary, "PlayTime", "T", "Thời gian chơi", 208f);
            trainerPartyValueText = BuildQuickInfo(summary, "Party", "THÚ", "Đội thú", 400f);

            RectTransform dividerOne = CreatePanel(summary, "QuickDividerOne", new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.38f), false);
            SetTopLeft(dividerOne, 198f, 154f, 1f, 48f);
            RectTransform dividerTwo = CreatePanel(summary, "QuickDividerTwo", new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.38f), false);
            SetTopLeft(dividerTwo, 390f, 154f, 1f, 48f);
        }

        Text BuildQuickInfo(RectTransform parent, string name, string iconText, string labelText, float left) {
            RectTransform iconBox = CreateRoundedPanel(parent, name + "Icon", PaleGreen, false);
            SetTopLeft(iconBox, left, 157f, 36f, 40f);
            Text icon = CreateText(iconBox, "Label", iconText, iconText.Length > 1 ? 10 : 16, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(icon.rectTransform);

            Text label = CreateText(parent, name + "Label", labelText, 13, FontStyle.Normal, MutedGreen, TextAnchor.MiddleLeft);
            SetTopLeft(label.rectTransform, left + 46f, 151f, 136f, 23f);
            Text value = CreateText(parent, name + "Value", "-", 16, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(value.rectTransform, left + 46f, 175f, 136f, 29f);
            return value;
        }

        void BuildMenuGrid(RectTransform parent) {
            RectTransform grid = CreatePanel(parent, "MenuGrid", Color.clear, false);
            SetTopStretch(grid, 18f, 238f, 18f, 732f);

            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = new Vector2(12f, 40f);
            layout.cellSize = new Vector2(280f, 205f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 2;

            AddMenuButton(grid, "Profile", "Thông tin người chơi", true, OpenProfileDirect);
            AddMenuButton(grid, "Pets", "Quản lý đội thú", true, OpenPetsDirect);
            AddMenuButton(grid, "Inventory", "Mở túi đồ", true, OpenInventoryDirect);
            AddMenuButton(grid, "Quest", "Theo dõi nhiệm vụ", true, OpenQuestDirect);
            AddMenuButton(grid, "Map", "Xem bản đồ", true, OpenMapDirect);
            AddMenuButton(grid, "Codex", "Tra cứu sinh vật", true, OpenCodexDirect);
        }

        void AddMenuButton(RectTransform parent, string title, string description, bool enabled, UnityEngine.Events.UnityAction action) {
            Button button = CreateTrainerButton(parent, title + "Button", Paper);
            button.interactable = enabled;
            if (enabled && action != null) button.onClick.AddListener(() => { if (Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "button:" + title)) action(); });
            AddOutline(button.gameObject, new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.82f), 1f);

            RectTransform iconBox = CreateRoundedPanel(button.transform, "Icon", PaleGreen, false);
            SetTopLeft(iconBox, 16f, 60f, 78f, 84f);
            AddOutline(iconBox.gameObject, new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.55f), 1f);
            Text icon = CreateText(iconBox, "Label", GetMenuIcon(title), GetMenuIcon(title).Length > 3 ? 12 : 16, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(icon.rectTransform);

            Text label = CreateText(button.transform, "Label", VietnameseUI.Screen(title), 28, FontStyle.Bold, enabled ? Forest : MutedGreen, TextAnchor.MiddleLeft);
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 17;
            label.resizeTextMaxSize = 28;
            SetTopLeft(label.rectTransform, 108f, 30f, 158f, 74f);

            Text sub = CreateText(button.transform, "Description", description, 14, FontStyle.Normal, MutedGreen, TextAnchor.UpperLeft);
            SetTopLeft(sub.rectTransform, 108f, 108f, 158f, 52f);

            AddButtonHint(button, VietnameseUI.Screen(title) + " — " + description);
        }

        void BuildFooter(RectTransform parent) {
            RectTransform footer = CreatePanel(parent, "Footer", Color.clear, false);
            SetBottomStretch(footer, 18f, 12f, 18f, 86f);

            RectTransform divider = CreatePanel(footer, "FooterDivider", new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.5f), false);
            SetTopStretch(divider, 6f, 0f, 6f, 1f);

            descriptionText = CreateText(footer, "Description", "Chọn một mục trong menu.", 16, FontStyle.Normal, MutedGreen, TextAnchor.MiddleCenter);
            SetTopStretch(descriptionText.rectTransform, 102f, 20f, 176f, 52f);

            Button settings = CreateTrainerButton(footer, "SettingsButton", Paper);
            SetTopLeft(settings.GetComponent<RectTransform>(), 6f, 20f, 52f, 52f);
            AddOutline(settings.gameObject, BorderGreen, 1f);
            Text settingsIcon = CreateText(settings.transform, "Icon", "CĐ", 13, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(settingsIcon.rectTransform);
            settings.onClick.AddListener(() => OpenSettings(UiReturnTarget.Menu));
            AddButtonHint(settings, "Cài đặt");

            RectTransform settingsTooltip = CreateRoundedPanel(footer, "SettingsTooltip", Forest, false);
            SetTopLeft(settingsTooltip, 64f, 29f, 92f, 32f);
            Text settingsTooltipText = CreateText(settingsTooltip, "Label", "Cài đặt", 14, FontStyle.Bold, Paper, TextAnchor.MiddleCenter);
            Stretch(settingsTooltipText.rectTransform);
            settingsTooltip.gameObject.SetActive(false);
            AddHoverVisibility(settings, settingsTooltip.gameObject);

            Button back = CreateTrainerButton(footer, "BackButton", Paper);
            SetTopRight(back.GetComponent<RectTransform>(), 6f, 20f, MenuTypography.BackWidth, MenuTypography.BackHeight);
            AddOutline(back.gameObject, AccentGreen, 1f);
            Text backLabel = CreateText(back.transform, "Label", "←  TRỞ LẠI", 19, FontStyle.Bold, Paper, TextAnchor.MiddleCenter);
            Stretch(backLabel.rectTransform);
            MenuTypography.StyleBack(back);
            back.onClick.AddListener(() => { if (Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "menu-visibility")) CloseMenu(); });
            AddButtonHint(back, "Trở về trò chơi");
        }

        void BuildFeaturePlaceholder(RectTransform parent) {
            placeholderPanel = CreatePanel(parent, "FeaturePlaceholderPanel", new Color(0.94f, 0.90f, 0.78f, 0.98f), true);
            SetFullscreenFeaturePanel(placeholderPanel, 36f, 34f, 36f, 34f);

            RectTransform header = CreatePanel(placeholderPanel, "Header", new Color(0.82f, 0.73f, 0.54f, 1f), true);
            SetTopStretch(header, 0f, 0f, 0f, 82f);

            placeholderTitle = CreateText(header, "Title", "Tính năng", 38, FontStyle.Bold, new Color(0.13f, 0.12f, 0.08f, 1f), TextAnchor.MiddleLeft);
            SetTopStretch(placeholderTitle.rectTransform, 34f, 16f, 96f, 52f);

            Button close = CreateButton(header, "CloseButton", "X", true);
            SetTopRight(close.GetComponent<RectTransform>(), 24f, 18f, 46f, 46f);
            close.onClick.AddListener(HandlePlaceholderBack);

            placeholderBody = CreateText(placeholderPanel, "Body", string.Empty, 22, FontStyle.Normal, new Color(0.20f, 0.18f, 0.13f, 1f), TextAnchor.MiddleCenter);
            SetTopStretch(placeholderBody.rectTransform, 72f, 130f, 72f, 260f);

            Button back = CreateButton(placeholderPanel, "BackButton", "Trở lại", true);
            SetBottomRight(back.GetComponent<RectTransform>(), 34f, 30f, 132f, 50f);
            back.onClick.AddListener(HandlePlaceholderBack);

            placeholderPanel.gameObject.SetActive(false);
        }

        void OpenFeaturePlaceholder(string featureName) {
            OpenFeaturePlaceholder(featureName, UiReturnTarget.Menu);
        }

        void OpenFeaturePlaceholder(string featureName, UiReturnTarget returnTarget) {
            placeholderReturnTarget = returnTarget;
            OpenMenu();
            if (placeholderPanel == null) return;

            string title = string.IsNullOrWhiteSpace(featureName) ? "Tính năng" : featureName;
            if (placeholderTitle != null) placeholderTitle.text = VietnameseUI.Screen(title);
            if (placeholderBody != null) {
                placeholderBody.text = "Tính năng này chưa sẵn sàng. Hãy quay lại sau.";
            }

            if (descriptionText != null) descriptionText.text = "Tính năng đang được hoàn thiện.";
            placeholderPanel.SetAsLastSibling();
            placeholderPanel.gameObject.SetActive(true);
            SetInputMode(GameInputMode.Menu);
        }

        void HideFeaturePlaceholder() {
            if (placeholderPanel != null) placeholderPanel.gameObject.SetActive(false);
        }

        void HandlePlaceholderBack() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "placeholder-back")) return;
            UiReturnTarget returnTarget = placeholderReturnTarget;
            HideFeaturePlaceholder();

            if (returnTarget == UiReturnTarget.Gameplay) CloseMenu();
            else RefreshInputModeFromOpenUi();
        }

        bool IsInventoryOpen() {
            return inventory != null && inventory.IsOpen;
        }

        bool IsMapOpen() {
            return mapInput != null && mapInput.IsOpen;
        }

        bool IsProfileOpen() {
            return profile != null && profile.IsOpen;
        }

        void CloseInventory() {
            if (inventory != null && inventory.IsOpen) inventory.Close();
        }

        void CloseMap() {
            if (mapInput != null && mapInput.IsOpen) mapInput.CloseMap();
        }

        void SubscribeMapBack() {
            WorldMapController worldMap = mapSystem != null ? mapSystem.WorldMap : null;
            if (worldMap == null) worldMap = FindFirstObjectByType<WorldMapController>();
            if (subscribedWorldMap == worldMap) return;

            UnsubscribeMapBack();
            subscribedWorldMap = worldMap;
            if (subscribedWorldMap != null) subscribedWorldMap.BackRequested += HandleMapBack;
        }

        void UnsubscribeMapBack() {
            if (subscribedWorldMap != null) subscribedWorldMap.BackRequested -= HandleMapBack;
            subscribedWorldMap = null;
        }

        void HandleMapBack() {
            UiReturnTarget returnTarget = mapReturnTarget;
            CloseMap();

            if (returnTarget == UiReturnTarget.Menu) OpenMenu();
            else RefreshInputModeFromOpenUi();
        }

        void SubscribeProfile() {
            if (profile == null) return;
            profile.BackRequested -= HandleProfileBack;
            profile.BackRequested += HandleProfileBack;
        }

        void UnsubscribeProfile() {
            if (profile == null) return;
            profile.BackRequested -= HandleProfileBack;
        }

        void HandleProfileBack() {
            CloseProfile(false);
            if (profileReturnTarget == UiReturnTarget.Menu) OpenMenu();
            else {
                controlLock?.UnlockControls(this);
                SetRouterGameplayBlock(false);
                RestoreCursorState();
                RefreshInputModeFromOpenUi();
            }
            profileReturnTarget = UiReturnTarget.Gameplay;
        }

        void CloseProfile(bool restoreGameplay) {
            if (profile != null && profile.IsOpen) profile.Close();
            if (!restoreGameplay) return;
            if (IsMenuOpen || IsInventoryOpen() || IsMapOpen() || dialogueOwners.Count > 0) return;

            controlLock?.UnlockControls(this);
            SetRouterGameplayBlock(false);
            RestoreCursorState();
            SetInputMode(GameInputMode.Gameplay);
        }

        void SetRouterGameplayBlock(bool blocked) {
            InventoryInputController.SetExternalGameplayInputBlocked(this, blocked);
        }

        void SaveCursorState() {
            if (cursorStateSaved) return;

            previousCursorVisible = Cursor.visible;
            previousLockState = Cursor.lockState;
            cursorStateSaved = true;
        }

        void RestoreCursorState() {
            if (!cursorStateSaved) return;

            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousLockState;
            cursorStateSaved = false;
        }

        void RefreshTrainerSummary() {
            if (!IsMenuOpen) return;
            if (profileProvider == null) ResolveProfileProvider();
            if (petRoster == null) petRoster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);

            PlayerProfileSnapshot snapshot = profileProvider != null
                ? profileProvider.GetSnapshot()
                : default;
            if (trainerNameText != null) {
                trainerNameText.text = string.IsNullOrWhiteSpace(snapshot.DisplayName)
                    ? "Người huấn luyện"
                    : snapshot.DisplayName;
            }
            if (trainerLevelText != null) {
                trainerLevelText.text = snapshot.Level > 0 ? "Lv. " + snapshot.Level : "Lv. -";
            }

            float experience01 = snapshot.RequiredExperience > 0
                ? Mathf.Clamp01((float)snapshot.CurrentExperience / snapshot.RequiredExperience)
                : 0f;
            if (trainerExperienceFill != null) trainerExperienceFill.fillAmount = experience01;
            if (trainerExperienceValueText != null) {
                trainerExperienceValueText.text = snapshot.RequiredExperience > 0
                    ? snapshot.CurrentExperience.ToString("N0") + " / " + snapshot.RequiredExperience.ToString("N0")
                    : "- / -";
            }

            if (trainerAvatarImage != null) {
                trainerAvatarImage.sprite = snapshot.Avatar;
                trainerAvatarImage.color = snapshot.Avatar != null ? Color.white : PaleGreen;
            }
            if (trainerAvatarPlaceholder != null) trainerAvatarPlaceholder.gameObject.SetActive(snapshot.Avatar == null);

            if (trainerAreaValueText != null) {
                trainerAreaValueText.text = string.IsNullOrWhiteSpace(snapshot.CurrentArea) ? "Chưa rõ khu vực" : snapshot.CurrentArea;
            }
            if (trainerPlayTimeValueText != null) trainerPlayTimeValueText.text = FormatCompactPlayTime(snapshot.PlayTimeSeconds);
            if (trainerPartyValueText != null) trainerPartyValueText.text = (petRoster != null ? petRoster.PartyCount : 0) + " / 6";
        }

        static string FormatCompactPlayTime(double seconds) {
            TimeSpan duration = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            int hours = Mathf.Max(0, Mathf.FloorToInt((float)duration.TotalHours));
            return hours.ToString("00") + ":" + duration.Minutes.ToString("00") + ":" + duration.Seconds.ToString("00");
        }

        void AddButtonHint(Button button, string hint) {
            if (button == null) return;
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => SetMenuDescription(hint));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => SetMenuDescription("Chọn một mục trong menu."));
            trigger.triggers.Add(exit);
        }

        static void AddHoverVisibility(Button button, GameObject target) {
            if (button == null || target == null) return;
            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => target.SetActive(true));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => target.SetActive(false));
            trigger.triggers.Add(exit);
        }

        void SetMenuDescription(string value) {
            if (descriptionText != null) descriptionText.text = value;
        }

        static string GetMenuIcon(string title) {
            switch (title) {
                case "Profile": return "HS";
                case "Pets": return "THÚ";
                case "Inventory": return "TÚI";
                case "Quest": return "NV";
                case "Settings": return "CĐ";
                case "Map": return "BĐ";
                case "Codex": return "CN";
                default: return title.Substring(0, 1);
            }
        }

        static void ConfigureCanvas(Canvas canvas) {
            if (canvas == null) return;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        static RectTransform EnsureRoot(Transform canvasTransform) {
            Transform existing = canvasTransform.Find(RootName);
            GameObject rootObject = existing != null
                ? existing.gameObject
                : new GameObject(RootName, typeof(RectTransform));

            rootObject.transform.SetParent(canvasTransform, false);
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            Stretch(rect);
            rect.SetAsLastSibling();
            return rect;
        }

        static RectTransform CreatePanel(Transform parent, string name, Color color, bool raycastTarget) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = SolidSprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return obj.GetComponent<RectTransform>();
        }

        static RectTransform CreateRoundedPanel(Transform parent, string name, Color color, bool raycastTarget) {
            RectTransform rect = CreatePanel(parent, name, color, raycastTarget);
            Image image = rect.GetComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            return rect;
        }

        static Button CreateButton(Transform parent, string name, string label, bool raycastTarget) {
            RectTransform rect = CreatePanel(parent, name, new Color(0.04f, 0.13f, 0.16f, 0.95f), raycastTarget);
            Button button = rect.gameObject.AddComponent<Button>();
            SetButtonColors(button, new Color(0.04f, 0.13f, 0.16f, 0.95f));

            if (!string.IsNullOrWhiteSpace(label)) {
                Text text = CreateText(rect, "Text", label, 16, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform);
            }

            return button;
        }

        static Button CreateTrainerButton(Transform parent, string name, Color baseColor) {
            RectTransform rect = CreateRoundedPanel(parent, name, baseColor, true);
            Button button = rect.gameObject.AddComponent<Button>();
            Image image = rect.GetComponent<Image>();
            button.targetGraphic = image;

            bool dark = baseColor.grayscale < 0.45f;
            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = dark
                ? new Color(0.12f, 0.39f, 0.18f, baseColor.a)
                : PaleGreen;
            colors.pressedColor = dark
                ? new Color(0.05f, 0.22f, 0.10f, baseColor.a)
                : new Color(0.80f, 0.89f, 0.75f, baseColor.a);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.82f, 0.84f, 0.78f, 0.75f);
            colors.fadeDuration = 0.09f;
            button.colors = colors;
            return button;
        }

        static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, Color color, TextAnchor alignment) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text label = obj.GetComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        static void SetButtonColors(Button button, Color baseColor) {
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = baseColor;

            ColorBlock colors = button.colors;
            colors.normalColor = baseColor;
            colors.highlightedColor = baseColor + new Color(0.06f, 0.08f, 0.07f, 0f);
            colors.pressedColor = baseColor * 0.82f;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.03f, 0.07f, 0.08f, 0.54f);
            button.colors = colors;
        }

        static void AddOutline(GameObject target, Color color, float distance) {
            if (target == null) return;
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        static void ClearChildren(Transform parent) {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) DestroySafe(parent.GetChild(i).gameObject);
        }

        static void DestroySafe(GameObject obj) {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        static void Stretch(RectTransform rect) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetTopRight(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetTopStretch(RectTransform rect, float left, float top, float right, float height) {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        static void SetBottomStretch(RectTransform rect, float left, float bottom, float right, float height) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
            rect.localScale = Vector3.one;
        }

        static void SetInset(RectTransform rect, float inset) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        static void SetFullscreenFeaturePanel(RectTransform rect, float left, float top, float right, float bottom) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            rect.localScale = Vector3.one;
        }

        static void SetBottomRight(RectTransform rect, float right, float bottom, float width, float height) {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, bottom);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static bool Pressed(KeyCode key) {
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        static Sprite SolidSprite {
            get {
                if (solidSprite != null) return solidSprite;

                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                solidSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                solidSprite.hideFlags = HideFlags.HideAndDontSave;
                return solidSprite;
            }
        }

        static Sprite RoundedSprite {
            get {
                if (roundedSprite != null) return roundedSprite;
                roundedSprite = CreateRoundedSprite();
                return roundedSprite;
            }
        }

        static Sprite CreateRoundedSprite() {
            const int size = 64;
            const float radius = 12f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "GameMenuRounded",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1f) * 0.5f;
            float straight = center - radius;
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = Mathf.Max(Mathf.Abs(x - center) - straight, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - center) - straight, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect,
                new Vector4(14f, 14f, 14f, 14f));
            sprite.name = "GameMenuRounded";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Font DefaultFont => MenuTypography.Font;
    }
}
