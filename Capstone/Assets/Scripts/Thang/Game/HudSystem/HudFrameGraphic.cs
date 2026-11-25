using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem {
    // Vector geometry keeps the HUD border at a constant thickness at any panel size.
    [AddComponentMenu("UI/Gameplay HUD Frame")]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudFrameGraphic : MaskableGraphic {
        public enum Shape { Chamfered, PetPanel, Circle, Diamond, ChevronLeft, ChevronRight, Pawprint }
        [SerializeField] Shape shape;
        [SerializeField] Color border = new Color(.2f, .36f, .28f, 1f);
        [SerializeField] float thickness = 1.4f;
        [SerializeField] float corner = 14f;
        readonly Vector2[] points = new Vector2[96];
        readonly Capstone.Game.UISystem.SoftReadingMesh reading=new Capstone.Game.UISystem.SoftReadingMesh();Vector2[] readingContour;Vector2 readingSize;
        internal bool IsDarkReading=>shape==Shape.PetPanel&&color.a>.9f;

        public void Configure(Shape value, Color fill, Color line, float width = 1.4f, float cut = 14f) {
            shape = value; color = fill; border = line; thickness = width; corner = cut;
            SetVerticesDirty();
        }
        public void SetBorder(Color line, float width = 1.4f) {
            if (border == line && Mathf.Approximately(thickness, width)) return;
            border = line; thickness = width; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear();
            Rect r = rectTransform.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            if (shape == Shape.Pawprint) {
                // Five filled pads, independent of the font's emoji support.
                AddPad(vh, r, new Vector2(.5f, .29f), new Vector2(.27f, .22f), 0f);
                AddPad(vh, r, new Vector2(.16f, .57f), new Vector2(.11f, .145f), 25f);
                AddPad(vh, r, new Vector2(.37f, .77f), new Vector2(.105f, .15f), 10f);
                AddPad(vh, r, new Vector2(.63f, .77f), new Vector2(.105f, .15f), -10f);
                AddPad(vh, r, new Vector2(.84f, .57f), new Vector2(.11f, .145f), -25f);
                return;
            }
            if (shape == Shape.ChevronLeft || shape == Shape.ChevronRight) {
                float direction = shape == Shape.ChevronLeft ? -1f : 1f;
                Vector2 tip = r.center + new Vector2(direction * r.width * .27f, 0f);
                AddStroke(vh, r.center + new Vector2(-direction * r.width * .27f, r.height * .4f), tip);
                AddStroke(vh, tip, r.center + new Vector2(-direction * r.width * .27f, -r.height * .4f));
                return;
            }
            int count = GetPoints(r);
            Vector2 center = r.center;
            if(IsDarkReading){
                const float scale=1.2f;
                if(readingContour==null||readingSize!=r.size){readingSize=r.size;readingContour=new Vector2[count];for(int i=0;i<count;i++)readingContour[i]=new Vector2(points[i].x-r.xMin,r.yMax-points[i].y)/scale;}
                reading.Ensure(r.width/scale,r.height/scale,"hud-pet-panel",true,readingContour);
                for(int i=0;i<reading.Points.Length;i++){var p=reading.Points[i]*scale;vh.AddVert(new Vector3(r.xMin+p.x,r.yMax-p.y),reading.Colors[i],Vector2.zero);}
                for(int i=0;i<reading.Indices.Length;i+=3)vh.AddTriangle(reading.Indices[i],reading.Indices[i+2],reading.Indices[i+1]);
            }else if (color.a > 0f) {
                vh.AddVert(center, color, Vector2.zero);
                for (int i = 0; i < count; i++) vh.AddVert(points[i], color, Vector2.zero);
                for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
            }
            if (border.a <= 0f || thickness <= 0f) return;
            for (int i = 0; i < count; i++) {
                Vector2 a = points[i], b = points[(i + 1) % count];
                Vector2 before = (a - points[(i + count - 1) % count]).normalized;
                Vector2 edge = (b - a).normalized;
                Vector2 after = (points[(i + 2) % count] - b).normalized;
                Vector2 n = new Vector2(-edge.y, edge.x);
                Vector2 na = (new Vector2(-before.y, before.x) + n).normalized;
                Vector2 nb = (n + new Vector2(-after.y, after.x)).normalized;
                Vector2 ai = a + na * (thickness / Mathf.Max(.3f, Vector2.Dot(na, n)));
                Vector2 bi = b + nb * (thickness / Mathf.Max(.3f, Vector2.Dot(nb, n)));
                int v = vh.currentVertCount;
                vh.AddVert(a, border, Vector2.zero); vh.AddVert(b, border, Vector2.zero);
                vh.AddVert(bi, border, Vector2.zero); vh.AddVert(ai, border, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v, v + 2, v + 3);
            }
        }
        void AddPad(VertexHelper vh, Rect rect, Vector2 position, Vector2 radius, float degrees) {
            const int segments = 24;
            Vector2 center = rect.min + Vector2.Scale(position, rect.size);
            float rotation = degrees * Mathf.Deg2Rad, sin = Mathf.Sin(rotation), cos = Mathf.Cos(rotation);
            int first = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);
            for (int i = 0; i < segments; i++) {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 offset = new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y);
                offset = new Vector2(offset.x * cos - offset.y * sin, offset.x * sin + offset.y * cos);
                vh.AddVert(center + Vector2.Scale(offset, rect.size), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                vh.AddTriangle(first, first + i + 1, first + (i + 1) % segments + 1);
        }
        void AddStroke(VertexHelper vh, Vector2 from, Vector2 to) {
            Vector2 edge = (to - from).normalized;
            Vector2 normal = new Vector2(-edge.y, edge.x) * thickness * .5f;
            int v = vh.currentVertCount;
            vh.AddVert(from - normal, color, Vector2.zero); vh.AddVert(to - normal, color, Vector2.zero);
            vh.AddVert(to + normal, color, Vector2.zero); vh.AddVert(from + normal, color, Vector2.zero);
            vh.AddTriangle(v, v + 1, v + 2); vh.AddTriangle(v, v + 2, v + 3);
        }
        int GetPoints(Rect r) {
            if (shape == Shape.Circle) {
                for (int i = 0; i < 96; i++) {
                    float a = i * Mathf.PI * 2f / 96;
                    points[i] = r.center + new Vector2(Mathf.Cos(a) * r.width * .5f, Mathf.Sin(a) * r.height * .5f);
                }
                return 96;
            }
            if (shape == Shape.Diamond) {
                points[0] = new Vector2(r.xMax, r.center.y); points[1] = new Vector2(r.center.x, r.yMax);
                points[2] = new Vector2(r.xMin, r.center.y); points[3] = new Vector2(r.center.x, r.yMin);
                return 4;
            }
            float c = Mathf.Min(corner, r.width * .25f, r.height * .25f);
            float right = shape == Shape.PetPanel ? Mathf.Min(38f, r.height * .3f) : c;
            points[0] = new Vector2(r.xMin + c, r.yMin);
            points[1] = new Vector2(r.xMax - right, r.yMin);
            points[2] = new Vector2(r.xMax, r.yMin + right);
            points[3] = new Vector2(r.xMax, r.yMax - right);
            points[4] = new Vector2(r.xMax - right, r.yMax);
            points[5] = new Vector2(r.xMin + c, r.yMax);
            points[6] = new Vector2(r.xMin, r.yMax - c);
            points[7] = new Vector2(r.xMin, r.yMin + c);
            return 8;
        }
    }
}
