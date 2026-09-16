using System;
using System.Collections;
using UnityEngine;

namespace Capstone.Game.PetSummon
{
    [DisallowMultipleComponent]
    public sealed class PetSummonReleaseEffect : MonoBehaviour
    {
        [Header("Colour")]
        [SerializeField] Color beamColor = new Color(0.2f, 1f, 0.58f, 0.72f);
        [SerializeField] Color coreColor = new Color(0.86f, 1f, 0.94f, 1f);

        [Header("Timing")]
        [SerializeField, Min(0.05f)] float beamBuildDuration = 0.18f;
        [SerializeField, Min(0.05f)] float revealDuration = 0.48f;
        [SerializeField, Min(0.05f)] float recallBeamBuildDuration = 0.16f;
        [SerializeField, Min(0.05f)] float recallDuration = 0.46f;
        [SerializeField, Min(0f)] float settleDuration = 0.12f;

        [Header("Shape")]
        [SerializeField, Min(0.01f)] float beamWidth = 0.16f;
        [SerializeField, Min(0f)] float beamWobble = 0.055f;
        [SerializeField, Range(0.01f, 1f)] float initialHorizontalScale = 0.12f;
        [SerializeField, Range(0.01f, 1f)] float initialVerticalScale = 0.04f;
        [SerializeField, Min(0f)] float beamTargetHeight = 0.06f;
        [SerializeField, Min(0f)] float particleHeight = 0.42f;
        [SerializeField, Range(0, 64)] int burstParticleCount = 26;

        [Header("Energy Motion")]
        [SerializeField, Min(0f)] float energyScrollSpeed = 2.8f;
        [SerializeField, Range(16, 128)] int noiseTextureSize = 64;
        [SerializeField, Range(1f, 12f)] float noiseTiling = 4.5f;
        [SerializeField, Range(0f, 0.35f)] float distortionAmount = 0.14f;

        Transform scaledPet;
        Vector3 originalPetScale;
        GameObject effectRoot;
        Material effectMaterial;
        Texture2D effectNoiseTexture;

        public IEnumerator PlaySummon(PetController pet, Transform beamSource, Vector3 spawnPoint,
            Action releasePet)
        {
            Cancel();
            if (pet == null)
            {
                yield break;
            }

            scaledPet = pet.transform;
            originalPetScale = scaledPet.localScale;
            scaledPet.localScale = SqueezedScale(originalPetScale);

            LineRenderer aura = null;
            LineRenderer core = null;
            Light releaseLight = null;
            ParticleSystem particles = null;
            CreateVisuals(spawnPoint, out aura, out core, out releaseLight, out particles);

            Vector3 target = spawnPoint + Vector3.up * beamTargetHeight;
            try
            {
                float elapsed = 0f;
                while (elapsed < beamBuildDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / beamBuildDuration);
                    float eased = Smooth01(t);
                    Vector3 source = ResolveSourcePosition(beamSource, target);
                    Vector3 currentTarget = Vector3.Lerp(source, target, eased);
                    ScrollEnergy(Time.time, 1f);
                    UpdateBeam(aura, source, currentTarget, eased, Time.time, 0f);
                    UpdateBeam(core, source, currentTarget, eased, Time.time, 1.7f);
                    SetLight(releaseLight, Mathf.Sin(t * Mathf.PI) * 1.8f);
                    yield return null;
                }

                releasePet?.Invoke();
                particles?.Play(true);

                elapsed = 0f;
                while (elapsed < revealDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / revealDuration);
                    float eased = EaseOutBack(t);
                    if (scaledPet != null)
                    {
                        scaledPet.localScale = DistortedScale(originalPetScale,
                            Mathf.LerpUnclamped(initialHorizontalScale, 1f, eased),
                            Mathf.Lerp(initialVerticalScale, 1f, Smooth01(t)), t, 1f - t);
                    }

                    Vector3 source = ResolveSourcePosition(beamSource, target);
                    float fade = 1f - Smooth01(t);
                    ScrollEnergy(Time.time, 1f);
                    UpdateBeam(aura, source, target, fade, Time.time, 0f);
                    UpdateBeam(core, source, target, Mathf.Sqrt(fade), Time.time, 1.7f);
                    SetLight(releaseLight, fade * 2.2f);
                    yield return null;
                }

