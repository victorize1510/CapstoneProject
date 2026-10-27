using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Decorative reading layers only. No input, data, opacity-tree or gameplay ownership.</summary>
    public static class UiReadability {
        public const float LightAlpha = .24f, DarkAlpha = .44f, Feather = 24f;
        public static void Bind(VisualElement root) {
            if (root == null || root.ClassListContains("readability-ui")) return;
            root.AddToClassList("readability-ui");
            var sheet = Resources.Load<StyleSheet>("UI/Readability20261026/Readability");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            foreach(string id in new[]{"box-panel","codex-panel","shop-panel","settings-panel"}) {
                var legacy=root.Q(id);if(legacy==null)continue;
                foreach(var header in legacy.Query<VisualElement>(className:"screen-header").ToList()){header.style.backgroundColor=Color.clear;Add(header,false,"rect");}
                foreach(var label in legacy.Query<Label>().ToList()) {
                    bool control=false;for(var p=label.parent;p!=null&&p!=legacy;p=p.parent)if(p is UnityEngine.UIElements.Button||p.ClassListContains("unity-base-field")){control=true;break;}
                    bool semantic=false;foreach(string cls in label.GetClasses())if(cls.Contains("rarity")||cls.Contains("element")||cls.Contains("status"))semantic=true;
                    if(!control&&!semantic)label.style.color=GreenAngularGeometry.Ink;
                }
                SoftenGroups(legacy,"box-footer");
            }
            UiPanelSections.Bind(root);
        }
        public static UiReadingSurface Add(VisualElement host, bool dark = false, string shape = "panel") {
            if (host == null) return null;
            var existing = host.Q<UiReadingSurface>("soft-reading"); if (existing != null) return existing;
            var layer = new UiReadingSurface { name = "soft-reading", Dark = dark, Shape = shape };
            layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            host.Insert(0, layer); return layer;
        }
        public static SoftReadingGraphic Add(RectTransform host, bool dark = false, string shape = "panel") {
            var child = host.Find("SoftReading"); if (child != null) return child.GetComponent<SoftReadingGraphic>();
            var go = new GameObject("SoftReading", typeof(RectTransform), typeof(SoftReadingGraphic)); go.transform.SetParent(host, false); go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var layer = go.GetComponent<SoftReadingGraphic>(); layer.Dark = dark; layer.Shape = shape; layer.raycastTarget = false; return layer;
        }
        public static void SoftenGroups(VisualElement root, params string[] classes) {
            foreach (string cls in classes) foreach (var host in root.Query<VisualElement>(className: cls).ToList()) {
                if (host is Label || host is UnityEngine.UIElements.Button) continue;
                host.style.backgroundColor = Color.clear; Add(host);
            }
        }
        public static void BehindLabel(Label label, bool dark = false) {
            if (label?.parent == null) return;
            var parent = label.parent; string name = "soft-reading-" + label.name;
            if (parent.Q<UiReadingSurface>(name) != null) return;
            var layer = new UiReadingSurface { name = name, Dark = dark, Shape = "rect" }; parent.Insert(parent.IndexOf(label), layer);
            layer.style.position = Position.Absolute;
            void Fit() { var r = label.layout; layer.style.left = r.x; layer.style.top = r.y; layer.style.width = r.width; layer.style.height = r.height; layer.style.display = label.resolvedStyle.display; }
            label.RegisterCallback<GeometryChangedEvent>(_ => Fit()); label.RegisterCallback<DetachFromPanelEvent>(_ => layer.RemoveFromHierarchy()); Fit();
        }
    }
    /// <summary>Cached polygon-clipped grid. Both renderers consume the same feather and vertex colors.</summary>
    public sealed class SoftReadingMesh {
        public Vector2[] Points; public Color32[] Colors; public int[] Indices;
        Vector2 size; string shape; bool dark,contrast;
        public void Ensure(float width, float height, string outline, bool onWorld,Vector2[] silhouette=null,bool highContrast=false) {
            if (Points != null && size == new Vector2(width, height) && shape == outline && dark == onWorld&&contrast==highContrast) return;
            size = new Vector2(width, height); shape = outline; dark = onWorld;contrast=highContrast;
            var positions = new List<Vector2>(); var colors = new List<Color32>(); var triangles = new List<int>();
            float f = Mathf.Min(UiReadability.Feather, width / 2, height / 2);
            var polygon = silhouette??(outline == "rect" ? new[] { Vector2.zero, new Vector2(width, 0), new Vector2(width, height), new Vector2(0, height) } : GreenAngularGeometry.Polygon(outline, width, height, 0));
            float[] xs = { 0, f * .5f, f, width - f, width - f * .5f, width }, ys = { 0, f * .5f, f, height - f, height - f * .5f, height };
            for (int y = 0; y < ys.Length - 1; y++) for (int x = 0; x < xs.Length - 1; x++) {
                if (xs[x + 1] <= xs[x] || ys[y + 1] <= ys[y]) continue;
                var clipped = GreenAngularGeometry.ClipRect(polygon, Rect.MinMaxRect(xs[x], ys[y], xs[x + 1], ys[y + 1])); if (clipped.Length < 3) continue;
                int start = positions.Count;
                foreach (var p in clipped) {
                    float distance = float.MaxValue;
                    for (int i = 0; i < polygon.Length; i++) { var a = polygon[i]; var edge = polygon[(i + 1) % polygon.Length] - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, edge) / Mathf.Max(edge.sqrMagnitude, .0001f)); distance = Mathf.Min(distance, Vector2.Distance(p, a + edge * t)); }
                    var tint = onWorld ? GreenAngularGeometry.Ink : highContrast ? (Color)new Color32(185,196,215,255) : GreenAngularGeometry.Panel;
                    tint.a = (onWorld ? UiReadability.DarkAlpha : highContrast ? .82f : UiReadability.LightAlpha) * Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / Mathf.Max(f, .01f)));
                    positions.Add(p); colors.Add(tint);
                }
                foreach (int index in GreenAngularGeometry.Triangles(clipped)) triangles.Add(start + index);
            }
            Points = positions.ToArray(); Colors = colors.ToArray(); Indices = triangles.ToArray();
        }
        public void Paint(MeshGenerationContext context, float width, float height, string outline, bool onWorld,bool highContrast=false) {
            if (width < 2 || height < 2) return; Ensure(width, height, outline, onWorld,null,highContrast);
            if (Points.Length == 0) return; var mesh = context.Allocate(Points.Length, Indices.Length);
            for (int i = 0; i < Points.Length; i++) mesh.SetNextVertex(new Vertex { position = new Vector3(Points[i].x, Points[i].y, Vertex.nearZ), tint = Colors[i] });
            foreach (int index in Indices) mesh.SetNextIndex((ushort)index);
        }
    }
    public sealed class UiReadingSurface : VisualElement {
        public bool Dark; public string Shape = "panel";
        readonly SoftReadingMesh mesh = new SoftReadingMesh();
        public UiReadingSurface() { pickingMode = PickingMode.Ignore; generateVisualContent += c => mesh.Paint(c, layout.width, layout.height, Shape, Dark); }
    }
    public sealed class SoftReadingGraphic : MaskableGraphic {
        public bool Dark; public string Shape = "panel";
        public float LogicalScale = 1f;
        readonly SoftReadingMesh mesh = new SoftReadingMesh();
        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear(); var r = rectTransform.rect; if (r.width < 2 || r.height < 2) return; float scale = Mathf.Max(.01f, LogicalScale); mesh.Ensure(r.width / scale, r.height / scale, Shape, Dark);
            for (int i = 0; i < mesh.Points.Length; i++) { var p = mesh.Points[i] * scale; vh.AddVert(new Vector3(r.xMin + p.x, r.yMax - p.y), mesh.Colors[i], Vector2.zero); }
            for (int i = 0; i < mesh.Indices.Length; i += 3) vh.AddTriangle(mesh.Indices[i], mesh.Indices[i + 2], mesh.Indices[i + 1]);
        }
    }
}
