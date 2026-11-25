using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Lives on the Animator object, including replacement evolution visuals.</summary>
    [DisallowMultipleComponent]
    public sealed class PetSkillAnimationRelay : MonoBehaviour {
        PetSkillPresentationPlayer owner;
        Animator animator;
        PetCryEventReceiver cry;
        public void Bind(PetSkillPresentationPlayer player, Animator source) {
            owner = player; animator = source; cry = GetComponent<PetCryEventReceiver>();
        }
        public void OnPetSkillRelease(AnimationEvent animationEvent) {
            if (owner != null && animationEvent != null && (cry == null || !cry.IsCosmetic))
                owner.NotifyAnimationRelease(animator, animationEvent.animatorStateInfo);
        }
    }
}
