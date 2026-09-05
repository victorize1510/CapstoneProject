using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetReleasePanelController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] MonsterInventoryAdapter inventory = null;
        [SerializeField] PetReleaseService releaseService = null;
        [SerializeField] bool autoFindReferences = true;

        VisualElement overlay;
        VisualElement dialog;
        Button closeButton;
        Button cancelButton;
        Button confirmButton;
        VisualElement portrait;
        Label portraitFallback;
        Label petName;
        Label species;
        Label element;
        Label rarity;
        Label level;
        Label stage;
        VisualElement refundList;
        Label refundEmpty;
        Label goldValue;
        Label favoriteWarning;
        Label dailyCount;
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
            if (overlay == null || releaseService == null) return;

            PetReleasePreview preview = releaseService.CreatePreview(targetPet);
            string displayName = string.IsNullOrWhiteSpace(preview.DisplayName) ? "Chọn một pet" : preview.DisplayName;
            SetPortrait(portrait, portraitFallback, preview.Icon, displayName);
            if (petName != null) petName.text = displayName;
            if (species != null) species.text = DisplayOrDash(preview.Species);
            if (element != null) element.text = FormatElement(preview.Element);
            if (rarity != null) rarity.text = FormatRarity(preview.Rarity);
            if (level != null) level.text = preview.IsValid ? $"Lv. {preview.Level}" : "-";
            if (stage != null) stage.text = preview.IsValid ? preview.EvolutionStage.ToString() : "-";
            if (goldValue != null) goldValue.text = $"+{preview.GoldValue:N0}";

            RefreshRefunds(preview.Refunds);
            if (favoriteWarning != null) {
                favoriteWarning.style.display = preview.IsFavorite ? DisplayStyle.Flex : DisplayStyle.None;
            }
            if (dailyCount != null) {
                dailyCount.text = $"PET ĐÃ THẢ: {preview.ReleasedToday} / {preview.DailyReleaseLimit} HÔM NAY";
            }
            if (confirmButton != null) confirmButton.SetEnabled(preview.CanRelease);
            if (feedback != null) {
                feedback.text = !string.IsNullOrWhiteSpace(transientFeedback)
                    ? transientFeedback
                    : preview.DisabledReason;
            }
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            if (Provider == null && autoFindReferences) {
                PetCommandHudProvider provider = FindFirstObjectByType<PetCommandHudProvider>(FindObjectsInactive.Include);
                if (provider != null) petHudProvider = provider;
            }
            if (inventory == null) inventory = GetComponent<MonsterInventoryAdapter>();
            if (inventory == null) inventory = GetComponentInParent<MonsterInventoryAdapter>();
            if (inventory == null && autoFindReferences) {
                inventory = FindFirstObjectByType<MonsterInventoryAdapter>(FindObjectsInactive.Include);
            }
            if (releaseService == null) releaseService = GetComponent<PetReleaseService>();
            if (releaseService == null && Application.isPlaying) releaseService = gameObject.AddComponent<PetReleaseService>();
            releaseService?.Bind(inventory);
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
            overlay = root.Q<VisualElement>("release-modal-overlay");
            dialog = root.Q<VisualElement>("release-modal-dialog");
            closeButton = root.Q<Button>("release-close-button");
            cancelButton = root.Q<Button>("release-cancel-button");
            confirmButton = root.Q<Button>("release-confirm-button");
            portrait = root.Q<VisualElement>("release-pet-portrait");
            portraitFallback = root.Q<Label>("release-pet-portrait-fallback");
            petName = root.Q<Label>("release-pet-name");
            species = root.Q<Label>("release-pet-species");
            element = root.Q<Label>("release-pet-element");
            rarity = root.Q<Label>("release-pet-rarity");
            level = root.Q<Label>("release-pet-level");
            stage = root.Q<Label>("release-pet-stage");
            refundList = root.Q<VisualElement>("release-refund-list");
            refundEmpty = root.Q<Label>("release-refund-empty");
            goldValue = root.Q<Label>("release-gold-value");
            favoriteWarning = root.Q<Label>("release-favorite-warning");
            dailyCount = root.Q<Label>("release-daily-count");
            feedback = root.Q<Label>("release-feedback");
        }

        void RegisterControls() {
            if (controlsRegistered || overlay == null) return;
            if (closeButton != null) closeButton.clicked += Close;
            if (cancelButton != null) cancelButton.clicked += Close;
            if (confirmButton != null) confirmButton.clicked += ConfirmRelease;
            controlsRegistered = true;
        }

        void UnregisterControls() {
            if (!controlsRegistered) return;
            if (closeButton != null) closeButton.clicked -= Close;
            if (cancelButton != null) cancelButton.clicked -= Close;
            if (confirmButton != null) confirmButton.clicked -= ConfirmRelease;
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

        void HandleInventoryChanged(IReadOnlyList<InventoryItemSnapshot> _) {
            if (IsOpen) Refresh();
        }

        void ConfirmRelease() {
            if (releaseService == null) return;
            if (!releaseService.TryRelease(targetPet, out string message)) {
                transientFeedback = message;
                Refresh();
                return;
            }

            CommandProvider?.NotifyHudDataChanged();
            Close();
        }

        void RefreshRefunds(IReadOnlyList<PetReleaseRefundPreview> refunds) {
            if (refundList == null) return;
            refundList.Clear();

            bool hasRefund = refunds != null && refunds.Count > 0;
            refundList.style.display = hasRefund ? DisplayStyle.Flex : DisplayStyle.None;
            if (refundEmpty != null) refundEmpty.style.display = hasRefund ? DisplayStyle.None : DisplayStyle.Flex;
            if (!hasRefund) return;

            for (int i = 0; i < refunds.Count; i++) {
                PetReleaseRefundPreview refund = refunds[i];
                if (refund == null) continue;

                var card = new VisualElement();
                card.AddToClassList("release-refund-card");
                var icon = new VisualElement();
                icon.AddToClassList("release-refund-icon");
                var fallback = new Label("◆");
                fallback.AddToClassList("release-refund-icon-fallback");
                icon.Add(fallback);
                SetPortrait(icon, fallback, refund.Icon, "◆");

                var copy = new VisualElement();
                copy.AddToClassList("release-refund-copy");
                var name = new Label(DisplayOrDash(refund.DisplayName));
                name.AddToClassList("release-refund-name");
                var count = new Label($"x{refund.RefundQuantity:N0}");
                count.AddToClassList("release-refund-count");
                copy.Add(name);
                copy.Add(count);
                card.Add(icon);
                card.Add(copy);
                refundList.Add(card);
            }
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
