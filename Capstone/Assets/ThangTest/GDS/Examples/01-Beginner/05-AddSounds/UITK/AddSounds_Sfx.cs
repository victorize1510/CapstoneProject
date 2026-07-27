using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Examples {

    [RequireComponent(typeof(AudioSource))]
    public class AddSounds_Sfx : MonoBehaviour {
        [Header("Sounds")]
        [SerializeField] AudioClip Fail;
        [SerializeField] AudioClip Pick;
        [SerializeField] AudioClip Place;

        IStore store;
        AudioSource audioSource;

        void Awake() {
            store = StoreLocator.Get();
            audioSource = GetComponent<AudioSource>();
        }
        // Subscribe to all events
        void OnEnable() { store.Bus.OnAny<Result>(PlaySound); }
        void OnDisable() { store.Bus.OffAny<Result>(PlaySound); }

        // Play the event audio clip (if defined)
        void PlaySound(Result result) {
            var clip = GetClip(result);
            if (clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        // Map an event to its corresponding audio clip
        AudioClip GetClip(Result result) => result switch {
            Core.Events.Fail => Fail,
            PickItemSuccess => Pick,
            PlaceItemSuccess => Place,
            _ => null
        };

    }

}