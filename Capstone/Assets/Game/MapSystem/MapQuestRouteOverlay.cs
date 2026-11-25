using System;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    internal sealed class MapQuestRouteOverlay : MaskableGraphic {
        static readonly Color32 Edge = new Color32(14, 34, 49, 240);
        static readonly Color32 Trail = new Color32(111, 241, 255, 255);
        Vector3[] corners = Array.Empty<Vector3>();
        Rect viewBounds;
        Quaternion rotation;
        Camera sceneCamera;
        bool minimap;

        internal static void Refresh(RawImage map, ref MapQuestRouteOverlay overlay, Camera projection,
            QuestMapMarkerBridge bridge, bool draw, bool useArt, bool forMinimap) {
            var points = draw && bridge != null && bridge.isActiveAndEnabled ? bridge.RouteCorners : Array.Empty<Vector3>();
            if (overlay == null) {
                if (points.Length < 2) return;
                var layer = MapArtProjection.CreateOverlay(map.rectTransform, "Tracked Quest Route");
                overlay = layer.gameObject.AddComponent<MapQuestRouteOverlay>();
                overlay.raycastTarget = false;
                overlay.minimap = forMinimap;
                layer.SetAsFirstSibling();
            }
            overlay.gameObject.SetActive(points.Length >= 2);
            if (points.Length < 2) return;
            Rect view = MapArtProjection.ViewBounds(projection);
            Camera camera = useArt ? null : projection;
            Quaternion yaw = camera != null ? camera.transform.rotation : Quaternion.identity;
            if (ReferenceEquals(overlay.corners, points) && overlay.viewBounds == view &&
                overlay.sceneCamera == camera && overlay.rotation == yaw) return;
            overlay.corners = points;
            overlay.viewBounds = view;
            overlay.sceneCamera = camera;
            overlay.rotation = yaw;
            overlay.SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();
            if (corners.Length < 2 || viewBounds.width <= 0 || viewBounds.height <= 0) return;
            Rect viewport = rectTransform.rect;
            for (int pass = 0; pass < 2; pass++) {
                // The outline is drawn as one pass so it cannot cut across the bright trail at turns.
                float halfWidth = pass == 0 ? (minimap ? 2.8f : 4f) : (minimap ? 1.4f : 2.4f);
                Color32 color = pass == 0 ? Edge : Trail;
                Vector2 start = Project(corners[0], viewport);
                for (int i = 1; i < corners.Length; i++) {
                    Vector2 finish = Project(corners[i], viewport);
                    AddSegment(mesh, start, finish, halfWidth, color);
                    start = finish;
                }
            }
        }

        Vector2 Project(Vector3 position, Rect viewport) {
            Vector2 normalized = sceneCamera != null ? (Vector2)sceneCamera.WorldToViewportPoint(position) :
                MapArtProjection.Normalize(viewBounds, new Vector2(position.x, position.z));
            return viewport.min + Vector2.Scale(viewport.size, normalized);
        }

        static void AddSegment(VertexHelper mesh, Vector2 start, Vector2 finish, float halfWidth, Color32 color) {
            Vector2 delta = finish - start;
            if (delta.sqrMagnitude < .01f) return;
            Vector2 tangent = delta.normalized * halfWidth;
            Vector2 normal = new Vector2(-tangent.y, tangent.x);
            int first = mesh.currentVertCount;
            mesh.AddVert(start - tangent - normal, color, Vector2.zero);
            mesh.AddVert(start - tangent + normal, color, Vector2.zero);
            mesh.AddVert(finish + tangent + normal, color, Vector2.zero);
            mesh.AddVert(finish + tangent - normal, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
