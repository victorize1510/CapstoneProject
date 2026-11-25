using System;
using System.Linq;
using Capstone.Game.CreatureSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    internal readonly struct MapEncounterZone {
        internal const string GrassGroup = "ZoneGrass";
        internal const string CaveGroup = "ZoneCave";
        internal readonly Bounds Bounds;
        internal readonly string Label, Group;
        internal readonly CreatureEncounterZone Source;

        internal MapEncounterZone(Bounds bounds, string label, string group) : this(bounds, label, group, null) {}

        internal MapEncounterZone(Bounds bounds, string label, string group, CreatureEncounterZone source) {
            Source = source;
            Bounds = bounds;
            Label = label;
            Group = group;
        }

        internal static CreatureEncounterZone[] Find(Scene scene) {
            var sources = UnityEngine.Object.FindObjectsByType<CreatureEncounterZone>(FindObjectsSortMode.InstanceID)
                .Where(zone => zone.gameObject.scene == scene && IsActive(zone)).ToArray();
            // Chapter I has compatibility entries under its quest root. Prefer the scene's authored groups for that same spawn ID.
            var groupedIds = sources.Where(zone => GroupOf(zone) != null && !string.IsNullOrEmpty(zone.zoneId))
                .Select(zone => zone.zoneId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return sources.Where(zone => GroupOf(zone) != null || !groupedIds.Contains(zone.zoneId)).ToArray();
        }

        internal static bool IsActive(CreatureEncounterZone zone) => zone != null && zone.isActiveAndEnabled &&
            zone.spawningEnabled && zone.zoneBounds != null && zone.zoneBounds.enabled && zone.zoneBounds.isTrigger;

        internal static string GroupOf(CreatureEncounterZone zone) {
            for (Transform parent = zone.transform.parent; parent != null; parent = parent.parent) {
                if (string.Equals(parent.name, GrassGroup, StringComparison.OrdinalIgnoreCase)) return GrassGroup;
                if (string.Equals(parent.name, CaveGroup, StringComparison.OrdinalIgnoreCase)) return CaveGroup;
            }
            return null;
        }

        internal static bool IsSecretStarter(CreatureEncounterZone zone) =>
            zone.zoneId is "capstone-special-dragon-fire" or "capstone-special-shell" or "capstone-special-sun-blossom";

        internal static string DisplayName(CreatureEncounterZone zone, int fallback = 0) {
            if (IsSecretStarter(zone)) return string.Empty;
            string name = (zone.zoneName ?? "").Trim();
            if (name.StartsWith("Zone ", StringComparison.OrdinalIgnoreCase) && int.TryParse(name.Substring(5), out int number))
                return $"Vùng {number}";
            if (name.Length > 0 && !string.Equals(name, "Wild Zone", StringComparison.OrdinalIgnoreCase)) return name;
            return fallback > 0 ? $"Vùng {fallback}" : "Vùng thú hoang";
        }
    }

    internal sealed class MapZoneOverlay : MaskableGraphic {
        const int Segments = 48;
        static readonly Color32 LineColor = new Color32(250, 217, 137, 232);
        static readonly Color32 FillColor = new Color32(253, 208, 107, 33);
        MapEncounterZone[] zones = Array.Empty<MapEncounterZone>();
        Text[] labels = Array.Empty<Text>();
        Rect viewBounds;
        bool minimap;
        RectTransform info;
        CreatureEncounterZone pinned, displayed;

        internal static MapZoneOverlay Create(RawImage map, bool forMinimap) {
            var layer = MapArtProjection.CreateOverlay(map.rectTransform, "Encounter Zones");
            var overlay = layer.gameObject.AddComponent<MapZoneOverlay>();
            overlay.minimap = forMinimap;
            overlay.raycastTarget = false;
            return overlay;
        }

        internal void SetZones(MapEncounterZone[] source, Rect artBounds, string group) {
            zones = (source ?? Array.Empty<MapEncounterZone>()).Where(zone =>
                (group == null || zone.Group == null || zone.Group == group) &&
                (artBounds.width <= 0 || artBounds.Contains(new Vector2(zone.Bounds.center.x, zone.Bounds.center.z)))).ToArray();
            if (labels.Length != zones.Length) {
                foreach (Text label in labels) {
                    label.gameObject.SetActive(false);
                    Destroy(label.gameObject);
                }
                labels = new Text[zones.Length];
                for (int i = 0; i < labels.Length; i++) labels[i] = CreateLabel();
            }
            for (int i = 0; i < labels.Length; i++) labels[i].text = labels[i].gameObject.name = zones[i].Label;
            if (!zones.Any(zone => zone.Source == pinned)) pinned = null;
            UpdateLabels();
            SetVerticesDirty();
        }

        internal void Refresh(Camera projection, bool useArt) {
            Rect view = projection != null ? MapArtProjection.ViewBounds(projection) : default;
            if (view == viewBounds && (useArt ? null : projection) == sceneCamera &&
                (sceneCamera == null || rotation == sceneCamera.transform.rotation)) return;
            viewBounds = view;
            sceneCamera = useArt ? null : projection;
            if (sceneCamera != null) rotation = sceneCamera.transform.rotation;
            UpdateLabels();
            SetVerticesDirty();
        }

        protected override void OnDisable() {
            pinned = displayed = null;
            if (info != null) info.gameObject.SetActive(false);
            base.OnDisable();
        }

        void Update() {
            if (minimap || viewBounds.width <= 0 || !Cursor.visible) return;
            Rect viewport = rectTransform.rect;
            var rootCanvas = canvas?.rootCanvas;
            Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? rootCanvas.worldCamera : null;
            CreatureEncounterZone hovered = null;
            string title = null;
            float nearest = float.PositiveInfinity;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, Input.mousePosition, eventCamera, out var local)
                && viewport.Contains(local)) {
                foreach (var zone in zones) {
                    if (zone.Source == null) continue;
                    float radius = Mathf.Max(zone.Bounds.extents.x, zone.Bounds.extents.z);
                    if (radius <= 0f) continue;
                    Vector2 center = viewport.min + Vector2.Scale(viewport.size, Project(zone.Bounds.center));
                    var distance = local - center;
                    var normalized = new Vector2(distance.x * viewBounds.width / (radius * viewport.width),
                        distance.y * viewBounds.height / (radius * viewport.height));
                    if (normalized.sqrMagnitude > 1f || normalized.sqrMagnitude >= nearest) continue;
                    hovered = zone.Source;
                    title = zone.Label;
                    nearest = normalized.sqrMagnitude;
                }
                if (Input.GetMouseButtonDown(0)) pinned = pinned == hovered ? null : hovered;
            }
            var next = hovered != null ? hovered : pinned;
            if (next == displayed) return;
            displayed = next;
            if (next == null) {
                if (info != null) info.gameObject.SetActive(false);
                return;
            }
            BuildInfo(next, title ?? zones.First(zone => zone.Source == next).Label);
        }

        void BuildInfo(CreatureEncounterZone source, string title) {
            if (info != null) {
                info.gameObject.SetActive(false);
                Destroy(info.gameObject);
            }
            var block = new GameObject("Zone Information", typeof(RectTransform), typeof(Image),
                typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            info = block.GetComponent<RectTransform>();
            info.SetParent(rectTransform, false);
            info.SetAsLastSibling();
            info.anchorMin = info.anchorMax = info.pivot = Vector2.one;
            info.anchoredPosition = new Vector2(-18, -18);
            info.sizeDelta = new Vector2(290, 0);
            var image = block.GetComponent<Image>();
            image.color = new Color(.07f, .10f, .17f, .96f);
            image.raycastTarget = false;
            var stack = block.GetComponent<VerticalLayoutGroup>();
            stack.padding = new RectOffset(12, 12, 8, 8);
            stack.spacing = 5;
            stack.childControlHeight = true;
            stack.childForceExpandHeight = false;
            block.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            CreateInfoLabel(info, title, 21, 30);
            var entries = source.PreviewPopulation().ToArray();
            if (entries.Length == 0) CreateInfoLabel(info, "Đang cập nhật danh sách thú", 16, 32);
            foreach (var (name, sprite, min, max) in entries) {
                var row = new GameObject("Creature", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                row.transform.SetParent(info, false);
                row.GetComponent<LayoutElement>().preferredHeight = 38;
                var layout = row.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                if (sprite != null) {
                    var portrait = new GameObject("Portrait", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    portrait.transform.SetParent(row.transform, false);
                    var icon = portrait.GetComponent<Image>();
                    icon.sprite = sprite;
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    portrait.GetComponent<LayoutElement>().preferredWidth = 36;
                    portrait.GetComponent<LayoutElement>().preferredHeight = 36;
                }
                var levels = min == max ? $"Cấp {min}" : $"Cấp {min}–{max}";
                var label = CreateInfoLabel(row.transform, $"{name}\n{levels}", 16, 38);
                label.GetComponent<LayoutElement>().flexibleWidth = 1;
            }
        }

        static Text CreateInfoLabel(Transform parent, string value, int size, float height) {
            var child = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            child.transform.SetParent(parent, false);
            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
            child.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        Camera sceneCamera;
        Quaternion rotation;

        Vector2 Project(Vector3 point) => sceneCamera != null ? (Vector2)sceneCamera.WorldToViewportPoint(point) :
            MapArtProjection.Normalize(viewBounds, new Vector2(point.x, point.z));

        Text CreateLabel() {
            var label = new GameObject("Zone Label", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            label.rectTransform.SetParent(rectTransform, false);
            label.rectTransform.sizeDelta = new Vector2(96f, 28f);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = minimap ? 15 : 22;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontStyle = FontStyle.Bold;
            label.color = LineColor;
            label.raycastTarget = false;
            var outline = label.GetComponent<Outline>();
            outline.effectColor = new Color32(19, 22, 28, 255);
            outline.effectDistance = new Vector2(1.3f, -1.3f);
            return label;
        }

        void UpdateLabels() {
            for (int i = 0; i < zones.Length; i++) {
                var label = labels[i];
                if (viewBounds.width <= 0 || viewBounds.height <= 0) {
                    label.gameObject.SetActive(false);
                    continue;
                }
                var point = Project(zones[i].Bounds.center);
                label.gameObject.SetActive(point.x >= 0 && point.x <= 1 && point.y >= 0 && point.y <= 1);
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = point;
                label.rectTransform.anchoredPosition = new Vector2(0, minimap ? -12f : -16f);
            }
        }

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            if (viewBounds.width <= 0 || viewBounds.height <= 0) return;
            Rect viewport = rectTransform.rect;
            foreach (var zone in zones) {
                Bounds bounds = zone.Bounds;
                float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                if (radius <= 0f) continue;
                Vector2 point = Project(bounds.center);
                Vector2 localCenter = viewport.min + Vector2.Scale(viewport.size, point);
                var ring = new Vector2(radius * viewport.width / viewBounds.width, radius * viewport.height / viewBounds.height);
                if (!viewport.Overlaps(new Rect(localCenter - ring, 2f * ring))) continue;
                AddRing(mesh, localCenter, ring);
            }
        }

        void AddRing(VertexHelper mesh, Vector2 center, Vector2 radii) {
            int first = mesh.currentVertCount;
            mesh.AddVert(center, FillColor, Vector2.zero);
            float thickness = minimap ? 1.5f : 2.5f;
            var inner = new Vector2(Mathf.Max(0, radii.x - thickness), Mathf.Max(0, radii.y - thickness));
            for (int i = 0; i <= Segments; i++) {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + Vector2.Scale(direction, inner), FillColor, Vector2.zero);
                mesh.AddVert(center + Vector2.Scale(direction, inner), LineColor, Vector2.zero);
                mesh.AddVert(center + Vector2.Scale(direction, radii), LineColor, Vector2.zero);
                if (i == 0) continue;
                int current = first + 1 + 3 * i;
                mesh.AddTriangle(first, current - 3, current);
                mesh.AddTriangle(current - 2, current - 1, current + 2);
                mesh.AddTriangle(current - 2, current + 2, current + 1);
            }
        }
    }
}
