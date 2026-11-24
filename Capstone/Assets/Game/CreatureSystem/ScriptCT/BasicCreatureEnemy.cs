using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.AI;
using Unity.Profiling;

namespace Capstone.Game.CreatureSystem
{
    public enum EnemyAiState
    {
        Idle,
        Patrol,
        Chase,
        Position,
        Attack,
        Search,
        Return,
        Recover,
        Paused,
        Downed,
        Captured,
        Dead
    }

    /// <summary>AI chi quyet dinh hanh dong; combat chung phat don, encounter quan ly tai sinh.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DummyEnemy), typeof(CreatureCombatant), typeof(CreatureAnimationDriver))]
    public sealed class BasicCreatureEnemy : MonoBehaviour
    {
        static readonly ProfilerMarker TickMarker = new ProfilerMarker("Capstone.EnemyAI.Tick");
        [Tooltip("Ban kinh prefab cu; di ngang khong kich dong AI nay.")]
        [Min(1)]
        public float aggroRadius = 10;
        [Min(2)]
        public float leashRadius = 18;
        [Min(.2f)]
        public float attackRange = 2.3f;
        [Min(.1f)]
        public float moveSpeed = 3;
        [Min(.2f)]
        public float attackInterval = 1.4f;
        [Tooltip("Field cu; don trung that theo tien do animation.")]
        [Min(.05f)]
        public float hitDelay = .35f;
        [Min(.1f)]
        public float attackDuration = .85f;
        [Min(0)]
        public float patrolRadius = 3.5f;
        public Collider territory;
        [Header("Phan cong va ky nang")]
        public bool useSkills = true;
        [Range(0, 1)]
        public float skillChance = .35f;
        [Min(.1f)]
        public float decisionInterval = .3f;
        [Min(.1f)]
        public float pathInterval = .25f;
        [Min(.1f)]
        public float searchSeconds = 2;
        public EnemyAiState CurrentState { get; private set; } = EnemyAiState.Idle;
        public string State => CurrentState.ToString();
        public string LastReason { get; private set; }

        public int LandedHits { get; private set; }

        public int PathRequests => navigation != null ? navigation.PathRequests : 0;
        public CreatureCombatant Target => target;
        public Transform ThreatOwner => threatOwner;
        public Vector3 Home => home;
        public bool AtHome => initialized && (transform.position - home).sqrMagnitude <= .36f;
        public bool HasCommittedAttack => basicActive || (actions != null && actions.IsSkillBusy);
        internal System.Func<float> DecisionRandomSource;
        DummyEnemy health;
        CreatureCombatant combat, target, basicTarget;
        CreatureAnimationDriver animationDriver;
        PetCombatActionDriver actions;
        PetHudRuntimeStats stats;
        CombatVisualReaction reaction;
        NavMeshAgent agent;
        EnemyNavigationAgent navigation;
        PetCommandInput threatInput;
        Transform threatOwner;
        PetSummon.PetSummonDirector threatSummon;
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly int[] eligible = new int[4];
        System.Random random;
        Vector3 home, patrolPoint, lastSeen;
        float decisionAt, patrolAt, nextAttack, lostAt = -1, basicAt, replacementUntil;
        int lastSkill = -1, basicHash, basicRevision, basicToken;
        Capstone.Game.Elements.CombatElementSnapshot basicElements;
        bool initialized, basicActive, basicReleased, waitingForReplacement;
        CreatureAction lastAction;
        EnemyAiState beforePause;
        void Awake()
        {
            health = GetComponent<DummyEnemy>();
            combat = GetComponent<CreatureCombatant>();
            stats = GetComponent<PetHudRuntimeStats>();
            animationDriver = GetComponent<CreatureAnimationDriver>();
            agent = GetComponent<NavMeshAgent>();
            reaction = GetComponent<CombatVisualReaction>();
            navigation = new EnemyNavigationAgent(transform, agent, GetComponent<CapsuleCollider>(), InsideTerritory);
            random = new System.Random(System.Guid.NewGuid().GetHashCode());
            if (agent != null)
            {
                agent.updateRotation = false;
                agent.avoidancePriority = 25 + ((health.EncounterId.GetHashCode() & 0x7fffffff) % 50);
                agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            }

            CreatureCombatContext.Get(gameObject);
            actions = GetComponent<PetCombatActionDriver>() ?? gameObject.AddComponent<PetCombatActionDriver>();
        }

        void OnEnable()
        {
            CombatDamageEvents.Applied += Damaged;
        }

