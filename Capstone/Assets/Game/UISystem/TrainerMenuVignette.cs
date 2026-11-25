using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>Small decorative art with a left alpha fade; no material instance or Update loop.</summary>
    public sealed class TrainerMenuVignette : Image {
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            if (sprite == null) return;
            var rect = GetPixelAdjustedRect();
            // Preserve each illustration's proportions inside its common corner slot.
            float aspect = sprite.rect.width / sprite.rect.height;
            if (rect.width / rect.height > aspect) {
                float width = rect.height * aspect;
                rect.x += (rect.width - width) * .5f; rect.width = width;
            } else {
                float height = rect.width / aspect;
                rect.y += (rect.height - height) * .5f; rect.height = height;
            }
            var uv = DataUtility.GetOuterUV(sprite);
            const int steps = 8;
            for (int i = 0; i <= steps; i++) {
                float t = (float)i / steps;
                Color tint = color; tint.a *= Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .22f));
                float x = Mathf.Lerp(rect.xMin, rect.xMax, t), u = Mathf.Lerp(uv.x, uv.z, t);
                mesh.AddVert(new Vector3(x, rect.yMin), tint, new Vector2(u, uv.y));
                mesh.AddVert(new Vector3(x, rect.yMax), tint, new Vector2(u, uv.w));
                if (i == steps) continue;
                int n = i * 2;
                mesh.AddTriangle(n, n + 1, n + 3); mesh.AddTriangle(n + 3, n + 2, n);
            }
        }
    }
}
