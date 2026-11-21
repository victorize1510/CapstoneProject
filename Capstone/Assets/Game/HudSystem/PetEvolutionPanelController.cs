using System;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    // Compatibility entry point for existing Pet/Party buttons. The HUD owns the
    // single text-only confirmation; no second portrait screen is constructed.
    [DisallowMultipleComponent, RequireComponent(typeof(UIDocument))]
    public sealed class PetEvolutionPanelController : MonoBehaviour {
        [SerializeField] MonoBehaviour petHudProvider;
        MonsterInventoryAdapter inventory;
        EvolutionHudPromptController prompt;
        PetController targetPet;
        PetCommandHudProvider CommandProvider => petHudProvider as PetCommandHudProvider;
        public bool IsOpen => prompt != null && prompt.IsShowing(targetPet);

        public void Bind(UIDocument document, MonsterInventoryAdapter targetInventory, VisualTreeAsset unusedTree) {
            inventory = targetInventory != null ? targetInventory : inventory;
            ResolveReferences();
        }
        void ResolveReferences() {
            if (petHudProvider == null) petHudProvider = FindFirstObjectByType<PetCommandHudProvider>();
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
            if (prompt == null) prompt = FindFirstObjectByType<EvolutionHudPromptController>();
        }
        public void Open(PetController pet = null, bool automaticPrompt = false) {
            ResolveReferences();
            var selected = pet != null ? pet : CommandProvider?.GetSelectedPetController();
            if (selected == null) return;
            if (prompt == null) {
                Debug.LogWarning("Không tìm thấy popup tiến hóa trên Gameplay HUD.", this);
                return;
            }
            // Close the menu first so its gameplay/pause owner cannot hide the HUD.
            GetComponent<MonsterInventoryController>()?.Close();
            targetPet = selected;
            if (automaticPrompt) prompt.Enqueue(targetPet);
            else prompt.RequestManual(targetPet);
        }
        public void Close() {
            if (targetPet != null) prompt?.CloseFor(targetPet);
            targetPet = null;
        }
        public void DismissPanel() { if (prompt != null) prompt.TryDismissForBack(); targetPet = null; }
        void OnDisable() => Close();
        void ConfirmEvolution() => prompt?.ConfirmCurrent();

        public bool TryOpenFromInventory(InventoryItemSnapshot item) {
            if (!IsEvolutionItem(item)) return false;
            var controller = GetComponent<MonsterInventoryController>() ?? FindFirstObjectByType<MonsterInventoryController>();
            controller?.OpenPetPartyPanel();
            var party = GetComponent<PetPartyPanelController>() ?? FindFirstObjectByType<PetPartyPanelController>();
            party?.BeginEvolutionItemSelection(item.ItemId, this);
            return true;
        }
        public bool IsEvolutionItem(InventoryItemSnapshot item) {
            if (item == null) return false;
            ResolveReferences();
            for (int i = 0; i < 6; i++) {
                var pet = CommandProvider?.GetPetControllerAt(i);
                var metadata = pet != null ? pet.GetComponentInChildren<PetCollectionMetadata>(true) : null;
                if (metadata == null || !metadata.TryGetNextEvolutionRule(out var rule)
                    || rule.Trigger == PetEvolutionTrigger.Level) continue;
                if (string.Equals(item.ItemId, rule.RequiredItemId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, rule.RequiredItemId, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
        public bool CanUseForSelectedPet(InventoryItemSnapshot item) {
            if (item == null) return false;
            ResolveReferences();
            var service = GetComponent<PetEvolutionService>();
            var preview = service?.CreatePreview(CommandProvider?.GetSelectedPetController(), PetEvolutionMethod.Item, item.ItemId);
            return preview?.Rule != null && preview.Rule.Trigger != PetEvolutionTrigger.Level
                && (string.Equals(item.ItemId, preview.RequiredItemId, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Name, preview.RequiredItemId, StringComparison.OrdinalIgnoreCase));
        }
    }
}