        void Start()
        {
            home = transform.position;
            navigation.ResetProgress();
            initialized = true;
            patrolAt = Time.time + .5f + (float)random.NextDouble() * 2;
        }

        void OnDisable()
        {
            CombatDamageEvents.Applied -= Damaged;
            animationDriver?.Cry?.CancelAmbient();
            CancelActions();
            target = null;
            threatOwner = null;
            threatInput = null;
            threatSummon = null;
            waitingForReplacement = false;
            replacementUntil = 0;
            Stop();
        }

        internal void FinishHitReaction()
        {
            if(health!=null&&!health.IsAlive)return;
            lastAction = CreatureAction.None;
        }

        public bool CanTarget(CreatureCombatant candidate)
        {
            if (candidate == null || !candidate.CanBeAttacked || candidate.IsEnemy || threatOwner == null)
            {
                return false;
            }

            var pet = candidate.GetComponent<PetController>();
            return pet != null && !pet.IsWildEncounter && pet.owner == threatOwner && InsideTerritory(candidate.transform.position);
        }

        Transform disengagedOwner;
        public void AcceptCombatIntent(Transform owner) { if (disengagedOwner == owner) disengagedOwner = null; }
        public void DisengageFrom(Transform owner) {
            if (health == null || !health.IsAlive || health.IsBoss) return;
            disengagedOwner = owner;
            BeginReturn("Player chose another opponent");
        }
        void Damaged(CombatDamageResult result)
        {
            if (!initialized || result.Target != gameObject || result.HpLost <= 0 || !health.IsAlive || health.CaptureLocked || GameplayPause.IsPaused)
            {
                return;
            }

            var pet = result.Hit.Source != null ? result.Hit.Source.GetComponent<PetController>() : null;
            if (pet == null || pet.IsWildEncounter || pet.owner == null)
            {
                return;
            }

            if (reaction == null)
            {
                reaction = GetComponent<CombatVisualReaction>();
            }

            if (pet.owner == disengagedOwner) return;
            bool first = target == null;
            threatOwner = pet.owner;
            threatInput = threatOwner.GetComponent<PetCommandInput>();
            threatSummon = threatOwner.GetComponent<PetSummon.PetSummonDirector>();
            var candidate = pet.GetComponent<CreatureCombatant>();
            if (!CanTarget(candidate))
            {
                candidate = FindThreatPet();
            }

            if (!CanTarget(candidate))
            {
                if (WaitingForReplacement())
                {
                    waitingForReplacement = true;
                    return;
                }

                BeginReturn("Damager has no targetable pet inside territory");
                return;
            }

            target = candidate;
            lastSeen = target.transform.position;
            lostAt = -1;
            decisionAt = Time.time + .25f;
            if (first)
            {
                nextAttack = Mathf.Max(nextAttack, decisionAt);
            }

            if (CurrentState == EnemyAiState.Return || CurrentState == EnemyAiState.Recover || CurrentState == EnemyAiState.Idle || CurrentState == EnemyAiState.Patrol)
            {
                SetState(EnemyAiState.Position);
            }
        }

