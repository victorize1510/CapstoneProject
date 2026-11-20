using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.VFX;
using Unity.Profiling;
using Capstone.Game.Inventory;

namespace Capstone.Game.Skills
{
    [Serializable]
    public sealed class PetSkillSocket
    {
        public string key;
        public Transform anchor;
    }

    /// <summary>Chi trinh dien; khong mo phong dan hay tinh damage. Tai nguyen co vong doi huu han.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PetHudRuntimeStats))]
    public sealed class PetSkillPresentationPlayer : MonoBehaviour
    {
        public List<PetSkillSocket> sockets = new List<PetSkillSocket>();
        [SerializeField, Range(1, 32)]
        int maxLiveEffects = 12;
        [SerializeField, Range(0, 24)]
        int maxPooledEffects = 8;
        PetHudRuntimeStats stats;
        PetController pet;
        Capstone.Game.CreatureSystem.CreatureCombatContext context;
        Animator boundAnimator;
        Transform effectsRoot;
        readonly List<Cast> casts = new List<Cast>(8);
        readonly List<Playback> playing = new List<Playback>(12);
        readonly List<Playback> pool = new List<Playback>(8);
        int nextToken;
        static readonly ProfilerMarker CueMarker = new ProfilerMarker("Capstone.Combat.VFX.Cue");
        public int ActiveCastId { get; private set; }

        public int LiveEffectCount => playing.Count;
        public int PooledEffectCount => pool.Count;
        public int ReleasedCount { get; private set; }

        public float VisualScale { get; private set; } = 1;
        public event Action<int, string> SkillReleased;
        public event Action<int, string> SkillCastStarted;
        public event Action<int, string, int> SkillProjectileReleased;
        public Vector3 SocketPosition(string key) => ResolveSocket(key).position;
        sealed class Cast
        {
            public int token;
            public SkillHudData skill;
            public PetSkillPresentationProfile profile;
            public float animationStart, releaseAt, animationEnd, expiresAt;
            public bool released, coordinated;
            public int emissions = 1, releaseMask, resolvedMask;
        }

        sealed class Playback
        {
            public GameObject prefab, root;
            public ParticleSystem[] particles;
            public VisualEffect[] graphs;
            public TrailRenderer[] trails;
            public AudioSource[] embeddedAudio;
            public AudioSource audio;
            public int token;
            public bool castCue;
            public float expiresAt;
            public Transform follow, frame;
            public Vector3 offset;
            public Quaternion rotation;
        }

        void OnEnable()
        {
            stats = GetComponent<PetHudRuntimeStats>();
            pet = GetComponentInParent<PetController>();
            context = Capstone.Game.CreatureSystem.CreatureCombatContext.Get(gameObject);
            stats.SkillAnimationAccepted -= Begin;
            stats.SkillAnimationAccepted += Begin;
            BindAnimator(stats.VisualAnimator);
        }

        void OnDisable()
        {
            if (stats != null)
            {
                stats.SkillAnimationAccepted -= Begin;
            }

            CancelPresentation();
        }

        void OnDestroy()
        {
            foreach (var item in pool)
            {
                if (item.root != null)
                {
                    Destroy(item.root);
                }
            }

            pool.Clear();
            if (effectsRoot != null)
            {
                Destroy(effectsRoot.gameObject);
            }
        }

        public void BindAnimator(Animator animator)
        {
            if (boundAnimator != null && boundAnimator != animator)
            {
                CancelPresentation();
            }

            boundAnimator = animator;
            var binding = animator != null ? animator.GetComponent<Capstone.Game.CreatureSystem.CreatureVisualBinding>() : null;
            VisualScale = binding != null ? Mathf.Clamp(binding.presentationScale, .25f, 3) : 1;
            if (animator == null)
            {
                return;
            }

            var relay = animator.GetComponent<PetSkillAnimationRelay>();
            if (relay == null && Application.isPlaying)
            {
                relay = animator.gameObject.AddComponent<PetSkillAnimationRelay>();
            }

            if (relay != null)
            {
                relay.Bind(this, animator);
            }
        }

        void Begin(SkillHudData skill)
        {
            var profile = stats.GetSkillPresentation(skill.skillId);
            if (profile == null)
            {
                return;
            }

            // Windup bi thay the khong duoc phat tre don cua cast cu.
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (!casts[i].released)
                {
                    StopOwned(casts[i].token, false);
                    casts.RemoveAt(i);
                }
            }

            if (casts.Count >= 8)
            {
                StopOwned(casts[0].token, false);
                casts.RemoveAt(0);
            }

