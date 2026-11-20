using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.MapSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Skills;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public partial class PetController : MonoBehaviour
{
    private static readonly float[] DirectAvoidanceAngles = {35f, 70f, 105f, 140f};
    private static readonly Vector3[] SafeSpotFallbackOffsets = {new Vector3(1.7f, 0f, -1.7f), new Vector3(-2.2f, 0f, 0f), new Vector3(2.2f, 0f, 0f), new Vector3(0f, 0f, -2.2f)};
    private enum PetState
    {
        Dormant,
        Summoning,
        FollowOwner,
        RoamAroundOwner,
        MoveToPoint,
        ChaseTarget,
        AttackTarget
    }

    [Header("Tham chieu")]
    public Transform owner;
    [SerializeField, Tooltip("Only for explicitly authored wild capture prefabs; owned pets keep this off.")]
    bool wildEncounter;
    public bool IsWildEncounter => wildEncounter;
    public Animator animator;
    public float SummonPresentationAnimationDuration => summonAnimationDuration;
    public bool HasSummonPresentationAnimationFinished
    {
        get
        {
            if (!summonPresentationAnimationStarted)
                return false;
            if (!summonCastActive)
                return Time.time - summonStartedAt >= summonAnimationDuration;
            if (animator == null)
                return true;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            bool inCast = info.shortNameHash == Animator.StringToHash("Cast Spell");
            return !animator.IsInTransition(0) && (inCast && info.normalizedTime >= .98f || !inCast && Time.time - summonStartedAt >= summonAnimationDuration);
        }
    }

    public void BindVisualAnimator(Animator nextAnimator)
    {
        if (nextAnimator == null)
            return;
        if (animator != nextAnimator)
        {
            hasPendingSkillAnimation = false;
            pendingSkillCandidates = null;
            skillAnimationUntil = 0;
            currentAnimatorState = null;
        }

        RestoreSummonAnimatorSpeed();
        animator = nextAnimator;
        animatorInitiallyEnabled = nextAnimator.enabled;
        ConfigureAnimator();
    }

    public NavMeshAgent agent;
    [Header("Theo chu")]
    public Vector3 followOffset = new Vector3(-1.4f, 0f, -1.6f);
    public float followDistance = 4f;
    public float followResumeDistance = 7f;
    public float leashDistance = 10f;
    public float teleportDistance = 24f;
    [Header("Di chuyen")]
    public float followSpeed = 4.2f;
    public float commandSpeed = 5.3f;
    public float acceleration = 18f;
    public float angularSpeed = 720f;
    public float turnSharpness = 14f;
    public float pointStoppingDistance = 0.35f;
    public bool useNavMeshWhenAvailable = false;
    public bool useDirectMoveFallback = true;
    public bool snapFallbackToGround = true;
    public LayerMask groundMask = ~0;
    [Header("Di chuyen khong co NavMesh")]
    [Min(0.1f)]
    public float directBodyRadius = 0.32f;
    [Min(0.2f)]
    public float directProbeHeight = 0.65f;
    [Min(0.2f)]
    public float directLookAhead = 0.9f;
    [Min(0.05f)]
    public float directMaxStepHeight = 0.3f;
    [Range(0.1f, 1f)]
    public float directMinGroundNormal = 0.65f;
    public LayerMask directObstacleMask = ~0;
    [Min(0.1f)]
    public float minimumNavigationRadius = 0.65f;
    [Min(0.02f)]
    public float navPathRefreshInterval = 0.12f;
    [Min(0.01f)]
    public float navDestinationThreshold = 0.15f;
    [Min(0.1f)]
    public float navMeshRetryInterval = 1f;
    [Header("Chien dau")]
    public float attackRange = 1.45f;
    public float attackApproachDistance = 0.65f;
    public float attackReachPadding = 0.18f;
    public float attackRepathInterval = 0.12f;
    public bool snapFaceTargetOnCommand = true;
    public float attackDamage = 20f;
    public float CurrentBaseAttackDamage
    {
        get
        {
            PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
            return metadata != null ? Mathf.Max(0f, metadata.Attack) : Mathf.Max(0f, attackDamage);
        }
    }

    public float attackCooldown = 0.95f;
    public float attackHitDelay = 0.28f;
    public float attackStateTime = 0.75f;
    [Header("Trieu hoi")]
    public bool useSelfInput = false;
    public KeyCode summonKey = KeyCode.Alpha1;
    public float spawnDuration = 1.1f;
    public bool summonOnCommand = false;
    [Header("Di dao")]
    public bool roamAroundOwner = true;
    public float roamMinRadius = 2.2f;
    public float roamMaxRadius = 5f;
    public float roamIdleTimeMin = 1f;
    public float roamIdleTimeMax = 3f;
    public float roamWalkSpeed = 2f;
    public float roamRunSpeed = 4.6f;
    public float roamRunChance = 0.12f;
    public float roamPointTolerance = 0.45f;
    [Header("Trang thai Animator")]
    public string[] spawnStates = {"Spawn", "Idle"};
    public string[] idleStates = {"Idle", "Idle 1", "Idle Happy"};
    public string[] moveStates = {"Move", "Fly Forward In Place", "Run Forward In Place", "Walk Forward In Place", "Run Forward", "Walk Forward"};
    public string[] attackStates = {"Attack", "Bite Attack", "Bite Attack Low", "Projectile Attack", "Projectile Attack Low", "Cast Spell", "Blast Attack", "Wing Attack"};
    public float locomotionFade = 0.14f;
    public float attackFade = 0.08f;
    [Header("Tham so Animator")]
    public string speedParameter = "Speed";
    public string movingParameter = "IsMoving";
    public string attackTriggerParameter = "Attack";
    public string attackingParameter = "IsAttacking";
    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();
    private readonly HashSet<int> triggerParameters = new HashSet<int>();
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly RaycastHit[] directObstacleHits = new RaycastHit[24];
    private PetState state = PetState.Dormant;
    private DummyEnemy target;
    private MapMarker mapMarker;
    private PetLevelUpService levelUpService;
    private Vector3 commandedPoint;
    private Vector3 attackDestination;
    private Vector3 roamDestination;
    private Vector3 lastRoamDirection;
    private string currentAnimatorState;
    private float attackStartedAt;
    private float summonStartedAt;
    private float summonAnimationDuration;
    private float summonAnimatorSpeedBefore;
    private bool summonAnimatorSpeedOverridden;
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
    private bool recallLocked;
    private DummyEnemy recallFacingEnemy;
    private bool recallWasInCombat;
    private bool summonRevealPending;
    private bool summonCastActive;
    private bool summonPresentationAnimationStarted;
    private bool warnedMissingCastSpell;
    private bool navDestinationInitialized;
    private readonly Dictionary<Renderer, bool> rendererEnabledStates = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Collider, bool> colliderEnabledStates = new Dictionary<Collider, bool>();
    private readonly Collider[] presenceHits = new Collider[24];
    private bool animatorInitiallyEnabled;
    private bool markerInitiallyEnabled;
    private bool markerPresenceCached;
    private Vector3 lastNavDestination;
    private float directAvoidanceUntil;
    private float directAvoidanceAngle;
    private float directStuckSince;
    private float nextNavPathRefreshAt;
    private float nextNavMeshRetryAt;
    private int directAvoidanceSide = 1;
    private int roamOrbitSide = 1;
    private int roamStepsInDirection;
    public bool IsSummoned { get; private set; }

    public bool IsSummonPresentationPending => summonRevealPending;
    private Capstone.Game.CreatureSystem.CreatureCombatant combatant;
    private PetHudRuntimeStats combatStats;
    private PetCombatActionDriver combatActions;
    private CombatOffenseRoll basicRoll;
    private Capstone.Game.Elements.CombatElementSnapshot basicElements;
    private int basicToken;
    internal void BindCombatant(Capstone.Game.CreatureSystem.CreatureCombatant value)
    {
        combatant = value;
        combatStats = GetComponent<PetHudRuntimeStats>();
    }

    internal void BindCombatActions(PetCombatActionDriver value)
    {
        combatActions = value;
    }

    internal float BasicSkillDelay => state == PetState.AttackTarget && (hasPendingHit || Time.time < pendingHitAt + .08f) ? Mathf.Max(0, pendingHitAt + .08f - Time.time) : 0;
    internal void HoldCoordinatedSkill()
    {
        hasPendingHit = false;
        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
        skillStopsMovement = true;
        skillFacesTarget = false;
        StopMovement(false);
        SetAnimatorBool(attackingParameter, false);
    }

    internal void ReleaseCoordinatedSkill()
    {
        skillAnimationUntil = 0;
        currentAnimatorState = null;
    }

    internal void FaceCombatDirection(Vector3 direction)
    {
        direction.y = 0;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), 720 * Time.deltaTime);
    }

    public bool CanReceiveCommands
    {
        get
        {
            return IsSummoned && state != PetState.Summoning && !recallLocked && (combatant == null || (combatStats != null && combatStats.Health > 0));
        }
    }

    public bool HasCombatTarget
    {
        get
        {
            return target != null && target.IsAlive;
        }
    }

    public DummyEnemy CombatTarget => target != null && target.IsAlive ? target : null;
    private bool defeatPresentation;
    private float defeatDeadline;
    private float hitReactionUntil;
    internal void HoldHitReaction(float seconds)
    {
        hitReactionUntil = Time.time + seconds;
    }

    internal void FinishHitReaction()
    {
        hitReactionUntil = 0;
        currentAnimatorState = null;
    }

    internal void HandleCombatDefeat()
    {
        if (defeatPresentation)
            return;
        bool visible = IsSummoned;
        combatActions?.Cancel();
        EnterDormant();
        if (!visible || animator == null)
            return;
        CacheWorldPresence();
        foreach (KeyValuePair<Renderer, bool> entry in rendererEnabledStates)
            if (entry.Key != null)
                entry.Key.enabled = entry.Value;
        animator.enabled = animatorInitiallyEnabled;
        defeatPresentation = true;
        defeatDeadline = Time.time + .3f;
        Capstone.Game.CreatureSystem.PetDefeatRecall.Play(this);
    }

    internal void FinishDefeatRecall() { EnterDormant(); }

    internal void AwardCombatDefeat(DummyEnemy enemy)
    {
        AwardDefeatExperience(enemy);
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
        animatorInitiallyEnabled = animator != null && animator.enabled;
        CacheWorldPresence();
        if (!wildEncounter)
            SetWorldPresence(false);
    }

    private void OnEnable()
    {
        if (wildEncounter)
            return;
        EnsureMapMarker();
        // Recall da tat marker; OnEnable lan sau khong duoc coi do la cau hinh goc.
        if (!markerPresenceCached)
        {
            markerInitiallyEnabled = mapMarker != null && mapMarker.enabled;
            markerPresenceCached = true;
        }
        if (!IsSummoned && mapMarker != null)
        {
            mapMarker.enabled = false;
        }
    }

    private void Start()
    {
        if (wildEncounter)
            return;
        if (owner == null)
        {
            BasicPlayerMovement player = FindFirstObjectByType<BasicPlayerMovement>();
            if (player != null)
            {
                owner = player.transform;
            }
        }

        // Pet tao trong luc load save co the duoc khoi phuc truoc frame
        // Start dau tien; khong an lai pet sau khi khoi phuc thanh cong.
        if (!IsSummoned)
        {
            EnterDormant();
        }
    }

    private void Update()
    {
        if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            return;
        if (wildEncounter)
            return;
        if (defeatPresentation)
        {
            if (animator == null || !animator.enabled || Time.time >= defeatDeadline)
            {
                EnterDormant();
                return;
            }

            return;
        }

        if (Time.time < hitReactionUntil)
        {
            StopMovement(false);
            return;
        }

        if (!Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked && useSelfInput && summonKey != KeyCode.None && Input.GetKeyDown(summonKey))
        {
            ToggleSummonOrRecall();
            return;
        }

        if (!IsSummoned)
        {
            return;
        }

        if (recallLocked)
        {
            StopMovement(false);
            UpdateRecallFacing();
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
        if (!IsSummoned) QueueOwnedRuntimeStorage();
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
                Summon();
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
                Summon();
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
        directStuckSince = 0f;
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
        hasPendingHit = false;
        SetAnimatorBool(attackingParameter, false);
        hasRoamDestination = false;
        directStuckSince = 0f;
        state = PetState.FollowOwner;
        MoveToFollowSpot();
    }

    private float FindAnimationDuration(string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Mathf.Max(0.05f, clip.length);
            }
        }

        return fallback;
    }

    private void UpdateRecallFacing()
    {
        Vector3 origin = hasRecallVisualCenter ? recallVisualCenter : transform.position;
        if (recallWasInCombat)
        {
            if (!recallFacingTargetLost && recallFacingEnemy != null && recallFacingEnemy.IsAlive)
                FaceDirection(recallFacingEnemy.TargetPosition - origin);
            else
                recallFacingTargetLost = true;
        // Mat muc tieu combat khong duoc ep pet quay ngay ve chu.
        }
        else if (owner != null)
            FaceDirection(owner.position - origin);
    }

    private Vector3 recallVisualCenter;
    private bool hasRecallVisualCenter;
    private bool recallFacingTargetLost;
    public void SetRecallVisualCenter(Vector3 center)
    {
        if (!recallLocked)
            return;
        recallVisualCenter = center;
        hasRecallVisualCenter = true;
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
        hasPendingHit = false;
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
            Summon();
            return;
        }

        if (state == PetState.Summoning)
        {
            return;
        }

        ReturnToOwner();
    }

    private void OnDisable()
    {
        RestoreSummonAnimatorSpeed();
        if (defeatPresentation)
            EnterDormant();
    }

    private void UpdateFollow()
    {
        float ownerDistance = FlatDistance(transform.position, owner.position);
        if (ownerDistance > teleportDistance || Mathf.Abs(transform.position.y - owner.position.y) > 3f)
        {
            if (TeleportNearOwner())
            {
                return;
            }
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
        if (ownerDistance > teleportDistance || Mathf.Abs(transform.position.y - owner.position.y) > 3f)
        {
            if (TeleportNearOwner())
            {
                return;
            }
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
        if (owner == null || !TryFindWalkableGround(owner.position, 1f, out Vector3 ownerGround))
        {
            ScheduleNextRoam();
            return;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 direction;
            if (lastRoamDirection.sqrMagnitude > 0.001f)
            {
                float turn = Random.Range(35f, 85f) * roamOrbitSide;
                direction = Quaternion.Euler(0f, turn, 0f) * lastRoamDirection;
            }
            else
            {
                Vector2 random = Random.insideUnitCircle.normalized;
                direction = new Vector3(random.x, 0f, random.y);
            }

            if (direction.sqrMagnitude < 0.001f)
            {
                continue;
            }

            float radius = Random.Range(roamMinRadius, Mathf.Max(roamMinRadius, roamMaxRadius));
            Vector3 desired = owner.position + direction.normalized * radius;
            desired.y = ownerGround.y;
            if (!TryFindWalkableGround(desired, 0.65f, out desired) || !HasRoomAt(desired))
            {
                continue;
            }

            if (useNavMeshWhenAvailable && agent != null && agent.enabled && NavMesh.SamplePosition(desired, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
            {
                desired = hit.position;
            }

            roamDestination = desired;
            activeRoamSpeed = Random.value < roamRunChance ? roamRunSpeed : roamWalkSpeed;
            hasRoamDestination = FlatDistance(transform.position, roamDestination) > roamPointTolerance;
            if (hasRoamDestination)
            {
                lastRoamDirection = direction.normalized;
                if (++roamStepsInDirection >= 3 && (roamStepsInDirection >= 6 || Random.value < 0.35f))
                {
                    roamOrbitSide = -roamOrbitSide;
                    roamStepsInDirection = 0;
                }

                return;
            }
        }

        ScheduleNextRoam();
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
                DummyEnemy attackedEnemy = target;
                if (attackedEnemy != null)
                    attackedEnemy.ApplyCombatHit(new CombatHit(gameObject, "basic", basicToken, basicRoll.RawDamage, CreatureDamageKind.Physical, basicRoll.Critical, false, attackedEnemy.ClosestPoint(transform.position + Vector3.up), 0, basicElements));
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
        nextAttackAt = Time.time + attackCooldown * (combatant != null ? combatant.AttackIntervalScale : 1f);
        pendingHitAt = Time.time + attackHitDelay;
        hasPendingHit = true;
        basicToken++;
        basicRoll = RollBasicAttack();
        basicElements = combatant != null ? combatant.SnapshotElements(Capstone.Game.HudSystem.PetElement.Normal) : default;
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

    private void AwardDefeatExperience(DummyEnemy defeatedEnemy)
    {
        if (defeatedEnemy == null || defeatedEnemy.IsTrainingDummy)
        {
            return;
        }

        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        if (metadata == null)
            metadata = gameObject.AddComponent<PetCollectionMetadata>();
        bool gainedEffort = metadata.GrantEffortValues(defeatedEnemy.EffortYield);
        if (gainedEffort)
        {
            FindFirstObjectByType<PlayerSaveController>()?.RequestSave();
            FindFirstObjectByType<PetCommandHudProvider>()?.NotifyHudDataChanged();
        }

        if (defeatedEnemy.ExperienceReward <= 0)
            return;
        if (levelUpService == null)
        {
            levelUpService = FindFirstObjectByType<PetLevelUpService>();
        }

        if (levelUpService == null)
        {
            levelUpService = gameObject.AddComponent<PetLevelUpService>();
        }

        levelUpService.GrantBattleExperience(this, defeatedEnemy.ExperienceReward, out _);
    }

    private float CalculateAttackDamage()
    {
        return RollBasicAttack().RawDamage;
    }

    private CombatOffenseRoll RollBasicAttack()
    {
        if (combatant != null)
            return combatant.RollOffense(CreatureDamageKind.Physical);
        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        if (metadata == null)
            return new CombatOffenseRoll(Mathf.Max(0f, attackDamage), false);
        PetStatSnapshot stats = metadata.CurrentStats;
        float roll = Random.value;
        return new CombatOffenseRoll(CreatureCombatRules.Offense(stats, CreatureDamageKind.Physical, 1, 0, roll), roll < stats.CriticalRate / 100f);
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

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        var actionDriver = GetComponent<Capstone.Game.CreatureSystem.CreatureAnimationDriver>();
        if (actionDriver != null)
        {
            actionDriver.Bind(animator);
            if (actionDriver.Profile != null)
            {
                actionDriver.TryGetStates(Capstone.Game.CreatureSystem.CreatureAction.Idle, out idleStates);
                actionDriver.TryGetStates(Capstone.Game.CreatureSystem.CreatureAction.Move, out moveStates);
                actionDriver.TryGetStates(Capstone.Game.CreatureSystem.CreatureAction.Attack, out attackStates);
                actionDriver.TryGetStates(Capstone.Game.CreatureSystem.CreatureAction.Spawn, out spawnStates);
            }
        }

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
        if (animator == null || IsSkillAnimationActive() || state == PetState.AttackTarget || state == PetState.Dormant || state == PetState.Summoning)
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
        return (combatActions != null && combatActions.IsSkillBusy) || Time.time < skillAnimationUntil;
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
