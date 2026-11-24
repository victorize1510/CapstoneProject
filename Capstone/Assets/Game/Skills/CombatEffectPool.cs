using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Unity.Profiling;

namespace Capstone.Game.Skills {
    /// <summary>Small scene-owned pool for persistent buff and projectile visuals.</summary>
    public static class CombatEffectPool {
        sealed class Item {
            public GameObject prefab, value;
            public ParticleSystem[] particles;
            public TrailRenderer[] trails;
            public VisualEffect[] graphs;
        }
        static readonly List<Item> idle = new List<Item>(24);
        static readonly Dictionary<GameObject, Item> active = new Dictionary<GameObject, Item>();
        static readonly List<GameObject> dead = new List<GameObject>(64);
        static Transform root, inactiveRoot;
        public static int IdleCount => idle.Count;
        public static int ActiveCount => active.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() {
            idle.Clear(); active.Clear(); dead.Clear(); root = inactiveRoot = null;
            SceneManager.sceneUnloaded -= Prune; SceneManager.sceneUnloaded += Prune;
        }
        static void Prune(Scene scene) {
            dead.Clear(); foreach(var pair in active)if(pair.Value.value==null)dead.Add(pair.Key);
            foreach(var key in dead)active.Remove(key);
            for(int i=idle.Count-1;i>=0;i--)if(idle[i].value==null)idle.RemoveAt(i);
            dead.Clear();
        }
        public static GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) {
            if (prefab == null) return null;
            // Visual-only backpressure. A dropped effect never cancels a projectile or changes damage.
            if (active.Count >= 64) return null;
            if (root == null) {
                idle.Clear(); active.Clear();
                root = new GameObject("Combat effect pool").transform;
                inactiveRoot = new GameObject("Inactive visuals").transform;
                inactiveRoot.SetParent(root, false);
                inactiveRoot.gameObject.SetActive(false);
            }
            Item item = null;
            for (int i = idle.Count - 1; i >= 0; i--) {
                if (idle[i].value == null) { idle.RemoveAt(i); continue; }
                if (idle[i].prefab != prefab) continue;
                item = idle[i]; idle.RemoveAt(i); break;
            }
            if (item == null) {
                var created = TryInstantiateVisual(prefab, inactiveRoot);
                if (created == null) return null;
                created.SetActive(false);
                DisableVisualColliders(created);
                item = new Item {
                    prefab = prefab, value = created,
                    particles = created.GetComponentsInChildren<ParticleSystem>(true),
                    trails = created.GetComponentsInChildren<TrailRenderer>(true),
                    graphs = created.GetComponentsInChildren<VisualEffect>(true)
                };
            }
            var value = item.value;
            value.transform.SetParent(parent != null ? parent : root, false);
            value.transform.SetPositionAndRotation(position, rotation);
            value.transform.localScale = prefab.transform.localScale;
            value.SetActive(true); active[value] = item;
            foreach (var trail in item.trails) trail.Clear();
            foreach (var graph in item.graphs) if (graph.isActiveAndEnabled) graph.Reinit();
            foreach (var particle in item.particles) { particle.Clear(false); particle.Play(false); }
            return value;
        }
        internal static GameObject TryInstantiateVisual(GameObject prefab, Transform parent)
        {
            if (prefab == null) return null;
            try
            {
                // The non-generic overload lets us validate prefab-variant references before casting.
                Object clone = Object.Instantiate((Object)prefab, parent, false);
                if (clone is GameObject visual) return visual;
                if (clone != null) Object.Destroy(clone);
                Debug.LogWarning($"Combat VFX '{prefab.name}' did not instantiate a GameObject.", prefab);
            }
            catch (System.Exception error)
            {
                Debug.LogWarning($"Combat VFX '{prefab.name}' could not be created: {error.Message}", prefab);
            }
            return null;
        }

        internal static void DisableVisualColliders(GameObject visual) {
            foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
        public static void PlayOneShot(GameObject prefab,Vector3 position,float seconds=2) {
            var value=Rent(prefab,position,Quaternion.identity);if(value==null)return;
            var lifetime=value.GetComponent<CombatEffectLifetime>()??value.AddComponent<CombatEffectLifetime>();
            lifetime.Arm(Mathf.Clamp(seconds,.1f,5));
        }
        public static void Return(GameObject value) {
            if (value == null) return;
            var lifetime=value.GetComponent<CombatEffectLifetime>();if(lifetime!=null)lifetime.enabled=false;
            if (!active.TryGetValue(value, out var item)) { Object.Destroy(value); return; }
            active.Remove(value);
            foreach (var particle in item.particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            value.SetActive(false);
            if (root == null || idle.Count >= 24) { Object.Destroy(value); return; }
            value.transform.SetParent(inactiveRoot, false); idle.Add(item);
        }
    }

    // Ton tai tren effect pool, khong phu thuoc actor vua bi an/huy.
    internal sealed class CombatEffectLifetime:MonoBehaviour {
        float expires;
        internal void Arm(float seconds){expires=Time.time+seconds;enabled=true;}
        void Update(){if(!Capstone.Game.UISystem.GameplayPause.IsPaused&&Time.time>=expires)CombatEffectPool.Return(gameObject);}
    }

    public static class CombatAudioVoices {
        static readonly List<AudioSource> voices = new List<AudioSource>(8);
        static uint random = 0x6a09e667;
        static readonly ProfilerMarker VoiceMarker = new ProfilerMarker("Capstone.Combat.Audio.Play");
        public static int LiveCount { get { int n=0;foreach(var source in voices)if(source!=null&&source.isPlaying)n++;return n; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { voices.Clear(); random = 0x6a09e667; }
        public static float PitchVariation() {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return .95f + (random & 65535) / 65535f * .1f;
        }
        public static void Play(AudioSource voice) {
            using (VoiceMarker.Auto()) {
            if (voice == null || voice.clip == null || !voice.isActiveAndEnabled) return;
            for (int i = voices.Count - 1; i >= 0; i--) if (voices[i] == null || !voices[i].isPlaying || voices[i] == voice) voices.RemoveAt(i);
            if (voices.Count >= 8) { voices[0].Stop(); voices.RemoveAt(0); }
            voices.Add(voice); voice.Play();
            }
        }
    }
}
