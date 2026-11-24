using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    // Dung camera choi, khong dua vao Renderer.isVisible cua minimap/camera khac.
    public static class CameraEnemyVisibility
    {
        static readonly RaycastHit[] hits = new RaycastHit[32];
        public static bool CanSee(Camera camera, Transform viewer, DummyEnemy enemy, float padding = .02f)
        {
            if (camera == null || enemy == null || !enemy.isActiveAndEnabled || !enemy.IsSelectable) return false;
            Vector3 point = enemy.TargetPosition;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= camera.nearClipPlane || viewport.x < padding || viewport.x > 1 - padding
                || viewport.y < padding || viewport.y > 1 - padding) return false;
            Vector3 delta = point - camera.transform.position;
            int count = Physics.RaycastNonAlloc(camera.transform.position, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].transform;
                if (hit.IsChildOf(enemy.transform) || viewer != null && hit.IsChildOf(viewer)) continue;
                var pet = hits[i].collider.GetComponentInParent<PetController>();
                if (pet != null && !pet.IsWildEncounter && pet.owner == viewer) continue;
                return false;
            }
            return true;
        }
    }
}