        CreatureCombatant FindThreatPet()
        {
            if (threatInput == null)
            {
                return null;
            }

            var active = threatInput.activePet != null ? threatInput.activePet.GetComponent<CreatureCombatant>() : null;
            if (CanTarget(active))
            {
                return active;
            }

            foreach (var pet in threatInput.petSlots)
            {
                var candidate = pet != null ? pet.GetComponent<CreatureCombatant>() : null;
                if (CanTarget(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        bool WaitingForReplacement()
        {
            var pending = threatSummon != null ? threatSummon.PendingSummonPet : null;
            if (pending == null && threatInput != null && threatInput.activePet != null && threatInput.activePet.IsSummonPresentationPending)
            {
                pending = threatInput.activePet;
            }

            if (pending == null)
            {
                replacementUntil = 0;
                return false;
            }

            if (replacementUntil <= 0)
            {
                replacementUntil = Time.time + 12;
            }

            return Time.time < replacementUntil;
        }

        void Update()
        {
            using (TickMarker.Auto())
                Tick();
        }

        void Tick()
        {
            if (!initialized)
            {
                return;
            }

            if (GameplayPause.IsPaused)
            {
                if (CurrentState != EnemyAiState.Paused)
                {
                    beforePause = CurrentState;
                    CurrentState = EnemyAiState.Paused;
                }

                return;
            }

            if (CurrentState == EnemyAiState.Paused)
            {
                CurrentState = beforePause;
                decisionAt = 0;
                navigation.Resume();
            }

            if (!combat.IsEnemy)
            {
                animationDriver.Cry?.CancelAmbient();
                CancelActions();
                target = null;
                Stop();
                SetState(EnemyAiState.Captured);
                return;
            }

            if (!health.IsAlive)
            {
                var terminal=health.IsDowned?EnemyAiState.Downed:EnemyAiState.Dead;
                // CancelBasic resets lastAction; repeating it restarted Dead every frame.
                if(CurrentState!=terminal)CancelActions();
                target = null;
                Stop();
                SetState(terminal, health.IsDowned ? CreatureAction.Stun : CreatureAction.Death);
                return;
            }

            if (health.CaptureLocked)
            {
                animationDriver.Cry?.CancelAmbient();
                CancelActions();
                Stop();
                return;
            }

            if (combat.Stunned) {
                if (basicActive || actions.IsSkillBusy) CancelActions();
                Stop();
                return;
            }
            if (basicActive && (!CanTarget(basicTarget) || combat.AttackLocked))
            {
                CancelBasic();
            }

            if (actions.IsSkillBusy && !CanTarget(target))
            {
                actions.Cancel();
                lastAction = CreatureAction.None;
            }

            if (reaction != null && reaction.IsHitPlaying && !HasCommittedAttack)
            {
                animationDriver.Cry?.CancelAmbient();
                Stop();
                return;
            }

            if (!InsideTerritory(transform.position) && CurrentState != EnemyAiState.Return)
            {
                BeginReturn("Actor left territory");
            }

            bool decide = Time.time >= decisionAt;
            if (decide)
            {
                decisionAt = Time.time + Mathf.Max(.1f, decisionInterval);
                if (CurrentState != EnemyAiState.Return && threatOwner != null)
                {
                    if (!CanTarget(target))
                    {
                        target = FindThreatPet();
                    }

                    if (CanTarget(target))
                    {
                        waitingForReplacement = false;
                        replacementUntil = 0;
                    }

                    if (!CanTarget(target))
                    {
                        if (WaitingForReplacement())
                        {
                            waitingForReplacement = true;
                            target = null;
                        }
                        else
                        {
                            BeginReturn("No summoned living pet in territory");
                        }
                    }
                    else if (ClearSight(target))
                    {
                        waitingForReplacement = false;
                        replacementUntil = 0;
                        lastSeen = target.transform.position;
                        lostAt = -1;
                    }
                    else
                    {
                        if (lostAt < 0)
                        {
                            lostAt = Time.time;
                        }

                        if (Time.time - lostAt >= searchSeconds && !health.IsBoss)
                        {
                            BeginReturn("Lost sight");
                        }
                    }
                }
            }

            if (waitingForReplacement)
            {
                animationDriver.Cry?.CancelAmbient();
                Stop();
                SetState(EnemyAiState.Position, CreatureAction.Idle);
                return;
            }

            if (target == null || CurrentState == EnemyAiState.Return)
            {
                var cry = animationDriver.Cry;
                if (threatOwner != null || HasCommittedAttack || !InsideTerritory(transform.position)
                    || (CurrentState != EnemyAiState.Idle && CurrentState != EnemyAiState.Patrol))
                    cry?.CancelAmbient();
                else if (cry != null && cry.TickAmbient(animationDriver))
                {
                    Stop();
                    return;
                }
                TickUnengaged();
                return;
            }

            animationDriver.Cry?.CancelAmbient();
            Face(target.Center - transform.position);
            if (HasCommittedAttack)
            {
                Stop();
                return;
            }

            if (lostAt >= 0)
            {
                Move(lastSeen, EnemyAiState.Search);
                return;
            }

            if (decide && Time.time >= nextAttack && useSkills && (DecisionRandomSource?.Invoke() ?? (float)random.NextDouble()) < skillChance && TrySkill())
            {
                Stop();
                SetState(EnemyAiState.Attack);
                return;
            }

            if (!InBasicRange(target))
            {
                Move(target.transform.position, EnemyAiState.Chase);
                return;
            }

            Stop();
            SetState(EnemyAiState.Position, CreatureAction.Idle);
            if (decide && Time.time >= nextAttack && ClearSight(target))
            {
                StartBasic();
            }
        }

        void TickUnengaged()
        {
            if (!AtHome && CurrentState != EnemyAiState.Patrol)
            {
                Move(home, EnemyAiState.Return);
                return;
            }

            if (CurrentState == EnemyAiState.Return)
            {
                threatOwner = null;
                threatInput = null;
                navigation.ClearFailures();
            }

            if (health.CurrentHealth < health.maxHealth)
            {
                Stop();
                SetState(EnemyAiState.Recover, CreatureAction.Idle);
                return;
            }

            if (CurrentState == EnemyAiState.Recover)
            {
                GetComponent<PveCombatState>()?.ResetEncounter();
                threatOwner = null;
                threatInput = null;
            }

            if (CurrentState == EnemyAiState.Patrol && Time.time < patrolAt)
            {
                if ((transform.position - patrolPoint).sqrMagnitude > .16f)
                {
                    Move(patrolPoint, EnemyAiState.Patrol);
                }
                else
                {
                    Stop();
                    SetState(EnemyAiState.Patrol, CreatureAction.Idle);
                }

                return;
            }

            Stop();
            SetState(EnemyAiState.Idle, CreatureAction.Idle);
            if (patrolRadius <= 0 || Time.time < patrolAt)
            {
                return;
            }

            patrolAt = Time.time + 2 + (float)random.NextDouble() * 2;
            float angle = (float)random.NextDouble() * Mathf.PI * 2, radius = Mathf.Sqrt((float)random.NextDouble()) * patrolRadius;
            Vector3 point = home + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            if (InsideTerritory(point) && NavMesh.SamplePosition(point, out var sample, 1, NavMesh.AllAreas) && InsideTerritory(sample.position))
            {
                patrolPoint = sample.position;
                Move(patrolPoint, EnemyAiState.Patrol);
            }
        }

        bool TrySkill()
        {
            int count = 0;
            var skills = stats.GetSkills();
            for (int i = 0; i < skills.Count && i < 4; i++)
            {
                if (!stats.PrepareCoordinatedSkill(i, out var candidate) || stats.SpeciesSkillSet == null || !stats.SpeciesSkillSet.TryGet(candidate.skillId, out var entry))
                {
                    continue;
                }

                var profile = entry.skill.combat;
                if (profile == null || entry.Presentation == null)
                {
                    continue;
                }

                if (profile.delivery == PetSkillDelivery.SelfBuff)
                {
                    if (combat.OffenseBuffRemaining > .05f)
                    {
                        continue;
                    }
                }
                else if (!CanTarget(target))
                {
                    continue;
                }

                eligible[count++] = i;
            }

            int start = 0;
            for (int i = 0; i < count; i++)
            {
                if (eligible[i] > lastSkill)
                {
                    start = i;
                    break;
                }
            }

            for (int n = 0; n < count; n++)
            {
                int slot = eligible[(start + n) % count];
                if (actions.Submit(slot))
                {
                    lastSkill = slot;
                    nextAttack = Time.time + attackInterval * combat.AttackIntervalScale;
                    return true;
                }
            }

            return false;
        }

        bool InBasicRange(CreatureCombatant victim) => CanTarget(victim) && Vector3.Distance(combat.ClosestPoint(victim.Center), victim.ClosestPoint(combat.Center)) <= attackRange;
        void StartBasic()
        {
            if (combat.AttackLocked) return;
            var animator = animationDriver.BoundAnimator;
            if (animator == null || !animationDriver.TryGetStates(CreatureAction.Attack, out var states))
            {
                LastReason = "Missing Attack animation mapping";
                nextAttack = Time.time + 1;
                return;
            }

            basicHash = Animator.StringToHash(states[0]);
            basicRevision = animationDriver.BindingRevision;
            if (!animationDriver.TryPlay(CreatureAction.Attack))
            {
                nextAttack = Time.time + 1;
                return;
            }

            basicTarget = target;
            basicReleased = false;
            basicActive = true;
            basicAt = Time.time;
            basicToken++;
            basicElements = combat.SnapshotElements(PetElement.Normal);
            nextAttack = Time.time + Mathf.Max(.2f, attackInterval * combat.AttackIntervalScale);
            SetState(EnemyAiState.Attack);
        }

        void LateUpdate()
        {
            if (GameplayPause.IsPaused || !basicActive)
            {
                return;
            }

            var animator = animationDriver.BoundAnimator;
            if (!health.IsAlive || health.CaptureLocked || !CanTarget(basicTarget) || animator == null || basicRevision != animationDriver.BindingRevision)
            {
                CancelBasic();
                return;
            }

            if (Time.time - basicAt > 5)
            {
                LastReason = "Attack animation watchdog";
                CancelBasic();
                return;
            }

            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).fullPathHash == basicHash)
            {
                state = animator.GetNextAnimatorStateInfo(0);
            }

            if (state.fullPathHash != basicHash)
            {
                if (Time.time - basicAt > .4f)
                {
                    LastReason = "Attack animation did not enter";
                    CancelBasic();
                }

                return;
            }

            if (!basicReleased && state.normalizedTime >= .36f)
            {
                basicReleased = true;
                if (InBasicRange(basicTarget) && ClearSight(basicTarget))
                {
                    var roll = combat.RollOffense(CreatureDamageKind.Physical);
                    if (basicTarget.ReceiveHit(new CombatHit(gameObject, "basic", basicToken, roll.RawDamage, CreatureDamageKind.Physical, roll.Critical, false, basicTarget.Center, 0, basicElements)))
                    {
                        LandedHits++;
                    }
                }
            }

            if (state.normalizedTime >= .85f)
            {
                CancelBasic();
            }
        }

        void CancelBasic()
        {
            basicActive = false;
            basicReleased = false;
            basicTarget = null;
            lastAction = CreatureAction.None;
        }

        void CancelActions()
        {
            CancelBasic();
            actions?.Cancel();
        }

        void BeginReturn(string reason)
        {
            LastReason = reason;
            CancelActions();
            target = null;
            threatOwner = null;
            threatInput = null;
            threatSummon = null;
            waitingForReplacement = false;
            replacementUntil = 0;
            lostAt = -1;
            navigation.RequestRepath();
            SetState(EnemyAiState.Return);
        }

        public bool InsideTerritory(Vector3 point) => territory != null && territory.enabled && territory.gameObject.activeInHierarchy ? (territory.ClosestPoint(point) - point).sqrMagnitude < .0025f : (point - home).sqrMagnitude <= leashRadius * leashRadius;
        bool ClearSight(CreatureCombatant victim)
        {
            if (victim == null)
            {
                return false;
            }

            Vector3 delta = victim.Center - combat.Center;
            if (delta.sqrMagnitude < .0001f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(combat.Center, delta.normalized, hits, delta.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!hits[i].transform.IsChildOf(transform) && !hits[i].transform.IsChildOf(victim.transform))
                {
                    return false;
                }
            }

            return true;
        }

        void Move(Vector3 destination, EnemyAiState state)
        {
            if (combat.MovementLocked) { Stop(); return; }
            var result = navigation.Move(destination, state == EnemyAiState.Return, state == EnemyAiState.Chase, moveSpeed * combat.MovementScale, attackRange, pathInterval);
            if (!string.IsNullOrEmpty(navigation.LastReason))
            {
                LastReason = navigation.LastReason;
            }

            if (result == EnemyNavigationResult.HoldState)
            {
                SetState(state, animationDriver.ResolveLocomotion(ActualSpeed()));
                return;
            }

            if (result == EnemyNavigationResult.ReturnHome)
            {
                BeginReturn(LastReason);
                return;
            }

            if (result == EnemyNavigationResult.Moving
                && (state == EnemyAiState.Patrol || state == EnemyAiState.Return)
                && !HasCommittedAttack && agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
            {
                // Follow the actual path velocity, including turns around obstacles.
                Vector3 direction = agent.velocity;
                direction.y = 0;
                if (direction.sqrMagnitude > .01f)
                {
                    Face(direction);
                }
            }

            SetState(state, animationDriver.ResolveLocomotion(ActualSpeed()));
        }

        public bool TryRetreatStep(Vector3 direction, float distance)
        {
            if (GameplayPause.IsPaused || distance <= 0)
            {
                return false;
            }

            bool moved = navigation.TryRetreatStep(direction, distance);
            if (!string.IsNullOrEmpty(navigation.LastReason))
            {
                LastReason = navigation.LastReason;
            }

            return moved;
        }

        void Stop() => navigation?.Stop();
        float ActualSpeed() => agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped
            ? Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude : 0f;
        void Face(Vector3 direction)
        {
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 540 * Time.deltaTime);
            }
        }

        void SetState(EnemyAiState state, CreatureAction action = CreatureAction.None)
        {
            bool locomotion = action == CreatureAction.Idle || action == CreatureAction.Walk || action == CreatureAction.Run
                || action == CreatureAction.Move || action == CreatureAction.Fly;
            if (CurrentState == state && (action == CreatureAction.None || lastAction == action
                && (!locomotion || animationDriver.IsPlaying(action))))
            {
                return;
            }

            CurrentState = state;
            if (action == CreatureAction.None || HasCommittedAttack)
            {
                return;
            }

            lastAction = action;
            if (!animationDriver.TryPlay(action) && action == CreatureAction.Stun)
            {
                animationDriver.TryPlay(CreatureAction.Idle);
            }
        }
    }
}
