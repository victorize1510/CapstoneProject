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

        public int CastToken => token;
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
            if(stats.SpeciesSkillSet==null||!stats.SpeciesSkillSet.TryGet(candidate.skillId,out var entry)||entry.skill.combat==null)return false;
            var data=entry.skill.combat;
            var input=pet!=null&&pet.owner!=null?pet.owner.GetComponent<PetCommandInput>():null;
            var executor=GetComponent<PetSkillCombatExecutor>()??gameObject.AddComponent<PetSkillCombatExecutor>();
            var victim=executor.ResolveIntentTarget(data);
            if(data.delivery==PetSkillDelivery.SelfBuff||!victim.Alive)return StartSkill(slot,candidate);
            if(!context.CanHit(victim))return false;
            animator=stats.VisualAnimator;revision=binding!=null?binding.BindingRevision:0;
            positioningSlot=slot;skill=candidate;positioningTarget=victim;positioningProfile=data;
            positioningStarted=positioningProgressAt=Time.time;positioningLast=transform.position;retreatUsed=0;
            Phase=PetCombatPhase.Positioning;
            if(!context.IsEnemy){pet?.HoldCoordinatedSkill();CombatEngagement.Select(input,victim.Enemy);}
            return true;
        }
        void PositionSkill() {
            if(!positioningTarget.Alive||!context.CanHit(positioningTarget)||Time.time-positioningStarted>3){LastRejection="Mục tiêu hết hiệu lực hoặc quá thời gian vào tầm.";Cancel();return;}
            Vector3 delta=positioningTarget.Center-transform.position;delta.y=0;
            context.Face(delta);
            float distance=Vector3.Distance(context.GetComponent<CreatureCombatant>().Center,positioningTarget.Center);
            bool near=positioningProfile.delivery==PetSkillDelivery.Melee;
            float desired=near?Mathf.Max(.2f,positioningProfile.range*.8f):Mathf.Min(positioningProfile.preferredDistance,positioningProfile.range*.85f);
            bool advance=distance>(near?desired:positioningProfile.range*.9f);
            bool retreat=!near&&delta.magnitude<desired-.5f&&retreatUsed<positioningProfile.retreatDistance;
            if(!advance&&!retreat){
                if(!stats.PrepareCoordinatedSkill(positioningSlot,out var candidate)||candidate.skillId!=skill.skillId){Cancel();return;}
                var exec=GetComponent<PetSkillCombatExecutor>()??gameObject.AddComponent<PetSkillCombatExecutor>();exec.SkipNextRetreat=true;exec.StageTarget(positioningTarget);
                Phase=PetCombatPhase.Ready;
                if(!StartSkill(positioningSlot,candidate)){if(exec!=null)exec.SkipNextRetreat=false;Finish(false);}
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
            if(Time.time-positioningProgressAt>1){LastRejection="Không tìm được bước di chuyển an toàn.";Cancel();}
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
                LastRejection = "Kỹ năng chưa sẵn sàng.";
                return false;
            }

            float remaining = IsSkillBusy ? chainRemaining : pet != null && !context.IsEnemy ? pet.BasicSkillDelay : 0;
            if (IsSkillBusy || remaining > 0)
            {
                if (remaining > BufferSeconds || (IsSkillBusy && !released))
                {
                    LastRejection = "Đang thực hiện động tác.";
                    return false;
                }

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
            {
                return false;
            }

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
            {
                return false;
            }

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
                return false;
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
                if(IsSkillBusy)LastRejection="Actor, quyền điều khiển hoặc dạng thú đã thay đổi.";
                Cancel();
                return;
            }

            if (IsSkillBusy)
            {
                if(Phase==PetCombatPhase.Positioning){PositionSkill();return;}
                if (Time.time - startedAt > 5)
                {
                    LastRejection = "Animation không hoàn tất; đã hủy an toàn.";
                    Cancel();
                    return;
                }

                if (!animationStarted && (execution == null || !execution.IsRetreating))
                {
                    StartAnimation();
                }

                if (execution != null)
                {
                    context.Face(execution.AimDirection);
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

            if (IsSkillBusy || (pet != null && !context.IsEnemy && pet.BasicSkillDelay > 0))
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

            if (state.fullPathHash != hash)
            {
                if (Time.time - animationStartedAt > .4f)
                {
                    LastRejection = "Không vào được animation kỹ năng.";
                    Cancel();
                }

                return;
            }

            normalized = Mathf.Max(normalized, state.normalizedTime);
            chainRemaining = Mathf.Max(0, profile.chainNormalized - normalized) * clipDuration / Mathf.Max(.1f, profile.playbackSpeed);
            if (!released && normalized >= profile.releaseNormalized)
            {
                released = true;
                Phase = PetCombatPhase.Released;
                LastReleaseNormalized = normalized;
                presentation.NotifyRelease(token);
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

            if (released && normalized >= profile.chainNormalized)
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
            }
            if (returnAfterAction)
            {
                returnAfterAction = false;
                pet?.RequestReturnNearOwner();
            }
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
