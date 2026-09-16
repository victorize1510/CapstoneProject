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

        static readonly Color Paper = new Color(0.995f, 0.995f, 0.975f, 1f);
        static readonly Color Forest = new Color(0.075f, 0.28f, 0.13f, 1f);
        static readonly Color AccentGreen = new Color(0.25f, 0.55f, 0.24f, 1f);
        static readonly Color PaleGreen = new Color(0.88f, 0.94f, 0.83f, 1f);
        static readonly Color BorderGreen = new Color(0.35f, 0.53f, 0.25f, 0.95f);
        static readonly Color MutedGreen = new Color(0.30f, 0.39f, 0.30f, 1f);

        static Sprite solidSprite;
        static Sprite roundedSprite;
        static Font cachedFont;

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
        float nextReferenceResolveTime;
        float nextTrainerSummaryRefreshTime;

        const float ReferenceRetryInterval = 1f;

        public bool IsMenuOpen => root != null && root.gameObject.activeSelf;
        public GameInputMode CurrentMode { get; private set; } = GameInputMode.Gameplay;
        public bool IsUiOpen => CurrentMode != GameInputMode.Gameplay;

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
            UnsubscribeProfileProvider();
            UnsubscribeProfile();
            UnsubscribeMapBack();
            CloseProfile(false);
            CloseMenu();
            SetRouterGameplayBlock(false);
            ReleaseInputOwnership();
        }

        void Update() {
            ResolveRuntimeReferences(false);
            RefreshInputModeFromOpenUi();

            if (IsMenuOpen && Time.unscaledTime >= nextTrainerSummaryRefreshTime) {
                nextTrainerSummaryRefreshTime = Time.unscaledTime + 0.5f;
                RefreshTrainerSummary();
            }

            if (CurrentMode != GameInputMode.Gameplay) {
                if (Pressed(closeKey)) {
                    CloseTopUi();
                    return;
                }

                if (CurrentMode == GameInputMode.Menu && Pressed(menuKey)) {
                    ToggleMenuFromInput();
                }
                return;
            }

            if (Pressed(closeKey)) {
                if (dialogueOwners.Count == 0) OpenSettingsFromGameplay();
                return;
            }

            if (Pressed(menuKey)) {
                ToggleMenuFromInput();
                return;
            }

            if (Pressed(inventoryKey)) {
                ToggleInventoryFromInput();
                return;
            }

            if (Pressed(mapKey)) {
                ToggleMapFromInput();
                return;
            }

            if (CanOpenDirectPanelShortcut() && Pressed(questKey)) {
                OpenQuestDirect();
                return;
            }

            if (CanOpenDirectPanelShortcut() && Pressed(petsKey)) {
                OpenPetsDirect();
            }
        }

        bool CanOpenDirectPanelShortcut() {
            return CurrentMode == GameInputMode.Gameplay || CurrentMode == GameInputMode.Menu;
        }

        [ContextMenu("Rebuild Game Menu")]
        public void RebuildMenu() {
            ResolveReferences();
            EnsureCanvas();

            root = EnsureRoot(targetCanvas.transform);
            ClearChildren(root);

            BuildDimmer(root);
            panel = CreateRoundedPanel(root, "TrainerMenuPanel", Paper, true);
            SetRightPanel(panel, 0f, 0f, 620f, 1080f);
            AddOutline(panel.gameObject, BorderGreen, 2f);

            BuildHeader(panel);
            BuildMenuGrid(panel);
            BuildFooter(panel);
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
        }

        public void CloseMenu() {
            if (root != null) root.gameObject.SetActive(false);
            HideFeaturePlaceholder();

            if (!IsInventoryOpen() && !IsMapOpen() && !IsProfileOpen() && dialogueOwners.Count == 0) {
                controlLock?.UnlockControls(this);
                SetRouterGameplayBlock(false);
                RestoreCursorState();
            }

            RefreshInputModeFromOpenUi();
        }

        public void ToggleMenu() {
            if (IsMenuOpen) CloseMenu();
            else OpenMenu();
        }

        void ToggleMenuFromInput() {
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

        void ToggleInventoryFromInput() {
            if (IsInventoryOpen()) {
                CloseInventory();
                RefreshInputModeFromOpenUi();
                return;
            }

            OpenInventoryDirect();
        }

        void ToggleMapFromInput() {
            if (IsMapOpen()) {
                HandleMapBack();
                return;
            }

            OpenMap(UiReturnTarget.Gameplay);
        }

        public void OpenInventoryDirect() {
            ResolveReferences();

            if (IsInventoryOpen()) {
                CloseInventory();
                RefreshInputModeFromOpenUi();
                return;
            }

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

        public void OpenQuestDirect() {
            ResolveReferences();
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
            ResolveReferences();
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
            ResolveReferences();
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

        public void OpenShopFromWorld() {
            ResolveReferences();
            CloseProfile(false);
            CloseInventory();
            CloseMap();
            OpenFeaturePlaceholder("Shop", UiReturnTarget.Gameplay);
        }

        public void OpenMapDirect() {
            OpenMap(IsMenuOpen ? UiReturnTarget.Menu : UiReturnTarget.Gameplay);
        }

        void OpenMap(UiReturnTarget returnTarget) {
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
            OpenFeaturePlaceholder("Settings", UiReturnTarget.Gameplay);
        }

        public void OpenProfileDirect() {
            ResolveReferences();

            if (profile == null) {
                OpenFeaturePlaceholder("Profile");
                return;
            }

            CloseInventory();
            CloseMap();
            HideFeaturePlaceholder();
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
            RefreshInputModeFromOpenUi();

            if (IsProfileOpen()) {
                CloseProfile(true);
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
                    HandleMapBack();
                    return;
                case GameInputMode.Inventory:
                    if (inventory == null || !inventory.TryCloseTopmostPanel()) CloseInventory();
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
                if (ownedInventoryInput != null) ownedInventoryInput.SetOpenCloseHotkeysEnabled(true);
                ownedInventoryInput = inventoryInput;
                if (ownedInventoryInput != null) ownedInventoryInput.SetOpenCloseHotkeysEnabled(false);
            }

            if (ownedMapInput != mapInput) {
                if (ownedMapInput != null) ownedMapInput.SetOpenCloseHotkeysEnabled(true);
                ownedMapInput = mapInput;
                if (ownedMapInput != null) ownedMapInput.SetOpenCloseHotkeysEnabled(false);
            }
        }

        void ReleaseInputOwnership() {
            if (ownedInventoryInput != null) ownedInventoryInput.SetOpenCloseHotkeysEnabled(true);
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
            CurrentMode = mode;
            mapSystem?.SetMinimapVisible(mode == GameInputMode.Gameplay);
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
            trainerAvatarPlaceholder = CreateText(avatarFrame, "AvatarPlaceholder", "PLAYER", 15, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(trainerAvatarPlaceholder.rectTransform);

            Text title = CreateText(summary, "Title", "TRAINER MENU", 30, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 138f, 10f, 420f, 44f);

            trainerNameText = CreateText(summary, "PlayerName", "Trainer", 22, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(trainerNameText.rectTransform, 140f, 50f, 410f, 34f);

            RectTransform levelBadge = CreateRoundedPanel(summary, "LevelBadge", Forest, false);
            SetTopLeft(levelBadge, 138f, 88f, 88f, 40f);
            AddOutline(levelBadge.gameObject, AccentGreen, 1f);
            trainerLevelText = CreateText(levelBadge, "Level", "Lv. -", 17, FontStyle.Bold, Paper, TextAnchor.MiddleCenter);
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

            trainerAreaValueText = BuildQuickInfo(summary, "Area", "A", "Current Area", 16f);
            trainerPlayTimeValueText = BuildQuickInfo(summary, "PlayTime", "T", "Play Time", 208f);
            trainerPartyValueText = BuildQuickInfo(summary, "Party", "PET", "Party Pets", 400f);

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

            AddMenuButton(grid, "Profile", "Player overview", true, OpenProfileDirect);
            AddMenuButton(grid, "Pets", "Manage party pets", true, OpenPetsDirect);
            AddMenuButton(grid, "Inventory", "Open bag", true, OpenInventoryDirect);
            AddMenuButton(grid, "Quest", "Quest journal", true, OpenQuestDirect);
            AddMenuButton(grid, "Map", "Open world map", true, OpenMapDirect);
            AddMenuButton(grid, "Codex", "Creature records", true, () => OpenFeaturePlaceholder("Codex"));
        }

        void AddMenuButton(RectTransform parent, string title, string description, bool enabled, UnityEngine.Events.UnityAction action) {
            Button button = CreateTrainerButton(parent, title + "Button", Paper);
            button.interactable = enabled;
            if (enabled && action != null) button.onClick.AddListener(action);
            AddOutline(button.gameObject, new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.82f), 1f);

            RectTransform iconBox = CreateRoundedPanel(button.transform, "Icon", PaleGreen, false);
            SetTopLeft(iconBox, 16f, 60f, 78f, 84f);
            AddOutline(iconBox.gameObject, new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.55f), 1f);
            Text icon = CreateText(iconBox, "Label", GetMenuIcon(title), GetMenuIcon(title).Length > 3 ? 12 : 16, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(icon.rectTransform);

            Text label = CreateText(button.transform, "Label", title, 33, FontStyle.Bold, enabled ? Forest : MutedGreen, TextAnchor.MiddleLeft);
            SetTopLeft(label.rectTransform, 108f, 40f, 158f, 54f);

            Text sub = CreateText(button.transform, "Description", description, 14, FontStyle.Normal, MutedGreen, TextAnchor.UpperLeft);
            SetTopLeft(sub.rectTransform, 108f, 108f, 158f, 52f);

            AddButtonHint(button, title + " — " + description);
        }

        void BuildFooter(RectTransform parent) {
            RectTransform footer = CreatePanel(parent, "Footer", Color.clear, false);
            SetBottomStretch(footer, 18f, 12f, 18f, 86f);

            RectTransform divider = CreatePanel(footer, "FooterDivider", new Color(BorderGreen.r, BorderGreen.g, BorderGreen.b, 0.5f), false);
            SetTopStretch(divider, 6f, 0f, 6f, 1f);

            descriptionText = CreateText(footer, "Description", "Select a menu item.", 16, FontStyle.Normal, MutedGreen, TextAnchor.MiddleCenter);
            SetTopStretch(descriptionText.rectTransform, 102f, 20f, 176f, 52f);

            Button settings = CreateTrainerButton(footer, "SettingsButton", Paper);
            SetTopLeft(settings.GetComponent<RectTransform>(), 6f, 20f, 52f, 52f);
            AddOutline(settings.gameObject, BorderGreen, 1f);
            Text settingsIcon = CreateText(settings.transform, "Icon", "CFG", 13, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
            Stretch(settingsIcon.rectTransform);
            settings.onClick.AddListener(() => OpenFeaturePlaceholder("Settings", UiReturnTarget.Menu));
            AddButtonHint(settings, "Settings");

            RectTransform settingsTooltip = CreateRoundedPanel(footer, "SettingsTooltip", Forest, false);
            SetTopLeft(settingsTooltip, 64f, 29f, 92f, 32f);
            Text settingsTooltipText = CreateText(settingsTooltip, "Label", "Settings", 14, FontStyle.Bold, Paper, TextAnchor.MiddleCenter);
            Stretch(settingsTooltipText.rectTransform);
            settingsTooltip.gameObject.SetActive(false);
            AddHoverVisibility(settings, settingsTooltip.gameObject);

            Button back = CreateTrainerButton(footer, "BackButton", Forest);
            SetTopRight(back.GetComponent<RectTransform>(), 6f, 20f, 150f, 52f);
            AddOutline(back.gameObject, AccentGreen, 1f);
            Text backLabel = CreateText(back.transform, "Label", "<   Back", 19, FontStyle.Bold, Paper, TextAnchor.MiddleCenter);
            Stretch(backLabel.rectTransform);
            back.onClick.AddListener(CloseMenu);
            AddButtonHint(back, "Back to gameplay");
        }

        void BuildFeaturePlaceholder(RectTransform parent) {
            placeholderPanel = CreatePanel(parent, "FeaturePlaceholderPanel", new Color(0.94f, 0.90f, 0.78f, 0.98f), true);
            SetFullscreenFeaturePanel(placeholderPanel, 36f, 34f, 36f, 34f);

            RectTransform header = CreatePanel(placeholderPanel, "Header", new Color(0.82f, 0.73f, 0.54f, 1f), true);
            SetTopStretch(header, 0f, 0f, 0f, 82f);

            placeholderTitle = CreateText(header, "Title", "Feature", 38, FontStyle.Bold, new Color(0.13f, 0.12f, 0.08f, 1f), TextAnchor.MiddleLeft);
            SetTopStretch(placeholderTitle.rectTransform, 34f, 16f, 96f, 52f);

            Button close = CreateButton(header, "CloseButton", "X", true);
            SetTopRight(close.GetComponent<RectTransform>(), 24f, 18f, 46f, 46f);
            close.onClick.AddListener(HandlePlaceholderBack);

            placeholderBody = CreateText(placeholderPanel, "Body", string.Empty, 22, FontStyle.Normal, new Color(0.20f, 0.18f, 0.13f, 1f), TextAnchor.MiddleCenter);
            SetTopStretch(placeholderBody.rectTransform, 72f, 130f, 72f, 260f);

            Button back = CreateButton(placeholderPanel, "BackButton", "Back", true);
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

            string title = string.IsNullOrWhiteSpace(featureName) ? "Feature" : featureName;
            if (placeholderTitle != null) placeholderTitle.text = title;
            if (placeholderBody != null) {
                placeholderBody.text = title + "\n\nPanel nay chua co backend that. Khi co system rieng, minh chi can noi nut menu nay sang controller cua system do.";
            }

            if (descriptionText != null) descriptionText.text = title + " is a placeholder panel.";
            placeholderPanel.SetAsLastSibling();
            placeholderPanel.gameObject.SetActive(true);
            SetInputMode(GameInputMode.Menu);
        }

        void HideFeaturePlaceholder() {
            if (placeholderPanel != null) placeholderPanel.gameObject.SetActive(false);
        }

        void HandlePlaceholderBack() {
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
            OpenMenu();
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
            if (profileProvider == null) ResolveProfileProvider();
            if (petRoster == null) petRoster = FindFirstObjectByType<PetBoxRuntimeProvider>(FindObjectsInactive.Include);

            PlayerProfileSnapshot snapshot = profileProvider != null
                ? profileProvider.GetSnapshot()
                : default;
            if (trainerNameText != null) {
                trainerNameText.text = string.IsNullOrWhiteSpace(snapshot.DisplayName)
                    ? "Trainer"
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
                trainerAreaValueText.text = string.IsNullOrWhiteSpace(snapshot.CurrentArea) ? "Unknown Area" : snapshot.CurrentArea;
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
            exit.callback.AddListener(_ => SetMenuDescription("Select a menu item."));
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
                case "Profile": return "YOU";
                case "Pets": return "PAW";
                case "Inventory": return "BAG";
                case "Quest": return "LOG";
                case "Settings": return "CFG";
                case "Map": return "MAP";
                case "Codex": return "BOOK";
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

        static void SetRightPanel(RectTransform rect, float right, float top, float width, float height) {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-right, -top);
            rect.sizeDelta = new Vector2(width, height);
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

        static Font DefaultFont {
            get {
                if (cachedFont != null) return cachedFont;

                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return cachedFont;
            }
        }
    }
}
