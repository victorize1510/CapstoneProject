using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Skills;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Reads committed results. Never affects health, targeting, physics or the global clock.</summary>
    [DefaultExecutionOrder(90)]
    public sealed class CombatFeedback : MonoBehaviour {
        const int Channel = 1 << 12;
        const float HitLowering = .35f;
        static CombatFeedback live;
        struct Pulse { public GameObject visual; public Transform anchor, ground; public Renderer body; public float end; }
        readonly List<Pulse> pulses = new List<Pulse>(12);
        readonly AudioSource[] voices = new AudioSource[2];
        CombatFeelSettings settings;
        CinemachineImpulseSource impulse;
        PlayerFreeLookRig cameraRig;
        bool listenersBound;
        float nextImpulse;
        int nextAudio;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void ResetStatic() { live = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)] static void Boot() { Ensure(); }
        static void Ensure() {
            if (live != null) return;
            live = new GameObject("Combat feedback").AddComponent<CombatFeedback>(); DontDestroyOnLoad(live.gameObject);
        }
        void Awake() {
            live = this; settings = Resources.Load<CombatFeelSettings>("CombatFeelSettings");
            impulse = gameObject.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseChannel = Channel;
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
            impulse.ImpulseDefinition.ImpulseDuration = settings != null ? settings.cameraDuration : .1f;
            for (int i = 0; i < voices.Length; i++) {
                var holder = new GameObject("Hit sound " + i); holder.transform.SetParent(transform, false);
                voices[i] = holder.AddComponent<AudioSource>(); voices[i].playOnAwake = false; voices[i].spatialBlend = 1; voices[i].maxDistance = 25;
                SettingsSystem.GameAudioRouting.Route(voices[i], SettingsSystem.GameAudioBus.SFX);
            }
            SceneManager.activeSceneChanged += SceneChanged; BindCamera();
        }
        void BindCamera() {
            cameraRig = FindFirstObjectByType<PlayerFreeLookRig>();
            listenersBound = cameraRig != null && cameraRig.IsReady;
            if (!listenersBound) return;
            BindListener(cameraRig.freeCamera); BindListener(cameraRig.lockCamera);
        }
        static void BindListener(CinemachineCamera camera) {
            if (camera == null) return;
            var listener = camera.GetComponent<CinemachineImpulseListener>();
            if (listener == null) { listener = camera.gameObject.AddComponent<CinemachineImpulseListener>(); listener.ChannelMask = Channel; listener.Gain = 1; }
            else listener.ChannelMask |= Channel;
            listener.UseCameraSpace = true;
            // Strongest prevents coincident hits from stacking camera displacement.
            listener.SignalCombinationMode = CinemachineImpulseListener.SignalCombinationModes.UseLargest;
            listener.ReactionSettings = default;
        }
        void SceneChanged(Scene previous, Scene current) { Clear(); BindCamera(); }
        void OnDestroy() { SceneManager.activeSceneChanged -= SceneChanged; Clear(); if (live == this) live = null; }
        void OnApplicationFocus(bool value) { if (!value) Clear(); }
        void Clear() { foreach (var pulse in pulses) CombatEffectPool.Return(pulse.visual); pulses.Clear(); foreach (var source in voices) if (source != null) source.Stop(); }
        void LateUpdate() {
            if (InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) { Clear(); return; }
            for (int i = pulses.Count - 1; i >= 0; i--) {
                var pulse = pulses[i];
                if (Time.time >= pulse.end || pulse.visual == null) {
                    CombatEffectPool.Return(pulse.visual);
                    pulses.RemoveAt(i);
                } else if (pulse.anchor != null && pulse.anchor.gameObject.activeInHierarchy) {
                    pulse.visual.transform.position = HitPosition(pulse.anchor, pulse.ground, pulse.body);
                }
            }
        }
        public static void Show(CombatDamageResult result) {
            if (!Application.isPlaying || InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) return;
            Ensure(); live.Apply(result);
        }
        void Apply(CombatDamageResult result) {
            if (!result.Hit.DamageOverTime && result.Target != null) {
                var reaction = result.Target.GetComponent<CombatVisualReaction>();
                if (reaction == null) reaction = result.Target.AddComponent<CombatVisualReaction>();
                reaction.Recoil();
            }
            if (result.Hit.Critical && !result.Hit.DamageOverTime && result.Hit.Source != null) {
                var reaction = result.Hit.Source.GetComponent<CombatVisualReaction>();
                if (reaction == null) reaction = result.Hit.Source.AddComponent<CombatVisualReaction>();
                reaction.Pause(settings != null ? settings.criticalVisualPause : .035f);
            }
            if (settings != null) {
                bool periodic = result.Hit.DamageOverTime;
                bool basic = result.Hit.SkillId == "basic" || result.Hit.SkillId == "legacy";
                GameObject prefab = periodic ? settings.bleedImpact : settings.directImpact;
                bool combined = !periodic && prefab != null;
                if (prefab == null && basic && !periodic) prefab = settings.basicImpact;
                if (prefab != null) {
                    int limit = SettingsSystem.GameSettingsService.EffectsQuality == PetSummon.PetVFXQuality.Low ? 6 : 12;
                    while (pulses.Count >= limit) { CombatEffectPool.Return(pulses[0].visual); pulses.RemoveAt(0); }
                    CreatureCombatant actor = !periodic && result.Target != null ? result.Target.GetComponent<CreatureCombatant>() : null;
                    Transform anchor = actor != null ? actor.HitVisualAnchor : null;
                    Transform ground = actor != null ? actor.HitVisualGround : null;
                    Renderer body = actor != null ? actor.HitVisualRenderer : null;
                    Vector3 position = anchor != null ? HitPosition(anchor, ground, body)
                        : periodic ? result.Hit.Point : ResolveEnemyImpactPosition(result);
                    var value = CombatEffectPool.Rent(prefab, position, Quaternion.identity);
                    if (value != null) pulses.Add(new Pulse { visual = value, anchor = anchor, ground = ground, body = body, end = Time.time + (combined ? .55f : .3f) });
                }
                var clip = periodic ? settings.bleedSound : basic ? settings.basicSound : null;
                if (clip != null) {
                    var voice = voices[nextAudio++ % voices.Length];
                    voice.transform.position = result.Hit.Point; voice.clip = clip;
                    voice.volume = result.Hit.DamageOverTime ? .07f : .18f;
                    voice.pitch = CombatAudioVoices.PitchVariation(); CombatAudioVoices.Play(voice);
                }
            }
            TryShakeCamera(result);
        }

        static Vector3 HitPosition(Transform anchor, Transform ground, Renderer body) {
            Vector3 position = anchor.position;
            float groundY = ground != null && ground.gameObject.activeInHierarchy ? ground.position.y : position.y;
            // Some imported rigs use the same transform for Body and Ground.
            if (position.y - groundY < .001f && body != null && body.enabled && body.gameObject.activeInHierarchy)
                groundY = body.bounds.min.y;
            position.y -= Mathf.Max(0, position.y - groundY) * HitLowering;
            return position;
        }

        Vector3 ResolveEnemyImpactPosition(CombatDamageResult result) {
            if (result.Target == null || !result.Target.TryGetComponent<DummyEnemy>(out var enemy))
                return result.Hit.Point;

            Bounds body = enemy.GetTargetBounds();
            float radius = body.extents.magnitude;
            if (radius < .001f) return result.Hit.Point;
            Vector3 center = body.center;
            center.y -= body.extents.y * HitLowering;

            Camera view = cameraRig != null ? cameraRig.brain?.OutputCamera : null;
            if (view == null || !view.isActiveAndEnabled) view = Camera.main;
            if (view == null) return center;

            Vector3 toView = view.transform.position - center;
            if (body.Contains(view.transform.position) || toView.sqrMagnitude < .0001f) return center;
            Vector3 towardView = view.orthographic ? -view.transform.forward : toView.normalized;
            Vector3 outside = center + towardView * (radius + body.extents.y * HitLowering + .1f);
            var sightLine = new Ray(outside, -towardView);
            if (!body.IntersectRay(sightLine, out float depth)) return center;

            // Keep the hit on the camera-facing surface of prefabs without an authored socket.
            float clearance = Mathf.Clamp(radius * .06f, .025f, .12f);
            float offset = Vector3.Dot(sightLine.GetPoint(depth) - center, towardView) + clearance;
            return center + towardView * Mathf.Min(offset, toView.magnitude * .85f);
        }

        void TryShakeCamera(CombatDamageResult result) {
            var hit = result.Hit;
            if (hit.Source == null || hit.DamageOverTime || hit.EmissionIndex < hit.EmissionCount - 1
                || Time.unscaledTime < nextImpulse) return;

            bool hitsPlayerSide = IsPlayerSide(result.Target);
            if (!hitsPlayerSide && !IsPlayerSide(hit.Source)) return;

            var attacker = hit.Source.GetComponentInParent<DummyEnemy>();
            bool strong = hit.Critical || (hitsPlayerSide && attacker != null && attacker.IsBoss);
            if (!strong && hit.SkillId != "wing strike" && hit.SkillId != "cyclone.flying"
                && hit.SkillId != "breath.dragon" && hit.SkillId != "laser.electric") return;

            if (!listenersBound || cameraRig == null || !cameraRig.IsReady) BindCamera();
            if (!listenersBound) return;
            Transform camera = cameraRig.source.transform;
            if ((camera.position - hit.Point).sqrMagnitude >= 625) return;

            float maxAmplitude = Mathf.Clamp(settings != null ? settings.cameraAmplitude : .03f, 0, .03f);
            if (maxAmplitude == 0) return;
            float duration = settings != null ? settings.cameraDuration : .1f;
            impulse.ImpulseDefinition.ImpulseDuration = Mathf.Min(.2f, duration + (strong ? .04f : 0));
            Vector3 travel = hit.Point - hit.Source.transform.position;
            Vector2 direction = new Vector2(Vector3.Dot(travel, camera.right), Vector3.Dot(travel, camera.up));
            if (direction.sqrMagnitude < .01f) direction = Vector2.up;
            nextImpulse = Time.unscaledTime + .2f;
            impulse.GenerateImpulseAtPositionWithVelocity(hit.Point, (Vector3)direction.normalized * (strong ? maxAmplitude : maxAmplitude * (2f / 3f)));
        }

        static bool IsPlayerSide(GameObject actor) {
            if (actor == null) return false;
            var pet = actor.GetComponentInParent<PetController>();
            if (pet != null)
                return !pet.IsWildEncounter && pet.owner != null && pet.owner.GetComponentInParent<PetCommandInput>() != null;
            return actor.GetComponentInParent<PetCommandInput>() != null;
        }
    }
}
