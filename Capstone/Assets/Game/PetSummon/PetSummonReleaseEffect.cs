using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.Rendering;

namespace Capstone.Game.PetSummon
{
    [DisallowMultipleComponent]
    public sealed class PetSummonReleaseEffect : MonoBehaviour
    {
        [SerializeField] PetVFXProfile vfxProfile;
        [SerializeField] PetVFXQuality quality = PetVFXQuality.Medium;
        [SerializeField] Shader bodyGlowShader;
        [SerializeField, Min(.1f)] float presentationSpeed = 1f;

        sealed class BodyBinding
        {
            public Renderer source, glow;
            public Bounds localBounds, poseBounds;
            public bool enabledBefore, offscreenBefore;
            public readonly List<Material> sources = new List<Material>();
            public readonly List<Vector3> poseVertices = new List<Vector3>();
            public Material[] materials;
        }

        static class ShaderIds
        {
            public static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
            public static readonly int BodyCenter = Shader.PropertyToID("_BodyCenter");
            public static readonly int PresentationCenter = Shader.PropertyToID("_PresentationCenter");
            public static readonly int PresentationScale = Shader.PropertyToID("_PresentationScale");
            public static readonly int WhiteBlend = Shader.PropertyToID("_WhiteBlend");
            public static readonly int GlowEmission = Shader.PropertyToID("_GlowEmission");
            public static readonly int DissolveAmount = Shader.PropertyToID("_DissolveAmount");
            public static readonly int DissolveDirection = Shader.PropertyToID("_DissolveDirection");
            public static readonly int EdgeWidth = Shader.PropertyToID("_EdgeWidth");
            public static readonly int EdgeEmission = Shader.PropertyToID("_EdgeEmission");
            public static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
            public static readonly int ReferenceWorldToLocal = Shader.PropertyToID("_ReferenceWorldToLocal");
            public static readonly int ReferenceCenter = Shader.PropertyToID("_ReferenceCenter");
            public static readonly int ReferenceRadius = Shader.PropertyToID("_ReferenceRadius");
            public static readonly int RecallBall = Shader.PropertyToID("_RecallBall");
            public static readonly int RecallAxis = Shader.PropertyToID("_RecallAxis");
            public static readonly int RecallWarp = Shader.PropertyToID("_RecallWarp");
        }

        readonly List<BodyBinding> bodies = new List<BodyBinding>();
        readonly Stack<BodyBinding> skinnedPool = new Stack<BodyBinding>();
        readonly Stack<BodyBinding> staticPool = new Stack<BodyBinding>();
        readonly List<Renderer> rendererBuffer = new List<Renderer>();
        readonly List<Renderer> hiddenBallRenderers = new List<Renderer>();
        readonly Dictionary<Material, Material> materialCache = new Dictionary<Material, Material>();
        MaterialPropertyBlock properties;
        PetSummonVfxInstance spawnInstance, recallInstance, activeInstance;
        Material defaultBodyMaterial;
        Mesh poseMesh;
        Transform poolRoot, releaseBall;
        SphereCollider ballCollider;
        PetController presentationPet;
        Animator presentationAnimator;
        AnimatorCullingMode cullingBefore;
        Vector3 bodyCenter, bodyHalfExtents, presentationCenter, spawnOrigin, haloHalfExtents;
        PetRecallWarp recallWarp;
        bool haloInitialized;
        float bodyRadius, cullingRadius, presentationScale = 1, captureProgress;
        float effectTime, dissolveAmount, whiteBlend, glowOpacity = 1;
        bool spawning, originalVisible, petHidden;
        int sequenceVersion;

        public PetVFXProfile Profile => vfxProfile;
        public PetVFXQuality Quality => quality;
        public Vector3 PresentationCenter => presentationCenter;
        public float PresentationScale => presentationScale;
        public float CaptureProgress => captureProgress;
        public PetSummonVfxInstance ActiveVfx => activeInstance;
        public int CachedMaterialCount => materialCache.Count;

