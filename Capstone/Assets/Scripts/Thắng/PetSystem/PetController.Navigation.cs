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
    private bool TryPlaceForSummon(Vector3 requestedPosition)
    {
        if (!TryFindWalkableGround(requestedPosition, 0.65f, out Vector3 groundPoint) || !HasRoomAt(groundPoint))
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

    private bool TryFindSafeSpotNearOwner(out Vector3 spot, float ownerGroundTolerance = 1f, bool expandedSearch = false,
        bool requireClearOwnerSide = false)
    {
        spot = default;
        if (owner == null || !TryFindWalkableGround(owner.position, ownerGroundTolerance, out Vector3 ownerGround))
        {
            return false;
        }

        if (TryResolveSafeSpot(followOffset, ownerGround, requireClearOwnerSide, out spot))
        {
            return true;
        }

        for (int i = 0; i < SafeSpotFallbackOffsets.Length; i++)
        {
            if (TryResolveSafeSpot(SafeSpotFallbackOffsets[i], ownerGround, requireClearOwnerSide, out spot))
            {
                return true;
            }
        }

        if (expandedSearch)
        {
            // Offset co dinh co the nam trong vat can khi vua load save.
            // Tim nen di duoc gan do thay vi an pet vo thoi han.
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 2.6f : 4f;
                for (int step = 0; step < 12; step++)
                {
                    float angle = step * Mathf.PI / 6f;
                    Vector3 offset = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                    if (TryResolveSafeSpot(offset, ownerGround, requireClearOwnerSide, out spot))
                        return true;
                }
            }
        }

        return false;
    }

    private bool TryResolveSafeSpot(Vector3 localOffset, Vector3 ownerGround, bool requireClearOwnerSide, out Vector3 spot)
    {
        Vector3 candidate = owner.TransformPoint(localOffset);
        candidate.y = ownerGround.y;
        if (TryFindWalkableGround(candidate, 0.6f, out spot) && HasRoomAt(spot)
            && (!requireClearOwnerSide || HasClearReturnRoute(ownerGround, spot)))
        {
            return true;
        }

        spot = default;
        return false;
    }

    private bool HasClearReturnRoute(Vector3 ownerGround, Vector3 petGround)
    {
        if (Mathf.Abs(ownerGround.y - petGround.y) > 1f) return false;
        Vector3 delta = petGround - ownerGround;
        float distance = delta.magnitude;
        if (distance <= 0.01f) return true;

        int count = Physics.RaycastNonAlloc(ownerGround + Vector3.up * directProbeHeight, delta / distance,
            directObstacleHits, distance, directObstacleMask, QueryTriggerInteraction.Ignore);
        if (count == directObstacleHits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = directObstacleHits[i].collider;
            if (obstacle != null && !IsIgnoredDirectObstacle(obstacle)) return false;
        }
        return true;
    }

    private void UpdateCloseFollow()
    {
        Vector3 offset = followOffset;
        offset.y = 0;
        if (offset.sqrMagnitude < .01f) offset = Vector3.back;
        var body = GetComponent<CapsuleCollider>();
        var playerBody = owner.GetComponent<CharacterController>();
        float radius = body != null ? Mathf.Max(body.bounds.extents.x, body.bounds.extents.z) : directBodyRadius;
        float playerRadius = playerBody != null ? playerBody.radius * Mathf.Max(owner.lossyScale.x, owner.lossyScale.z) : .5f;
        float spacing = Mathf.Max(closeFollowDistance, radius + playerRadius + .3f);
        Vector3 destination = owner.position + owner.TransformDirection(offset.normalized) * spacing;
        float distance = FlatDistance(transform.position, destination);
        if (distance <= .4f && HasClearReturnRoute(owner.position, transform.position))
        {
            commandedReturnAt = -1f;
            returnProgressPosition = transform.position;
            StopMovement();
            return;
        }
        if (commandedReturnAt < 0f || FlatDistance(transform.position, returnProgressPosition) >= .2f)
        {
            commandedReturnAt = Time.time + CommandedReturnTimeout;
            returnProgressPosition = transform.position;
        }
        if (Time.time >= commandedReturnAt)
        {
            if (TeleportNearOwner(forReturnCommand: true))
            {
                returnProgressPosition = transform.position;
                commandedReturnAt = Time.time + CommandedReturnTimeout;
                return;
            }
            commandedReturnAt = Time.time + .25f;
        }
        float baseSpeed = enableBasicAttacks ? followSpeed : Mathf.Max(followSpeed, commandSpeed);
        float speed = Mathf.Lerp(baseSpeed, baseSpeed * fastReturnSpeedMultiplier, Mathf.InverseLerp(.4f, 3f, distance));
        MoveTo(destination, speed, .3f);
        UpdateAnimator(GetCurrentSpeed());
    }

    private bool HasRoomAt(Vector3 point)
    {
        int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * 0.45f, point + Vector3.up * 1.2f, Mathf.Max(0.25f, directBodyRadius), presenceHits, directObstacleMask, QueryTriggerInteraction.Ignore);
        if (count == presenceHits.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            Collider candidate = presenceHits[i];
            if (candidate != null && !IsIgnoredDirectObstacle(candidate) && !IsSupportingGround(candidate, point) && candidate.bounds.max.y > point.y + directMaxStepHeight)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsSupportingGround(Collider collider, Vector3 point)
    {
        Ray ray = new Ray(point + Vector3.up, Vector3.down);
        return collider.Raycast(ray, out RaycastHit hit, 1.5f) && hit.normal.y >= directMinGroundNormal && Mathf.Abs(hit.point.y - point.y) <= 0.2f;
    }

    private void MoveTo(Vector3 destination, float speed, float stopDistance)
    {
        if (combatant != null && combatant.MovementLocked) { StopMovement(false); return; }
        if (combatant != null)
            speed *= combatant.MovementScale;
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
        bool destinationChanged = !navDestinationInitialized || (navDestination - lastNavDestination).sqrMagnitude > destinationThreshold * destinationThreshold;
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

    private bool TryPlaceAgentOnNavMesh(Vector3 position, float searchRadius, bool forReturnCommand = false)
    {
        if (agent == null || !agent.gameObject.activeInHierarchy || !NavMesh.SamplePosition(position, out NavMeshHit hit, Mathf.Max(0.25f, searchRadius), agent.areaMask) || Mathf.Abs(hit.position.y - position.y) > 0.35f)
        {
            return false;
        }

        if (forReturnCommand && (!TryFindWalkableGround(owner.position, 2.5f, out Vector3 ownerGround)
            || !HasRoomAt(hit.position) || !HasClearReturnRoute(ownerGround, hit.position)))
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

        float step = Mathf.Min(Mathf.Max(0f, speed) * Time.deltaTime, toDestination.magnitude - stopDistance);
        if (step <= 0f)
        {
            directStuckSince = 0f;
            StopMovement();
            return;
        }

        if (!TryChooseDirectStep(current, toDestination.normalized, step, out Vector3 next, out Vector3 direction))
        {
            if (directStuckSince <= 0f)
            {
                directStuckSince = Time.time;
            }
            else if (commandedReturnAt < 0f && Time.time - directStuckSince > 2.5f
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
        directMoveSpeed = FlatDistance(current, transform.position) / Mathf.Max(Time.deltaTime, 0.0001f);
        directMoveFrame = Time.frameCount;
    }

    private bool TryChooseDirectStep(Vector3 current, Vector3 desiredDirection, float step, out Vector3 next, out Vector3 chosenDirection)
    {
        chosenDirection = desiredDirection;
        if (Time.time < directAvoidanceUntil && Mathf.Abs(directAvoidanceAngle) > 0.01f)
        {
            Vector3 heldDirection = Quaternion.Euler(0f, directAvoidanceAngle, 0f) * desiredDirection;
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
                Vector3 candidateDirection = Quaternion.Euler(0f, avoidanceAngle, 0f) * desiredDirection;
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
        Vector3 probeOrigin = current + Vector3.up * Mathf.Max(directProbeHeight, radius + 0.1f);
        float lookAhead = step + Mathf.Max(radius, directLookAhead);
        int count = Physics.SphereCastNonAlloc(probeOrigin, radius, direction, directObstacleHits, lookAhead, directObstacleMask, QueryTriggerInteraction.Ignore);
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

            bool walkableGround = hit.normal.y >= directMinGroundNormal && hit.point.y <= current.y + directMaxStepHeight;
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

    // Chi skill da duoc chap nhan moi goi khi di chuyen thuong dang tam dung.
    // Lui bi chan thi dung lui va tiep tuc cast; khong ne ngang hay teleport.
    public bool TrySkillRetreatStep(Vector3 direction, float distance)
    {
        if (!CanReceiveCommands || distance <= 0 || !IsSkillAnimationActive())
            return false;
        direction.y = 0;
        if (direction.sqrMagnitude < .001f)
            return false;
        direction.Normalize();
        Vector3 current = transform.position;
        if (!TryDirectStep(current, direction, distance, out Vector3 next))
            return false;
        var capsule = GetComponent<CapsuleCollider>();
        Bounds bounds = capsule != null ? capsule.bounds : new Bounds(current + Vector3.up, new Vector3(.6f, 2, .6f));
        float radius = Mathf.Max(.1f, Mathf.Min(bounds.extents.x, bounds.extents.z) * .95f);
        float half = Mathf.Max(0, bounds.extents.y - radius);
        int count = Physics.CapsuleCastNonAlloc(bounds.center + Vector3.up * half, bounds.center - Vector3.up * half, radius, direction, directObstacleHits, distance + .04f, directObstacleMask, QueryTriggerInteraction.Ignore);
        if (count == directObstacleHits.Length)
            return false;
        for (int i = 0; i < count; i++)
        {
            var hit = directObstacleHits[i];
            if (hit.collider == null || hit.transform.IsChildOf(transform) ||
                (owner != null && hit.transform.IsChildOf(owner)))
                continue;
            if (hit.normal.y >= directMinGroundNormal && hit.point.y <= current.y + directMaxStepHeight)
                continue;
            return false;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (!NavMesh.SamplePosition(next, out NavMeshHit sampled, .15f, agent.areaMask) || NavMesh.Raycast(current, sampled.position, out _, agent.areaMask))
                return false;
            StopMovement(false);
            agent.Move(sampled.position - current);
        }
        else
        {
            StopMovement(false);
            transform.position = next;
        }

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

    private bool TeleportNearOwner(bool forReturnCommand = false)
    {
        if (owner == null || FlatDistance(transform.position, owner.position) <= Mathf.Max(1f, teleportDistance))
            return false;
        if (!TryFindSafeSpotNearOwner(out Vector3 spot, forReturnCommand ? 2.5f : 1f,
            expandedSearch: forReturnCommand, requireClearOwnerSide: forReturnCommand))
        {
            return false;
        }

        if (useNavMeshWhenAvailable && TryPlaceAgentOnNavMesh(spot, 0.75f, forReturnCommand))
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
            return agent.isStopped ? 0f : Vector3.ProjectOnPlane(agent.velocity, Vector3.up).magnitude;
        }

        return directMoveFrame == Time.frameCount ? directMoveSpeed : 0f;
    }

    private bool TryFindWalkableGround(Vector3 position, float maxHeightChange, out Vector3 groundPoint)
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
            if (hit.collider == null || IsIgnoredDirectObstacle(hit.collider) || (hit.collider.attachedRigidbody != null && !hit.collider.attachedRigidbody.isKinematic) || hit.normal.y < directMinGroundNormal || Mathf.Abs(hit.point.y - position.y) > maxHeightChange)
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

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
