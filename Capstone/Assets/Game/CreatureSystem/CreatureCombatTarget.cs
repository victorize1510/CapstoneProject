using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    /// <summary>Dung duoc ca dummy legacy va combatant that; khong tao duong tinh damage song song.</summary>
    public readonly struct CreatureCombatTarget
    {
        public readonly CreatureCombatant Combatant;
        public readonly DummyEnemy Enemy;
        public GameObject Object => Combatant != null ? Combatant.gameObject : Enemy != null ? Enemy.gameObject : null;
        public bool Exists => Object != null;
        public bool IsEnemy => Combatant != null ? Combatant.IsEnemy : Enemy != null && Enemy.enabled;
        public bool Alive => Combatant != null ? Combatant.CanBeAttacked : Enemy != null && Enemy.isActiveAndEnabled && Enemy.IsAlive && !Enemy.CaptureLocked;
        public Vector3 Center => Combatant != null ? Combatant.Center : Enemy != null ? Enemy.TargetPosition : Vector3.zero;
        public CreatureCombatTarget(CreatureCombatant value)
        {
            Combatant = value;
            Enemy = value != null ? value.GetComponent<DummyEnemy>() : null;
        }

        public CreatureCombatTarget(DummyEnemy value)
        {
            Enemy = value;
            Combatant = value != null ? value.GetComponent<CreatureCombatant>() : null;
        }

        public static CreatureCombatTarget From(Collider collider)
        {
            if (collider == null)
            {
                return default;
            }

            var combat = collider.GetComponentInParent<CreatureCombatant>();
            return combat != null ? new CreatureCombatTarget(combat) : new CreatureCombatTarget(collider.GetComponentInParent<DummyEnemy>());
        }

        public Vector3 ClosestPoint(Vector3 point) => Combatant != null ? Combatant.ClosestPoint(point) : Enemy != null ? Enemy.ClosestPoint(point) : point;
        public CombatDamageResult Apply(CombatHit hit) => Combatant != null ? Combatant.ApplyCombatHit(hit) : Enemy != null ? Enemy.ApplyCombatHit(hit) : new CombatDamageResult(hit, null, 0, false);
    }
}