        public void ConfigureProfile(PetVFXProfile profile) { vfxProfile = profile; }
        public void ConfigureQuality(PetVFXQuality preset) { quality = preset; }

#if UNITY_EDITOR
        public void PreviewAt(float seconds)
        {
            effectTime = seconds;
            UpdateBodyBounds();
            if (spawning) UpdateSpawn(); else UpdateRecall();
        }
#endif

        void Awake() { if (Application.isPlaying) Warmup(); }

        void Warmup()
        {
            properties ??= new MaterialPropertyBlock();
            if (poolRoot == null) {
                var go = new GameObject("PetSummon_VfxPool") {
                    hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave
                };
                poolRoot = go.transform;
            }
            if (spawnInstance == null) spawnInstance = CreateInstance(false);
            if (recallInstance == null) recallInstance = CreateInstance(true);
            if (poseMesh == null) poseMesh = new Mesh { name = "PetSummon_PoseBuffer", hideFlags = HideFlags.DontSave };
        }

        PetSummonVfxInstance CreateInstance(bool recall)
        {
            PetSummonVfxInstance prefab = vfxProfile != null
                ? (recall ? vfxProfile.recallPrefab : vfxProfile.spawnPrefab) : null;
            PetSummonVfxInstance instance;
            if (prefab != null) instance = Instantiate(prefab, poolRoot);
            else {
                var go = new GameObject(recall ? "RecallVFX" : "SpawnVFX");
                go.transform.SetParent(poolRoot, false);
                instance = go.AddComponent<PetSummonVfxInstance>(); instance.BuildLayout(recall);
            }
            instance.gameObject.hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
            instance.Warmup(vfxProfile); instance.gameObject.SetActive(false);
            return instance;
        }

        public IEnumerator PlaySummon(PetController pet, Vector3 spawnPoint, Transform ball, Action releasePet,
            Vector3? groundPoint = null)
        {
            Cancel();
            if (pet == null || ball == null) yield break;
            int version = sequenceVersion;
            try {
                Warmup(); spawning = true; presentationPet = pet; releaseBall = ball;
                ballCollider = ball.GetComponentInChildren<SphereCollider>(true);
                spawnOrigin = ResolveBallCenter(ball, ballCollider);
                releasePet?.Invoke();
                if (!pet.IsSummoned) yield break;
                pet.SetSummonPresentationPosition(spawnPoint);
                pet.StartSummonPresentationAnimation(presentationSpeed);
                BeginBody(pet);
                // A newly summoned Animator must evaluate its first real frame before sampling the skin.
                // Keep both body renderers hidden during that frame so the pet cannot flash at its landing.
                if (Application.isPlaying) {
                    yield return null;
                    if (version != sequenceVersion || pet == null || ball == null) yield break;
                }
                UpdateBodyBounds();
                presentationCenter = spawnOrigin; presentationScale = .04f;
                activeInstance = spawnInstance;
                activeInstance.Begin(false, vfxProfile, quality, spawnOrigin, bodyHalfExtents, presentationSpeed);
                activeInstance.SetGroundPoint(groundPoint ?? spawnPoint);
                HideBall(ball);
                float elapsed = 0;
                effectTime = 0;
                float duration = Mathf.Max(.1f,vfxProfile != null ? vfxProfile.spawnDuration : .8f) / Mathf.Max(.1f,presentationSpeed);
                UpdateSpawn();
                while (elapsed < duration && version == sequenceVersion) {
                    effectTime = .8f * Mathf.Clamp01(elapsed / duration);
                    yield return null;
                    if (version != sequenceVersion) yield break;
                    elapsed += Time.deltaTime;
                    if (pet == null || ball == null) yield break;
                }
                if (version != sequenceVersion || pet == null) yield break;
                effectTime = .8f; UpdateSpawn(); RestoreBody(); ReturnVfx();
                // VFX finishes independently; never stretch the flash or accelerate Cast Spell.
                float animationDeadline = Time.time + Mathf.Max(3,pet.SummonPresentationAnimationDuration + .5f);
                while (!pet.HasSummonPresentationAnimationFinished && Time.time < animationDeadline
                    && version == sequenceVersion && pet.IsSummoned) yield return null;
                if (version != sequenceVersion || !pet.IsSummoned) yield break;
                if (!pet.HasSummonPresentationAnimationFinished)
                    Debug.LogWarning("Pet summon animation exceeded its completion timeout.",pet);
                if (!pet.CompleteSummonPresentation(spawnPoint)) pet.Withdraw();
            }
            finally { if (version == sequenceVersion) RestoreAndReturn(); }
        }

