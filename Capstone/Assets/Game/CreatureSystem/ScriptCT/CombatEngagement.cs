using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    // Muc tieu giao tranh tach khoi khoa camera va lua chon thu phuc; khong luu save.
    [DisallowMultipleComponent]
    public sealed class CombatEngagement : MonoBehaviour
    {
        public DummyEnemy Primary { get; private set; }
        public static void Select(PetCommandInput input, DummyEnemy enemy)
        {
            if (input == null || enemy == null || !enemy.IsAlive) return;
            var state = input.GetComponent<CombatEngagement>() ?? input.gameObject.AddComponent<CombatEngagement>();
            if (state.Primary == enemy) return;
            var previous = state.Primary;
            state.Primary = enemy;
            enemy.GetComponent<BasicCreatureEnemy>()?.AcceptCombatIntent(input.transform);
            if (previous != null && previous.IsAlive && !previous.IsBoss)
                previous.GetComponent<BasicCreatureEnemy>()?.DisengageFrom(input.transform);
        }
        public DummyEnemy HudTarget
        {
            get
            {
                foreach (var enemy in DummyEnemy.ActiveEnemies)
                {
                    if (enemy == null || !enemy.IsBoss || !enemy.IsAlive) continue;
                    var brain = enemy.GetComponent<BasicCreatureEnemy>();
                    if (brain != null && brain.isActiveAndEnabled && brain.ThreatOwner == transform && brain.State!="Return" && brain.State!="Recover")
                        return enemy;
                }
                var primaryBrain=Primary!=null?Primary.GetComponent<BasicCreatureEnemy>():null;
                if(primaryBrain!=null&&(primaryBrain.State=="Return"||primaryBrain.State=="Recover"))Primary=null;
                return Primary != null && Primary.IsAlive ? Primary : null;
            }
        }
    }
}
