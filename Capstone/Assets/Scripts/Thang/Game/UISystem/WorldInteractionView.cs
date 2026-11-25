using UnityEngine;

namespace Capstone.Game.UISystem {
    // One visibility rule for both the prompt and the command. Camera orbit never grants sight.
    public static class WorldInteractionView {
        public const float DefaultViewAngle = 100f;
        static readonly RaycastHit[] Hits = new RaycastHit[32];
        static int consumedFrame = -1;

        public static bool CanInteract(Transform player, Transform target, float distance,
            float viewAngle = DefaultViewAngle, LayerMask obstructionLayers = default) {
            if (player == null || target == null || !target.gameObject.activeInHierarchy) return false;
            Vector3 delta = target.position - player.position;
            if (delta.sqrMagnitude > distance * distance) return false;

            Transform facing = player;
            if (player.TryGetComponent(out PlayerVisualSwitcher switcher) && switcher.ActiveVisualRoot != null)
                facing = switcher.ActiveVisualRoot.transform;
            Vector3 forward = Vector3.ProjectOnPlane(facing.forward, Vector3.up).normalized;
            Vector3 direction = Vector3.ProjectOnPlane(delta, Vector3.up);
            // A target directly beneath the player is still reachable (ground loot).
            if (direction.sqrMagnitude > .0025f && Vector3.Dot(forward, direction.normalized)
                < Mathf.Cos(Mathf.Clamp(viewAngle, 1f, 180f) * .5f * Mathf.Deg2Rad)) return false;

            Vector3 eye = player.position + Vector3.up * 1.5f;
            if (player.TryGetComponent(out CharacterController controller))
                eye = player.TransformPoint(controller.center + Vector3.up * controller.height * .35f);
            Vector3 point = target.position + Vector3.up * .1f;
            if (target.TryGetComponent(out Collider targetCollider)) point = targetCollider.bounds.center;
            Vector3 ray = point - eye;
            if (ray.sqrMagnitude < .0001f) return true;
            int mask = obstructionLayers.value == 0 ? Physics.DefaultRaycastLayers : obstructionLayers.value;
            int count = Physics.RaycastNonAlloc(eye, ray.normalized, Hits, ray.magnitude, mask, QueryTriggerInteraction.Ignore);
            // A crowded query must never silently allow interaction through an unreported wall.
            if (count == Hits.Length) return false;
            for (int i = 0; i < count; i++) {
                Transform hit = Hits[i].collider.transform;
                if (hit == player || hit.IsChildOf(player) || hit == target || hit.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }

        public static bool TryConsumePress() {
            // Multiple listeners may observe the same E press; only one executes it.
            if (consumedFrame == Time.frameCount || !PlayerActionGate.TryAccept("world-interact")) return false;
            consumedFrame = Time.frameCount;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => consumedFrame = -1;
    }
}
