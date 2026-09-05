using AAMAP;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class MapSystemController : MonoBehaviour {
        [Header("References")]
        [SerializeField] AAMapRuntimeBinder aaBinder = null;
        [SerializeField] MinimapController minimap = null;
        [SerializeField] WorldMapController worldMap = null;
        [SerializeField] MapMarkerManager markerManager = null;
        [SerializeField] MapIconRegistry iconRegistry = null;
        [SerializeField] MapInputController input = null;
        [SerializeField] LocalPlayerControlLock controlLock = null;
        [SerializeField] Transform playerTarget = null;
        [SerializeField] bool autoFindPlayer = true;
        [SerializeField] bool disableMinimap;

        bool minimapVisibleRequested = true;

        public AAMapRuntimeBinder Binder => aaBinder;
        public MinimapController Minimap => minimap;
        public WorldMapController WorldMap => worldMap;
        public MapMarkerManager MarkerManager => markerManager;
        public MapIconRegistry IconRegistry => iconRegistry;
        public Transform PlayerTarget => playerTarget;
        public bool IsWorldMapOpen => worldMap != null && worldMap.IsOpen;

        void Awake() {
            ResolveReferences();
            ApplySetup();
        }

        void Start() {
            ResolveReferences();
            ApplySetup();
        }

        public void ResolveReferences() {
            if (aaBinder == null) aaBinder = FindFirst<AAMapRuntimeBinder>();
            if (minimap == null) minimap = GetComponent<MinimapController>();
            if (worldMap == null) worldMap = GetComponent<WorldMapController>();
            if (markerManager == null) markerManager = GetComponent<MapMarkerManager>();
            if (iconRegistry == null) iconRegistry = GetComponent<MapIconRegistry>();
            if (input == null) input = GetComponent<MapInputController>();
            if (controlLock == null) controlLock = FindFirst<LocalPlayerControlLock>();
            if (playerTarget == null && aaBinder != null) playerTarget = aaBinder.PlayerTarget;
            if (playerTarget == null && autoFindPlayer) playerTarget = AAMapRuntimeBinder.FindPlayerTarget();
        }

        public void ApplySetup() {
            if (playerTarget == null && autoFindPlayer) ResolveReferences();

            if (aaBinder != null) {
                aaBinder.SetPlayerTarget(playerTarget);
                aaBinder.ResolveReferences();
                aaBinder.ApplyBindings();
            }

            if (disableMinimap || !minimapVisibleRequested) DisableMinimapSceneObjects();
            else EnableMinimapSceneObjects();
            if (worldMap != null) worldMap.SetTarget(playerTarget);
            if (input != null) input.SetReferences(this, worldMap, worldMap != null ? worldMap.MapManager : null, controlLock);
            if (iconRegistry != null) iconRegistry.SetMarkerManager(markerManager);
            if (markerManager != null) markerManager.ScanMarkers();
        }

        public void SetPlayerTarget(Transform target) {
            playerTarget = target;
            ApplySetup();
        }

        public void OpenWorldMap(bool focusPlayer) {
            ResolveReferences();
            if (worldMap == null) return;
            worldMap.OpenMap(focusPlayer);
        }

        public void CloseWorldMap() {
            ResolveReferences();
            if (worldMap != null) worldMap.CloseMap();
        }

        public void ToggleWorldMap() {
            if (IsWorldMapOpen) CloseWorldMap();
            else OpenWorldMap(true);
        }

        public void SetMinimapVisible(bool visible) {
            if (minimapVisibleRequested == visible) return;
            minimapVisibleRequested = visible;
            if (disableMinimap || !visible) DisableMinimapSceneObjects();
            else EnableMinimapSceneObjects();
        }

        public void FocusWorldPosition(Vector3 worldPosition, bool openMap) {
            ResolveReferences();
            if (worldMap != null) worldMap.Focus(worldPosition, openMap);
            else if (aaBinder != null) aaBinder.FocusMap(worldPosition, openMap);
        }

        void DisableMinimapSceneObjects() {
            if (minimap != null) minimap.enabled = false;

            MinimapManager manager = ResolveMinimapManager();
            if (manager != null) manager.gameObject.SetActive(false);

            GameObject cameraObject = ResolveMinimapCameraObject();
            if (cameraObject != null) cameraObject.SetActive(false);
        }

        void EnableMinimapSceneObjects() {
            MinimapManager manager = ResolveMinimapManager();
            if (manager != null) manager.gameObject.SetActive(true);

            GameObject cameraObject = ResolveMinimapCameraObject();
            if (cameraObject != null) cameraObject.SetActive(true);
            if (minimap != null) {
                minimap.enabled = true;
                minimap.SetTarget(playerTarget);
            }
        }

        MinimapManager ResolveMinimapManager() {
            if (minimap != null && minimap.Manager != null) return minimap.Manager;
            if (aaBinder != null && aaBinder.MinimapManager != null) return aaBinder.MinimapManager;
            return FindFirst<MinimapManager>();
        }

        GameObject ResolveMinimapCameraObject() {
            if (minimap != null && minimap.Camera != null) return minimap.Camera.gameObject;
            if (aaBinder != null && aaBinder.MinimapCamera != null) return aaBinder.MinimapCamera;
            return GameObject.Find("Minimap Camera");
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