        public IEnumerator PlayRecall(PetController pet, Transform ball, Action hidePet)
        {
            Cancel();
            if (pet == null || ball == null) yield break;
            int version = sequenceVersion;
            try {
                Warmup(); spawning = false; presentationPet = pet; releaseBall = ball;
                ballCollider = ball.GetComponentInChildren<SphereCollider>(true);
                pet.SetRecallPresentation(true);
                BeginBody(pet); UpdateBodyBounds();
                activeInstance = recallInstance;
                activeInstance.Begin(true,vfxProfile,quality,bodyCenter,bodyHalfExtents,presentationSpeed);
                float elapsed = 0;
                effectTime = 0;
                float duration = Mathf.Max(.1f,vfxProfile != null ? vfxProfile.recallDuration : .65f) / Mathf.Max(.1f,presentationSpeed);
                UpdateRecall();
                while (elapsed < duration && version == sequenceVersion) {
                    effectTime = .65f * Mathf.Clamp01(elapsed / duration);
                    if (effectTime >= .56f && !petHidden) { petHidden = true; hidePet?.Invoke(); }
                    yield return null;
                    if (version != sequenceVersion) yield break;
                    elapsed += Time.deltaTime;
                    if (pet == null || ball == null) yield break;
                }
                if (version != sequenceVersion) yield break;
                if (!petHidden) { petHidden = true; hidePet?.Invoke(); }
            }
            finally { if (version == sequenceVersion) RestoreAndReturn(); }
        }

        void LateUpdate()
        {
            if (presentationPet == null || activeInstance == null) return;
            UpdateBodyBounds();
            if (spawning) UpdateSpawn(); else UpdateRecall();
        }

        void UpdateSpawn()
        {
            float growth = Smooth(Mathf.Clamp01(effectTime / .46f));
            presentationScale = Mathf.Lerp(.04f,1,growth);
            presentationCenter = Vector3.Lerp(spawnOrigin,bodyCenter,growth);
            dissolveAmount = 1-Smooth(Mathf.InverseLerp(.04f,.20f,effectTime));
            whiteBlend = (vfxProfile != null ? vfxProfile.whiteBlend : .75f)
                * (1-Smooth(Mathf.InverseLerp(.46f,.72f,effectTime)));
            glowOpacity = Mathf.Clamp01((.8f-effectTime)/.16f);
            if (effectTime >= .72f) RestoreBody();
            ApplyBody();
            activeInstance?.UpdatePresentation(effectTime,presentationCenter,spawnOrigin,
                bodyHalfExtents*presentationScale,haloHalfExtents*presentationScale,
                presentationPet.transform.rotation,bodyCenter,bodyHalfExtents);
        }

        void UpdateRecall()
        {
            if (releaseBall == null) return;
            Vector3 ball = ResolveBallCenter(releaseBall,ballCollider);
            captureProgress = Smooth(Mathf.InverseLerp(.18f,.56f,effectTime));
            recallWarp = PetRecallWarp.Create(bodyCenter,ball,bodyHalfExtents,presentationPet.transform.rotation,
                effectTime,vfxProfile);
            float bodyCapture = recallWarp.enabled
                ? Smooth(Mathf.InverseLerp(.18f+recallWarp.delay,.56f,effectTime)) : captureProgress;
            presentationScale = 1-bodyCapture;
            presentationCenter = Vector3.Lerp(bodyCenter,ball,bodyCapture);
            dissolveAmount = Smooth(Mathf.InverseLerp(.40f,.56f,effectTime));
            whiteBlend = (vfxProfile != null ? vfxProfile.whiteBlend : .75f) * Mathf.Clamp01(effectTime/.08f);
            glowOpacity = 1;
            presentationPet?.SetRecallVisualCenter(presentationCenter);
            ApplyBody();
            activeInstance?.UpdatePresentation(effectTime,presentationCenter,ball,
                bodyHalfExtents*presentationScale,haloHalfExtents*presentationScale,
                presentationPet.transform.rotation,bodyCenter,bodyHalfExtents);
        }

