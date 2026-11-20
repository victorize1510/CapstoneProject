using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    [Serializable] public sealed class PetDamageContribution {
        public PetController pet;
        public string petId;
        public int level;
        public float healthLost;
    }
    [DisallowMultipleComponent]
    public sealed class PveCombatState : MonoBehaviour {
        readonly List<PetDamageContribution> contributions=new List<PetDamageContribution>(8);
        CreatureCombatant combat;PetController pet;PetHudRuntimeStats stats;DummyEnemy enemy;BasicCreatureEnemy brain;
        float lastUnsafe,nextTick,lastTick;
        public IReadOnlyList<PetDamageContribution> Contributions=>contributions;
        public float SafeSeconds=>Mathf.Max(0,Time.time-lastUnsafe);
        void Awake(){combat=GetComponent<CreatureCombatant>();pet=GetComponent<PetController>();stats=GetComponent<PetHudRuntimeStats>();enemy=GetComponent<DummyEnemy>();brain=GetComponent<BasicCreatureEnemy>();lastUnsafe=lastTick=Time.time;}
        public void ResetEncounter(){contributions.Clear();lastUnsafe=Time.time;}
        static PveCombatState Get(GameObject target){if(target==null)return null;var state=target.GetComponent<PveCombatState>();if(state==null)state=target.AddComponent<PveCombatState>();return state;}
        internal static void Record(CombatDamageResult result){
            if(result.HpLost<=0)return;
            var source=Get(result.Hit.Source);var target=Get(result.Target);if(source!=null)source.lastUnsafe=Time.time;if(target==null)return;target.lastUnsafe=Time.time;
            var pet=result.Hit.Source!=null?result.Hit.Source.GetComponent<PetController>():null;
            if(pet==null||pet.IsWildEncounter||pet.owner==null)return;
            var metadata=pet.GetComponent<PetCollectionMetadata>();var stats=pet.GetComponent<PetHudRuntimeStats>();if(metadata==null||stats==null)return;
            PetDamageContribution item=null;foreach(var existing in target.contributions)if(existing.petId==metadata.PetId&&existing.level==stats.Level){item=existing;break;}
            if(item==null){item=new PetDamageContribution{pet=pet,petId=metadata.PetId,level=stats.Level};target.contributions.Add(item);}item.healthLost+=result.HpLost;
        }
        void Update(){
            if(UISystem.GameplayPause.IsPaused)return;
            if(Time.time<nextTick)return;nextTick=Time.time+.25f;float delta=Mathf.Min(.35f,Mathf.Max(0,Time.time-lastTick));lastTick=Time.time;
            if(stats==null||combat==null||!combat.IsAlive)return;
            if(combat.IsEnemy){
                if(brain==null)brain=GetComponent<BasicCreatureEnemy>();
                if(brain==null||!brain.AtHome||brain.Target!=null||brain.State=="Return"||enemy.CaptureLocked){lastUnsafe=Time.time;return;}
                if(SafeSeconds>=8)enemy.RecoverHealth(stats.MaxHealth*.1f*delta);
                return;
            }
            if(pet==null||!pet.IsSummoned||!pet.CanReceiveCommands){lastUnsafe=Time.time;return;}
            bool pursued=pet.HasCombatTarget;
            foreach(var hostile in DummyEnemy.ActiveEnemies){var ai=hostile!=null&&hostile.IsAlive?hostile.GetComponent<BasicCreatureEnemy>():null;if(ai!=null&&ai.enabled&&ai.Target==combat){pursued=true;break;}}
            if(pursued){lastUnsafe=Time.time;return;}
            if(SafeSeconds<8)return;
            float health=Mathf.Min(stats.MaxHealth,stats.Health+stats.MaxHealth*.05f*delta);
            if(Mathf.Abs(health-stats.Health)>.0001f)stats.SetStatus(health,stats.MaxHealth,0,0);
        }
    }
}
