using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.WorldFeedback
{
    /// <summary>Whole-avatar close-up fade, scoped by CameraVisibility to the gameplay camera.</summary>
    internal sealed class CameraNearPlayerVisibility : IDisposable
    {
        sealed class Entry
        {
            public Renderer renderer;
            public Material[] original, applied, copies;
            public bool appliedNow, oldForceOff, criticalOnly;
        }

        static readonly int ActorFade = Shader.PropertyToID("_CapstoneActorFade");
        static readonly int SceneryFade = Shader.PropertyToID("_CapstoneCameraFade");
        readonly List<Entry> entries = new List<Entry>(16);
        readonly List<Material> current = new List<Material>(8);
        readonly Shader lit, simple, unlit;
        Transform root;
        bool cachedMaterials, rebuild;
        public float Fade { get; private set; }
        public int MaterialCount { get; private set; }
        public int RendererCount => entries.Count;

        internal CameraNearPlayerVisibility(Shader lit, Shader simple, Shader unlit)
        { this.lit = lit; this.simple = simple; this.unlit = unlit; }

        // Bounds distance protects heads/hair/outfits of both player models, rather
        // than assuming one body radius. Finish fading before the lens cuts the skin.
        internal static float DesiredFade(Bounds bounds, Vector3 cameraPosition, float lensRadius)
        {
            float hide = Mathf.Max(.25f, lensRadius + .08f);
            float distance = Mathf.Sqrt(bounds.SqrDistance(cameraPosition));
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hide, hide + .65f, distance));
        }

        internal void Update(Transform playerRoot, Camera camera, float deltaTime)
        {
            if (root != playerRoot || rebuild) Bind(playerRoot);
            if (camera == null || root == null) { Reset(); return; }
            bool found = false;
            Bounds bounds = default;
            foreach (var entry in entries)
            {
                var renderer = entry.renderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            if (!found) { Reset(); return; }
            float halfHeight = camera.nearClipPlane * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad);
            float radius = Mathf.Sqrt(halfHeight * halfHeight * (1 + camera.aspect * camera.aspect)
                + camera.nearClipPlane * camera.nearClipPlane);
            float desired = DesiredFade(bounds, camera.transform.position, radius);
            // A wall can move the camera inside the body in one frame. Safety must
            // be immediate there; only the surrounding transition is time-smoothed.
            Fade = desired >= .999f ? 1 : Mathf.MoveTowards(Fade, desired,
                Mathf.Max(0, deltaTime) / (desired > Fade ? .08f : .18f));
            if (Fade > .001f && !cachedMaterials) CacheMaterials();
        }

        void Bind(Transform next)
        {
            Dispose(); root = next; rebuild = false;
            if (root == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer)) continue;
                if (renderer.GetComponentInParent<Canvas>() != null
                    || renderer.GetComponentInParent<AAMAP.MapIcon>(true) != null
                    || renderer.GetComponentInParent<PetController>() != null) continue;
                entries.Add(new Entry { renderer = renderer });
            }
        }

        void CacheMaterials()
        {
            cachedMaterials = true;
            foreach (var entry in entries)
            {
                if (entry.renderer == null) continue;
                entry.original = entry.renderer.sharedMaterials;
                entry.applied = (Material[])entry.original.Clone();
                entry.copies = new Material[entry.original.Length];
                for (int i = 0; i < entry.original.Length; i++)
                {
                    var original = entry.original[i];
                    Shader adapter = original == null || original.shader == null ? null
                        : original.shader.name == "Universal Render Pipeline/Lit" ? lit
                        : original.shader.name == "Universal Render Pipeline/Simple Lit" ? simple
                        : original.shader.name == "Universal Render Pipeline/Unlit" ? unlit : null;
                    if (adapter == null) { entry.criticalOnly = true; continue; }
                    var copy = new Material(original) { shader = adapter,
                        name = original.name + " (Near player)", hideFlags = HideFlags.HideAndDontSave };
                    copy.shaderKeywords = original.shaderKeywords;
                    copy.renderQueue = original.renderQueue;
                    copy.SetFloat(SceneryFade, 0);
                    copy.SetFloat(ActorFade, 0);
                    entry.copies[i] = entry.applied[i] = copy;
                    MaterialCount++;
                }
            }
        }

        bool Matches(Material[] materials)
        {
            if (materials == null || current.Count != materials.Length) return false;
            for (int i = 0; i < current.Count; i++) if (current[i] != materials[i]) return false;
            return true;
        }

        internal void Apply()
        {
            if (Fade <= .001f || !cachedMaterials) return;
            foreach (var entry in entries)
            {
                var renderer = entry.renderer;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                current.Clear(); renderer.GetSharedMaterials(current);
                if (!Matches(entry.original)) { rebuild = true; continue; }
                entry.oldForceOff = renderer.forceRenderingOff;
                // Unknown/custom shaders stay authored; hide them only at critical
                // proximity rather than converting them to a wrong generic material.
                if (entry.criticalOnly && Fade >= .999f) renderer.forceRenderingOff = true;
                foreach (var copy in entry.copies) if (copy != null) copy.SetFloat(ActorFade, Fade);
                renderer.sharedMaterials = entry.applied;
                entry.appliedNow = true;
            }
        }

        internal void Restore()
        {
            foreach (var entry in entries)
            {
                if (!entry.appliedNow || entry.renderer == null) continue;
                current.Clear(); entry.renderer.GetSharedMaterials(current);
                if (Matches(entry.applied)) entry.renderer.sharedMaterials = entry.original;
                entry.renderer.forceRenderingOff = entry.oldForceOff;
                entry.appliedNow = false;
            }
        }

        internal void Reset() { Restore(); Fade = 0; }
        public void Dispose()
        {
            Reset();
            foreach (var entry in entries)
                if (entry.copies != null) foreach (var material in entry.copies)
                    if (material != null) UnityEngine.Object.Destroy(material);
            entries.Clear(); root = null; cachedMaterials = false; MaterialCount = 0;
        }
    }
}
