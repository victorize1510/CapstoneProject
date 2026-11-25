using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.Skills
{
    public enum PetCombatPhase
    {
        Ready,
        Retreat,
        Windup,
        Released,
        Recovery,
        Positioning
    }

    /// <summary>Moi actor co mot dong tac da cam ket va mot lenh dem ngan; VFX khong tinh thoi diem damage.</summary>
    [DefaultExecutionOrder(-60), DisallowMultipleComponent]
    public sealed class PetCombatActionDriver : MonoBehaviour
    {
        const float BufferSeconds = .2f;
        static readonly int Speed = Animator.StringToHash("CombatSpeed");
        PetController pet;
        CreatureCombatContext context;
        PetHudRuntimeStats stats;
        PetSkillPresentationPlayer presentation;
        PetSkillCombatExecutor execution;
        CreatureAnimationDriver binding;
        Animator animator;
        SkillHudData skill;
        PetSkillPresentationProfile profile;
        int hash, token, revision, queued = -1;
        int emissionCount = 1, nextEmission = 1;
        float burstEndNormalized;
        string queuedId;
        float queuedUntil, startedAt, animationStartedAt, normalized, clipDuration, chainRemaining;
        bool animationStarted, released, focused = true;
        bool returnAfterAction;
        public PetCombatPhase Phase { get; private set; }

        public bool IsSkillBusy => Phase != PetCombatPhase.Ready;
        public int BufferedSlot => queued;
        public int StartedCount { get; private set; }

        public int CompletedCount { get; private set; }

        public int CancelledCount { get; private set; }

        public float LastReleaseNormalized { get; private set; }

        public float Normalized => normalized;
        public string LastRejection { get; private set; }
        public float LastRejectionAt { get; private set; } = float.NegativeInfinity;

        internal bool Reject(string reason)
        {
            LastRejection = reason;
            LastRejectionAt = Time.unscaledTime;
            return false;
        }

        public int CastToken => token;
        internal void InterruptWindup() {
            if (Phase == PetCombatPhase.Windup && !released) Cancel();
        }
        int positioningSlot;
        CreatureCombatTarget positioningTarget;
        PetSkillCombatProfile positioningProfile;
        float positioningStarted, positioningProgressAt, retreatUsed;
        Vector3 positioningLast;
        bool positioningMoving;
        void PositioningAnimation(bool moving) {
            if(positioningMoving==moving)return;positioningMoving=moving;
            binding?.TryPlay(moving?CreatureAction.Move:CreatureAction.Idle);
        }
        bool BeginPositioned(int slot, SkillHudData candidate) {
            if (stats.SpeciesSkillSet == null || !stats.SpeciesSkillSet.TryGet(candidate.skillId, out var entry) || entry.skill.combat == null)
                return Reject("Kỹ năng thiếu cấu hình cho dạng pet hiện tại.");
            var data = entry.skill.combat;
            if (!GetComponent<CreatureCombatant>().MayUseSkill(data))
                return Reject("Pet chưa thể tấn công trong trạng thái hiện tại.");
            var executor=GetComponent<PetSkillCombatExecutor>()??gameObject.AddComponent<PetSkillCombatExecutor>();
            var victim=executor.ResolveIntentTarget(data);
            if (data.delivery == PetSkillDelivery.SelfBuff) return StartSkill(slot, candidate);
            if (!context.IsEnemy && data.delivery != PetSkillDelivery.Melee)
            {
                if (context.CanHit(victim) && !executor.InReach(victim, data))
                {
                    if (data.allowFreeCastWhenTargetUnavailable && !data.RequiresTarget)
                        return StartSkillWithTarget(slot, candidate, executor, default, victim.Center - transform.position);
                    return Reject("Mục tiêu ngoài tầm hoặc bị che khuất. Hãy đưa pet đến gần hơn.");
                }

                if (!victim.Exists && data.RequiresTarget)
                    return Reject("Cần mục tiêu trong tầm kỹ năng.");

                return StartSkillWithTarget(slot, candidate, executor, victim);
            }
            if (!context.CanHit(victim))
            {
                if (!data.RequiresTarget) return StartSkill(slot, candidate);
                return Reject("Cần mục tiêu trong tầm kỹ năng.");
            }
            animator=stats.VisualAnimator;revision=binding!=null?binding.BindingRevision:0;
            positioningSlot=slot;skill=candidate;positioningTarget=victim;positioningProfile=data;
            positioningStarted=positioningProgressAt=Time.time;positioningLast=transform.position;retreatUsed=0;
            Phase=PetCombatPhase.Positioning;
            if (!context.IsEnemy) pet?.HoldCoordinatedSkill();
            return true;
        }
        void PositionSkill() {
            if (!context.CanHit(positioningTarget) || Time.time - positioningStarted > 3f)
            {
                FinishPositioningOrReject("Mục tiêu hết hiệu lực hoặc quá thời gian vào tầm.");
                return;
            }
            Vector3 delta=positioningTarget.Center-transform.position;delta.y=0;
            context.Face(delta);
            var executor = GetComponent<PetSkillCombatExecutor>();
            float distance = delta.magnitude;
            bool near = positioningProfile.delivery == PetSkillDelivery.Melee;
            float desired = Mathf.Min(positioningProfile.preferredDistance, positioningProfile.CastRange * .85f);
            bool advance = !executor.InReach(positioningTarget, positioningProfile);
            bool retreat = !advance && !near && distance < desired - .5f && retreatUsed < positioningProfile.retreatDistance;
            if(!advance&&!retreat){
                StartPositionedSkill(executor, positioningTarget);
                return;
            }
            float advanceSpeed = near && !context.IsEnemy && pet != null ? pet.MeleeApproachSpeed(distance) : 3f;
            float step=(retreat?positioningProfile.retreatSpeed:advanceSpeed)*Time.deltaTime;
            if(retreat)step=Mathf.Min(step,positioningProfile.retreatDistance-retreatUsed);
            bool moved=context.Retreat(delta.normalized*(retreat?-1:1),step);
            PositioningAnimation(moved);
            if(retreat&&moved)retreatUsed+=step;
            if(Vector3.Distance(positioningLast,transform.position)>.03f){positioningProgressAt=Time.time;positioningLast=transform.position;}
            if(!moved&&retreat){retreatUsed=positioningProfile.retreatDistance;return;}
            if (Time.time - positioningProgressAt > 1f)
                FinishPositioningOrReject("Không tìm được đường vào tầm kỹ năng.");
        }

        void FinishPositioningOrReject(string reason)
        {
            if (!context.IsEnemy && !positioningProfile.RequiresTarget)
                StartPositionedSkill(GetComponent<PetSkillCombatExecutor>(), default);
            else
            {
                Reject(reason);
                Cancel();
            }
        }

        void StartPositionedSkill(PetSkillCombatExecutor executor, CreatureCombatTarget target)
        {
            if (!stats.PrepareCoordinatedSkill(positioningSlot, out var candidate) || candidate.skillId != skill.skillId)
            {
                Reject(string.IsNullOrEmpty(stats.LastSkillRejection) ? "Kỹ năng vừa thay đổi." : stats.LastSkillRejection);
                Cancel();
                return;
            }
            Phase = PetCombatPhase.Ready;
            if (!StartSkillWithTarget(positioningSlot, candidate, executor, target))
                Finish(false);
        }

        bool StartSkillWithTarget(int slot, SkillHudData candidate, PetSkillCombatExecutor executor,
            CreatureCombatTarget target, Vector3 fallbackDirection = default)
        {
            executor.SkipNextRetreat = true;
            executor.StageTarget(target, fallbackDirection);
            if (StartSkill(slot, candidate)) return true;
            executor.ClearTargetIntent();
            return false;
        }

        void OnEnable()
        {
            pet = GetComponent<PetController>();
            stats = GetComponent<PetHudRuntimeStats>();
            context = CreatureCombatContext.Get(gameObject);
            presentation = GetComponent<PetSkillPresentationPlayer>();
            binding = GetComponent<CreatureAnimationDriver>();
            pet?.BindCombatActions(this);
        }

        void OnDisable()
        {
            Cancel();
        }

        void OnApplicationFocus(bool value)
        {
            focused = value;
            if (!value && !Capstone.Game.UISystem.GameplayPause.IsPaused && (context == null || !context.IsEnemy))
            {
                Cancel();
            }
        }

        bool InputReady()
        {
            return stats != null && context != null && context.CanAct && (context.IsEnemy || focused);
        }

        public bool Submit(int slot)
        {
            LastRejection = "";
            if (!InputReady() || !stats.PrepareCoordinatedSkill(slot, out var candidate))
            {
                return Reject(!string.IsNullOrEmpty(stats?.LastSkillRejection) ? stats.LastSkillRejection : "Kỹ năng chưa sẵn sàng.");
            }

            if (IsSkillBusy)
            {
                if (chainRemaining > BufferSeconds || !released)
                    return Reject("Pet đang thực hiện kỹ năng khác.");

                queued = slot;
                queuedId = candidate.skillId;
                queuedUntil = Time.time + BufferSeconds + .025f;
                return true;
            }

            return BeginPositioned(slot, candidate);
        }

        bool StartSkill(int slot, SkillHudData candidate)
        {
            PositioningAnimation(false);
            animator = stats.VisualAnimator;
            profile = stats.GetSkillPresentation(candidate.skillId);
            if (animator == null || profile == null)
                return Reject("Kỹ năng thiếu animation hoặc cấu hình hiệu ứng.");

            hash = 0;
            foreach (string name in candidate.animatorStates)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                int value = Animator.StringToHash(name.Contains(".") ? name : "Base Layer." + name);
                if (animator.HasState(0, value))
                {
                    hash = value;
                    break;
                }
            }

            if (hash == 0)
                return Reject("Dạng pet này thiếu animation kỹ năng.");

            revision = binding != null ? binding.BindingRevision : 0;
            skill = candidate;
            released = animationStarted = false;
            normalized = 0;
            startedAt = Time.time;
            clipDuration = Mathf.Max(.1f, skill.animationDuration);
            chainRemaining = 10;
            Phase = PetCombatPhase.Retreat;
            if (!context.IsEnemy)
            {
                pet?.HoldCoordinatedSkill();
            }

            if (!stats.CommitCoordinatedSkill(slot, candidate))
            {
                Finish(false);
                return Reject(string.IsNullOrEmpty(stats.LastSkillRejection) ? "Kỹ năng chưa sẵn sàng." : stats.LastSkillRejection);
            }

            execution = GetComponent<PetSkillCombatExecutor>();
            presentation = GetComponent<PetSkillPresentationPlayer>();
            token = presentation != null ? presentation.ActiveCastId : 0;
            if (token == 0)
            {
                Cancel();
                return false;
            }

            emissionCount = presentation.EmissionCount(token);
            nextEmission = 1;
            burstEndNormalized = profile.releaseNormalized;
            if (stats.SpeciesSkillSet != null && stats.SpeciesSkillSet.TryGet(candidate.skillId, out var entry) && entry.skill.combat != null)
            {
                burstEndNormalized = Mathf.Max(profile.releaseNormalized, entry.skill.combat.burstEndNormalized);
            }

            StartedCount++;
            if (!context.IsEnemy && pet != null && pet.owner != null)
                CombatEngagement.Select(pet.owner.GetComponent<PetCommandInput>(), execution?.LastCastTarget);
            if (execution == null || !execution.IsRetreating)
            {
                StartAnimation();
            }

            return true;
        }

        void StartAnimation()
        {
            animationStarted = true;
            animationStartedAt = Time.time;
            Phase = PetCombatPhase.Windup;
            foreach (var parameter in animator.parameters)
            {
                if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float)
                {
                    animator.SetFloat(Speed, profile.playbackSpeed);
                    break;
                }
            }

            animator.CrossFadeInFixedTime(hash, .09f, 0, 0);
            bool hasSkillSound = skill.combatProfile?.authoredStatusRules == true
                ? skill.combatProfile.authoredReleaseSound != null : profile.release?.sound != null;
            binding?.Cry?.BeginSkill(binding, hash, hasSkillSound: hasSkillSound);
            presentation.StartCoordinatedAnimation(token);
        }

        void Update()
        {
            if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if (!InputReady() || (IsSkillBusy && ((binding != null && binding.BindingRevision != revision) || animator != stats.VisualAnimator)))
            {
                if (IsSkillBusy) Reject("Pet hiện không thể nhận lệnh kỹ năng.");
                Cancel();
                return;
            }

            if (IsSkillBusy)
            {
                if(Phase==PetCombatPhase.Positioning){PositionSkill();return;}
                if (!released && Time.time - startedAt > 5)
                {
                    Reject("Animation kỹ năng không hoàn tất.");
                    Cancel();
                    return;
                }

                if (!animationStarted && (execution == null || !execution.IsRetreating))
                {
                    StartAnimation();
                }

                if (execution != null)
                {
                    bool channel = skill.combatProfile != null && (skill.combatProfile.pattern == PetSkillPattern.Cone || skill.combatProfile.pattern == PetSkillPattern.Beam);
                    if (channel) context.FaceChannel(execution.AimDirection);
                    else context.Face(execution.AimDirection);
                }
            }

            if (queued < 0)
            {
                return;
            }

            if (Time.time > queuedUntil)
            {
                ClearBuffer();
                return;
            }

            if (IsSkillBusy)
            {
                return;
            }

            int slot = queued;
            string id = queuedId;
            ClearBuffer();
            if (stats.PrepareCoordinatedSkill(slot, out var candidate) && candidate.skillId == id)
            {
                BeginPositioned(slot, candidate);
            }
        }

        void LateUpdate()
        {
            if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if (!IsSkillBusy || !animationStarted || animator == null)
            {
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                var next = animator.GetNextAnimatorStateInfo(0);
                if (next.fullPathHash == hash)
                {
                    state = next;
                }
            }

            bool inCastState = state.fullPathHash == hash;
            if (!inCastState && !released)
            {
                if (Time.time - animationStartedAt > .4f)
                {
                    Reject("Không vào được animation kỹ năng.");
                    Cancel();
                }

                return;
            }

            // A blend out after release must not cancel the launched projectiles or a channel.
            float elapsedNormalized = (Time.time - animationStartedAt) * profile.playbackSpeed / Mathf.Max(.05f, clipDuration);
            normalized = Mathf.Max(normalized, inCastState ? state.normalizedTime : elapsedNormalized);
            chainRemaining = Mathf.Max(0, profile.chainNormalized - normalized) * clipDuration / Mathf.Max(.1f, profile.playbackSpeed);
            if (!released && normalized >= profile.releaseNormalized && presentation.NotifyRelease(token))
            {
                released = true;
                Phase = PetCombatPhase.Released;
                LastReleaseNormalized = normalized;
            }

            while (released && nextEmission < emissionCount && normalized >= Mathf.Lerp(profile.releaseNormalized, burstEndNormalized, nextEmission / (float)(emissionCount - 1)))
            {
                presentation.NotifySubRelease(token, nextEmission);
                nextEmission++;
            }

            if (released)
            {
                Phase = PetCombatPhase.Recovery;
            }

            if (released && normalized >= profile.chainNormalized && nextEmission >= emissionCount && execution?.ChannelActive(token) != true)
            {
                Finish(true);
            }
        }

        void ClearBuffer()
        {
            queued = -1;
            queuedId = null;
            queuedUntil = 0;
        }

        void Finish(bool completed)
        {
            binding?.Cry?.CancelSkill();
            PositioningAnimation(false);
            if (animator != null)
            {
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.nameHash == Speed && parameter.type == AnimatorControllerParameterType.Float)
                    {
                        animator.SetFloat(Speed, 1);
                        break;
                    }
                }
            }

            if (completed)
            {
                presentation?.FinishCoordinatedAnimation(token);
                CompletedCount++;
            }

            Phase = PetCombatPhase.Ready;
            animationStarted = released = false;
            chainRemaining = 0;
            if (context == null || !context.IsEnemy)
            {
                pet?.ReleaseCoordinatedSkill();
                if (returnAfterAction || skill.combatProfile?.delivery == PetSkillDelivery.Melee)
                    pet?.RequestReturnNearOwner();
            }
            returnAfterAction = false;
        }

        public void Cancel()
        {
            returnAfterAction = false;
            ClearBuffer();
            if (!IsSkillBusy)
            {
                return;
            }

            presentation?.CancelPresentation();
            execution?.Cancel();
            CancelledCount++;
            Finish(false);
        }

        public void ReturnToOwner() => RequestReturn();

        public bool RequestReturn()
        {
            pet?.CancelSkillPursuit();
            ClearBuffer();
            if (IsSkillBusy && released)
            {
                returnAfterAction = true;
                return true;
            }
            presentation?.CancelUnreleased();
            execution?.CancelWindup();
            if (IsSkillBusy)
            {
                CancelledCount++;
                Finish(false);
            }
            return false;
        }
    }
}
