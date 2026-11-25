using Capstone.Game.HudSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.SettingsSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    [DisallowMultipleComponent]
    internal sealed class LocomotionSound : MonoBehaviour {
        enum Motion { Silent, Grass, Stone, Flying, Bee, Snake }
        static AudioClip[][] clips;
        BasicPlayerMovement player;
        PetController pet;
        DummyEnemy enemy;
        PetCollectionMetadata metadata;
        CreatureAnimationDriver animationDriver;
        AudioSource source;
        Vector3 lastPosition;
        Motion previous;
        float nextStep;
        float lastValidMotion;
        int step;

        internal static void Attach(GameObject actor) {
            if (actor.GetComponent<LocomotionSound>() == null) actor.AddComponent<LocomotionSound>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetClips() => clips = null;

        void Start() {
            player = GetComponent<BasicPlayerMovement>();
            pet = GetComponent<PetController>();
            enemy = GetComponent<DummyEnemy>();
            metadata = GetComponentInChildren<PetCollectionMetadata>(true);
            animationDriver = GetComponent<CreatureAnimationDriver>();
            clips ??= new[] {
                System.Array.Empty<AudioClip>(), Load("Di1", "Di2", "Di3"), Load("DiTrenDa"),
                Load("Bay"), Load("OngBay"), Load("Ran01", "Ran02", "Ran03")
            };
        }

        static AudioClip[] Load(params string[] names) =>
            System.Array.ConvertAll(names, clip => Resources.Load<AudioClip>("Audio/Locomotion/" + clip));

        void OnEnable() => lastPosition = transform.position;
        void OnDisable() => Stop();

        void Stop() {
            if (source != null && source.isPlaying) source.Stop();
            previous = Motion.Silent;
        }

        void LateUpdate() {
            Vector3 change = transform.position - lastPosition;
            lastPosition = transform.position;
            float planar = change.x * change.x + change.z * change.z;
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0 || change.sqrMagnitude > 4f ||
                (enemy != null && (!enemy.IsAlive || enemy.CaptureLocked)) ||
                (enemy == null && pet != null && !pet.CanReceiveCommands) ||
                (player != null && !player.IsGroundedForPetThrow)) {
                Stop();
                return;
            }
            if (planar < .01f * deltaTime * deltaTime) {
                if (previous != Motion.Stone || Time.time - lastValidMotion > .12f) Stop();
                return;
            }
            lastValidMotion = Time.time;

            Motion current = ResolveMotion();
            if (current == Motion.Silent) {
                Stop();
                return;
            }

            if (source == null) {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = player != null ? 0 : 1;
                source.maxDistance = 14;
                GameAudioRouting.Route(source, GameAudioBus.SFX);
            }

            if (current != previous) {
                source.Stop();
                step = 0;
                nextStep = 0;
                previous = current;
                source.volume = player == null ? .25f : current == Motion.Stone ? .52f : .32f;
            }

            // DiTrenDa is a sequence of steps. Let it play through while moving.
            bool looping = current == Motion.Flying || current == Motion.Bee || current == Motion.Stone;
            if (Time.time < nextStep || (looping && source.isPlaying)) return;
            var set = clips[(int)current];
            AudioClip clip = set[step++ % set.Length];
            if (clip == null) return;
            source.loop = looping;
            source.clip = clip;
            source.Play();
            float speed = Mathf.Sqrt(planar) / deltaTime;
            nextStep = Time.time + (current == Motion.Snake ? clip.length : Mathf.Lerp(.56f, .32f, Mathf.InverseLerp(1f, 6f, speed)));
        }

        Motion ResolveMotion() {
            string family = metadata != null ? metadata.DefinitionId : null;
            if (family == "ghost") return Motion.Silent;
            if (family == "sun-blossom") return metadata.FormCode == "01" ? Motion.Silent : Motion.Bee;
            if (family == "snake-fire") return Motion.Snake;
            if (animationDriver != null && animationDriver.IsPlaying(CreatureAction.Fly)) return Motion.Flying;
            bool cave = ChapterWorldLayout.IsCaveRegion(gameObject.scene, transform.position);
            return cave ? Motion.Stone : Motion.Grass;
        }
    }
}
