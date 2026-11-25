using UnityEngine;
using Capstone.Game.HudSystem;

namespace Capstone.Game.CaptureSystem
{
    public static class WildCaptureTargeting
    {
        public readonly struct Selection
        {
            public readonly DummyEnemy Enemy;
            public readonly WildPetCaptureTarget Target;
            public readonly CaptureFailure Failure;
            internal Selection(DummyEnemy enemy, WildPetCaptureTarget target, CaptureFailure failure)
            { Enemy = enemy; Target = target; Failure = failure; }
        }

        static readonly RaycastHit[] hits = new RaycastHit[48];
        static readonly Selection empty = new Selection(null, null, CaptureFailure.NoTarget);

        public static WildPetCaptureTarget Find(PetCommandInput player, BasicCameraFollow camera)
        {
            var selection = Preview(player, camera, false, null);
            return selection.Failure == CaptureFailure.None ? selection.Target : null;
        }

        internal static Camera View(PetCommandInput player, BasicCameraFollow camera)
            => camera != null && camera.TryGetComponent<Camera>(out var result) ? result
                : player != null && player.commandCamera != null ? player.commandCamera : Camera.main;

        public static Selection Preview(PetCommandInput player, BasicCameraFollow follow, bool aim, DummyEnemy previous)
        {
            var view = View(player, follow);
            if (player == null || view == null) return empty;
            if (!aim)
            {
                if (follow != null && follow.TryGetLockedEnemy(out var locked)) return Check(player, view, locked, false);
                if (player.SelectedEnemy != null) return Check(player, view, player.SelectedEnemy, false);
            }
            else
            {
                var ray = view.ViewportPointToRay(new Vector3(.5f, .5f));
                var direct = FirstHit(ray, Mathf.Max(35f, Range + 12), player.transform);
                if (direct != null && direct.GetComponentInParent<DummyEnemy>() is DummyEnemy enemy)
                    return Check(player, view, enemy, true);
            }

            const float holdMargin = .016f;
            float radius = aim ? .048f : .32f;
            float bestScore = float.PositiveInfinity;
            Selection best = empty, invalid = empty;
            float invalidScore = float.PositiveInfinity;
            foreach (var enemy in DummyEnemy.ActiveEnemies)
            {
                if (enemy == null || !enemy.IsSelectable) continue;
                Vector3 point = view.WorldToViewportPoint(enemy.TargetPosition);
                if (point.z <= view.nearClipPlane) continue;
                float centered = new Vector2((point.x - .5f) * view.aspect, point.y - .5f).magnitude;
                if (centered > radius + (enemy == previous ? holdMargin : 0)) continue;
                float score = centered - (enemy == previous ? holdMargin : 0) + Mathf.Min(point.z, 50) * .0003f;
                var result = Check(player, view, enemy, aim);
                if (result.Failure != CaptureFailure.None)
                {
                    if (score < invalidScore)
                    {
                        invalid = result;
                        invalidScore = score;
                    }
                    continue;
                }
                if (score < bestScore || Mathf.Approximately(score, bestScore)
                    && best.Enemy != null && enemy.GetInstanceID() < best.Enemy.GetInstanceID())
                {
                    best = result;
                    bestScore = score;
                }
            }
            return best.Enemy != null ? best : invalid;
        }

        static float Range => CaptureAssets.Settings != null ? Mathf.Max(1, CaptureAssets.Settings.targetRange) : 15;

        internal static Selection Check(PetCommandInput player, Camera view, DummyEnemy enemy, bool aim)
        {
            if (player == null || view == null || enemy == null || !enemy.isActiveAndEnabled) return empty;
            var target = enemy.GetComponent<WildPetCaptureTarget>();
            CaptureFailure failure;
            if (target == null || !target.IsWild) failure = CaptureFailure.NotWild;
            else if (!target.Allowed) failure = CaptureFailure.ProtectedTarget;
            else if (!enemy.IsAlive && !enemy.IsDownedAvailable) failure = CaptureFailure.Defeated;
            else if (enemy.CaptureLocked) failure = CaptureFailure.Busy;
            else if (target.Metadata == null || target.Stats == null
                || !PetStatCalculator.TryGetProfile(target.Metadata, out var profile)) failure = CaptureFailure.MissingConfiguration;
            else if (!profile.IsCatchable) failure = CaptureFailure.ProtectedTarget;
            else
            {
                Vector3 origin = player.transform.position + Vector3.up;
                Vector3 delta = enemy.TargetPosition - origin;
                Vector3 viewport = view.WorldToViewportPoint(enemy.TargetPosition);
                float range = Range;
                if (delta.sqrMagnitude > range * range) failure = CaptureFailure.OutOfRange;
                else if (viewport.z <= view.nearClipPlane || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1
                    || !aim && Vector3.Dot(player.transform.forward, Vector3.ProjectOnPlane(delta, Vector3.up)) < 0)
                    failure = CaptureFailure.OutOfView;
                else if (Blocked(origin, enemy, player.transform) || Blocked(view.transform.position, enemy, player.transform))
                    failure = CaptureFailure.Obstructed;
                else failure = CaptureFailure.None;
            }
            return new Selection(enemy, target, failure);
        }

        static bool Blocked(Vector3 origin, DummyEnemy enemy, Transform viewer)
        {
            Vector3 delta = enemy.TargetPosition - origin;
            Collider hit = FirstHit(new Ray(origin, delta.normalized), delta.magnitude, viewer);
            return hit != null && !hit.transform.IsChildOf(enemy.transform);
        }

        static Collider FirstHit(Ray ray, float length, Transform viewer)
        {
            int count = Physics.RaycastNonAlloc(ray, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            var candidates = count == hits.Length
                ? Physics.RaycastAll(ray, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) : hits;
            if (candidates != hits) count = candidates.Length;
            float nearest = float.PositiveInfinity;
            Collider closest = null;
            for (int i = 0; i < count; i++)
            {
                var candidate = candidates[i].collider;
                if (candidate == null || candidates[i].distance >= nearest || candidate.transform.IsChildOf(viewer)) continue;
                var pet = candidate.GetComponentInParent<PetController>();
                if (pet != null && !pet.IsWildEncounter && pet.owner == viewer) continue;
                nearest = candidates[i].distance;
                closest = candidate;
            }
            return closest;
        }
    }
}
