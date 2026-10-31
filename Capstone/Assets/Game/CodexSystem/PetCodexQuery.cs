using System;
using System.Collections.Generic;
using Capstone.Game.UISystem;

namespace Capstone.Game.CodexSystem {
    public sealed class PetCodexQuery {
        public const int PageSize = 25;
        public string Search = string.Empty;
        public CodexType? Type;
        public string Region;
        public Capstone.Game.HudSystem.PetRarity? Rarity;
        public CodexDiscovery? Discovery;
        public CodexSort Sort;
        readonly Dictionary<PetCodexEntryViewData, string> nameKeys = new Dictionary<PetCodexEntryViewData, string>();
        readonly Comparison<PetCodexEntryViewData> comparison;

        public PetCodexQuery() { comparison = Compare; }

        public void Apply(IReadOnlyList<PetCodexEntryViewData> entries, List<PetCodexEntryViewData> output) {
            output.Clear();
            nameKeys.Clear();
            string needle = VietnameseUI.SearchKey(Search);
            if (entries == null) return;
            for (int i = 0; i < entries.Count; i++) {
                var entry = entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id)) continue;
                if (Type.HasValue && (!entry.Known || (entry.PrimaryType != Type && entry.SecondaryType != Type))) continue;
                if (Region != null && (!entry.Known || entry.Region != Region)) continue;
                if (Rarity.HasValue && (!entry.Known || entry.Rarity != Rarity)) continue;
                if (Discovery.HasValue && entry.Discovery != Discovery) continue;
                // Normalize each candidate once per query, rather than on every
                // sort comparison. Rebuilding avoids stale keys after provider edits.
                string nameKey = entry.Known && (needle.Length > 0 || Sort == CodexSort.Name)
                    ? VietnameseUI.SearchKey(entry.Name) : string.Empty;
                // Do not leak undiscovered names, types or regions through filters.
                if (needle.Length > 0 && !entry.Number.ToString("D2").Contains(needle)
                    && !entry.Number.ToString("D3").Contains(needle)
                    && (!entry.Known || !nameKey.Contains(needle))) continue;
                if (Sort == CodexSort.Name) nameKeys[entry] = nameKey;
                output.Add(entry);
            }
            output.Sort(comparison);
        }

        int Compare(PetCodexEntryViewData a, PetCodexEntryViewData b) {
            int order = 0;
            switch (Sort) {
                case CodexSort.Name: order = string.Compare(nameKeys[a], nameKeys[b], StringComparison.Ordinal); break;
                case CodexSort.Type: order = (a.Known ? a.PrimaryType : CodexType.Unknown).CompareTo(b.Known ? b.PrimaryType : CodexType.Unknown); break;
                case CodexSort.Region: order = string.Compare(a.Known ? a.Region : null, b.Known ? b.Region : null, StringComparison.Ordinal); break;
                case CodexSort.Rarity: order = (b.Known ? b.Rarity : 0).CompareTo(a.Known ? a.Rarity : 0); break;
            }
            if (order == 0) order = a.Number.CompareTo(b.Number);
            return order != 0 ? order : string.Compare(a.Id, b.Id, StringComparison.Ordinal);
        }

        public static string TypeName(CodexType type) {
            if (type == CodexType.Mythic) return "Thần thoại";
            return Enum.TryParse(type.ToString(), out Capstone.Game.HudSystem.PetElement element)
                ? VietnameseUI.Element(element) : VietnameseUI.Unknown;
        }
    }
}