        void BeginBody(PetController pet)
        {
            haloInitialized = false;
            presentationAnimator = pet.animator != null ? pet.animator : pet.GetComponentInChildren<Animator>();
            if (presentationAnimator != null) {
                cullingBefore = presentationAnimator.cullingMode;
                presentationAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                presentationAnimator.Update(0);
            }
            rendererBuffer.Clear(); pet.GetComponentsInChildren(true,rendererBuffer);
            PetEvolutionVisualController visual = pet.GetComponentInChildren<PetEvolutionVisualController>(true);
            for (int i=0;i<rendererBuffer.Count;i++) {
                Renderer source = rendererBuffer[i];
                if (source == null || !source.enabled || !source.gameObject.activeInHierarchy
                    || source.name == "PetSummon_BodyGlow" || source.GetComponentInParent<AAMAP.MapIcon>(true) != null
                    || source.GetComponentInParent<Canvas>(true) != null || source.GetComponentInParent<ParticleSystem>(true) != null
                    || visual != null && !visual.IsCurrentVisualRenderer(source)) continue;
                bool skinned = source is SkinnedMeshRenderer;
                Mesh mesh = skinned ? ((SkinnedMeshRenderer)source).sharedMesh
                    : source.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                Stack<BodyBinding> pool = skinned ? skinnedPool : staticPool;
                BodyBinding binding = pool.Count > 0 ? pool.Pop() : NewBody(skinned);
                binding.source = source; binding.enabledBefore = source.enabled;
                binding.offscreenBefore = source is SkinnedMeshRenderer savedSkin && savedSkin.updateWhenOffscreen;
                // Register before touching renderer state so a failed setup still returns the binding.
                bodies.Add(binding);
                binding.glow.transform.SetParent(source.transform,false);
                binding.glow.transform.localPosition = Vector3.zero;
                binding.glow.transform.localRotation = Quaternion.identity;
                binding.glow.transform.localScale = Vector3.one;
                binding.glow.gameObject.SetActive(true);
                binding.glow.enabled = false;
                if (skinned) {
                    var original = (SkinnedMeshRenderer)source; var glow = (SkinnedMeshRenderer)binding.glow;
                    original.updateWhenOffscreen = true;
                    glow.sharedMesh = mesh; glow.bones = original.bones; glow.rootBone = original.rootBone;
                    glow.quality = original.quality; glow.updateWhenOffscreen = true;
                    binding.localBounds = original.localBounds;
                    glow.localBounds = binding.localBounds;
                } else {
                    binding.glow.GetComponent<MeshFilter>().sharedMesh = mesh;
                    binding.localBounds = binding.poseBounds = mesh.bounds;
                    binding.poseVertices.Clear();
                    if (mesh.isReadable) mesh.GetVertices(binding.poseVertices);
                }
                binding.sources.Clear(); source.GetSharedMaterials(binding.sources);
                int materialCount = Mathf.Max(1,mesh.subMeshCount);
                if (binding.materials == null || binding.materials.Length != materialCount) binding.materials = new Material[materialCount];
                for (int m=0;m<materialCount;m++) binding.materials[m] = GetBodyMaterial(binding.sources.Count > 0
                    ? binding.sources[Mathf.Min(m,binding.sources.Count-1)] : null);
                binding.glow.sharedMaterials = binding.materials;
                source.enabled = false;
            }
            if (bodies.Count == 0) throw new InvalidOperationException("Pet has no current body renderer for summon presentation.");
        }

        void CapturePoseBounds()
        {
            for (int i=0;i<bodies.Count;i++) {
                BodyBinding binding=bodies[i];
                if (!(binding.source is SkinnedMeshRenderer skin)) continue;
                // Compensate renderer scale here; its world matrix applies it once in UpdateBodyBounds.
                skin.BakeMesh(poseMesh,true);
                poseMesh.RecalculateBounds();
                poseMesh.GetVertices(binding.poseVertices);
                binding.poseBounds=poseMesh.bounds;
                binding.localBounds.Encapsulate(binding.poseBounds);
                ((SkinnedMeshRenderer)binding.glow).localBounds=binding.localBounds;
            }
        }

