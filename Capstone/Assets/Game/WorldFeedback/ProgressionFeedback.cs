using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.SettingsSystem;
using UnityEngine;

namespace Capstone.Game.WorldFeedback {
    public sealed class ProgressionFeedback : ScriptableObject {
        [SerializeField] GameObject levelEffect;
        static ProgressionFeedback assets;
        static AudioClip chime;
        static float nextChime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { nextChime = 0; }

        internal static HashSet<string> Learned(PetHudRuntimeStats stats) =>
            stats.GetLearnedSkills().Where(skill => skill.unlocked).Select(skill => skill.skillId).ToHashSet(StringComparer.Ordinal);

        internal static void PetProgress(PetController pet, PetHudRuntimeStats stats, HashSet<string> previous, bool levelUp) {
            string name = pet.GetComponentInChildren<PetCollectionMetadata>(true)?.ResolveDisplayName(stats.DisplayName) ?? stats.DisplayName;
            var newSkills = stats.GetLearnedSkills().Where(skill => skill.unlocked && !previous.Contains(skill.skillId)).ToArray();
            int level = stats.Level;
            CaptureEventBatch.Publish(pet, "progression:" + level + ":" + levelUp, () => {
                if (pet == null) return;
                if (levelUp) Level(name, level, stats.Icon, pet.IsSummoned ? pet.transform : null);
                foreach (var skill in newSkills) Toast($"{name} học {skill.displayName}", skill.icon);
            });
        }

        internal static void PlayerLevel(int level, Transform actor) {
            CaptureEventBatch.Publish(actor, "player-level", () => Level("Nhân vật", level, null, actor));
        }

        static void Toast(string title, Sprite icon) {
            if (Application.isPlaying)
                ItemAcquisitionFeedback.Publish(new ItemAcquisitionReceipt(Guid.NewGuid().ToString("N"), icon, title, AcquisitionSource.Progression));
        }

        static void Level(string name, int level, Sprite icon, Transform actor) {
            if (!Application.isPlaying) return;
            Toast($"{name} lên cấp {level}", icon);
            if (assets == null) assets = Resources.Load<ProgressionFeedback>("ProgressionFeedback");
            if (actor != null && assets != null && assets.levelEffect != null)
                Destroy(Instantiate(assets.levelEffect, actor.position, Quaternion.identity), 2f);
            if (Time.unscaledTime < nextChime) return;
            nextChime = Time.unscaledTime + .65f;
            var sound = new GameObject("Level Up Chime").AddComponent<AudioSource>();
            sound.spatialBlend = 0;
            GameAudioRouting.Route(sound, GameAudioBus.SFX);
            if (chime == null) chime = CreateChime();
            sound.PlayOneShot(chime, .42f);
            Destroy(sound.gameObject, chime.length + .2f);
        }

        static AudioClip CreateChime() {
            const int rate = 24000;
            var samples = new float[rate];
            float[] notes = { 523.25f, 659.25f, 783.99f };
            for (int note = 0; note < notes.Length; note++) {
                int start = note * 2900;
                for (int i = 0; i < 14400; i++) {
                    float time = i / (float)rate;
                    float phase = 2f * Mathf.PI * time * notes[note];
                    float envelope = Mathf.Min(1f, time / .018f) * Mathf.Exp(-8f * time) * (1f - time / .6f);
                    samples[start + i] += .28f * envelope * (Mathf.Sin(phase) + .12f * Mathf.Sin(2f * phase));
                }
            }
            var clip = AudioClip.Create("Soft level-up chime", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
