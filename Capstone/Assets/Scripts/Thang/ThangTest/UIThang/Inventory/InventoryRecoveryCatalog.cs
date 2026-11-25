using UnityEngine;

namespace Capstone.Game.Inventory {
    /// <summary>Loads the explicitly approved sample definitions before inventory save restore.</summary>
    [CreateAssetMenu(menuName="Game/Inventory/Recovery Catalog")]
    public sealed class InventoryRecoveryCatalog : ScriptableObject {
        public MonsterItemDefinition[] items = System.Array.Empty<MonsterItemDefinition>();
        static InventoryRecoveryCatalog cached;
        public static InventoryRecoveryCatalog Load() => cached != null ? cached : cached = Resources.Load<InventoryRecoveryCatalog>("Inventory/RecoveryItems");
    }
}