        BodyBinding NewBody(bool skinned)
        {
            var go = new GameObject("PetSummon_BodyGlow") {
                hideFlags = Application.isPlaying ? HideFlags.None : HideFlags.DontSave
            };
            go.transform.SetParent(poolRoot,false);
            Renderer renderer;
            if (skinned) renderer = go.AddComponent<SkinnedMeshRenderer>();
            else { go.AddComponent<MeshFilter>(); renderer = go.AddComponent<MeshRenderer>(); }
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return new BodyBinding { glow = renderer };
        }

        Material GetBodyMaterial(Material source)
        {
            if (source != null && materialCache.TryGetValue(source,out Material existing)) return existing;
            if (source == null && defaultBodyMaterial != null) return defaultBodyMaterial;
            Shader shader = vfxProfile != null && vfxProfile.bodyShader != null ? vfxProfile.bodyShader
                : bodyGlowShader != null ? bodyGlowShader : Shader.Find("Capstone/PetSummonBodyGlow");
            if (shader == null) throw new InvalidOperationException("Pet materialize shader is missing.");
            var material = new Material(shader) { name = "PetSummon_CachedBodyGlow", hideFlags = HideFlags.DontSave };
            if (vfxProfile != null && vfxProfile.dissolveNoise != null)
                material.SetTexture("_DissolveNoise",vfxProfile.dissolveNoise);
            if (source != null) {
                string texture = source.HasProperty("_BaseMap") ? "_BaseMap" : source.HasProperty("_MainTex") ? "_MainTex" : null;
                if (texture != null) {
                    material.SetTexture("_BaseMap",source.GetTexture(texture));
                    material.SetTextureScale("_BaseMap",source.GetTextureScale(texture));
                    material.SetTextureOffset("_BaseMap",source.GetTextureOffset(texture));
                }
                string tint = source.HasProperty("_BaseColor") ? "_BaseColor" : source.HasProperty("_Color") ? "_Color" : null;
                if (tint != null) material.SetColor("_BaseColor",source.GetColor(tint));
                materialCache.Add(source,material);
            } else defaultBodyMaterial = material;
            return material;
        }

        void UpdateBodyBounds()
        {
            if (presentationPet == null) return;
            // Animation can move the skin's centre substantially during Cast Spell.
            // Sample the current pose, rather than carrying the first pose through the whole sequence.
            // The same mesh buffer is reused and only the current form is baked while VFX is active.
            CapturePoseBounds();
            Transform root = presentationPet.transform;
            // This frame has the pet's orientation but no scale: renderer matrices already include all parent scales.
            Matrix4x4 worldToFrame = Matrix4x4.TRS(root.position,root.rotation,Vector3.one).inverse;
            bool poseFound = false, cullingFound = false;
            Bounds posed = default, conservative = default;
            for (int i=0;i<bodies.Count;i++) {
                BodyBinding binding = bodies[i]; if (binding.source == null) continue;
                Matrix4x4 matrix = worldToFrame*binding.source.transform.localToWorldMatrix;
                if (binding.poseVertices.Count > 0) {
                    for (int v=0;v<binding.poseVertices.Count;v++) {
                        Vector3 point=matrix.MultiplyPoint3x4(binding.poseVertices[v]);
                        if (!poseFound) { posed=new Bounds(point,Vector3.zero); poseFound=true; }
                        else posed.Encapsulate(point);
                    }
                } else EncapsulateTransformedBounds(binding.poseBounds,matrix,ref posed,ref poseFound);
                EncapsulateTransformedBounds(binding.localBounds,matrix,ref conservative,ref cullingFound);
            }
            if (!poseFound) return;
            bodyCenter = root.position+root.rotation*posed.center;
            bodyHalfExtents = Vector3.Max(posed.extents,Vector3.one*.001f);
            // A box's half extents alone do not enclose its vertices in an ellipsoid.
            // Fit the posed surface first; padding and the transient 10% cap apply to that fit.
            float fitSquared=1;
            for (int i=0;i<bodies.Count;i++) {
                BodyBinding binding=bodies[i]; if (binding.source == null) continue;
                Matrix4x4 matrix=worldToFrame*binding.source.transform.localToWorldMatrix;
                for (int v=0;v<binding.poseVertices.Count;v++) {
                    Vector3 offset=matrix.MultiplyPoint3x4(binding.poseVertices[v])-posed.center;
                    Vector3 normalized=new Vector3(offset.x/bodyHalfExtents.x,offset.y/bodyHalfExtents.y,offset.z/bodyHalfExtents.z);
                    fitSquared=Mathf.Max(fitSquared,normalized.sqrMagnitude);
                }
                if (binding.poseVertices.Count==0) fitSquared=Mathf.Max(fitSquared,3);
            }
            Vector3 fittedHalo=bodyHalfExtents*Mathf.Sqrt(fitSquared);
            Vector3 targetHalo=fittedHalo*(1+Mathf.Clamp(vfxProfile != null ? vfxProfile.haloPadding : .05f,0,.1f));
            haloHalfExtents=haloInitialized
                ? PetRecallWarp.SmoothHalo(haloHalfExtents,targetHalo,fittedHalo,Application.isPlaying?Time.deltaTime:0)
                : targetHalo;
            haloInitialized=true;
            bodyRadius = Mathf.Max(.001f,bodyHalfExtents.magnitude)
                * (vfxProfile != null ? Mathf.Max(.1f,vfxProfile.effectScale) : 1);
            // Imported skin bounds remain conservative for culling, never for the flash's visible size.
            cullingRadius = Mathf.Max(bodyRadius,conservative.extents.magnitude
                + Vector3.Distance(conservative.center,posed.center));
        }

