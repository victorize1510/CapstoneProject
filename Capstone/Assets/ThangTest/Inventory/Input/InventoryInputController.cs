using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace Capstone.Game.Inventory {
    [DisallowMultipleComponent]
    public sealed class InventoryInputController : MonoBehaviour {
        [Header("References")]
        [SerializeField] MonsterInventoryController inventory = null;
        [SerializeField] LocalPlayerControlLock playerControlLock = null;
        [SerializeField] UIDocument document = null;

        [Header("Keys")]
        [SerializeField] KeyCode primaryToggleKey = KeyCode.I;
        [SerializeField] KeyCode secondaryToggleKey = KeyCode.Tab;
        [SerializeField] KeyCode closeKey = KeyCode.Escape;
        [SerializeField] KeyCode previousCategoryKey = KeyCode.Q;
        [SerializeField] KeyCode nextCategoryKey = KeyCode.E;
        [SerializeField] KeyCode confirmKey = KeyCode.Return;
        [SerializeField] KeyCode alternateConfirmKey = KeyCode.KeypadEnter;

        [Header("Startup")]
        [SerializeField] bool closeOnStart = true;

        [Header("Cursor")]
        [SerializeField] bool suppressHoverOnKeyboardItemNavigation = true;
        [SerializeField] float mouseMoveThreshold = 2f;

        CursorLockMode previousLockState;
        bool previousCursorVisible;
        bool cursorStateSaved;
        bool keyboardNavigationMode;
        Vector3 lastMousePosition;
        VisualElement registeredRoot;
        int handledFrame = -1;

        public static bool GameplayInputBlocked { get; private set; }

        void Awake() {
            ResolveReferences();
        }

        void OnEnable() {
            ResolveReferences();
            SubscribeInventory();
            RegisterUiKeys();
        }

        void Start() {
            if (closeOnStart && inventory != null) inventory.Close();
        }

        void OnDisable() {
            if (inventory != null) inventory.VisibilityChanged -= HandleInventoryVisibilityChanged;
            UnregisterUiKeys();
            RestoreGameplayState();
            GameplayInputBlocked = false;
        }

        void Update() {
            if (inventory != null && inventory.IsOpen) {
                UpdateMouseNavigationReveal();
            }

            if (handledFrame == Time.frameCount) return;

            if (Pressed(primaryToggleKey)) TryHandleKey(primaryToggleKey);
            else if (Pressed(secondaryToggleKey)) TryHandleKey(secondaryToggleKey);
            else if (Pressed(closeKey)) TryHandleKey(closeKey);
            else if (Pressed(previousCategoryKey)) TryHandleKey(previousCategoryKey);
            else if (Pressed(nextCategoryKey)) TryHandleKey(nextCategoryKey);
            else if (Pressed(KeyCode.W)) TryHandleKey(KeyCode.W);
            else if (Pressed(KeyCode.UpArrow)) TryHandleKey(KeyCode.UpArrow);
            else if (Pressed(KeyCode.S)) TryHandleKey(KeyCode.S);
            else if (Pressed(KeyCode.DownArrow)) TryHandleKey(KeyCode.DownArrow);
            else if (Pressed(confirmKey)) TryHandleKey(confirmKey);
            else if (Pressed(alternateConfirmKey)) TryHandleKey(alternateConfirmKey);
        }

        public void OpenInventory() {
            ResolveReferences();
            if (inventory == null) return;

            inventory.Open();
        }

        public void CloseInventory() {
            ResolveReferences();
            if (inventory == null) {
                RestoreGameplayState();
                return;
            }

            if (inventory.IsOpen) inventory.Close();
            else RestoreGameplayState();
        }

        public void ToggleInventory() {
            if (inventory != null && inventory.IsOpen) CloseInventory();
            else OpenInventory();
        }

        public void Bind(MonsterInventoryController inventoryController) {
            if (inventory == inventoryController) {
                ResolveReferences();
                RegisterUiKeys();
                return;
            }

            if (inventory != null) inventory.VisibilityChanged -= HandleInventoryVisibilityChanged;
            inventory = inventoryController;
            ResolveReferences();
            SubscribeInventory();
            RegisterUiKeys();
        }

        void ResolveReferences() {
            inventory = inventory != null ? inventory : GetComponent<MonsterInventoryController>();
            if (inventory == null) inventory = GetComponent<MonsterInventoryView>();
            if (inventory == null) inventory = GetComponentInChildren<MonsterInventoryController>(true);
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryController>();

            document = document != null ? document : GetComponent<UIDocument>();
            if (document == null && inventory != null) document = inventory.GetComponent<UIDocument>();
            if (document == null) document = GetComponentInChildren<UIDocument>(true);

            playerControlLock = playerControlLock != null ? playerControlLock : GetComponent<LocalPlayerControlLock>();
            if (playerControlLock == null) playerControlLock = GetComponentInParent<LocalPlayerControlLock>();
            if (playerControlLock == null && inventory != null) playerControlLock = inventory.GetComponent<LocalPlayerControlLock>();
            if (playerControlLock == null) playerControlLock = gameObject.AddComponent<LocalPlayerControlLock>();
        }

        void SubscribeInventory() {
            if (inventory == null) return;

            inventory.VisibilityChanged -= HandleInventoryVisibilityChanged;
            inventory.VisibilityChanged += HandleInventoryVisibilityChanged;
        }

        void RegisterUiKeys() {
            UnregisterUiKeys();
            if (document == null || document.rootVisualElement == null) return;

            registeredRoot = document.rootVisualElement;
            registeredRoot.RegisterCallback<KeyDownEvent>(HandleUiKeyDown, TrickleDown.TrickleDown);
        }

        void UnregisterUiKeys() {
            if (registeredRoot == null) return;

            registeredRoot.UnregisterCallback<KeyDownEvent>(HandleUiKeyDown, TrickleDown.TrickleDown);
            registeredRoot = null;
        }

        void HandleUiKeyDown(KeyDownEvent evt) {
            if (evt == null || handledFrame == Time.frameCount) return;
            if (!TryHandleKey(evt.keyCode)) return;

            evt.StopImmediatePropagation();
        }

        void HandleInventoryVisibilityChanged(bool isOpen) {
            if (isOpen) {
                SaveCursorState();
                playerControlLock?.LockControls();
                GameplayInputBlocked = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                keyboardNavigationMode = false;
                lastMousePosition = Input.mousePosition;
                SetKeyboardNavigationMode(false);
                FocusInventoryUi();
            } else {
                RestoreGameplayState();
            }
        }

        bool TryHandleKey(KeyCode key) {
            if (key == KeyCode.None) return false;

            if (key == primaryToggleKey || key == secondaryToggleKey) {
                ToggleInventory();
                MarkHandled();
                return true;
            }

            if (inventory == null || !inventory.IsOpen) return false;

            if (key == closeKey) {
                CloseInventory();
                MarkHandled();
                return true;
            }

            if (key == previousCategoryKey) {
                inventory.SelectPreviousCategory();
                MarkHandled();
                return true;
            }

            if (key == nextCategoryKey) {
                inventory.SelectNextCategory();
                MarkHandled();
                return true;
            }

            if (key == KeyCode.W || key == KeyCode.UpArrow) {
                inventory.SelectPreviousItem();
                EnterKeyboardItemNavigation();
                MarkHandled();
                return true;
            }

            if (key == KeyCode.S || key == KeyCode.DownArrow) {
                inventory.SelectNextItem();
                EnterKeyboardItemNavigation();
                MarkHandled();
                return true;
            }

            if (key == confirmKey || key == alternateConfirmKey) {
                inventory.ConfirmSelected();
                MarkHandled();
                return true;
            }

            return false;
        }

        void MarkHandled() {
            handledFrame = Time.frameCount;
        }

        void FocusInventoryUi() {
            if (document == null || document.rootVisualElement == null) return;

            var focusTarget = document.rootVisualElement.Q<VisualElement>("monster-inventory-root") ?? document.rootVisualElement;
            focusTarget.focusable = true;
            focusTarget.Focus();
        }

        void EnterKeyboardItemNavigation() {
            if (!suppressHoverOnKeyboardItemNavigation || inventory == null || !inventory.IsOpen) return;

            keyboardNavigationMode = true;
            lastMousePosition = Input.mousePosition;
            SetKeyboardNavigationMode(true);
        }

        void UpdateMouseNavigationReveal() {
            Vector3 mousePosition = Input.mousePosition;
            if (!keyboardNavigationMode) {
                lastMousePosition = mousePosition;
                return;
            }

            float threshold = Mathf.Max(0.1f, mouseMoveThreshold);
            if ((mousePosition - lastMousePosition).sqrMagnitude < threshold * threshold) return;

            keyboardNavigationMode = false;
            Cursor.visible = true;
            lastMousePosition = mousePosition;
            SetKeyboardNavigationMode(false);
        }

        void SetKeyboardNavigationMode(bool enabled) {
            if (document == null || document.rootVisualElement == null) return;

            var root = document.rootVisualElement.Q<VisualElement>("monster-inventory-root") ?? document.rootVisualElement;
            if (enabled) root.AddToClassList("keyboard-navigation");
            else root.RemoveFromClassList("keyboard-navigation");
        }

        void SaveCursorState() {
            if (cursorStateSaved) return;

            previousLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorStateSaved = true;
        }

        void RestoreGameplayState() {
            playerControlLock?.UnlockControls();
            GameplayInputBlocked = false;
            keyboardNavigationMode = false;
            SetKeyboardNavigationMode(false);

            if (!cursorStateSaved) return;

            Cursor.lockState = previousLockState;
            Cursor.visible = previousCursorVisible;
            cursorStateSaved = false;
        }

        static bool Pressed(KeyCode key) {
            return key != KeyCode.None && Input.GetKeyDown(key);
        }
    }
}
