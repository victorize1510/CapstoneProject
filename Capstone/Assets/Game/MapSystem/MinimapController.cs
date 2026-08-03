using AAMAP;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class MinimapController : MonoBehaviour {
        [Header("References")]
        [SerializeField] MinimapManager minimapManager = null;
        [SerializeField] Camera minimapCamera = null;
        [SerializeField] Transform target = null;
        [SerializeField] bool autoFindPlayer = true;

        [Header("Camera")]
        [SerializeField, Min(1f)] float cameraHeight = 60f;
        [SerializeField, Min(1f)] float cameraRange = 28f;
        [SerializeField] bool rotateWithTarget = false;
        [SerializeField] bool keepCameraAboveTarget = true;
        [SerializeField, Min(0f)] float followSharpness = 20f;
        [SerializeField] bool applyCullingMask = false;
        [SerializeField] LayerMask cullingMask = ~0;

        [Header("World Bounds")]
        [SerializeField] bool clampToWorldBounds = true;
        [SerializeField] Vector2 worldCenter = Vector2.zero;
        [SerializeField] Vector2 worldSize = new Vector2(120f, 120f);

        public MinimapManager Manager => minimapManager;
        public Camera Camera => minimapCamera;
        public Transform Target => target;

        void Awake() {
            ResolveReferences();
            ApplySettings();
            EnsureOpaqueMinimapVisuals();
        }

        void Start() {
            ResolveReferences();
            ApplySettings();
            EnsureOpaqueMinimapVisuals();
        }

        void LateUpdate() {
            if (!keepCameraAboveTarget || minimapCamera == null || target == null) return;

            Vector3 desired = ClampCameraPosition(new Vector3(target.position.x, cameraHeight, target.position.z));
            float t = followSharpness <= 0f ? 1f : 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            minimapCamera.transform.position = Vector3.Lerp(minimapCamera.transform.position, desired, t);
            minimapCamera.transform.rotation = Quaternion.Euler(90f, rotateWithTarget ? target.eulerAngles.y : 0f, 0f);
        }

        public void SetTarget(Transform newTarget) {
            target = newTarget;
            ApplySettings();
        }

        public void SetReferences(MinimapManager manager, Camera camera, Transform newTarget) {
            minimapManager = manager != null ? manager : minimapManager;
            minimapCamera = camera != null ? camera : minimapCamera;
            target = newTarget != null ? newTarget : target;
            ApplySettings();
        }

        public void Zoom(float direction) {
            cameraRange = Mathf.Max(1f, cameraRange - Mathf.Sign(direction) * 4f);
            ApplySettings();
        }

        public void SetWorldBounds(Vector2 center, Vector2 size) {
            worldCenter = center;
            worldSize = new Vector2(Mathf.Max(1f, size.x), Mathf.Max(1f, size.y));
        }

        void ResolveReferences() {
            if (minimapManager == null) minimapManager = FindFirst<MinimapManager>();
            if (minimapCamera == null && minimapManager != null && minimapManager.GetCamera() != null) {
                minimapCamera = minimapManager.GetCamera().GetComponent<Camera>();
            }
            if (minimapCamera == null) {
                GameObject cameraObject = GameObject.Find("Minimap Camera");
                if (cameraObject != null) minimapCamera = cameraObject.GetComponent<Camera>();
            }
            if (target == null && autoFindPlayer) target = AAMapRuntimeBinder.FindPlayerTarget();
        }

        void ApplySettings() {
            if (minimapManager != null) {
                if (target != null) minimapManager.SetTargetObject(target.gameObject);
                if (minimapCamera != null) minimapManager.SetCamera(minimapCamera.gameObject);
                minimapManager.SetCameraHeight(cameraHeight);
                minimapManager.SetCameraRange(cameraRange);
                minimapManager.rotateWithTarget = rotateWithTarget;
                minimapManager.clearFlags = MinimapClearFlags.SolidColor;
                minimapManager.backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
                minimapManager.minimapOpacity = 1f;
                minimapManager.minimapColor = Color.white;
            }

            if (minimapCamera != null) {
                minimapCamera.orthographic = true;
                minimapCamera.orthographicSize = cameraRange;
                minimapCamera.transform.position = ClampCameraPosition(new Vector3(
                    minimapCamera.transform.position.x,
                    cameraHeight,
                    minimapCamera.transform.position.z));
                minimapCamera.nearClipPlane = 0.1f;
                minimapCamera.farClipPlane = Mathf.Max(cameraHeight + 200f, 300f);
                minimapCamera.clearFlags = CameraClearFlags.SolidColor;
                minimapCamera.backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
                if (applyCullingMask) minimapCamera.cullingMask = cullingMask;
            }
        }

        void EnsureOpaqueMinimapVisuals() {
            Color backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
            Transform root = minimapManager != null ? minimapManager.transform : null;
            Transform mask = root != null ? root.Find("Minimap Mask") : null;
            if (mask == null) return;

            Image maskImage = mask.GetComponent<Image>();
            if (maskImage != null) {
                maskImage.color = Color.white;
                maskImage.raycastTarget = false;
            }

            Mask maskComponent = mask.GetComponent<Mask>();
            if (maskComponent != null) maskComponent.showMaskGraphic = false;

            Image filler = EnsureRuntimeImage(mask, "Minimap Background Filler", backgroundColor);
            if (filler != null) filler.transform.SetAsFirstSibling();

            Image grid = EnsureRuntimeImage(mask, "Minimap Grid", Color.clear);
            if (grid != null) grid.gameObject.SetActive(false);

            Transform displayTransform = mask.Find("Minimap Display");
            RawImage display = displayTransform != null ? displayTransform.GetComponent<RawImage>() : null;
            if (display != null) {
                display.color = Color.white;
                display.raycastTarget = false;
                if (minimapManager != null && minimapManager.renderTexture != null) display.texture = minimapManager.renderTexture;
                Stretch(display.rectTransform);
                display.transform.SetSiblingIndex(1);
            }
        }

        static Image EnsureRuntimeImage(Transform parent, string name, Color color) {
            Transform child = parent.Find(name);
            if (child == null) {
                GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
                obj.transform.SetParent(parent, false);
                child = obj.transform;
            }

            RectTransform rect = child.GetComponent<RectTransform>();
            if (rect != null) Stretch(rect);

            Image image = child.GetComponent<Image>();
            if (image == null) image = child.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static void Stretch(RectTransform rect) {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        Vector3 ClampCameraPosition(Vector3 position) {
            position.y = cameraHeight;
            if (!clampToWorldBounds || minimapCamera == null) return position;

            float halfWidth = cameraRange * Mathf.Max(0.01f, minimapCamera.aspect);
            float halfHeight = cameraRange;
            float halfWorldX = Mathf.Max(0.5f, worldSize.x * 0.5f);
            float halfWorldZ = Mathf.Max(0.5f, worldSize.y * 0.5f);

            float minX = worldCenter.x - halfWorldX + halfWidth;
            float maxX = worldCenter.x + halfWorldX - halfWidth;
            float minZ = worldCenter.y - halfWorldZ + halfHeight;
            float maxZ = worldCenter.y + halfWorldZ - halfHeight;

            position.x = minX <= maxX ? Mathf.Clamp(position.x, minX, maxX) : worldCenter.x;
            position.z = minZ <= maxZ ? Mathf.Clamp(position.z, minZ, maxZ) : worldCenter.y;
            return position;
        }

        void OnValidate() {
            worldSize = new Vector2(Mathf.Max(1f, worldSize.x), Mathf.Max(1f, worldSize.y));
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