            if (++nextToken <= 0)
            {
                nextToken = 1;
            }

            float start = Time.time + Mathf.Max(0, skill.windupSeconds);
            var driver = GetComponent<PetCombatActionDriver>();
            bool coordinated = driver != null && driver.IsSkillBusy;
            var cast = new Cast{token = nextToken, skill = skill, profile = profile, animationStart = start, coordinated = coordinated, releaseAt = start + Mathf.Max(0, profile.releaseDelaySeconds), animationEnd = coordinated || profile.releaseMode == PetSkillReleaseMode.NormalizedAnimation ? start + 5 : start + Mathf.Max(0.05f, skill.animationDuration) + Mathf.Max(0, skill.recoverySeconds), expiresAt = start + (coordinated || profile.releaseMode == PetSkillReleaseMode.NormalizedAnimation ? 5 : Mathf.Max(0.05f, skill.animationDuration)) + Mathf.Clamp(profile.impactWindowSeconds, 0.1f, 30)};
            casts.Add(cast);
            ActiveCastId = cast.token;
            PlayCue(profile.cast, cast, true, null, Quaternion.identity);
            BindAnimator(stats.VisualAnimator);
            SkillCastStarted?.Invoke(cast.token, skill.skillId);
        }

        void Update()
        {
            if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if ((!context.IsEnemy && InventoryInputController.GameplayInputBlocked) || PetEvolutionPresentationController.AnyPlaying)
            {
                CancelPresentation();
                return;
            }

            if ((casts.Count > 0 || playing.Count > 0) && (stats == null || !stats.isActiveAndEnabled || !context.CanAct))
            {
                CancelPresentation();
                return;
            }

            float now = Time.time;
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                var cast = casts[i];
                if (!cast.coordinated && !cast.released && cast.profile.releaseMode == PetSkillReleaseMode.Timed && now >= cast.releaseAt && now <= cast.animationEnd)
                {
                    Release(cast);
                }

                if (now >= cast.animationEnd)
                {
                    StopOwned(cast.token, true);
                }

                // Don can chien co the da dung token ngay trong Release.
                if (now >= cast.expiresAt && i < casts.Count && ReferenceEquals(casts[i], cast))
                {
                    if (ActiveCastId == cast.token)
                    {
                        ActiveCastId = 0;
                    }

                    casts.RemoveAt(i);
                }
            }

            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var item = playing[i];
                if (item.root == null || now >= item.expiresAt)
                {
                    Return(i);
                    continue;
                }

