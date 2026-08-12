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

        [Header("UI Layout")]
        [SerializeField] bool enforceTopLeftLayout = true;
        [SerializeField] Vector2 minimapSize = new Vector2(236f, 236f);
        [SerializeField] Vector2 topLeftOffset = new Vector2(28f, -28f);
        [SerializeField, Min(0f)] float maskInset = 20f;
        [SerializeField, Min(0f)] float frameOverscan = 30f;

        RenderTexture runtimeMinimapTexture;

        public MinimapManager Manager => minimapManager;
        public Camera Camera => minimapCamera;
        public Transform Target => target;

        void Awake() {
            ResolveReferences();
            ApplySettings();
            EnsureOpaqueMinimapVisuals();
        }

        void OnEnable() {
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
            if (keepCameraAboveTarget && minimapCamera != null && target != null) {
                EnsureMinimapRenderTexture();
                Vector3 desired = ClampCameraPosition(new Vector3(target.position.x, cameraHeight, target.position.z));
                float t = followSharpness <= 0f ? 1f : 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
                minimapCamera.transform.position = Vector3.Lerp(minimapCamera.transform.position, desired, t);
                minimapCamera.transform.rotation = Quaternion.Euler(90f, rotateWithTarget ? target.eulerAngles.y : 0f, 0f);
            }

            EnsureOpaqueMinimapVisuals();
        }

        void OnDestroy() {
            if (runtimeMinimapTexture == null) return;
            runtimeMinimapTexture.Release();
            Destroy(runtimeMinimapTexture);
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
            RenderTexture texture = EnsureMinimapRenderTexture();

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
                minimapManager.haveBorder = false;
                minimapManager.haveZoomButtons = false;
                minimapManager.displayDirections = false;
                minimapManager.displayGrid = false;
                if (texture != null) minimapManager.renderTexture = texture;
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
                if (texture != null) minimapCamera.targetTexture = texture;
                if (applyCullingMask) minimapCamera.cullingMask = cullingMask;
            }
        }

        void EnsureOpaqueMinimapVisuals() {
            Color backgroundColor = new Color(0.015f, 0.035f, 0.035f, 1f);
            Transform root = minimapManager != null ? minimapManager.transform : null;
            Transform mask = root != null ? root.Find("Minimap Mask") : null;
            if (mask == null) return;

            RectTransform rootRect = root.GetComponent<RectTransform>();
            if (enforceTopLeftLayout && rootRect != null) ConfigureMinimapRoot(rootRect);

            Transform zoomButtons = root.Find("Minimap Zoom Buttons");
            if (zoomButtons != null) zoomButtons.gameObject.SetActive(false);

            Transform border = root.Find("Minimap Border");
            if (border != null) border.gameObject.SetActive(false);

            Transform directions = root.Find("Minimap Directions");
            if (directions != null) directions.gameObject.SetActive(false);

            Image rootImage = root.GetComponent<Image>();
            if (rootImage != null) {
                rootImage.color = Color.clear;
                rootImage.raycastTarget = false;
            }

            RectTransform maskRect = mask.GetComponent<RectTransform>();
            if (maskRect != null) Inset(maskRect, maskInset);

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
                RenderTexture texture = EnsureMinimapRenderTexture();
                if (texture != null) display.texture = texture;
                Stretch(display.rectTransform);
                display.transform.SetSiblingIndex(1);
            }

            ConfigureCustomFrame(root.Find("Thang Minimap Frame"));
        }

        RenderTexture EnsureMinimapRenderTexture() {
            RenderTexture texture = minimapManager != null ? minimapManager.renderTexture : null;
            if (texture == null) texture = runtimeMinimapTexture;

            if (texture == null) {
                runtimeMinimapTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32) {
                    name = "Minimap_RT_Runtime",
                    useMipMap = false,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture = runtimeMinimapTexture;
            }

            if (!texture.IsCreated()) texture.Create();
            if (minimapManager != null) minimapManager.renderTexture = texture;
            if (minimapCamera != null) minimapCamera.targetTexture = texture;
            return texture;
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

        static void Inset(RectTransform rect, float inset) {
            if (rect == null) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            rect.localScale = Vector3.one;
        }

        void ConfigureCustomFrame(Transform frameTransform) {
            if (frameTransform == null) return;

            RectTransform frame = frameTransform.GetComponent<RectTransform>();
            if (frame != null) {
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.pivot = new Vector2(0.5f, 0.5f);
                frame.offsetMin = new Vector2(-frameOverscan, -frameOverscan);
                frame.offsetMax = new Vector2(frameOverscan, frameOverscan);
                frame.localScale = Vector3.one;
            }

            Image image = frameTransform.GetComponent<Image>();
            if (image != null) {
                image.color = Color.white;
                image.raycastTarget = false;
                image.preserveAspect = true;
            }

            frameTransform.SetAsLastSibling();
            frameTransform.gameObject.SetActive(true);
        }

        void ConfigureMinimapRoot(RectTransform rect) {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = topLeftOffset;
            rect.sizeDelta = new Vector2(Mathf.Max(120f, minimapSize.x), Mathf.Max(120f, minimapSize.y));
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
            minimapSize = new Vector2(Mathf.Max(120f, minimapSize.x), Mathf.Max(120f, minimapSize.y));
            maskInset = Mathf.Max(0f, maskInset);
            frameOverscan = Mathf.Max(0f, frameOverscan);
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
