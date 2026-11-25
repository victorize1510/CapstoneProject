using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.SettingsSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem {
    [DisallowMultipleComponent]
    internal sealed class LocomotionSound : MonoBehaviour {
        enum Motion { Silent, Grass, Stone, Flying, Bee, Snake }
        static readonly Dictionary<int, (ChapterWorldLayout world, Bounds? rockBounds)> surfaces = new();
        static AudioClip[][] clips;
        BasicPlayerMovement player;
        PetController pet;
        DummyEnemy enemy;
        PetCollectionMetadata metadata;
        CreatureAnimationDriver animationDriver;
        ChapterWorldLayout world;
        Bounds? caveRockBounds;
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
        static void ResetSceneCache() {
            surfaces.Clear();
            clips = null;
            SceneManager.sceneUnloaded -= RemoveScene;
            SceneManager.sceneUnloaded += RemoveScene;
        }

        static void RemoveScene(Scene scene) => surfaces.Remove(scene.handle);

        void Start() {
            player = GetComponent<BasicPlayerMovement>();
            pet = GetComponent<PetController>();
            enemy = GetComponent<DummyEnemy>();
            metadata = GetComponentInChildren<PetCollectionMetadata>(true);
            animationDriver = GetComponent<CreatureAnimationDriver>();
            (world, caveRockBounds) = ResolveSceneSurface(gameObject.scene);
            clips ??= new[] {
                System.Array.Empty<AudioClip>(), Load("Di1", "Di2", "Di3"), Load("DiTrenDa"),
                Load("Bay"), Load("OngBay"), Load("Ran01", "Ran02", "Ran03")
            };
        }

        static (ChapterWorldLayout, Bounds?) ResolveSceneSurface(Scene scene) {
            if (surfaces.TryGetValue(scene.handle, out var existing)) return existing;
            ChapterWorldLayout layout = null;
            Transform cave = null;
            foreach (var root in scene.GetRootGameObjects()) {
                layout ??= root.GetComponentInChildren<ChapterWorldLayout>(true);
                if (layout != null) break;
                if (cave != null) continue;
                foreach (var node in root.GetComponentsInChildren<Transform>(true)) {
                    if (node.name != "MapCave") continue;
                    cave = node;
                    break;
                }
            }
            Bounds? rockBounds = null;
            if (layout == null && cave != null) {
                foreach (var collider in cave.GetComponentsInChildren<Collider>()) {
                    if (!collider.enabled || collider.isTrigger || collider.bounds.size.sqrMagnitude < .01f) continue;
                    var combined = rockBounds ?? collider.bounds;
                    combined.Encapsulate(collider.bounds);
                    rockBounds = combined;
                }
                if (rockBounds is Bounds combinedBounds) {
                    combinedBounds.Expand(new Vector3(2f, 6f, 2f));
                    rockBounds = combinedBounds;
                }
            }
            return surfaces[scene.handle] = (layout, rockBounds);
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
            bool cave = world != null ? world.InCave(transform.position)
                : caveRockBounds?.Contains(transform.position) == true
                    || gameObject.scene.name.IndexOf("cave", StringComparison.OrdinalIgnoreCase) >= 0;
            return cave ? Motion.Stone : Motion.Grass;
        }
    }
}
