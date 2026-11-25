
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Demos.Basic {

    [RequireComponent(typeof(AudioSource))]
    public class Basic_Sfx : MonoBehaviour {

        [Header("Sounds")]
        [SerializeField] AudioClip Fail;
        [SerializeField] AudioClip Pick;
        [SerializeField] AudioClip Place;
        [SerializeField] AudioClip Buy;
        [SerializeField] AudioClip Sell;
        [SerializeField] AudioClip Craft;

        IStore store;
        AudioSource audioSource;

        void Awake() {
            store = StoreLocator.Get();
            audioSource = GetComponent<AudioSource>();
        }

        void OnEnable() { store.Bus.OnAny<Result>(PlaySound); }
        void OnDisable() { store.Bus.OffAny<Result>(PlaySound); }

        void PlaySound(Result result) {
            var clip = GetClip(result);
            if (clip == null) return;
            audioSource.pitch = Random.Range(0.85f, 1.05f);
            audioSource.PlayOneShot(clip);
        }

        AudioClip GetClip(Result result) => result switch {
            Core.Events.Fail => Fail,
            DropWorldItemSuccess => Place,
            PickWorldItemSuccess => Pick,
            BuyItemSuccess => Buy,
            SellItemSuccess => Sell,
            CraftItemSuccess => Craft,
            PickItemSuccess => Pick,
            PlaceItemSuccess => Place,
            _ => null
        };

    }

}