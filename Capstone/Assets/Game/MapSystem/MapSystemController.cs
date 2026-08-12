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
        [SerializeField] bool disableMinimap = true;

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

            if (disableMinimap) DisableMinimapSceneObjects();
            else if (minimap != null) minimap.SetTarget(playerTarget);
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

        public void FocusWorldPosition(Vector3 worldPosition, bool openMap) {
            ResolveReferences();
            if (worldMap != null) worldMap.Focus(worldPosition, openMap);
            else if (aaBinder != null) aaBinder.FocusMap(worldPosition, openMap);
        }

        void DisableMinimapSceneObjects() {
            if (minimap != null) minimap.enabled = false;

            MinimapManager manager = FindFirst<MinimapManager>();
            if (manager != null) manager.gameObject.SetActive(false);

            GameObject cameraObject = GameObject.Find("Minimap Camera");
            if (cameraObject != null) cameraObject.SetActive(false);
        }
        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
