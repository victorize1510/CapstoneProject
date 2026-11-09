using System.Collections.Generic;
using Capstone.Game.Skills;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Visual authoring only: no AI, ownership, health, cooldown or save state.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class CreatureVisualBinding : MonoBehaviour {
        public CreatureAnimationProfile animationProfile;
        public PetSpeciesSkillSet skillSet;
        public List<PetSkillSocket> sockets = new List<PetSkillSocket>();
    }
}
