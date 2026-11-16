using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Unity.Profiling;

namespace Capstone.Game.WorldFeedback {
    /// <summary>Main-camera-only, opt-in/lazily cached dither. Restores original materials after each camera.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class CameraVisibility : MonoBehaviour {
        sealed class Entry {
            public Renderer Renderer; public Material[] Original, Faded, Applied; public float Amount, Desired, Touched; public bool IsApplied;
        }
        readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
        readonly Dictionary<Collider, CameraOccluder> colliderCache = new Dictionary<Collider, CameraOccluder>();
        readonly HashSet<Renderer> unsupported = new HashSet<Renderer>();
        readonly RaycastHit[] hits = new RaycastHit[48];
        readonly List<Entry> remove = new List<Entry>();
        readonly List<Material> current = new List<Material>(8);
        struct Scope { public Camera Camera; public Vector4 A, B; public float Enabled; }
        readonly Stack<Scope> scopes = new Stack<Scope>(4);
        BasicCameraFollow source; Camera cameraSource; PetCommandInput party; PlayerVisualSwitcher visuals;
        Shader shader, simpleShader, unlitShader; float pollAt, resolveAt;
        Vector4 focusA, focusB;
        CameraNearPlayerVisibility nearPlayer;
        public float NearPlayerFade => nearPlayer != null ? nearPlayer.Fade : 0;
        public int NearPlayerMaterialCount => nearPlayer != null ? nearPlayer.MaterialCount : 0;
        public int FadedRendererCount { get; private set; }
        public int CachedRendererCount => entries.Count;
        public int UnsupportedRendererCount { get; private set; }
        public int MaterialCloneCount { get; private set; }
        public Camera GameplayCamera => cameraSource;
        static readonly int Amount = Shader.PropertyToID("_CapstoneCameraFade");
        static readonly int View = Shader.PropertyToID("_CapstoneOcclusionView");
        static readonly ProfilerMarker ScanMarker = new ProfilerMarker("Capstone.Camera.OcclusionScan");
        static readonly ProfilerMarker MaterialMarker = new ProfilerMarker("Capstone.Camera.MaterialScope");
        static readonly int FocusA = Shader.PropertyToID("_CapstoneFocusA");
        static readonly int FocusB = Shader.PropertyToID("_CapstoneFocusB");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Install() {
            SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }
        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Attach(); }
        static void Attach() {
            var camera = FindFirstObjectByType<BasicCameraFollow>();
            if (camera != null && camera.GetComponent<CameraVisibility>() == null) camera.gameObject.AddComponent<CameraVisibility>();
        }
        void Awake() {
            source = GetComponent<BasicCameraFollow>(); cameraSource = GetComponent<Camera>();
            shader = Resources.Load<Shader>("WorldFeedback/CameraDitherLit");
            simpleShader = Resources.Load<Shader>("WorldFeedback/CameraDitherSimpleLit");
            unlitShader = Resources.Load<Shader>("WorldFeedback/CameraDitherUnlit");
            nearPlayer = new CameraNearPlayerVisibility(shader, simpleShader, unlitShader);
        }
        void OnEnable() {
            RenderPipelineManager.beginCameraRendering += BeginCamera;
            RenderPipelineManager.endCameraRendering += EndCamera;
            SceneManager.activeSceneChanged += SceneChanged;
        }
        void OnDisable() {
            RenderPipelineManager.beginCameraRendering -= BeginCamera;
            RenderPipelineManager.endCameraRendering -= EndCamera;
            SceneManager.activeSceneChanged -= SceneChanged; Clear();
        }
        void SceneChanged(Scene previous, Scene current) { Clear(); cameraSource = null; party = null; visuals = null; resolveAt = 0; }
        void Clear() {
            nearPlayer?.Dispose();
            foreach (var entry in entries.Values) Dispose(entry);
            entries.Clear(); colliderCache.Clear(); unsupported.Clear();
            if (scopes.Count > 0) { Scope outer = default; while (scopes.Count > 0) outer = scopes.Pop(); SetGlobals(outer.A, outer.B, outer.Enabled); }
            FadedRendererCount = MaterialCloneCount = 0;
        }
        bool Same(Material[] expected) { if(current.Count!=expected.Length)return false;for(int i=0;i<expected.Length;i++)if(current[i]!=expected[i])return false;return true; }
        void Restore(Entry entry) {
            if (entry.IsApplied && entry.Renderer != null) {
                current.Clear(); entry.Renderer.GetSharedMaterials(current);
                if (Same(entry.Applied)) entry.Renderer.sharedMaterials = entry.Original;
            }
            entry.IsApplied = false;
        }
        void Dispose(Entry entry) {
            Restore(entry); foreach (var mat in entry.Faded) if (mat != null) { Destroy(mat); MaterialCloneCount--; }
        }
        void LateUpdate() {
            if (source == null || !source.enabled || shader == null || PetEvolutionPresentationController.AnyPlaying) {
                nearPlayer?.Reset();
                foreach (var entry in entries.Values) { entry.Desired = entry.Amount = 0; Restore(entry); } FadedRendererCount = 0; return;
            }
            if (Time.unscaledTime >= resolveAt) {
                resolveAt = Time.unscaledTime + 1;
                if (cameraSource == null) cameraSource = Camera.main;
                if (party == null) party = FindFirstObjectByType<PetCommandInput>();
                if (visuals == null && party != null) visuals = party.GetComponent<PlayerVisualSwitcher>();
            }
            if (cameraSource == null) return;
            var playerVisual = visuals != null ? visuals.ActiveVisualRoot : null;
            nearPlayer?.Update(playerVisual != null ? playerVisual.transform : source.target, cameraSource, Time.unscaledDeltaTime);
            Vector3 playerPoint = source.target != null ? source.target.position + Vector3.up * 1.1f : source.transform.position;
            if (party != null) playerPoint = party.transform.position + Vector3.up * 1.1f;
            var pet = party != null ? party.activePet : null;
            bool hasPet = pet != null && pet.CanReceiveCommands;
            Vector3 petPoint = hasPet ? pet.transform.position + Vector3.up * .8f : playerPoint;
            focusA = new Vector4(playerPoint.x, playerPoint.y, playerPoint.z, .65f);
            focusB = new Vector4(petPoint.x, petPoint.y, petPoint.z, hasPet ? .8f : 0);
            if (Time.unscaledTime >= pollAt) {
                using (ScanMarker.Auto()) {
                pollAt = Time.unscaledTime + .05f;
                foreach (var entry in entries.Values) entry.Desired = Time.unscaledTime - entry.Touched < .14f ? 1 : 0;
                CastTo(playerPoint); if (hasPet) CastTo(petPoint);
                }
            }
            FadedRendererCount = 0; remove.Clear();
            foreach (var entry in entries.Values) {
                entry.Amount = Mathf.MoveTowards(entry.Amount, entry.Desired, Time.unscaledDeltaTime / (entry.Desired > entry.Amount ? .15f : .25f));
                if (entry.Amount > .001f) FadedRendererCount++;
                if (entry.Renderer == null || entry.Amount <= 0 && Time.unscaledTime - entry.Touched > 20) remove.Add(entry);
            }
            foreach (var entry in remove) { Dispose(entry); entries.Remove(entry.Renderer); }
        }
        internal static bool Eligible(Renderer renderer) {
            if (!(renderer is MeshRenderer) || renderer.GetComponentInParent<PetController>() != null
                || renderer.GetComponentInParent<PetCommandInput>() != null || renderer.GetComponentInParent<CreatureCombatant>() != null
                || renderer.GetComponentInParent<Canvas>() != null || renderer.GetComponentInParent<ParticleSystem>() != null) return false;
            var set = renderer.GetComponentInParent<CameraOccluder>();
            return set == null || (!set.actor && set.kind != CameraOccluder.Kind.Ignore && set.kind != CameraOccluder.Kind.Glass);
        }
        CameraOccluder Resolve(Collider collider) {
            if (collider == null || collider is TerrainCollider || collider.GetComponentInParent<PetController>() != null
                || collider.GetComponentInParent<PetCommandInput>() != null || collider.GetComponentInParent<CreatureCombatant>() != null) return null;
            if (colliderCache.TryGetValue(collider, out var cached)) return cached;
            var explicitSet = collider.GetComponentInParent<CameraOccluder>();
            if (explicitSet != null) { colliderCache[collider] = explicitSet; return explicitSet; }
            if (colliderCache.Count >= 512) colliderCache.Clear();
            // Exported props often put their collider in a sibling Collision child.
            // Search a bounded prefab-sized ancestor, never an arbitrary whole scene hierarchy.
            Transform scope = collider.transform;
            var renderers = scope.GetComponentsInChildren<Renderer>();
            for (int level = 0; renderers.Length == 0 && level < 2 && scope.parent != null; level++) {
                scope = scope.parent;
                renderers = scope.GetComponentsInChildren<Renderer>();
            }
            if (renderers.Length == 0 || renderers.Length > 24) { colliderCache[collider] = null; return null; }
            var set = scope.GetComponent<CameraOccluder>(); if (set == null) set = scope.gameObject.AddComponent<CameraOccluder>(); set.renderers = renderers;
            set.actor = collider.GetComponentInParent<PetController>() != null;
            colliderCache[collider] = set; return set;
        }
        void CastTo(Vector3 point) {
            Vector3 start = cameraSource.transform.position, delta = point - start;
            if (delta.magnitude <= .15f) return;
            var ray = new Ray(start, delta.normalized);
            int count = Physics.SphereCastNonAlloc(start, .09f, ray.direction, hits, delta.magnitude - .12f, source.cameraCollisionMask, QueryTriggerInteraction.Ignore);
            TouchHits(count, ray, delta.magnitude);
            // Reverse cast sees a wall containing the camera; no indiscriminate camera-overlap fading.
            count = Physics.RaycastNonAlloc(point, -ray.direction, hits, delta.magnitude, source.cameraCollisionMask, QueryTriggerInteraction.Ignore);
            TouchHits(count, ray, delta.magnitude);
        }
        void TouchHits(int count, Ray ray, float distance) {
            for (int i = 0; i < count; i++) {
                var hit = hits[i]; if (hit.collider == null || Mathf.Abs(hit.normal.y) > .78f) continue;
                Touch(Resolve(hit.collider), ray, distance, hit.point);
            }
        }
        void Touch(CameraOccluder set, Ray ray, float distance, Vector3 hitPoint) {
            if (set == null || set.actor || set.kind == CameraOccluder.Kind.Ignore || set.kind == CameraOccluder.Kind.Glass) return;
            foreach (var renderer in set.Resolve()) {
                if (!Eligible(renderer) || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var bounds = renderer.bounds; bounds.Expand(.15f);
                if (!bounds.IntersectRay(ray, out float at) || at >= distance - .12f || bounds.SqrDistance(hitPoint) > .09f) continue;
                var entry = Cache(renderer, set.actor);
                if (entry == null) continue;
                entry.Desired = 1; entry.Touched = Time.unscaledTime;
            }
        }
        Entry Cache(Renderer renderer, bool actor) {
            if (renderer == null || !Eligible(renderer)) return null;
            if (unsupported.Contains(renderer)) return null;
            if (entries.TryGetValue(renderer, out var cached)) return cached;
            if (entries.Count >= 96) return null;
            var original = renderer.sharedMaterials;
            if (original.Length > 8) { unsupported.Add(renderer); UnsupportedRendererCount++; return null; }
            var replacement = new Material[original.Length]; bool supported = false;
            for (int i = 0; i < original.Length; i++) {
                var mat = original[i];
                bool compatible = mat != null && mat.shader != null && (mat.shader.name == "Universal Render Pipeline/Lit"
                    || mat.shader.name == "Universal Render Pipeline/Simple Lit" || mat.shader.name == "Universal Render Pipeline/Unlit");
                if (!compatible || mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > .5f) continue;
                Shader adapter = mat.shader.name == "Universal Render Pipeline/Unlit" ? unlitShader
                    : mat.shader.name == "Universal Render Pipeline/Simple Lit" ? simpleShader : shader;
                if (adapter == null) continue;
                var copy = new Material(mat) { shader = adapter, name = mat.name + " (Camera cutaway)", hideFlags = HideFlags.HideAndDontSave };
                copy.shaderKeywords = mat.shaderKeywords; copy.renderQueue = mat.renderQueue; copy.SetFloat(Amount, 0); MaterialCloneCount++;
                replacement[i] = copy; supported = true;
            }
            if (!supported) { UnsupportedRendererCount++; unsupported.Add(renderer); return null; }
            var applied = new Material[original.Length];
            for (int i = 0; i < original.Length; i++) applied[i] = replacement[i] != null ? replacement[i] : original[i];
            var entry = new Entry { Renderer = renderer, Original = original, Faded = replacement, Applied = applied, Touched = Time.unscaledTime };
            entries.Add(renderer, entry); return entry;
        }
        void BeginCamera(ScriptableRenderContext context, Camera camera) {
            using (MaterialMarker.Auto()) {
            scopes.Push(new Scope { Camera = camera, A = Shader.GetGlobalVector(FocusA), B = Shader.GetGlobalVector(FocusB), Enabled = Shader.GetGlobalFloat(View) });
            foreach (var entry in entries.Values) Restore(entry);
            nearPlayer?.Restore();
            bool main = camera == cameraSource && source != null && source.enabled && !PetEvolutionPresentationController.AnyPlaying;
            SetGlobals(focusA, focusB, main ? 1 : 0);
            if (main) { Apply(); nearPlayer?.Apply(); }
            }
        }
        static void SetGlobals(Vector4 a, Vector4 b, float enabled) { Shader.SetGlobalVector(FocusA,a); Shader.SetGlobalVector(FocusB,b); Shader.SetGlobalFloat(View,enabled); }
        void Apply() {
            remove.Clear();
            foreach (var entry in entries.Values) {
                if (entry.Renderer == null || !entry.Renderer.enabled || !entry.Renderer.gameObject.activeInHierarchy || entry.Amount <= .001f) continue;
                current.Clear(); entry.Renderer.GetSharedMaterials(current);
                if (!Same(entry.Original)) { remove.Add(entry); continue; }
                // Null unsupported slots retain their own original material.
                for (int i = 0; i < entry.Faded.Length; i++) if (entry.Faded[i] != null) entry.Faded[i].SetFloat(Amount, entry.Amount);
                entry.Renderer.sharedMaterials = entry.Applied;
                entry.IsApplied = true;
            }
            foreach (var e in remove) { Dispose(e); entries.Remove(e.Renderer); unsupported.Remove(e.Renderer); }
        }
        void EndCamera(ScriptableRenderContext context, Camera camera) {
            using (MaterialMarker.Auto()) {
                foreach (var entry in entries.Values) Restore(entry);
                nearPlayer?.Restore();
                if (scopes.Count == 0) return;
                var scope = scopes.Pop(); SetGlobals(scope.A,scope.B,scope.Enabled);
                if (scopes.Count > 0 && scopes.Peek().Camera == cameraSource) { Apply(); nearPlayer?.Apply(); }
            }
        }
    }
}
