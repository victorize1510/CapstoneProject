using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Lives on the Animator object, including replacement evolution visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class PetSkillAnimationRelay : MonoBehaviour {
        PetSkillPresentationPlayer owner;
        Animator animator;
        public void Bind(PetSkillPresentationPlayer player, Animator source) { owner = player; animator = source; }
        public void OnPetSkillRelease(AnimationEvent animationEvent) {
            if (owner != null && animationEvent != null) owner.NotifyAnimationRelease(animator, animationEvent.animatorStateInfo);
        }
    }
}
