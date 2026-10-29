using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using Capstone.Game.UISystem;
using UnityEngine;

namespace Capstone.Game.CodexSystem {
    /// <summary>Authored catalog + actual owned pets. No fixture injection, new save or species-favorite persistence.</summary>
    [DisallowMultipleComponent]
    public sealed class PetCodexRuntimeProvider : MonoBehaviour, IPetCodexProvider {
        [SerializeField] PetCodexCatalog catalog;
        PetCommandInput party;
        PetBoxRuntimeProvider box;
        PlayerSaveController saves;
        readonly List<PetCodexEntryViewData> entries = new List<PetCodexEntryViewData>();
        readonly List<PetCollectionMetadata> owned = new List<PetCollectionMetadata>();
        bool refreshing;
        public event Action Changed;
        public IReadOnlyList<PetCodexEntryViewData> Entries => entries;
        public bool CanSetFavorite => false;
        public bool TrySetFavorite(string id, bool favorite) => false;

        void OnEnable() => RefreshSnapshot();
        void OnDisable() => Unsubscribe();
        void OnLoaded(PlayerSaveData _) => RefreshSnapshot();
        void Unsubscribe() {
            if (box != null) box.Changed -= RefreshSnapshot;
            if (saves != null) saves.LoadCompleted -= OnLoaded;
            foreach (var pet in owned) if (pet != null) pet.Changed -= RefreshSnapshot;
        }
        void AddOwned(PetController pet) {
            var metadata = pet != null ? pet.GetComponentInChildren<PetCollectionMetadata>(true) : null;
            if (metadata == null || metadata.IsReleased || owned.Contains(metadata)) return;
            owned.Add(metadata);
            if (isActiveAndEnabled) metadata.Changed += RefreshSnapshot;
        }
        public void RefreshSnapshot() {
            if (refreshing) return;
            refreshing = true;
            try {
                Unsubscribe(); owned.Clear(); entries.Clear();
                if (catalog == null) catalog = PetCodexCatalog.Default;
                if (party == null) party = FindFirstObjectByType<PetCommandInput>();
                if (box == null) box = FindFirstObjectByType<PetBoxRuntimeProvider>();
                if (saves == null) saves = FindFirstObjectByType<PlayerSaveController>();
                if (isActiveAndEnabled) {
                    if (box != null) box.Changed += RefreshSnapshot;
                    if (saves != null) saves.LoadCompleted += OnLoaded;
                }
                if (party?.petSlots != null) foreach (var pet in party.petSlots) AddOwned(pet);
                if (box != null) foreach (var pet in box.StoredPets) AddOwned(pet);
                if (catalog?.species != null) foreach (var definition in catalog.species) {
                    var source = definition?.Metadata;
                    if (source == null || string.IsNullOrWhiteSpace(definition.id)) continue;
                    bool captured = owned.Exists(definition.Matches);
                    entries.Add(new PetCodexEntryViewData {
                        Id = definition.id, Number = definition.number, Name = source.Species,
                        PrimaryType = MapType(source.Element), SecondaryType = MapType(source.SecondaryElement),
                        Rarity = source.Rarity, Region = definition.region,
                        Discovery = captured ? CodexDiscovery.Captured : CodexDiscovery.Unknown,
                        Description = source.CodexDescription, Icon = definition.Icon, Portrait = definition.portrait,
                        Height = definition.height, Weight = definition.weight, Habitat = definition.habitat,
                        Trait = definition.trait, FavoriteFood = definition.favoriteFood,
                        Evolution = new List<PetCodexEvolutionViewData>()
                    });
                }
                if (catalog?.species != null) foreach (var definition in catalog.species) {
                    var entry = entries.Find(e => e.Id == definition?.id);
                    var source = definition?.Metadata;
                    if (entry == null || source == null) continue;
                    var nodes = (List<PetCodexEvolutionViewData>)entry.Evolution;
                    if (source.EvolutionRules == null) { entry.Evolution = null; continue; }
                    foreach (var rule in source.EvolutionRules) {
                        if (rule == null || rule.SourceStage != source.EvolutionStage) continue;
                        var target = catalog.FindForm(source.DefinitionId, rule.TargetFormId);
                        if (target == null) continue;
                        nodes.Add(new PetCodexEvolutionViewData {
                            Name = target.Metadata.Species, Icon = target.Icon,
                            Known = entries.Exists(e => e.Id == target.id && e.Known), Requirement = Requirement(rule)
                        });
                    }
                }
                Changed?.Invoke();
            } finally { refreshing = false; }
        }
        string Requirement(PetEvolutionRule rule) {
            string itemName = "vật phẩm tiến hóa";
            if (catalog.evolutionItems != null) foreach (var item in catalog.evolutionItems)
                if (item != null && item.StableId == rule.RequiredItemId) { itemName = VietnameseUI.ItemName(item.Name); break; }
            string level = "Cấp " + rule.RequiredLevel;
            string itemText = itemName + " ×" + rule.RequiredItemQuantity;
            return rule.Trigger == PetEvolutionTrigger.Level ? level
                : rule.Trigger == PetEvolutionTrigger.Item ? itemText : level + " hoặc " + itemText;
        }
        static CodexType MapType(PetElement value) => Enum.TryParse(value.ToString(), out CodexType result) ? result : CodexType.Unknown;
    }
}
