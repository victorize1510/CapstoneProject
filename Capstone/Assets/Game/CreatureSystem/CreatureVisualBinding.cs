using System.Collections.Generic;
using Capstone.Game.Skills;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Visual authoring only: no AI, ownership, health, cooldown or save state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class CreatureVisualBinding : MonoBehaviour {
        public CreatureAnimationProfile animationProfile;
        public PetSpeciesSkillSet skillSet;
        [Tooltip("Visual cue scale only; never changes hit radius, range, damage or MP.")]
        [Range(.25f, 3)] public float presentationScale = 1;
        public List<PetSkillSocket> sockets = new List<PetSkillSocket>();

        [Header("Kích thước actor theo Form (0 = giữ thiết lập prefab)")]
        [Min(0)] public float actorRadius;
        [Min(0)] public float actorHeight;
        public Vector3 actorCenter;

        internal bool TryApplyActorGeometry(CapsuleCollider capsule) {
            if (actorRadius <= 0f || actorHeight < 2f * actorRadius) return false;
            capsule.radius = actorRadius;
            capsule.height = actorHeight;
            capsule.center = actorCenter;
            if (capsule.TryGetComponent<PetController>(out var pet)) {
                pet.minimumNavigationRadius = actorRadius;
                pet.directBodyRadius = actorRadius;
            }
            if (capsule.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var agent)) {
                agent.radius = actorRadius;
                agent.height = actorHeight;
            }
            return true;
        }
    }
}
