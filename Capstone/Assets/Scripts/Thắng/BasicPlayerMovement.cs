using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class BasicPlayerMovement : MonoBehaviour
{
    private const float InputDeadZone = 0.05f;

    [Header("References")]
    public Animator animator;
    public Transform cameraTransform;

    [Header("Movement Fallback")]
    public float slowRunSpeed = 3.5f;
    public float fastRunSpeed = 6f;
    public float backwardSpeed = 2.8f;
    public float acceleration = 18f;
    public float deceleration = 22f;
    public float turnSpeed = 720f;
    public bool cameraRelativeMovement = true;

    [Header("Root Motion")]
    public bool useRootMotion = true;
    public bool fallbackToCodeMotionWhenNoRootMotion = true;
    public float slowRunRootMotionScale = 1f;
    public float fastRunRootMotionScale = 1f;
    public float backwardRootMotionScale = 1f;
    public float rollRootMotionScale = 1f;
    public float rootMotionFallbackThreshold = 0.01f;

    [Header("Jump")]
    public float jumpHeight = 1.4f;
    public float gravity = -25f;
    public float groundedStickForce = -2f;

    [Header("Roll")]
    public KeyCode rollKey = KeyCode.Q;
    public float idleRollDuration = 0.9f;
    public float runRollDuration = 0.85f;
    public float idleRollDistance = 2.2f;
    public float runRollDistance = 3.6f;
    public AnimationCurve rollMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Look Back")]
    public int aimMouseButton = 1;
    public float lookBackDotThreshold = -0.35f;

    [Header("Throw")]
    public float throwDuration = 0.75f;

    [Header("Idle")]
    public float idleChillDelay = 5f;

    [Header("Model Anchor")]
    public bool keepAnimatorTransformPinned = true;

    [Header("Animator States")]
    public string idleState = "Idle";
    public string idleChillState = "IdleChill";
    public string slowRunState = "SlowRun";
    public string fastRunState = "FastRun";
    public string runningBackwardState = "RunningBackward";
    public string runLookBackState = "RunLookBack";
    public string idleToRollState = "IdleToRoll";
    public string runToRollingState = "RunToRolling";
    public string jumpState = "Jump";
    public string throwState = "Throw";
    public float locomotionFadeTime = 0.12f;
    public float actionFadeTime = 0.06f;

    [Header("Animator Parameters")]
    public string speedParameter = "Speed";
    public string moveXParameter = "MoveX";
    public string moveYParameter = "MoveY";
    public string groundedParameter = "Grounded";
    public string verticalSpeedParameter = "VerticalSpeed";
    public string fastRunParameter = "FastRun";
    public string backwardParameter = "Backward";
    public string lookBackParameter = "LookBack";
    public string rollingParameter = "Rolling";
    public string jumpingParameter = "Jumping";
    public string throwingParameter = "Throwing";

    public bool IsRolling
    {
        get { return isRolling; }
    }

    public bool IsAiming
    {
        get { return aimHeld; }
    }

    private CharacterController characterController;
    private BasicCameraFollow cameraFollow;
    private Transform animatorTransform;
    private Vector3 animatorLocalPosition;
    private Quaternion animatorLocalRotation;
    private Vector2 moveInput;
    private Vector3 moveDirection;
    private Vector3 horizontalVelocity;
    private Vector3 rollDirection;
    private float verticalVelocity;
    private float idleTimer;
    private float rollTimer;
    private float activeRollDuration;
    private float activeRollDistance;
    private float previousRollCurveValue;
    private float throwTimer;
    private int currentStateHash;
    private bool isRolling;
    private bool isThrowing;
    private bool isJumping;
    private bool grounded;
    private bool hasMoveInput;
    private bool fastRunHeld;
    private bool aimHeld;
    private bool movingBackward;
    private bool lookBackActive;
    private bool runLookBackActive;
    private string activeRollState;
    private readonly HashSet<int> animatorParameters = new HashSet<int>();

    private void Reset()
    {
        characterController = GetComponent<CharacterController>();
        animator = FindBestAnimator();

        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
    }

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (animator == null)
        {
            animator = FindBestAnimator();
        }

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        RefreshCameraFollow();
        ConfigureAnimatorRootMotion();
        CacheAnimatorParameters();
        CaptureAnimatorAnchor();
    }

    private void LateUpdate()
    {
        PinAnimatorTransform();
    }

    private void OnValidate()
    {
        slowRunSpeed = Mathf.Max(0f, slowRunSpeed);
        fastRunSpeed = Mathf.Max(slowRunSpeed, fastRunSpeed);
        backwardSpeed = Mathf.Max(0f, backwardSpeed);
        acceleration = Mathf.Max(0.1f, acceleration);
        deceleration = Mathf.Max(0.1f, deceleration);
        idleChillDelay = Mathf.Max(0f, idleChillDelay);
        idleRollDuration = Mathf.Max(0.1f, idleRollDuration);
        runRollDuration = Mathf.Max(0.1f, runRollDuration);
        idleRollDistance = Mathf.Max(0f, idleRollDistance);
        runRollDistance = Mathf.Max(0f, runRollDistance);
        throwDuration = Mathf.Max(0.1f, throwDuration);
        slowRunRootMotionScale = Mathf.Max(0f, slowRunRootMotionScale);
        fastRunRootMotionScale = Mathf.Max(0f, fastRunRootMotionScale);
        backwardRootMotionScale = Mathf.Max(0f, backwardRootMotionScale);
        rollRootMotionScale = Mathf.Max(0f, rollRootMotionScale);
        rootMotionFallbackThreshold = Mathf.Max(0f, rootMotionFallbackThreshold);

        if (rollMotionCurve == null || rollMotionCurve.length == 0)
        {
            rollMotionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }
    }

    private void Update()
    {
        RefreshCameraFollow();
        ConfigureAnimatorRootMotion();

        moveInput = ReadMoveInput();
        bool jumpPressed = WantsToJump();
        bool rollPressed = Input.GetKeyDown(rollKey);
        bool throwPressed = Input.GetMouseButtonDown(aimMouseButton);
        fastRunHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        aimHeld = Input.GetMouseButton(aimMouseButton);

        grounded = characterController.isGrounded;
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
            isJumping = false;
        }

        moveDirection = GetMoveDirection(moveInput);
        hasMoveInput = moveInput.sqrMagnitude > InputDeadZone * InputDeadZone && moveDirection.sqrMagnitude > 0.001f;
        movingBackward = aimHeld && hasMoveInput && moveInput.y < -0.15f;
        lookBackActive = aimHeld || IsMovingAgainstFacing();
        runLookBackActive = aimHeld && hasMoveInput && fastRunHeld && moveInput.y > 0.2f && !movingBackward;

        if (!isRolling && grounded && rollPressed)
        {
            StartRoll(hasMoveInput, moveDirection);
        }
        else if (!isRolling && grounded && throwPressed)
        {
            StartThrow();
        }
        else if (!isRolling && grounded && jumpPressed)
        {
            StartJump();
        }

        RotateToward(GetCurrentFacingDirection());
        UpdateActionTimers(Time.deltaTime);
        UpdateIdleTimer();
        UpdateAnimation();
        UpdateAnimatorParameters();

        if (animator == null || !useRootMotion)
        {
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            ApplyCharacterMotion(BuildFallbackHorizontalDelta(deltaTime), deltaTime);
        }
    }

    public void ApplyAnimatorRootMotion(Vector3 animatorDeltaPosition, Quaternion animatorDeltaRotation)
    {
        if (!enabled || characterController == null)
        {
            return;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 horizontalDelta = BuildRootMotionHorizontalDelta(animatorDeltaPosition, deltaTime);
        ApplyCharacterMotion(horizontalDelta, deltaTime);
    }

    private Vector2 ReadMoveInput()
    {
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        return Vector2.ClampMagnitude(input, 1f);
    }

    private Vector3 GetMoveDirection(Vector2 input)
    {
        Vector3 direction;

        if (cameraRelativeMovement && cameraTransform != null)
        {
            direction = GetCameraForward() * input.y + GetCameraRight() * input.x;
        }
        else
        {
            direction = GetFlatForward(transform) * input.y + GetFlatRight(transform) * input.x;
        }

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private Vector3 GetCurrentFacingDirection()
    {
        if (isRolling)
        {
            return rollDirection;
        }

        if (aimHeld)
        {
            return GetCameraForward();
        }

        if (hasMoveInput)
        {
            return moveDirection;
        }

        return GetFlatForward(transform);
    }

    private Vector3 BuildRootMotionHorizontalDelta(Vector3 animatorDeltaPosition, float deltaTime)
    {
        if (!ShouldUseRootMotionHorizontal())
        {
            return Vector3.zero;
        }

        Vector3 horizontalDelta = animatorDeltaPosition;
        horizontalDelta.y = 0f;
        horizontalDelta *= GetCurrentRootMotionScale();

        if (horizontalDelta.magnitude <= rootMotionFallbackThreshold && fallbackToCodeMotionWhenNoRootMotion)
        {
            return BuildFallbackHorizontalDelta(deltaTime);
        }

        return horizontalDelta;
    }

    private bool ShouldUseRootMotionHorizontal()
    {
        return isRolling || isThrowing || (grounded && hasMoveInput && !isJumping);
    }

    private float GetCurrentRootMotionScale()
    {
        if (isRolling)
        {
            return rollRootMotionScale;
        }

        if (movingBackward)
        {
            return backwardRootMotionScale;
        }

        return fastRunHeld ? fastRunRootMotionScale : slowRunRootMotionScale;
    }

    private Vector3 BuildFallbackHorizontalDelta(float deltaTime)
    {
        if (isRolling)
        {
            return BuildRollFallbackDelta(deltaTime);
        }

        if (isThrowing || isJumping || !hasMoveInput)
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, deceleration * deltaTime);
            return horizontalVelocity * deltaTime;
        }

        float targetSpeed = movingBackward ? backwardSpeed : fastRunHeld ? fastRunSpeed : slowRunSpeed;
        Vector3 targetVelocity = moveDirection * targetSpeed;
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, acceleration * deltaTime);
        return horizontalVelocity * deltaTime;
    }

    private Vector3 BuildRollFallbackDelta(float deltaTime)
    {
        float normalizedTime = Mathf.Clamp01(rollTimer / activeRollDuration);
        float curveValue = EvaluateRollCurve(normalizedTime);
        float deltaDistance = Mathf.Max(0f, curveValue - previousRollCurveValue) * activeRollDistance;
        previousRollCurveValue = curveValue;
        return rollDirection * deltaDistance;
    }

    private void ApplyCharacterMotion(Vector3 horizontalDelta, float deltaTime)
    {
        verticalVelocity += gravity * deltaTime;

        Vector3 motion = horizontalDelta;
        motion.y = verticalVelocity * deltaTime;

        CollisionFlags collisionFlags = characterController.Move(motion);
        bool groundedAfterMove = characterController.isGrounded || (collisionFlags & CollisionFlags.Below) != 0;

        if (groundedAfterMove && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
            isJumping = false;
        }

        grounded = groundedAfterMove;
        horizontalVelocity = horizontalDelta / deltaTime;
    }

    private void StartRoll(bool rollingFromMovement, Vector3 desiredMoveDirection)
    {
        isRolling = true;
        isThrowing = false;
        rollTimer = 0f;
        idleTimer = 0f;
        previousRollCurveValue = 0f;
        horizontalVelocity = Vector3.zero;
        activeRollDuration = rollingFromMovement ? runRollDuration : idleRollDuration;
        activeRollDistance = rollingFromMovement ? runRollDistance : idleRollDistance;
        activeRollState = rollingFromMovement ? runToRollingState : idleToRollState;

        rollDirection = rollingFromMovement && desiredMoveDirection.sqrMagnitude > 0.001f
            ? desiredMoveDirection.normalized
            : GetFlatForward(transform);

        RotateToward(rollDirection);
        PlayAnimation(activeRollState, actionFadeTime, true);
    }

    private void StartJump()
    {
        isJumping = true;
        isThrowing = false;
        idleTimer = 0f;
        verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        PlayAnimation(jumpState, actionFadeTime, true);
    }

    private void StartThrow()
    {
        isThrowing = true;
        throwTimer = 0f;
        idleTimer = 0f;
        PlayAnimation(throwState, actionFadeTime, true);
    }

    private void UpdateActionTimers(float deltaTime)
    {
        if (isRolling)
        {
            rollTimer += deltaTime;
            if (rollTimer >= activeRollDuration)
            {
                isRolling = false;
            }
        }

        if (isThrowing)
        {
            throwTimer += deltaTime;
            if (throwTimer >= throwDuration)
            {
                isThrowing = false;
            }
        }
    }

    private float EvaluateRollCurve(float normalizedTime)
    {
        if (rollMotionCurve == null || rollMotionCurve.length == 0)
        {
            return normalizedTime;
        }

        return Mathf.Clamp01(rollMotionCurve.Evaluate(normalizedTime));
    }

    private bool WantsToJump()
    {
        return Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space);
    }

    private void UpdateIdleTimer()
    {
        if (grounded && !isRolling && !isThrowing && !isJumping && !hasMoveInput)
        {
            idleTimer += Time.deltaTime;
        }
        else
        {
            idleTimer = 0f;
        }
    }

    private void UpdateAnimation()
    {
        if (animator == null)
        {
            return;
        }

        if (isRolling)
        {
            PlayAnimation(activeRollState, actionFadeTime, false);
            return;
        }

        if (isThrowing)
        {
            PlayAnimation(throwState, actionFadeTime, false);
            return;
        }

        if (!grounded || isJumping)
        {
            PlayAnimation(jumpState, actionFadeTime, false);
            return;
        }

        if (!hasMoveInput)
        {
            PlayAnimation(idleTimer >= idleChillDelay ? idleChillState : idleState, locomotionFadeTime, false);
            return;
        }

        if (movingBackward)
        {
            PlayAnimation(runningBackwardState, locomotionFadeTime, false);
            return;
        }

        if (runLookBackActive)
        {
            PlayAnimation(runLookBackState, locomotionFadeTime, false);
            return;
        }

        PlayAnimation(fastRunHeld ? fastRunState : slowRunState, locomotionFadeTime, false);
    }

    private void PlayAnimation(string stateName, float fadeTime, bool forceRestart)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        if (!forceRestart && currentStateHash == stateHash)
        {
            return;
        }

        animator.CrossFadeInFixedTime(stateHash, fadeTime, 0, 0f);
        currentStateHash = stateHash;
    }

    private void UpdateAnimatorParameters()
    {
        if (animator == null)
        {
            return;
        }

        float normalizedSpeed = Mathf.InverseLerp(0f, fastRunSpeed, new Vector2(horizontalVelocity.x, horizontalVelocity.z).magnitude);
        SetAnimatorFloat(speedParameter, normalizedSpeed);
        SetAnimatorFloat(moveXParameter, moveInput.x);
        SetAnimatorFloat(moveYParameter, moveInput.y);
        SetAnimatorBool(groundedParameter, grounded);
        SetAnimatorFloat(verticalSpeedParameter, verticalVelocity);
        SetAnimatorBool(fastRunParameter, fastRunHeld);
        SetAnimatorBool(backwardParameter, movingBackward);
        SetAnimatorBool(lookBackParameter, lookBackActive);
        SetAnimatorBool(rollingParameter, isRolling);
        SetAnimatorBool(jumpingParameter, !grounded || isJumping);
        SetAnimatorBool(throwingParameter, isThrowing);
    }

    private bool IsMovingAgainstFacing()
    {
        Vector3 flatVelocity = horizontalVelocity;
        flatVelocity.y = 0f;

        if (flatVelocity.sqrMagnitude < 0.25f)
        {
            return false;
        }

        return Vector3.Dot(flatVelocity.normalized, GetFlatForward(transform)) <= lookBackDotThreshold;
    }

    private void RotateToward(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private Vector3 GetFlatForward(Transform source)
    {
        Vector3 forward = source.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            return Vector3.forward;
        }

        return forward.normalized;
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
        Vector3 right = Vector3.Cross(Vector3.up, forward);

        if (right.sqrMagnitude < 0.001f)
        {
            return GetFlatRight(transform);
        }

        return right.normalized;
    }

    private Vector3 GetFlatRight(Transform source)
    {
        Vector3 right = source.right;
        right.y = 0f;

        if (right.sqrMagnitude < 0.001f)
        {
            return Vector3.right;
        }

        return right.normalized;
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

    private void ConfigureAnimatorRootMotion()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = useRootMotion;

        AnimatorRootMotionRelay relay = animator.GetComponent<AnimatorRootMotionRelay>();
        if (relay == null)
        {
            relay = animator.gameObject.AddComponent<AnimatorRootMotionRelay>();
        }

        relay.movement = this;
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
        if (animator == null)
        {
            return;
        }

        animatorTransform = animator.transform;
        animatorLocalPosition = animatorTransform.localPosition;
        animatorLocalRotation = animatorTransform.localRotation;
    }

    private void PinAnimatorTransform()
    {
        if (!keepAnimatorTransformPinned || animator == null)
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
