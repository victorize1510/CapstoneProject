using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.MapSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Skills;
using UnityEngine;
using UnityEngine.AI;

public partial class PetController
{
    internal void FinishWildCapture(Transform nextOwner)
    {
        wildEncounter = false;
        owner = nextOwner;
        IsSummoned = false;
        state = PetState.Dormant;
        SetWorldPresence(false);
        QueueOwnedRuntimeStorage();
    }

    public bool ShouldShowVisualRenderer(Renderer renderer)
    {
        if (renderer == null || (!IsSummoned && !wildEncounter))
            return false;
        var visual = GetComponentInChildren<Capstone.Game.HudSystem.PetEvolutionVisualController>(true);
        return (visual == null || visual.IsCurrentVisualRenderer(renderer)) && rendererEnabledStates.TryGetValue(renderer, out bool allowed) && allowed;
    }

    public void SetVisualRendererEnabled(Renderer renderer, bool enabled)
    {
        if (renderer == null)
            return;
        rendererEnabledStates[renderer] = enabled;
        renderer.enabled = enabled && (IsSummoned || wildEncounter);
    }

    public void RefreshVisualPresence() => SetWorldPresence(IsSummoned || wildEncounter);
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

        if (owner == null || !TryFindSafeSpotNearOwner(out Vector3 spot) || !TrySummonAt(spot, owner.forward))
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
        if (combatant != null && (combatStats == null || combatStats.Health <= 0))
            return false;
        PrepareOwnedRuntime();
        if (!TryPlaceForSummon(worldPosition))
        {
            QueueOwnedRuntimeStorage();
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

        string[] castState = {"Cast Spell"};
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

    public bool TryRestoreSummonedNearOwner()
    {
        PrepareOwnedRuntime();
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
            QueueOwnedRuntimeStorage();
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

    private void EnterDormant()
    {
        defeatPresentation = false;
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
        QueueOwnedRuntimeStorage();
    }

    private void CacheWorldPresence()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.GetComponentInParent<AAMAP.MapIcon>(true) == null && !rendererEnabledStates.ContainsKey(renderer))
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
            AnimatorStateInfo castState = animator != null ? animator.GetCurrentAnimatorStateInfo(0) : default;
            bool castFinished = animator != null && !animator.IsInTransition(0) && castState.shortNameHash == Animator.StringToHash("Cast Spell") && castState.normalizedTime >= 0.98f;
            bool castExitedAfterDuration = animator != null && !animator.IsInTransition(0) && castState.shortNameHash != Animator.StringToHash("Cast Spell") && Time.time - summonStartedAt >= summonAnimationDuration;
            if (!castFinished && !castExitedAfterDuration && Time.time - summonStartedAt < Mathf.Max(3f, summonAnimationDuration + 0.5f))
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
        if (!summonAnimatorSpeedOverridden)
            return;
        if (animator != null)
            animator.speed = summonAnimatorSpeedBefore;
        summonAnimatorSpeedOverridden = false;
    }
}
