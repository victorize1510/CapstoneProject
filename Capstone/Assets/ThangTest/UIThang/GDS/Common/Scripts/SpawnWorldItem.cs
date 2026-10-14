using UnityEngine;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Common {

    /// <summary>
    /// Spawns an item prefab on the ground when it is dropped.
    /// Requires player position (Defaults to world center).
    /// Despawns (destroys) the game object when it is clicked.
    /// </summary>
    public class SpawnWorldItem : MonoBehaviour {
        [Tooltip("Wraps the item prefab. WorldItem is used to pick the item back from the world.")]
        [SerializeField] GameObject WrapperPrefab;

        [Header("Spawn")]
        [SerializeField] Transform spawnPoint;
        [SerializeField, Range(0, 5)] float spawnRadius;
        [SerializeField] Vector3 spawnOffset;

        IStore store;

        void Awake() { store = StoreLocator.Get(); }

        private void OnEnable() {
            store.Bus.On<Core.Events.SpawnWorldItem>(SpawnItem);
            store.Bus.On<DespawnWorldItem>(DespawnItem);
        }

        private void OnDisable() {
            store.Bus.Off<Core.Events.SpawnWorldItem>(SpawnItem);
            store.Bus.Off<DespawnWorldItem>(DespawnItem);
        }

        // Spawn item handler
        // Spawns an item prefab at target location
        // Register on click on the world item to allow despawning
        void SpawnItem(Core.Events.SpawnWorldItem e) {
            var pos = spawnPoint == null ? e.Pos : spawnPoint.position;
            pos = pos + RandomPointOnCircle(spawnRadius) + spawnOffset;

            var initialItemRotation = new Vector3(0, 145, 0);
            var instance = Instantiate(WrapperPrefab, pos, Quaternion.Euler(initialItemRotation));
            var worldItem = instance.GetComponent<IWorldItem>();
            if (worldItem == null) { Debug.LogWarning("IWorldItem component is required!", this); return; }

            worldItem.Item = e.Item;
            worldItem.OnClick += OnWorldItemClick;
            store.Bus.Publish(new DropWorldItemSuccess(e.Item));
        }

        // Despawn item handler
        void DespawnItem(DespawnWorldItem e) {
            e.WorldItem.Despawn();
            store.Bus.Publish(new PickWorldItemSuccess(e.WorldItem));
        }

        void OnWorldItemClick(IWorldItem worldItem) {
            // Do not pickup item if ghost is not empty
            // TODO: should this be handled in drag service instead?
            if (!store.Ghost.Empty) return;
            store.Bus.Publish(new PickWorldItem(worldItem));
        }

        Vector3 RandomPointOnCircle(float radius) {
            Vector2 p = Random.insideUnitCircle.normalized * radius;
            return new Vector3(p.x, 0, p.y);
        }

    }

}