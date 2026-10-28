using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;

namespace Capstone.Game.Skills {
    [Serializable]
    public sealed class PetSkillSocket {
        public string key;
        public Transform anchor;
    }

    /// <summary>Presentation only: no projectile simulation, hit detection or damage. All resources have bounded lifetimes.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PetHudRuntimeStats))]
    public sealed class PetSkillPresentationPlayer : MonoBehaviour {
        public List<PetSkillSocket> sockets = new List<PetSkillSocket>();
        [SerializeField, Range(1, 32)] int maxLiveEffects = 12;
        [SerializeField, Range(0, 24)] int maxPooledEffects = 8;
        PetHudRuntimeStats stats;
        PetController pet;
        Animator boundAnimator;
        Transform effectsRoot;
        readonly List<Cast> casts = new List<Cast>(8);
        readonly List<Playback> playing = new List<Playback>(12);
        readonly List<Playback> pool = new List<Playback>(8);
        int nextToken;
        public int ActiveCastId { get; private set; }
        public int LiveEffectCount => playing.Count;
        public int PooledEffectCount => pool.Count;
        public int ReleasedCount { get; private set; }
        public event Action<int, string> SkillReleased;

        sealed class Cast {
            public int token;
            public SkillHudData skill;
            public PetSkillPresentationProfile profile;
            public float animationStart, releaseAt, animationEnd, expiresAt;
            public bool released;
        }
        sealed class Playback {
            public GameObject prefab, root;
            public ParticleSystem[] particles;
            public VisualEffect[] graphs;
            public AudioSource[] embeddedAudio;
            public AudioSource audio;
            public int token;
            public bool castCue;
            public float expiresAt;
            public Transform follow;
            public Vector3 offset;
            public Quaternion rotation;
        }

