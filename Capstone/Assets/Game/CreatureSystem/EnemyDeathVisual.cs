using UnityEngine;
namespace Capstone.Game.CreatureSystem {
    // Duong an visual cho actor khong co EncounterSpawnPoint; khong tu them respawn.
    public sealed class EnemyDeathVisual:MonoBehaviour {
        DummyEnemy enemy;CreatureAnimationDriver driver;float started;bool warned;
        public string CompletionReason { get; private set; }
        void Awake(){enemy=GetComponent<DummyEnemy>();driver=GetComponent<CreatureAnimationDriver>();started=Time.time;}
        void LateUpdate(){
            if(UISystem.GameplayPause.IsPaused||enemy==null||enemy.IsAlive||enemy.IsDowned)return;
            var animator=driver!=null?driver.BoundAnimator:null;
            bool ready=animator==null||driver==null||!driver.TryGetStates(CreatureAction.Death,out _);
            if(ready)CompletionReason="MissingAnimation";
            float watchdog=3;
            if(animator!=null&&driver!=null&&driver.TryGetStates(CreatureAction.Death,out var states)){
                var info=animator.GetCurrentAnimatorStateInfo(0);
                foreach(var state in states)if(info.IsName(state)){watchdog=Mathf.Max(3,info.length+1);if(info.normalizedTime>=1){ready=true;CompletionReason="Animation";}}
            }
            if(!ready&&Time.time-started<watchdog)return;
            if(!ready){CompletionReason="Watchdog";WarnStuck();}
            if((animator==null||driver==null||!driver.TryGetStates(CreatureAction.Death,out _))&&!warned){warned=true;WarnMissing();}
            enemy.DeathVisualComplete=true;
            if(!enemy.DeathManagedByEncounter){
                var settings=Resources.Load<CombatFeelSettings>("CombatFeelSettings");
                Skills.CombatEffectPool.PlayOneShot(settings!=null?settings.deathSmoke:null,enemy.TargetPosition);
                gameObject.SetActive(false);
            }
            enabled=false;
        }
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void WarnMissing(){Debug.LogWarning("[PvE] Missing Dead mapping/Animator; hidden safely: "+name,this);}
        [System.Diagnostics.Conditional("UNITY_EDITOR"),System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void WarnStuck(){Debug.LogWarning("[PvE] Dead animation did not complete; bounded fallback: "+name,this);}
    }
}
