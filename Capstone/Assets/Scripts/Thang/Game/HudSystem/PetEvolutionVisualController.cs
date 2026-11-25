using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetEvolutionVisualController : MonoBehaviour {
        [SerializeField] Transform visualRoot = null;
        [SerializeField] GameObject baseVisual = null;
        [SerializeField] GameObject baseVisualPrefab = null;

        GameObject runtimeVisual;
        GameObject runtimePrefab;
        Renderer[] baseRenderers;
        Animator baseAnimator;
        public Transform ActiveVisualTransform => runtimeVisual != null ? runtimeVisual.transform
        : (baseVisual != null ? baseVisual.transform : transform);
        public Animator ActiveAnimator => runtimeVisual != null ? runtimeVisual.GetComponentInChildren<Animator>(true)
        : baseAnimator;

        void Awake() {
            if (visualRoot == null) visualRoot = transform;
            CacheBaseVisual();
        }

        void CacheBaseVisual(bool refresh = false) {
            if (baseRenderers != null && !refresh) return;
            GameObject source = baseVisual != null ? baseVisual : gameObject;
            var renderers = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
                if (renderer != null && renderer.GetComponentInParent<AAMAP.MapIcon>(true) == null
                    && (runtimeVisual == null || !renderer.transform.IsChildOf(runtimeVisual.transform)))
                    renderers.Add(renderer);
            baseRenderers = renderers.ToArray();
            baseAnimator = source.GetComponent<Animator>();
            if (baseAnimator == null)
                foreach (var candidate in source.GetComponentsInChildren<Animator>(true))
                    if (runtimeVisual == null || !candidate.transform.IsChildOf(runtimeVisual.transform)) {
                        baseAnimator = candidate;
                        break;
                    }
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

            // Nested scene/prefab visuals may not have been present in the first
            // Awake cache. Refresh only at form/reveal boundaries, never per frame.
            CacheBaseVisual(true);

            if (runtimeVisual != null && runtimePrefab == modelOnlyPrefab) {
                // A dormant scene actor can finish Awake after the save was applied.
                // Repair bindings/presence as well as the mesh on the next reveal.
                ActivateRuntimeVisual();
                error = string.Empty;
                return true;
            }
            if (baseVisualPrefab != null && baseVisualPrefab == modelOnlyPrefab) { RestoreBaseVisual(); error = string.Empty; return true; }

            if (visualRoot == null) visualRoot = transform;
            CacheBaseVisual();

            GameObject nextVisual;
            try {
                // Keep the destination form's authored local scale under the actor's visual root.
                nextVisual = Instantiate(modelOnlyPrefab, visualRoot, false);
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
            Animator nextAnimator = nextVisual.GetComponentInChildren<Animator>(true);
            if (nextAnimator == null) {
                Destroy(nextVisual);
                error = "Model tiến hóa thiếu Animator.";
                return false;
            }
            if (runtimeVisual != null) {
                GetComponentInParent<PetHudRuntimeStats>()?.GetComponent<Capstone.Game.Skills.PetSkillPresentationPlayer>()?.CancelPresentation();
                runtimeVisual.SetActive(false);
                Destroy(runtimeVisual);
            }
            runtimeVisual = nextVisual;
            runtimePrefab = modelOnlyPrefab;
            ActivateRuntimeVisual();
            error = string.Empty;
            return true;
        }

        void ActivateRuntimeVisual() {
            CacheBaseVisual();
            PetController pet = GetComponentInParent<PetController>();
            foreach (Renderer renderer in baseRenderers) {
                if (renderer == null) continue;
                if (pet != null) pet.SetVisualRendererEnabled(renderer, false);
                else renderer.enabled = false;
            }
            if (baseAnimator != null) baseAnimator.enabled = false;
            var nextAnimator = runtimeVisual.GetComponentInChildren<Animator>(true);
            // Presence may have disabled this Animator during save hydration.
            if (nextAnimator != null) nextAnimator.enabled = true;
            BindAnimator(nextAnimator);
            pet?.RefreshVisualPresence();
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
            RestoreBaseVisual();
        }

        void RestoreBaseVisual() {
            GetComponentInParent<PetHudRuntimeStats>()?.GetComponent<Capstone.Game.Skills.PetSkillPresentationPlayer>()?.CancelPresentation();
            CacheBaseVisual(true);
            if (runtimeVisual != null) {
                runtimeVisual.SetActive(false);
                Destroy(runtimeVisual);
            }
            runtimeVisual = null;
            runtimePrefab = null;
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
