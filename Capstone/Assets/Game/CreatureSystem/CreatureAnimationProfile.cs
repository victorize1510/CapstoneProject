using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    public enum CreatureAction { None, Idle, Move, Walk, Run, Fly, Spawn, Attack, Bite, Cast, Blast, Projectile, WingStrike, Hit, Death }
    [Serializable] public sealed class CreatureActionBinding {
        public CreatureAction action;
        [Tooltip("Exact Animator state path. Source clips and controllers keep their original names.")]
        public string state;
    }
    [Serializable] public sealed class CreatureSkillAction {
        public string skillId;
        public CreatureAction action;
    }
    [CreateAssetMenu(menuName = "Capstone/Creatures/Animation Profile", fileName = "Animation_")]
    public sealed class CreatureAnimationProfile : ScriptableObject {
        public string definitionId;
        public string formId;
        public List<CreatureActionBinding> actions = new List<CreatureActionBinding>();
        public List<CreatureSkillAction> skills = new List<CreatureSkillAction>();
    }
}
