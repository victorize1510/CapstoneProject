using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Capstone.Game.Skills {
    public enum PetSkillReleaseMode { Timed, AnimationEvent, NormalizedAnimation }
    [Serializable]
    public sealed class PetSkillCue {
        [Tooltip("Prefab chua ParticleSystem hoac VisualEffect. Co the de trong.")]
        public GameObject effectPrefab;
        public AudioClip sound;
        [Tooltip("Khoa trong danh sach socket cua presentation player. De trong de dung goc pet.")]
        public string socket;
        public bool followSocket;
        [Tooltip("Vi tri theo socket; goc xoay va offset theo truc actor, phu hop VFX tai mieng.")]
        public bool actorSpace;
        [Tooltip("Aim a following channel effect along the combat direction.")]
        public bool followChannelAim;
        [Tooltip("Center and stretch a following effect over the visible actor bounds.")]
        public bool fitActorBounds;
        [Tooltip("Đặt gốc hiệu ứng ở chân và giữ toàn bộ vòng bảo vệ ở phía trên mốc này.")]
        public bool anchorAtActorFeet;
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
        [Range(.05f, .95f)] public float releaseNormalized = .36f;
        [Range(.1f, 1)] public float chainNormalized = .85f;
        [Range(.75f, 1.25f)] public float playbackSpeed = 1;
        [Tooltip("So giay sau khi animation bat dau, ngoai khoang chuan bi truoc animation cua skill.")]
        [Min(0)] public float releaseDelaySeconds = 0.25f;
        [Tooltip("Gioi han thoi gian nhan callback va cham that; khong tu tao dan hoac damage.")]
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
