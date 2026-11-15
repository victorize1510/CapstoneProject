using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    public enum CreatureRole { OwnedPet, Wild, Enemy }
    /// <summary>Initial visual/health wiring only. Enemy decision AI is deliberately not implemented here.</summary>
    [DisallowMultipleComponent]
    public sealed class CreatureRolePresentation : MonoBehaviour {
        [Tooltip("Spawn presentation preset, not ownership authority. Captured individuals use PetController.IsWildEncounter/party state. Enemy AI is not part of this pilot.")]
        public CreatureRole role;
        CreatureAnimationDriver driver;
        DummyEnemy enemy;
        void Awake() {
            driver = GetComponent<CreatureAnimationDriver>();
            var animator = GetComponentInChildren<Animator>(true);
            driver?.Bind(animator);
            GetComponent<PetHudRuntimeStats>()?.BindVisualAnimator(animator);
            enemy = GetComponent<DummyEnemy>();
        }
        void OnEnable() { if (enemy != null) enemy.Defeated += Defeated; }
        void OnDisable() { if (enemy != null) enemy.Defeated -= Defeated; }
        void Start() {
            if (role == CreatureRole.OwnedPet) return;
            var stats = GetComponent<PetHudRuntimeStats>();
            if (enemy != null && stats != null) {
                if (enemy.IsTrainingDummy) {
                    enemy.InitializeCaptureHealth(enemy.maxHealth, enemy.maxHealth);
                    stats.SetStatus(enemy.maxHealth, enemy.maxHealth, stats.Energy, stats.MaxEnergy);
                } else enemy.InitializeCaptureHealth(stats.Health, stats.MaxHealth);
            }
            driver?.TryPlay(CreatureAction.Idle);
        }
        void Defeated(GameObject source) { driver?.TryPlay(CreatureAction.Death); }
    }
}
