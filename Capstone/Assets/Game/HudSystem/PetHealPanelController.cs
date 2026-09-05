using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetHealPanelController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] MonsterInventoryAdapter inventory = null;
        [SerializeField] PetHealService healService = null;
        [SerializeField] bool autoFindReferences = true;

        VisualElement overlay;
        VisualElement dialog;
        Button closeButton;
        Button confirmButton;
        VisualElement portrait;
        Label portraitFallback;
        Label petName;
        Label species;
        Label element;
        Label rarity;
        Label status;
        Label currentHealth;
        Label resultHealth;
        Label healthArrow;
        VisualElement currentFill;
        VisualElement recoveryFill;
        Label healedAmount;
        VisualElement itemIcon;
        Label itemIconFallback;
        Label itemName;
        Label itemEffect;
        Label itemCount;
        Label feedback;

        IPetHudProvider subscribedProvider;
        PetController targetPet;
        bool controlsRegistered;
        string transientFeedback = string.Empty;

        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        PetCommandHudProvider CommandProvider => petHudProvider as PetCommandHudProvider;
        public bool IsOpen => overlay != null && overlay.resolvedStyle.display != DisplayStyle.None;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            RegisterControls();
            Subscribe();
            Close();
        }

        void OnDisable() {
            Unsubscribe();
            UnregisterControls();
        }

        public void Bind(UIDocument targetDocument, MonsterInventoryAdapter targetInventory) {
            document = targetDocument != null ? targetDocument : document;
            inventory = targetInventory != null ? targetInventory : inventory;
            ResolveReferences();
            CacheElements();
            RegisterControls();
            Subscribe();
            Close();
        }

        public void Open(PetController pet = null) {
            if (overlay == null) CacheElements();
            if (overlay == null) return;

            ResolveReferences();
            targetPet = pet != null ? pet : CommandProvider?.GetSelectedPetController();
            transientFeedback = string.Empty;
            overlay.style.display = DisplayStyle.Flex;
            overlay.BringToFront();
            Refresh();
            dialog?.Focus();
        }

        public void Close() {
            transientFeedback = string.Empty;
            targetPet = null;
            if (overlay != null) overlay.style.display = DisplayStyle.None;
        }

        public void Refresh() {
            if (overlay == null) CacheElements();
            if (overlay == null || healService == null) return;

            PetHealPreview preview = healService.CreatePreview(targetPet);
            RefreshPet(preview);
            RefreshHealth(preview);
            RefreshItem(preview);

            if (confirmButton != null) {
                confirmButton.text = preview.IsFullHealth ? "FULL HP" : "HEAL";
                confirmButton.SetEnabled(preview.CanHeal);
            }
            if (feedback != null) {
                feedback.text = !string.IsNullOrWhiteSpace(transientFeedback)
                    ? transientFeedback
                    : preview.DisabledReason;
                feedback.EnableInClassList("is-success", !string.IsNullOrWhiteSpace(transientFeedback));
            }
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            if (Provider == null && autoFindReferences) {
                PetCommandHudProvider provider = FindFirstObjectByType<PetCommandHudProvider>();
                if (provider != null) petHudProvider = provider;
            }
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null && autoFindReferences) inventory = FindFirstObjectByType<MonsterInventoryAdapter>();
            if (healService == null) healService = GetComponent<PetHealService>();
            if (healService == null && Application.isPlaying) healService = gameObject.AddComponent<PetHealService>();
            healService?.Bind(inventory);
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
            overlay = root.Q<VisualElement>("heal-modal-overlay");
            dialog = root.Q<VisualElement>("heal-modal-dialog");
            closeButton = root.Q<Button>("heal-close-button");
            confirmButton = root.Q<Button>("heal-confirm-button");
            portrait = root.Q<VisualElement>("heal-pet-portrait");
            portraitFallback = root.Q<Label>("heal-pet-portrait-fallback");
            petName = root.Q<Label>("heal-pet-name");
            species = root.Q<Label>("heal-pet-species");
            element = root.Q<Label>("heal-pet-element");
            rarity = root.Q<Label>("heal-pet-rarity");
            status = root.Q<Label>("heal-pet-status");
            currentHealth = root.Q<Label>("heal-current-health");
            resultHealth = root.Q<Label>("heal-result-health");
            healthArrow = root.Q<Label>("heal-health-arrow");
            currentFill = root.Q<VisualElement>("heal-current-fill");
            recoveryFill = root.Q<VisualElement>("heal-recovery-fill");
            healedAmount = root.Q<Label>("heal-restored-amount");
            itemIcon = root.Q<VisualElement>("heal-item-icon");
            itemIconFallback = root.Q<Label>("heal-item-icon-fallback");
            itemName = root.Q<Label>("heal-item-name");
            itemEffect = root.Q<Label>("heal-item-effect");
            itemCount = root.Q<Label>("heal-item-count");
            feedback = root.Q<Label>("heal-feedback");
        }

        void RegisterControls() {
            if (controlsRegistered || overlay == null) return;
            if (closeButton != null) closeButton.clicked += Close;
            if (confirmButton != null) confirmButton.clicked += ConfirmHeal;
            controlsRegistered = true;
        }

        void UnregisterControls() {
            if (!controlsRegistered) return;
            if (closeButton != null) closeButton.clicked -= Close;
            if (confirmButton != null) confirmButton.clicked -= ConfirmHeal;
            controlsRegistered = false;
        }

        void Subscribe() {
            IPetHudProvider provider = Provider;
            if (!ReferenceEquals(subscribedProvider, provider)) {
                UnsubscribeProvider();
                subscribedProvider = provider;
                if (subscribedProvider != null) subscribedProvider.HudDataChanged += HandleDataChanged;
            }
            if (inventory != null) {
                inventory.ItemsChanged -= HandleInventoryChanged;
                inventory.ItemsChanged += HandleInventoryChanged;
            }
        }

        void Unsubscribe() {
            UnsubscribeProvider();
            if (inventory != null) inventory.ItemsChanged -= HandleInventoryChanged;
        }

        void UnsubscribeProvider() {
            if (subscribedProvider != null) subscribedProvider.HudDataChanged -= HandleDataChanged;
            subscribedProvider = null;
        }

        void HandleDataChanged() {
            if (IsOpen) Refresh();
        }

        void HandleInventoryChanged(System.Collections.Generic.IReadOnlyList<InventoryItemSnapshot> _) {
            if (IsOpen) Refresh();
        }

        void ConfirmHeal() {
            if (healService == null) return;
            bool success = healService.TryHeal(targetPet, out string message);
            if (success) CommandProvider?.NotifyHudDataChanged();
            transientFeedback = message;
            Refresh();
        }

        void RefreshPet(PetHealPreview preview) {
            string baseName = preview.RuntimeStats != null ? preview.RuntimeStats.DisplayName : "Chọn một pet";
            string displayName = preview.Metadata != null ? preview.Metadata.ResolveDisplayName(baseName) : baseName;
            if (petName != null) petName.text = displayName;
            if (species != null) species.text = preview.Metadata != null ? DisplayOrDash(preview.Metadata.Species) : "-";
            if (element != null) element.text = preview.Metadata != null ? FormatElement(preview.Metadata.Element) : "-";
            if (rarity != null) rarity.text = preview.Metadata != null ? FormatRarity(preview.Metadata.Rarity) : "-";
            if (status != null) status.text = preview.IsValid ? "Bình thường" : "-";
            SetPortrait(portrait, portraitFallback, preview.RuntimeStats?.Icon, displayName);
        }

        void RefreshHealth(PetHealPreview preview) {
            int current = Mathf.RoundToInt(preview.CurrentHealth);
            int result = Mathf.RoundToInt(preview.ResultHealth);
            int maximum = Mathf.RoundToInt(preview.MaxHealth);
            if (currentHealth != null) currentHealth.text = preview.IsValid ? $"{current:N0} / {maximum:N0}" : "- / -";
            if (resultHealth != null) resultHealth.text = preview.IsValid ? $"{result:N0} / {maximum:N0}" : "- / -";
            if (healthArrow != null) healthArrow.style.display = preview.IsFullHealth ? DisplayStyle.None : DisplayStyle.Flex;
            if (resultHealth != null) resultHealth.style.display = preview.IsFullHealth ? DisplayStyle.None : DisplayStyle.Flex;

            float currentPercent = Percent(preview.CurrentHealth, preview.MaxHealth);
            float resultPercent = Percent(preview.ResultHealth, preview.MaxHealth);
            SetFill(currentFill, currentPercent);
            if (recoveryFill != null) {
                recoveryFill.style.left = Length.Percent(currentPercent * 100f);
                recoveryFill.style.width = Length.Percent(Mathf.Max(0f, resultPercent - currentPercent) * 100f);
                recoveryFill.style.display = preview.ActualHealAmount > 0.01f ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (healedAmount != null) {
                healedAmount.text = preview.IsFullHealth
                    ? "HP ĐÃ ĐẦY"
                    : $"+{Mathf.RoundToInt(preview.ActualHealAmount):N0} HP";
                healedAmount.EnableInClassList("is-full", preview.IsFullHealth);
            }
        }

        void RefreshItem(PetHealPreview preview) {
            if (itemName != null) itemName.text = DisplayOrDash(preview.ItemName);
            if (itemEffect != null) itemEffect.text = $"Hồi {preview.HealAmount:N0} HP";
            if (itemCount != null) {
                itemCount.text = $"{preview.OwnedQuantity:N0} / {preview.RequiredQuantity:N0}";
                itemCount.EnableInClassList("is-missing", preview.OwnedQuantity < preview.RequiredQuantity);
            }
            SetPortrait(itemIcon, itemIconFallback, preview.ItemIcon, "+");
        }

        static void SetFill(VisualElement fill, float percent) {
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01(percent) * 100f);
        }

        static float Percent(float current, float maximum) {
            return maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        }

        static void SetPortrait(VisualElement target, Label fallback, Sprite sprite, string fallbackText) {
            if (target == null) return;
            if (sprite != null) {
                target.style.backgroundImage = new StyleBackground(sprite);
                if (fallback != null) fallback.style.display = DisplayStyle.None;
            }
            else {
                target.style.backgroundImage = StyleKeyword.None;
                if (fallback != null) {
                    string value = string.IsNullOrWhiteSpace(fallbackText) ? "?" : fallbackText.Trim();
                    fallback.text = value.Substring(0, 1).ToUpperInvariant();
                    fallback.style.display = DisplayStyle.Flex;
                }
            }
        }

        static string DisplayOrDash(string value) {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        static string FormatElement(PetElement value) {
            return value == PetElement.Unknown ? "-" : value.ToString();
        }

        static string FormatRarity(PetRarity value) {
            switch (value) {
                case PetRarity.Common: return "★";
                case PetRarity.Uncommon: return "★★";
                case PetRarity.Rare: return "★★★";
                case PetRarity.Epic: return "★★★★";
                case PetRarity.Legendary: return "★★★★★";
                default: return "-";
            }
        }
    }
}
