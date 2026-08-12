using AAMAP;
using Capstone.Game.Inventory;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class MapInputController : MonoBehaviour {
        [Header("References")]
        [SerializeField] MapSystemController mapSystem = null;
        [SerializeField] AAMapRuntimeBinder mapBinder = null;
        [SerializeField] WorldMapController worldMap = null;
        [SerializeField] MapManager mapManager = null;
        [SerializeField] LocalPlayerControlLock controlLock = null;

        [Header("Keys")]
        [SerializeField] KeyCode toggleMapKey = KeyCode.M;
        [SerializeField] KeyCode closeMapKey = KeyCode.Escape;
        [SerializeField] KeyCode zoomInKey = KeyCode.KeypadPlus;
        [SerializeField] KeyCode zoomOutKey = KeyCode.Minus;
        [SerializeField] KeyCode alternateZoomOutKey = KeyCode.KeypadMinus;
#if ENABLE_INPUT_SYSTEM
        [SerializeField] Key toggleMapInputKey = Key.M;
        [SerializeField] Key closeMapInputKey = Key.Escape;
        [SerializeField] Key zoomInInputKey = Key.NumpadPlus;
        [SerializeField] Key alternateZoomInInputKey = Key.Equals;
        [SerializeField] Key zoomOutInputKey = Key.Minus;
        [SerializeField] Key alternateZoomOutInputKey = Key.NumpadMinus;
#endif

        [Header("Behaviour")]
        [SerializeField] bool lockPlayerWhileOpen = true;
        [SerializeField] bool showCursorWhileOpen = true;
        [SerializeField] bool enableOpenCloseHotkeys = true;
        [SerializeField, Min(0f)] float dragDeadZone = 0.1f;

        bool cursorStateSaved;
        bool previousCursorVisible;
        CursorLockMode previousLockState;
        bool dragging;
        Vector2 lastPointerPosition;

        public bool IsOpen {
            get {
                ResolveReferences();
                if (worldMap != null) return worldMap.IsOpen;
                return mapManager != null && mapManager.IsMapEnabled();
            }
        }

        void Awake() {
            ResolveReferences();
            DisableBuiltInMapInput();
        }

        void Start() {
            ResolveReferences();
            DisableBuiltInMapInput();

            if (IsOpen) {
                CloseMap();
            }
        }

        void Update() {
            ResolveReferences();
            DisableBuiltInMapInput();

            if (enableOpenCloseHotkeys && WasTogglePressed()) {
                ToggleMap();
                return;
            }

            if (enableOpenCloseHotkeys && IsOpen && WasClosePressed()) {
                CloseMap();
                return;
            }

            if (IsOpen) {
                HandleWorldMapPointerInput();
            } else {
                dragging = false;
            }
        }

        public void OpenMap() {
            ResolveReferences();
            if (IsOpen) return;

            SaveCursorState();
            if (showCursorWhileOpen) {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (lockPlayerWhileOpen && controlLock != null) {
                controlLock.LockControls();
            }

            if (mapSystem != null) mapSystem.OpenWorldMap(true);
            else if (worldMap != null) worldMap.OpenMap(false);
            else if (mapManager != null) {
                PrepareMapManagerForCustomUi();
                mapManager.EnableMap();
            }
        }

        public void CloseMap() {
            ResolveReferences();

            if (mapSystem != null) mapSystem.CloseWorldMap();
            else if (worldMap != null) worldMap.CloseMap();
            else if (mapManager != null) {
                PrepareMapManagerForCustomUi();
                mapManager.DisableMap();
            }

            dragging = false;

            if (lockPlayerWhileOpen && controlLock != null) {
                controlLock.UnlockControls();
            }

            RestoreCursorState();
        }

        public void ToggleMap() {
            if (IsOpen) CloseMap();
            else OpenMap();
        }

        public void ZoomIn() {
            ResolveReferences();
            if (worldMap != null) worldMap.Zoom(1f);
            else if (mapManager != null) mapManager.ZoomIn();
        }

        public void ZoomOut() {
            ResolveReferences();
            if (worldMap != null) worldMap.Zoom(-1f);
            else if (mapManager != null) mapManager.ZoomOut();
        }

        public void ClearWaypoint() {
            ResolveReferences();
            if (worldMap != null) worldMap.ClearWaypoint();
        }

        public void SetReferences(MapSystemController system, WorldMapController world, MapManager manager, LocalPlayerControlLock playerLock) {
            mapSystem = system != null ? system : mapSystem;
            worldMap = world != null ? world : worldMap;
            mapManager = manager != null ? manager : mapManager;
            controlLock = playerLock != null ? playerLock : controlLock;
            DisableBuiltInMapInput();
        }

        public void SetOpenCloseHotkeysEnabled(bool enabled) {
            enableOpenCloseHotkeys = enabled;
        }

        void HandleWorldMapPointerInput() {
            if (worldMap == null) return;

            float scroll = ReadScrollDelta();
            if (Mathf.Abs(scroll) > 0.01f) {
                worldMap.Zoom(scroll);
            }

            if (WasZoomInPressed()) {
                worldMap.Zoom(1f);
            }

            if (WasZoomOutPressed()) {
                worldMap.Zoom(-1f);
            }

            Vector2 pointer = ReadPointerPosition();
            if (WasPrimaryPointerPressed()) {
                dragging = true;
                lastPointerPosition = pointer;
            }

            if (WasPrimaryPointerReleased()) {
                dragging = false;
            }

            if (dragging && IsPrimaryPointerHeld()) {
                Vector2 delta = pointer - lastPointerPosition;
                lastPointerPosition = pointer;
                if (delta.sqrMagnitude > dragDeadZone * dragDeadZone) {
                    worldMap.Pan(delta);
                }
            }

            if (WasSecondaryPointerPressed()) {
                worldMap.TrySetWaypoint(pointer);
            }
        }

        void ResolveReferences() {
            if (mapSystem == null) mapSystem = FindFirst<MapSystemController>();
            if (mapBinder == null) mapBinder = FindFirst<AAMapRuntimeBinder>();

            if (mapSystem != null) {
                mapSystem.ResolveReferences();
                if (worldMap == null) worldMap = mapSystem.WorldMap;
            }

            if (mapBinder != null) {
                mapBinder.ResolveReferences();
                mapBinder.ApplyBindings();
            }

            if (worldMap == null) worldMap = FindFirst<WorldMapController>();
            if (mapManager == null && worldMap != null) mapManager = worldMap.MapManager;
            if (mapManager == null && mapBinder != null) mapManager = mapBinder.MapManager;
            if (mapManager == null) mapManager = FindFirst<MapManager>();
            if (controlLock == null) controlLock = FindFirst<LocalPlayerControlLock>();
        }

        void DisableBuiltInMapInput() {
            if (mapManager == null) return;
            mapManager.enablingShortcut = KeyCode.None;
            mapManager.disablingShortcut = KeyCode.None;
            PrepareMapManagerForCustomUi();
        }

        void PrepareMapManagerForCustomUi() {
            if (mapManager == null) return;

            mapManager.disableMinimap = false;
            mapManager.haveBorder = false;
            mapManager.haveZoomButtons = false;
            mapManager.haveExitButton = false;
            mapManager.displayDirections = false;
            mapManager.displayGrid = false;
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

        bool WasTogglePressed() {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && toggleMapInputKey != Key.None && Keyboard.current[toggleMapInputKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(toggleMapKey);
#else
            return false;
#endif
        }

        bool WasClosePressed() {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && closeMapInputKey != Key.None && Keyboard.current[closeMapInputKey].wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(closeMapKey);
#else
            return false;
#endif
        }

        bool WasZoomInPressed() {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null
                   && ((zoomInInputKey != Key.None && Keyboard.current[zoomInInputKey].wasPressedThisFrame)
                       || (alternateZoomInInputKey != Key.None && Keyboard.current[alternateZoomInInputKey].wasPressedThisFrame));
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(zoomInKey);
#else
            return false;
#endif
        }

        bool WasZoomOutPressed() {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null
                   && ((zoomOutInputKey != Key.None && Keyboard.current[zoomOutInputKey].wasPressedThisFrame)
                       || (alternateZoomOutInputKey != Key.None && Keyboard.current[alternateZoomOutInputKey].wasPressedThisFrame));
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(zoomOutKey) || Input.GetKeyDown(alternateZoomOutKey);
#else
            return false;
#endif
        }

        float ReadScrollDelta() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
        }

        Vector2 ReadPointerPosition() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        bool WasPrimaryPointerPressed() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        bool WasPrimaryPointerReleased() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonUp(0);
#else
            return false;
#endif
        }

        bool IsPrimaryPointerHeld() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(0);
#else
            return false;
#endif
        }

        bool WasSecondaryPointerPressed() {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}

