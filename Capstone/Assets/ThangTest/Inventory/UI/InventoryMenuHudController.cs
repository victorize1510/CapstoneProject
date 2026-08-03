using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class InventoryMenuHudController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour playerStatsProviderBehaviour = null;
        [SerializeField] MonoBehaviour currencyProviderBehaviour = null;
        [SerializeField] bool autoInstallPlaceholderProvider = true;

        IPlayerStatsHudProvider playerStatsProvider;
        ICurrencyHudProvider currencyProvider;

        Label levelLabel;
        Label hpValueLabel;
        Label expValueLabel;
        Label goldValueLabel;
        Label gemValueLabel;
        ProgressBar hpBar;
        ProgressBar expBar;
        Button partyButton;
        Button mapButton;
        Button settingsButton;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            SubscribeProviders();
            Refresh();
            ConfigurePlaceholderTabs();
        }

        void OnDisable() {
            UnsubscribeProviders();
        }

        public void Bind(UIDocument newDocument) {
            UnsubscribeProviders();
            document = newDocument != null ? newDocument : document;
            ResolveReferences();
            CacheElements();
            SubscribeProviders();
            Refresh();
            ConfigurePlaceholderTabs();
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            ResolveProviders();
        }

        void ResolveProviders() {
            if (playerStatsProviderBehaviour == null && currencyProviderBehaviour == null && autoInstallPlaceholderProvider) {
                var placeholder = GetComponent<InventoryMenuHudDataProvider>();
                if (placeholder == null) placeholder = gameObject.AddComponent<InventoryMenuHudDataProvider>();
                playerStatsProviderBehaviour = placeholder;
                currencyProviderBehaviour = placeholder;
            }

            playerStatsProvider = playerStatsProviderBehaviour as IPlayerStatsHudProvider;
            currencyProvider = currencyProviderBehaviour as ICurrencyHudProvider;
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;

            var root = document.rootVisualElement;
            levelLabel = root.Q<Label>("hud-level-label");
            hpValueLabel = root.Q<Label>("hud-hp-value");
            expValueLabel = root.Q<Label>("hud-exp-value");
            goldValueLabel = root.Q<Label>("hud-gold-value");
            gemValueLabel = root.Q<Label>("hud-gem-value");
            hpBar = root.Q<ProgressBar>("hud-hp-bar");
            expBar = root.Q<ProgressBar>("hud-exp-bar");
            partyButton = root.Q<Button>("nav-party-button");
            mapButton = root.Q<Button>("nav-map-button");
            settingsButton = root.Q<Button>("nav-settings-button");
        }

        void SubscribeProviders() {
            if (playerStatsProvider != null) playerStatsProvider.HudDataChanged += Refresh;
            if (currencyProvider != null && !ReferenceEquals(currencyProvider, playerStatsProvider)) currencyProvider.HudDataChanged += Refresh;
        }

        void UnsubscribeProviders() {
            if (playerStatsProvider != null) playerStatsProvider.HudDataChanged -= Refresh;
            if (currencyProvider != null && !ReferenceEquals(currencyProvider, playerStatsProvider)) currencyProvider.HudDataChanged -= Refresh;
        }

        void Refresh() {
            RefreshPlayerStats();
            RefreshCurrency();
        }

        void RefreshPlayerStats() {
            int level = playerStatsProvider != null ? playerStatsProvider.Level : 1;
            int currentHp = playerStatsProvider != null ? playerStatsProvider.CurrentHp : 0;
            int maxHp = playerStatsProvider != null ? playerStatsProvider.MaxHp : 1;
            int currentExp = playerStatsProvider != null ? playerStatsProvider.CurrentExp : 0;
            int requiredExp = playerStatsProvider != null ? playerStatsProvider.RequiredExp : 1;

            if (levelLabel != null) levelLabel.text = "Lv. " + level;
            if (hpValueLabel != null) hpValueLabel.text = currentHp + " / " + maxHp;
            if (expValueLabel != null) expValueLabel.text = currentExp + " / " + requiredExp;
            SetProgress(hpBar, currentHp, maxHp);
            SetProgress(expBar, currentExp, requiredExp);
        }

        void RefreshCurrency() {
            int gold = currencyProvider != null ? currencyProvider.Gold : 0;
            int gems = currencyProvider != null ? currencyProvider.Gems : 0;

            if (goldValueLabel != null) goldValueLabel.text = gold.ToString("N0");
            if (gemValueLabel != null) gemValueLabel.text = gems.ToString("N0");
        }

        void ConfigurePlaceholderTabs() {
            SetDisabledPlaceholder(partyButton, "Party system is not connected yet.");
            SetDisabledPlaceholder(mapButton, "Map system is not connected yet.");
            SetDisabledPlaceholder(settingsButton, "Settings menu is not connected yet.");
        }

        static void SetDisabledPlaceholder(Button button, string tooltip) {
            if (button == null) return;
            button.tooltip = tooltip;
            button.SetEnabled(false);
        }

        static void SetProgress(ProgressBar bar, int current, int max) {
            if (bar == null) return;

            max = Mathf.Max(1, max);
            current = Mathf.Clamp(current, 0, max);
            bar.lowValue = 0f;
            bar.highValue = max;
            bar.value = current;
            bar.title = string.Empty;
        }
    }
}
