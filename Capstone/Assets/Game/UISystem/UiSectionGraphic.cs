using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>The same section palette and cached polygon for generated uGUI panels.</summary>
    public sealed class UiSectionGraphic : MaskableGraphic {
        public bool Dark;
        public float LogicalScale = 1f;
        readonly UiSectionFrameMesh mesh = new UiSectionFrameMesh();

        public static UiSectionGraphic Attach(RectTransform host, bool dark = false, float logicalScale = 1f) {
            var child = host.Find("SectionFrame");
            if (child != null) return child.GetComponent<UiSectionGraphic>();
            var go = new GameObject("SectionFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(UiSectionGraphic));
            go.transform.SetParent(host, false); go.transform.SetAsFirstSibling();
            var graphic = go.GetComponent<UiSectionGraphic>();
            graphic.Dark = dark; graphic.LogicalScale = logicalScale; graphic.raycastTarget = false;
            var rect = graphic.rectTransform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return graphic;
        }
        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear(); var rect = rectTransform.rect; float scale = Mathf.Max(.01f, LogicalScale);
            float width = rect.width / scale, height = rect.height / scale;
            if (width < 2 || height < 2) return;
            mesh.Ensure(width, height);
            Vector3 Position(Vector2 p) => new Vector3(rect.xMin + p.x * scale, rect.yMax - p.y * scale);
            foreach (var p in mesh.Points) vh.AddVert(Position(p), UiSectionFrameMesh.Fill(p.y, height, false, Dark), Vector2.zero);
            for (int i = 0; i < mesh.Indices.Length; i += 3) vh.AddTriangle(mesh.Indices[i], mesh.Indices[i+2], mesh.Indices[i+1]);
            void Line(Vector2 a, Vector2 b, Color tint, float thickness) {
                var normal = new Vector2(-(b-a).y, (b-a).x).normalized * thickness * .5f;
                int start = vh.currentVertCount;
                vh.AddVert(Position(a-normal), tint, Vector2.zero); vh.AddVert(Position(b-normal), tint, Vector2.zero);
                vh.AddVert(Position(b+normal), tint, Vector2.zero); vh.AddVert(Position(a+normal), tint, Vector2.zero);
                vh.AddTriangle(start, start+2, start+1); vh.AddTriangle(start, start+3, start+2);
            }
            for (int i = 0; i < mesh.Points.Length; i++) Line(mesh.Points[i], mesh.Points[(i+1)%mesh.Points.Length], UiSectionFrameMesh.Edge, 1.3f);
            Line(new Vector2(20,1), new Vector2(Mathf.Min(60,width-20),1), UiSectionFrameMesh.Accent, 1.6f);
            Line(new Vector2(width-54,height-1), new Vector2(width-20,height-1), UiSectionFrameMesh.Accent, 1.6f);
        }
    }
}
