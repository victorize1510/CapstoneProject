using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Capstone.Game.HudSystem {
    /// <summary>Render-only snapshot: no prefab cloning, gameplay behaviours or colliders.</summary>
    public sealed class PetPortraitPreview : IDisposable {
        sealed class Part { public Renderer Source; public Mesh Mesh, StaticMesh; public MeshFilter Filter; public Transform Transform; public readonly List<Vector3> Vertices = new List<Vector3>(); }
        readonly List<Part> parts = new List<Part>();
        GameObject stage;
        Camera camera;
        RenderTexture texture;
        PetController source;
        PetCollectionMetadata metadata;
        string form;
        float nextBake;
        float viewportAspect = 1f;
        public Texture Texture => texture;
        public Color BackgroundColor { get; set; } = Color.white;

        public void SetViewportSize(float width, float height) {
            if (width <= 0 || height <= 0) return;
            float aspect = Mathf.Clamp(width / height, .25f, 4f);
            if (Mathf.Abs(viewportAspect - aspect) < .002f) return;
            viewportAspect = aspect;
            ResizeTexture();
            nextBake = 0;
        }

        void ResizeTexture() {
            if (texture == null) return;
            int width = viewportAspect >= 1f ? 640 : Mathf.RoundToInt(640 * viewportAspect);
            int height = viewportAspect >= 1f ? Mathf.RoundToInt(640 / viewportAspect) : 640;
            if (texture.width != width || texture.height != height) {
                texture.Release(); texture.width = width; texture.height = height; texture.Create();
            }
            if (camera != null) camera.aspect = texture.width / (float)texture.height;
        }

        public bool Show(PetController pet) {
            var nextMetadata = source == pet && stage != null ? metadata : pet != null ? pet.GetComponentInChildren<PetCollectionMetadata>(true) : null;
            string nextForm = nextMetadata?.CurrentFormId;
            if (source == pet && form == nextForm && stage != null) return parts.Count > 0;
            Clear();
            if (pet == null) return false;
            source = pet; metadata = nextMetadata; form = nextForm;
            stage = new GameObject("PetPortraitRenderStage") { hideFlags = HideFlags.HideAndDontSave };
            stage.transform.position = new Vector3(18000, 18000, 18000);
            texture = new RenderTexture(640, 640, 24) { name = "PetPortraitTexture", hideFlags = HideFlags.HideAndDontSave };
            texture.Create();
            var cameraObject = new GameObject("PortraitCamera", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            cameraObject.transform.SetParent(stage.transform, false);
            camera = cameraObject.GetComponent<Camera>(); camera.targetTexture = texture;
            ResizeTexture();
            camera.orthographic = true; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = BackgroundColor;
            camera.nearClipPlane = .01f; camera.farClipPlane = 200;
            RenderPipelineManager.endCameraRendering += OnRendered;
            var visual = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
            foreach (Renderer renderer in pet.GetComponentsInChildren<Renderer>(true)) {
                if (visual != null && !visual.IsCurrentVisualRenderer(renderer)) continue;
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (renderer is MeshRenderer && renderer.GetComponent<MeshFilter>()?.sharedMesh == null) continue;
                var obj = new GameObject("PortraitMesh", typeof(MeshFilter), typeof(MeshRenderer)) { layer = 31, hideFlags = HideFlags.HideAndDontSave };
                obj.transform.SetParent(stage.transform, false);
                var target = obj.GetComponent<MeshRenderer>(); target.sharedMaterials = renderer.sharedMaterials;
                target.shadowCastingMode = ShadowCastingMode.Off; target.receiveShadows = false;
                parts.Add(new Part { Source = renderer, Mesh = renderer is SkinnedMeshRenderer ? new Mesh { name = "PortraitPose", hideFlags = HideFlags.HideAndDontSave } : null,
                    StaticMesh = renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>().sharedMesh : null,
                    Filter = obj.GetComponent<MeshFilter>(), Transform = obj.transform });
            }
            nextBake = 0; Update(); return parts.Count > 0;
        }

        public void Update() {
            if (source == null || camera == null || Time.unscaledTime < nextBake) return;
            nextBake = Time.unscaledTime + .1f;
            Bounds projectedBounds = default; bool any = false;
            Quaternion viewRotation = Quaternion.LookRotation(-new Vector3(.65f,.25f,1f).normalized,Vector3.up);
            Quaternion inverseView = Quaternion.Inverse(viewRotation);
            Quaternion inverseRotation = Quaternion.Inverse(source.transform.rotation);
            foreach (var part in parts) {
                if (part.Source == null) continue;
                var transform = part.Source.transform;
                part.Transform.position = stage.transform.position + inverseRotation * (transform.position - source.transform.position);
                part.Transform.rotation = inverseRotation * transform.rotation;
                part.Transform.localScale = transform.lossyScale;
                if (part.Source is SkinnedMeshRenderer skin) { skin.BakeMesh(part.Mesh); part.Filter.sharedMesh = part.Mesh; }
                else part.Filter.sharedMesh = part.StaticMesh;
                var mesh = part.Filter.sharedMesh; if (mesh == null) continue;
                // Work near the origin and fit the actual projected silhouette.
                // A world AABB's corners overestimate rotated wings and shift framing.
                Matrix4x4 localToStage = Matrix4x4.TRS(part.Transform.localPosition,part.Transform.localRotation,part.Transform.localScale);
                var vertices = part.Vertices;
                if (mesh.isReadable) {
                    mesh.GetVertices(vertices);
                    foreach (var vertex in vertices) Encapsulate(inverseView * localToStage.MultiplyPoint3x4(vertex), ref projectedBounds, ref any);
                } else {
                    // Static imported meshes may disable CPU access; their mesh bounds are still geometric bounds.
                    for (int i = 0; i < 8; i++) {
                        var point = mesh.bounds.center + Vector3.Scale(mesh.bounds.extents,
                            new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                        Encapsulate(inverseView * localToStage.MultiplyPoint3x4(point), ref projectedBounds, ref any);
                    }
                }
            }
            if (!any) return;
            float distance = Mathf.Max(2, projectedBounds.size.magnitude * 1.6f);
            camera.transform.rotation = viewRotation;
            camera.transform.position = stage.transform.position + viewRotation * (projectedBounds.center + Vector3.back * distance);
            float extent = Mathf.Max(projectedBounds.extents.y,projectedBounds.extents.x / camera.aspect);
            camera.orthographicSize = Mathf.Max(.1f, extent * 1.08f);
            camera.farClipPlane = Mathf.Max(20, distance + projectedBounds.size.magnitude * 2);
            camera.enabled = true;
        }
        // URP renders the new pose once; keep the texture until the next 10 Hz bake.
        void OnRendered(ScriptableRenderContext context, Camera renderedCamera) {
            if (renderedCamera == camera) camera.enabled = false;
        }
        public void Clear() {
            RenderPipelineManager.endCameraRendering -= OnRendered;
            source = null; metadata = null;
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            foreach (var part in parts) Destroy(part.Mesh);
            parts.Clear();
            if (texture != null) { texture.Release(); Destroy(texture); }
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            stage = null; camera = null; texture = null;
        }
        public void Dispose() => Clear();
        static void Encapsulate(Vector3 point, ref Bounds bounds, ref bool any) {
            if (!any) { bounds = new Bounds(point, Vector3.zero); any = true; }
            else bounds.Encapsulate(point);
        }
        static void Destroy(UnityEngine.Object value) { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
