using System.Collections.Generic;
using Capstone.Game.MapSystem;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class PetController : MonoBehaviour
{
    private enum PetState
    {
        Underground,
        Summoning,
        FollowOwner,
        RoamAroundOwner,
        MoveToPoint,
        ChaseTarget,
        AttackTarget
    }

    [Header("References")]
    public Transform owner;
    public Animator animator;
    public NavMeshAgent agent;

    [Header("Follow")]
    public Vector3 followOffset = new Vector3(-1.4f, 0f, -1.6f);
    public float followDistance = 2.2f;
    public float followResumeDistance = 3.1f;
    public float leashDistance = 12f;
    public float teleportDistance = 35f;

    [Header("Movement")]
    public float followSpeed = 4.2f;
    public float commandSpeed = 5.3f;
    public float acceleration = 18f;
    public float angularSpeed = 720f;
    public float turnSharpness = 14f;
    public float pointStoppingDistance = 0.35f;
    public bool useNavMeshWhenAvailable = true;
    public bool useDirectMoveFallback = true;
    public bool snapFallbackToGround = true;
    public LayerMask groundMask = ~0;
    [Min(0.02f)] public float navPathRefreshInterval = 0.12f;
    [Min(0.01f)] public float navDestinationThreshold = 0.15f;

    [Header("Combat")]
    public float attackRange = 1.45f;
    public float attackApproachDistance = 0.65f;
    public float attackReachPadding = 0.18f;
    public float attackRepathInterval = 0.12f;
    public bool snapFaceTargetOnCommand = true;
    public float attackDamage = 20f;
    public float attackCooldown = 0.95f;
    public float attackHitDelay = 0.28f;
    public float attackStateTime = 0.75f;

    [Header("Summon")]
    public bool startUnderground = true;
    public bool useSelfInput = false;
    public KeyCode summonKey = KeyCode.Alpha1;
    public Vector3 summonOffset = new Vector3(0f, 0f, 2.4f);
    public float spawnDuration = 1.1f;
    public bool keepUndergroundNearOwner = true;
    public bool summonOnCommand = false;
    [Min(0.05f)] public float undergroundFollowThreshold = 0.25f;

    [Header("Roam")]
    public bool roamAroundOwner = true;
    public float roamMinRadius = 1.8f;
    public float roamMaxRadius = 4.2f;
    public float roamIdleTimeMin = 0.75f;
    public float roamIdleTimeMax = 2.2f;
    public float roamWalkSpeed = 2.1f;
    public float roamRunSpeed = 4.6f;
    public float roamRunChance = 0.3f;
    public float roamPointTolerance = 0.45f;

    [Header("Animator States")]
    public string[] undergroundStates = { "Underground", "Idle" };
    public string[] spawnStates = { "Spawn", "Idle" };
    public string[] idleStates = { "Idle", "Idle 1", "Idle Happy" };
    public string[] moveStates = { "Move", "Fly Forward In Place", "Run Forward In Place", "Walk Forward In Place", "Run Forward", "Walk Forward" };
    public string[] attackStates = { "Attack", "Bite Attack", "Bite Attack Low", "Projectile Attack", "Projectile Attack Low", "Cast Spell", "Blast Attack", "Wing Attack" };
    public float locomotionFade = 0.14f;
    public float attackFade = 0.08f;

    [Header("Animator Parameters")]
    public string speedParameter = "Speed";
    public string movingParameter = "IsMoving";
    public string attackTriggerParameter = "Attack";
    public string attackingParameter = "IsAttacking";

    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();
    private readonly HashSet<int> triggerParameters = new HashSet<int>();
    private readonly RaycastHit[] groundHits = new RaycastHit[4];

    private PetState state = PetState.FollowOwner;
    private DummyEnemy target;
    private MapMarker mapMarker;
    private Vector3 commandedPoint;
    private Vector3 attackDestination;
    private Vector3 roamDestination;
    private string currentAnimatorState;
    private float attackStartedAt;
    private float summonStartedAt;
    private float nextRoamAt;
    private float activeRoamSpeed;
    private float nextAttackAt;
    private float nextAttackRepathAt;
    private float pendingHitAt;
    private float directMoveSpeed;
    private float skillAnimationUntil;
    private float pendingSkillStartAt;
    private float pendingSkillFade;
    private string[] pendingSkillCandidates;
    private bool skillStopsMovement;
    private bool skillFacesTarget;
    private bool hasRoamDestination;
    private bool hasPendingHit;
    private bool hasPendingSkillAnimation;
    private bool navDestinationInitialized;
    private bool undergroundSpotInitialized;
    private Vector3 lastNavDestination;
    private Vector3 lastUndergroundSpot;
    private float nextNavPathRefreshAt;

    public bool IsSummoned { get; private set; }

    public bool CanReceiveCommands
    {
        get { return IsSummoned && state != PetState.Summoning; }
    }

    public bool HasCombatTarget
    {
        get { return target != null && target.IsAlive; }
    }

    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        ConfigureAnimator();
        ConfigureAgent();
    }

    private void OnEnable()
    {
        EnsureMapMarker();
    }

    private void Start()
    {
        if (owner == null)
        {
            BasicPlayerMovement player = FindFirstObjectByType<BasicPlayerMovement>();
            if (player != null)
            {
                owner = player.transform;
            }
        }

        if (startUnderground)
        {
            EnterUnderground();
        }
        else
        {
            IsSummoned = true;
            state = PetState.FollowOwner;
            PlayAnimatorState(idleStates, locomotionFade, true);
        }
    }

    private void Update()
    {
        if (!Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked
            && useSelfInput
            && summonKey != KeyCode.None
            && Input.GetKeyDown(summonKey))
        {
            ToggleSummonOrRecall();
            return;
        }

        if (!IsSummoned)
        {
            UpdateUnderground();
            return;
        }

        if (state == PetState.Summoning)
        {
            UpdateSummoning();
            return;
        }

        if (owner == null && state == PetState.FollowOwner)
        {
            StopMovement();
            UpdateAnimator(0f);
            return;
        }

        if (IsSkillAnimationActive())
        {
            TryStartPendingSkillAnimation();

            if (skillStopsMovement)
            {
                StopMovement(false);
            }

            if (skillFacesTarget && target != null && target.IsAlive)
            {
                FacePoint(GetTargetPosition());
            }

            return;
        }

        switch (state)
        {
            case PetState.FollowOwner:
                UpdateFollow();
                break;
            case PetState.RoamAroundOwner:
                UpdateRoamAroundOwner();
                break;
            case PetState.MoveToPoint:
                UpdateMoveToPoint();
                break;
            case PetState.ChaseTarget:
                UpdateChaseTarget();
                break;
            case PetState.AttackTarget:
                UpdateAttackTarget();
                break;
        }
    }

    public void AssignOwner(Transform newOwner)
    {
        owner = newOwner;
        undergroundSpotInitialized = false;
        if (state == PetState.FollowOwner)
        {
            MoveToFollowSpot();
        }
    }

    public void CommandMove(Vector3 worldPoint)
    {
        if (!CanReceiveCommands)
        {
            if (summonOnCommand)
            {
                BeginSummon();
            }

            return;
        }

        target = null;
        hasRoamDestination = false;
        commandedPoint = worldPoint;
        state = PetState.MoveToPoint;
        MoveTo(commandedPoint, commandSpeed, pointStoppingDistance);
    }

    public void CommandAttack(DummyEnemy enemy)
    {
        if (!CanReceiveCommands)
        {
            if (summonOnCommand)
            {
                BeginSummon();
            }

            return;
        }

        if (enemy == null || !enemy.IsAlive)
        {
            ReturnToOwner();
            return;
        }

        target = enemy;
        hasRoamDestination = false;
        state = PetState.ChaseTarget;
        nextAttackRepathAt = 0f;
        if (snapFaceTargetOnCommand)
        {
            SnapFacingToPoint(GetTargetPosition());
        }

        MoveToAttackRange();
    }

    public void ReturnToOwner()
    {
        if (!CanReceiveCommands)
        {
            return;
        }

        target = null;
        hasRoamDestination = false;
        state = PetState.FollowOwner;
        MoveToFollowSpot();
    }

    public void Summon()
    {
        if (IsSummoned)
        {
            target = null;
            hasRoamDestination = false;
            if (state != PetState.Summoning)
            {
                state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
                ScheduleNextRoam(0.1f);
            }

            return;
        }

        BeginSummon();
    }

    public void HideUnderground()
    {
        EnterUnderground();
    }

    public void Withdraw()
    {
        if (!IsSummoned && state == PetState.Underground)
        {
            return;
        }

        EnterUnderground();
    }

    public bool PlaySkillAnimation(string[] candidates, float duration, float fade, bool stopMovement, bool faceTarget)
    {
        return PlaySkillAnimation(candidates, duration, fade, stopMovement, faceTarget, 0f, 0f);
    }

    public bool PlaySkillAnimation(string[] candidates, float duration, float fade, bool stopMovement, bool faceTarget, float windupSeconds, float recoverySeconds)
    {
        if (candidates == null || candidates.Length == 0)
        {
            return false;
        }

        if (!HasAnimatorState(candidates))
        {
            return false;
        }

        duration = Mathf.Max(0.05f, duration);
        fade = Mathf.Max(0f, fade);
        windupSeconds = Mathf.Max(0f, windupSeconds);
        recoverySeconds = Mathf.Max(0f, recoverySeconds);

        skillStopsMovement = stopMovement;
        skillFacesTarget = faceTarget;
        if (skillStopsMovement)
        {
            StopMovement(false);
        }

        if (skillFacesTarget && target != null && target.IsAlive)
        {
            FacePoint(GetTargetPosition());
        }

        if (windupSeconds > 0f)
        {
            pendingSkillCandidates = candidates;
            pendingSkillFade = fade;
            pendingSkillStartAt = Time.time + windupSeconds;
            hasPendingSkillAnimation = true;
            skillAnimationUntil = pendingSkillStartAt + duration + recoverySeconds;
            return true;
        }

        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
        if (!PlayAnimatorState(candidates, fade, true))
        {
            return false;
        }

        skillAnimationUntil = Time.time + duration + recoverySeconds;
        return true;
    }

    private void ToggleSummonOrRecall()
    {
        if (!IsSummoned)
        {
            BeginSummon();
            return;
        }

        if (state == PetState.Summoning)
        {
            return;
        }

        ReturnToOwner();
    }

    private void EnterUnderground()
    {
        IsSummoned = false;
        target = null;
        state = PetState.Underground;
        undergroundSpotInitialized = false;
        StopMovement(false);
        MoveUndergroundNearOwner();
        PlayAnimatorState(undergroundStates, locomotionFade, true);
    }

    private void UpdateUnderground()
    {
        StopMovement(false);
        MoveUndergroundNearOwner();
        PlayAnimatorState(undergroundStates, locomotionFade, false);
    }

    private void MoveUndergroundNearOwner()
    {
        if (!keepUndergroundNearOwner || owner == null)
        {
            return;
        }

        Vector3 desiredSpot = owner.TransformPoint(summonOffset);
        float threshold = Mathf.Max(0.05f, undergroundFollowThreshold);
        if (undergroundSpotInitialized
            && (desiredSpot - lastUndergroundSpot).sqrMagnitude <= threshold * threshold)
        {
            return;
        }

        lastUndergroundSpot = desiredSpot;
        undergroundSpotInitialized = true;

        MoveToSummonSpot();
    }

    private void MoveToSummonSpot()
    {
        if (owner == null)
        {
            return;
        }

        Vector3 spot = owner.TransformPoint(summonOffset);
        spot = SnapToGround(spot);

        if (agent != null && agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spot, out hit, 3f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = spot;
            }
        }
        else
        {
            transform.position = spot;
        }

        FaceDirection(owner.forward);
    }

    private void BeginSummon()
    {
        if (state == PetState.Summoning)
        {
            return;
        }

        IsSummoned = true;
        target = null;
        hasRoamDestination = false;
        state = PetState.Summoning;
        summonStartedAt = Time.time;
        StopMovement(false);
        MoveToSummonSpot();
        PlayAnimatorState(spawnStates, attackFade, true);
    }

    private void UpdateSummoning()
    {
        StopMovement(false);
        if (Time.time - summonStartedAt < spawnDuration)
        {
            return;
        }

        state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
        ScheduleNextRoam(0.25f);
        PlayAnimatorState(idleStates, locomotionFade, true);
        if (!roamAroundOwner)
        {
            MoveToFollowSpot();
        }
    }

    private void UpdateFollow()
    {
        float ownerDistance = FlatDistance(transform.position, owner.position);
        if (ownerDistance > teleportDistance)
        {
            TeleportNearOwner();
            return;
        }

        if (ownerDistance > leashDistance || ownerDistance > followResumeDistance)
        {
            MoveToFollowSpot();
        }
        else if (ownerDistance <= followDistance)
        {
            StopMovement(false);
            if (roamAroundOwner)
            {
                state = PetState.RoamAroundOwner;
                ScheduleNextRoam(0.2f);
            }
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    private void UpdateRoamAroundOwner()
    {
        if (owner == null)
        {
            StopMovement();
            return;
        }

        float ownerDistance = FlatDistance(transform.position, owner.position);
        if (ownerDistance > teleportDistance)
        {
            TeleportNearOwner();
            return;
        }

        if (ownerDistance > leashDistance || ownerDistance > followResumeDistance)
        {
            hasRoamDestination = false;
            state = PetState.FollowOwner;
            MoveToFollowSpot();
            UpdateAnimator(GetCurrentSpeed());
            return;
        }

        if (hasRoamDestination)
        {
            MoveTo(roamDestination, activeRoamSpeed, roamPointTolerance);
            if (ReachedPoint(roamDestination, roamPointTolerance + 0.1f))
            {
                hasRoamDestination = false;
                StopMovement(false);
                ScheduleNextRoam();
            }

            UpdateAnimator(GetCurrentSpeed());
            return;
        }

        StopMovement(false);
        if (Time.time >= nextRoamAt)
        {
            PickNextRoamPoint();
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    private void PickNextRoamPoint()
    {
        if (owner == null)
        {
            return;
        }

        Vector2 random = Random.insideUnitCircle;
        if (random.sqrMagnitude < 0.001f)
        {
            random = Vector2.up;
        }

        float radius = Random.Range(roamMinRadius, Mathf.Max(roamMinRadius, roamMaxRadius));
        Vector3 desired = owner.position + new Vector3(random.normalized.x, 0f, random.normalized.y) * radius;
        desired = SnapToGround(desired);

        if (agent != null && agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(desired, out hit, 2.5f, NavMesh.AllAreas))
            {
                desired = hit.position;
            }
        }

        roamDestination = desired;
        activeRoamSpeed = Random.value < roamRunChance ? roamRunSpeed : roamWalkSpeed;
        hasRoamDestination = FlatDistance(transform.position, roamDestination) > roamPointTolerance;
        if (!hasRoamDestination)
        {
            ScheduleNextRoam();
        }
    }

    private void ScheduleNextRoam(float initialDelay = -1f)
    {
        float delay = initialDelay >= 0f ? initialDelay : Random.Range(roamIdleTimeMin, Mathf.Max(roamIdleTimeMin, roamIdleTimeMax));
        nextRoamAt = Time.time + delay;
    }

    private void UpdateMoveToPoint()
    {
        MoveTo(commandedPoint, commandSpeed, pointStoppingDistance);
        if (ReachedDestination(pointStoppingDistance + 0.1f))
        {
            ReturnToOwner();
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    private void UpdateChaseTarget()
    {
        if (target == null || !target.IsAlive)
        {
            ReturnToOwner();
            return;
        }

        if (IsTargetInAttackRange())
        {
            BeginAttack();
            return;
        }

        if (Time.time >= nextAttackRepathAt || !HasMovePath())
        {
            MoveToAttackRange();
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    private void UpdateAttackTarget()
    {
        StopMovement();

        if (target == null || !target.IsAlive)
        {
            SetAnimatorBool(attackingParameter, false);
            ReturnToOwner();
            return;
        }

        FacePoint(GetTargetPosition());

        if (hasPendingHit && Time.time >= pendingHitAt)
        {
            hasPendingHit = false;
            if (IsTargetInAttackRange())
            {
                target.TakeDamage(attackDamage, gameObject);
            }
        }

        if (Time.time - attackStartedAt >= attackStateTime)
        {
            SetAnimatorBool(attackingParameter, false);

            if (!target.IsAlive)
            {
                ReturnToOwner();
            }
            else if (!IsTargetInAttackRange())
            {
                state = PetState.ChaseTarget;
                nextAttackRepathAt = 0f;
            }
            else if (Time.time >= nextAttackAt)
            {
                BeginAttack();
            }
            else
            {
                PlayAnimatorState(idleStates, locomotionFade, false);
            }
        }
    }

    private void BeginAttack()
    {
        if (target == null || !target.IsAlive)
        {
            ReturnToOwner();
            return;
        }

        state = PetState.AttackTarget;
        attackStartedAt = Time.time;
        nextAttackAt = Time.time + attackCooldown;
        pendingHitAt = Time.time + attackHitDelay;
        hasPendingHit = true;

        StopMovement(false);
        FacePoint(GetTargetPosition());
        SetAnimatorBool(attackingParameter, true);

        if (!SetAnimatorTrigger(attackTriggerParameter))
        {
            PlayAnimatorState(attackStates, attackFade, true);
        }
    }

    private void MoveToAttackRange()
    {
        attackDestination = GetAttackDestination();
        nextAttackRepathAt = Time.time + Mathf.Max(0.03f, attackRepathInterval);
        MoveTo(attackDestination, commandSpeed, Mathf.Min(pointStoppingDistance, attackApproachDistance));
    }

    private void MoveToFollowSpot()
    {
        if (owner == null)
        {
            return;
        }

        Vector3 desired = owner.TransformPoint(followOffset);
        MoveTo(desired, followSpeed, followDistance);
    }

    private void MoveTo(Vector3 destination, float speed, float stopDistance)
    {
        if (TryMoveWithAgent(destination, speed, stopDistance))
        {
            return;
        }

        if (useDirectMoveFallback)
        {
            MoveDirect(destination, speed, stopDistance);
        }
    }

    private bool TryMoveWithAgent(Vector3 destination, float speed, float stopDistance)
    {
        if (!useNavMeshWhenAvailable || agent == null || !agent.enabled || !agent.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            if (!TryWarpToNavMesh(transform.position, 2.5f))
            {
                return false;
            }
        }

        Vector3 navDestination = destination;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, 2.5f, NavMesh.AllAreas))
        {
            navDestination = hit.position;
        }

        agent.speed = speed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stopDistance;
        float destinationThreshold = Mathf.Max(0.01f, navDestinationThreshold);
        bool destinationChanged = !navDestinationInitialized
            || (navDestination - lastNavDestination).sqrMagnitude
                > destinationThreshold * destinationThreshold;
        bool needsPath = agent.isStopped || (!agent.hasPath && !agent.pathPending);
        bool canRefreshDestination = Time.time >= nextNavPathRefreshAt;

        agent.isStopped = false;
        if (needsPath || (destinationChanged && canRefreshDestination))
        {
            agent.SetDestination(navDestination);
            lastNavDestination = navDestination;
            navDestinationInitialized = true;
            nextNavPathRefreshAt = Time.time + Mathf.Max(0.02f, navPathRefreshInterval);
        }

        FaceVelocity(agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity);
        return true;
    }

    private bool TryWarpToNavMesh(Vector3 position, float searchRadius)
    {
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(position, out hit, searchRadius, NavMesh.AllAreas))
        {
            return false;
        }

        agent.Warp(hit.position);
        navDestinationInitialized = false;
        return agent.isOnNavMesh;
    }

    private void MoveDirect(Vector3 destination, float speed, float stopDistance)
    {
        Vector3 current = transform.position;
        Vector3 flatDestination = new Vector3(destination.x, current.y, destination.z);
        Vector3 toDestination = flatDestination - current;

        if (toDestination.sqrMagnitude <= stopDistance * stopDistance)
        {
            StopMovement();
            return;
        }

        Vector3 direction = toDestination.normalized;
        Vector3 next = current + direction * speed * Time.deltaTime;
        if (snapFallbackToGround)
        {
            next = SnapToGround(next);
        }

        transform.position = next;
        FaceDirection(direction);
        directMoveSpeed = speed;
    }

    private void StopMovement(bool updateAnimation = true)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
            }

            if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        navDestinationInitialized = false;
        directMoveSpeed = 0f;
        if (state != PetState.AttackTarget)
        {
            if (updateAnimation && state != PetState.Underground && state != PetState.Summoning)
            {
                UpdateAnimator(0f);
            }
        }
    }

    private bool ReachedDestination(float tolerance)
    {
        return ReachedPoint(commandedPoint, tolerance);
    }

    private bool ReachedPoint(Vector3 point, float tolerance)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            return !agent.pathPending && agent.remainingDistance <= tolerance;
        }

        return FlatDistance(transform.position, point) <= tolerance;
    }

    private void TeleportNearOwner()
    {
        Vector3 spot = owner.TransformPoint(followOffset);
        if (agent != null && agent.enabled)
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(spot, out hit, 4f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
            else
            {
                transform.position = SnapToGround(spot);
            }
        }
        else
        {
            transform.position = SnapToGround(spot);
        }

        StopMovement();
    }

    private Vector3 GetTargetPosition()
    {
        return target != null ? target.TargetPosition : transform.position;
    }

    private Vector3 GetTargetClosestPoint()
    {
        return target != null ? target.ClosestPoint(transform.position) : transform.position;
    }

    private Vector3 GetAttackDestination()
    {
        if (target == null)
        {
            return transform.position;
        }

        Vector3 targetPosition = GetTargetPosition();
        Vector3 closestPoint = GetTargetClosestPoint();
        Vector3 awayFromTarget = transform.position - targetPosition;
        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.001f)
        {
            awayFromTarget = -transform.forward;
            awayFromTarget.y = 0f;
        }

        return closestPoint + awayFromTarget.normalized * Mathf.Max(0.05f, attackApproachDistance);
    }

    private bool IsTargetInAttackRange()
    {
        if (target == null || !target.IsAlive)
        {
            return false;
        }

        return FlatDistance(transform.position, GetTargetClosestPoint()) <= attackRange + attackReachPadding;
    }

    private bool HasMovePath()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return false;
        }

        return agent.hasPath && !agent.isStopped;
    }

    private float GetCurrentSpeed()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            return agent.velocity.magnitude;
        }

        return directMoveSpeed;
    }

    private Vector3 SnapToGround(Vector3 position)
    {
        Vector3 origin = position + Vector3.up * 1.5f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 4f, groundMask, QueryTriggerInteraction.Ignore);
        if (count <= 0)
        {
            return position;
        }

        int best = 0;
        float bestDistance = groundHits[0].distance;
        for (int i = 1; i < count; i++)
        {
            if (groundHits[i].distance < bestDistance)
            {
                best = i;
                bestDistance = groundHits[i].distance;
            }
        }

        position.y = groundHits[best].point.y;
        return position;
    }

    private void FaceVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.001f)
        {
            FaceDirection(velocity.normalized);
        }
    }

    private void FacePoint(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            FaceDirection(direction.normalized);
        }
    }

    private void SnapFacingToPoint(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
    }

    private void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        agent.updateRotation = false;
        agent.speed = followSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = followDistance;
        agent.autoBraking = true;
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        floatParameters.Clear();
        boolParameters.Clear();
        triggerParameters.Clear();

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            int hash = Animator.StringToHash(parameter.name);
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                floatParameters.Add(hash);
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                boolParameters.Add(hash);
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                triggerParameters.Add(hash);
            }
        }
    }

    private void UpdateAnimator(float speed)
    {
        if (animator == null || IsSkillAnimationActive() || state == PetState.AttackTarget || state == PetState.Underground || state == PetState.Summoning)
        {
            return;
        }

        bool moving = speed > 0.08f || (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.isStopped);
        SetAnimatorFloat(speedParameter, speed);
        SetAnimatorBool(movingParameter, moving);
        PlayAnimatorState(moving ? moveStates : idleStates, locomotionFade, false);
    }

    private bool PlayAnimatorState(string[] candidates, float fade, bool restart)
    {
        if (animator == null || candidates == null)
        {
            return false;
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            if (!restart && currentAnimatorState == candidate)
            {
                return true;
            }

            int hash;
            if (!TryGetStateHash(candidate, out hash))
            {
                continue;
            }

            currentAnimatorState = candidate;
            animator.CrossFadeInFixedTime(hash, fade, 0, restart ? 0f : float.NegativeInfinity);
            return true;
        }

        return false;
    }

    private bool HasAnimatorState(string[] candidates)
    {
        if (animator == null || candidates == null)
        {
            return false;
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            int hash;
            if (TryGetStateHash(candidate, out hash))
            {
                return true;
            }
        }

        return false;
    }

    private void TryStartPendingSkillAnimation()
    {
        if (!hasPendingSkillAnimation || Time.time < pendingSkillStartAt)
        {
            return;
        }

        PlayAnimatorState(pendingSkillCandidates, pendingSkillFade, true);
        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
    }

    private bool TryGetStateHash(string stateName, out int hash)
    {
        string fullName = "Base Layer." + stateName;
        hash = Animator.StringToHash(fullName);
        if (animator.HasState(0, hash))
        {
            return true;
        }

        hash = Animator.StringToHash(stateName);
        return animator.HasState(0, hash);
    }

    private bool IsSkillAnimationActive()
    {
        return Time.time < skillAnimationUntil;
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        int hash = Animator.StringToHash(parameterName);
        if (floatParameters.Contains(hash))
        {
            animator.SetFloat(hash, value, 0.08f, Time.deltaTime);
        }
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        int hash = Animator.StringToHash(parameterName);
        if (boolParameters.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private bool SetAnimatorTrigger(string parameterName)
    {
        int hash = Animator.StringToHash(parameterName);
        if (!triggerParameters.Contains(hash))
        {
            return false;
        }

        animator.ResetTrigger(hash);
        animator.SetTrigger(hash);
        return true;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void EnsureMapMarker()
    {
        if (mapMarker == null)
        {
            mapMarker = GetComponent<MapMarker>();
        }

        if (mapMarker == null)
        {
            mapMarker = gameObject.AddComponent<MapIcon>();
        }

        mapMarker.ConfigureRuntime(MapMarkerType.Pet, gameObject.name, gameObject.name, null, default, true, true);
    }
}

