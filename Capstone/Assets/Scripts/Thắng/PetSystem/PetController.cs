using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.MapSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class PetController : MonoBehaviour
{
    private static readonly float[] DirectAvoidanceAngles = { 35f, 70f, 105f, 140f };
    private static readonly Vector3[] SafeSpotFallbackOffsets =
    {
        new Vector3(1.7f, 0f, -1.7f),
        new Vector3(-2.2f, 0f, 0f),
        new Vector3(2.2f, 0f, 0f),
        new Vector3(0f, 0f, -2.2f)
    };

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

    [Header("References")]
    public Transform owner;
    public Animator animator;
    public float SummonPresentationAnimationDuration => summonAnimationDuration;
    public bool HasSummonPresentationAnimationFinished
    {
        get
        {
            if (!summonPresentationAnimationStarted) return false;
            if (!summonCastActive) return Time.time - summonStartedAt >= summonAnimationDuration;
            if (animator == null) return true;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            bool inCast = info.shortNameHash == Animator.StringToHash("Cast Spell");
            return !animator.IsInTransition(0) && (inCast && info.normalizedTime >= .98f
                || !inCast && Time.time - summonStartedAt >= summonAnimationDuration);
        }
    }
    public void BindVisualAnimator(Animator nextAnimator)
    {
        if (nextAnimator == null) return;
        RestoreSummonAnimatorSpeed();
        animator = nextAnimator;
        animatorInitiallyEnabled = nextAnimator.enabled;
        ConfigureAnimator();
    }

    public bool ShouldShowVisualRenderer(Renderer renderer)
    {
        if (renderer == null || !IsSummoned) return false;
        var visual = GetComponentInChildren<Capstone.Game.HudSystem.PetEvolutionVisualController>(true);
        return (visual == null || visual.IsCurrentVisualRenderer(renderer))
            && rendererEnabledStates.TryGetValue(renderer, out bool allowed) && allowed;
    }

    public void SetVisualRendererEnabled(Renderer renderer, bool enabled)
    {
        if (renderer == null) return;
        rendererEnabledStates[renderer] = enabled;
        renderer.enabled = enabled && IsSummoned;
    }

    public void RefreshVisualPresence() => SetWorldPresence(IsSummoned);
    public NavMeshAgent agent;

    [Header("Follow")]
    public Vector3 followOffset = new Vector3(-1.4f, 0f, -1.6f);
    public float followDistance = 4f;
    public float followResumeDistance = 7f;
    public float leashDistance = 10f;
    public float teleportDistance = 24f;

    [Header("Movement")]
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
    [Header("Movement without NavMesh")]
    [Min(0.1f)] public float directBodyRadius = 0.32f;
    [Min(0.2f)] public float directProbeHeight = 0.65f;
    [Min(0.2f)] public float directLookAhead = 0.9f;
    [Min(0.05f)] public float directMaxStepHeight = 0.3f;
    [Range(0.1f, 1f)] public float directMinGroundNormal = 0.65f;
    public LayerMask directObstacleMask = ~0;
    [Min(0.1f)] public float minimumNavigationRadius = 0.65f;
    [Min(0.02f)] public float navPathRefreshInterval = 0.12f;
    [Min(0.01f)] public float navDestinationThreshold = 0.15f;
    [Min(0.1f)] public float navMeshRetryInterval = 1f;

    [Header("Combat")]
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
            return Mathf.Max(0f, attackDamage + (metadata != null ? metadata.Attack : 0));
        }
    }
    public float attackCooldown = 0.95f;
    public float attackHitDelay = 0.28f;
    public float attackStateTime = 0.75f;

    [Header("Summon")]
    public bool useSelfInput = false;
    public KeyCode summonKey = KeyCode.Alpha1;
    public float spawnDuration = 1.1f;
    public bool summonOnCommand = false;

    [Header("Roam")]
    public bool roamAroundOwner = true;
    public float roamMinRadius = 2.2f;
    public float roamMaxRadius = 5f;
    public float roamIdleTimeMin = 1f;
    public float roamIdleTimeMax = 3f;
    public float roamWalkSpeed = 2f;
    public float roamRunSpeed = 4.6f;
    public float roamRunChance = 0.12f;
    public float roamPointTolerance = 0.45f;

    [Header("Animator States")]
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

    public bool CanReceiveCommands
    {
        get { return IsSummoned && state != PetState.Summoning && !recallLocked; }
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
        animatorInitiallyEnabled = animator != null && animator.enabled;
        CacheWorldPresence();
        SetWorldPresence(false);
    }

    private void OnEnable()
    {
        EnsureMapMarker();
        markerInitiallyEnabled = mapMarker != null && mapMarker.enabled;
        if (!IsSummoned && mapMarker != null)
        {
            mapMarker.enabled = false;
        }
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

        // A pet instantiated while loading a save can be restored before its first
        // Start frame. Do not hide it again after that restore has succeeded.
        if (!IsSummoned)
        {
            EnterDormant();
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
        hasRoamDestination = false;
        directStuckSince = 0f;
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

        if (owner == null || !TryFindSafeSpotNearOwner(out Vector3 spot)
            || !TrySummonAt(spot, owner.forward))
        {
            Debug.LogWarning("Pet summon failed: không có mặt đất an toàn gần player.", this);
        }
    }

    public void SummonAt(Vector3 worldPosition, Vector3 facingDirection)
    {
        if (!TrySummonAt(worldPosition, facingDirection))
        {
            Debug.LogWarning("Pet summon failed: vị trí spawn không có nền an toàn.", this);
        }
    }

    public bool TrySummonAt(Vector3 worldPosition, Vector3 facingDirection)
    {
        return TrySummonAt(worldPosition, facingDirection, false);
    }

    public bool TrySummonAt(Vector3 worldPosition, Vector3 facingDirection, bool deferSpawnAnimation)
    {
        if (!TryPlaceForSummon(worldPosition))
        {
            return false;
        }

        IsSummoned = true;
        SetWorldPresence(true);
        BeginSummon(deferSpawnAnimation);
        if (facingDirection.sqrMagnitude > 0.001f)
        {
            FaceDirection(facingDirection);
        }

        return true;
    }

    public void SetSummonPresentationPosition(Vector3 position)
    {
        if (!IsSummoned || !summonRevealPending)
        {
            return;
        }

        DisableNavigationAgent();
        SetPresentationColliders(false);
        transform.position = position;
    }

    public bool CompleteSummonPresentation(Vector3 groundPosition)
    {
        if (!IsSummoned || !summonRevealPending || !TryPlaceForSummon(groundPosition))
        {
            return false;
        }

        StartSummonPresentationAnimation();
        SetPresentationColliders(true);
        summonRevealPending = false;
        return true;
    }

    public void StartSummonPresentationAnimation(float presentationSpeed = 1f)
    {
        if (!IsSummoned || !summonRevealPending || summonPresentationAnimationStarted)
        {
            return;
        }

        summonPresentationAnimationStarted = true;
        summonStartedAt = Time.time;
        float speed = Mathf.Max(0.1f, presentationSpeed);
        if (animator != null)
        {
            summonAnimatorSpeedBefore = animator.speed;
            summonAnimatorSpeedOverridden = true;
            animator.speed = speed;
        }
        string[] castState = { "Cast Spell" };
        if (HasAnimatorState(castState) && PlayAnimatorState(castState, attackFade, true))
        {
            summonAnimationDuration = FindAnimationDuration("Cast Spell", spawnDuration) / speed;
            summonCastActive = true;
        }
        else
        {
            if (!warnedMissingCastSpell)
            {
                Debug.LogWarning("Pet has no Cast Spell animation; using its spawn animation.", this);
                warnedMissingCastSpell = true;
            }
            PlayAnimatorState(spawnStates, attackFade, true);
            summonAnimationDuration = spawnDuration / speed;
            summonCastActive = false;
        }
    }

    public void SetRecallPresentation(bool active)
    {
        if (active)
        {
            DisableNavigationAgent();
        }

        SetPresentationColliders(!active && IsSummoned);
    }

    private void SetPresentationColliders(bool visible)
    {
        foreach (KeyValuePair<Collider, bool> entry in colliderEnabledStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = visible && entry.Value;
            }
        }
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

    public bool TryRestoreSummonedNearOwner()
    {
        if (!TryFindSafeSpotNearOwner(out Vector3 spot, 2.5f, true) || !TryPlaceForSummon(spot))
        {
            EnterDormant();
            return false;
        }

        IsSummoned = true;
        SetWorldPresence(true);
        recallLocked = false;
        target = null;
        hasRoamDestination = false;
        directStuckSince = 0f;
        state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
        ScheduleNextRoam(0.5f);
        PlayAnimatorState(idleStates, locomotionFade, true);
        return true;
    }

    public void Withdraw()
    {
        if (!IsSummoned && state == PetState.Dormant)
        {
            return;
        }

        EnterDormant();
    }

    public void SetRecallLocked(bool locked)
    {
        if (!locked)
        {
            recallFacingEnemy = null;
            recallWasInCombat = false;
            hasRecallVisualCenter = false;
            recallFacingTargetLost = false;
        }
        if (recallLocked == locked)
        {
            return;
        }

        if (locked)
        {
            hasRecallVisualCenter = false;
            recallFacingTargetLost = false;
            recallWasInCombat = HasCombatTarget;
            recallFacingEnemy = recallWasInCombat ? target : null;
            recallLocked = true;
            target = null;
            hasRoamDestination = false;
            hasPendingHit = false;
            hasPendingSkillAnimation = false;
            pendingSkillCandidates = null;
            skillAnimationUntil = 0f;
            StopMovement(false);
            SetAnimatorBool(attackingParameter, false);
            UpdateRecallFacing();
            return;
        }

        recallLocked = false;
        recallFacingEnemy = null;
        recallWasInCombat = false;

        if (IsSummoned)
        {
            state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
            ScheduleNextRoam(0.1f);
        }
    }

    private void UpdateRecallFacing()
    {
        Vector3 origin = hasRecallVisualCenter ? recallVisualCenter : transform.position;
        if (recallWasInCombat)
        {
            if (!recallFacingTargetLost && recallFacingEnemy != null && recallFacingEnemy.IsAlive)
                FaceDirection(recallFacingEnemy.TargetPosition - origin);
            else recallFacingTargetLost = true;
            // A lost combat target must not turn the pet back toward its owner.
        }
        else if (owner != null) FaceDirection(owner.position - origin);
    }

    private Vector3 recallVisualCenter;
    private bool hasRecallVisualCenter;
    private bool recallFacingTargetLost;

    public void SetRecallVisualCenter(Vector3 center)
    {
        if (!recallLocked) return;
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

    private void EnterDormant()
    {
        RestoreSummonAnimatorSpeed();
        summonRevealPending = false;
        summonCastActive = false;
        summonPresentationAnimationStarted = false;
        recallLocked = false;
        IsSummoned = false;
        target = null;
        hasPendingHit = false;
        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
        skillAnimationUntil = 0f;
        state = PetState.Dormant;
        hasRoamDestination = false;
        lastRoamDirection = Vector3.zero;
        directStuckSince = 0f;
        StopMovement(false);
        SetWorldPresence(false);
    }

    private bool TryPlaceForSummon(Vector3 requestedPosition)
    {
        if (!TryFindWalkableGround(requestedPosition, 0.65f, out Vector3 groundPoint)
            || !HasRoomAt(groundPoint))
        {
            return false;
        }

        if (useNavMeshWhenAvailable && TryPlaceAgentOnNavMesh(groundPoint, 0.75f))
        {
            return true;
        }

        DisableNavigationAgent();

        transform.position = groundPoint;
        return true;
    }

    private bool TryFindSafeSpotNearOwner(out Vector3 spot, float ownerGroundTolerance = 1f,
        bool expandedSearch = false)
    {
        spot = default;
        if (owner == null || !TryFindWalkableGround(owner.position, ownerGroundTolerance,
                out Vector3 ownerGround))
        {
            return false;
        }

        if (TryResolveSafeSpot(followOffset, ownerGround.y, out spot))
        {
            return true;
        }

        for (int i = 0; i < SafeSpotFallbackOffsets.Length; i++)
        {
            if (TryResolveSafeSpot(SafeSpotFallbackOffsets[i], ownerGround.y, out spot))
            {
                return true;
            }
        }

        if (expandedSearch)
        {
            // A fixed offset can land inside scenery when a save first loads.
            // Search nearby walkable ground rather than hiding the pet forever.
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 2.6f : 4f;
                for (int step = 0; step < 12; step++)
                {
                    float angle = step * Mathf.PI / 6f;
                    Vector3 offset = new Vector3(Mathf.Sin(angle) * radius, 0f,
                        Mathf.Cos(angle) * radius);
                    if (TryResolveSafeSpot(offset, ownerGround.y, out spot)) return true;
                }
            }
        }

        return false;
    }

    private bool TryResolveSafeSpot(Vector3 localOffset, float groundHeight, out Vector3 spot)
    {
        Vector3 candidate = owner.TransformPoint(localOffset);
        candidate.y = groundHeight;
        if (TryFindWalkableGround(candidate, 0.6f, out spot) && HasRoomAt(spot))
        {
            return true;
        }

        spot = default;
        return false;
    }

    private bool HasRoomAt(Vector3 point)
    {
        int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * 0.45f,
            point + Vector3.up * 1.2f, Mathf.Max(0.25f, directBodyRadius),
            presenceHits, directObstacleMask, QueryTriggerInteraction.Ignore);
        if (count == presenceHits.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            Collider candidate = presenceHits[i];
            if (candidate != null && !IsIgnoredDirectObstacle(candidate)
                && !IsSupportingGround(candidate, point)
                && candidate.bounds.max.y > point.y + directMaxStepHeight)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSupportingGround(Collider collider, Vector3 point)
    {
        Ray ray = new Ray(point + Vector3.up, Vector3.down);
        return collider.Raycast(ray, out RaycastHit hit, 1.5f)
            && hit.normal.y >= directMinGroundNormal
            && Mathf.Abs(hit.point.y - point.y) <= 0.2f;
    }

    private void CacheWorldPresence()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.GetComponentInParent<AAMAP.MapIcon>(true) == null
                && !rendererEnabledStates.ContainsKey(renderer))
            {
                rendererEnabledStates.Add(renderer, renderer.enabled);
            }
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && !colliderEnabledStates.ContainsKey(collider))
            {
                colliderEnabledStates.Add(collider, collider.enabled);
            }
        }

    }

    private void SetWorldPresence(bool visible)
    {
        CacheWorldPresence();
        foreach (KeyValuePair<Renderer, bool> entry in rendererEnabledStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = visible && entry.Value;
            }
        }

        foreach (KeyValuePair<Collider, bool> entry in colliderEnabledStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = visible && entry.Value;
            }
        }

        if (animator != null)
        {
            animator.enabled = visible && animatorInitiallyEnabled;
        }

        if (mapMarker != null)
        {
            mapMarker.enabled = visible && markerInitiallyEnabled;
        }

        if (!visible)
        {
            DisableNavigationAgent();
        }
    }

    private void BeginSummon(bool deferSpawnAnimation)
    {
        if (state == PetState.Summoning)
        {
            return;
        }

        RestoreSummonAnimatorSpeed();
        recallLocked = false;
        nextNavMeshRetryAt = 0f;
        target = null;
        hasRoamDestination = false;
        directStuckSince = 0f;
        IsSummoned = true;
        state = PetState.Summoning;
        summonStartedAt = Time.time;
        summonRevealPending = deferSpawnAnimation;
        summonCastActive = false;
        summonPresentationAnimationStarted = false;
        summonAnimationDuration = spawnDuration;
        StopMovement(false);
        SetAnimatorFloat(speedParameter, 0f);
        SetAnimatorBool(movingParameter, false);
        if (!deferSpawnAnimation)
        {
            PlayAnimatorState(spawnStates, attackFade, true);
        }
    }

    private void UpdateSummoning()
    {
        StopMovement(false);
        if (summonRevealPending)
        {
            return;
        }

        if (summonCastActive)
        {
            AnimatorStateInfo castState = animator != null
                ? animator.GetCurrentAnimatorStateInfo(0) : default;
            bool castFinished = animator != null && !animator.IsInTransition(0)
                && castState.shortNameHash == Animator.StringToHash("Cast Spell")
                && castState.normalizedTime >= 0.98f;
            bool castExitedAfterDuration = animator != null && !animator.IsInTransition(0)
                && castState.shortNameHash != Animator.StringToHash("Cast Spell")
                && Time.time - summonStartedAt >= summonAnimationDuration;
            if (!castFinished && !castExitedAfterDuration && Time.time - summonStartedAt
                < Mathf.Max(3f, summonAnimationDuration + 0.5f))
            {
                return;
            }

            if (!castFinished && !castExitedAfterDuration)
            {
                Debug.LogWarning("Cast Spell did not finish before the summon animation timeout.", this);
            }
        }
        else if (Time.time - summonStartedAt < summonAnimationDuration)
        {
            return;
        }

        summonCastActive = false;
        RestoreSummonAnimatorSpeed();
        state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
        ScheduleNextRoam(0.25f);
        PlayAnimatorState(idleStates, locomotionFade, true);
        if (!roamAroundOwner)
        {
            MoveToFollowSpot();
        }
    }

    private void RestoreSummonAnimatorSpeed()
    {
        if (!summonAnimatorSpeedOverridden) return;
        if (animator != null) animator.speed = summonAnimatorSpeedBefore;
        summonAnimatorSpeedOverridden = false;
    }

    private void OnDisable()
    {
        RestoreSummonAnimatorSpeed();
    }

    private void UpdateFollow()
    {
        float ownerDistance = FlatDistance(transform.position, owner.position);
        if (ownerDistance > teleportDistance
            || Mathf.Abs(transform.position.y - owner.position.y) > 3f)
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
        if (ownerDistance > teleportDistance
            || Mathf.Abs(transform.position.y - owner.position.y) > 3f)
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
        if (owner == null || !TryFindWalkableGround(owner.position, 1f,
                out Vector3 ownerGround))
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
            if (!TryFindWalkableGround(desired, 0.65f, out desired)
                || !HasRoomAt(desired))
            {
                continue;
            }

            if (useNavMeshWhenAvailable && agent != null && agent.enabled
                && NavMesh.SamplePosition(desired, out NavMeshHit hit, 2.5f,
                    NavMesh.AllAreas))
            {
                desired = hit.position;
            }

            roamDestination = desired;
            activeRoamSpeed = Random.value < roamRunChance ? roamRunSpeed : roamWalkSpeed;
            hasRoamDestination = FlatDistance(transform.position, roamDestination) > roamPointTolerance;
            if (hasRoamDestination)
            {
                lastRoamDirection = direction.normalized;
                if (++roamStepsInDirection >= 3
                    && (roamStepsInDirection >= 6 || Random.value < 0.35f))
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
                bool wasAlive = attackedEnemy != null && attackedEnemy.IsAlive;
                attackedEnemy?.TakeDamage(CalculateAttackDamage(), gameObject);
                if (wasAlive && attackedEnemy != null && !attackedEnemy.IsAlive)
                {
                    AwardDefeatExperience(attackedEnemy);
                }
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
            return;
        }

        StopMovement();
    }

    private bool TryMoveWithAgent(Vector3 destination, float speed, float stopDistance)
    {
        if (!useNavMeshWhenAvailable || agent == null || !agent.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!agent.enabled && !TryEnableAgentOnNavMesh())
        {
            return false;
        }

        if (!agent.isOnNavMesh)
        {
            if (!TryWarpToNavMesh(transform.position, 2.5f))
            {
                DisableNavigationAgent();
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
        agent.stoppingDistance = Mathf.Max(stopDistance, agent.radius * 0.35f);
        float destinationThreshold = Mathf.Max(0.01f, navDestinationThreshold);
        bool destinationChanged = !navDestinationInitialized
            || (navDestination - lastNavDestination).sqrMagnitude
                > destinationThreshold * destinationThreshold;
        bool needsPath = agent.isStopped || (!agent.hasPath && !agent.pathPending);
        bool canRefreshDestination = Time.time >= nextNavPathRefreshAt;

        agent.isStopped = false;
        if (needsPath || (destinationChanged && canRefreshDestination))
        {
            if (!agent.SetDestination(navDestination))
            {
                DisableNavigationAgent();
                return false;
            }
            lastNavDestination = navDestination;
            navDestinationInitialized = true;
            nextNavPathRefreshAt = Time.time + Mathf.Max(0.02f, navPathRefreshInterval);
        }

        FaceVelocity(agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity);
        return true;
    }

    private void AwardDefeatExperience(DummyEnemy defeatedEnemy)
    {
        if (defeatedEnemy == null)
        {
            return;
        }

        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        if (metadata == null) metadata = gameObject.AddComponent<PetCollectionMetadata>();
        bool gainedEffort = metadata.GrantEffortValues(defeatedEnemy.EffortYield);
        if (gainedEffort)
        {
            FindFirstObjectByType<PlayerSaveController>()?.RequestSave();
            FindFirstObjectByType<PetCommandHudProvider>()?.NotifyHudDataChanged();
        }
        if (defeatedEnemy.ExperienceReward <= 0) return;

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
        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        if (metadata == null) return Mathf.Max(0f, attackDamage);

        PetStatSnapshot stats = metadata.CurrentStats;
        float damage = Mathf.Max(0f, attackDamage + stats.Attack);
        if (Random.value >= stats.CriticalRate / 100f)
        {
            return damage;
        }

        return damage * (1f + stats.CriticalDamage / 100f);
    }

    private bool TryEnableAgentOnNavMesh()
    {
        if (agent == null)
        {
            return false;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            return true;
        }

        DisableNavigationAgent();

        if (Time.time < nextNavMeshRetryAt)
        {
            return false;
        }

        nextNavMeshRetryAt = Time.time + Mathf.Max(0.1f, navMeshRetryInterval);
        return TryPlaceAgentOnNavMesh(transform.position, 6f);
    }

    private bool TryWarpToNavMesh(Vector3 position, float searchRadius)
    {
        return TryPlaceAgentOnNavMesh(position, searchRadius);
    }

    private bool TryPlaceAgentOnNavMesh(Vector3 position, float searchRadius)
    {
        if (agent == null || !agent.gameObject.activeInHierarchy
            || !NavMesh.SamplePosition(position, out NavMeshHit hit,
                Mathf.Max(0.25f, searchRadius), agent.areaMask)
            || Mathf.Abs(hit.position.y - position.y) > 0.35f)
        {
            return false;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.Warp(hit.position))
            {
                return false;
            }
        }
        else
        {
            DisableNavigationAgent();

            transform.position = hit.position;
            agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                DisableNavigationAgent();
                return false;
            }
        }

        agent.isStopped = true;
        agent.ResetPath();
        navDestinationInitialized = false;
        return true;
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

        float step = Mathf.Min(Mathf.Max(0f, speed) * Time.deltaTime,
            toDestination.magnitude - stopDistance);
        if (step <= 0f)
        {
            directStuckSince = 0f;
            StopMovement();
            return;
        }

        if (!TryChooseDirectStep(current, toDestination.normalized, step,
                out Vector3 next, out Vector3 direction))
        {
            if (directStuckSince <= 0f)
            {
                directStuckSince = Time.time;
            }
            else if (Time.time - directStuckSince > 2.5f
                && (state == PetState.FollowOwner || state == PetState.RoamAroundOwner))
            {
                TeleportNearOwner();
                directStuckSince = 0f;
            }

            StopMovement();
            return;
        }

        directStuckSince = 0f;
        transform.position = next;
        FaceDirection(direction);
        directMoveSpeed = step / Mathf.Max(Time.deltaTime, 0.0001f);
    }

    private bool TryChooseDirectStep(Vector3 current, Vector3 desiredDirection, float step,
        out Vector3 next, out Vector3 chosenDirection)
    {
        chosenDirection = desiredDirection;
        if (Time.time < directAvoidanceUntil && Mathf.Abs(directAvoidanceAngle) > 0.01f)
        {
            Vector3 heldDirection = Quaternion.Euler(0f, directAvoidanceAngle, 0f)
                * desiredDirection;
            if (TryDirectStep(current, heldDirection, step, out next))
            {
                chosenDirection = heldDirection;
                return true;
            }
        }

        if (TryDirectStep(current, desiredDirection, step, out next))
        {
            directAvoidanceAngle = 0f;
            return true;
        }

        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            int side = sideIndex == 0 ? directAvoidanceSide : -directAvoidanceSide;
            for (int angleIndex = 0; angleIndex < DirectAvoidanceAngles.Length; angleIndex++)
            {
                float avoidanceAngle = side * DirectAvoidanceAngles[angleIndex];
                Vector3 candidateDirection = Quaternion.Euler(0f, avoidanceAngle, 0f)
                    * desiredDirection;
                if (!TryDirectStep(current, candidateDirection, step, out next))
                {
                    continue;
                }

                directAvoidanceSide = side;
                directAvoidanceAngle = avoidanceAngle;
                directAvoidanceUntil = Time.time + 0.55f;
                chosenDirection = candidateDirection;
                return true;
            }
        }

        next = current;
        return false;
    }

    private bool TryDirectStep(Vector3 current, Vector3 direction, float step, out Vector3 next)
    {
        next = current;
        float radius = Mathf.Max(0.1f, directBodyRadius);
        Vector3 probeOrigin = current + Vector3.up
            * Mathf.Max(directProbeHeight, radius + 0.1f);
        float lookAhead = step + Mathf.Max(radius, directLookAhead);
        int count = Physics.SphereCastNonAlloc(probeOrigin, radius, direction,
            directObstacleHits, lookAhead, directObstacleMask, QueryTriggerInteraction.Ignore);
        if (count == directObstacleHits.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = directObstacleHits[i];
            Collider obstacle = hit.collider;
            if (obstacle == null || IsIgnoredDirectObstacle(obstacle))
            {
                continue;
            }

            bool walkableGround = hit.normal.y >= directMinGroundNormal
                && hit.point.y <= current.y + directMaxStepHeight;
            if (!walkableGround)
            {
                return false;
            }
        }

        Vector3 candidate = current + direction * step;
        if (snapFallbackToGround)
        {
            if (!TryFindWalkableGround(candidate, directMaxStepHeight, out Vector3 groundPoint))
            {
                return false;
            }

            candidate.y = groundPoint.y;
        }

        next = candidate;
        return true;
    }

    private bool IsIgnoredDirectObstacle(Collider obstacle)
    {
        if (obstacle.transform.IsChildOf(transform))
        {
            return true;
        }

        if (owner != null && obstacle.transform.IsChildOf(owner))
        {
            return true;
        }

        PetController pet = obstacle.GetComponentInParent<PetController>();
        if (pet != null)
        {
            return true;
        }

        DummyEnemy enemy = obstacle.GetComponentInParent<DummyEnemy>();
        return enemy != null && enemy == target;
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
            if (updateAnimation && state != PetState.Dormant && state != PetState.Summoning)
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

    private bool TeleportNearOwner()
    {
        if (!TryFindSafeSpotNearOwner(out Vector3 spot))
        {
            return false;
        }

        if (useNavMeshWhenAvailable && TryPlaceAgentOnNavMesh(spot, 0.75f))
        {
            StopMovement();
            return true;
        }

        DisableNavigationAgent();

        transform.position = spot;
        StopMovement();
        return true;
    }

    private void DisableNavigationAgent()
    {
        if (agent == null)
        {
            return;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        if (agent.enabled)
        {
            agent.enabled = false;
        }

        navDestinationInitialized = false;
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

    private bool TryFindWalkableGround(Vector3 position, float maxHeightChange,
        out Vector3 groundPoint)
    {
        groundPoint = position;
        Vector3 origin = position + Vector3.up * 1.5f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 4f, groundMask, QueryTriggerInteraction.Ignore);
        if (count <= 0 || count == groundHits.Length)
        {
            return false;
        }

        int best = -1;
        float bestHeightDelta = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || IsIgnoredDirectObstacle(hit.collider)
                || (hit.collider.attachedRigidbody != null
                    && !hit.collider.attachedRigidbody.isKinematic)
                || hit.normal.y < directMinGroundNormal
                || Mathf.Abs(hit.point.y - position.y) > maxHeightChange)
            {
                continue;
            }

            float heightDelta = Mathf.Abs(hit.point.y - position.y);
            if (heightDelta >= bestHeightDelta)
            {
                continue;
            }

            best = i;
            bestHeightDelta = heightDelta;
        }

        if (best < 0)
        {
            return false;
        }

        groundPoint.y = groundHits[best].point.y;
        return true;
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

        if (!useNavMeshWhenAvailable)
        {
            DisableNavigationAgent();
        }

        agent.updateRotation = false;
        agent.speed = followSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = followDistance;
        agent.radius = Mathf.Max(agent.radius, minimumNavigationRadius);
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.autoTraverseOffMeshLink = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
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

