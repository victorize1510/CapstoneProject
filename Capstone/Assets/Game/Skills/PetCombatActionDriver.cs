using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.Skills {
    public enum PetCombatPhase { Ready, Retreat, Windup, Released, Recovery }

    /// <summary>One actor, one committed animation, one short input buffer. No gameplay timers in VFX.</summary>
    [DefaultExecutionOrder(-60), DisallowMultipleComponent]
    public sealed class PetCombatActionDriver : MonoBehaviour {
        const float BufferSeconds = .2f;
        static readonly int Speed = Animator.StringToHash("CombatSpeed");
        PetController pet;
        PetHudRuntimeStats stats;
        PetSkillPresentationPlayer presentation;
        PetSkillCombatExecutor execution;
        CreatureAnimationDriver binding;
        Animator animator;
        SkillHudData skill;
        PetSkillPresentationProfile profile;
        int hash, token, revision, queued = -1;
        string queuedId;
        float queuedUntil, startedAt, animationStartedAt, normalized, clipDuration, chainRemaining;
        bool animationStarted, released, focused = true;
        public PetCombatPhase Phase { get; private set; }
        public bool IsSkillBusy => Phase != PetCombatPhase.Ready;
        public int BufferedSlot => queued;
        public int StartedCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int CancelledCount { get; private set; }
        public float LastReleaseNormalized { get; private set; }
        public float Normalized => normalized;
        public string LastRejection { get; private set; }
        public int CastToken => token;
        void OnEnable() {
            pet = GetComponent<PetController>(); stats = GetComponent<PetHudRuntimeStats>();
            presentation = GetComponent<PetSkillPresentationPlayer>(); binding = GetComponent<CreatureAnimationDriver>();
            pet?.BindCombatActions(this);
        }
        void OnDisable() { Cancel(); }
        void OnApplicationFocus(bool value) { focused = value; if (!value) Cancel(); }
        bool InputReady() {
            if (!focused || pet == null || !pet.CanReceiveCommands || stats == null || stats.Health <= 0 || InventoryInputController.GameplayInputBlocked) return false;
            var owner = pet.owner != null ? pet.owner.GetComponent<PetCommandInput>() : null;
            return owner == null || (owner.activePet == pet && owner.CanUsePetSkills);
        }
        public bool Submit(int slot) {
            LastRejection = "";
            if (!InputReady() || !stats.PrepareCoordinatedSkill(slot, out var candidate)) { LastRejection = "Kỹ năng chưa sẵn sàng."; return false; }
            float remaining = IsSkillBusy ? chainRemaining : pet.BasicSkillDelay;
            if (IsSkillBusy || remaining > 0) {
                if (remaining > BufferSeconds || (IsSkillBusy && !released)) { LastRejection = "Đang thực hiện động tác."; return false; }
                queued = slot; queuedId = candidate.skillId; queuedUntil = Time.time + BufferSeconds + .025f; return true;
            }
            return StartSkill(slot, candidate);
        }
        bool StartSkill(int slot, SkillHudData candidate) {
            animator = stats.VisualAnimator; profile = stats.GetSkillPresentation(candidate.skillId);
            if (animator == null || profile == null || profile.releaseMode != PetSkillReleaseMode.NormalizedAnimation) return false;
            hash = 0;
            foreach (string name in candidate.animatorStates) {
                if (string.IsNullOrWhiteSpace(name)) continue;
                int value = Animator.StringToHash(name.Contains(".") ? name : "Base Layer." + name);
                if (animator.HasState(0, value)) { hash = value; break; }
            }
            if (hash == 0) return false;
            revision = binding != null ? binding.BindingRevision : 0;
            skill = candidate; released = animationStarted = false; normalized = 0;
            startedAt = Time.time; clipDuration = Mathf.Max(.1f, skill.animationDuration);
            chainRemaining = 10; Phase = PetCombatPhase.Retreat;
            pet.HoldCoordinatedSkill();
            if (!stats.CommitCoordinatedSkill(slot, candidate)) { Finish(false); return false; }
            execution = GetComponent<PetSkillCombatExecutor>(); presentation = GetComponent<PetSkillPresentationPlayer>();
            token = presentation != null ? presentation.ActiveCastId : 0;
            if (token == 0) { Cancel(); return false; }
            StartedCount++;
            if (execution == null || !execution.IsRetreating) StartAnimation();
            return true;
        }
        void StartAnimation() {
            animationStarted = true; animationStartedAt = Time.time; Phase = PetCombatPhase.Windup;
            foreach (var parameter in animator.parameters) if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float) { animator.SetFloat(Speed, profile.playbackSpeed); break; }
            animator.CrossFadeInFixedTime(hash, .09f, 0, 0);
            presentation.StartCoordinatedAnimation(token);
        }
        void Update() {
            if (!InputReady() || (IsSkillBusy && ((binding != null && binding.BindingRevision != revision) || animator != stats.VisualAnimator))) { Cancel(); return; }
            if (IsSkillBusy) {
                if (Time.time - startedAt > 5) { LastRejection = "Animation không hoàn tất; đã hủy an toàn."; Cancel(); return; }
                if (!animationStarted && (execution == null || !execution.IsRetreating)) StartAnimation();
                if (execution != null) pet.FaceCombatDirection(execution.AimDirection);
            }
            if (queued < 0) return;
            if (Time.time > queuedUntil) { ClearBuffer(); return; }
            if (IsSkillBusy || pet.BasicSkillDelay > 0) return;
            int slot = queued; string id = queuedId; ClearBuffer();
            if (stats.PrepareCoordinatedSkill(slot, out var candidate) && candidate.skillId == id) StartSkill(slot, candidate);
        }
        void LateUpdate() {
            if (!IsSkillBusy || !animationStarted || animator == null) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0)) {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash == hash) state = next;
            }
            if (state.fullPathHash != hash) {
                if (Time.time - animationStartedAt > .4f) { LastRejection = "Không vào được animation kỹ năng."; Cancel(); }
                return;
            }
            normalized = Mathf.Max(normalized, state.normalizedTime);
            chainRemaining = Mathf.Max(0, profile.chainNormalized - normalized) * clipDuration / Mathf.Max(.1f, profile.playbackSpeed);
            if (!released && normalized >= profile.releaseNormalized) {
                released = true; Phase = PetCombatPhase.Released; LastReleaseNormalized = normalized;
                presentation.NotifyRelease(token);
            }
            if (released) Phase = PetCombatPhase.Recovery;
            if (released && normalized >= profile.chainNormalized) Finish(true);
        }
        void ClearBuffer() { queued = -1; queuedId = null; queuedUntil = 0; }
        void Finish(bool completed) {
            if (animator != null) foreach (var parameter in animator.parameters)
                if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float) { animator.SetFloat(Speed, 1); break; }
            if (completed) { presentation?.FinishCoordinatedAnimation(token); CompletedCount++; }
            Phase = PetCombatPhase.Ready; animationStarted = released = false; chainRemaining = 0;
            pet?.ReleaseCoordinatedSkill();
        }
        public void Cancel() {
            ClearBuffer();
            if (!IsSkillBusy) return;
            presentation?.CancelPresentation(); execution?.Cancel();
            CancelledCount++; Finish(false);
        }
    }
}
