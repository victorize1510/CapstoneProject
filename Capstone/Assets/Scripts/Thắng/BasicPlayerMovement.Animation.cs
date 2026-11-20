using System;
using System.Collections.Generic;
using UnityEngine;

public partial class BasicPlayerMovement
{
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
        return animator.HasState(0, Animator.StringToHash(stateName)) || animator.HasState(0, Animator.StringToHash("Base Layer." + stateName));
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
        stateFallbacks[idleNeutralState] = new[]{"Idle", "Neutral Idle"};
        stateFallbacks[idleSadState] = new[]{"IdleChill", "Sad Idle"};
        stateFallbacks[idleHappyState] = new[]{idleSadState, "Happy Idle", "IdleChill", idleNeutralState, "Idle"};
        stateFallbacks[idleBattleState] = new[]{"Idle", "IdleBattle"};
        stateFallbacks[sprintState] = new[]{"FastRun", "Fast Run"};
        stateFallbacks[sprintingToRollState] = new[]{"RunToRolling", "Run To Rolling"};
        // Trang thai roi khong fallback sang Jump de tranh nhin nhu nhay lan hai.
        stateFallbacks[floatingState] = new[]{idleNeutralState};
        stateFallbacks[idleJumpState] = new[]{jumpState, "Jump"};
        stateFallbacks[crouchIdleState] = new[]{idleNeutralState, "Idle"};
        stateFallbacks[pickingUpState] = new[]{idleNeutralState, "Idle"};
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

    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            configuredAnimator = null;
            animatorConfigurationInitialized = false;
            return;
        }

        if (animatorConfigurationInitialized && configuredAnimator == animator && configuredRootMotion == useRootMotion)
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
