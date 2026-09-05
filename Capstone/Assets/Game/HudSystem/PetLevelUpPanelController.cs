using System;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetLevelUpPanelController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] MonsterInventoryAdapter inventory = null;
        [SerializeField] PetLevelUpService levelUpService = null;
        [SerializeField] PetEvolutionService evolutionService = null;
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
        Label currentLevel;
        Label nextLevel;
        Label maxLabel;
        VisualElement expFill;
        Label expValue;
        Label expNeeded;
        VisualElement healthFill;
        Label healthValue;
        VisualElement itemIcon;
        Label itemIconFallback;
        Label itemName;
        Label itemCount;
        Label feedback;
        VisualElement newSkillSection;
        VisualElement newSkillIcon;
        Label newSkillIconFallback;
        Label newSkillName;
        Label newSkillDescription;
        Label newSkillUnlock;
        VisualElement evolutionCurrentIcon;
        Label evolutionCurrentFallback;
        VisualElement evolutionNextIcon;
        Label evolutionNextFallback;
        Label evolutionState;
        Label evolutionLevel;
        Label evolutionFriendship;
        Button evolutionButton;

        IPetHudProvider subscribedProvider;
        PetController targetPet;
        bool controlsRegistered;
        string transientFeedback = string.Empty;

        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        PetCommandHudProvider CommandProvider => petHudProvider as PetCommandHudProvider;
        public bool IsOpen => overlay != null && overlay.style.display.value != DisplayStyle.None;

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
            if (overlay == null || levelUpService == null) return;

            PetLevelUpPreview preview = levelUpService.CreatePreview(targetPet);
            RefreshSummary(preview);
            RefreshStats(preview);
            RefreshNewSkill(preview);
            RefreshEvolution(preview);
            RefreshItem(preview);

            if (confirmButton != null) {
                confirmButton.text = preview.IsMaxLevel ? "MAX LEVEL" : "LEVEL UP";
                confirmButton.SetEnabled(preview.CanLevelUp);
            }
            if (feedback != null) {
                feedback.text = !string.IsNullOrWhiteSpace(transientFeedback)
                    ? transientFeedback
                    : preview.DisabledReason;
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
            if (levelUpService == null) levelUpService = GetComponent<PetLevelUpService>();
            if (levelUpService == null && Application.isPlaying) levelUpService = gameObject.AddComponent<PetLevelUpService>();
            levelUpService?.Bind(inventory);
            if (evolutionService == null) evolutionService = GetComponent<PetEvolutionService>();
            evolutionService?.Bind(inventory);
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
            overlay = root.Q<VisualElement>("level-up-modal-overlay");
            dialog = root.Q<VisualElement>("level-up-modal-dialog");
            closeButton = root.Q<Button>("level-up-close-button");
            confirmButton = root.Q<Button>("level-up-confirm-button");
            portrait = root.Q<VisualElement>("level-up-pet-portrait");
            portraitFallback = root.Q<Label>("level-up-pet-portrait-fallback");
            petName = root.Q<Label>("level-up-pet-name");
            species = root.Q<Label>("level-up-species");
            element = root.Q<Label>("level-up-element");
            rarity = root.Q<Label>("level-up-rarity");
            currentLevel = root.Q<Label>("level-up-current-level");
            nextLevel = root.Q<Label>("level-up-next-level");
            maxLabel = root.Q<Label>("level-up-max-label");
            expFill = root.Q<VisualElement>("level-up-exp-fill");
            expValue = root.Q<Label>("level-up-exp-value");
            expNeeded = root.Q<Label>("level-up-exp-needed");
            healthFill = root.Q<VisualElement>("level-up-health-fill");
            healthValue = root.Q<Label>("level-up-health-value");
            itemIcon = root.Q<VisualElement>("level-up-item-icon");
            itemIconFallback = root.Q<Label>("level-up-item-icon-fallback");
            itemName = root.Q<Label>("level-up-item-name");
            itemCount = root.Q<Label>("level-up-item-count");
            feedback = root.Q<Label>("level-up-feedback");
            newSkillSection = root.Q<VisualElement>("level-up-new-skill-section");
            newSkillIcon = root.Q<VisualElement>("level-up-skill-icon");
            newSkillIconFallback = root.Q<Label>("level-up-skill-icon-fallback");
            newSkillName = root.Q<Label>("level-up-skill-name");
            newSkillDescription = root.Q<Label>("level-up-skill-description");
            newSkillUnlock = root.Q<Label>("level-up-skill-unlock");
            evolutionCurrentIcon = root.Q<VisualElement>("level-up-evolution-current-icon");
            evolutionCurrentFallback = root.Q<Label>("level-up-evolution-current-fallback");
            evolutionNextIcon = root.Q<VisualElement>("level-up-evolution-next-icon");
            evolutionNextFallback = root.Q<Label>("level-up-evolution-next-fallback");
            evolutionState = root.Q<Label>("level-up-evolution-state");
            evolutionLevel = root.Q<Label>("level-up-evolution-level");
            evolutionFriendship = root.Q<Label>("level-up-evolution-friendship");
            evolutionButton = root.Q<Button>("level-up-evolution-button");
        }

        void RegisterControls() {
            if (controlsRegistered || overlay == null) return;
            if (closeButton != null) closeButton.clicked += Close;
            if (confirmButton != null) confirmButton.clicked += ConfirmLevelUp;
            if (evolutionButton != null) evolutionButton.clicked += OpenEvolutionPanel;
            controlsRegistered = true;
        }

        void UnregisterControls() {
            if (!controlsRegistered) return;
            if (closeButton != null) closeButton.clicked -= Close;
            if (confirmButton != null) confirmButton.clicked -= ConfirmLevelUp;
            if (evolutionButton != null) evolutionButton.clicked -= OpenEvolutionPanel;
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

        void ConfirmLevelUp() {
            if (levelUpService == null) return;
            bool success = levelUpService.TryLevelUp(targetPet, out string message);
            if (success) CommandProvider?.NotifyHudDataChanged();
            transientFeedback = message;
            Refresh();
            if (!success && feedback != null) feedback.text = message;
        }

        void OpenEvolutionPanel() {
            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            controller?.OpenPetEvolutionPanel(targetPet);
        }

        void RefreshSummary(PetLevelUpPreview preview) {
            string baseName = preview.RuntimeStats != null ? preview.RuntimeStats.DisplayName : "Chọn một pet";
            string displayName = preview.Metadata != null ? preview.Metadata.ResolveDisplayName(baseName) : baseName;
            if (petName != null) petName.text = displayName;
            if (species != null) species.text = preview.Metadata != null ? DisplayOrDash(preview.Metadata.Species) : "-";
            if (element != null) element.text = preview.Metadata != null ? FormatElement(preview.Metadata.Element) : "-";
            if (rarity != null) rarity.text = preview.Metadata != null ? FormatRarity(preview.Metadata.Rarity) : "-";
            SetPortrait(portrait, portraitFallback, preview.RuntimeStats?.Icon, displayName);

            if (currentLevel != null) currentLevel.text = preview.IsValid ? $"Lv. {preview.CurrentLevel}" : "Lv. -";
            if (nextLevel != null) {
                nextLevel.text = preview.IsValid ? $"Lv. {preview.NextLevel}" : "Lv. -";
                nextLevel.style.display = preview.IsMaxLevel ? DisplayStyle.None : DisplayStyle.Flex;
            }
            if (maxLabel != null) maxLabel.style.display = preview.IsMaxLevel ? DisplayStyle.Flex : DisplayStyle.None;
            SetFill(expFill, Percent(preview.CurrentExperience, preview.RequiredExperience));
            if (expValue != null) expValue.text = preview.IsValid ? $"{preview.CurrentExperience:N0} / {preview.RequiredExperience:N0}" : "- / -";
            if (expNeeded != null) expNeeded.text = preview.IsMaxLevel ? "Đã đạt cấp tối đa" : $"Cần thêm {preview.ExperienceNeeded:N0} EXP";
            float health = preview.RuntimeStats?.Health ?? 0f;
            float maxHealth = preview.RuntimeStats?.MaxHealth ?? 0f;
            SetFill(healthFill, Percent(health, maxHealth));
            if (healthValue != null) healthValue.text = maxHealth > 0f ? $"{Mathf.RoundToInt(health):N0} / {Mathf.RoundToInt(maxHealth):N0}" : "- / -";
        }

        void RefreshStats(PetLevelUpPreview preview) {
            SetStat("hp", preview.CurrentHealth, preview.NextHealth);
            SetStat("atk", preview.CurrentAttack, preview.NextAttack);
            SetStat("def", preview.CurrentDefense, preview.NextDefense);
            SetStat("spd", preview.CurrentSpeed, preview.NextSpeed);
        }

        void SetStat(string id, int current, int next) {
            SetText($"level-up-stat-{id}-current", current.ToString("N0"));
            SetText($"level-up-stat-{id}-next", next.ToString("N0"));
            SetText($"level-up-stat-{id}-delta", $"+{Mathf.Max(0, next - current):N0}");
        }

        void RefreshNewSkill(PetLevelUpPreview preview) {
            if (newSkillSection != null) newSkillSection.style.display = DisplayStyle.Flex;

            if (!preview.HasNewSkill) {
                SetPortrait(newSkillIcon, newSkillIconFallback, null, "-");
                if (newSkillName != null) newSkillName.text = "Không có kỹ năng mới";
                if (newSkillDescription != null) newSkillDescription.text = preview.IsMaxLevel
                    ? "Pet đã đạt cấp tối đa."
                    : $"Không mở kỹ năng ở Lv. {preview.NextLevel}.";
                if (newSkillUnlock != null) newSkillUnlock.text = string.Empty;
                return;
            }

            SkillHudData skill = preview.NewSkill;
            string skillName = DisplayOrDash(skill.displayName);
            SetPortrait(newSkillIcon, newSkillIconFallback, skill.icon, skillName);
            if (newSkillName != null) newSkillName.text = skillName;
            if (newSkillDescription != null) {
                newSkillDescription.text = string.IsNullOrWhiteSpace(skill.description)
                    ? FormatElement(skill.element)
                    : skill.description;
            }
            if (newSkillUnlock != null) newSkillUnlock.text = $"Sẽ học ở Lv. {preview.NextLevel}";
        }

        void RefreshEvolution(PetLevelUpPreview preview) {
            PetCollectionMetadata metadata = preview.Metadata;
            Sprite currentIcon = preview.RuntimeStats?.Icon;
            SetPortrait(evolutionCurrentIcon, evolutionCurrentFallback, currentIcon,
                preview.RuntimeStats != null ? preview.RuntimeStats.DisplayName : "?");

            if (evolutionService == null) evolutionService = GetComponent<PetEvolutionService>();
            evolutionService?.Bind(inventory);
            PetEvolutionPreview evolution = evolutionService?.CreatePreview(preview.Pet);

            if (metadata == null || evolution == null || !evolution.IsConfigured) {
                SetPortrait(evolutionNextIcon, evolutionNextFallback, null, "-");
                if (evolutionState != null) evolutionState.text = "Đã đạt dạng tiến hóa cuối";
                if (evolutionLevel != null) evolutionLevel.text = string.Empty;
                if (evolutionFriendship != null) evolutionFriendship.text = string.Empty;
                if (evolutionButton != null) evolutionButton.style.display = DisplayStyle.None;
                return;
            }

            string nextName = DisplayOrDash(evolution.TargetName);
            SetPortrait(evolutionNextIcon, evolutionNextFallback, evolution.TargetIcon, nextName);
            if (evolutionState != null) evolutionState.text = evolution.CanEvolve
                ? $"Đã sẵn sàng tiến hóa thành {nextName}"
                : $"Dạng tiếp theo: {nextName}";
            if (evolutionLevel != null) evolutionLevel.text = evolution.Rule.Trigger == PetEvolutionTrigger.Level
                ? $"Cấp yêu cầu: Lv. {evolution.RequiredLevel} • Hiện tại Lv. {evolution.CurrentLevel}"
                : "Tiến hóa bằng vật phẩm, không khóa theo level";
            if (evolutionFriendship != null) evolutionFriendship.text = evolution.Rule.Trigger == PetEvolutionTrigger.Item
                ? $"{evolution.RequiredItemId}: {evolution.OwnedItemQuantity} / {evolution.RequiredItemQuantity}"
                : evolution.CanEvolve
                    ? "Đã đạt điều kiện. Có thể tiến hóa bất cứ lúc nào."
                    : evolution.DisabledReason;
            if (evolutionButton != null) {
                evolutionButton.style.display = DisplayStyle.Flex;
                evolutionButton.SetEnabled(evolution.CanEvolve);
            }
        }

        void RefreshItem(PetLevelUpPreview preview) {
            if (itemName != null) itemName.text = DisplayOrDash(preview.ItemName);
            if (itemCount != null) {
                itemCount.text = $"{preview.OwnedItemQuantity:N0} / {preview.RequiredItemQuantity:N0}";
                itemCount.EnableInClassList("is-missing", preview.OwnedItemQuantity < preview.RequiredItemQuantity);
            }
            SetPortrait(itemIcon, itemIconFallback, preview.ItemIcon, "✦");
        }

        void SetText(string name, string value) {
            Label label = document?.rootVisualElement?.Q<Label>(name);
            if (label != null) label.text = value;
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
                    fallback.text = string.IsNullOrWhiteSpace(fallbackText) ? "?" : fallbackText.Substring(0, 1).ToUpperInvariant();
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
            return value == PetRarity.Unknown ? "-" : value.ToString();
        }
    }
}
