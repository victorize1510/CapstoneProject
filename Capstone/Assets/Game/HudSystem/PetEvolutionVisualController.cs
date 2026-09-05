using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetEvolutionVisualController : MonoBehaviour {
        [SerializeField] Transform visualRoot = null;
        [SerializeField] GameObject baseVisual = null;

        GameObject runtimeVisual;

        public bool TryApply(GameObject modelOnlyPrefab, out string error) {
            if (modelOnlyPrefab == null) {
                error = string.Empty;
                return true;
            }

            if (visualRoot == null) {
                error = "Chưa gán Visual Root cho PetEvolutionVisualController.";
                return false;
            }

            if (runtimeVisual != null) Destroy(runtimeVisual);
            if (baseVisual != null) baseVisual.SetActive(false);

            runtimeVisual = Instantiate(modelOnlyPrefab, visualRoot);
            runtimeVisual.name = modelOnlyPrefab.name + " (Evolution Visual)";
            runtimeVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            runtimeVisual.transform.localScale = Vector3.one;
            error = string.Empty;
            return true;
        }
    }
}