        void OnEnable() {
            stats = GetComponent<PetHudRuntimeStats>(); pet = GetComponentInParent<PetController>();
            stats.SkillAnimationAccepted -= Begin;
            stats.SkillAnimationAccepted += Begin;
            BindAnimator(stats.VisualAnimator);
        }
        void OnDisable() {
            if (stats != null) stats.SkillAnimationAccepted -= Begin;
            CancelPresentation();
        }
        void OnDestroy() {
            foreach (var item in pool) if (item.root != null) Destroy(item.root);
            pool.Clear();
            if (effectsRoot != null) Destroy(effectsRoot.gameObject);
        }
        public void BindAnimator(Animator animator) {
            boundAnimator = animator;
            if (animator == null) return;
            var relay = animator.GetComponent<PetSkillAnimationRelay>();
            if (relay == null && Application.isPlaying) relay = animator.gameObject.AddComponent<PetSkillAnimationRelay>();
            if (relay != null) relay.Bind(this, animator);
        }
        void Begin(SkillHudData skill) {
            var profile = stats.GetSkillPresentation(skill.skillId);
            if (profile == null) return;
            // A replaced windup cannot emit a delayed release from the previous cast.
            for (int i = casts.Count - 1; i >= 0; i--) if (!casts[i].released) { StopOwned(casts[i].token, false); casts.RemoveAt(i); }
            if (casts.Count >= 8) { StopOwned(casts[0].token, false); casts.RemoveAt(0); }
            if (++nextToken <= 0) nextToken = 1;
            float start = Time.time + Mathf.Max(0, skill.windupSeconds);
            var cast = new Cast {
                token = nextToken, skill = skill, profile = profile, animationStart = start,
                releaseAt = start + Mathf.Max(0, profile.releaseDelaySeconds),
                animationEnd = start + Mathf.Max(0.05f, skill.animationDuration) + Mathf.Max(0, skill.recoverySeconds),
                expiresAt = start + Mathf.Max(0.05f, skill.animationDuration) + Mathf.Clamp(profile.impactWindowSeconds, 0.1f, 30)
            };
            casts.Add(cast); ActiveCastId = cast.token;
            PlayCue(profile.cast, cast, true, null, Quaternion.identity);
            BindAnimator(stats.VisualAnimator);
        }
        void Update() {
            if ((casts.Count > 0 || playing.Count > 0) && (stats == null || !stats.isActiveAndEnabled || (pet != null && !pet.CanReceiveCommands))) {
                CancelPresentation(); return;
            }
            float now = Time.time;
            for (int i = casts.Count - 1; i >= 0; i--) {
                var cast = casts[i];
                if (!cast.released && cast.profile.releaseMode == PetSkillReleaseMode.Timed && now >= cast.releaseAt && now <= cast.animationEnd) Release(cast);
                if (now >= cast.animationEnd) StopOwned(cast.token, true);
                if (now >= cast.expiresAt) { if (ActiveCastId == cast.token) ActiveCastId = 0; casts.RemoveAt(i); }
            }
            for (int i = playing.Count - 1; i >= 0; i--) {
                var item = playing[i];
                if (item.root == null || now >= item.expiresAt) { Return(i); continue; }
                if (item.follow != null) item.root.transform.SetPositionAndRotation(item.follow.TransformPoint(item.offset), item.follow.rotation * item.rotation);
            }
        }
        public bool NotifyRelease(int castId) {
            foreach (var cast in casts) if (cast.token == castId && !cast.released && Time.time >= cast.animationStart && Time.time <= cast.animationEnd) { Release(cast); return true; }
            return false;
        }
        public void NotifyAnimationRelease(Animator source, AnimatorStateInfo state) {
            if (source != boundAnimator) return;
            foreach (var cast in casts) {
                if (cast.token != ActiveCastId || cast.profile.releaseMode != PetSkillReleaseMode.AnimationEvent) continue;
                if (cast.skill.animatorStates == null) return;
                foreach (var name in cast.skill.animatorStates) if (!string.IsNullOrWhiteSpace(name) && (state.IsName(name) || state.IsName("Base Layer." + name))) { NotifyRelease(cast.token); return; }
            }
        }
        void Release(Cast cast) {
            cast.released = true; StopOwned(cast.token, true);
            PlayCue(cast.profile.release, cast, false, null, Quaternion.identity);
            ReleasedCount++; SkillReleased?.Invoke(cast.token, cast.skill.skillId);
        }
        /// <summary>The real collision/combat handler must supply the token from SkillReleased. A token can hit once.</summary>
        public bool NotifyImpact(int castId, Vector3 position, Vector3 normal) {
            for (int i = 0; i < casts.Count; i++) {
                var cast = casts[i];
                if (cast.token != castId || !cast.released || Time.time > cast.expiresAt) continue;
                PlayCue(cast.profile.impact, cast, false, position, normal.sqrMagnitude > 0.001f ? Quaternion.FromToRotation(Vector3.up, normal.normalized) : Quaternion.identity);
                casts.RemoveAt(i); if (ActiveCastId == castId) ActiveCastId = 0; return true;
            }
            return false;
        }
        public void CancelPresentation() {
            casts.Clear(); ActiveCastId = 0;
            for (int i = playing.Count - 1; i >= 0; i--) Return(i);
        }
        Transform ResolveSocket(string key) {
            if (!string.IsNullOrWhiteSpace(key) && sockets != null) foreach (var socket in sockets)
                if (socket != null && socket.anchor != null && string.Equals(socket.key, key, StringComparison.OrdinalIgnoreCase)) return socket.anchor;
            return transform;
        }
        void PlayCue(PetSkillCue cue, Cast cast, bool castCue, Vector3? worldPosition, Quaternion worldRotation) {
            if (cue == null || (cue.effectPrefab == null && cue.sound == null)) return;
            if (playing.Count >= Mathf.Clamp(maxLiveEffects, 1, 32)) Return(0);
            Playback item = null;
            for (int i = pool.Count - 1; i >= 0; i--) if (pool[i].prefab == cue.effectPrefab && pool[i].root != null) { item = pool[i]; pool.RemoveAt(i); break; }
            if (item == null) item = CreatePlayback(cue.effectPrefab);
            var anchor = ResolveSocket(cue.socket);
            item.token = cast.token; item.castCue = castCue;
            item.expiresAt = Time.time + Mathf.Clamp(cue.lifetimeSeconds, 0.05f, 30);
            item.follow = !worldPosition.HasValue && cue.followSocket ? anchor : null;
            item.offset = cue.localOffset; item.rotation = Quaternion.Euler(cue.localEulerAngles);
            item.root.transform.SetPositionAndRotation(worldPosition.HasValue ? worldPosition.Value + worldRotation * cue.localOffset : anchor.TransformPoint(cue.localOffset), (worldPosition.HasValue ? worldRotation : anchor.rotation) * item.rotation);
            item.root.transform.localScale = Vector3.one * Mathf.Max(0.01f, cue.scale);
            item.root.SetActive(true);
            foreach (var particle in item.particles) { particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear); particle.Play(false); }
            foreach (var graph in item.graphs) graph.Reinit();
            item.audio.outputAudioMixerGroup = cast.profile.mixerGroup;
            item.audio.spatialBlend = cast.profile.spatialBlend;
            item.audio.minDistance = Mathf.Max(0.1f, cast.profile.minDistance);
            item.audio.maxDistance = Mathf.Max(item.audio.minDistance, cast.profile.maxDistance);
            item.audio.volume = Mathf.Clamp01(cue.volume); item.audio.pitch = Mathf.Clamp(cue.pitch, 0.1f, 3);
            item.audio.clip = cue.sound;
            if (cue.sound != null) item.audio.Play();
            playing.Add(item);
        }
        Playback CreatePlayback(GameObject prefab) {
            if (effectsRoot == null) {
                effectsRoot = new GameObject(name + " Skill Effects").transform;
                if (gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(effectsRoot.gameObject, gameObject.scene);
            }
            var root = new GameObject("Skill Cue"); root.SetActive(false); root.transform.SetParent(effectsRoot, false);
            GameObject visual = prefab != null ? Instantiate(prefab, root.transform, false) : null;
            if (visual != null) visual.SetActive(true);
            var audio = root.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.rolloffMode = AudioRolloffMode.Logarithmic;
            var result = new Playback {
                prefab = prefab, root = root, audio = audio,
                particles = root.GetComponentsInChildren<ParticleSystem>(true),
                graphs = root.GetComponentsInChildren<VisualEffect>(true),
                embeddedAudio = visual != null ? visual.GetComponentsInChildren<AudioSource>(true) : Array.Empty<AudioSource>()
            };
            return result;
        }
        void StopOwned(int token, bool castOnly) {
            for (int i = playing.Count - 1; i >= 0; i--) if (playing[i].token == token && (!castOnly || playing[i].castCue)) Return(i);
        }
        void Return(int index) {
            var item = playing[index]; playing.RemoveAt(index);
            if (item.root == null) return;
            foreach (var particle in item.particles) if (particle != null) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var graph in item.graphs) if (graph != null) graph.Stop();
            foreach (var audio in item.embeddedAudio) if (audio != null) audio.Stop();
            item.audio.Stop(); item.audio.clip = null; item.follow = null; item.root.SetActive(false);
            if (pool.Count < Mathf.Clamp(maxPooledEffects, 0, 24)) pool.Add(item);
            else Destroy(item.root);
        }
    }
}