        static void EncapsulateTransformedBounds(Bounds bounds,Matrix4x4 matrix,ref Bounds total,ref bool found)
        {
            Vector3 min=bounds.min, max=bounds.max;
            for (int corner=0;corner<8;corner++) {
                Vector3 point=matrix.MultiplyPoint3x4(new Vector3((corner&1)==0?min.x:max.x,
                    (corner&2)==0?min.y:max.y,(corner&4)==0?min.z:max.z));
                if (!found) { total=new Bounds(point,Vector3.zero); found=true; } else total.Encapsulate(point);
            }
        }

        void ApplyBody()
        {
            if (presentationPet == null) return;
            Transform reference = presentationPet.transform;
            float rootScale = Mathf.Max(.0001f,Mathf.Max(reference.lossyScale.x,Mathf.Max(reference.lossyScale.y,reference.lossyScale.z)));
            Color edge = vfxProfile != null ? vfxProfile.primaryColor : new Color(.72f,1,1,1);
            Matrix4x4 referenceMatrix=reference.worldToLocalMatrix;
            Vector3 referenceCenter=referenceMatrix.MultiplyPoint3x4(bodyCenter);
            float emission=(vfxProfile != null ? vfxProfile.bodyEmission : .12f) * (spawning ? whiteBlend/.75f : 1);
            float edgeWidth=vfxProfile != null ? vfxProfile.edgeWidth : .035f;
            float edgeEmission=vfxProfile != null ? vfxProfile.edgeEmission : 1.5f;
            Bounds displayBounds=new Bounds(presentationCenter,Vector3.one*Mathf.Max(.005f,cullingRadius*2.2f*presentationScale));
            if (!spawning && recallWarp.enabled) {
                // A vertex can still be at the far side while the leading vertices reach the ball.
                // Rotation never increases an offset's length, so this corridor contains every warped vertex.
                displayBounds=new Bounds(bodyCenter,Vector3.one*cullingRadius*2.2f);
                displayBounds.Encapsulate(recallWarp.ball+Vector3.one*cullingRadius);
                displayBounds.Encapsulate(recallWarp.ball-Vector3.one*cullingRadius);
            }
            for (int i=0;i<bodies.Count;i++) {
                BodyBinding binding = bodies[i]; if (binding.glow == null || binding.source == null) continue;
                Renderer glow = binding.glow;
                glow.enabled = glowOpacity > .001f && !petHidden;
                if (!glow.enabled) continue;
                for (int m=0;m<binding.materials.Length;m++) {
                    properties.Clear(); binding.source.GetPropertyBlock(properties,m);
                    properties.SetColor(ShaderIds.GlowColor,new Color(1,1,1,glowOpacity));
                    properties.SetVector(ShaderIds.BodyCenter,bodyCenter);
                    properties.SetVector(ShaderIds.PresentationCenter,presentationCenter);
                    properties.SetFloat(ShaderIds.PresentationScale,presentationScale);
                    properties.SetFloat(ShaderIds.WhiteBlend,whiteBlend);
                    properties.SetFloat(ShaderIds.GlowEmission,emission);
                    properties.SetFloat(ShaderIds.DissolveAmount,dissolveAmount);
                    properties.SetFloat(ShaderIds.DissolveDirection,spawning ? 0 : 1);
                    properties.SetFloat(ShaderIds.EdgeWidth,edgeWidth);
                    properties.SetFloat(ShaderIds.EdgeEmission,edgeEmission);
                    properties.SetColor(ShaderIds.EdgeColor,edge);
                    properties.SetMatrix(ShaderIds.ReferenceWorldToLocal,referenceMatrix);
                    properties.SetVector(ShaderIds.ReferenceCenter,referenceCenter);
                    properties.SetFloat(ShaderIds.ReferenceRadius,bodyRadius/rootScale);
                    properties.SetVector(ShaderIds.RecallBall,recallWarp.ball);
                    properties.SetVector(ShaderIds.RecallWarp,new Vector4(!spawning && recallWarp.enabled ? 1 : 0,
                        effectTime,recallWarp.delay,recallWarp.twist));
                    // The axis's W component carries the extent of the original pose along the pull direction.
                    properties.SetVector(ShaderIds.RecallAxis,new Vector4(recallWarp.axis.x,recallWarp.axis.y,
                        recallWarp.axis.z,recallWarp.axialExtent));
                    glow.SetPropertyBlock(properties,m);
                }
                glow.bounds = displayBounds;
            }
        }

