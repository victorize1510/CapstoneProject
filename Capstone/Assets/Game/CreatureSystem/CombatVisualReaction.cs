using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    /// <summary>Recoil nhe va dung hinh cuc bo; khong di chuyen actor hoac collider.</summary>
    [DefaultExecutionOrder(80), DisallowMultipleComponent]
    public sealed class CombatVisualReaction : MonoBehaviour
    {
        PetHudRuntimeStats stats;
        PetController pet;
        bool resolved;
        Animator animator, pausedAnimator;
        Transform visual;
        Quaternion baseRotation;
        float recoilAt = -1, pauseUntil, previousSpeed, nextPause;
        bool rotated;
        CreatureAnimationDriver driver;
        BasicCreatureEnemy enemy;
        float hitUntil, nextHit;
        public bool IsHitPlaying => Time.time < hitUntil;
        void Awake()
        {
            stats = GetComponent<PetHudRuntimeStats>();
            pet = GetComponent<PetController>();
            Bind();
        }

        void Bind()
        {
            if (stats == null && resolved)
            {
                return;
            }

            var next = stats != null ? stats.VisualAnimator : GetComponentInChildren<Animator>();
            resolved = true;
            if (animator == next)
            {
                return;
            }

            Restore();
            animator = next;
            visual = next != null && next.transform != transform ? next.transform : null;
        }

        public void Recoil()
        {
            Bind();
            if (stats != null && stats.Health <= 0)
            {
                return;
            }

            recoilAt = Capstone.Game.UISystem.GameplayPause.PlayTime;
            if (Time.time < nextHit)
            {
                return;
            }

            if (driver == null)
            {
                driver = GetComponent<CreatureAnimationDriver>();
            }

            if (enemy == null)
            {
                enemy = GetComponent<BasicCreatureEnemy>();
            }

            var action = GetComponent<Skills.PetCombatActionDriver>();
            // Khong restart don da cam ket hoac cast dang chay chi de hien clip bi danh.
            if ((enemy != null && enemy.HasCommittedAttack) || (action != null && action.IsSkillBusy) || (pet != null && pet.BasicSkillDelay > 0))
            {
                return;
            }

            if (driver != null && driver.TryPlay(CreatureAction.Hit, .06f))
            {
                hitUntil = Time.time + .28f;
                nextHit = Time.time + .35f;
                pet?.HoldHitReaction(.28f);
            }
        }

        public void Pause(float seconds)
        {
            Bind();
            if (seconds <= 0 || animator == null || !animator.enabled || Capstone.Game.UISystem.GameplayPause.PlayTime < nextPause)
            {
                return;
            }

            RestorePause();
            nextPause = Capstone.Game.UISystem.GameplayPause.PlayTime + .2f;
            previousSpeed = animator.speed;
            pausedAnimator = animator;
            pausedAnimator.speed = 0;
            pauseUntil = Capstone.Game.UISystem.GameplayPause.PlayTime + Mathf.Clamp(seconds, 0, .05f);
        }

        void Update()
        {
            if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            Bind();
            RestoreRotation();
            if(stats!=null&&stats.Health<=0){Restore();return;}
            if (hitUntil > 0 && Time.time >= hitUntil)
            {
                hitUntil = 0;
                pet?.FinishHitReaction();
                enemy?.FinishHitReaction();
            }

            if (pausedAnimator != null && Capstone.Game.UISystem.GameplayPause.PlayTime >= pauseUntil)
            {
                RestorePause();
            }

            bool ownedPet = pet != null && !pet.IsWildEncounter;
            if ((ownedPet && InventoryInputController.GameplayInputBlocked) || PetEvolutionPresentationController.AnyPlaying || (ownedPet && !pet.CanReceiveCommands))
            {
                Restore();
            }
        }

        void LateUpdate()
        {
            if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if (visual == null || recoilAt < 0)
            {
                return;
            }

            float t = (Capstone.Game.UISystem.GameplayPause.PlayTime - recoilAt) / .12f;
            if (t >= 1)
            {
                recoilAt = -1;
                return;
            }

            baseRotation = visual.localRotation;
            visual.localRotation = baseRotation * Quaternion.Euler(-3 * Mathf.Sin(t * Mathf.PI), 0, 0);
            rotated = true;
        }

        void RestorePause()
        {
            if (pausedAnimator != null && Mathf.Approximately(pausedAnimator.speed, 0))
            {
                pausedAnimator.speed = previousSpeed;
            }

            pausedAnimator = null;
        }

        void RestoreRotation()
        {
            if (rotated && visual != null)
            {
                visual.localRotation = baseRotation;
            }

            rotated = false;
        }

        void Restore()
        {
            RestorePause();
            RestoreRotation();
            recoilAt = -1;
            if (hitUntil > 0)
            {
                hitUntil = 0;
                pet?.FinishHitReaction();
                enemy?.FinishHitReaction();
            }
        }

        void OnDisable()
        {
            Restore();
        }

        void OnApplicationFocus(bool value)
        {
            if (!value && !Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                Restore();
            }
        }
    }
}
