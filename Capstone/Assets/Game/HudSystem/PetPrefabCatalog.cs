using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [CreateAssetMenu(menuName = "Game/Pets/Pet Prefab Catalog", fileName = "PetPrefabCatalog")]
    public sealed class PetPrefabCatalog : ScriptableObject {
        [Serializable]
        public sealed class Entry {
            [SerializeField] string definitionId = string.Empty;
            [SerializeField] string formId = string.Empty;
            [SerializeField] PetController prefab = null;

            public string DefinitionId => definitionId?.Trim() ?? string.Empty;
            public string FormId => formId?.Trim() ?? string.Empty;
            public PetController Prefab => prefab;
        }

        [SerializeField] List<Entry> entries = new List<Entry>();

        public bool TryInstantiate(string definitionId, Transform parent, out PetController pet) {
            return TryInstantiate(definitionId, string.Empty, parent, out pet);
        }

        public bool TryInstantiate(string definitionId, string formId, Transform parent, out PetController pet) {
            pet = null;
            if (!TryGetPrefab(definitionId, formId, out PetController prefab)) return false;
            pet = Instantiate(prefab, parent);
            return pet != null;
        }

        public bool TryGetPrefab(string definitionId, out PetController pet) {
            return TryGetPrefab(definitionId, string.Empty, out pet);
        }

        public bool TryGetPrefab(string definitionId, string formId, out PetController pet) {
            pet = null;
            if (string.IsNullOrWhiteSpace(definitionId)) return false;

            string expected = CanonicalDefinitionId(definitionId.Trim());
            string expectedForm = CanonicalFormId(formId?.Trim() ?? string.Empty);
            if (expectedForm.Length == 0 && string.Equals(expected, "Dragon Dusk",
                StringComparison.OrdinalIgnoreCase)) expectedForm = "01";
            PetController fallback = null;
            for (int i = 0; i < entries.Count; i++) {
                Entry entry = entries[i];
                if (entry == null || entry.Prefab == null
                    || !string.Equals(CanonicalDefinitionId(entry.DefinitionId), expected,
                        StringComparison.OrdinalIgnoreCase)) continue;

                if (string.Equals(entry.FormId, expectedForm, StringComparison.OrdinalIgnoreCase)) {
                    if (pet != null && pet != entry.Prefab) { pet = null; return false; }
                    pet = entry.Prefab;
                }
                else if (entry.FormId.Length == 0) fallback = entry.Prefab;
            }

            if (pet == null) pet = fallback;
            return pet != null;
        }

        void OnValidate() {
            entries.RemoveAll(entry => entry == null);
        }

        static string CanonicalDefinitionId(string value) {
            if (string.Equals(value, "Dragon Dusk Winged", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Dragon Dusk Elder", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Dragon Nightfall", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "Dragon Darkness", StringComparison.OrdinalIgnoreCase)) return "Dragon Dusk";
            return value;
        }

        static string CanonicalFormId(string value) {
            if (string.Equals(value, "base", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "dragon-dusk-base", StringComparison.OrdinalIgnoreCase)) return "01";
            if (string.Equals(value, "dragon-dusk-winged", StringComparison.OrdinalIgnoreCase)) return "02";
            if (string.Equals(value, "dragon-dusk-elder", StringComparison.OrdinalIgnoreCase)) return "03";
            return value;
        }
    }
}
