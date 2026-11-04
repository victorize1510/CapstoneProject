using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CaptureSystem {
    [DefaultExecutionOrder(200), DisallowMultipleComponent, RequireComponent(typeof(PetController), typeof(DummyEnemy))]
    public sealed class WildPetCaptureTarget : MonoBehaviour {
        [SerializeField] bool encounterCaptureAllowed = true;
        PetController pet; DummyEnemy enemy; PetCollectionMetadata metadata; PetHudRuntimeStats stats;
        bool transferred;
        public PetController Pet => pet != null ? pet : pet = GetComponent<PetController>();
        public PetCollectionMetadata Metadata => metadata != null ? metadata : metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        public DummyEnemy Enemy => enemy != null ? enemy : enemy = GetComponent<DummyEnemy>();
        public PetHudRuntimeStats Stats => stats != null ? stats : stats = GetComponentInChildren<PetHudRuntimeStats>(true);
        internal bool StagedForSave { get; set; }
        public bool IsWild => !transferred && Pet != null && Pet.IsWildEncounter;
        public bool Allowed => encounterCaptureAllowed && Enemy != null && !Enemy.IsBoss;
        void Awake() {
            if (!IsWild) { if(Enemy!=null)Enemy.enabled=false; return; }
            if (Metadata == null || Stats == null) return;
            // HUD Awake may already have resolved legacy defaults. The dedicated spawn
            // guard, not that fallback flag, determines whether this individual was rolled.
            Metadata.InitializeWildIndividual();
            Metadata.SyncRuntimeHealth(Stats); Enemy.InitializeCaptureHealth(Stats.Health, Stats.MaxHealth);
        }
        public void SyncHealthFromEnemy() {
            if (IsWild && Stats != null) Stats.SetStatus(Enemy.CurrentHealth, Enemy.maxHealth, Stats.Energy, Stats.MaxEnergy);
        }
        internal void Lock(bool locked) { if (Enemy != null) Enemy.CaptureLocked = locked; }
        internal void Complete(Transform owner) {
            transferred = true; StagedForSave = false; Enemy.FinishCaptureWithoutDefeat(); Pet.FinishWildCapture(owner);
        }
        void OnDisable() { if (!transferred) Lock(false); }
    }
}
