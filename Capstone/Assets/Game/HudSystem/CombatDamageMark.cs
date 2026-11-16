using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem {
    /// <summary>Font-independent crit star / DOT dots. Shares the UI white texture, no per-hit sprite allocations.</summary>
    public sealed class CombatDamageMark : MaskableGraphic {
        public bool Dot;
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            if (Dot) { Diamond(mesh, new Vector2(-2, 0), 2.4f, 2.4f); Diamond(mesh, new Vector2(3, 0), 1.5f, 1.5f); }
            else { Diamond(mesh, Vector2.zero, 5.5f, 8); Diamond(mesh, new Vector2(5, 5), 1.8f, 2.6f); }
        }
        void Diamond(VertexHelper mesh, Vector2 center, float width, float height) {
            int at = mesh.currentVertCount;
            mesh.AddVert(center + Vector2.up * height, color, Vector2.zero);
            mesh.AddVert(center + Vector2.right * width, color, Vector2.zero);
            mesh.AddVert(center + Vector2.down * height, color, Vector2.zero);
            mesh.AddVert(center + Vector2.left * width, color, Vector2.zero);
            mesh.AddTriangle(at, at + 1, at + 2); mesh.AddTriangle(at, at + 2, at + 3);
        }
    }
}
