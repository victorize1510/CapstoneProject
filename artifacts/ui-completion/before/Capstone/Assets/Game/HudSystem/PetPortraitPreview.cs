using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Capstone.Game.HudSystem {
    /// <summary>Render-only snapshot: no prefab cloning, gameplay behaviours or colliders.</summary>
    public sealed class PetPortraitPreview : IDisposable {
        sealed class Part { public Renderer Source; public Mesh Mesh; public MeshFilter Filter; public Transform Transform; }
        readonly List<Part> parts = new List<Part>();
        GameObject stage;
        Camera camera;
        RenderTexture texture;
        PetController source;
        float nextBake;
        public Texture Texture => texture;

        public bool Show(PetController pet) {
            if (source == pet && stage != null) return parts.Count > 0;
            Clear();
            if (pet == null) return false;
            source = pet;
            stage = new GameObject("PetPortraitRenderStage") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(18000, 18000, 18000);
            texture = new RenderTexture(640, 640, 24) { name = "PetPortraitTexture", hideFlags = HideFlags.HideAndDontSave };
            texture.Create();
            var cameraObject = new GameObject("PortraitCamera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.GetComponent<Camera>(); camera.targetTexture = texture;
            camera.orthographic = true; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.91f, .95f, .87f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 200;
            var visual = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
            foreach (Renderer renderer in pet.GetComponentsInChildren<Renderer>(true)) {
                if (visual != null && !visual.IsCurrentVisualRenderer(renderer)) continue;
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                var obj = new GameObject("PortraitMesh", typeof(MeshFilter), typeof(MeshRenderer)) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
                obj.transform.SetParent(stage.transform, false);
                var target = obj.GetComponent<MeshRenderer>(); target.sharedMaterials = renderer.sharedMaterials;
                target.shadowCastingMode = ShadowCastingMode.Off; target.receiveShadows = false;
                parts.Add(new Part { Source = renderer, Mesh = new Mesh { name = "PortraitPose", hideFlags = HideFlags.HideAndDontSave }, Filter = obj.GetComponent<MeshFilter>(), Transform = obj.transform });
            }
            nextBake = 0; Update(); return parts.Count > 0;
        }

        public void Update() {
            if (source == null || camera == null || Time.unscaledTime < nextBake) return;
            nextBake = Time.unscaledTime + .1f;
            Bounds bounds = default; bool any = false;
            foreach (var part in parts) {
                if (part.Source == null) continue;
                var transform = part.Source.transform;
                part.Transform.position = stage.transform.position + Quaternion.Inverse(source.transform.rotation) * (transform.position - source.transform.position);
                part.Transform.rotation = Quaternion.Inverse(source.transform.rotation) * transform.rotation;
                part.Transform.localScale = transform.lossyScale;
                if (part.Source is SkinnedMeshRenderer skin) { skin.BakeMesh(part.Mesh); part.Filter.sharedMesh = part.Mesh; }
                else part.Filter.sharedMesh = part.Source.GetComponent<MeshFilter>().sharedMesh;
                var mesh = part.Filter.sharedMesh; if (mesh == null) continue;
                // Fit the actual pose vertices, not the wide culling bounds.
                var vertices = new List<Vector3>(); mesh.GetVertices(vertices);
                foreach (var vertex in vertices) { Vector3 world = part.Transform.TransformPoint(vertex); if (!any) { bounds = new Bounds(world, Vector3.zero); any = true; } else bounds.Encapsulate(world); }
            }
            if (!any) return;
            Vector3 direction = new Vector3(.65f, .25f, 1f).normalized;
            float distance = Mathf.Max(2, bounds.size.magnitude * 1.6f);
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
            float extent = 0;
            Vector3 min = bounds.min, max = bounds.max;
            for (int i = 0; i < 8; i++) {
                Vector3 point = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
                Vector3 projected = camera.transform.InverseTransformPoint(point);
                extent = Mathf.Max(extent, Mathf.Abs(projected.y), Mathf.Abs(projected.x) / camera.aspect);
            }
            camera.orthographicSize = Mathf.Max(.1f, extent * 1.08f);
            camera.farClipPlane = Mathf.Max(20, distance + bounds.size.magnitude * 2);
        }
        public void Clear() {
            source = null;
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            foreach (var part in parts) Destroy(part.Mesh);
            parts.Clear();
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            stage = null; camera = null; texture = null;
        }
        public void Dispose() => Clear();
        static void Destroy(UnityEngine.Object value) { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
