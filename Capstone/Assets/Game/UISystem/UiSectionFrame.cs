using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>View-only section boundary. Cached geometry, no focus or pointer ownership.</summary>
    public sealed class UiSectionFrame : VisualElement {
        readonly UiSectionFrameMesh frame = new UiSectionFrameMesh();
        public UiSectionFrame() {
            name = "section-frame"; pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = style.right = style.top = style.bottom = 0;
            generateVisualContent += context => frame.Paint(context, layout.width, layout.height, false);
        }
        public static void Attach(VisualElement host) {
            if (host == null) return;
            foreach (var child in host.Children()) if (child is UiSectionFrame) return;
            host.Insert(0, new UiSectionFrame());
        }
    }

    /// <summary>Single polygon edge and quiet steel page; shared by Quest and Companion.</summary>
    public sealed class UiSectionFrameMesh {
        Vector2 size;
        public Vector2[] Points { get; private set; }
        public int[] Indices { get; private set; }
        public static readonly Color Edge = new Color32(110,126,153,235);
        public static readonly Color Accent = new Color32(204,173,131,215);
        public static Color Fill(float y, float height, bool dossier = false, bool dark = false) {
            var tint = dark ? Color.Lerp(new Color32(34,46,69,255), new Color32(24,34,54,255), y / height)
                : Color.Lerp(new Color32(198,207,224,255), new Color32(164,179,203,255), y / height);
            tint.a = dark ? .84f : dossier ? .78f : .66f;
            return tint;
        }
        public void Ensure(float width, float height) {
            if (Points == null || size != new Vector2(width, height)) {
                size = new Vector2(width, height);
                Points = GreenAngularGeometry.Polygon("panel", width, height, 1);
                Indices = GreenAngularGeometry.Triangles(Points);
            }
        }
        public void Paint(MeshGenerationContext context, float width, float height, bool dossier) {
            if (!float.IsFinite(width) || !float.IsFinite(height) || width < 2 || height < 2) return;
            Ensure(width, height);
            var points = Points; var indices = Indices;
            var mesh = context.Allocate(points.Length, indices.Length);
            foreach (var p in points) {
                var tint = Fill(p.y, height, dossier);
                mesh.SetNextVertex(new Vertex { position = new Vector3(p.x,p.y,Vertex.nearZ), tint = tint });
            }
            foreach (int index in indices) mesh.SetNextIndex((ushort)index);
            var pen = context.painter2D;
            pen.BeginPath(); pen.MoveTo(points[0]); for (int i=1;i<points.Length;i++) pen.LineTo(points[i]); pen.ClosePath();
            pen.lineWidth = 1.3f; pen.strokeColor = Edge; pen.Stroke();
            pen.BeginPath(); pen.MoveTo(new Vector2(20,1)); pen.LineTo(new Vector2(dossier ? 100 : 60,1));
            pen.MoveTo(new Vector2(width-54,height-1)); pen.LineTo(new Vector2(width-20,height-1));
            pen.lineWidth = 1.6f; pen.strokeColor = Accent; pen.Stroke();
            if (dossier) {
                // Binding spine keeps the task sheet distinct from action buttons.
                pen.BeginPath(); pen.MoveTo(new Vector2(10,18)); pen.LineTo(new Vector2(10,height-18));
                pen.lineWidth = 3; pen.strokeColor = new Color32(83,100,131,130); pen.Stroke();
            }
        }
    }
}
