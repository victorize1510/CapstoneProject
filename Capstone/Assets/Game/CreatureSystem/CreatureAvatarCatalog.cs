using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Generated lightweight projection. Contains sprites and keys, never prefabs, stat profiles or skill sets.</summary>
    public sealed class CreatureAvatarCatalog : ScriptableObject {
        [Serializable]
        public sealed class Entry {
            public string definitionId;
            public string[] aliases = Array.Empty<string>();
            public string formId;
            public Sprite avatar;
        }
        public List<Entry> entries = new List<Entry>();
        readonly Dictionary<string, Entry> byKey = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool cached;
        static CreatureAvatarCatalog loaded;
        static bool attemptedLoad;
        public void Invalidate() { cached = false; byKey.Clear(); ambiguous.Clear(); }
        void OnEnable() => Invalidate();
        void OnValidate() => Invalidate();
        public bool TryResolve(string definitionId, string formId, out Sprite avatar) {
            avatar = null;
            if (string.IsNullOrWhiteSpace(definitionId)) return false;
            if (!cached) {
                cached = true;
                foreach (var entry in entries) {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.definitionId) || string.IsNullOrWhiteSpace(entry.formId)) continue;
                    Add(entry.definitionId, entry);
                    if (entry.aliases != null) foreach (var alias in entry.aliases) Add(alias, entry);
                }
            }
            string key = Key(definitionId, formId);
            if (ambiguous.Contains(key) || !byKey.TryGetValue(key, out var found)) return false;
            avatar = found.avatar;
            return true; // A configured null avatar must show the UI's missing-art state, not a different form's image.
        }
        void Add(string id, Entry entry) {
            if (string.IsNullOrWhiteSpace(id)) return;
            string key = Key(id, entry.formId);
            if (byKey.TryGetValue(key, out var existing) && existing != entry) { ambiguous.Add(key); return; }
            byKey[key] = entry;
        }
        static string Key(string id, string form) => id.Trim() + "\n" + CanonicalForm(form);
        static string CanonicalForm(string form) {
            string key = (form ?? string.Empty).Trim();
            if (key.Length == 0 || key.Equals("base", StringComparison.OrdinalIgnoreCase) || key.Equals("dragon-dusk-base", StringComparison.OrdinalIgnoreCase)) return "01";
            if (key.Equals("dragon-dusk-winged", StringComparison.OrdinalIgnoreCase)) return "02";
            if (key.Equals("dragon-dusk-elder", StringComparison.OrdinalIgnoreCase)) return "03";
            return key;
        }
        public static Sprite Resolve(string definitionId, string formId, Sprite legacyFallback) {
            if (!attemptedLoad) { attemptedLoad = true; loaded = Resources.Load<CreatureAvatarCatalog>("Creatures/CreatureAvatarCatalog"); }
            return loaded != null && loaded.TryResolve(definitionId, formId, out var sprite) ? sprite : legacyFallback;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { loaded = null; attemptedLoad = false; }
    }
}
