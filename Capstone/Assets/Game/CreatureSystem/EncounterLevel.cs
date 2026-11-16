using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Scene-authored encounter initialization; never rerolls on enable, damage or capture.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent]
    public sealed class EncounterLevel : MonoBehaviour {
        public bool openingEncounter;
        bool initialized;
        public int RolledLevel { get; private set; }
        void Start() => Initialize();
        public void Initialize() {
            if (initialized) return;
            var enemy = GetComponent<DummyEnemy>();
            var pet = GetComponent<PetController>();
            var stats = GetComponent<PetHudRuntimeStats>();
            if (enemy == null || enemy.IsTrainingDummy || enemy.IsBoss || stats == null || (pet != null && !pet.IsWildEncounter)) return;
            initialized = true;
            RolledLevel = openingEncounter ? 5 : PveBalanceRules.EncounterLevel(Random.Range(0, 1000000) / 1000000d);
            stats.SetIdentity(stats.DisplayName, RolledLevel, stats.Icon);
            stats.SetStatus(stats.MaxHealth, stats.MaxHealth, 0, 0);
            enemy.InitializeCaptureHealth(stats.Health, stats.MaxHealth);
        }
    }
}
