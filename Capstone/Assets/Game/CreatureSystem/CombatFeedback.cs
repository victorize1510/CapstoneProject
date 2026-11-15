using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Skills;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Reads committed results. Never affects health, targeting, physics or the global clock.</summary>
    public sealed class CombatFeedback : MonoBehaviour {
        const int Channel = 1 << 12;
        static CombatFeedback live;
        struct Pulse { public GameObject visual; public float end; }
        readonly List<Pulse> pulses = new List<Pulse>(12);
        readonly AudioSource[] voices = new AudioSource[2];
        CombatFeelSettings settings;
        CinemachineImpulseSource impulse;
        PlayerFreeLookRig cameraRig;
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
            }
            SceneManager.activeSceneChanged += SceneChanged; BindCamera();
        }
        void BindCamera() {
            cameraRig = FindFirstObjectByType<PlayerFreeLookRig>();
            if (cameraRig == null || !cameraRig.IsReady) return;
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
        void Clear() { foreach (var pulse in pulses) CombatEffectPool.Return(pulse.visual); pulses.Clear(); foreach (var source in voices) if (source != null) source.Stop(); }
        void Update() {
            if (InventoryInputController.GameplayInputBlocked || PetEvolutionPresentationController.AnyPlaying) { Clear(); return; }
            for (int i = pulses.Count - 1; i >= 0; i--) if (Time.time >= pulses[i].end || pulses[i].visual == null) { CombatEffectPool.Return(pulses[i].visual); pulses.RemoveAt(i); }
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
            if (settings != null && (result.Hit.DamageOverTime || result.Hit.SkillId == "basic" || result.Hit.SkillId == "legacy")) {
                GameObject prefab = result.Hit.DamageOverTime ? settings.bleedImpact : settings.basicImpact;
                if (prefab != null) {
                    if (pulses.Count >= 12) { CombatEffectPool.Return(pulses[0].visual); pulses.RemoveAt(0); }
                    var value = CombatEffectPool.Rent(prefab, result.Hit.Point, Quaternion.identity);
                    pulses.Add(new Pulse { visual = value, end = Time.time + .3f });
                }
                var clip = result.Hit.DamageOverTime ? settings.bleedSound : settings.basicSound;
                if (clip != null) {
                    var voice = voices[nextAudio++ % voices.Length];
                    voice.transform.position = result.Hit.Point; voice.clip = clip;
                    voice.volume = result.Hit.DamageOverTime ? .07f : .18f;
                    voice.pitch = CombatAudioVoices.PitchVariation(); CombatAudioVoices.Play(voice);
                }
            }
            if (!result.Hit.DamageOverTime && (result.Hit.Critical || result.Hit.SkillId == "wing strike") && Time.unscaledTime >= nextImpulse
                && cameraRig != null && cameraRig.IsReady && cameraRig.source != null
                && (cameraRig.source.transform.position - result.Hit.Point).sqrMagnitude < 625) {
                nextImpulse = Time.unscaledTime + .2f;
                float amplitude = settings != null ? settings.cameraAmplitude : .03f;
                impulse.GenerateImpulseAtPositionWithVelocity(result.Hit.Point, new Vector3(.5f, 1, 0).normalized * amplitude * (result.Hit.Critical ? 1 : .6f));
            }
        }
    }
}
