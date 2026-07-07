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
        IdleBattle,
        SlowRun,
        Sprint,
        StandingToCrouch,
        CrouchIdle,
        CrouchWalking,
        CrouchToStanding,
        CrouchToSprint,
        IdleToRoll,
        SprintingToRoll,
        RunningBackward,
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
    public float sprintTurnSpeed = 420f;
    public float aimTurnSpeed = 900f;
    public bool cameraRelativeMovement = true;

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
    public float jumpHeight = 1.35f;
    public float gravity = -25f;
    public float groundedStickForce = -2f;

    [Header("Grounding")]
    public bool snapFeetToGround = true;
    public LayerMask groundLayers = ~0;
    public float groundSnapDistance = 0.45f;
    public float groundSnapProbeHeight = 0.35f;
    public float groundContactOffset = 0f;

    [Header("Roll")]
    public KeyCode rollKey = KeyCode.Q;
    public float idleRollDuration = 2.15f;
    public float sprintRollDuration = 1.35f;
    public float idleRollDistance = 2.6f;
    public float sprintRollDistance = 3.7f;
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
    public float crouchToSprintDuration = 0.65f;

    [Header("Actions")]
    public KeyCode pickUpKey = KeyCode.E;
    public int aimMouseButton = 1;
    public bool rightMouseButtonThrows = false;
    public bool requirePickupTarget = false;
    public bool hasPickupTarget = false;
    public float jumpDuration = 0.95f;
    public float pickUpDuration = 1.15f;
    public float throwDuration = 0.95f;
    public bool useAnimatorStateLengthForActions = true;
    public float actionExitNormalizedTime = 0.98f;
    public float minimumActionDuration = 0.08f;

    [Header("Idle / Battle")]
    public bool isInSafeArea = true;
    public bool isEnemyNearby = false;
    public bool isEnemyDetectedPlayer = false;
    public float idleSadDelay = 5f;
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
    public string idleBattleState = "IdleBattle";
    public string slowRunState = "SlowRun";
    public string sprintState = "Sprint";
    public string standingToCrouchState = "StandingToCrouch";
    public string crouchIdleState = "CrouchIdle";
    public string crouchWalkingState = "CrouchWalking";
    public string crouchToStandingState = "CrouchToStanding";
    public string crouchToSprintState = "CrouchToSprint";
    public string idleToRollState = "IdleToRoll";
    public string sprintingToRollState = "SprintingToRoll";
    public string runningBackwardState = "RunningBackward";
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
    private float verticalVelocity;
    private float idleTimer;
    private float stateTimer;
    private float activeStateDuration;
    private float previousRollCurveValue;
    private float battleRelaxTimer = float.PositiveInfinity;
    private float enemyProbeTimer;
    private float lastRootMotionTime = -100f;
    private float lastHorizontalRootMotionTime = -100f;
    private int currentStateHash;
    private float standingControllerHeight;
    private float standingControllerRadius;
    private bool activeOneShot;
    private bool grounded;
    private bool hasMoveInput;
    private bool sprintHeld;
    private bool aimHeld;
    private bool isCrouching;
    private bool crouchWanted;
    private bool returnToCrouchAfterAction;
    private bool autoEnemyNearby;
    private bool standingControllerShapeCaptured;
    private bool controllerSizedForRoll;

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
        sprintTurnSpeed = Mathf.Max(1f, sprintTurnSpeed);
        aimTurnSpeed = Mathf.Max(1f, aimTurnSpeed);
        jumpHeight = Mathf.Max(0f, jumpHeight);
        idleRollDuration = Mathf.Max(0.1f, idleRollDuration);
        sprintRollDuration = Mathf.Max(0.1f, sprintRollDuration);
        idleRollDistance = Mathf.Max(0f, idleRollDistance);
        sprintRollDistance = Mathf.Max(0f, sprintRollDistance);
        rollControllerHeight = Mathf.Max(0.1f, rollControllerHeight);
        rollControllerRadius = Mathf.Clamp(rollControllerRadius, 0.01f, rollControllerHeight * 0.5f);
        standingToCrouchDuration = Mathf.Max(0.1f, standingToCrouchDuration);
        crouchToStandingDuration = Mathf.Max(0.1f, crouchToStandingDuration);
        crouchToSprintDuration = Mathf.Max(0.1f, crouchToSprintDuration);
        jumpDuration = Mathf.Max(0.1f, jumpDuration);
        pickUpDuration = Mathf.Max(0.1f, pickUpDuration);
        throwDuration = Mathf.Max(0.1f, throwDuration);
        idleSadDelay = Mathf.Max(0f, idleSadDelay);
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

        UpdateFacing();
        MoveCharacter(Time.deltaTime);
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
        moveInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        moveDirection = GetMoveDirection(moveInput);
        hasMoveInput = moveInput.sqrMagnitude > InputDeadZone * InputDeadZone && moveDirection.sqrMagnitude > 0.001f;
        sprintHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        aimHeld = Input.GetMouseButton(aimMouseButton);
    }

    private void EvaluateRequests()
    {
        if (EnemyThreatActive && isCrouching)
        {
            crouchWanted = false;
            pendingAfterStand = PendingAfterStand.None;
            StartOneShot(PlayerState.CrouchToStanding, crouchToStandingDuration);
            return;
        }

        if (Input.GetKeyDown(rollKey))
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

        if (WantsToJump())
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

        if (WantsToThrow())
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

        if (Input.GetKeyDown(pickUpKey) && CanPickUp)
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

        if (Input.GetKeyDown(crouchKey) || Input.GetKeyDown(alternateCrouchKey))
        {
            ToggleCrouch();
            return;
        }

        if (isCrouching && sprintHeld && hasMoveInput)
        {
            crouchWanted = false;
            StartOneShot(PlayerState.CrouchToSprint, crouchToSprintDuration);
            return;
        }

    }

    private void ToggleCrouch()
    {
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
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        StartOneShot(PlayerState.Jump, jumpDuration);
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
        SetRollControllerShape(IsRollState(state));
        if (ShouldLockHorizontalMotionForAction(state))
        {
            horizontalVelocity = Vector3.zero;
        }
        PlayState(state, true);
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

        if (currentState == PlayerState.Jump && !grounded && stateTimer < activeStateDuration + 0.35f)
        {
            return false;
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

    private void CompleteOneShot()
    {
        PlayerState completedState = currentState;
        activeOneShot = false;
        if (IsRollState(completedState))
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
        else if (completedState == PlayerState.CrouchToSprint)
        {
            isCrouching = false;
            crouchWanted = false;
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

    private void EvaluateLocomotionState()
    {
        if (activeOneShot)
        {
            return;
        }

        if (isCrouching)
        {
            currentState = hasMoveInput ? PlayerState.CrouchWalking : PlayerState.CrouchIdle;
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
            currentState = PlayerState.IdleSad;
            return;
        }

        currentState = PlayerState.IdleNeutral;
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

        verticalVelocity += gravity * deltaTime;
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

        if (currentState == PlayerState.Jump && rootMotionDrivesJump)
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
            || currentState == PlayerState.CrouchWalking
            || currentState == PlayerState.CrouchToSprint;
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
            || state == PlayerState.Jump
            || state == PlayerState.PickingUp
            || state == PlayerState.Throw;
    }

    private static bool IsReversePlaybackState(PlayerState state)
    {
        return state == PlayerState.StandingToCrouch;
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

    private static bool ShouldLockHorizontalMotionForAction(PlayerState state)
    {
        return state != PlayerState.Jump && !IsRollState(state);
    }

    private void CaptureStandingControllerShape()
    {
        if (characterController == null || standingControllerShapeCaptured || controllerSizedForRoll)
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

    private void RestoreStandingControllerShape()
    {
        if (!controllerSizedForRoll || characterController == null || !standingControllerShapeCaptured)
        {
            return;
        }

        ApplyControllerShape(standingControllerHeight, standingControllerRadius, standingControllerCenter);
        controllerSizedForRoll = false;
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
            || state == PlayerState.CrouchWalking;
    }

    private bool ShouldSnapToGroundForCurrentState()
    {
        return currentState != PlayerState.Jump;
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
            || (currentState == PlayerState.Jump && rootMotionDrivesJump)
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

        if (currentState == PlayerState.SlowRun || currentState == PlayerState.Jump)
        {
            return hasMoveInput ? (sprintHeld ? sprintSpeed : slowRunSpeed) : 0f;
        }

        return 0f;
    }

    private Vector3 GetTargetMoveDirection()
    {
        if (hasMoveInput)
        {
            return moveDirection.normalized;
        }

        return Vector3.zero;
    }

    private void UpdateFacing()
    {
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
        return Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
    }

    private bool WantsToThrow()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        return rightMouseButtonThrows && Input.GetMouseButtonDown(aimMouseButton);
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
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, GetTurnSpeed(direction) * Time.deltaTime);
    }

    private float GetTurnSpeed(Vector3 targetDirection)
    {
        if (aimHeld)
        {
            return aimTurnSpeed;
        }

        if (currentState == PlayerState.Sprint || (sprintHeld && hasMoveInput))
        {
            float angle = Vector3.Angle(GetFlatForward(transform), targetDirection);
            return angle > 110f ? sprintTurnSpeed * 0.75f : sprintTurnSpeed;
        }

        return turnSpeed;
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
            case PlayerState.CrouchToSprint:
                return crouchToSprintState;
            case PlayerState.IdleToRoll:
                return idleToRollState;
            case PlayerState.SprintingToRoll:
                return sprintingToRollState;
            case PlayerState.RunningBackward:
                return runningBackwardState;
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
        stateFallbacks[idleBattleState] = new[] { "Idle", "IdleBattle" };
        stateFallbacks[sprintState] = new[] { "FastRun", "Fast Run" };
        stateFallbacks[sprintingToRollState] = new[] { "RunToRolling", "Run To Rolling" };
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
        bool jumping = currentState == PlayerState.Jump || !grounded;
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

        if (cameraTransform != null && cameraFollow == null)
        {
            cameraFollow = cameraTransform.GetComponent<BasicCameraFollow>();
        }
    }

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = useRootMotion;
        AnimatorRootMotionRelay relay = animator.GetComponent<AnimatorRootMotionRelay>();

        if (!useRootMotion)
        {
            if (relay != null)
            {
                relay.movement = null;
            }

            return;
        }

        if (relay == null)
        {
            relay = animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
        }

        relay.movement = this;
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

