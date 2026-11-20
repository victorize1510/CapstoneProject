using System;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Skills;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.CreatureSystem {
    /// <summary>One encounter owner. The captured individual is detached, never recycled or destroyed.</summary>
    [DisallowMultipleComponent]
    public sealed class EncounterSpawnPoint : MonoBehaviour {
        public GameObject encounterPrefab;
        public DummyEnemy initialActor;
        public GameObject smokePrefab, downedStarsPrefab;
        public bool openingEncounter;
        [Min(.1f)] public float respawnDelay = 5;
        public Vector3 actorScale = Vector3.one;
        public EnemySettings settings = new EnemySettings();
        [Serializable] public sealed class EnemySettings {
            public float aggroRadius = 10, leashRadius = 18, attackRange = 2.3f, moveSpeed = 3,
                attackInterval = 1.4f, hitDelay = .35f, attackDuration = .85f, patrolRadius = 3.5f;
            public Collider territory;
            public bool useSkills=true;
            [Range(0,1)] public float skillChance=.35f;
            public float decisionInterval=.3f,pathInterval=.25f,searchSeconds=2;
            public void Read(BasicCreatureEnemy brain) {
                aggroRadius=brain.aggroRadius;leashRadius=brain.leashRadius;attackRange=brain.attackRange;moveSpeed=brain.moveSpeed;
                attackInterval=brain.attackInterval;hitDelay=brain.hitDelay;attackDuration=brain.attackDuration;patrolRadius=brain.patrolRadius;territory=brain.territory;
                useSkills=brain.useSkills;skillChance=brain.skillChance;decisionInterval=brain.decisionInterval;pathInterval=brain.pathInterval;searchSeconds=brain.searchSeconds;
            }
            public void Apply(BasicCreatureEnemy brain) {
                brain.aggroRadius=aggroRadius;brain.leashRadius=leashRadius;brain.attackRange=attackRange;brain.moveSpeed=moveSpeed;
                brain.attackInterval=attackInterval;brain.hitDelay=hitDelay;brain.attackDuration=attackDuration;brain.patrolRadius=patrolRadius;brain.territory=territory;
                brain.useSkills=useSkills;brain.skillChance=skillChance;brain.decisionInterval=decisionInterval;brain.pathInterval=pathInterval;brain.searchSeconds=searchSeconds;
            }
        }
        enum Phase { Live, Death, Waiting }
        Phase phase;
        DummyEnemy actor;
        WildPetCaptureTarget capture;
        Action<GameObject> defeatHandler;
        CreatureAnimationDriver driver;
        GameObject smoke;
        float phaseAt, nextSpawnAt, smokeUntil;
        internal CreatureEncounterZone ZoneOwner;
        readonly Collider[] overlaps = new Collider[32];
        public DummyEnemy CurrentActor => actor;
        public int Generation { get; private set; }
        public bool RespawnPending => phase == Phase.Waiting;
        public float RespawnAt => nextSpawnAt;
        public string LifecycleState => phase.ToString();
        void OnEnable() {
            if (actor == null && initialActor != null) Attach(initialActor);
            else if (actor != null) Attach(actor);
            else { phase=Phase.Waiting;nextSpawnAt=ZoneOwner!=null?Time.time:Time.time+respawnDelay; }
        }
        void OnDisable() {
            Unsubscribe(); ReturnSmoke(); Generation++;
            // No coroutines, delayed invokes or global listeners survive this owner.
            nextSpawnAt=float.PositiveInfinity;
        }
        void Unsubscribe() { if (actor != null && defeatHandler != null) actor.Defeated -= defeatHandler; defeatHandler=null; }
        void Attach(DummyEnemy next) {
            next.DeathManagedByEncounter=true;next.DeathVisualComplete=false;
            Unsubscribe(); actor=next;capture=next.GetComponent<WildPetCaptureTarget>();driver=next.GetComponent<CreatureAnimationDriver>();
            next.autoRevive=false;Generation++;phase=Phase.Live;
            var expected=next;
            defeatHandler = _ => { if(isActiveAndEnabled && actor==expected && phase==Phase.Live) { phase=Phase.Death;phaseAt=Time.time; } };
            next.Defeated+=defeatHandler;
            if(capture != null && !capture.IsWild) DetachCaptured();
            else if(!next.IsAlive && !next.IsDowned){phase=Phase.Death;phaseAt=Time.time;}
        }
        void DetachCaptured() {
            Unsubscribe(); if(initialActor==actor)initialActor=null;
            // Do not reparent, hide, destroy, reset HP or reuse a captured actor.
            actor=null;capture=null;driver=null;ScheduleRespawn();
        }
        void ScheduleRespawn() { Generation++;phase=Phase.Waiting;nextSpawnAt=Time.time+Mathf.Max(.1f,respawnDelay); }
        void Update() {
            if(UISystem.GameplayPause.IsPaused)return;
            if(smoke!=null && Time.time>=smokeUntil)ReturnSmoke();
            if(phase==Phase.Live) {
                if(actor==null){ScheduleRespawn();return;}
                if(capture!=null && !capture.IsWild){DetachCaptured();return;}
            } else if(phase==Phase.Death) {
                if(actor==null){ScheduleRespawn();return;}
                if(DeathFinished()) {
                    actor.DeathVisualComplete=true;
                    smoke=CombatEffectPool.Rent(smokePrefab,actor.TargetPosition,Quaternion.identity);
                    smokeUntil=Time.time+2;
                    actor.gameObject.SetActive(false);
                    Unsubscribe();if(initialActor==actor)initialActor=null;
                    Destroy(actor.gameObject);actor=null;capture=null;driver=null;
                    ScheduleRespawn();
                }
            } else if(Time.time>=nextSpawnAt) {
                if(!TrySpawn())nextSpawnAt=Time.time+1; // Blocked home retries; no overlapping replacements.
            }
        }
        bool DeathFinished() {
            var animator=driver!=null?driver.BoundAnimator:null;
            if(animator==null||!animator.isActiveAndEnabled)return true;
            if(Time.time-phaseAt<.08f)return false;
            if(!driver.TryGetStates(CreatureAction.Death,out var states))return true;
            var state=animator.GetCurrentAnimatorStateInfo(0);
            foreach(string name in states)if(state.IsName(name))return state.normalizedTime>=1f||Time.time-phaseAt>Mathf.Max(3,state.length+1);
            return Time.time-phaseAt>=3;
        }
        bool TrySpawn() {
            // Chi cac slot do Zone tao moi dung nhanh nay; encounter cu giu nguyen.
            if (ZoneOwner != null) {
                if (actor != null || !ZoneOwner.TryCreateActor(this, out var zoneActor)) return false;
                Attach(zoneActor);
                return true;
            }
            if(encounterPrefab==null || actor!=null || !FindSafeHome(out var position))return false;
            var go=Instantiate(encounterPrefab,position,transform.rotation);
            go.transform.localScale=actorScale;
            var next=go.GetComponent<DummyEnemy>();
            if(next==null){Destroy(go);return false;}
            var brain=go.GetComponent<BasicCreatureEnemy>()??go.AddComponent<BasicCreatureEnemy>();settings.Apply(brain);
            var level=go.GetComponent<EncounterLevel>()??go.AddComponent<EncounterLevel>();level.openingEncounter=openingEncounter;
            var stars=go.GetComponent<DownedStatusVisual>()??go.AddComponent<DownedStatusVisual>();stars.effectPrefab=downedStarsPrefab;
            Attach(next);level.Initialize();return true;
        }
        bool FindSafeHome(out Vector3 position) {
            position=default;
            var capsule=encounterPrefab!=null?encounterPrefab.GetComponent<CapsuleCollider>():null;
            float scale=Mathf.Max(Mathf.Abs(actorScale.x),Mathf.Abs(actorScale.z));
            float radius=capsule!=null?Mathf.Max(.15f,capsule.radius*scale):.5f;
            float height=capsule!=null?Mathf.Max(radius*2,capsule.height*Mathf.Abs(actorScale.y)):1.5f;
            for(int attempt=0;attempt<7;attempt++) {
                Vector2 offset=attempt==0?Vector2.zero:UnityEngine.Random.insideUnitCircle*2;
                Vector3 point=transform.position+new Vector3(offset.x,0,offset.y);
                if(!NavMesh.SamplePosition(point,out var nav,.75f,NavMesh.AllAreas)||Mathf.Abs(nav.position.y-transform.position.y)>.6f)continue;
                Vector3 center=nav.position+(capsule!=null?transform.rotation*Vector3.Scale(capsule.center,actorScale):Vector3.up*height*.5f);
                Vector3 end=Vector3.up*Mathf.Max(0,height*.5f-radius);
                int count=Physics.OverlapCapsuleNonAlloc(center-end+Vector3.up*.08f,center+end+Vector3.up*.08f,radius*.95f,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                if(count!=0)continue;
                position=nav.position;return true;
            }
            return false;
        }
        void ReturnSmoke() { if(smoke!=null)CombatEffectPool.Return(smoke);smoke=null; }
    }
}