                if (item.follow != null && item.frame != null)
                {
                    item.root.transform.SetPositionAndRotation(item.follow.position + item.frame.TransformVector(item.offset), item.frame.rotation * item.rotation);
                }
            }
        }

        public bool NotifyRelease(int castId)
        {
            foreach (var cast in casts)
            {
                if (cast.token == castId && !cast.released && Time.time >= cast.animationStart && Time.time <= cast.animationEnd)
                {
                    Release(cast);
                    return true;
                }
            }

            return false;
        }

        public void StartCoordinatedAnimation(int token)
        {
            foreach (var cast in casts)
            {
                if (cast.token == token)
                {
                    cast.animationStart = Time.time;
                    return;
                }
            }
        }

        public void FinishCoordinatedAnimation(int token)
        {
            foreach (var cast in casts)
            {
                if (cast.token == token)
                {
                    cast.animationEnd = Time.time;
                    break;
                }
            }

            StopOwned(token, true);
        }

        public void NotifyAnimationRelease(Animator source, AnimatorStateInfo state)
        {
            if (source != boundAnimator)
            {
                return;
            }

            foreach (var cast in casts)
            {
                if (cast.coordinated || cast.token != ActiveCastId || cast.profile.releaseMode != PetSkillReleaseMode.AnimationEvent)
                {
                    continue;
                }

                if (cast.skill.animatorStates == null)
                {
                    return;
                }

                foreach (var name in cast.skill.animatorStates)
                {
                    if (!string.IsNullOrWhiteSpace(name) && (state.IsName(name) || state.IsName("Base Layer." + name)))
                    {
                        NotifyRelease(cast.token);
                        return;
                    }
                }
            }
        }

        void Release(Cast cast)
        {
            cast.released = true;
            cast.releaseMask |= 1;
            StopOwned(cast.token, true);
            PlayCue(cast.profile.release, cast, false, null, Quaternion.identity);
            ReleasedCount++;
            SkillReleased?.Invoke(cast.token, cast.skill.skillId);
        }

        /// <summary>Xu ly va cham that phai dung token cua SkillReleased; moi token chi trung mot lan.</summary>
        public void ConfigureEmissions(int token, int count)
        {
            foreach (var cast in casts)
            {
                if (cast.token == token)
                {
                    cast.emissions = Mathf.Clamp(count, 1, 8);
                    return;
                }
            }
        }

        public int EmissionCount(int token)
        {
            foreach (var cast in casts)
            {
                if (cast.token == token)
                {
                    return cast.emissions;
                }
            }

            return 1;
        }

        public bool NotifySubRelease(int token, int emission)
        {
            foreach (var cast in casts)
            {
                if (cast.token == token && cast.released && emission > 0 && emission < cast.emissions && (cast.releaseMask & (1 << emission)) == 0)
                {
                    cast.releaseMask |= 1 << emission;
                    PlayCue(cast.profile.release, cast, false, null, Quaternion.identity);
                    SkillProjectileReleased?.Invoke(token, cast.skill.skillId, emission);
                    return true;
                }
            }

            return false;
        }

        public bool CanImpact(int castId) => CanImpact(castId, 0);
        public bool CanImpact(int castId, int emission)
        {
            foreach (var cast in casts)
            {
                if (cast.token == castId && emission >= 0 && emission < cast.emissions && (cast.releaseMask & (1 << emission)) != 0 && (cast.resolvedMask & (1 << emission)) == 0 && Time.time <= cast.expiresAt)
                {
                    return true;
                }
            }

            return false;
        }

        public bool NotifyImpact(int castId, Vector3 position, Vector3 normal) => NotifyImpact(castId, 0, position, normal);
        public bool NotifyImpact(int castId, int emission, Vector3 position, Vector3 normal)
        {
            for (int i = 0; i < casts.Count; i++)
            {
                var cast = casts[i];
                if (cast.token != castId || !CanImpact(castId, emission))
                {
                    continue;
                }

                PlayCue(cast.profile.impact, cast, false, position, normal.sqrMagnitude > 0.001f ? Quaternion.FromToRotation(Vector3.up, normal.normalized) : Quaternion.identity);
                ResolveEmission(i, emission);
                return true;
            }

            return false;
        }

        void ResolveEmission(int index, int emission)
        {
            var cast = casts[index];
            cast.resolvedMask |= 1 << emission;
            if (cast.resolvedMask == ((1 << cast.emissions) - 1))
            {
                casts.RemoveAt(index);
                if (ActiveCastId == cast.token)
                {
                    ActiveCastId = 0;
                }
            }
        }

        public void CompleteMiss(int token) => CompleteMiss(token, 0);
        public void CompleteMiss(int token, int emission)
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (casts[i].token == token && emission >= 0 && emission < casts[i].emissions)
                {
                    StopOwned(token, true);
                    ResolveEmission(i, emission);
                    break;
                }
            }
        }

        public void CancelPresentation()
        {
            casts.Clear();
            ActiveCastId = 0;
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                Return(i);
            }
        }

        public void CancelUnreleased()
        {
            for (int i = casts.Count - 1; i >= 0; i--)
            {
                if (!casts[i].released)
                {
                    StopOwned(casts[i].token, false);
                    if (ActiveCastId == casts[i].token)
                    {
                        ActiveCastId = 0;
                    }

                    casts.RemoveAt(i);
                }
            }
        }

        Transform ResolveSocket(string key)
        {
            if (!string.IsNullOrWhiteSpace(key) && sockets != null)
            {
                foreach (var socket in sockets)
                {
                    if (socket != null && socket.anchor != null && string.Equals(socket.key, key, StringComparison.OrdinalIgnoreCase))
                    {
                        return socket.anchor;
                    }
                }
            }

            return transform;
        }

        void PlayCue(PetSkillCue cue, Cast cast, bool castCue, Vector3? worldPosition, Quaternion worldRotation)
        {
            using (CueMarker.Auto())
            {
                if (cue == null || (cue.effectPrefab == null && cue.sound == null))
                {
                    return;
                }

                if (playing.Count >= Mathf.Clamp(maxLiveEffects, 1, 32))
                {
                    Return(0);
                }

                Playback item = null;
                for (int i = pool.Count - 1; i >= 0; i--)
                {
                    if (pool[i].prefab == cue.effectPrefab && pool[i].root != null)
                    {
                        item = pool[i];
                        pool.RemoveAt(i);
                        break;
                    }
                }

                if (item == null)
                {
                    item = CreatePlayback(cue.effectPrefab);
                }

                var anchor = ResolveSocket(cue.socket);
                var frame = cue.actorSpace ? transform : anchor;
                item.token = cast.token;
                item.castCue = castCue;
                item.expiresAt = Time.time + Mathf.Clamp(cue.lifetimeSeconds, 0.05f, 30);
                item.follow = !worldPosition.HasValue && cue.followSocket ? anchor : null;
                item.frame = frame;
                item.offset = cue.localOffset;
                item.rotation = Quaternion.Euler(cue.localEulerAngles);
                item.root.transform.SetPositionAndRotation(worldPosition.HasValue ? worldPosition.Value + worldRotation * cue.localOffset : anchor.position + frame.TransformVector(cue.localOffset), (worldPosition.HasValue ? worldRotation : frame.rotation) * item.rotation);
                item.root.transform.localScale = Vector3.one * Mathf.Max(0.01f, cue.scale) * VisualScale;
                foreach (var trail in item.trails)
                {
                    if (trail != null)
                    {
                        trail.Clear();
                    }
                }

                item.root.SetActive(true);
                foreach (var particle in item.particles)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    particle.Play(false);
                }

                foreach (var graph in item.graphs)
                {
                    graph.Reinit();
                }

                item.audio.outputAudioMixerGroup = cast.profile.mixerGroup;
                item.audio.spatialBlend = cast.profile.spatialBlend;
                item.audio.minDistance = Mathf.Max(0.1f, cast.profile.minDistance);
                item.audio.maxDistance = Mathf.Max(item.audio.minDistance, cast.profile.maxDistance);
                item.audio.volume = Mathf.Clamp01(cue.volume);
                item.audio.pitch = Mathf.Clamp(cue.pitch * CombatAudioVoices.PitchVariation(), 0.1f, 3);
                item.audio.clip = cue.sound;
                if (cue.sound != null)
                {
                    CombatAudioVoices.Play(item.audio);
                }

                playing.Add(item);
            }
        }

        Playback CreatePlayback(GameObject prefab)
        {
            if (effectsRoot == null)
            {
                effectsRoot = new GameObject(name + " Skill Effects").transform;
                if (gameObject.scene.IsValid())
                {
                    SceneManager.MoveGameObjectToScene(effectsRoot.gameObject, gameObject.scene);
                }
            }

            var root = new GameObject("Skill Cue");
            root.SetActive(false);
            root.transform.SetParent(effectsRoot, false);
            GameObject visual = prefab != null ? Instantiate(prefab, root.transform, false) : null;
            if (visual != null)
            {
                visual.SetActive(true);
            }

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            var result = new Playback{prefab = prefab, root = root, audio = audio, particles = root.GetComponentsInChildren<ParticleSystem>(true), graphs = root.GetComponentsInChildren<VisualEffect>(true), trails = root.GetComponentsInChildren<TrailRenderer>(true), embeddedAudio = visual != null ? visual.GetComponentsInChildren<AudioSource>(true) : Array.Empty<AudioSource>()};
            // Tat am thanh play-on-awake de moi cue deu qua gioi han voice chung.
            foreach (var embedded in result.embeddedAudio)
            {
                if (embedded != null)
                {
                    embedded.Stop();
                    embedded.playOnAwake = false;
                    embedded.enabled = false;
                }
            }

            return result;
        }

        void StopOwned(int token, bool castOnly)
        {
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                if (playing[i].token == token && (!castOnly || playing[i].castCue))
                {
                    Return(i);
                }
            }
        }

        void Return(int index)
        {
            var item = playing[index];
            playing.RemoveAt(index);
            if (item.root == null)
            {
                return;
            }

            foreach (var particle in item.particles)
            {
                if (particle != null)
                {
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

            foreach (var graph in item.graphs)
            {
                if (graph != null)
                {
                    graph.Stop();
                }
            }

            foreach (var trail in item.trails)
            {
                if (trail != null)
                {
                    trail.Clear();
                }
            }

            foreach (var audio in item.embeddedAudio)
            {
                if (audio != null)
                {
                    audio.Stop();
                }
            }

            item.audio.Stop();
            item.audio.clip = null;
            item.follow = null;
            item.frame = null;
            item.root.SetActive(false);
            if (pool.Count < Mathf.Clamp(maxPooledEffects, 0, 24))
            {
                pool.Add(item);
            }
            else
            {
                Destroy(item.root);
            }
        }

        void OnApplicationFocus(bool value)
        {
            if (!value && !Capstone.Game.UISystem.GameplayPause.IsPaused && (context == null || !context.IsEnemy))
            {
                CancelPresentation();
            }
        }
    }
}
