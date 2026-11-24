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
        Capstone.Game.CreatureSystem.CreatureAnimationDriver animationDriver;
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
        internal float MouthClearance => Mathf.Clamp(.22f * VisualScale, .14f, .4f);
        internal static bool IsMouthSocket(string key) => string.Equals(key, "Mouth", StringComparison.OrdinalIgnoreCase);
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
            public GameObject prefab, root, visual;
            public Vector3 prefabLocalPosition;
            public ParticleSystem[] particles;
            public int[] particleCaps;
            public VisualEffect[] graphs;
            public TrailRenderer[] trails;
            public AudioSource[] embeddedAudio;
            public AudioSource audio;
            public int token;
            public bool castCue, followChannelAim, mouthRelease, fitActorBounds;
            public Vector3 actorLocalCenter;
            public PetSkillCombatExecutor executor;
            public float expiresAt;
            public Transform follow, frame;
            public Vector3 offset;
            public Quaternion rotation;
        }

        void OnEnable()
        {
            stats = GetComponent<PetHudRuntimeStats>();
            pet = GetComponentInParent<PetController>();
            animationDriver = GetComponent<Capstone.Game.CreatureSystem.CreatureAnimationDriver>();
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

            // Bind the current form before publishing a new cast. Changing rig cancels only the old casts.
            BindAnimator(stats.VisualAnimator);

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
                    PositionFollowingCue(item, item.follow);
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
            PlayCue(cast.profile.release, cast, false, null, Quaternion.identity, true);
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
                    cast.emissions = Mathf.Clamp(count, 1, 10);
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
        internal void KeepImpactOpen(int castId, float until)
        {
            foreach (var cast in casts)
                if (cast.token == castId) {
                    cast.expiresAt = Mathf.Max(cast.expiresAt, until);
                    break;
                }
        }
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
            animationDriver?.Cry?.CancelSkill();
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
                        animationDriver?.Cry?.CancelSkill();
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
                Transform head = null;
                foreach (var socket in sockets)
                {
                    if (socket == null || socket.anchor == null) continue;
                    if (string.Equals(socket.key, key, StringComparison.OrdinalIgnoreCase)) return socket.anchor;
                    if (string.Equals(socket.key, "Head", StringComparison.OrdinalIgnoreCase)) head = socket.anchor;
                }
                if (IsMouthSocket(key) && head != null) return head;
            }

            return transform;
        }

        void PositionFollowingCue(Playback item, Transform anchor)
        {
            var orientation = item.fitActorBounds
                ? Quaternion.Euler(0, item.frame.eulerAngles.y, 0)
                : item.frame.rotation;
            var offset = item.frame.TransformVector(item.offset);
            Vector3 position = item.fitActorBounds ? transform.TransformPoint(item.actorLocalCenter) : anchor.position;
            if (item.followChannelAim && item.executor != null)
            {
                orientation = Quaternion.LookRotation(item.executor.ChannelDirection(item.token, anchor.position));
                offset = orientation * Vector3.Scale(item.offset, item.frame.lossyScale);
            }
            if (item.mouthRelease)
                offset += (item.followChannelAim ? orientation * Vector3.forward : transform.forward) * MouthClearance;
            item.root.transform.SetPositionAndRotation(position + offset, orientation * item.rotation);
        }

        Bounds GetActorVisualBounds()
        {
            bool found = false;
            Bounds result = default;
            if (boundAnimator != null)
            {
                foreach (var renderer in boundAnimator.GetComponentsInChildren<Renderer>())
                {
                    if (!renderer.enabled || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)) continue;
                    Bounds mesh = renderer.localBounds;
                    Matrix4x4 toActor = transform.worldToLocalMatrix * renderer.localToWorldMatrix;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        Vector3 point = new Vector3((corner & 1) != 0 ? mesh.max.x : mesh.min.x,
                            (corner & 2) != 0 ? mesh.max.y : mesh.min.y, (corner & 4) != 0 ? mesh.max.z : mesh.min.z);
                        point = toActor.MultiplyPoint3x4(point);
                        if (found) result.Encapsulate(point);
                        else { result = new Bounds(point, Vector3.zero); found = true; }
                    }
                }
            }
            return found ? result : new Bounds(Vector3.up * .5f, Vector3.one);
        }

        void PlayCue(PetSkillCue cue, Cast cast, bool castCue, Vector3? worldPosition, Quaternion worldRotation, bool firstRelease = false)
        {
            using (CueMarker.Auto())
            {
                if (cue == null) return;
                var combat = cast.skill.combatProfile;
                var sound = combat != null && combat.authoredStatusRules
                    ? firstRelease ? combat.authoredReleaseSound : null
                    : cue.sound;
                if (cue.effectPrefab == null && sound == null)
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
                var frame = cue.actorSpace || cue.fitActorBounds ? transform : anchor;
                item.token = cast.token;
                item.castCue = castCue;
                item.expiresAt = Time.time + Mathf.Clamp(cue.lifetimeSeconds, 0.05f, 30);
                item.follow = !worldPosition.HasValue && cue.followSocket ? anchor : null;
                item.frame = frame;
                item.offset = cue.localOffset;
                item.rotation = Quaternion.Euler(cue.localEulerAngles);
                item.mouthRelease = !castCue && !worldPosition.HasValue && IsMouthSocket(cue.socket) &&
                    combat != null && (combat.delivery == PetSkillDelivery.Projectile ||
                        combat.pattern == PetSkillPattern.Cone || combat.pattern == PetSkillPattern.Beam);
                item.followChannelAim = cue.followChannelAim;
                item.executor = cue.followChannelAim ? GetComponent<PetSkillCombatExecutor>() : null;
                item.fitActorBounds = cue.fitActorBounds && !worldPosition.HasValue;
                Vector3 scale = Vector3.one * Mathf.Max(.01f, cue.scale) * VisualScale;
                Vector3 fittedVisualOffset = Vector3.zero;
                if (item.fitActorBounds)
                {
                    Bounds bounds = GetActorVisualBounds();
                    item.actorLocalCenter = bounds.center;
                    var size = Vector3.Scale(bounds.size, transform.lossyScale);
                    float AxisSize(Vector3 axis)
                    {
                        axis = item.rotation * axis;
                        return Mathf.Max(.5f, Mathf.Abs(axis.x * size.x) + Mathf.Abs(axis.y * size.y) +
                            Mathf.Abs(axis.z * size.z));
                    }
                    scale = new Vector3(AxisSize(Vector3.right), AxisSize(Vector3.up), AxisSize(Vector3.forward)) * cue.scale;
                    if (cue.anchorAtActorFeet)
                    {
                        var ground = ResolveSocket("Ground");
                        item.actorLocalCenter.y = ground != transform ? transform.InverseTransformPoint(ground.position).y : bounds.min.y;
                        // Lift the center of the upright ring while its scene anchor stays at the feet.
                        float height = Mathf.Max(.5f, Mathf.Abs(size.y)) * cue.scale;
                        Vector3 lift = Quaternion.Inverse(item.rotation) * (Vector3.up * (height * .5f));
                        fittedVisualOffset = new Vector3(lift.x / scale.x, lift.y / scale.y, lift.z / scale.z);
                    }
                }
                if (item.visual != null)
                    item.visual.transform.localPosition = item.mouthRelease || item.fitActorBounds ||
                        (worldPosition.HasValue && combat != null && combat.GroundsImpact)
                        ? fittedVisualOffset : item.prefabLocalPosition;
                if (worldPosition.HasValue)
                    item.root.transform.SetPositionAndRotation(worldPosition.Value + worldRotation * item.offset, worldRotation * item.rotation);
                else
                    PositionFollowingCue(item, anchor);
                item.root.transform.localScale = scale;
                foreach (var trail in item.trails)
                {
                    if (trail != null)
                    {
                        trail.Clear();
                    }
                }

                item.root.SetActive(true);
                float particleBudget = SettingsSystem.GameSettingsService.EffectsQuality == PetSummon.PetVFXQuality.Low ? .6f : 1f;
                for (int i = 0; i < item.particles.Length; i++)
                {
                    var particle = item.particles[i];
                    particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = particle.main;
                    main.maxParticles = Mathf.Max(1, Mathf.CeilToInt(item.particleCaps[i] * particleBudget));
                    if (item.fitActorBounds) main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    particle.Play(false);
                }

                foreach (var graph in item.graphs)
                {
                    graph.Reinit();
                }

                SettingsSystem.GameAudioRouting.Route(item.audio, SettingsSystem.GameAudioBus.Skill, cast.profile.mixerGroup);
                item.audio.spatialBlend = cast.profile.spatialBlend;
                item.audio.minDistance = Mathf.Max(0.1f, cast.profile.minDistance);
                item.audio.maxDistance = Mathf.Max(item.audio.minDistance, cast.profile.maxDistance);
                item.audio.volume = Mathf.Clamp01(cue.volume);
                item.audio.pitch = Mathf.Clamp(cue.pitch * CombatAudioVoices.PitchVariation(), 0.1f, 3);
                item.audio.clip = sound;
                if (sound != null)
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
            GameObject visual = CombatEffectPool.TryInstantiateVisual(prefab, root.transform);
            if (visual != null)
            {
                CombatEffectPool.DisableVisualColliders(visual);
                visual.SetActive(true);
            }

            var audio = root.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.rolloffMode = AudioRolloffMode.Logarithmic;
            var result = new Playback{prefab = prefab, root = root, visual = visual,
                prefabLocalPosition = visual != null ? visual.transform.localPosition : Vector3.zero, audio = audio, particles = root.GetComponentsInChildren<ParticleSystem>(true), graphs = root.GetComponentsInChildren<VisualEffect>(true), trails = root.GetComponentsInChildren<TrailRenderer>(true), embeddedAudio = visual != null ? visual.GetComponentsInChildren<AudioSource>(true) : Array.Empty<AudioSource>()};
            result.particleCaps = new int[result.particles.Length];
            for (int i = 0; i < result.particles.Length; i++)
                result.particleCaps[i] = result.particles[i].main.maxParticles;
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
