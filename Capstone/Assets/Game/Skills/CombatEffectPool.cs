using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.Skills {
    /// <summary>Small scene-owned pool for persistent buff and projectile visuals.</summary>
    public static class CombatEffectPool {
        sealed class Item { public GameObject prefab, value; public ParticleSystem[] particles; public TrailRenderer[] trails; }
        static readonly List<Item> idle = new List<Item>(24);
        static readonly Dictionary<GameObject, Item> active = new Dictionary<GameObject, Item>();
        static Transform root;
        public static int IdleCount => idle.Count;
        public static int ActiveCount => active.Count;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { idle.Clear(); active.Clear(); root = null; }
        public static GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null) {
            if (prefab == null) return null;
            if (root == null) {
                idle.Clear(); active.Clear();
                root = new GameObject("Combat effect pool").transform;
            }
            Item item = null;
            for (int i = idle.Count - 1; i >= 0; i--) {
                if (idle[i].value == null) { idle.RemoveAt(i); continue; }
                if (idle[i].prefab != prefab) continue;
                item = idle[i]; idle.RemoveAt(i); break;
            }
            if (item == null) {
                var created = Object.Instantiate(prefab, root);
                item = new Item { prefab = prefab, value = created, particles = created.GetComponentsInChildren<ParticleSystem>(true), trails = created.GetComponentsInChildren<TrailRenderer>(true) };
            }
            var value = item.value;
            value.transform.SetParent(parent != null ? parent : root, false);
            value.transform.SetPositionAndRotation(position, rotation);
            value.transform.localScale = prefab.transform.localScale;
            value.SetActive(true); active[value] = item;
            foreach (var trail in item.trails) trail.Clear();
            foreach (var particle in item.particles) { particle.Clear(false); particle.Play(false); }
            return value;
        }
        public static void Return(GameObject value) {
            if (value == null) return;
            if (!active.TryGetValue(value, out var item)) { Object.Destroy(value); return; }
            active.Remove(value);
            foreach (var particle in item.particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            value.SetActive(false);
            if (root == null || idle.Count >= 24) { Object.Destroy(value); return; }
            value.transform.SetParent(root, false); idle.Add(item);
        }
    }

    public static class CombatAudioVoices {
        static readonly List<AudioSource> voices = new List<AudioSource>(8);
        static uint random = 0x6a09e667;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { voices.Clear(); random = 0x6a09e667; }
        public static float PitchVariation() {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return .95f + (random & 65535) / 65535f * .1f;
        }
        public static void Play(AudioSource voice) {
            for (int i = voices.Count - 1; i >= 0; i--) if (voices[i] == null || !voices[i].isPlaying || voices[i] == voice) voices.RemoveAt(i);
            if (voices.Count >= 8) { voices[0].Stop(); voices.RemoveAt(0); }
            voices.Add(voice); voice.Play();
        }
    }
}
