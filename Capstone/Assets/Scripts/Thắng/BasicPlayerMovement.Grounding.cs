using System;
using System.Collections.Generic;
using UnityEngine;

public partial class BasicPlayerMovement
{
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
        float liftWeight = jumpControllerCenterLiftCurve != null && jumpControllerCenterLiftCurve.length > 0 ? Mathf.Max(0f, jumpControllerCenterLiftCurve.Evaluate(normalizedTime)) : 0f;
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
        Capstone.Game.CreatureSystem.CreatureCombatant.RegisterPlayerBody(characterController);
    }

    private void UpdateGroundedBeforeMove()
    {
        grounded = characterController != null && characterController.isGrounded;
        if (grounded && verticalVelocity < 0f)
        {
            verticalVelocity = groundedStickForce;
        }
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
            if (hit.collider == null || OwnsCollider(hit.collider) ||
                Capstone.Game.CreatureSystem.CreatureCombatTarget.From(hit.collider).Exists)
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
}
