using AAMAP;
using UnityEngine;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class AAMapRuntimeBinder : MonoBehaviour {
        [Header("AA Map")]
        [SerializeField] MinimapManager minimapManager = null;
        [SerializeField] MapManager mapManager = null;
        [SerializeField] GameObject minimapCamera = null;
        [SerializeField] GameObject mapCamera = null;

        [Header("Target")]
        [SerializeField] Transform playerTarget = null;
        [SerializeField] bool autoFindPlayer = true;
        [SerializeField] bool rotateMinimapWithPlayer = false;
        [SerializeField] bool minimapDisabled;

        [Header("World Map")]
        [SerializeField] bool startMapClosed = true;
        [SerializeField] bool disableAAMapBuiltInInput = true;
        [SerializeField, Min(1f)] float defaultMapCameraHeight = 160f;
        [SerializeField, Min(1f)] float defaultMapOrthographicSize = 500f;

        public MinimapManager MinimapManager => minimapManager;
        public MapManager MapManager => mapManager;
        public GameObject MinimapCamera => minimapCamera;
        public GameObject MapCamera => mapCamera;
        public Transform PlayerTarget => playerTarget;

        void Awake() {
            ResolveReferences();
            ApplyBindings();
        }

        void Start() {
            ResolveReferences();
            ApplyBindings();

            if (startMapClosed && mapManager != null && mapManager.IsMapEnabled()) {
                PrepareMapManagerForCustomUi();
                mapManager.DisableMap();
            }
        }

        public void SetReferences(MinimapManager minimap, MapManager map, GameObject minimapCam, GameObject worldMapCam, Transform target) {
            minimapManager = minimap != null ? minimap : minimapManager;
            mapManager = map != null ? map : mapManager;
            minimapCamera = minimapCam != null ? minimapCam : minimapCamera;
            mapCamera = worldMapCam != null ? worldMapCam : mapCamera;
            playerTarget = target != null ? target : playerTarget;
            ApplyBindings();
        }

        public void SetPlayerTarget(Transform target) {
            playerTarget = target;
            ApplyBindings();
        }

        public void ResolveReferences() {
            if (minimapManager == null) minimapManager = FindFirst<MinimapManager>();
            if (mapManager == null) mapManager = FindFirst<MapManager>();

            if (minimapCamera == null && minimapManager != null) minimapCamera = minimapManager.GetCamera();
            if (mapCamera == null && mapManager != null) mapCamera = mapManager.GetMapCamera();
            if (minimapCamera == null) minimapCamera = GameObject.Find("Minimap Camera");
            if (mapCamera == null) mapCamera = GameObject.Find("Map Camera");

            if (playerTarget == null && autoFindPlayer) playerTarget = FindPlayerTarget();
        }

        public void ApplyBindings() {
            if (disableAAMapBuiltInInput && mapManager != null) {
                mapManager.enablingShortcut = KeyCode.None;
                mapManager.disablingShortcut = KeyCode.None;
            }

            if (minimapManager != null) {
                if (minimapDisabled) {
                    minimapManager.gameObject.SetActive(false);
                    if (minimapCamera != null) minimapCamera.SetActive(false);
                } else {
                    if (playerTarget != null) minimapManager.SetTargetObject(playerTarget.gameObject);
                    if (minimapCamera != null) minimapManager.SetCamera(minimapCamera);
                    minimapManager.rotateWithTarget = rotateMinimapWithPlayer;
                }
            }

            if (mapManager != null) {
                PrepareMapManagerForCustomUi();
                if (mapCamera != null) mapManager.SetMapCamera(mapCamera);
                mapManager.disableMinimap = true;
                mapManager.minimapManager = null;
                mapManager.minimapGameObject = null;
            }
        }

        public void FocusMap(Vector3 worldPosition, bool openMap) {
            ResolveReferences();
            ApplyBindings();

            if (mapManager == null) return;

            float cameraHeight = mapCamera != null
                ? Mathf.Max(1f, mapCamera.transform.position.y)
                : defaultMapCameraHeight;

            mapManager.SetCameraPosition(new Vector3(worldPosition.x, cameraHeight, worldPosition.z));
            mapManager.SetCameraOrthograpicSize(defaultMapOrthographicSize);

            if (openMap && !mapManager.IsMapEnabled()) {
                PrepareMapManagerForCustomUi();
                mapManager.EnableMap();
            }
        }

        void PrepareMapManagerForCustomUi() {
            if (mapManager == null) return;

            // Custom controllers manage minimap visibility and UI chrome.
            mapManager.disableMinimap = true;
            mapManager.haveBorder = false;
            mapManager.haveZoomButtons = false;
            mapManager.haveExitButton = false;
            mapManager.displayDirections = false;
            mapManager.displayGrid = false;
        }

        public static Transform FindPlayerTarget() {
            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (behaviour != null && behaviour.GetType().Name == "BasicPlayerMovement") return behaviour.transform;
            }

            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            return taggedPlayer != null ? taggedPlayer.transform : null;
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
