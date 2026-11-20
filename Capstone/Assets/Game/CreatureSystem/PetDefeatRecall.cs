using System.Collections;
using Capstone.Game.PetSummon;
using UnityEngine;
namespace Capstone.Game.CreatureSystem {
    // Trinh dien thu khan cap, khong hoi HP hay doi slot.
    public sealed class PetDefeatRecall : MonoBehaviour {
        public static void Play(PetController pet) {
            var host=new GameObject("Emergency pet recall");
            host.AddComponent<PetDefeatRecall>().StartCoroutine(host.GetComponent<PetDefeatRecall>().Run(pet));
        }
        IEnumerator Run(PetController pet) {
            var anchor=new GameObject("Emergency recall destination");
            var effect=gameObject.AddComponent<PetSummonReleaseEffect>();
            effect.ConfigureProfile(pet.owner!=null?pet.owner.GetComponent<PetSummonReleaseEffect>()?.Profile:null);
            try {
                anchor.transform.position=pet.owner!=null?pet.owner.position+Vector3.up:pet.transform.position+Vector3.up;
                yield return effect.PlayRecall(pet,anchor.transform,()=>{if(pet!=null)pet.FinishDefeatRecall();},.25f);
            } finally {
                effect.Cancel();if(pet!=null)pet.FinishDefeatRecall();Destroy(anchor);Destroy(gameObject);
            }
        }
        void OnDisable(){GetComponent<PetSummonReleaseEffect>()?.Cancel();}
    }
}
