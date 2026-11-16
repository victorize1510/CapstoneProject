using System;
using System.Collections.Generic;
using GDS.Core;
using UnityEngine;

namespace Capstone.Game.Inventory {
    /// <summary>Canonical definitions shared by save restoration, shops and rewards.</summary>
    public static class InventoryDefinitionCatalog {
        static readonly Dictionary<string, MonsterItemDefinition> ids = new Dictionary<string, MonsterItemDefinition>(StringComparer.OrdinalIgnoreCase);
        static readonly List<MonsterItemDefinition> definitions = new List<MonsterItemDefinition>();
        static bool loaded;
        public static IReadOnlyList<MonsterItemDefinition> Definitions { get { Load(); return definitions; } }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() { loaded = false; ids.Clear(); definitions.Clear(); }
        public static void Load() {
            if (loaded) return;
            loaded = true;
            var catalog = Resources.Load<ItemBaseCatalogSO>("Inventory/ItemCatalog");
            if (catalog == null) return;
            foreach (var entry in catalog.Items) {
                if (!(entry is MonsterItemDefinition item)) continue;
                if (ids.TryGetValue(item.StableId, out var previous)) {
                    if (previous != item) Debug.LogError("Trùng ID trong danh mục vật phẩm: " + item.StableId, catalog);
                    continue;
                }
                ids.Add(item.StableId, item); definitions.Add(item);
            }
        }
        public static MonsterItemDefinition Find(string id) {
            if (string.IsNullOrWhiteSpace(id)) return null;
            Load(); id = id.Trim();
            if (ids.TryGetValue(id, out var result)) return result;
            foreach (var item in definitions) {
                if (!item.MatchesId(id)) continue;
                if (result != null && result != item) { Debug.LogError("Alias vật phẩm bị trùng: " + id); return null; }
                result = item;
            }
            return result;
        }
    }
}
