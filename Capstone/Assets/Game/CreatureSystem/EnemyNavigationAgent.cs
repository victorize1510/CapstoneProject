using System;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.CreatureSystem
{
    internal enum EnemyNavigationResult
    {
        Moving,
        Stopped,
        HoldState,
        ReturnHome
    }

    /// <summary>Quan ly path va vat can; khong chon muc tieu, animation hoac damage.</summary>
    internal sealed class EnemyNavigationAgent
    {
        const float RecoveryRadius = .75f;
        const float RecoveryHeight = .5f;
        const float ProgressInterval = 1f;
        const float ProgressDistanceSquared = .04f;
        const int MaximumFailures = 3;
        const int MaximumTerritorySamples = 256;
        readonly Transform actor;
        readonly NavMeshAgent agent;
        readonly CapsuleCollider capsule;
        readonly Func<Vector3, bool> insideTerritory;
        readonly NavMeshPath path = new NavMeshPath();
        readonly Vector3[] corners = new Vector3[64];
        readonly RaycastHit[] hits = new RaycastHit[32];
        Vector3 lastProgress, pathDestination;
        float pathAt, navigationRetryAt, progressAt;
        int failures;
        bool forceRepath;
        public int PathRequests { get; private set; }

        public string LastReason { get; private set; }

        public EnemyNavigationAgent(Transform actor, NavMeshAgent agent, CapsuleCollider capsule, Func<Vector3, bool> insideTerritory)
        {
            this.actor = actor;
            this.agent = agent;
            this.capsule = capsule;
            this.insideTerritory = insideTerritory;
        }

        public void ResetProgress()
        {
            lastProgress = actor.position;
            progressAt = Time.time;
        }

        public void Resume()
        {
            RequestRepath();
            ResetProgress();
        }

        public void RequestRepath() => pathAt = 0;
        public void ClearFailures() => failures = 0;
        bool Ready()
        {
            if (agent == null)
            {
                LastReason = "Thieu NavMeshAgent";
                return false;
            }

            if (agent.enabled && agent.isOnNavMesh)
            {
                return true;
            }

            if (Time.time < navigationRetryAt)
            {
                return false;
            }

            navigationRetryAt = Time.time + 1;
            if (!NavMesh.SamplePosition(actor.position, out var hit, RecoveryRadius, agent.areaMask) || Mathf.Abs(hit.position.y - actor.position.y) > RecoveryHeight)
            {
                LastReason = "Vi tri khong co NavMesh hop le";
                return false;
            }

            agent.enabled = true;
            return agent.Warp(hit.position) && agent.isOnNavMesh;
        }

        public EnemyNavigationResult Move(Vector3 destination, bool returning, bool chasing, float moveSpeed, float attackRange, float pathInterval)
        {
            if (!Ready())
            {
                Stop();
                return EnemyNavigationResult.Stopped;
            }

            if (agent.pathPending)
            {
                return EnemyNavigationResult.HoldState;
            }

            agent.speed = moveSpeed;
            agent.stoppingDistance = chasing ? Mathf.Max(.2f, attackRange * .7f) : .3f;
            if (Time.time >= pathAt && (!agent.hasPath || agent.isPathStale || forceRepath || (destination - pathDestination).sqrMagnitude > .25f))
            {
                pathAt = Time.time + Mathf.Max(.1f, pathInterval);
                PathRequests++;
                bool valid = NavMesh.SamplePosition(destination, out var point, 1.5f, agent.areaMask) && agent.CalculatePath(point.position, path) && path.status == NavMeshPathStatus.PathComplete;
                if (valid && !returning)
                {
                    valid = PathInsideTerritory();
                }

                if (!valid)
                {
                    Stop();
                    LastReason = "Path khong day du hoac vuot lanh tho";
                    return ++failures >= MaximumFailures && !returning ? EnemyNavigationResult.ReturnHome : EnemyNavigationResult.Stopped;
                }

                if (!agent.SetPath(path))
                {
                    Stop();
                    return EnemyNavigationResult.HoldState;
                }

                pathDestination = destination;
                forceRepath = false;
            }

            if (!agent.hasPath)
            {
                Stop();
                return EnemyNavigationResult.Stopped;
            }

            if (Time.time - progressAt >= ProgressInterval)
            {
                if ((actor.position - lastProgress).sqrMagnitude < ProgressDistanceSquared && agent.remainingDistance > agent.stoppingDistance + .3f)
                {
                    pathAt = 0;
                    forceRepath = true;
                    if (++failures >= MaximumFailures && !returning)
                    {
                        LastReason = "Di chuyen bi ket";
                        return EnemyNavigationResult.ReturnHome;
                    }
                }
                else
                {
                    failures = 0;
                }

                ResetProgress();
            }

            agent.isStopped = false;
            return EnemyNavigationResult.Moving;
        }

        bool PathInsideTerritory()
        {
            int count = path.GetCornersNonAlloc(corners);
            if (count == corners.Length)
            {
                return false;
            }

            int samples = 0;
            for (int i = 0; i < count; i++)
            {
                if (!insideTerritory(corners[i]))
                {
                    return false;
                }

                if (i == 0)
                {
                    continue;
                }

                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(corners[i - 1], corners[i]) / .5f));
                if ((samples += steps) > MaximumTerritorySamples)
                {
                    return false;
                }

                for (int step = 1; step < steps; step++)
                {
                    if (!insideTerritory(Vector3.Lerp(corners[i - 1], corners[i], step / (float)steps)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool TryRetreatStep(Vector3 direction, float distance)
        {
            if (!Ready())
            {
                return false;
            }

            direction.y = 0;
            if (direction.sqrMagnitude < .001f)
            {
                return false;
            }

            Vector3 next = actor.position + direction.normalized * distance;
            if (!insideTerritory(next) || !NavMesh.SamplePosition(next, out var point, .2f, agent.areaMask) || NavMesh.Raycast(actor.position, point.position, out _, agent.areaMask))
            {
                return false;
            }

            if (capsule != null)
            {
                Vector3 center = actor.TransformPoint(capsule.center);
                Vector3 up = Vector3.up * Mathf.Max(0, capsule.height * .5f - capsule.radius);
                int count = Physics.CapsuleCastNonAlloc(center - up, center + up, capsule.radius * .9f, direction.normalized, hits, distance + .03f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                if (count == hits.Length)
                {
                    return false;
                }

                for (int i = 0; i < count; i++)
                {
                    if (!hits[i].transform.IsChildOf(actor))
                    {
                        return false;
                    }
                }
            }

            agent.Move(point.position - actor.position);
            return true;
        }

        public void Stop()
        {
            if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            agent.isStopped = true;
            if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }
    }
}
