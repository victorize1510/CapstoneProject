#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Capstone.Game.QuestSystem.Editor {
    internal static class CaveDropInspection {
        const float SearchDepth = 12f;
        const int MaxMarkers = 400;
        static readonly List<Vector3> drops = new();

        [MenuItem("Tools/Capstone/Cave/Scan open drop edges")]
        static void Scan() {
            var scene = SceneManager.GetActiveScene();
            ChapterWorldLayout layout = null;
            foreach (var root in scene.GetRootGameObjects()) {
                layout = root.GetComponentInChildren<ChapterWorldLayout>();
                if (layout != null) break;
            }
            if (layout == null) { Debug.LogWarning("Mở scene có Chapter I World rồi quét lại."); return; }
            var bounds = new SerializedObject(layout).FindProperty("caveRegionBounds").vector4Value;
            var region = new Rect(bounds.x, bounds.y, bounds.z, bounds.w);
            if (region.width <= 0 || region.height <= 0) { Debug.LogWarning("Chưa có ranh giới MapCave."); return; }
            Clear();
            Physics.SyncTransforms();
            var nav = NavMesh.CalculateTriangulation();
            if (nav.indices.Length == 0) { Debug.LogWarning("Không thấy NavMesh đã bake đang hoạt động."); return; }
            var edges = new Dictionary<(Vector3Int, Vector3Int), (Vector3 a, Vector3 b, Vector3 inside)>();
            var shared = new HashSet<(Vector3Int, Vector3Int)>();
            for (int triangle = 0; triangle < nav.indices.Length; triangle += 3) {
                Vector3 a = nav.vertices[nav.indices[triangle]], b = nav.vertices[nav.indices[triangle + 1]],
                    c = nav.vertices[nav.indices[triangle + 2]];
                Vector3 center = (a + b + c) / 3f;
                if (!region.Contains(new Vector2(center.x, center.z))) continue;
                AddEdge(a, b, c);
                AddEdge(b, c, a);
                AddEdge(c, a, b);
            }

            void AddEdge(Vector3 a, Vector3 b, Vector3 inside) {
                var start = Vector3Int.RoundToInt(a * 100f);
                var end = Vector3Int.RoundToInt(b * 100f);
                if (start.x > end.x || start.x == end.x &&
                    (start.y > end.y || start.y == end.y && start.z > end.z)) (start, end) = (end, start);
                if (!edges.TryAdd((start, end), (a, b, inside))) shared.Add((start, end));
            }

            var cells = new HashSet<Vector3Int>();
            int tested = 0;
            foreach (var pair in edges) {
                if (shared.Contains(pair.Key)) continue;
                var (a, b, inside) = pair.Value;
                var tangent = Vector3.ProjectOnPlane(b - a, Vector3.up);
                if (tangent.sqrMagnitude < .25f) continue;
                Vector3 outward = Vector3.Cross(Vector3.up, tangent.normalized);
                if (Vector3.Dot(outward, inside - a) > 0f) outward = -outward;
                int samples = Mathf.Max(1, Mathf.CeilToInt(tangent.magnitude / 2f));
                for (int i = 0; i < samples; i++) {
                    Vector3 edge = Vector3.Lerp(a, b, (i + .5f) / samples);
                    Vector3 outside = edge + outward * 1.2f;
                    tested++;
                    if (NavMesh.SamplePosition(outside, out var continuation, .8f, NavMesh.AllAreas) &&
                        Mathf.Abs(continuation.position.y - edge.y) < .8f) continue;
                    if (Physics.Raycast(edge + Vector3.up * .9f, outward, 1.25f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Raycast(outside + Vector3.up * .5f, Vector3.down, SearchDepth, Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore)) continue;
                    if (cells.Add(Vector3Int.RoundToInt(edge / 2f)) && drops.Count < MaxMarkers) drops.Add(edge);
                }
            }
            SceneView.duringSceneGui += Draw;
            SceneView.RepaintAll();
            Debug.Log($"MapCave: {tested} mẫu mép, {cells.Count} vị trí nghi rơi sâu, {drops.Count} dấu đỏ trong Scene View.");
        }

        [MenuItem("Tools/Capstone/Cave/Clear drop markers")]
        static void Clear() {
            drops.Clear();
            SceneView.duringSceneGui -= Draw;
            SceneView.RepaintAll();
        }

        static void Draw(SceneView view) {
            if (Event.current.type != EventType.Repaint) return;
            using var color = new Handles.DrawingScope(new Color(1f, .24f, .15f));
            foreach (var point in drops) {
                Handles.DrawLine(point, point + Vector3.up * 2f, 2f);
                Handles.SphereHandleCap(0, point + Vector3.up * 2f, Quaternion.identity, .5f, EventType.Repaint);
            }
        }
    }
}
#endif