                if (scaledPet != null)
                {
                    scaledPet.localScale = originalPetScale;
                }

                if (settleDuration > 0f)
                {
                    yield return new WaitForSeconds(settleDuration);
                }
            }
            finally
            {
                if (scaledPet != null)
                {
                    scaledPet.localScale = originalPetScale;
                }

                CleanupVisuals();
                scaledPet = null;
            }
        }

        public IEnumerator PlayRecall(PetController pet, Transform beamTarget, Action hidePet)
        {
            Cancel();
            if (pet == null || beamTarget == null)
            {
                yield break;
            }

            scaledPet = pet.transform;
            originalPetScale = scaledPet.localScale;

            LineRenderer aura = null;
            LineRenderer core = null;
            Light recallLight = null;
            ParticleSystem particles = null;
            CreateVisuals(scaledPet.position, out aura, out core, out recallLight, out particles);
            SetRecallGradient(aura, beamColor);
            SetRecallGradient(core, coreColor);
            particles?.Play(true);

            try
            {
                float elapsed = 0f;
                while (elapsed < recallBeamBuildDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / recallBeamBuildDuration);
                    float eased = Smooth01(t);
                    Vector3 source = ResolvePetCenter(pet);
                    Vector3 target = beamTarget.position;
                    Vector3 currentTarget = Vector3.Lerp(source, target, eased);
                    ScrollEnergy(Time.time, -1f);
                    UpdateBeam(aura, source, currentTarget, eased, Time.time, 0f);
                    UpdateBeam(core, source, currentTarget, eased, Time.time, 1.7f);
                    MoveAndSetLight(recallLight, currentTarget,
                        Mathf.Sin(t * Mathf.PI * 0.5f) * 1.8f);
                    yield return null;
                }

                elapsed = 0f;
                while (elapsed < recallDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / recallDuration);
                    float eased = Smooth01(t);
                    if (scaledPet != null)
                    {
                        scaledPet.localScale = DistortedScale(originalPetScale,
                            Mathf.Lerp(1f, initialHorizontalScale, eased),
                            Mathf.Lerp(1f, initialVerticalScale, eased), t, t);
                    }

                    Vector3 source = ResolvePetCenter(pet);
                    Vector3 target = beamTarget.position;
                    float pulse = 0.72f + Mathf.Sin(Time.time * 24f) * 0.18f;
                    ScrollEnergy(Time.time, -1f);
                    UpdateBeam(aura, source, target, pulse, Time.time, 0f);
                    UpdateBeam(core, source, target, 1f, Time.time, 1.7f);
                    MoveAndSetLight(recallLight, target, Mathf.Lerp(1.8f, 2.6f, eased));
                    yield return null;
                }

                hidePet?.Invoke();
            }
            finally
            {
                if (scaledPet != null)
                {
                    scaledPet.localScale = originalPetScale;
                }

                CleanupVisuals();
                scaledPet = null;
            }
        }

        public void Cancel()
        {
            if (scaledPet != null)
            {
                scaledPet.localScale = originalPetScale;
                scaledPet = null;
            }

            CleanupVisuals();
        }

        void OnDisable()
        {
            Cancel();
        }

        void OnDestroy()
        {
            Cancel();
            ReleaseCachedResources();
        }

        void CreateVisuals(Vector3 spawnPoint, out LineRenderer aura, out LineRenderer core,
            out Light releaseLight, out ParticleSystem particles)
        {
            effectRoot = new GameObject("PetSummon_ReleaseFx");
            effectRoot.transform.position = spawnPoint;
            EnsureEffectResources();

            aura = CreateLine("Aura", beamColor, beamWidth, effectMaterial);
            core = CreateLine("Core", coreColor, beamWidth * 0.34f, effectMaterial);

            releaseLight = effectRoot.AddComponent<Light>();
            releaseLight.type = LightType.Point;
            releaseLight.color = beamColor;
            releaseLight.range = 3f;
            releaseLight.intensity = 0f;
            releaseLight.shadows = LightShadows.None;

            particles = CreateParticles(effectMaterial);
        }

        LineRenderer CreateLine(string lineName, Color color, float width, Material material)
        {
            GameObject lineObject = new GameObject(lineName);
            lineObject.transform.SetParent(effectRoot.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 12;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, 0f);
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 2;
            line.textureMode = LineTextureMode.Tile;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            if (material != null)
            {
                line.sharedMaterial = material;
            }

            return line;
        }

        ParticleSystem CreateParticles(Material material)
        {
            GameObject particleObject = new GameObject("Burst");
            particleObject.transform.SetParent(effectRoot.transform, false);
            particleObject.transform.localPosition = Vector3.up * particleHeight;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.7f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.45f, 1.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.11f);
            main.startColor = new ParticleSystem.MinMaxGradient(coreColor, beamColor);
            main.maxParticles = Mathf.Max(8, burstParticleCount + 4);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            if (burstParticleCount > 0)
            {
                emission.SetBursts(new[]
                {
                    new ParticleSystem.Burst(0f, (short)burstParticleCount)
                });
            }

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.18f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(coreColor, 0f),
                    new GradientColorKey(beamColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            ParticleSystemRenderer renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }

            return particles;
        }

        void EnsureEffectResources()
        {
            if (effectNoiseTexture == null)
            {
                effectNoiseTexture = CreateNoiseTexture();
            }

            if (effectMaterial == null)
            {
                effectMaterial = CreateEffectMaterial();
            }
        }

        Material CreateEffectMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                return null;
            }

            Material material = new Material(shader)
            {
                name = "PetSummon_RuntimeFx",
                color = coreColor,
                renderQueue = 3000,
                hideFlags = HideFlags.DontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", coreColor);
            }

            if (effectNoiseTexture != null)
            {
                if (material.HasProperty("_MainTex"))
                {
                    material.SetTexture("_MainTex", effectNoiseTexture);
                    material.SetTextureScale("_MainTex", new Vector2(noiseTiling, 1f));
                }

                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", effectNoiseTexture);
                    material.SetTextureScale("_BaseMap", new Vector2(noiseTiling, 1f));
                }
            }

            return material;
        }

        Texture2D CreateNoiseTexture()
        {
            int size = Mathf.Clamp(noiseTextureSize, 16, 128);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            {
                name = "PetSummon_ProceduralNoise",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)size;
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size;
                    float broad = TileablePerlin(u, v, 3f, 11.7f, 37.1f);
                    float detail = TileablePerlin(u, v, 7f, 53.4f, 19.8f);
                    float noise = Mathf.Clamp01(broad * 0.7f + detail * 0.3f);
                    float softEdge = SoftEdge(u) * SoftEdge(v);
                    float alpha = Mathf.SmoothStep(0.22f, 0.82f, noise) * softEdge;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static float TileablePerlin(float u, float v, float frequency, float offsetX, float offsetY)
        {
            float x = u * frequency;
            float y = v * frequency;
            float x0 = Mathf.PerlinNoise(x + offsetX, y + offsetY);
            float x1 = Mathf.PerlinNoise(x - frequency + offsetX, y + offsetY);
            float y0 = Mathf.PerlinNoise(x + offsetX, y - frequency + offsetY);
            float xy = Mathf.PerlinNoise(x - frequency + offsetX, y - frequency + offsetY);
            float blendX0 = Mathf.Lerp(x0, x1, u);
            float blendX1 = Mathf.Lerp(y0, xy, u);
            return Mathf.Lerp(blendX0, blendX1, v);
        }

        static float SoftEdge(float value)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(value / 0.12f))
                * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - value) / 0.12f));
        }

        void ScrollEnergy(float time, float direction)
        {
            if (effectMaterial == null)
            {
                return;
            }

            Vector2 offset = new Vector2(time * energyScrollSpeed * direction, 0f);
            if (effectMaterial.HasProperty("_MainTex"))
            {
                effectMaterial.SetTextureOffset("_MainTex", offset);
            }

            if (effectMaterial.HasProperty("_BaseMap"))
            {
                effectMaterial.SetTextureOffset("_BaseMap", offset);
            }
        }

        void UpdateBeam(LineRenderer line, Vector3 start, Vector3 end, float intensity,
            float time, float phase)
        {
            if (line == null)
            {
                return;
            }

            intensity = Mathf.Clamp01(intensity);
            line.enabled = intensity > 0.001f;
            line.widthMultiplier = (line.name == "Core" ? beamWidth * 0.34f : beamWidth) * intensity;

            Vector3 direction = end - start;
            Vector3 side = Vector3.Cross(direction.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.right;
            }
            else
            {
                side.Normalize();
            }

            int lastIndex = line.positionCount - 1;
            for (int i = 0; i < line.positionCount; i++)
            {
                float t = lastIndex > 0 ? i / (float)lastIndex : 0f;
                float envelope = Mathf.Sin(t * Mathf.PI);
                float wave = Mathf.Sin(t * 15f + time * 18f + phase);
                Vector3 point = Vector3.Lerp(start, end, t)
                    + side * (wave * beamWobble * envelope * intensity);
                line.SetPosition(i, point);
            }
        }

        static Vector3 ResolveSourcePosition(Transform source, Vector3 fallback)
        {
            return source != null ? source.position : fallback + Vector3.up * 0.25f;
        }

        static void SetLight(Light releaseLight, float intensity)
        {
            if (releaseLight != null)
            {
                releaseLight.intensity = Mathf.Max(0f, intensity);
            }
        }

        static void MoveAndSetLight(Light effectLight, Vector3 position, float intensity)
        {
            if (effectLight == null)
            {
                return;
            }

            effectLight.transform.position = position;
            effectLight.intensity = Mathf.Max(0f, intensity);
        }

        Vector3 ResolvePetCenter(PetController pet)
        {
            if (pet != null)
            {
                return pet.transform.position + Vector3.up * particleHeight;
            }

            return effectRoot != null
                ? effectRoot.transform.position + Vector3.up * particleHeight
                : Vector3.up * particleHeight;
        }

        static void SetRecallGradient(LineRenderer line, Color color)
        {
            if (line == null)
            {
                return;
            }

            line.startColor = new Color(color.r, color.g, color.b, 0.12f);
            line.endColor = color;
        }

        Vector3 SqueezedScale(Vector3 scale)
        {
            return new Vector3(
                scale.x * initialHorizontalScale,
                scale.y * initialVerticalScale,
                scale.z * initialHorizontalScale);
        }

        Vector3 DistortedScale(Vector3 scale, float horizontal, float vertical, float progress,
            float distortionWeight)
        {
            float flutter = Mathf.Sin(progress * Mathf.PI * 5f + Time.time * 8f)
                * distortionAmount * Mathf.Clamp01(distortionWeight);
            float x = Mathf.Max(0.01f, horizontal * (1f + flutter));
            float z = Mathf.Max(0.01f, horizontal * (1f - flutter));
            float stretch = 1f + Mathf.Abs(flutter) * 1.4f;
            return new Vector3(scale.x * x, scale.y * Mathf.Max(0.01f, vertical * stretch),
                scale.z * z);
        }

        void CleanupVisuals()
        {
            DestroyRuntimeObject(effectRoot);
            effectRoot = null;
        }

        void ReleaseCachedResources()
        {
            DestroyRuntimeObject(effectMaterial);
            DestroyRuntimeObject(effectNoiseTexture);
            effectMaterial = null;
            effectNoiseTexture = null;
        }

        static void DestroyRuntimeObject(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        static float EaseOutBack(float value)
        {
            value = Mathf.Clamp01(value) - 1f;
            const float overshoot = 1.35f;
            return 1f + (overshoot + 1f) * value * value * value
                + overshoot * value * value;
        }
    }
}
