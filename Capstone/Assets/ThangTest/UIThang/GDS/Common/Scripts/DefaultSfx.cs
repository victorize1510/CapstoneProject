using UnityEngine;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Common {

    [RequireComponent(typeof(AudioSource))]
    public class DefaultSfx : MonoBehaviour {

        [Header("Sounds")]
        [SerializeField] AudioClip Fail;
        [SerializeField] AudioClip Pick;
        [SerializeField] AudioClip Place;
        [SerializeField] AudioClip Buy;
        [SerializeField] AudioClip Sell;
        [SerializeField] AudioClip Craft;
        [SerializeField] AudioClip Rotate;

        IStore store;
        AudioSource audioSource;

        // Get the store reference from the service locatar on awake
        void Awake() {
            store = StoreLocator.Get();
            audioSource = GetComponent<AudioSource>();
        }

        // Listen to all events
        void OnEnable() { store.Bus.OnAny<CustomEvent>(PlaySound); }
        void OnDisable() { store.Bus.OffAny<CustomEvent>(PlaySound); }

        // Play a clip associated with the event, if available
        // Randomize the pitch
        void PlaySound(CustomEvent result) {
            var clip = GetClip(result);
            if (clip == null) return;
            audioSource.pitch = Random.Range(0.85f, 1.05f);
            audioSource.PlayOneShot(clip);
        }

        // Map events to audio clips
        // This could be a dictionary
        AudioClip GetClip(CustomEvent result) => result switch {
            Core.Events.Fail => Fail,
            DropWorldItemSuccess => Place,
            PickWorldItemSuccess => Pick,
            BuyItemSuccess => Buy,
            SellItemSuccess => Sell,
            CraftItemSuccess => Craft,
            PickItemSuccess => Pick,
            PlaceItemSuccess => Place,
            RotateGhostItem => Rotate,
            _ => null
        };

    }

}