        void RestoreBody()
        {
            if (originalVisible) return;
            for (int i=0;i<bodies.Count;i++) {
                BodyBinding binding = bodies[i];
                if (binding.source != null) binding.source.enabled = binding.enabledBefore
                    && presentationPet != null && presentationPet.ShouldShowVisualRenderer(binding.source);
            }
            originalVisible = true;
        }

        void HideBall(Transform ball)
        {
            rendererBuffer.Clear(); ball.GetComponentsInChildren(true,rendererBuffer);
            for (int i=0;i<rendererBuffer.Count;i++) {
                Renderer renderer = rendererBuffer[i]; if (!renderer.enabled) continue;
                hiddenBallRenderers.Add(renderer); renderer.enabled = false;
            }
        }

        public static Vector3 ResolveBallCenter(Transform ball, SphereCollider collider)
            => collider != null ? collider.transform.TransformPoint(collider.center) : ball.position;

        public void Cancel() { sequenceVersion++; RestoreAndReturn(); }

        void ReturnVfx()
        {
            if (activeInstance == null) return;
            activeInstance.ResetPresentation(); activeInstance.gameObject.SetActive(false); activeInstance = null;
        }

        void RestoreAndReturn()
        {
            RestoreBody(); ReturnVfx();
            for (int i=0;i<bodies.Count;i++) {
                BodyBinding binding = bodies[i];
                if (binding.source is SkinnedMeshRenderer original) original.updateWhenOffscreen = binding.offscreenBefore;
                // The pet or its current model can be destroyed before the coroutine is cancelled.
                if (binding.glow == null) { binding.source=null; binding.sources.Clear(); continue; }
                binding.glow.enabled = false; binding.glow.gameObject.SetActive(false);
                binding.glow.transform.SetParent(poolRoot,false); binding.glow.ResetBounds();
                if (binding.glow is SkinnedMeshRenderer glow) {
                    glow.sharedMesh = null; glow.bones = Array.Empty<Transform>(); glow.rootBone = null; skinnedPool.Push(binding);
                } else { binding.glow.GetComponent<MeshFilter>().sharedMesh = null; staticPool.Push(binding); }
                binding.source = null; binding.sources.Clear();
            }
            bodies.Clear(); rendererBuffer.Clear();
            if (presentationAnimator != null) {
                presentationAnimator.cullingMode = cullingBefore;
            }
            presentationAnimator = null;
            for (int i=0;i<hiddenBallRenderers.Count;i++)
                if (hiddenBallRenderers[i] != null) hiddenBallRenderers[i].enabled = true;
            hiddenBallRenderers.Clear();
            if (presentationPet != null) {
                if (presentationPet.IsSummonPresentationPending) presentationPet.Withdraw();
                presentationPet.SetRecallPresentation(false);
            }
            presentationPet = null; releaseBall = null; ballCollider = null;
            bodyRadius = cullingRadius = captureProgress = dissolveAmount = effectTime = 0;
            bodyHalfExtents = Vector3.zero;
            presentationScale = glowOpacity = 1;
            originalVisible = petHidden = spawning = false;
        }

