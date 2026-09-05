using System.Collections.Generic;
using System.Linq;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    [DisallowMultipleComponent]
    public sealed class CompassHudController : MonoBehaviour {
        const string RootName = "CompassHUDRoot";
        const int DirectionCount = 8;

        static readonly string[] DirectionNames = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        static readonly float[] DirectionAngles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        static Sprite solidSprite;
        static Font cachedFont;

        [Header("References")]
        [SerializeField] Canvas targetCanvas = null;
        [SerializeField] Camera targetCamera = null;
        [SerializeField] Transform viewer = null;

        [Header("Layout")]
        [SerializeField] bool buildOnAwake = true;
        [SerializeField] bool hideWhileUiOpen = true;
        [SerializeField] Vector2 anchoredPosition = new Vector2(0f, -24f);
        [SerializeField] Vector2 size = new Vector2(680f, 64f);
        [SerializeField, Range(30f, 180f)] float visibleAngle = 105f;

        [Header("Ticks")]
        [SerializeField, Range(5f, 45f)] float tickStepDegrees = 15f;
        [SerializeField] float majorTickHeight = 15f;
        [SerializeField] float minorTickHeight = 8f;

        [Header("Markers")]
        [SerializeField] bool showEnemyMarkers = true;
        [SerializeField] bool showQuestMarkers = true;
        [SerializeField] bool showWaypointMarkers = true;
        [SerializeField, Min(0f)] float markerMaxDistance = 220f;
        [SerializeField] Color enemyMarkerColor = new Color(1f, 0.27f, 0.18f, 1f);
        [SerializeField] Color bossMarkerColor = new Color(1f, 0.12f, 0.55f, 1f);
        [SerializeField] Color questMarkerColor = new Color(0.35f, 0.95f, 1f, 1f);
        [SerializeField] Color waypointMarkerColor = new Color(1f, 0.82f, 0.22f, 1f);

        RectTransform root;
        RectTransform directionRoot;
        RectTransform tickRoot;
        RectTransform markerRoot;
        Image centerNeedle;

        readonly List<Text> directionLabels = new List<Text>();
        readonly List<TickBinding> ticks = new List<TickBinding>();
        readonly List<MarkerBinding> markerBindings = new List<MarkerBinding>();
        readonly List<MapMarker> markerCache = new List<MapMarker>();

        bool markersDirty = true;

        void Awake() {
            ResolveReferences();
            if (buildOnAwake) RebuildCompass();
        }

        void OnEnable() {
            MapMarker.MarkerEnabled += HandleMarkerChanged;
            MapMarker.MarkerDisabled += HandleMarkerChanged;
            MapMarker.MarkerChanged += HandleMarkerChanged;
            ResolveReferences();
            if (root == null && buildOnAwake) RebuildCompass();
            SyncCompassVisibility();
            RefreshMarkers(true);
        }

        void OnDisable() {
            MapMarker.MarkerEnabled -= HandleMarkerChanged;
            MapMarker.MarkerDisabled -= HandleMarkerChanged;
            MapMarker.MarkerChanged -= HandleMarkerChanged;
        }

        void LateUpdate() {
            ResolveRuntimeReferences();
            SyncCompassVisibility();
            if (root != null && !root.gameObject.activeSelf) return;

            if (markersDirty) {
                RefreshMarkers(true);
            }

            UpdateCompass();
        }

        void HandleMarkerChanged(MapMarker marker) {
            markersDirty = true;
        }

        [ContextMenu("Rebuild Compass")]
        public void RebuildCompass() {
            ResolveReferences();
            EnsureCanvas();

            root = EnsureRoot(targetCanvas.transform);
            ClearChildren(root);

            BuildBackground(root);
            tickRoot = CreateRect(root, "Ticks");
            Stretch(tickRoot);
            directionRoot = CreateRect(root, "Directions");
            Stretch(directionRoot);
            markerRoot = CreateRect(root, "Markers");
            Stretch(markerRoot);
            centerNeedle = CreateImage(root, "CenterNeedle", new Color(0.35f, 0.95f, 1f, 1f));
            SetAnchored(centerNeedle.rectTransform, new Vector2(0f, -9f), new Vector2(8f, 18f), new Vector2(0.5f, 1f));

            BuildDirections();
            BuildTicks();
            RefreshMarkers(true);
            SyncCompassVisibility();
            UpdateCompass();
        }

        public void SetReferences(Canvas canvas, Camera camera, Transform viewerTransform) {
            targetCanvas = canvas != null ? canvas : targetCanvas;
            targetCamera = camera != null ? camera : targetCamera;
            viewer = viewerTransform != null ? viewerTransform : viewer;
            UpdateCompass();
        }

        void ResolveReferences() {
            EnsureCanvas();
            ResolveRuntimeReferences();
        }

        void ResolveRuntimeReferences() {
            if (targetCamera == null) targetCamera = Camera.main;
            if (viewer == null) viewer = FindPlayerTransform();
        }

        void EnsureCanvas() {
            if (targetCanvas != null) return;

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

        static void ConfigureCanvas(Canvas canvas) {
            if (canvas == null) return;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        RectTransform EnsureRoot(Transform canvasTransform) {
            Transform existing = canvasTransform.Find(RootName);
            GameObject rootObject = existing != null
                ? existing.gameObject
                : new GameObject(RootName, typeof(RectTransform));

            rootObject.transform.SetParent(canvasTransform, false);
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
            rect.SetAsLastSibling();
            return rect;
        }

        void BuildBackground(RectTransform parent) {
            Image line = CreateImage(parent, "CompassLine", new Color(0.74f, 0.61f, 0.37f, 0.78f));
            SetAnchored(line.rectTransform, new Vector2(0f, -28f), new Vector2(size.x, 2f), new Vector2(0.5f, 1f));

            Image glow = CreateImage(parent, "CompassSoftBack", new Color(0.02f, 0.06f, 0.07f, 0.30f));
            SetAnchored(glow.rectTransform, new Vector2(0f, -30f), new Vector2(size.x + 34f, 34f), new Vector2(0.5f, 1f));
            glow.transform.SetAsFirstSibling();
        }

        void BuildDirections() {
            directionLabels.Clear();
            for (int i = 0; i < DirectionCount; i++) {
                Text label = CreateText(directionRoot, "Direction_" + DirectionNames[i], DirectionNames[i], 16, FontStyle.Bold, new Color(0.92f, 0.84f, 0.62f, 1f), TextAnchor.MiddleCenter);
                label.rectTransform.sizeDelta = new Vector2(46f, 22f);
                directionLabels.Add(label);
            }
        }

        void BuildTicks() {
            ticks.Clear();
            int tickCount = Mathf.CeilToInt(360f / tickStepDegrees);
            for (int i = 0; i < tickCount; i++) {
                float angle = i * tickStepDegrees;
                bool major = Mathf.Approximately(Mathf.Repeat(angle, 45f), 0f);
                Image tick = CreateImage(tickRoot, "Tick_" + Mathf.RoundToInt(angle), major
                    ? new Color(0.92f, 0.84f, 0.62f, 0.95f)
                    : new Color(0.92f, 0.84f, 0.62f, 0.52f));
                tick.rectTransform.sizeDelta = new Vector2(2f, major ? majorTickHeight : minorTickHeight);
                ticks.Add(new TickBinding(angle, tick.rectTransform));
            }
        }

        void RefreshMarkers(bool rebuildAll) {
            markerCache.Clear();
            foreach (MapMarker marker in FindObjectsByType<MapMarker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) {
                if (ShouldUseMarker(marker)) markerCache.Add(marker);
            }

            if (!rebuildAll && markerBindings.Count == markerCache.Count) return;

            foreach (MarkerBinding binding in markerBindings) {
                if (binding.Root != null) DestroySafe(binding.Root.gameObject);
            }
            markerBindings.Clear();

            for (int i = 0; i < markerCache.Count; i++) {
                RectTransform markerRootTransform = CreateRect(markerRoot, "CompassMarker_" + i);
                markerRootTransform.sizeDelta = new Vector2(28f, 28f);

                Image diamond = CreateImage(markerRootTransform, "Icon", ResolveMarkerColor(markerCache[i]));
                diamond.rectTransform.sizeDelta = new Vector2(12f, 12f);
                diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                SetAnchored(diamond.rectTransform, Vector2.zero, new Vector2(12f, 12f), new Vector2(0.5f, 0.5f));

                Text distance = CreateText(markerRootTransform, "Distance", string.Empty, 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                SetAnchored(distance.rectTransform, new Vector2(0f, -16f), new Vector2(52f, 14f), new Vector2(0.5f, 0.5f));

                markerBindings.Add(new MarkerBinding(markerCache[i], markerRootTransform, diamond, distance));
            }

            markersDirty = false;
        }

        void UpdateCompass() {
            if (root == null || targetCamera == null) return;

            root.anchoredPosition = anchoredPosition;
            root.sizeDelta = size;

            float cameraYaw = targetCamera.transform.eulerAngles.y;
            float halfVisibleAngle = Mathf.Max(1f, visibleAngle * 0.5f);
            float halfWidth = Mathf.Max(1f, size.x * 0.5f);

            for (int i = 0; i < directionLabels.Count; i++) {
                float delta = Mathf.DeltaAngle(cameraYaw, DirectionAngles[i]);
                PositionCompassItem(directionLabels[i].rectTransform, delta, halfVisibleAngle, halfWidth, -10f, true);
            }

            foreach (TickBinding tick in ticks) {
                float delta = Mathf.DeltaAngle(cameraYaw, tick.Angle);
                PositionCompassItem(tick.Rect, delta, halfVisibleAngle, halfWidth, -28f, true);
            }

            for (int i = markerBindings.Count - 1; i >= 0; i--) {
                MarkerBinding binding = markerBindings[i];
                if (binding.Marker == null || !ShouldUseMarker(binding.Marker)) {
                    if (binding.Root != null) binding.Root.gameObject.SetActive(false);
                    continue;
                }

                Vector3 origin = viewer != null ? viewer.position : targetCamera.transform.position;
                Vector3 offset = binding.Marker.transform.position - origin;
                offset.y = 0f;
                float distance = offset.magnitude;
                bool inRange = markerMaxDistance <= 0f || distance <= markerMaxDistance;
                bool hasDirection = offset.sqrMagnitude > 0.001f;
                if (!inRange || !hasDirection) {
                    binding.Root.gameObject.SetActive(false);
                    continue;
                }

                float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                float delta = Mathf.DeltaAngle(cameraYaw, bearing);
                PositionCompassItem(binding.Root, delta, halfVisibleAngle, halfWidth, 15f, false);
                binding.Root.gameObject.SetActive(Mathf.Abs(delta) <= halfVisibleAngle);
                binding.Icon.color = ResolveMarkerColor(binding.Marker);
                binding.Distance.text = FormatDistance(distance);
            }
        }

        void SyncCompassVisibility() {
            if (root == null) return;

            bool shouldShow = !hideWhileUiOpen || !InventoryInputController.GameplayInputBlocked;
            if (root.gameObject.activeSelf != shouldShow) root.gameObject.SetActive(shouldShow);
        }

        static void PositionCompassItem(RectTransform rect, float delta, float halfVisibleAngle, float halfWidth, float y, bool fadeAtEdge) {
            if (rect == null) return;

            bool visible = Mathf.Abs(delta) <= halfVisibleAngle;
            rect.gameObject.SetActive(visible);
            if (!visible) return;

            float normalized = Mathf.Clamp(delta / halfVisibleAngle, -1f, 1f);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(normalized * halfWidth, y);

            if (!fadeAtEdge) return;

            Graphic graphic = rect.GetComponent<Graphic>();
            if (graphic == null) return;

            Color color = graphic.color;
            color.a = Mathf.Lerp(0.2f, 1f, 1f - Mathf.Abs(normalized));
            graphic.color = color;
        }

        bool ShouldUseMarker(MapMarker marker) {
            if (marker == null || !marker.isActiveAndEnabled) return false;
            if (!marker.ShouldRender(viewer, true)) return false;

            switch (marker.MarkerType) {
                case MapMarkerType.Enemy:
                case MapMarkerType.Boss:
                    return showEnemyMarkers;
                case MapMarkerType.QuestAvailable:
                case MapMarkerType.QuestTarget:
                    return showQuestMarkers;
                case MapMarkerType.Custom:
                    return showWaypointMarkers && IsWaypointMarker(marker);
                default:
                    return false;
            }
        }

        bool IsWaypointMarker(MapMarker marker) {
            string id = marker.MarkerId ?? string.Empty;
            string title = marker.DisplayName ?? string.Empty;
            return id.ToLowerInvariant().Contains("waypoint") || title.ToLowerInvariant().Contains("waypoint");
        }

        Color ResolveMarkerColor(MapMarker marker) {
            if (marker == null) return Color.white;

            switch (marker.MarkerType) {
                case MapMarkerType.Enemy:
                    return enemyMarkerColor;
                case MapMarkerType.Boss:
                    return bossMarkerColor;
                case MapMarkerType.QuestAvailable:
                case MapMarkerType.QuestTarget:
                    return questMarkerColor;
                default:
                    return waypointMarkerColor;
            }
        }

        static string FormatDistance(float distance) {
            if (distance >= 1000f) return (distance / 1000f).ToString("0.0") + "km";
            return Mathf.RoundToInt(distance) + "m";
        }

        static Transform FindPlayerTransform() {
            try {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) return tagged.transform;
            } catch (UnityException) {
            }

            GameObject named = GameObject.Find("Player");
            return named != null ? named.transform : null;
        }

        static RectTransform CreateRect(Transform parent, string name) {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        static Image CreateImage(Transform parent, string name, Color color) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = SolidSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
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
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
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

        static void SetAnchored(RectTransform rect, Vector2 position, Vector2 rectSize, Vector2 anchor) {
            if (rect == null) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = rectSize;
            rect.localScale = Vector3.one;
        }

        static void ClearChildren(Transform parent) {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) {
                DestroySafe(parent.GetChild(i).gameObject);
            }
        }

        static void DestroySafe(GameObject obj) {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
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

        readonly struct TickBinding {
            public TickBinding(float angle, RectTransform rect) {
                Angle = angle;
                Rect = rect;
            }

            public readonly float Angle;
            public readonly RectTransform Rect;
        }

        readonly struct MarkerBinding {
            public MarkerBinding(MapMarker marker, RectTransform root, Image icon, Text distance) {
                Marker = marker;
                Root = root;
                Icon = icon;
                Distance = distance;
            }

            public readonly MapMarker Marker;
            public readonly RectTransform Root;
            public readonly Image Icon;
            public readonly Text Distance;
        }
    }
}
