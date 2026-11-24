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
        if (defeatPresentation || combatant != null && (combatStats == null || combatStats.Health <= 0))
            return false;
        if (!PrepareSummonVisual()) return false;
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
        if (!IsSummoned) return false;
        if (!summonRevealPending) return true;
        if (!TryPlaceForSummon(groundPosition))
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
        if (!IsSummoned || state != PetState.Summoning || summonPresentationAnimationStarted)
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

        var driver = creatureAnimation;
        var cry = driver != null ? driver.Cry : null;
        if (cry != null && cry.TryGetSummonState(driver, out summonAnimationHash))
        {
            animator.CrossFadeInFixedTime(summonAnimationHash, .06f, 0, 0);
            cry.BeginSummon(summonAnimationHash);
            summonAnimationDuration = cry.SummonHoldLimit;
            summonCastActive = true;
            return;
        }

        string[] states = spawnStates;
        if (driver != null && driver.Profile != null && !driver.TryGetStates(CreatureAction.Spawn, out states))
            states = null;
        summonAnimationHash = 0;
        if (states != null && PlayAnimatorState(states, attackFade, true))
        {
            foreach (string candidate in states)
                if (TryGetStateHash(candidate, out summonAnimationHash)) break;
            string name = states[0];
            int separator = name.LastIndexOf('.');
            if (separator >= 0) name = name.Substring(separator + 1);
            summonAnimationDuration = Mathf.Clamp(FindAnimationDuration(name, spawnDuration) / speed, .05f, 5f);
            summonCastActive = true;
        }
        else
        {
            if (driver != null && driver.TryGetStates(CreatureAction.Idle, out var idle))
                PlayAnimatorState(idle, locomotionFade, true);
            else PlayAnimatorState(idleStates, locomotionFade, true);
            summonAnimationDuration = 0f;
            summonCastActive = false;
        }
    }

    public bool TryRecoverSummonPresentation(Vector3 groundPosition)
    {
        if (!IsSummoned || recallLocked || (combatStats != null && combatStats.Health <= 0)
            || !CompleteSummonPresentation(groundPosition)) return false;
        SetWorldPresence(true);
        FinishSummoning();
        return true;
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
        if (visible) Capstone.Game.CreatureSystem.CreatureCombatant.RefreshPlayerPassThrough(gameObject);
    }

    public bool TryRestoreSummonedNearOwner() => TryRestoreSummonedNearOwner(false);

    internal bool TryRestoreSummonedNearOwner(bool playGreeting)
    {
        if (combatStats != null && combatStats.Health <= 0) return false;
        if (!PrepareSummonVisual()) return false;
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
        if (playGreeting)
            BeginSummon(false);
        else
        {
            ScheduleNextRoam(0.5f);
            PlayAnimatorState(idleStates, locomotionFade, true);
        }
        return true;
    }

    // Both saved-summoned restoration and a ball release must bind the saved form
    // after waking the retained actor (Awake can otherwise rebind its base Animator).
    bool PrepareSummonVisual()
    {
        PrepareOwnedRuntime();
        if (wildEncounter) return true;
        try
        {
            GetComponent<PetCollectionMetadata>()?.RestoreEvolutionPresentation(combatStats);
            return true;
        }
        catch (System.Exception error)
        {
            Debug.LogWarning("Pet summon rejected: cannot restore saved form. " + error.Message, this);
            QueueOwnedRuntimeStorage();
            return false;
        }
    }

    public void Withdraw()
    {
        if (defeatPresentation)
            return;
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
            CancelSkillPursuit();
            creatureAnimation?.Cry?.ResetPresence();
            hasRecallVisualCenter = false;
            recallFacingTargetLost = false;
            recallWasInCombat = HasCombatTarget;
            recallFacingEnemy = recallWasInCombat ? target : null;
            recallLocked = true;
            commandedReturnAt = -1f;
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
        combatant?.ClearTransientEffects();
        CancelSkillPursuit();
        creatureAnimation?.Cry?.ResetPresence();
        closeFollowRequested = returnAfterBasic = false;
        commandedReturnAt = -1f;
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

        SetPresentationColliders(visible);

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
            StartSummonPresentationAnimation();
        }
    }

    private void UpdateSummoning()
    {
        StopMovement(false);
        if (summonRevealPending || !HasSummonPresentationAnimationFinished)
            return;

        FinishSummoning();
    }

    private void FinishSummoning()
    {
        creatureAnimation?.Cry?.EndSummon();
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

    internal void ResumeAfterEvolution()
    {
        creatureAnimation?.Cry?.ResetPresence();
        combatActions?.Cancel();
        hasPendingHit = hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
        skillAnimationUntil = 0;
        target = null;
        currentAnimatorState = null;
        RestoreSummonAnimatorSpeed();
        if (!IsSummoned) return;
        summonRevealPending = summonCastActive = false;
        state = PetState.FollowOwner;
        StopMovement(false);
        var driver = GetComponent<CreatureAnimationDriver>();
        if (driver != null && driver.TryGetStates(CreatureAction.Idle, out var idle))
            PlayAnimatorState(idle, locomotionFade, true);
        else PlayAnimatorState(idleStates, locomotionFade, true);
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