        void OnDisable() { Cancel(); }
        void OnDestroy()
        {
            Cancel();
            foreach (Material material in materialCache.Values) DestroyResource(material);
            materialCache.Clear(); DestroyResource(defaultBodyMaterial); DestroyResource(poseMesh);
            if (poolRoot != null) DestroyResource(poolRoot.gameObject);
        }

        static void DestroyResource(UnityEngine.Object item) {
            if (item == null) return;
            if (Application.isPlaying) Destroy(item); else DestroyImmediate(item);
        }
        static float Smooth(float x) { x = Mathf.Clamp01(x); return x*x*(3-2*x); }
    }

    // CPU reference for the vertex deformation and its bounds/Editor checks.
    // PetSummonBodyGlow uses the same equations; animation is evaluated before this presentation.
    public struct PetRecallWarp
    {
        public Vector3 center, ball, axis;
        public float time, delay, twist, axialExtent;
        public bool enabled;

        public static PetRecallWarp Create(Vector3 center,Vector3 ball,Vector3 extents,Quaternion rotation,
            float time,PetVFXProfile profile)
        {
            Vector3 delta=ball-center;
            float distance=delta.magnitude;
            Vector3 axis=distance>.00001f?delta/distance:Vector3.forward;
            float span=Mathf.Abs(Vector3.Dot(axis,rotation*Vector3.right))*extents.x
                + Mathf.Abs(Vector3.Dot(axis,rotation*Vector3.up))*extents.y
                + Mathf.Abs(Vector3.Dot(axis,rotation*Vector3.forward))*extents.z;
            float proximity=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.5f,distance/Mathf.Max(.001f,span)));
            return new PetRecallWarp {
                center=center,ball=ball,axis=axis,time=time,axialExtent=Mathf.Max(.001f,span),
                enabled=profile==null || profile.deformRecall,
                delay=Mathf.Clamp(profile!=null?profile.recallPullDelay:.08f,0,.08f)*proximity,
                twist=Mathf.Clamp(profile!=null?profile.recallTwistDegrees:60f,0,60)*Mathf.Deg2Rad*proximity
            };
        }

        public Vector3 TransformPoint(Vector3 point)
        {
            Vector3 offset=point-center;
            if (!enabled) {
                float uniform=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.56f,time));
                return Vector3.Lerp(center,ball,uniform)+offset*(1-uniform);
            }
            float axial=Vector3.Dot(offset,axis);
            float near=Mathf.Clamp01(.5f+.5f*axial/axialExtent);
            float pull=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f+delay*(1-near),.56f,time));
            Vector3 radial=offset-axis*axial;
            float angle=twist*pull*(.35f+.65f*near);
            radial=radial*Mathf.Cos(angle)+Vector3.Cross(axis,radial)*Mathf.Sin(angle);
            return Vector3.Lerp(center,ball,pull)+axis*axial*(1-pull)+radial*((1-pull)*(1-pull));
        }

        public static Vector3 SmoothHalo(Vector3 current,Vector3 target,Vector3 model,float deltaTime)
        {
            float ease=1-Mathf.Exp(-Mathf.Max(0,deltaTime)/.08f);
            return Vector3.Min(Vector3.Max(target,Vector3.Lerp(current,target,ease)),model*1.1f);
        }
    }
}
