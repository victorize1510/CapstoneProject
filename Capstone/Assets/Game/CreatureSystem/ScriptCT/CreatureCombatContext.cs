using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    /// <summary>Cache quyen nhan lenh dung chung cho input pet va quyet dinh cua enemy.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CreatureCombatant))]
    public sealed class CreatureCombatContext : MonoBehaviour
    {
        CreatureCombatant combat;
        PetController pet;
        DummyEnemy enemy;
        BasicCreatureEnemy brain;
        public static CreatureCombatContext Get(GameObject go) => go.GetComponent<CreatureCombatContext>() ?? go.AddComponent<CreatureCombatContext>();
        void Awake()
        {
            combat = GetComponent<CreatureCombatant>();
            pet = GetComponent<PetController>();
            enemy = GetComponent<DummyEnemy>();
            brain = GetComponent<BasicCreatureEnemy>();
        }

        public bool IsEnemy => combat != null && combat.IsEnemy;
        public bool CanAct
        {
            get
            {
                if (combat == null || !combat.IsAlive || combat.Stunned || UISystem.GameplayPause.IsPaused)
                {
                    return false;
                }

                if (IsEnemy)
                {
                    if (brain == null)
                    {
                        brain = GetComponent<BasicCreatureEnemy>();
                    }

                    return enemy != null && !enemy.CaptureLocked && brain != null && brain.isActiveAndEnabled;
                }

                if (pet == null || !pet.CanReceiveCommands || Inventory.InventoryInputController.GameplayInputBlocked)
                {
                    return false;
                }

                var input = pet.owner != null ? pet.owner.GetComponent<PetCommandInput>() : null;
                return input == null || (input.activePet == pet && input.CanUsePetSkills);
            }
        }

        public CreatureCombatTarget Target => IsEnemy && brain != null ? new CreatureCombatTarget(brain.Target) : default;
        public bool CanHit(CreatureCombatTarget target)
        {
            if (!target.Exists || !target.Alive || target.Object == gameObject || target.IsEnemy == IsEnemy)
            {
                return false;
            }

            return !IsEnemy || (brain != null && brain.CanTarget(target.Combatant));
        }

        public bool Retreat(Vector3 direction, float distance) => combat != null && !combat.MovementLocked
            && (IsEnemy ? brain != null && brain.TryRetreatStep(direction, distance) : pet != null && pet.TrySkillRetreatStep(direction, distance));
        public void FaceChannel(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 100 * Time.deltaTime);
        }
        public void Face(Vector3 direction)
        {
            if (IsEnemy)
            {
                direction.y = 0;
                if (direction.sqrMagnitude > .001f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540 * Time.deltaTime);
                }
            }
            else
            {
                pet?.FaceCombatDirection(direction);
            }
        }
    }
}
