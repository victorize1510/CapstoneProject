using System.Collections.Generic;
using System.Linq;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.ProfileSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    public enum GameInputMode {
        Gameplay,
        Inventory,
        Menu,
        WorldMap,
        Dialogue
    }

    [DisallowMultipleComponent]
    public sealed class GameMenuController : MonoBehaviour {
        const string RootName = "GameMenuRoot";

        static Sprite solidSprite;
        static Font cachedFont;

        [Header("References")]
        [SerializeField] Canvas targetCanvas = null;
        [SerializeField] MonsterInventoryController inventory = null;
        [SerializeField] InventoryInputController inventoryInput = null;
        [SerializeField] MapInputController mapInput = null;
        [SerializeField] MapSystemController mapSystem = null;
        [SerializeField] ProfilePanelController profile = null;
        [SerializeField] LocalPlayerControlLock controlLock = null;

        [Header("Keys")]
        [SerializeField] KeyCode menuKey = KeyCode.Tab;
        [SerializeField] KeyCode inventoryKey = KeyCode.I;
        [SerializeField] KeyCode mapKey = KeyCode.M;
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
        bool cursorStateSaved;
        bool previousCursorVisible;
        CursorLockMode previousLockState;
        readonly HashSet<object> dialogueOwners = new HashSet<object>();
        InventoryInputController ownedInventoryInput;
        MapInputController ownedMapInput;
        ProfilePanelController configuredProfile;
        Canvas configuredProfileCanvas;
        float nextReferenceResolveTime;

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
            SubscribeProfile();
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
            UnsubscribeProfile();
            CloseProfile(false);
            CloseMenu();
            SetRouterGameplayBlock(false);
            ReleaseInputOwnership();
        }

        void Update() {
            ResolveRuntimeReferences(false);
            RefreshInputModeFromOpenUi();

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

            if (Pressed(closeKey)) {
                CloseTopUi();
            }
        }

        [ContextMenu("Rebuild Game Menu")]
        public void RebuildMenu() {
            ResolveReferences();
            EnsureCanvas();

            root = EnsureRoot(targetCanvas.transform);
            ClearChildren(root);

            BuildDimmer(root);
            panel = CreatePanel(root, "MenuPanel", new Color(0.025f, 0.08f, 0.10f, 0.94f), true);
            SetRightPanel(panel, 48f, 54f, 620f, 890f);

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
                CloseMap();
                RefreshInputModeFromOpenUi();
                return;
            }

            OpenMapDirect();
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

        public void OpenMapDirect() {
            ResolveReferences();

            if (mapInput != null && mapInput.IsOpen) {
                CloseMap();
                return;
            }

            CloseProfile(false);
            CloseMenu();
            CloseInventory();

            if (mapInput == null) {
                OpenFeaturePlaceholder("Map");
                return;
            }

            mapInput.OpenMap();
            SetInputMode(GameInputMode.WorldMap);
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
                HideFeaturePlaceholder();
                RefreshInputModeFromOpenUi();
                return;
            }

            switch (CurrentMode) {
                case GameInputMode.Dialogue:
                    break;
                case GameInputMode.WorldMap:
                    CloseMap();
                    break;
                case GameInputMode.Inventory:
                    if (inventory == null || !inventory.TryCloseTopmostPanel()) CloseInventory();
                    break;
                case GameInputMode.Menu:
                    CloseMenu();
                    break;
            }

            RefreshInputModeFromOpenUi();
        }

        void ResolveReferences() {
            EnsureCanvas();
            ResolveRuntimeReferences(true);
        }

        void ResolveRuntimeReferences(bool force) {
            bool needsReferences = inventory == null
                || inventoryInput == null
                || mapInput == null
                || mapSystem == null
                || profile == null
                || controlLock == null;
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

            if (inventoryInput == null && inventory != null) inventoryInput = inventory.GetComponent<InventoryInputController>();
            if (profile != null && (configuredProfile != profile || configuredProfileCanvas != targetCanvas)) {
                profile.Configure(targetCanvas);
                configuredProfile = profile;
                configuredProfileCanvas = targetCanvas;
                SubscribeProfile();
            }

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
            RectTransform dimmer = CreatePanel(parent, "Dimmer", new Color(0f, 0f, 0f, 0.44f), true);
            Stretch(dimmer);
        }

        void BuildHeader(RectTransform parent) {
            RectTransform header = CreatePanel(parent, "Header", new Color(0.03f, 0.11f, 0.14f, 0.96f), true);
            SetTopStretch(header, 24f, 22f, 24f, 132f);

            RectTransform avatar = CreatePanel(header, "Avatar", new Color(0.14f, 0.36f, 0.40f, 1f), true);
            SetTopLeft(avatar, 24f, 22f, 88f, 88f);

            Text title = CreateText(header, "Title", "Trainer Menu", 30, FontStyle.Bold, new Color(0.96f, 0.89f, 0.70f, 1f), TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, 132f, 24f, 310f, 42f);

            Text subtitle = CreateText(header, "Subtitle", "TAB close  |  I inventory  |  M map", 15, FontStyle.Normal, new Color(0.73f, 0.84f, 0.82f, 1f), TextAnchor.MiddleLeft);
            SetTopLeft(subtitle.rectTransform, 134f, 74f, 390f, 26f);

            Button close = CreateButton(header, "CloseButton", "X", true);
            SetTopRight(close.GetComponent<RectTransform>(), 20f, 22f, 42f, 42f);
            close.onClick.AddListener(CloseMenu);
        }

        void BuildMenuGrid(RectTransform parent) {
            RectTransform grid = CreatePanel(parent, "MenuGrid", new Color(0.02f, 0.07f, 0.09f, 0.7f), true);
            SetTopStretch(grid, 24f, 178f, 24f, 518f);

            GridLayoutGroup layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 22, 22);
            layout.spacing = new Vector2(22f, 22f);
            layout.cellSize = new Vector2(160f, 138f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;

            AddMenuButton(grid, "Profile", profile != null ? "Open player profile" : "Profile panel\nMissing", true, OpenProfileDirect);
            AddMenuButton(grid, "Pets", inventory != null ? "Open pet party" : "Pet panel\nMissing", true, OpenPetsDirect);
            AddMenuButton(grid, "Inventory", inventory != null ? "Open bag" : "Inventory panel\nMissing", true, OpenInventoryDirect);
            AddMenuButton(grid, "Quest", inventory != null ? "Open quest journal" : "Quest panel\nMissing", true, OpenQuestDirect);
            AddMenuButton(grid, "Settings", "Options\nSoon", true, () => OpenFeaturePlaceholder("Settings"));
            AddMenuButton(grid, "Map", mapInput != null ? "Open world map" : "Map panel\nMissing", true, OpenMapDirect);
            AddMenuButton(grid, "Codex", "Creature book\nSoon", true, () => OpenFeaturePlaceholder("Codex"));
            AddMenuButton(grid, "Store", "Shop\nSoon", true, () => OpenFeaturePlaceholder("Store"));
            AddMenuButton(grid, "Box", inventory != null ? "Manage party and pet storage" : "Box panel\nMissing", true, OpenBoxDirect);
        }

        void AddMenuButton(RectTransform parent, string title, string description, bool enabled, UnityEngine.Events.UnityAction action) {
            Button button = CreateButton(parent, title + "Button", string.Empty, true);
            button.interactable = enabled;
            if (enabled && action != null) button.onClick.AddListener(action);

            Text icon = CreateText(button.transform, "Icon", GetMenuIcon(title), 34, FontStyle.Bold, enabled ? new Color(0.88f, 0.72f, 0.48f, 1f) : new Color(0.58f, 0.58f, 0.52f, 0.75f), TextAnchor.MiddleCenter);
            SetTopStretch(icon.rectTransform, 8f, 18f, 8f, 42f);

            Text label = CreateText(button.transform, "Label", title, 18, FontStyle.Bold, enabled ? Color.white : new Color(0.75f, 0.75f, 0.69f, 0.8f), TextAnchor.MiddleCenter);
            SetTopStretch(label.rectTransform, 10f, 64f, 10f, 28f);

            Text sub = CreateText(button.transform, "Description", enabled ? description : description + "\nSoon", 12, FontStyle.Normal, enabled ? new Color(0.70f, 0.83f, 0.80f, 0.95f) : new Color(0.60f, 0.65f, 0.62f, 0.7f), TextAnchor.UpperCenter);
            SetTopStretch(sub.rectTransform, 10f, 96f, 10f, 38f);
        }

        void BuildFooter(RectTransform parent) {
            RectTransform footer = CreatePanel(parent, "Footer", new Color(0.03f, 0.10f, 0.12f, 0.84f), true);
            SetBottomStretch(footer, 24f, 22f, 24f, 92f);

            descriptionText = CreateText(footer, "Description", "Select a menu item.", 15, FontStyle.Normal, new Color(0.88f, 0.84f, 0.72f, 1f), TextAnchor.MiddleLeft);
            SetTopStretch(descriptionText.rectTransform, 18f, 14f, 144f, 60f);

            Button back = CreateButton(footer, "BackButton", "Back", true);
            SetTopRight(back.GetComponent<RectTransform>(), 18f, 22f, 106f, 46f);
            back.onClick.AddListener(CloseMenu);
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
            close.onClick.AddListener(HideFeaturePlaceholder);

            placeholderBody = CreateText(placeholderPanel, "Body", string.Empty, 22, FontStyle.Normal, new Color(0.20f, 0.18f, 0.13f, 1f), TextAnchor.MiddleCenter);
            SetTopStretch(placeholderBody.rectTransform, 72f, 130f, 72f, 260f);

            Button back = CreateButton(placeholderPanel, "BackButton", "Back", true);
            SetBottomRight(back.GetComponent<RectTransform>(), 34f, 30f, 132f, 50f);
            back.onClick.AddListener(HideFeaturePlaceholder);

            placeholderPanel.gameObject.SetActive(false);
        }

        void OpenFeaturePlaceholder(string featureName) {
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

        static string GetMenuIcon(string title) {
            switch (title) {
                case "Profile": return "P";
                case "Pets": return "Pet";
                case "Inventory": return "Bag";
                case "Quest": return "Q";
                case "Settings": return "Cfg";
                case "Map": return "Map";
                case "Codex": return "Book";
                case "Store": return "Shop";
                case "Box": return "Box";
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
