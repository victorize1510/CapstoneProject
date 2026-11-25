using System;
using UnityEngine;

namespace Capstone.Game.PetSummon
{
    // Mot cach doc clip va moc tha bong cho ca trieu hoi va thu phuc.
    internal static class PlayerThrowPlayback
    {
        public static bool TryRelease(BasicPlayerMovement movement, PlayerVisualSwitcher switcher, int fallbackFrame, out float normalized)
        {
            normalized = 0;
            var animator = switcher != null ? switcher.ActiveVisualAnimator : movement != null ? movement.animator : null;
            if (animator == null || animator.runtimeAnimatorController == null) return false;
            AnimationClip clip = null;
            var playing = animator.IsInTransition(0) ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
            string state = movement != null ? movement.throwState : "Throw";
            var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == Animator.StringToHash(state) && playing.Length > 0) clip = playing[0].clip;
            if (clip == null)
            {
                foreach (var candidate in animator.runtimeAnimatorController.animationClips)
                {
                    if (candidate.name.IndexOf("Throw", StringComparison.OrdinalIgnoreCase) >= 0) { clip = candidate; break; }
                }
            }
            if (clip == null || clip.length <= 0 || clip.frameRate <= 0) return false;
            var profile = switcher != null ? switcher.ActiveVisualProfile : null;
            int frame = profile != null && profile.throwReleaseFrame > 0 ? profile.throwReleaseFrame : fallbackFrame;
            normalized = frame / (clip.length * clip.frameRate);
            return normalized > 0 && normalized < 1;
        }

        public static bool TryProgress(BasicPlayerMovement movement, PlayerVisualSwitcher switcher, out float normalized)
        {
            normalized = 0;
            var animator = switcher != null ? switcher.ActiveVisualAnimator : movement != null ? movement.animator : null;
            if (animator == null) return false;
            int hash = Animator.StringToHash(movement != null ? movement.throwState : "Throw");
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == hash) { normalized = next.normalizedTime; return true; }
            }
            var current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash != hash) return false;
            normalized = current.normalizedTime;
            return true;
        }
    }
}
