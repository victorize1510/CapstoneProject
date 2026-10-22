using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetEvolutionVisualController : MonoBehaviour {
        [SerializeField] Transform visualRoot = null;
        [SerializeField] GameObject baseVisual = null;

        GameObject runtimeVisual;
        Renderer[] baseRenderers;
        Animator baseAnimator;

        void Awake() {
            if (visualRoot == null) visualRoot = transform;
            CacheBaseVisual();
        }

        void CacheBaseVisual() {
            if (baseRenderers != null) return;
            GameObject source = baseVisual != null ? baseVisual : gameObject;
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && renderer.GetComponentInParent<AAMAP.MapIcon>(true) == null)
                    renderers.Add(renderer);
            baseRenderers = renderers.ToArray();
            baseAnimator = source.GetComponent<Animator>();
            if (baseAnimator == null) baseAnimator = source.GetComponentInChildren<Animator>(true);
        }

        public bool IsCurrentVisualRenderer(Renderer renderer) {
            if (renderer == null) return false;
            if (runtimeVisual != null) return renderer.transform.IsChildOf(runtimeVisual.transform);
            CacheBaseVisual();
            return System.Array.IndexOf(baseRenderers, renderer) >= 0;
        }

        public bool TryApply(GameObject modelOnlyPrefab, out string error) {
            if (modelOnlyPrefab == null) {
                error = string.Empty;
                return true;
            }

            if (visualRoot == null) visualRoot = transform;
            CacheBaseVisual();

            GameObject nextVisual;
            try {
                nextVisual = Instantiate(modelOnlyPrefab, visualRoot);
            }
            catch (System.Exception exception) {
                error = $"Không tạo được model tiến hóa: {exception.Message}";
                return false;
            }
            if (nextVisual == null) {
                error = "Không tạo được model tiến hóa.";
                return false;
            }
            nextVisual.name = modelOnlyPrefab.name + " (Evolution Visual)";
            nextVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            nextVisual.transform.localScale = Vector3.one;
            Animator nextAnimator = nextVisual.GetComponentInChildren<Animator>(true);
            if (nextAnimator == null) {
                Destroy(nextVisual);
                error = "Model tiến hóa thiếu Animator.";
                return false;
            }
            if (runtimeVisual != null) {
                runtimeVisual.SetActive(false);
                Destroy(runtimeVisual);
            }
            runtimeVisual = nextVisual;
            PetController pet = GetComponentInParent<PetController>();
            foreach (Renderer renderer in baseRenderers) {
                if (renderer == null) continue;
                if (pet != null) pet.SetVisualRendererEnabled(renderer, false);
                else renderer.enabled = false;
            }
            if (baseAnimator != null) baseAnimator.enabled = false;
            BindAnimator(nextAnimator);
            pet?.RefreshVisualPresence();
            error = string.Empty;
            return true;
        }

        public void RestoreForm(int evolutionStage) {
            PetCollectionMetadata metadata = GetComponentInParent<PetCollectionMetadata>();
            GameObject previousPrefab = null;
            if (metadata != null && evolutionStage > 0) {
                var rules = metadata.EvolutionRules;
                for (int i = 0; i < rules.Count; i++) {
                    if (rules[i] != null && rules[i].TargetStage == evolutionStage) {
                        previousPrefab = rules[i].TargetVisualPrefab;
                        break;
                    }
                }
            }
            if (previousPrefab != null) {
                TryApply(previousPrefab, out _);
                return;
            }
            if (runtimeVisual != null) {
                runtimeVisual.SetActive(false);
                Destroy(runtimeVisual);
            }
            runtimeVisual = null;
            CacheBaseVisual();
            PetController pet = GetComponentInParent<PetController>();
            foreach (Renderer renderer in baseRenderers) {
                if (renderer == null) continue;
                if (pet != null) pet.SetVisualRendererEnabled(renderer, true);
                else renderer.enabled = true;
            }
            if (baseAnimator != null) baseAnimator.enabled = true;
            BindAnimator(baseAnimator);
            pet?.RefreshVisualPresence();
        }

        void BindAnimator(Animator selectedAnimator) {
            if (selectedAnimator == null) return;
            PetController pet = GetComponentInParent<PetController>();
            if (pet != null) pet.BindVisualAnimator(selectedAnimator);
            PetHudRuntimeStats stats = GetComponentInParent<PetHudRuntimeStats>();
            if (stats != null) stats.BindVisualAnimator(selectedAnimator);
        }
    }
}
