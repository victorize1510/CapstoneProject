using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BasicPlayerMovement : MonoBehaviour
{
    private const float InputDeadZone = 0.05f;

    private enum PlayerState
    {
        IdleNeutral,
        IdleSad,
        IdleHappy,
        IdleBattle,
        SlowRun,
        Sprint,
        StandingToCrouch,
        CrouchIdle,
        CrouchWalking,
        CrouchToStanding,
        IdleToRoll,
        SprintingToRoll,
        RunningBackward,
        Floating,
        IdleJump,
        Jump,
        PickingUp,
        Throw
    }

    private enum PendingAfterStand
    {
        None,
        Roll,
        Jump,
        PickUp,
        Throw
    }

    [Header("References")]
    public Animator animator;
    public Transform cameraTransform;

    [Header("Movement")]
    public float slowRunSpeed = 3.5f;
    public float sprintSpeed = 6f;
    public float backwardSpeed = 2.8f;
    public float crouchWalkSpeed = 1.55f;
    public float acceleration = 18f;
    public float deceleration = 24f;
    public float turnSpeed = 720f;
    public float aimTurnSpeed = 900f;
    public bool cameraRelativeMovement = true;

    [Header("Feature Toggles")]
    public bool enableJump = true;
    public bool enableCrouch = true;

    [Header("Animation Motion")]
    public bool useRootMotion = false;
    public bool rootMotionDrivesLocomotion = false;
    public bool rootMotionDrivesRoll = false;
    public bool rootMotionDrivesJump = false;
    public bool codeDrivesInPlaceLocomotion = true;
    public bool fallbackToCodeMotionWhenRootMotionIsSmall = false;
    public float rootMotionScale = 1f;
    public float rootMotionFallbackDelay = 0.08f;
    public float rootMotionFallbackThreshold = 0.005f;
    public bool applyAnimatorRootYaw = false;
    public bool applyAnimatorRootRotationToVisual = false;
    public bool keepAnimatorTransformPinned = false;

    [Header("Jump")]
    public KeyCode jumpKey = KeyCode.Space;
    public float jumpInputCooldown = 0.18f;
    public float jumpGroundLockTime = 0.12f;
    public float jumpHeight = 2.75f;
    public float gravity = -25f;
    public float fallGravityMultiplier = 1.45f;
    public float groundedStickForce = -2f;
    public float landingCarryDistance = 2.5f;
    public float landingCarryDuration = 0.55f;
    public float jumpForwardSpeedMultiplier = 1f;
    public float landingCarryDistanceRatio = 0.25f;
    public float landingCarrySpeedMultiplier = 0.5f;
    public float landingNudgeDistance = 0.35f;
    public bool resizeControllerDuringJump = false;
    public float jumpControllerHeight = 2.25f;
    public float jumpControllerRadius = 0.35f;
    public Vector3 jumpControllerCenter = new Vector3(0f, 1.125f, 0f);
    public bool liftControllerCenterDuringJump = true;
    public float jumpControllerCenterLift = 0.25f;
    public AnimationCurve jumpControllerCenterLiftCurve = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(0.45f, 1f),
        new Keyframe(1f, 0f));
    public float floatFallDelay = 0.18f;
    public float floatFallVerticalSpeed = -3f;

    [Header("Grounding")]
    public bool snapFeetToGround = true;
    public LayerMask groundLayers = ~0;
    public float groundSnapDistance = 0.45f;
    public float groundSnapProbeHeight = 0.35f;
    public float groundContactOffset = 0f;

    [Header("Roll")]
    public bool enableRoll = false;
    public KeyCode rollKey = KeyCode.Q;
    public float idleRollDuration = 0.78f;
    public float sprintRollDuration = 0.55f;
    public float idleRollDistance = 5.2f;
    public float sprintRollDistance = 7.2f;
    public bool resizeControllerDuringRoll = true;
    public float rollControllerHeight = 0.9f;
    public float rollControllerRadius = 0.3f;
    public Vector3 rollControllerCenter = new Vector3(0f, 0.45f, 0f);
    public AnimationCurve rollMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Crouch")]
    public KeyCode crouchKey = KeyCode.LeftControl;
    public KeyCode alternateCrouchKey = KeyCode.RightControl;
    public float standingToCrouchDuration = 0.5f;
    public float crouchToStandingDuration = 0.5f;

    [Header("Actions")]
    public KeyCode pickUpKey = KeyCode.E;
    public bool enableAimMode = false;
    public int aimMouseButton = 1;
    public bool rightMouseButtonThrows = false;
    public bool faceLockedEnemy = false;
    public bool requirePickupTarget = false;
    public bool hasPickupTarget = false;
    public float jumpDuration = 0.95f;
    public float pickUpDuration = 1.15f;
    public float throwDuration = 0.95f;
    public bool useAnimatorStateLengthForActions = true;
    public bool useAnimatorStateLengthForJump = false;
    public float actionExitNormalizedTime = 0.98f;
    public float minimumActionDuration = 0.08f;

    [Header("Idle / Battle")]
    public bool isInSafeArea = true;
    public bool isEnemyNearby = false;
    public bool isEnemyDetectedPlayer = false;
    public float idleSadDelay = 5f;
    public float idleAlternateInterval = 3f;
    public float battleRelaxDelay = 5f;

    [Header("Optional Enemy Probe")]
    public bool autoDetectEnemies = false;
    public float enemyDetectionRadius = 6f;
    public LayerMask enemyLayers = ~0;
    public float enemyProbeInterval = 0.25f;
    public string[] enemyTags = { "Enemy", "Monster" };
    public string[] friendlyTags = { "NPC", "Friendly" };

    [Header("Animator States")]
    public string idleNeutralState = "IdleNeutral";
    public string idleSadState = "IdleSad";
    public string idleHappyState = "IdleHappy";
    public string idleBattleState = "IdleBattle";
    public string slowRunState = "SlowRun";
    public string sprintState = "Sprint";
    public string standingToCrouchState = "StandingToCrouch";
    public string crouchIdleState = "CrouchIdle";
    public string crouchWalkingState = "CrouchWalking";
    public string crouchToStandingState = "CrouchToStanding";
    public string idleToRollState = "IdleToRoll";
    public string sprintingToRollState = "SprintingToRoll";
    public string runningBackwardState = "RunningBackward";
    public string floatingState = "Floating";
    public string idleJumpState = "IdleJump";
    public string jumpState = "Jump";
    public string pickingUpState = "PickingUp";
    public string throwState = "Throw";
    public float locomotionFadeTime = 0.12f;
    public float actionFadeTime = 0.06f;

    [Header("Animator Parameters")]
    public string speedParameter = "Speed";
    public string moveXParameter = "MoveX";
    public string moveYParameter = "MoveY";
    public string groundedParameter = "Grounded";
    public string verticalSpeedParameter = "VerticalSpeed";
    public string isMovingParameter = "IsMoving";
    public string isSprintingParameter = "IsSprinting";
    public string isCrouchingParameter = "IsCrouching";
    public string isEnemyNearbyParameter = "IsEnemyNearby";
    public string isRollingParameter = "IsRolling";
    public string isJumpingParameter = "IsJumping";
    public string isPickingUpParameter = "IsPickingUp";
    public string isThrowingParameter = "IsThrowing";
    public string idleTimerParameter = "IdleTimer";

    public bool IsRolling
    {
        get { return currentState == PlayerState.IdleToRoll || currentState == PlayerState.SprintingToRoll; }
    }


    public bool IsAiming
    {
        get { return aimHeld; }
    }

    public bool IsCrouching
    {
        get { return isCrouching; }
    }

    private readonly Collider[] enemyProbeHits = new Collider[16];
    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private readonly HashSet<int> animatorParameters = new HashSet<int>();
    private readonly Dictionary<string, string[]> stateFallbacks = new Dictionary<string, string[]>();

    private CharacterController characterController;
    private BasicCameraFollow cameraFollow;
    private Transform animatorTransform;
    private Vector3 animatorLocalPosition;
    private Quaternion animatorLocalRotation;
    private Vector3 standingControllerCenter;

    private PlayerState currentState = PlayerState.IdleNeutral;
    private PendingAfterStand pendingAfterStand = PendingAfterStand.None;
    private Vector2 moveInput;
    private Vector3 moveDirection;
    private Vector3 horizontalVelocity;
    private Vector3 rollDirection;
    private Vector3 jumpCarryDirection;
    private Vector3 landingCarryDirection;
    private float verticalVelocity;
    private float jumpHorizontalSpeed;
    private float jumpExpectedAirTime;
    private float jumpLandingCarryDistance;
    private float landingCarrySpeed;
    private float airborneTimer;
    private float idleTimer;
    private float stateTimer;
    private float activeStateDuration;
    private float previousRollCurveValue;
    private float landingCarryTimer;
    private float lastJumpStartTime = -100f;
    private float battleRelaxTimer = float.PositiveInfinity;
    private float enemyProbeTimer;
    private float lastRootMotionTime = -100f;
    private float lastHorizontalRootMotionTime = -100f;
    private int currentStateHash;
    private float standingControllerHeight;
    private float standingControllerRadius;
    private bool activeOneShot;
    private bool gameplayInputLocked;
    private bool grounded;
    private bool hasMoveInput;
    private bool sprintHeld;
    private bool aimHeld;
    private bool isCrouching;
    private bool crouchWanted;
    private bool returnToCrouchAfterAction;
    private bool jumpStartedFromMovement;
    private bool jumpConsumedUntilGrounded;
    private bool jumpInputReleasedSinceJump = true;
    private bool jumpLeftGround;
    private bool autoEnemyNearby;
    private bool standingControllerShapeCaptured;
    private bool controllerSizedForRoll;
    private bool controllerSizedForJump;
    private Animator configuredAnimator;
    private bool configuredRootMotion;
    private bool animatorConfigurationInitialized;

    private bool EnemyThreatActive
    {
        get { return isEnemyNearby || isEnemyDetectedPlayer || autoEnemyNearby; }
    }

    private bool ShouldUseBattleIdle
    {
        get { return EnemyThreatActive || battleRelaxTimer < battleRelaxDelay; }
    }

    private bool CanPickUp
    {
        get { return !requirePickupTarget || hasPickupTarget; }
    }

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        SanitizeRigidbody();
        animator = FindBestAnimator();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        CaptureStandingControllerShape();
        SanitizeRigidbody();

        if (animator == null)
        {
            animator = FindBestAnimator();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        BuildStateFallbacks();
        RefreshCameraFollow();
        ConfigureAnimator();
        CacheAnimatorParameters();
        CaptureAnimatorAnchor();
    }

    private void OnValidate()
    {
        slowRunSpeed = Mathf.Max(0f, slowRunSpeed);
        sprintSpeed = Mathf.Max(slowRunSpeed, sprintSpeed);
        backwardSpeed = Mathf.Max(0f, backwardSpeed);
        crouchWalkSpeed = Mathf.Max(0f, crouchWalkSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        deceleration = Mathf.Max(0.1f, deceleration);
        turnSpeed = Mathf.Max(1f, turnSpeed);
        aimTurnSpeed = Mathf.Max(1f, aimTurnSpeed);
        jumpInputCooldown = Mathf.Max(0f, jumpInputCooldown);
        jumpGroundLockTime = Mathf.Max(0f, jumpGroundLockTime);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        fallGravityMultiplier = Mathf.Max(1f, fallGravityMultiplier);
        landingCarryDistance = Mathf.Max(0f, landingCarryDistance);
        landingCarryDuration = Mathf.Max(0.05f, landingCarryDuration);
        jumpForwardSpeedMultiplier = Mathf.Max(0f, jumpForwardSpeedMultiplier);
        landingCarryDistanceRatio = Mathf.Clamp01(landingCarryDistanceRatio);
        landingCarrySpeedMultiplier = Mathf.Max(0f, landingCarrySpeedMultiplier);
        landingNudgeDistance = Mathf.Max(0f, landingNudgeDistance);
        jumpControllerHeight = Mathf.Max(0.1f, jumpControllerHeight);
        jumpControllerRadius = Mathf.Clamp(jumpControllerRadius, 0.01f, jumpControllerHeight * 0.5f);
        jumpControllerCenterLift = Mathf.Max(0f, jumpControllerCenterLift);
        floatFallDelay = Mathf.Max(0f, floatFallDelay);
        floatFallVerticalSpeed = Mathf.Min(0f, floatFallVerticalSpeed);
        idleRollDuration = Mathf.Max(0.1f, idleRollDuration);
        sprintRollDuration = Mathf.Max(0.1f, sprintRollDuration);
        idleRollDistance = Mathf.Max(0f, idleRollDistance);
        sprintRollDistance = Mathf.Max(0f, sprintRollDistance);
        rollControllerHeight = Mathf.Max(0.1f, rollControllerHeight);
        rollControllerRadius = Mathf.Clamp(rollControllerRadius, 0.01f, rollControllerHeight * 0.5f);
        standingToCrouchDuration = Mathf.Max(0.1f, standingToCrouchDuration);
        crouchToStandingDuration = Mathf.Max(0.1f, crouchToStandingDuration);
        jumpDuration = Mathf.Max(0.1f, jumpDuration);
        pickUpDuration = Mathf.Max(0.1f, pickUpDuration);
        throwDuration = Mathf.Max(0.1f, throwDuration);
        idleSadDelay = Mathf.Max(0f, idleSadDelay);
        idleAlternateInterval = Mathf.Max(0.1f, idleAlternateInterval);
        battleRelaxDelay = Mathf.Max(0f, battleRelaxDelay);
        rootMotionScale = Mathf.Max(0f, rootMotionScale);
        rootMotionFallbackDelay = Mathf.Max(0f, rootMotionFallbackDelay);
        rootMotionFallbackThreshold = Mathf.Max(0f, rootMotionFallbackThreshold);
        groundSnapDistance = Mathf.Max(0f, groundSnapDistance);
        groundSnapProbeHeight = Mathf.Max(0.01f, groundSnapProbeHeight);
        groundContactOffset = Mathf.Clamp(groundContactOffset, 0f, 0.05f);
        enemyDetectionRadius = Mathf.Max(0f, enemyDetectionRadius);
        enemyProbeInterval = Mathf.Max(0.05f, enemyProbeInterval);
        actionExitNormalizedTime = Mathf.Clamp(actionExitNormalizedTime, 0.5f, 1.1f);
        minimumActionDuration = Mathf.Max(0f, minimumActionDuration);

        if (rollMotionCurve == null || rollMotionCurve.length == 0)
        {
            rollMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        if (jumpControllerCenterLiftCurve == null || jumpControllerCenterLiftCurve.length == 0)
        {
            jumpControllerCenterLiftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
        }

        SanitizeRigidbody();
    }

    private void OnDisable()
    {
        RestoreStandingControllerShape();
    }

    private void Update()
    {
        RefreshCameraFollow();
        ConfigureAnimator();
        ReadInput();
        UpdateEnemyProbe(Time.deltaTime);
        UpdateGroundedBeforeMove();

        if (activeOneShot)
        {
            UpdateActiveOneShot(Time.deltaTime);
        }
        else
        {
            EvaluateRequests();
        }

        UpdateJumpControllerCenterLift();
        UpdateFacing();
        MoveCharacter(Time.deltaTime);
        UpdateLandingCarry(Time.deltaTime);
        UpdateAirborneState(Time.deltaTime);
        UpdateIdleAndBattleTimers(Time.deltaTime);

        if (!activeOneShot)
        {
            EvaluateLocomotionState();
        }

        PlayState(currentState, false);
        UpdateAnimatorParameters();
    }

    private void LateUpdate()
    {
        if (ShouldPinAnimatorTransform())
        {
            PinAnimatorTransform();
        }
    }

    public void SetGameplayInputLocked(bool locked)
    {
        gameplayInputLocked = locked;
        if (!locked)
        {
            return;
        }

        moveInput = Vector2.zero;
        moveDirection = Vector3.zero;
        hasMoveInput = false;
        sprintHeld = false;
        aimHeld = false;
    }

    public void ApplyAnimatorRootMotion(Vector3 animatorDeltaPosition, Quaternion animatorDeltaRotation)
    {
        if (!enabled || animator == null || !useRootMotion)
        {
            return;
        }

        ApplyAnimatorRootRotationToVisual(animatorDeltaRotation);

        bool useAnimatorMotion = ShouldUseAnimatorRootMotion();
        if (!useAnimatorMotion || characterController == null)
        {
            return;
        }

        Vector3 delta = animatorDeltaPosition * rootMotionScale;
        if (IsRollState(currentState))
        {
            delta.y = 0f;
        }

        float thresholdSqr = rootMotionFallbackThreshold * rootMotionFallbackThreshold;
        if (delta.sqrMagnitude <= thresholdSqr)
        {
            return;
        }

        characterController.Move(delta);
        lastRootMotionTime = Time.time;

        Vector3 horizontalDelta = new Vector3(delta.x, 0f, delta.z);
        if (horizontalDelta.sqrMagnitude > thresholdSqr)
        {
            horizontalVelocity = horizontalDelta / Mathf.Max(Time.deltaTime, 0.0001f);
            lastHorizontalRootMotionTime = Time.time;
        }

        SnapCharacterToGround(IsRollState(currentState));
    }

    private void ApplyAnimatorRootRotationToVisual(Quaternion animatorDeltaRotation)
    {
        if (!applyAnimatorRootRotationToVisual || !ShouldUseAnimatorRootRotation())
        {
            return;
        }

        if (animatorDeltaRotation == Quaternion.identity)
        {
            return;
        }

        Transform target = animator.transform;
        if (target == transform)
        {
            return;
        }

        target.localRotation *= animatorDeltaRotation;
    }

    private bool ShouldPinAnimatorTransform()
    {
        if (!keepAnimatorTransformPinned || animator == null || animator.transform == transform)
        {
            return false;
        }

        // Action clips from Mixamo often use their root transform for the roll/jump/pickup pose.
        // Pinning the Armature during those clips strips that motion and can make the body deform.
        return !activeOneShot && !ShouldUseAnimatorRootMotion();
    }

    private void ReadInput()
    {
        if (IsGameplayInputBlocked())
        {
            moveInput = Vector2.zero;
            moveDirection = Vector3.zero;
            hasMoveInput = false;
            sprintHeld = false;
            aimHeld = false;
            return;
        }

        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        moveDirection = GetMoveDirection(moveInput);
        hasMoveInput = moveInput.sqrMagnitude > InputDeadZone * InputDeadZone && moveDirection.sqrMagnitude > 0.001f;
        sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        aimHeld = enableAimMode && Input.GetMouseButton(aimMouseButton);

        if (hasMoveInput)
        {
            landingCarryTimer = 0f;
        }
    }

    private void EvaluateRequests()
    {
        bool inputBlocked = IsGameplayInputBlocked();

        if (EnemyThreatActive && isCrouching)
        {
            crouchWanted = false;
            pendingAfterStand = PendingAfterStand.None;
            StartOneShot(PlayerState.CrouchToStanding, crouchToStandingDuration);
            return;
        }

        if (!inputBlocked && enableRoll && Input.GetKeyDown(rollKey))
        {
            if (isCrouching)
            {
                crouchWanted = false;
                StartStandThen(PendingAfterStand.Roll, false);
            }
            else
            {
                StartRoll();
            }

            return;
        }

        if (!inputBlocked && WantsToJump())
        {
            if (isCrouching)
            {
                crouchWanted = false;
                StartStandThen(PendingAfterStand.Jump, false);
            }
            else
            {
                StartJump();
            }

            return;
        }

        if (!inputBlocked && WantsToThrow())
        {
            if (isCrouching)
            {
                crouchWanted = true;
                StartStandThen(PendingAfterStand.Throw, true);
            }
            else
            {
                StartThrow(false);
            }

            return;
        }

        if (!inputBlocked && Input.GetKeyDown(pickUpKey) && CanPickUp)
        {
            if (isCrouching)
            {
                crouchWanted = true;
                StartStandThen(PendingAfterStand.PickUp, true);
            }
            else
            {
                StartPickUp();
            }

            return;
        }

        if (!inputBlocked && enableCrouch && (Input.GetKeyDown(crouchKey) || Input.GetKeyDown(alternateCrouchKey)))
        {
            ToggleCrouch();
            return;
        }

        if (!inputBlocked && enableCrouch && isCrouching && sprintHeld && hasMoveInput)
        {
            crouchWanted = false;
            pendingAfterStand = PendingAfterStand.None;
            StartOneShot(PlayerState.CrouchToStanding, crouchToStandingDuration);
            return;
        }

    }

    private void ToggleCrouch()
    {
        if (!enableCrouch)
        {
            return;
        }

        if (isCrouching)
        {
            crouchWanted = false;
            pendingAfterStand = PendingAfterStand.None;
            StartOneShot(PlayerState.CrouchToStanding, crouchToStandingDuration);
            return;
        }

        if (!EnemyThreatActive)
        {
            crouchWanted = true;
            StartOneShot(PlayerState.StandingToCrouch, standingToCrouchDuration);
        }
    }

    private void StartStandThen(PendingAfterStand pendingAction, bool returnToCrouch)
    {
        pendingAfterStand = pendingAction;
        returnToCrouchAfterAction = returnToCrouch;
        StartOneShot(PlayerState.CrouchToStanding, crouchToStandingDuration);
    }

    private void StartRoll()
    {
        bool fromMovement = hasMoveInput;
        bool fromSprint = sprintHeld && hasMoveInput;

        rollDirection = fromMovement ? moveDirection.normalized : GetFlatForward(transform);
        if (aimHeld && !fromMovement)
        {
            rollDirection = GetCameraForward();
        }

        previousRollCurveValue = 0f;
        horizontalVelocity = Vector3.zero;
        SnapFacing(rollDirection);
        StartOneShot(fromSprint || fromMovement ? PlayerState.SprintingToRoll : PlayerState.IdleToRoll, fromSprint || fromMovement ? sprintRollDuration : idleRollDuration);
    }

    private void StartJump()
    {
        if (jumpConsumedUntilGrounded || !jumpInputReleasedSinceJump)
        {
            return;
        }

        bool fromMovement = hasMoveInput;
        jumpStartedFromMovement = fromMovement;
        jumpCarryDirection = fromMovement ? moveDirection.normalized : GetFlatForward(transform);
        jumpHorizontalSpeed = CalculateJumpHorizontalSpeed(fromMovement);
        jumpExpectedAirTime = CalculateJumpAirTime();
        jumpLandingCarryDistance = CalculateLandingCarryDistance(jumpHorizontalSpeed, jumpExpectedAirTime);

        if (fromMovement)
        {
            SnapFacing(jumpCarryDirection);
        }

        verticalVelocity = CalculateJumpVelocity();
        lastJumpStartTime = Time.time;
        landingCarryTimer = 0f;
        landingCarrySpeed = 0f;
        jumpConsumedUntilGrounded = true;
        jumpInputReleasedSinceJump = false;
        jumpLeftGround = false;
        airborneTimer = 0f;
        StartOneShot(fromMovement ? PlayerState.Jump : PlayerState.IdleJump, jumpDuration);
    }

    private void StartPickUp()
    {
        StartOneShot(PlayerState.PickingUp, pickUpDuration);
    }

    private void StartThrow(bool returningToCrouch)
    {
        returnToCrouchAfterAction = returningToCrouch;
        SnapFacing(aimHeld ? GetCameraForward() : GetFlatForward(transform));
        StartOneShot(PlayerState.Throw, throwDuration);
    }

    private void StartOneShot(PlayerState state, float fallbackDuration)
    {
        activeOneShot = true;
        currentState = state;
        stateTimer = 0f;
        activeStateDuration = Mathf.Max(minimumActionDuration, fallbackDuration);
        idleTimer = 0f;
        lastRootMotionTime = Time.time;
        lastHorizontalRootMotionTime = Time.time;
        if (IsRollState(state))
        {
            SetRollControllerShape(true);
        }
        else if (IsJumpState(state))
        {
            SetJumpControllerShape(true);
        }

        if (ShouldLockHorizontalMotionForAction(state))
        {
            ClearActionMomentum();
        }
        PlayState(state, true);
    }

    private void ClearActionMomentum()
    {
        horizontalVelocity = Vector3.zero;
        landingCarryTimer = 0f;
    }

    private void UpdateActiveOneShot(float deltaTime)
    {
        stateTimer += deltaTime;
        activeStateDuration = ResolveCurrentActionDuration(currentState, activeStateDuration);

        if (!HasCurrentActionFinished())
        {
            return;
        }

        CompleteOneShot();
    }

    private bool HasCurrentActionFinished()
    {
        if (stateTimer < GetMinimumActionDuration(currentState))
        {
            return false;
        }

        if (IsJumpState(currentState))
        {
            return HasJumpActionFinished();
        }

        if (IsReversePlaybackState(currentState))
        {
            return stateTimer >= activeStateDuration;
        }

        AnimatorStateInfo stateInfo;
        bool isCurrentState;
        if (useAnimatorStateLengthForActions && TryGetAnimatorStateInfo(currentState, out stateInfo, out isCurrentState))
        {
            if (!isCurrentState || animator.IsInTransition(0))
            {
                return false;
            }

            if (!stateInfo.loop)
            {
                if (IsReversePlaybackState(currentState))
                {
                    return stateInfo.normalizedTime <= 1f - actionExitNormalizedTime;
                }

                return stateInfo.normalizedTime >= actionExitNormalizedTime;
            }
        }

        return stateTimer >= activeStateDuration;
    }

    private bool HasJumpActionFinished()
    {
        if (stateTimer < activeStateDuration)
        {
            return false;
        }

        if (stateTimer < Mathf.Max(jumpGroundLockTime, minimumActionDuration))
        {
            return false;
        }

        if (!jumpLeftGround && stateTimer < activeStateDuration + jumpGroundLockTime)
        {
            return false;
        }

        return grounded;
    }

    private void CompleteOneShot()
    {
        PlayerState completedState = currentState;
        activeOneShot = false;
        if (IsRollState(completedState) || IsJumpState(completedState))
        {
            RestoreStandingControllerShape();
        }

        if (ShouldRestoreAnimatorAnchorAfterAction(completedState))
        {
            PinAnimatorTransform();
        }

        if (completedState == PlayerState.StandingToCrouch)
        {
            isCrouching = true;
            crouchWanted = true;
        }
        else if (completedState == PlayerState.CrouchToStanding)
        {
            isCrouching = false;
            ConsumePendingAfterStand();
            return;
        }
        else if (completedState == PlayerState.Throw || completedState == PlayerState.PickingUp)
        {
            if (returnToCrouchAfterAction && crouchWanted && !EnemyThreatActive)
            {
                returnToCrouchAfterAction = false;
                StartOneShot(PlayerState.StandingToCrouch, standingToCrouchDuration);
                return;
            }

            returnToCrouchAfterAction = false;
        }
        else if (IsJumpState(completedState))
        {
            BeginLandingCarryIfNeeded();
        }

        EvaluateLocomotionState();
    }

    private void ConsumePendingAfterStand()
    {
        PendingAfterStand pending = pendingAfterStand;
        pendingAfterStand = PendingAfterStand.None;

        if (pending == PendingAfterStand.Roll)
        {
            StartRoll();
            return;
        }

        if (pending == PendingAfterStand.Jump)
        {
            StartJump();
            return;
        }

        if (pending == PendingAfterStand.PickUp)
        {
            StartPickUp();
            return;
        }

        if (pending == PendingAfterStand.Throw)
        {
            StartThrow(returnToCrouchAfterAction);
            return;
        }

        EvaluateLocomotionState();
    }

    private float CalculateJumpVelocity()
    {
        return Mathf.Sqrt(Mathf.Max(0f, jumpHeight * -2f * gravity));
    }

    private float CalculateJumpAirTime()
    {
        float gravityMagnitude = Mathf.Abs(gravity);
        if (gravityMagnitude <= 0.001f)
        {
            return 0f;
        }

        float jumpVelocity = CalculateJumpVelocity();
        float riseTime = jumpVelocity / gravityMagnitude;
        float fallTime = Mathf.Sqrt((2f * jumpHeight) / (gravityMagnitude * Mathf.Max(1f, fallGravityMultiplier)));
        return riseTime + fallTime;
    }

    private float CalculateJumpHorizontalSpeed(bool fromMovement)
    {
        if (!fromMovement)
        {
            return 0f;
        }

        float inputSpeed = sprintHeld ? sprintSpeed : slowRunSpeed;
        float currentSpeed = new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        return Mathf.Max(inputSpeed, currentSpeed) * jumpForwardSpeedMultiplier;
    }

    private float CalculateLandingCarryDistance(float launchSpeed, float airTime)
    {
        float calculatedDistance = launchSpeed * airTime * landingCarryDistanceRatio;
        float distance = Mathf.Max(calculatedDistance, landingNudgeDistance);
        return Mathf.Clamp(distance, 0f, landingCarryDistance);
    }

    private void BeginLandingCarryIfNeeded()
    {
        if (!grounded || jumpLandingCarryDistance <= 0f)
        {
            jumpStartedFromMovement = false;
            return;
        }

        // Keeps the last running step in the jump clip from visually snapping in place.
        landingCarryDirection = jumpCarryDirection.sqrMagnitude > 0.001f ? jumpCarryDirection.normalized : GetFlatForward(transform);
        landingCarrySpeed = Mathf.Max(slowRunSpeed * 0.5f, jumpHorizontalSpeed * landingCarrySpeedMultiplier);
        landingCarryTimer = Mathf.Min(landingCarryDuration, jumpLandingCarryDistance / Mathf.Max(landingCarrySpeed, 0.1f));
        jumpStartedFromMovement = false;
    }

    private void UpdateLandingCarry(float deltaTime)
    {
        if (landingCarryTimer <= 0f)
        {
            return;
        }

        if (hasMoveInput)
        {
            landingCarryTimer = 0f;
            return;
        }

        landingCarryTimer = Mathf.Max(0f, landingCarryTimer - deltaTime);
    }

    private void UpdateAirborneState(float deltaTime)
    {
        if (!IsJumpKeyHeld())
        {
            jumpInputReleasedSinceJump = true;
        }

        if (grounded)
        {
            airborneTimer = 0f;
            if (jumpConsumedUntilGrounded
                && !activeOneShot
                && jumpInputReleasedSinceJump
                && Time.time - lastJumpStartTime >= jumpInputCooldown)
            {
                jumpConsumedUntilGrounded = false;
                jumpLeftGround = false;
            }

            return;
        }

        if (jumpConsumedUntilGrounded)
        {
            jumpLeftGround = true;
        }

        airborneTimer += deltaTime;
    }

    private bool HasLandingCarry
    {
        get { return landingCarryTimer > 0f && !hasMoveInput && grounded; }
    }

    private void EvaluateLocomotionState()
    {
        if (activeOneShot)
        {
            return;
        }

        if (ShouldUseFloatingState())
        {
            currentState = PlayerState.Floating;
            return;
        }

        if (isCrouching)
        {
            currentState = hasMoveInput ? PlayerState.CrouchWalking : PlayerState.CrouchIdle;
            return;
        }

        if (HasLandingCarry)
        {
            currentState = PlayerState.SlowRun;
            return;
        }

        if (hasMoveInput)
        {
            if (aimHeld && moveInput.y < -0.1f)
            {
                currentState = PlayerState.RunningBackward;
                return;
            }

            currentState = sprintHeld ? PlayerState.Sprint : PlayerState.SlowRun;
            return;
        }

        if (ShouldUseBattleIdle)
        {
            currentState = PlayerState.IdleBattle;
            return;
        }

        if (isInSafeArea && idleTimer >= idleSadDelay)
        {
            currentState = GetIdleVariationState();
            return;
        }

        currentState = PlayerState.IdleNeutral;
    }

    private PlayerState GetIdleVariationState()
    {
        if (animator == null)
        {
            return PlayerState.IdleSad;
        }

        bool hasSadIdle = HasAnimatorState(idleSadState);
        bool hasHappyIdle = HasAnimatorState(idleHappyState);

        if (!hasHappyIdle)
        {
            return PlayerState.IdleSad;
        }

        if (!hasSadIdle)
        {
            return PlayerState.IdleHappy;
        }

        float elapsedAfterDelay = Mathf.Max(0f, idleTimer - idleSadDelay);
        int variationIndex = Mathf.FloorToInt(elapsedAfterDelay / idleAlternateInterval);
        return variationIndex % 2 == 0 ? PlayerState.IdleSad : PlayerState.IdleHappy;
    }

    private bool ShouldUseFloatingState()
    {
        return !grounded && airborneTimer >= floatFallDelay && verticalVelocity <= floatFallVerticalSpeed;
    }

    private void MoveCharacter(float deltaTime)
    {
        if (characterController == null)
        {
            return;
        }

        deltaTime = Mathf.Max(deltaTime, 0.0001f);
        if (ShouldUseAnimatorRootMotion() && !ShouldFallbackToCodeMotion())
        {
            TrackRollFallbackProgress();
            grounded = characterController.isGrounded;
            if (grounded && verticalVelocity < 0f)
            {
                verticalVelocity = groundedStickForce;
            }

            return;
        }

        Vector3 horizontalDelta = BuildHorizontalDelta(deltaTime);

        verticalVelocity += GetCurrentGravity() * deltaTime;
        Vector3 motion = horizontalDelta;
        motion.y = verticalVelocity * deltaTime;

        CollisionFlags flags = characterController.Move(motion);
        grounded = characterController.isGrounded || (flags & CollisionFlags.Below) != 0;
        SnapCharacterToGround(ShouldSnapToGroundForCurrentState());

        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }

        horizontalVelocity = horizontalDelta / deltaTime;
    }

    private float GetCurrentGravity()
    {
        // Stronger falling gravity keeps the collider matched to snappy jump clips.
        return verticalVelocity < 0f ? gravity * fallGravityMultiplier : gravity;
    }

    private Vector3 BuildHorizontalDelta(float deltaTime)
    {
        if (activeOneShot && ShouldLockHorizontalMotionForAction(currentState))
        {
            horizontalVelocity = Vector3.zero;
            return Vector3.zero;
        }

        if (ShouldUseAnimatorRootMotion() && !ShouldFallbackToCodeMotion())
        {
            TrackRollFallbackProgress();
            return Vector3.zero;
        }

        if (currentState == PlayerState.IdleToRoll || currentState == PlayerState.SprintingToRoll)
        {
            return BuildRollDelta();
        }

        float targetSpeed = GetTargetSpeedForState();
        Vector3 targetVelocity = targetSpeed > 0f ? GetTargetMoveDirection() * targetSpeed : Vector3.zero;
        // Sprint only changes speed; direction changes stay responsive.
        float rate = targetSpeed > 0f ? acceleration : deceleration;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, rate * deltaTime);
        return horizontalVelocity * deltaTime;
    }

    private bool ShouldUseAnimatorRootMotion()
    {
        if (!useRootMotion || animator == null)
        {
            return false;
        }

        if (activeOneShot && ShouldLockHorizontalMotionForAction(currentState))
        {
            return false;
        }

        if ((currentState == PlayerState.IdleToRoll || currentState == PlayerState.SprintingToRoll) && rootMotionDrivesRoll)
        {
            return true;
        }

        if (IsJumpState(currentState) && rootMotionDrivesJump)
        {
            return true;
        }

        if (!rootMotionDrivesLocomotion)
        {
            return false;
        }

        if (codeDrivesInPlaceLocomotion && IsLoopingLocomotionState(currentState))
        {
            return false;
        }

        return currentState == PlayerState.SlowRun
            || currentState == PlayerState.Sprint
            || currentState == PlayerState.RunningBackward
            || currentState == PlayerState.CrouchWalking;
    }

    private bool ShouldUseAnimatorRootRotation()
    {
        if (!useRootMotion || animator == null || !activeOneShot)
        {
            return false;
        }

        return ShouldRestoreAnimatorAnchorAfterAction(currentState);
    }

    private static bool ShouldRestoreAnimatorAnchorAfterAction(PlayerState state)
    {
        return state == PlayerState.IdleToRoll
            || state == PlayerState.SprintingToRoll
            || IsJumpState(state)
            || state == PlayerState.PickingUp
            || state == PlayerState.Throw;
    }

    private static bool IsReversePlaybackState(PlayerState state)
    {
        return false;
    }

    private static bool IsCrouchTransitionState(PlayerState state)
    {
        return state == PlayerState.StandingToCrouch || state == PlayerState.CrouchToStanding;
    }

    private float GetMinimumActionDuration(PlayerState state)
    {
        if (IsCrouchTransitionState(state))
        {
            return Mathf.Max(0.35f, minimumActionDuration);
        }

        return minimumActionDuration;
    }

    private static bool IsRollState(PlayerState state)
    {
        return state == PlayerState.IdleToRoll || state == PlayerState.SprintingToRoll;
    }

    private static bool IsJumpState(PlayerState state)
    {
        return state == PlayerState.IdleJump || state == PlayerState.Jump;
    }

    private static bool ShouldLockHorizontalMotionForAction(PlayerState state)
    {
        return !IsJumpState(state) && !IsRollState(state);
    }

    private void CaptureStandingControllerShape()
    {
        if (characterController == null || standingControllerShapeCaptured || controllerSizedForRoll || controllerSizedForJump)
        {
            return;
        }

        standingControllerHeight = characterController.height;
        standingControllerRadius = characterController.radius;
        standingControllerCenter = characterController.center;
        standingControllerShapeCaptured = true;
    }

    private void SetRollControllerShape(bool rolling)
    {
        if (!resizeControllerDuringRoll || characterController == null)
        {
            return;
        }

        CaptureStandingControllerShape();
        if (!standingControllerShapeCaptured)
        {
            return;
        }

        if (rolling)
        {
            if (controllerSizedForRoll)
            {
                return;
            }

            ApplyControllerShape(rollControllerHeight, rollControllerRadius, rollControllerCenter);
            controllerSizedForRoll = true;
            SnapCharacterToGround(true);
            return;
        }

        RestoreStandingControllerShape();
    }

    private void SetJumpControllerShape(bool jumping)
    {
        if (!resizeControllerDuringJump || characterController == null)
        {
            return;
        }

        CaptureStandingControllerShape();
        if (!standingControllerShapeCaptured)
        {
            return;
        }

        if (jumping)
        {
            if (controllerSizedForJump)
            {
                return;
            }

            ApplyControllerShape(jumpControllerHeight, jumpControllerRadius, jumpControllerCenter);
            controllerSizedForJump = true;
            return;
        }

        RestoreStandingControllerShape();
    }

    private void UpdateJumpControllerCenterLift()
    {
        if (!liftControllerCenterDuringJump || characterController == null || !IsJumpState(currentState))
        {
            return;
        }

        CaptureStandingControllerShape();
        if (!standingControllerShapeCaptured)
        {
            return;
        }

        float duration = Mathf.Max(activeStateDuration, minimumActionDuration, 0.0001f);
        float normalizedTime = Mathf.Clamp01(stateTimer / duration);
        float liftWeight = jumpControllerCenterLiftCurve != null && jumpControllerCenterLiftCurve.length > 0
            ? Mathf.Max(0f, jumpControllerCenterLiftCurve.Evaluate(normalizedTime))
            : 0f;

        float height = resizeControllerDuringJump ? jumpControllerHeight : standingControllerHeight;
        float radius = resizeControllerDuringJump ? jumpControllerRadius : standingControllerRadius;
        Vector3 center = resizeControllerDuringJump ? jumpControllerCenter : standingControllerCenter;
        center.y += jumpControllerCenterLift * liftWeight;

        ApplyControllerShape(height, radius, center);
        controllerSizedForJump = true;
    }

    private void RestoreStandingControllerShape()
    {
        if ((!controllerSizedForRoll && !controllerSizedForJump) || characterController == null || !standingControllerShapeCaptured)
        {
            return;
        }

        ApplyControllerShape(standingControllerHeight, standingControllerRadius, standingControllerCenter);
        controllerSizedForRoll = false;
        controllerSizedForJump = false;
        SnapCharacterToGround(true);
    }

    private void ApplyControllerShape(float height, float radius, Vector3 center)
    {
        if (characterController == null)
        {
            return;
        }

        float safeHeight = Mathf.Max(0.1f, height);
        float safeRadius = Mathf.Clamp(radius, 0.01f, safeHeight * 0.5f);
        characterController.height = safeHeight;
        characterController.radius = safeRadius;
        characterController.center = center;
    }

    private bool IsLoopingLocomotionState(PlayerState state)
    {
        return state == PlayerState.SlowRun
            || state == PlayerState.Sprint
            || state == PlayerState.RunningBackward
            || state == PlayerState.Floating
            || state == PlayerState.CrouchWalking;
    }

    private bool ShouldSnapToGroundForCurrentState()
    {
        return !IsJumpState(currentState);
    }

    private bool ShouldFallbackToCodeMotion()
    {
        if (!fallbackToCodeMotionWhenRootMotionIsSmall)
        {
            return false;
        }

        float lastMotionTime = UsesHorizontalRootMotionForCurrentState() ? lastHorizontalRootMotionTime : lastRootMotionTime;
        return Time.time - lastMotionTime > rootMotionFallbackDelay;
    }

    private bool UsesHorizontalRootMotionForCurrentState()
    {
        return (currentState == PlayerState.IdleToRoll && rootMotionDrivesRoll)
            || (currentState == PlayerState.SprintingToRoll && rootMotionDrivesRoll)
            || (IsJumpState(currentState) && rootMotionDrivesJump)
            || (rootMotionDrivesLocomotion && !codeDrivesInPlaceLocomotion && !activeOneShot);
    }

    private void TrackRollFallbackProgress()
    {
        if (currentState != PlayerState.IdleToRoll && currentState != PlayerState.SprintingToRoll)
        {
            return;
        }

        float normalizedTime = Mathf.Clamp01(stateTimer / Mathf.Max(activeStateDuration, 0.0001f));
        float curveValue = rollMotionCurve != null && rollMotionCurve.length > 0
            ? Mathf.Clamp01(rollMotionCurve.Evaluate(normalizedTime))
            : normalizedTime;
        previousRollCurveValue = Mathf.Max(previousRollCurveValue, curveValue);
    }

    private Vector3 BuildRollDelta()
    {
        float normalizedTime = Mathf.Clamp01(stateTimer / Mathf.Max(activeStateDuration, 0.0001f));
        float curveValue = rollMotionCurve != null && rollMotionCurve.length > 0
            ? Mathf.Clamp01(rollMotionCurve.Evaluate(normalizedTime))
            : normalizedTime;
        float rollDistance = currentState == PlayerState.SprintingToRoll ? sprintRollDistance : idleRollDistance;
        float deltaDistance = Mathf.Max(0f, curveValue - previousRollCurveValue) * rollDistance;
        previousRollCurveValue = curveValue;
        return rollDirection * deltaDistance;
    }

    private float GetTargetSpeedForState()
    {
        if (HasLandingCarry)
        {
            return Mathf.Max(landingCarrySpeed, 0f);
        }

        if (activeOneShot && ShouldLockHorizontalMotionForAction(currentState))
        {
            return 0f;
        }

        if (currentState == PlayerState.CrouchWalking)
        {
            return crouchWalkSpeed;
        }

        if (currentState == PlayerState.RunningBackward)
        {
            return backwardSpeed;
        }

        if (currentState == PlayerState.Sprint)
        {
            return sprintSpeed;
        }

        if (currentState == PlayerState.Floating)
        {
            return hasMoveInput ? slowRunSpeed : new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        }

        if (currentState == PlayerState.SlowRun || IsJumpState(currentState))
        {
            if (IsJumpState(currentState) && jumpStartedFromMovement && jumpHorizontalSpeed > 0f)
            {
                return jumpHorizontalSpeed;
            }

            return hasMoveInput ? (sprintHeld ? sprintSpeed : slowRunSpeed) : 0f;
        }

        return 0f;
    }

    private Vector3 GetTargetMoveDirection()
    {
        if (HasLandingCarry)
        {
            return landingCarryDirection.sqrMagnitude > 0.001f ? landingCarryDirection.normalized : GetFlatForward(transform);
        }

        if (IsJumpState(currentState) && jumpStartedFromMovement && jumpCarryDirection.sqrMagnitude > 0.001f)
        {
            return jumpCarryDirection.normalized;
        }

        if (currentState == PlayerState.Floating && !hasMoveInput)
        {
            Vector3 velocityDirection = horizontalVelocity;
            velocityDirection.y = 0f;
            if (velocityDirection.sqrMagnitude > 0.001f)
            {
                return velocityDirection.normalized;
            }
        }

        if (hasMoveInput)
        {
            return moveDirection.normalized;
        }

        return Vector3.zero;
    }

    private void UpdateFacing()
    {
        Vector3 lockedTargetDirection;
        if (TryGetLockedTargetDirection(out lockedTargetDirection))
        {
            RotateToward(lockedTargetDirection);
            return;
        }

        if (ShouldLockFacingForCurrentAction())
        {
            return;
        }

        Vector3 facingDirection;

        if (currentState == PlayerState.IdleToRoll || currentState == PlayerState.SprintingToRoll)
        {
            facingDirection = rollDirection;
        }
        else if (aimHeld)
        {
            facingDirection = GetCameraForward();
        }
        else if (hasMoveInput)
        {
            facingDirection = GetLocomotionFacingDirection();
        }
        else
        {
            facingDirection = GetFlatForward(transform);
        }

        RotateToward(facingDirection);
    }

    private bool TryGetLockedTargetDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        if (!faceLockedEnemy)
        {
            return false;
        }

        if (cameraFollow == null || !cameraFollow.TryGetLockedTargetPosition(out Vector3 targetPosition))
        {
            return false;
        }

        direction = targetPosition - transform.position;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f;
    }

    private bool ShouldLockFacingForCurrentAction()
    {
        return activeOneShot && (currentState == PlayerState.PickingUp || currentState == PlayerState.Throw);
    }

    private Vector3 GetLocomotionFacingDirection()
    {
        Vector3 velocityDirection = horizontalVelocity;
        velocityDirection.y = 0f;

        if (velocityDirection.sqrMagnitude > 0.04f)
        {
            return velocityDirection.normalized;
        }

        return moveDirection;
    }

    private void UpdateGroundedBeforeMove()
    {
        grounded = characterController != null && characterController.isGrounded;
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }
    }

    private void UpdateIdleAndBattleTimers(float deltaTime)
    {
        if (!activeOneShot && grounded && !hasMoveInput && !isCrouching && !ShouldUseBattleIdle)
        {
            idleTimer += deltaTime;
        }
        else
        {
            idleTimer = 0f;
        }

        if (EnemyThreatActive)
        {
            battleRelaxTimer = 0f;
        }
        else
        {
            battleRelaxTimer += deltaTime;
        }
    }

    private bool WantsToJump()
    {
        if (!enableJump || IsGameplayInputBlocked())
        {
            return false;
        }

        if (activeOneShot || !grounded || jumpConsumedUntilGrounded || Time.time - lastJumpStartTime < jumpInputCooldown)
        {
            return false;
        }

        return jumpKey != KeyCode.None && Input.GetKeyDown(jumpKey);
    }

    private bool IsJumpKeyHeld()
    {
        if (!enableJump || IsGameplayInputBlocked())
        {
            return false;
        }

        return jumpKey != KeyCode.None && Input.GetKey(jumpKey);
    }

    private bool WantsToThrow()
    {
        if (IsGameplayInputBlocked())
        {
            return false;
        }

        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        return rightMouseButtonThrows && Input.GetMouseButtonDown(aimMouseButton);
    }

    private bool IsGameplayInputBlocked()
    {
        return gameplayInputLocked || Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked;
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 direction;

        if (cameraRelativeMovement && cameraTransform != null)
        {
            Vector3 forward = GetCameraForward();
            direction = forward * input.y + GetRightFromForward(forward) * input.x;
        }
        else
        {
            direction = GetFlatForward(transform) * input.y + GetFlatRight(transform) * input.x;
        }

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, GetTurnSpeed() * Time.deltaTime);
    }

    private float GetTurnSpeed()
    {
        return aimHeld ? aimTurnSpeed : turnSpeed;
    }

    private void SnapFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private Vector3 GetCameraForward()
    {
        if (cameraFollow != null)
        {
            return cameraFollow.PlanarForward;
        }

        if (cameraTransform != null)
        {
            return GetFlatForward(cameraTransform);
        }

        return GetFlatForward(transform);
    }

    private Vector3 GetCameraRight()
    {
        Vector3 forward = GetCameraForward();
        return GetRightFromForward(forward);
    }

    private Vector3 GetRightFromForward(Vector3 forward)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
    }

    private Vector3 GetFlatForward(Transform source)
    {
        Vector3 forward = source.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
    }

    private Vector3 GetFlatRight(Transform source)
    {
        Vector3 right = source.right;
        right.y = 0f;
        return right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
    }

    private void UpdateEnemyProbe(float deltaTime)
    {
        if (!autoDetectEnemies)
        {
            autoEnemyNearby = false;
            return;
        }

        enemyProbeTimer -= deltaTime;
        if (enemyProbeTimer > 0f)
        {
            return;
        }

        enemyProbeTimer = enemyProbeInterval;
        autoEnemyNearby = false;

        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, enemyDetectionRadius, enemyProbeHits, enemyLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = enemyProbeHits[i];
            if (hit == null || hit.transform.root == transform.root)
            {
                continue;
            }

            string hitTag = hit.gameObject.tag;
            if (TagMatches(hitTag, friendlyTags))
            {
                continue;
            }

            if (TagMatches(hitTag, enemyTags))
            {
                autoEnemyNearby = true;
                return;
            }
        }
    }

    private bool TagMatches(string tag, string[] tags)
    {
        if (string.IsNullOrEmpty(tag) || tags == null)
        {
            return false;
        }

        for (int i = 0; i < tags.Length; i++)
        {
            if (!string.IsNullOrEmpty(tags[i]) && tag == tags[i])
            {
                return true;
            }
        }

        return false;
    }

    private float ResolveCurrentActionDuration(PlayerState state, float fallbackDuration)
    {
        float minimumDuration = GetMinimumActionDuration(state);
        float duration = Mathf.Max(minimumDuration, fallbackDuration);

        if (IsJumpState(state) && !useAnimatorStateLengthForJump)
        {
            return duration;
        }

        AnimatorStateInfo stateInfo;
        bool isCurrentState;
        if (useAnimatorStateLengthForActions && TryGetAnimatorStateInfo(state, out stateInfo, out isCurrentState) && stateInfo.length > minimumDuration)
        {
            duration = Mathf.Max(duration, stateInfo.length);
        }

        return duration;
    }

    private bool TryGetAnimatorStateInfo(PlayerState state, out AnimatorStateInfo stateInfo, out bool isCurrentState)
    {
        stateInfo = default;
        isCurrentState = false;

        if (animator == null)
        {
            return false;
        }

        string stateName = GetPlayableStateName(state);
        if (string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int shortHash = Animator.StringToHash(stateName);
        int fullPathHash = Animator.StringToHash("Base Layer." + stateName);

        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextStateInfo = animator.GetNextAnimatorStateInfo(0);
            if (StateInfoMatches(nextStateInfo, shortHash, fullPathHash))
            {
                stateInfo = nextStateInfo;
                return true;
            }
        }

        AnimatorStateInfo currentStateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (StateInfoMatches(currentStateInfo, shortHash, fullPathHash))
        {
            stateInfo = currentStateInfo;
            isCurrentState = true;
            return true;
        }

        return false;
    }

    private void PlayState(PlayerState state, bool forceRestart)
    {
        if (animator == null)
        {
            return;
        }

        string playableStateName = GetPlayableStateName(state);
        if (string.IsNullOrWhiteSpace(playableStateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(playableStateName);
        if (!forceRestart && currentStateHash == stateHash)
        {
            return;
        }

        float fadeTime = activeOneShot ? GetActionFadeTime(state) : locomotionFadeTime;
        if (IsReversePlaybackState(state))
        {
            animator.CrossFade(playableStateName, fadeTime, 0, 0.999f);
        }
        else
        {
            animator.CrossFadeInFixedTime(playableStateName, fadeTime, 0, 0f);
        }

        currentStateHash = stateHash;
    }

    private float GetActionFadeTime(PlayerState state)
    {
        return IsCrouchTransitionState(state) ? Mathf.Max(actionFadeTime, 0.08f) : actionFadeTime;
    }
    private string GetPlayableStateName(PlayerState state)
    {
        string stateName = GetStateName(state);
        if (animator == null || HasAnimatorState(stateName))
        {
            return stateName;
        }

        string[] fallbacks;
        if (!stateFallbacks.TryGetValue(stateName, out fallbacks))
        {
            return stateName;
        }

        for (int i = 0; i < fallbacks.Length; i++)
        {
            if (HasAnimatorState(fallbacks[i]))
            {
                return fallbacks[i];
            }
        }

        return stateName;
    }

    private bool StateInfoMatches(AnimatorStateInfo stateInfo, int shortHash, int fullPathHash)
    {
        return stateInfo.shortNameHash == shortHash || stateInfo.fullPathHash == fullPathHash;
    }

    private bool HasAnimatorState(string stateName)
    {
        return animator.HasState(0, Animator.StringToHash(stateName))
            || animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
    }

    private string GetStateName(PlayerState state)
    {
        switch (state)
        {
            case PlayerState.IdleSad:
                return idleSadState;
            case PlayerState.IdleHappy:
                return idleHappyState;
            case PlayerState.IdleBattle:
                return idleBattleState;
            case PlayerState.SlowRun:
                return slowRunState;
            case PlayerState.Sprint:
                return sprintState;
            case PlayerState.StandingToCrouch:
                return standingToCrouchState;
            case PlayerState.CrouchIdle:
                return crouchIdleState;
            case PlayerState.CrouchWalking:
                return crouchWalkingState;
            case PlayerState.CrouchToStanding:
                return crouchToStandingState;
            case PlayerState.IdleToRoll:
                return idleToRollState;
            case PlayerState.SprintingToRoll:
                return sprintingToRollState;
            case PlayerState.RunningBackward:
                return runningBackwardState;
            case PlayerState.Floating:
                return floatingState;
            case PlayerState.IdleJump:
                return idleJumpState;
            case PlayerState.Jump:
                return jumpState;
            case PlayerState.PickingUp:
                return pickingUpState;
            case PlayerState.Throw:
                return throwState;
            default:
                return idleNeutralState;
        }
    }

    private void BuildStateFallbacks()
    {
        stateFallbacks.Clear();
        stateFallbacks[idleNeutralState] = new[] { "Idle", "Neutral Idle" };
        stateFallbacks[idleSadState] = new[] { "IdleChill", "Sad Idle" };
        stateFallbacks[idleHappyState] = new[] { idleSadState, "Happy Idle", "IdleChill", idleNeutralState, "Idle" };
        stateFallbacks[idleBattleState] = new[] { "Idle", "IdleBattle" };
        stateFallbacks[sprintState] = new[] { "FastRun", "Fast Run" };
        stateFallbacks[sprintingToRollState] = new[] { "RunToRolling", "Run To Rolling" };
        // Falling should never fall back to Jump, or it can look like a second jump.
        stateFallbacks[floatingState] = new[] { idleNeutralState };
        stateFallbacks[idleJumpState] = new[] { jumpState, "Jump" };
        stateFallbacks[crouchIdleState] = new[] { idleNeutralState, "Idle" };
        stateFallbacks[pickingUpState] = new[] { idleNeutralState, "Idle" };
    }

    private void UpdateAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        float planarSpeed = new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude;
        float normalizedSpeed = Mathf.InverseLerp(0f, sprintSpeed, planarSpeed);
        bool rolling = currentState == PlayerState.IdleToRoll || currentState == PlayerState.SprintingToRoll;
        bool jumping = IsJumpState(currentState) || !grounded;
        bool pickingUp = currentState == PlayerState.PickingUp;
        bool throwing = currentState == PlayerState.Throw;
        bool sprinting = currentState == PlayerState.Sprint;

        SetAnimatorFloat(speedParameter, normalizedSpeed);
        SetAnimatorFloat(moveXParameter, moveInput.x);
        SetAnimatorFloat(moveYParameter, moveInput.y);
        SetAnimatorBool(groundedParameter, grounded);
        SetAnimatorFloat(verticalSpeedParameter, verticalVelocity);
        SetAnimatorBool(isMovingParameter, hasMoveInput);
        SetAnimatorBool(isSprintingParameter, sprinting);
        SetAnimatorBool(isCrouchingParameter, isCrouching || currentState == PlayerState.CrouchIdle || currentState == PlayerState.CrouchWalking);
        SetAnimatorBool(isEnemyNearbyParameter, EnemyThreatActive);
        SetAnimatorBool(isRollingParameter, rolling);
        SetAnimatorBool(isJumpingParameter, jumping);
        SetAnimatorBool(isPickingUpParameter, pickingUp);
        SetAnimatorBool(isThrowingParameter, throwing);
        SetAnimatorFloat(idleTimerParameter, idleTimer);

        SetAnimatorBool("FastRun", sprinting);
        SetAnimatorBool("Backward", currentState == PlayerState.RunningBackward);
        SetAnimatorBool("Rolling", rolling);
        SetAnimatorBool("Jumping", jumping);
        SetAnimatorBool("Throwing", throwing);
    }

    private void RefreshCameraFollow()
    {
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraFollow != null && !cameraFollow.isActiveAndEnabled)
        {
            cameraFollow = null;
        }

        if (cameraTransform != null && cameraFollow == null)
        {
            cameraFollow = cameraTransform.GetComponent<BasicCameraFollow>();
        }
    }

    public void SetPlayerAnimator(Animator newAnimator, bool restartCurrentState = true)
    {
        if (animator == newAnimator)
        {
            if (animator != null)
            {
                animator.applyRootMotion = useRootMotion;
                CacheAnimatorParameters();
                if (restartCurrentState)
                {
                    currentStateHash = 0;
                    PlayState(currentState, true);
                }
            }
            return;
        }

        if (animator != null)
        {
            AnimatorRootMotionRelay oldRelay = animator.GetComponent<AnimatorRootMotionRelay>();
            if (oldRelay != null && oldRelay.movement == this)
            {
                oldRelay.movement = null;
            }
        }

        animator = newAnimator;
        animatorTransform = null;
        animatorLocalPosition = Vector3.zero;
        animatorLocalRotation = Quaternion.identity;
        currentStateHash = 0;

        ConfigureAnimator();
        CacheAnimatorParameters();
        CaptureAnimatorAnchor();

        if (restartCurrentState)
        {
            PlayState(currentState, true);
            UpdateAnimatorParameters();
        }
    }

    public void SetAvailableActions(bool jump, bool crouch, bool roll)
    {
        enableJump = jump;
        enableCrouch = crouch;
        enableRoll = roll;

        if (!enableCrouch && isCrouching)
        {
            isCrouching = false;
            crouchWanted = false;
            pendingAfterStand = PendingAfterStand.None;
            returnToCrouchAfterAction = false;

            if (currentState == PlayerState.CrouchIdle
                || currentState == PlayerState.CrouchWalking
                || IsCrouchTransitionState(currentState))
            {
                activeOneShot = false;
                RestoreStandingControllerShape();
                currentState = PlayerState.IdleNeutral;
                currentStateHash = 0;
                EvaluateLocomotionState();
                PlayState(currentState, true);
            }
        }
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            configuredAnimator = null;
            animatorConfigurationInitialized = false;
            return;
        }

        if (animatorConfigurationInitialized
            && configuredAnimator == animator
            && configuredRootMotion == useRootMotion)
        {
            return;
        }

        if (configuredAnimator != null && configuredAnimator != animator)
        {
            AnimatorRootMotionRelay previousRelay = configuredAnimator.GetComponent<AnimatorRootMotionRelay>();
            if (previousRelay != null && previousRelay.movement == this)
            {
                previousRelay.movement = null;
            }
        }

        animator.applyRootMotion = useRootMotion;
        AnimatorRootMotionRelay relay = animator.GetComponent<AnimatorRootMotionRelay>();

        if (!useRootMotion)
        {
            if (relay != null)
            {
                relay.movement = null;
            }

            configuredAnimator = animator;
            configuredRootMotion = false;
            animatorConfigurationInitialized = true;
            return;
        }

        if (relay == null)
        {
            relay = animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
        }

        relay.movement = this;
        configuredAnimator = animator;
        configuredRootMotion = true;
        animatorConfigurationInitialized = true;
    }

    private void SanitizeRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            return;
        }

        body.useGravity = false;
        body.isKinematic = true;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    private void SnapCharacterToGround(bool allowSnapWhileAirborne)
    {
        if (!snapFeetToGround || characterController == null)
        {
            return;
        }

        if (!allowSnapWhileAirborne && !grounded)
        {
            return;
        }

        float bottomOffset = characterController.center.y - characterController.height * 0.5f;
        Vector3 bottom = transform.position + Vector3.up * bottomOffset;
        Vector3 origin = bottom + Vector3.up * groundSnapProbeHeight;
        float contactOffset = Mathf.Max(0f, groundContactOffset);
        float castPadding = Mathf.Max(characterController.skinWidth, contactOffset);
        float maxDistance = groundSnapProbeHeight + groundSnapDistance + castPadding;
        int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, maxDistance, groundLayers, QueryTriggerInteraction.Ignore);

        float bestDistance = float.PositiveInfinity;
        RaycastHit bestHit = default;
        bool foundGround = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || hit.collider.transform.root == transform.root)
            {
                continue;
            }

            if (hit.distance < bestDistance)
            {
                bestDistance = hit.distance;
                bestHit = hit;
                foundGround = true;
            }
        }

        if (!foundGround)
        {
            return;
        }

        float desiredY = bestHit.point.y - bottomOffset + contactOffset;
        float deltaY = desiredY - transform.position.y;
        if (deltaY >= -0.001f || Mathf.Abs(deltaY) > groundSnapDistance + castPadding)
        {
            return;
        }

        CollisionFlags flags = characterController.Move(Vector3.up * deltaY);
        grounded = characterController.isGrounded || (flags & CollisionFlags.Below) != 0 || Mathf.Abs(deltaY) > 0.001f;
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }
    }

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (animator == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            animatorParameters.Add(parameter.nameHash);
        }
    }

    private void CaptureAnimatorAnchor()
    {
        if (animator == null || animator.transform == transform)
        {
            return;
        }

        animatorTransform = animator.transform;
        animatorLocalPosition = animatorTransform.localPosition;
        animatorLocalRotation = animatorTransform.localRotation;
    }

    private void PinAnimatorTransform()
    {
        if (animator == null || animator.transform == transform)
        {
            return;
        }

        if (animatorTransform != animator.transform)
        {
            CaptureAnimatorAnchor();
        }

        animatorTransform.localPosition = animatorLocalPosition;
        animatorTransform.localRotation = animatorLocalRotation;
    }

    private Animator FindBestAnimator()
    {
        Transform armature = FindChildRecursive(transform, "Armature");
        if (armature != null)
        {
            Animator armatureAnimator = armature.GetComponent<Animator>();
            if (armatureAnimator != null)
            {
                return armatureAnimator;
            }
        }

        Animator rootAnimator = GetComponent<Animator>();
        if (rootAnimator != null)
        {
            return rootAnimator;
        }

        return GetComponentInChildren<Animator>();
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform match = FindChildRecursive(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private bool HasAnimatorParameter(string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        int hash = Animator.StringToHash(parameterName);
        if (animatorParameters.Count == 0)
        {
            CacheAnimatorParameters();
        }

        return animatorParameters.Contains(hash);
    }

    private void SetAnimatorFloat(string parameterName, float value)
    {
        if (HasAnimatorParameter(parameterName))
        {
            animator.SetFloat(parameterName, value, 0.1f, Time.deltaTime);
        }
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (HasAnimatorParameter(parameterName))
        {
            animator.SetBool(parameterName, value);
        }
    }
}

