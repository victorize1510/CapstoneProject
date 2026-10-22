using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.CodexSystem {
    public enum CodexDiscovery { Unknown, Seen, Recorded, Captured }
    // Codex includes Mythic without changing gameplay's PetElement enum.
    public enum CodexType { Unknown, Fire, Water, Nature, Electric, Ice, Earth, Wind, Dark, Light, Mythic, Dragon }
    public enum CodexSort { Number, Type, Region, Rarity, Name }

    [Serializable]
    public sealed class PetCodexEvolutionViewData {
        public string Name;
        public string Requirement;
        public Sprite Icon;
        public bool Known;
    }

    [Serializable]
    public sealed class PetCodexEntryViewData {
        public string Id;
        public int Number;
        public string Name;
        public CodexType PrimaryType;
        public CodexType SecondaryType;
        public PetRarity Rarity;
        public string Region;
        public CodexDiscovery Discovery;
        public bool Favorite;
        public string Description;
        public string Height;
        public string Weight;
        public string Habitat;
        public string Trait;
        public string FavoriteFood;
        public Sprite Icon;
        public Sprite Portrait;
        public IReadOnlyList<PetCodexEvolutionViewData> Evolution;
        public bool Known => Discovery != CodexDiscovery.Unknown;
    }

    /// <summary>Read-only presentation snapshot. Persistence belongs to a future adapter.</summary>
    public interface IPetCodexProvider {
        event Action Changed;
        IReadOnlyList<PetCodexEntryViewData> Entries { get; }
        bool CanSetFavorite { get; }
        bool TrySetFavorite(string id, bool favorite);
    }
}
