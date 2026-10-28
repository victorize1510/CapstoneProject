using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Capstone.Game.Skills {
    public enum PetSkillReleaseMode { Timed, AnimationEvent }
    [Serializable]
    public sealed class PetSkillCue {
        [Tooltip("A prefab containing ParticleSystem and/or VisualEffect components. Optional.")]
        public GameObject effectPrefab;
        public AudioClip sound;
        [Tooltip("Key from the pet's presentation player socket list. Empty = pet root.")]
        public string socket;
        public bool followSocket;
        public Vector3 localOffset;
        public Vector3 localEulerAngles;
        [Min(0.01f)] public float scale = 1;
        [Range(0, 1)] public float volume = 1;
        [Range(0.1f, 3)] public float pitch = 1;
        [Range(0.05f, 30)] public float lifetimeSeconds = 3;
    }
    [CreateAssetMenu(menuName = "Capstone/Pet Skills/Presentation", fileName = "Presentation_")]
    public sealed class PetSkillPresentationProfile : ScriptableObject {
        public PetSkillReleaseMode releaseMode;
        [Tooltip("Seconds after animation START, in addition to the skill's pre-animation windup.")]
        [Min(0)] public float releaseDelaySeconds = 0.25f;
        [Tooltip("Accept real collision impact callbacks for at most this long; does not create a projectile or damage.")]
        [Range(0.1f, 30)] public float impactWindowSeconds = 5;
        public AudioMixerGroup mixerGroup;
        [Range(0, 1)] public float spatialBlend = 1;
        [Min(0.1f)] public float minDistance = 2;
        [Min(0.1f)] public float maxDistance = 35;
        public PetSkillCue cast = new PetSkillCue { followSocket = true };
        public PetSkillCue release = new PetSkillCue();
        public PetSkillCue impact = new PetSkillCue();
    }
}
