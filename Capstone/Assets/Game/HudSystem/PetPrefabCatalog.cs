using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [CreateAssetMenu(menuName = "Game/Pets/Pet Prefab Catalog", fileName = "PetPrefabCatalog")]
    public sealed class PetPrefabCatalog : ScriptableObject {
        [Serializable]
        public sealed class Entry {
            [SerializeField] string definitionId = string.Empty;
            [SerializeField] PetController prefab = null;

            public string DefinitionId => definitionId?.Trim() ?? string.Empty;
            public PetController Prefab => prefab;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public bool TryInstantiate(string definitionId, Transform parent, out PetController pet) {
            pet = null;
            if (!TryGetPrefab(definitionId, out PetController prefab)) return false;
            pet = Instantiate(prefab, parent);
            return pet != null;
        }

        public bool TryGetPrefab(string definitionId, out PetController pet) {
            pet = null;
            if (string.IsNullOrWhiteSpace(definitionId)) return false;

            string expected = definitionId.Trim();
            for (int i = 0; i < entries.Count; i++) {
                Entry entry = entries[i];
                if (entry == null || entry.Prefab == null
                    || !string.Equals(entry.DefinitionId, expected, StringComparison.OrdinalIgnoreCase)) continue;

                if (pet != null && pet != entry.Prefab) { pet = null; return false; }
                pet = entry.Prefab;
            }

            return pet != null;
        }

        void OnValidate() {
            entries.RemoveAll(entry => entry == null);
        }
    }
}
