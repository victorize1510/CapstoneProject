using System.Collections;
using Capstone.Game.PetSummon;
using UnityEngine;
namespace Capstone.Game.CreatureSystem {
    // Trinh dien thu khan cap, khong hoi HP hay doi slot.
    public sealed class PetDefeatRecall : MonoBehaviour {
        public static void Play(PetController pet, CreatureAnimationDriver driver) {
            var host = new GameObject("Emergency pet recall");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(host, pet.gameObject.scene);
            var recall = host.AddComponent<PetDefeatRecall>();
            recall.StartCoroutine(recall.Run(pet, driver));
        }
        IEnumerator Run(PetController pet, CreatureAnimationDriver driver) {
            var anchor = new GameObject("Emergency recall destination");
            anchor.transform.SetParent(transform, false);
            PetSummonReleaseEffect effect = null;
            try {
                var cry = driver != null ? driver.Cry : null;
                if (cry != null && cry.isActiveAndEnabled) {
                    while (pet != null && cry != null && cry.isActiveAndEnabled
                        && (UISystem.GameplayPause.IsPaused || !cry.DeathReady))
                        yield return null;
                } else if (driver != null && driver.TryGetActionHash(CreatureAction.Death, out int hash)) {
                    float deadline = Time.time + 1.5f;
                    var animator = driver.BoundAnimator;
                    while (pet != null && animator != null && animator.isActiveAndEnabled && Time.time < deadline) {
                        var state = animator.GetCurrentAnimatorStateInfo(0);
                        if (state.fullPathHash == hash && state.normalizedTime >= .72f) break;
                        yield return null;
                    }
                }
                if (pet == null) yield break;
                var ownerEffect = pet.owner != null ? pet.owner.GetComponent<PetSummonReleaseEffect>() : null;
                anchor.SetActive(false);
                effect = anchor.AddComponent<PetSummonReleaseEffect>();
                effect.ConfigureProfile(ownerEffect != null ? ownerEffect.Profile : null);
                effect.ConfigureQuality(ownerEffect != null ? ownerEffect.Quality : PetVFXQuality.Medium);
                anchor.SetActive(true);
                anchor.transform.position = (pet.owner != null ? pet.owner.position : pet.transform.position) + Vector3.up;
                yield return effect.PlayRecall(pet, anchor.transform, null);
            } finally {
                effect?.Cancel();
                if (pet != null) pet.FinishDefeatRecall();
                Destroy(gameObject);
            }
        }
    }
}
