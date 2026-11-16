using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Basic-attack brain only. No NPC behavior, skill casting, spawning or capture ownership.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DummyEnemy), typeof(CreatureCombatant), typeof(CreatureAnimationDriver))]
    public sealed class BasicCreatureEnemy : MonoBehaviour {
        [Min(1)] public float aggroRadius = 10;
        [Min(2)] public float leashRadius = 18;
        [Min(.2f)] public float attackRange = 2.3f;
        [Min(.1f)] public float moveSpeed = 3;
        [Min(.2f)] public float attackInterval = 1.4f;
        [Min(.05f)] public float hitDelay = .35f;
        [Min(.1f)] public float attackDuration = .85f;
        [Min(0)] public float patrolRadius=3.5f;
        [Tooltip("Optional territory trigger. Empty uses the existing home/leash radius.")]
        public Collider territory;
        public bool AtHome=>initialized&&(transform.position-home).sqrMagnitude<=.36f;
        NavMeshPath checkedPath;
        Vector3 patrolPoint;float patrolAt;bool pathBlocked;CreatureAction lastAction;
        public string State { get; private set; } = "Idle";
        public int LandedHits { get; private set; }
        public CreatureCombatant Target => target;
        DummyEnemy health;
        CreatureCombatant combat, target;
        CreatureAnimationDriver animationDriver;
        PetCommandInput input;
        NavMeshAgent agent;
        CapsuleCollider capsule;
        Vector3 home;
        float pollAt, hitAt, recoverAt, nextAttack, pathAt, navAt;
        bool pendingHit, initialized;
        readonly RaycastHit[] sightHits = new RaycastHit[24];
        void Awake() { checkedPath=new NavMeshPath();health = GetComponent<DummyEnemy>(); combat = GetComponent<CreatureCombatant>(); animationDriver = GetComponent<CreatureAnimationDriver>(); agent = GetComponent<NavMeshAgent>(); capsule = GetComponent<CapsuleCollider>(); }
        void Start() { home = transform.position; initialized = true;patrolAt=Time.time+Random.Range(2f,4f); }
        void OnDisable() { pendingHit = false; target = null; Stop(); }
        void Update() {
            if (!initialized) return;
            if (!health.IsAlive) { pendingHit = false; target = null; Stop(); SetState(health.IsDowned?"Downed":"Dead", health.IsDowned?CreatureAction.Downed:CreatureAction.Death); return; }
            if (health.CaptureLocked) { pendingHit = false; Stop(); return; }
            if (Time.time >= pollAt) {
                pollAt = Time.time + .3f;
                if (input == null) input = FindFirstObjectByType<PetCommandInput>();
                var pet = input != null ? input.activePet : null;
                var candidate = pet != null ? pet.GetComponent<CreatureCombatant>() : null;
                if (candidate == null && pet != null && pet.CanReceiveCommands && pet.GetComponent<Capstone.Game.HudSystem.PetHudRuntimeStats>() != null) candidate = pet.gameObject.AddComponent<CreatureCombatant>();
                if (target != null && (!target.CanBeAttacked || !InsideTerritory(target.transform.position))) target = null;
                if (State!="Return"&&target == null && candidate != null && candidate.CanBeAttacked && Vector3.Distance(transform.position, candidate.transform.position) <= aggroRadius && InsideTerritory(candidate.transform.position)) target = candidate;
            }
            if (target == null || !target.CanBeAttacked || !InsideTerritory(transform.position)) {
                target = null; pendingHit = false;
                if(State=="Patrol"&&health.CurrentHealth>=health.maxHealth&&Time.time<patrolAt){if((transform.position-patrolPoint).sqrMagnitude<.16f){Stop();SetState("Patrol",CreatureAction.Idle);}else Move(patrolPoint,"Patrol");return;}
                if (!AtHome) Move(home, "Return");
                else if(health.CurrentHealth<health.maxHealth){Stop();SetState("Recover",CreatureAction.Idle);}
                else{
                    if(State=="Recover")GetComponent<PveCombatState>()?.ResetEncounter();
                    Stop();SetState("Idle",CreatureAction.Idle);
                    if(patrolRadius>0&&Time.time>=patrolAt){patrolAt=Time.time+Random.Range(4f,7f);Vector2 offset=Random.insideUnitCircle*patrolRadius;Vector3 next=home+new Vector3(offset.x,0,offset.y);if(InsideTerritory(next)&&NavMesh.SamplePosition(next,out var point,1,NavMesh.AllAreas)){patrolPoint=point.position;Move(patrolPoint,"Patrol");}}
                }
                return;
            }
            Face(target.transform.position);
            if (pendingHit && Time.time >= hitAt) {
                pendingHit = false;
                if (InRange() && ClearSight()) {
                    var roll = combat.RollOffense(CreatureDamageKind.Physical);
                    if (target.ReceiveHit(new CombatHit(gameObject, "basic", 0, roll.RawDamage, CreatureDamageKind.Physical, roll.Critical, false, target.Center))) LandedHits++;
                }
            }
            if (Time.time < recoverAt) { Stop(); return; }
            if (!InRange()) { Move(target.transform.position, "Chase"); return; }
            Stop();
            if (Time.time >= nextAttack && ClearSight()) {
                nextAttack = Time.time + attackInterval * combat.AttackIntervalScale;
                hitAt = Time.time + hitDelay; recoverAt = Time.time + attackDuration; pendingHit = true;
                State = "Attack";lastAction=CreatureAction.Attack; animationDriver.TryPlay(CreatureAction.Attack);
            } else SetState("Idle", CreatureAction.Idle);
        }
        bool InsideTerritory(Vector3 point)=>territory!=null&&territory.enabled&&territory.gameObject.activeInHierarchy?(territory.ClosestPoint(point)-point).sqrMagnitude<.0025f:(point-home).sqrMagnitude<=leashRadius*leashRadius;
        bool InRange() => target != null && Vector3.Distance(combat.Center, target.Center) <= attackRange;
        bool ClearSight() {
            Vector3 delta = target.Center - combat.Center;
            int count = Physics.RaycastNonAlloc(combat.Center, delta.normalized, sightHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            // If the buffer saturates, fail closed instead of attacking through unknown geometry.
            if (count == sightHits.Length) return false;
            for (int i = 0; i < count; i++) if (!sightHits[i].transform.IsChildOf(transform) && !sightHits[i].transform.IsChildOf(target.transform)) return false;
            return true;
        }
        void Move(Vector3 destination, string state) {
            if (agent == null) { Stop();SetState(state,CreatureAction.Idle);return; }
            if (!agent.enabled || !agent.isOnNavMesh) {
                if (Time.time < navAt) { Stop();SetState(state,CreatureAction.Idle);return; } navAt = Time.time + 1;
                if (!NavMesh.SamplePosition(transform.position, out var hit, 2, NavMesh.AllAreas)||Mathf.Abs(hit.position.y-transform.position.y)>.5f) { Stop();SetState(state,CreatureAction.Idle);return; }
                agent.enabled = true; agent.Warp(hit.position);
            }
            if (!agent.isOnNavMesh) return;
            agent.speed = moveSpeed * combat.MovementScale; agent.stoppingDistance = state == "Chase" ? attackRange * .75f : .3f;
            if (Time.time >= pathAt) {
                pathAt = Time.time + .2f;
                pathBlocked=!NavMesh.SamplePosition(destination,out var point,1.5f,agent.areaMask)||!agent.CalculatePath(point.position,checkedPath)||checkedPath.status!=NavMeshPathStatus.PathComplete;
                if(!pathBlocked)agent.SetPath(checkedPath);
            }
            if(pathBlocked){Stop();SetState(state,CreatureAction.Idle);return;}
            agent.isStopped = false;
            SetState(state, CreatureAction.Move);
        }
        void MoveDirect(Vector3 destination,string state) {
            Vector3 delta=destination-transform.position;delta.y=0;
            if(delta.sqrMagnitude<.01f){Stop();return;}
            float step=Mathf.Min(delta.magnitude,moveSpeed*combat.MovementScale*Time.deltaTime);
            if(capsule==null){SetState("Idle",CreatureAction.Idle);return;}
            Vector3 center=transform.TransformPoint(capsule.center),up=Vector3.up*Mathf.Max(0,capsule.height*.5f-capsule.radius);
            int count=Physics.CapsuleCastNonAlloc(center-up+Vector3.up*.06f,center+up+Vector3.up*.06f,capsule.radius*.9f,delta.normalized,sightHits,step+.05f,~0,QueryTriggerInteraction.Ignore);
            if(count==sightHits.Length)return;
            for(int i=0;i<count;i++)if(!sightHits[i].transform.IsChildOf(transform)){SetState("Idle",CreatureAction.Idle);return;}
            Vector3 next=transform.position+delta.normalized*step;
            count=Physics.RaycastNonAlloc(next+Vector3.up*1.5f,Vector3.down,sightHits,3f,~0,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;Vector3 ground=default;bool found=false;
            for(int i=0;i<count;i++)if(!sightHits[i].transform.IsChildOf(transform)&&sightHits[i].normal.y>.6f&&sightHits[i].distance<nearest){nearest=sightHits[i].distance;ground=sightHits[i].point;found=true;}
            if(!found||Mathf.Abs(ground.y-transform.position.y)>.45f){SetState("Idle",CreatureAction.Idle);return;}
            next.y=ground.y;transform.position=next;Face(destination);SetState(state,CreatureAction.Move);
        }
        void Stop() { if (agent != null && agent.enabled && agent.isOnNavMesh) { agent.isStopped = true; if (agent.hasPath) agent.ResetPath(); } }
        void Face(Vector3 point) { Vector3 delta = point - transform.position; delta.y = 0; if (delta.sqrMagnitude > .01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(delta), 540 * Time.deltaTime); }
        void SetState(string state, CreatureAction action) { if (State == state&&lastAction==action) return; State = state;lastAction=action;if(!animationDriver.TryPlay(action)&&action==CreatureAction.Downed)animationDriver.TryPlay(CreatureAction.Death); }
    }
}
