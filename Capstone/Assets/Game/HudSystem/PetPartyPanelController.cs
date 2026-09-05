using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetPartyPanelController : MonoBehaviour {
        const int RenameMaxLength = 16;

        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] bool autoFindProvider = true;

        readonly List<Button> partyCards = new List<Button>(6);
        readonly List<VisualElement> skillCards = new List<VisualElement>(4);
        readonly Dictionary<Button, Action> buttonCallbacks = new Dictionary<Button, Action>();

        VisualElement root;
        VisualElement panel;
        VisualElement partyRow;
        VisualElement selectedPortrait;
        Label selectedPortraitFallback;
        Label selectedName;
        Label selectedSpecies;
        Label selectedLevel;
        Label selectedGender;
        Label selectedRarity;
        Label selectedElement;
        Label selectedHealthValue;
        Label selectedExperienceValue;
        VisualElement selectedHealthFill;
        VisualElement selectedExperienceFill;
        Label statHealth;
        Label statAttack;
        Label statDefense;
        Label statSpeed;
        VisualElement skillsRow;
        Label feedbackLabel;
        VisualElement tooltip;
        Label tooltipTitle;
        Label tooltipBody;
        Button backButton;
        Button renameButton;
        Button favoriteButton;
        Button evolutionButton;
        VisualElement renameOverlay;
        VisualElement renamePortrait;
        Label renamePortraitFallback;
        Label renameCurrentName;
        TextField renameInput;
        Label renamePlaceholder;
        Label renameCounter;
        Label renameError;
        Button renameSaveButton;
        Button renameCancelButton;
        Button renameCloseButton;
        VisualElement detailsOverlay;
        VisualElement detailsPortrait;
        Label detailsPortraitFallback;
        VisualElement detailsSkillsRow;
        VisualElement evolutionFlow;
        VisualElement evolutionMax;
        VisualElement evolutionRequirements;
        VisualElement evolutionCurrentIcon;
        Label evolutionCurrentFallback;
        VisualElement evolutionNextIcon;
        Label evolutionNextFallback;
        Button detailsCloseButton;
        Button detailsEvolutionButton;

        IPetHudProvider subscribedProvider;
        PetController detailsPet;
        string renameOriginalName = string.Empty;
        int pendingSwapSourceIndex = -1;
        bool controlsRegistered;

        sealed class TooltipData {
            public readonly string Title;
            public readonly string Body;

            public TooltipData(string title, string body) {
                Title = title;
                Body = body;
            }
        }

        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        PetCommandHudProvider CommandProvider => petHudProvider as PetCommandHudProvider;

        public event Action<string> ActionRequested;

        public bool IsRenameOpen => renameOverlay != null
            && renameOverlay.resolvedStyle.display != DisplayStyle.None;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            RegisterControls();
            SubscribeProvider();
            Refresh();
        }

        void OnDisable() {
            ClearSwapSelection();
            HideRenamePopup();
            CloseDetails();
            UnsubscribeProvider();
            UnregisterControls();
        }

        public void Bind(UIDocument targetDocument) {
            document = targetDocument != null ? targetDocument : document;
            CacheElements();
            RegisterControls();
            ResolveProvider();
            SubscribeProvider();
            Refresh();
        }

        public void Refresh() {
            if (panel == null) CacheElements();
            if (partyRow == null) return;

            ResolveProvider();
            SubscribeProvider();
            EnsurePartyCards();
            EnsureSkillCards();

            IReadOnlyList<PetSlotHudData> slots = Provider?.GetPetSlots();
            for (int i = 0; i < partyCards.Count; i++) {
                PetSlotHudData slot = slots != null && i < slots.Count ? slots[i] : default;
                RefreshPartyCard(partyCards[i], i, slot);
            }

            RefreshSelectedPet();
            RefreshSkills();
            if (IsDetailsOpen) RefreshDetails();
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            ResolveProvider();
        }

        void ResolveProvider() {
            if (Provider != null || !autoFindProvider) return;

        PetCommandHudProvider concreteProvider = FindFirstObjectByType<PetCommandHudProvider>();
            if (concreteProvider != null) petHudProvider = concreteProvider;
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;

            root = document.rootVisualElement.Q<VisualElement>("monster-inventory-root");
            panel = document.rootVisualElement.Q<VisualElement>("pets-panel");
            partyRow = document.rootVisualElement.Q<VisualElement>("pets-party-row");
            selectedPortrait = document.rootVisualElement.Q<VisualElement>("pets-selected-portrait");
            selectedPortraitFallback = document.rootVisualElement.Q<Label>("pets-selected-portrait-fallback");
            selectedName = document.rootVisualElement.Q<Label>("pets-selected-name");
            selectedSpecies = document.rootVisualElement.Q<Label>("pets-selected-species");
            selectedLevel = document.rootVisualElement.Q<Label>("pets-selected-level");
            selectedGender = document.rootVisualElement.Q<Label>("pets-selected-gender");
            selectedRarity = document.rootVisualElement.Q<Label>("pets-selected-rarity");
            selectedElement = document.rootVisualElement.Q<Label>("pets-selected-element");
            selectedHealthValue = document.rootVisualElement.Q<Label>("pets-health-value");
            selectedExperienceValue = document.rootVisualElement.Q<Label>("pets-exp-value");
            selectedHealthFill = document.rootVisualElement.Q<VisualElement>("pets-health-fill");
            selectedExperienceFill = document.rootVisualElement.Q<VisualElement>("pets-exp-fill");
            statHealth = document.rootVisualElement.Q<Label>("pets-stat-health");
            statAttack = document.rootVisualElement.Q<Label>("pets-stat-attack");
            statDefense = document.rootVisualElement.Q<Label>("pets-stat-defense");
            statSpeed = document.rootVisualElement.Q<Label>("pets-stat-speed");
            skillsRow = document.rootVisualElement.Q<VisualElement>("pets-skills-row");
            feedbackLabel = document.rootVisualElement.Q<Label>("pets-feedback-label");
            tooltip = document.rootVisualElement.Q<VisualElement>("pets-tooltip");
            tooltipTitle = document.rootVisualElement.Q<Label>("pets-tooltip-title");
            tooltipBody = document.rootVisualElement.Q<Label>("pets-tooltip-body");
            backButton = document.rootVisualElement.Q<Button>("pets-back-button");
            renameButton = document.rootVisualElement.Q<Button>("pets-rename-button");
            favoriteButton = document.rootVisualElement.Q<Button>("pets-favorite-button");
            evolutionButton = document.rootVisualElement.Q<Button>("pets-evolution-button");
            renameOverlay = document.rootVisualElement.Q<VisualElement>("pets-rename-overlay");
            renamePortrait = document.rootVisualElement.Q<VisualElement>("pets-rename-portrait");
            renamePortraitFallback = document.rootVisualElement.Q<Label>("pets-rename-portrait-fallback");
            renameCurrentName = document.rootVisualElement.Q<Label>("pets-rename-current-name");
            renameInput = document.rootVisualElement.Q<TextField>("pets-rename-input");
            renamePlaceholder = document.rootVisualElement.Q<Label>("pets-rename-placeholder");
            renameCounter = document.rootVisualElement.Q<Label>("pets-rename-counter");
            renameError = document.rootVisualElement.Q<Label>("pets-rename-error");
            renameSaveButton = document.rootVisualElement.Q<Button>("pets-rename-save");
            renameCancelButton = document.rootVisualElement.Q<Button>("pets-rename-cancel");
            renameCloseButton = document.rootVisualElement.Q<Button>("pets-rename-close");
            detailsOverlay = document.rootVisualElement.Q<VisualElement>("pet-details-overlay");
            detailsPortrait = document.rootVisualElement.Q<VisualElement>("pet-details-portrait");
            detailsPortraitFallback = document.rootVisualElement.Q<Label>("pet-details-portrait-fallback");
            detailsSkillsRow = document.rootVisualElement.Q<VisualElement>("pet-details-skills-row");
            evolutionFlow = document.rootVisualElement.Q<VisualElement>("pet-details-evolution-flow");
            evolutionMax = document.rootVisualElement.Q<VisualElement>("pet-details-evolution-max");
            evolutionRequirements = document.rootVisualElement.Q<VisualElement>("pet-details-evolution-requirements");
            evolutionCurrentIcon = document.rootVisualElement.Q<VisualElement>("pet-details-evolution-current-icon");
            evolutionCurrentFallback = document.rootVisualElement.Q<Label>("pet-details-evolution-current-fallback");
            evolutionNextIcon = document.rootVisualElement.Q<VisualElement>("pet-details-evolution-next-icon");
            evolutionNextFallback = document.rootVisualElement.Q<Label>("pet-details-evolution-next-fallback");
            detailsCloseButton = document.rootVisualElement.Q<Button>("pet-details-close-button");
            detailsEvolutionButton = document.rootVisualElement.Q<Button>("pet-details-evolution-button");
        }

        void RegisterControls() {
            if (controlsRegistered || root == null) return;

            RegisterButton(backButton, ClosePanel);
            RegisterButton(renameButton, OpenRenamePopup);
            RegisterButton(favoriteButton, ToggleFavorite);
            RegisterButton(renameSaveButton, SaveNickname);
            RegisterButton(renameCancelButton, HideRenamePopup);
            RegisterButton(renameCloseButton, HideRenamePopup);
            if (renameInput != null) {
                renameInput.maxLength = RenameMaxLength;
                renameInput.RegisterCallback<KeyDownEvent>(HandleRenameKeyDown);
                renameInput.RegisterValueChangedCallback(HandleRenameValueChanged);
            }
            Button levelUpButton = document?.rootVisualElement?.Q<Button>("pets-level-up-button");
            RegisterButton(levelUpButton, OpenLevelUpPanel);
            if (levelUpButton != null) {
                levelUpButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(levelUpButton, "Level Up", "Tăng cấp pet đang chọn."));
                levelUpButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            Button healButton = document?.rootVisualElement?.Q<Button>("pets-heal-button");
            RegisterButton(healButton, OpenHealPanel);
            if (healButton != null) {
                healButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(healButton, "Heal", "Hồi HP cho pet đang chọn bằng Potion."));
                healButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            Button skillsButton = document?.rootVisualElement?.Q<Button>("pets-skills-button");
            RegisterButton(skillsButton, OpenSkillsPanel);
            if (skillsButton != null) {
                skillsButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(skillsButton, "Skills", "Quản lý bộ kỹ năng của pet."));
                skillsButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            Button detailsButton = document?.rootVisualElement?.Q<Button>("pets-details-button");
            RegisterButton(detailsButton, () => OpenDetails());
            if (detailsButton != null) {
                detailsButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(detailsButton, "Details", "Mở thông tin chi tiết và Codex."));
                detailsButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            Button boxButton = document?.rootVisualElement?.Q<Button>("pets-box-button");
            RegisterButton(boxButton, OpenBoxPanel);
            if (boxButton != null) {
                boxButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(boxButton, "Box", "Mở kho pet và thay đổi đội hình."));
                boxButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            RegisterButton(evolutionButton, () => OpenEvolutionPanel());
            if (evolutionButton != null) {
                evolutionButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(evolutionButton, "Evolution", "Xem điều kiện và tiến hóa pet theo đúng thứ tự form."));
                evolutionButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            Button releaseButton = document?.rootVisualElement?.Q<Button>("pets-release-button");
            RegisterButton(releaseButton, OpenReleasePanel);
            if (releaseButton != null) {
                releaseButton.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(releaseButton, "Release", "Thả pet và nhận lại một phần tài nguyên đã đầu tư."));
                releaseButton.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }
            RegisterButton(detailsCloseButton, CloseDetails);
            RegisterButton(detailsEvolutionButton, () => OpenEvolutionPanel(detailsPet));

            controlsRegistered = true;
        }

        void UnregisterControls() {
            foreach (var pair in buttonCallbacks) pair.Key.clicked -= pair.Value;
            buttonCallbacks.Clear();

            if (root != null) {
            }
            if (renameInput != null) {
                renameInput.UnregisterCallback<KeyDownEvent>(HandleRenameKeyDown);
                renameInput.UnregisterValueChangedCallback(HandleRenameValueChanged);
            }

            controlsRegistered = false;
        }

        void RegisterActionButton(string buttonName, string title, string description) {
            Button button = document?.rootVisualElement?.Q<Button>(buttonName);
            RegisterButton(button, () => RequestPlaceholderAction(title, description));
            if (button == null) return;

            button.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(button, title, description));
            button.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
        }

        void OpenBoxPanel() {
            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetBoxPanel(true);
                return;
            }

            RequestPlaceholderAction("Box", "Không tìm thấy MonsterInventoryController để mở Box.");
        }

        void OpenSkillsPanel() {
            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetSkillsPanel();
                return;
            }

            RequestPlaceholderAction("Skills", "Không tìm thấy MonsterInventoryController để mở Skills Panel.");
        }

        void OpenLevelUpPanel() {
            PetController target = CommandProvider?.GetSelectedPetController();
            if (target == null) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để tăng cấp.";
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetLevelUpPanel(target);
                return;
            }

            RequestPlaceholderAction("Level Up", "Không tìm thấy MonsterInventoryController để mở Level Up Panel.");
        }

        void OpenHealPanel() {
            PetController target = CommandProvider?.GetSelectedPetController();
            if (target == null) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để hồi phục.";
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetHealPanel(target);
                return;
            }

            RequestPlaceholderAction("Heal", "Không tìm thấy MonsterInventoryController để mở Heal Popup.");
        }

        void OpenEvolutionPanel(PetController pet = null) {
            PetController target = pet != null ? pet : CommandProvider?.GetSelectedPetController();
            if (target == null) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để tiến hóa.";
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetEvolutionPanel(target);
                return;
            }

            RequestPlaceholderAction("Evolution", "Không tìm thấy MonsterInventoryController để mở Evolution Panel.");
        }

        void OpenReleasePanel() {
            PetController target = CommandProvider?.GetSelectedPetController();
            if (target == null) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để thả.";
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetReleasePanel(target);
                return;
            }

            RequestPlaceholderAction("Release", "Không tìm thấy MonsterInventoryController để mở Release Popup.");
        }

        public bool IsDetailsOpen => detailsOverlay != null
            && detailsOverlay.resolvedStyle.display != DisplayStyle.None;

        public void OpenDetails(PetController targetPet = null) {
            if (detailsOverlay == null) CacheElements();
            PetController target = targetPet != null ? targetPet : CommandProvider?.GetSelectedPetController();
            if (target == null || detailsOverlay == null) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để xem chi tiết.";
                return;
            }

            detailsPet = target;
            HideTooltip();
            HideRenamePopup();
            RefreshDetails();
            detailsOverlay.style.display = DisplayStyle.Flex;
            detailsOverlay.BringToFront();
        }

        public void CloseDetails() {
            if (detailsOverlay != null) detailsOverlay.style.display = DisplayStyle.None;
            detailsPet = null;
        }

        void RefreshDetails() {
            if (detailsPet == null || document == null || document.rootVisualElement == null) return;

            VisualElement documentRoot = document.rootVisualElement;
            IPetHudDataSource source = FindPetDataSource(detailsPet);
            IPetSkillLoadoutDataSource loadout = FindPetLoadoutSource(detailsPet);
            PetCollectionMetadata metadata = detailsPet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null) metadata = detailsPet.gameObject.AddComponent<PetCollectionMetadata>();

            string baseName = CleanPetName(source?.DisplayName, detailsPet.name);
            string displayName = metadata.ResolveDisplayName(baseName);
            int level = source != null ? Mathf.Max(0, source.Level) : 0;
            float health = source != null ? Mathf.Max(0f, source.Health) : 0f;
            float maxHealth = source != null ? Mathf.Max(0f, source.MaxHealth) : 0f;
            IReadOnlyList<SkillHudData> equippedSkills = source?.GetSkills();

            SetPortrait(detailsPortrait, detailsPortraitFallback, source?.Icon, displayName);
            SetNamedText(documentRoot, "pet-details-name", displayName);
            SetNamedText(documentRoot, "pet-details-species", DisplayOrDash(metadata.Species));
            SetNamedText(documentRoot, "pet-details-gender", DisplayOrDash(metadata.Gender));
            SetNamedText(documentRoot, "pet-details-element", FormatElement(metadata.Element));
            SetNamedText(documentRoot, "pet-details-rarity", FormatRarity(metadata.Rarity));
            SetNamedText(documentRoot, "pet-details-level", $"{level} / {metadata.MaxLevel}");
            SetNamedText(documentRoot, "pet-details-exp-value", FormatPair(metadata.Experience, metadata.ExperienceToNextLevel));
            SetNamedText(documentRoot, "pet-details-hp-value", FormatPair(health, maxHealth));
            SetFill(documentRoot.Q<VisualElement>("pet-details-exp-fill"), Percent(metadata.Experience, metadata.ExperienceToNextLevel));
            SetFill(documentRoot.Q<VisualElement>("pet-details-hp-fill"), Percent(health, maxHealth));

            SetNamedText(documentRoot, "pet-details-id", DisplayOrDash(metadata.PetId));
            SetNamedText(documentRoot, "pet-details-capture-date", DisplayOrDash(metadata.CaptureDate));
            SetNamedText(documentRoot, "pet-details-capture-location", DisplayOrDash(metadata.CaptureLocation));
            SetNamedText(documentRoot, "pet-details-trainer", DisplayOrDash(metadata.TrainerName));
            SetNamedText(documentRoot, "pet-details-personality", DisplayOrDash(metadata.Personality));
            string codexState = metadata.CodexRegistered ? "Đã đăng ký" : "Chưa đăng ký";
            SetNamedText(documentRoot, "pet-details-codex-state-basic", codexState);

            int largestCoreStat = Mathf.Max(1, metadata.Attack, metadata.Defense, metadata.Speed);
            SetDetailStat(documentRoot, "hp", maxHealth, Percent(health, maxHealth), false);
            SetDetailStat(documentRoot, "atk", metadata.Attack, Percent(metadata.Attack, largestCoreStat), false);
            SetDetailStat(documentRoot, "def", metadata.Defense, Percent(metadata.Defense, largestCoreStat), false);
            SetDetailStat(documentRoot, "spd", metadata.Speed, Percent(metadata.Speed, largestCoreStat), false);
            SetDetailStat(documentRoot, "crit-rate", metadata.CriticalRate, metadata.CriticalRate / 100f, true);
            SetDetailStat(documentRoot, "crit-dmg", metadata.CriticalDamagePercent, metadata.CriticalDamagePercent / 200f, true);

            RebuildDetailsSkills(equippedSkills, loadout != null
                ? loadout.EquippedSkillSlotCount
                : Mathf.Clamp(equippedSkills?.Count ?? 2, 2, 4));

            SetNamedText(documentRoot, "pet-details-codex-state", codexState);
            SetNamedText(documentRoot, "pet-details-codex-number", DisplayOrDash(metadata.CodexNumber));
            SetNamedText(documentRoot, "pet-details-codex-description",
                string.IsNullOrWhiteSpace(metadata.CodexDescription)
                    ? "Chưa có mô tả Codex cho pet này."
                    : metadata.CodexDescription.Trim());
            RefreshEvolution(documentRoot, metadata, source?.Icon, displayName);
        }

        void RebuildDetailsSkills(IReadOnlyList<SkillHudData> skills, int requestedSlotCount) {
            if (detailsSkillsRow == null) return;
            detailsSkillsRow.Clear();
            int slotCount = Mathf.Clamp(requestedSlotCount, 2, 4);

            for (int i = 0; i < slotCount; i++) {
                bool hasSkill = skills != null && i < skills.Count && skills[i].unlocked;
                SkillHudData skill = hasSkill ? skills[i] : default;
                var card = new VisualElement();
                card.AddToClassList("pet-details-skill-card");

                var icon = new VisualElement();
                icon.AddToClassList("pet-details-skill-icon");
                var fallback = CreateLabel(hasSkill ? FirstLetter(skill.displayName) : "?", "pet-details-skill-fallback");
                icon.Add(fallback);
                if (hasSkill && skill.icon != null) {
                    icon.style.backgroundImage = new StyleBackground(skill.icon);
                    fallback.style.display = DisplayStyle.None;
                }
                card.Add(icon);

                var text = new VisualElement();
                text.AddToClassList("pet-details-skill-text");
                text.Add(CreateLabel(hasSkill ? SafeName(skill.displayName) : "Chưa mở", "pet-details-skill-name"));
                text.Add(CreateLabel(hasSkill ? FormatElement(skill.element) : "Hệ: -", "pet-details-skill-element"));
                text.Add(CreateLabel("PP - / -", "pet-details-skill-pp"));
                card.Add(text);
                detailsSkillsRow.Add(card);
            }
        }

        void RefreshEvolution(VisualElement documentRoot, PetCollectionMetadata metadata, Sprite currentIcon, string currentName) {
            SetPortrait(evolutionCurrentIcon, evolutionCurrentFallback, currentIcon, currentName);
            SetNamedText(documentRoot, "pet-details-evolution-current-name", currentName);

            bool hasNext = metadata.HasNextEvolution;
            if (detailsEvolutionButton != null) detailsEvolutionButton.SetEnabled(hasNext);
            SetVisible(evolutionFlow, hasNext);
            SetVisible(evolutionMax, !hasNext);
            SetVisible(evolutionRequirements, hasNext);
            if (!hasNext) return;

            bool discovered = metadata.NextEvolutionDiscovered;
            SetPortrait(evolutionNextIcon, evolutionNextFallback, metadata.NextEvolutionIcon,
                discovered ? metadata.NextEvolutionName : "?");
            evolutionNextIcon?.EnableInClassList("is-silhouette", !discovered);
            SetNamedText(documentRoot, "pet-details-evolution-next-name",
                discovered ? DisplayOrDash(metadata.NextEvolutionName) : "???");

            Label levelRequirement = documentRoot.Q<Label>("pet-details-evolution-level");
            Label friendshipRequirement = documentRoot.Q<Label>("pet-details-evolution-friendship");
            Label extraRequirement = documentRoot.Q<Label>("pet-details-evolution-extra");
            bool hasLevel = metadata.EvolutionLevelRequirement > 0;
            bool hasFriendship = metadata.EvolutionFriendshipRequirement > 0;
            PetEvolutionService service = GetComponent<PetEvolutionService>();
            PetEvolutionPreview preview = service?.CreatePreview(detailsPet);
            bool usesItem = preview?.Rule?.Trigger == PetEvolutionTrigger.Item;
            bool hasExtra = usesItem || !string.IsNullOrWhiteSpace(metadata.ExtraEvolutionRequirement);

            SetVisible(levelRequirement, hasLevel || (!hasFriendship && !hasExtra));
            SetVisible(friendshipRequirement, hasFriendship);
            SetVisible(extraRequirement, hasExtra);
            if (levelRequirement != null) levelRequirement.text = hasLevel
                ? $"• Đạt Lv. {metadata.EvolutionLevelRequirement}"
                : "• Chưa thiết lập điều kiện";
            if (friendshipRequirement != null) friendshipRequirement.text = $"• Tình bạn ≥ {metadata.EvolutionFriendshipRequirement}";
            if (extraRequirement != null) {
                extraRequirement.text = usesItem
                    ? $"• Cần {preview.RequiredItemQuantity} {preview.RequiredItemId} ({preview.OwnedItemQuantity} đang có)"
                    : "• " + metadata.ExtraEvolutionRequirement.Trim();
            }
        }

        void RegisterButton(Button button, Action callback) {
            if (button == null || callback == null || buttonCallbacks.ContainsKey(button)) return;
            button.clicked += callback;
            buttonCallbacks.Add(button, callback);
        }

        void SubscribeProvider() {
            IPetHudProvider provider = Provider;
            if (ReferenceEquals(subscribedProvider, provider)) return;

            UnsubscribeProvider();
            subscribedProvider = provider;
            if (subscribedProvider != null) subscribedProvider.HudDataChanged += HandleHudDataChanged;
        }

        void UnsubscribeProvider() {
            if (subscribedProvider != null) subscribedProvider.HudDataChanged -= HandleHudDataChanged;
            subscribedProvider = null;
        }

        void HandleHudDataChanged() {
            Refresh();
        }

        void EnsurePartyCards() {
            if (partyRow == null || partyCards.Count == 6) return;

            partyRow.Clear();
            partyCards.Clear();

            for (int i = 0; i < 6; i++) {
                int slotIndex = i;
                var card = new Button { name = $"pets-party-card-{i + 1}", text = string.Empty, userData = i };
                card.AddToClassList("pet-party-card");
                card.RegisterCallback<ClickEvent>(
                    evt => HandlePartyCardClick(slotIndex, evt.clickCount),
                    TrickleDown.TrickleDown);

                var top = new VisualElement();
                top.AddToClassList("pet-card-top");
                top.Add(CreateLabel((i + 1).ToString(), "pet-card-slot-number"));
                top.Add(CreateLabel(i == 0 ? "LEAD" : string.Empty, "pet-card-lead"));
                top.Add(CreateLabel(string.Empty, "pet-card-favorite"));
                top.Add(CreateLabel(string.Empty, "pet-card-status"));
                card.Add(top);

                var content = new VisualElement();
                content.AddToClassList("pet-card-content");
                var portrait = new VisualElement { name = "portrait" };
                portrait.AddToClassList("pet-card-portrait");
                portrait.Add(CreateLabel("?", "pet-card-portrait-fallback"));
                content.Add(portrait);

                var info = new VisualElement();
                info.AddToClassList("pet-card-info");
                info.Add(CreateLabel("Empty", "pet-card-name"));
                info.Add(CreateLabel("Lv. -", "pet-card-level"));
                info.Add(CreateLabel("-  •  -  •  -", "pet-card-meta"));
                info.Add(CreateBar("HP", "pet-card-hp-fill", "pet-card-hp-value"));
                info.Add(CreateBar("EXP", "pet-card-exp-fill", "pet-card-exp-value"));
                content.Add(info);
                card.Add(content);

                partyCards.Add(card);
                partyRow.Add(card);
            }
        }

        void EnsureSkillCards() {
            if (skillsRow == null || skillCards.Count == 4) return;

            skillsRow.Clear();
            skillCards.Clear();
            for (int i = 0; i < 4; i++) {
                var card = new VisualElement { name = $"pets-skill-card-{i + 1}" };
                card.AddToClassList("pet-skill-card");
                var icon = new VisualElement { name = "skill-icon" };
                icon.AddToClassList("pet-skill-icon");
                icon.Add(CreateLabel((i + 1).ToString(), "pet-skill-icon-fallback"));
                card.Add(icon);
                var text = new VisualElement();
                text.AddToClassList("pet-skill-text");
                text.Add(CreateLabel("Locked", "pet-skill-name"));
                text.Add(CreateLabel("-", "pet-skill-meta"));
                card.Add(text);
                card.RegisterCallback<PointerEnterEvent>(_ => {
                    if (card.userData is TooltipData data) ShowTooltip(card, data.Title, data.Body);
                });
                card.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
                skillCards.Add(card);
                skillsRow.Add(card);
            }
        }

        void RefreshPartyCard(Button card, int index, PetSlotHudData slot) {
            card.EnableInClassList("is-empty", !slot.occupied);
            card.EnableInClassList("is-selected", slot.selected);
            card.EnableInClassList("is-summoned", slot.summoned);

            SetText(card, "pet-card-name", slot.occupied ? SafeName(slot.displayName) : "Empty");
            SetText(card, "pet-card-level", slot.occupied && slot.level > 0 ? $"Lv. {slot.level}" : "Lv. -");
            SetText(card, "pet-card-meta", slot.occupied ? "-  •  -  •  -" : "Empty slot");
            SetText(card, "pet-card-favorite", slot.occupied && slot.favorite ? "\u2605" : string.Empty);
            SetText(card, "pet-card-status", slot.summoned ? "ACTIVE" : string.Empty);
            PetController slotPet = slot.occupied ? CommandProvider?.GetPetControllerAt(index) : null;
            PetHudRuntimeStats runtimeStats = slotPet != null
                ? slotPet.GetComponentInChildren<PetHudRuntimeStats>(true)
                : null;
            PetCollectionMetadata metadata = slotPet != null
                ? slotPet.GetComponentInChildren<PetCollectionMetadata>(true)
                : null;
            SetText(card, "pet-card-hp-value", runtimeStats != null
                ? FormatPair(runtimeStats.Health, runtimeStats.MaxHealth)
                : "-");
            SetText(card, "pet-card-exp-value", metadata != null
                ? FormatPair(metadata.Experience, metadata.ExperienceToNextLevel)
                : "-");
            SetFill(card.Q<VisualElement>("pet-card-hp-fill"), runtimeStats != null
                ? Percent(runtimeStats.Health, runtimeStats.MaxHealth)
                : 0f);
            SetFill(card.Q<VisualElement>("pet-card-exp-fill"), metadata != null
                ? Percent(metadata.Experience, metadata.ExperienceToNextLevel)
                : 0f);

            VisualElement portrait = card.Q<VisualElement>("portrait");
            Label fallback = portrait?.Q<Label>(className: "pet-card-portrait-fallback");
            SetPortrait(portrait, fallback, slot.icon, slot.occupied ? SafeName(slot.displayName) : "?");
        }

        void RefreshSelectedPet() {
            PetStatusHudData pet = Provider != null ? Provider.GetSelectedPetStatus() : default;
            bool hasPet = pet.hasPet;
            string name = hasPet ? SafeName(pet.displayName) : "Chọn một pet";
            PetController selectedController = hasPet ? CommandProvider?.GetSelectedPetController() : null;
            PetCollectionMetadata metadata = selectedController != null
                ? selectedController.GetComponentInChildren<PetCollectionMetadata>(true)
                : null;

            if (selectedName != null) selectedName.text = name;
            bool favorite = hasPet && CommandProvider != null && CommandProvider.IsSelectedPetFavorite();
            if (renameButton != null) renameButton.SetEnabled(hasPet && CommandProvider != null);
            if (favoriteButton != null) {
                favoriteButton.SetEnabled(hasPet && CommandProvider != null);
                favoriteButton.text = favorite ? "★" : "☆";
                favoriteButton.tooltip = favorite ? "Bỏ yêu thích" : "Thêm vào yêu thích";
                favoriteButton.EnableInClassList("is-favorite", favorite);
            }
            if (evolutionButton != null) {
                PetEvolutionService service = GetComponent<PetEvolutionService>();
                PetEvolutionPreview evolution = hasPet ? service?.CreatePreview(selectedController) : null;
                evolutionButton.SetEnabled(evolution?.IsConfigured == true);
            }
            if (selectedSpecies != null) selectedSpecies.text = $"Loài: {(metadata != null ? DisplayOrDash(metadata.Species) : "-")}";
            if (selectedLevel != null) selectedLevel.text = hasPet && pet.level > 0 ? $"Lv. {pet.level}" : "Lv. -";
            if (selectedGender != null) selectedGender.text = $"Giới tính: {(metadata != null ? DisplayOrDash(metadata.Gender) : "-")}";
            if (selectedRarity != null) selectedRarity.text = $"Độ hiếm: {(metadata != null ? FormatRarity(metadata.Rarity) : "-")}";
            if (selectedElement != null) selectedElement.text = $"Hệ: {(metadata != null ? FormatElement(metadata.Element) : "-")}";
            if (selectedHealthValue != null) selectedHealthValue.text = FormatPair(pet.health, pet.maxHealth);
            if (selectedExperienceValue != null) selectedExperienceValue.text = metadata != null
                ? FormatPair(metadata.Experience, metadata.ExperienceToNextLevel)
                : "- / -";
            SetFill(selectedHealthFill, pet.HealthPercent);
            SetFill(selectedExperienceFill, metadata != null
                ? Percent(metadata.Experience, metadata.ExperienceToNextLevel)
                : 0f);
            SetPortrait(selectedPortrait, selectedPortraitFallback, pet.icon, name);

            if (statHealth != null) statHealth.text = pet.maxHealth > 0f ? Mathf.RoundToInt(pet.maxHealth).ToString() : "-";
            if (statAttack != null) statAttack.text = metadata != null ? metadata.Attack.ToString() : "-";
            if (statDefense != null) statDefense.text = metadata != null ? metadata.Defense.ToString() : "-";
            if (statSpeed != null) statSpeed.text = metadata != null ? metadata.Speed.ToString() : "-";
            if (feedbackLabel != null && !hasPet) feedbackLabel.text = "Chọn một slot có pet để xem thông tin.";
        }

        void RefreshSkills() {
            IReadOnlyList<SkillHudData> skills = Provider?.GetSkills();
            int slotCount = CommandProvider != null
                ? CommandProvider.GetEquippedSkillSlotCount()
                : skills != null && skills.Count > 0 ? Mathf.Clamp(skills.Count, 2, 4) : 0;
            for (int i = 0; i < skillCards.Count; i++) {
                bool visible = i < slotCount;
                skillCards[i].style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) {
                    skillCards[i].userData = null;
                    continue;
                }
                bool available = skills != null && i < skills.Count && skills[i].unlocked;
                SkillHudData skill = available ? skills[i] : default;
                VisualElement card = skillCards[i];
                card.EnableInClassList("is-locked", !available);
                SetText(card, "pet-skill-name", available ? SafeName(skill.displayName) : "Locked");
                SetText(card, "pet-skill-meta", available ? $"Lv. {Mathf.Max(1, skill.skillLevel)}" : "-");
                VisualElement icon = card.Q<VisualElement>("skill-icon");
                Label fallback = icon?.Q<Label>(className: "pet-skill-icon-fallback");
                SetPortrait(icon, fallback, skill.icon, available ? SafeName(skill.displayName) : (i + 1).ToString());

                string title = available ? SafeName(skill.displayName) : "Kỹ năng chưa mở";
                string body = available
                    ? BuildSkillTooltip(skill)
                    : "Pet này chưa có kỹ năng ở ô này.";
                card.tooltip = body;
                card.userData = new TooltipData(title, body);
            }
        }

        void SelectSlot(int index) {
            Provider?.SelectPetSlot(index);
            Refresh();
        }

        void HandlePartyCardClick(int index, int clickCount) {
            if (index < 0 || index >= partyCards.Count) return;

            if (pendingSwapSourceIndex >= 0 && clickCount == 1) {
                TryCompletePartySwap(index);
                return;
            }

            SelectSlot(index);
            if (clickCount < 2) return;

            IReadOnlyList<PetSlotHudData> slots = Provider?.GetPetSlots();
            PetSlotHudData slot = slots != null && index < slots.Count ? slots[index] : default;
            if (!slot.occupied) {
                if (feedbackLabel != null) feedbackLabel.text = "Hãy double-click một ô đang có pet.";
                return;
            }

            pendingSwapSourceIndex = index;
            UpdateSwapSelectionVisual();
            if (feedbackLabel != null) {
                feedbackLabel.text = $"Đã chọn vị trí {index + 1}. Click ô đích để đổi.";
            }
        }

        void TryCompletePartySwap(int targetIndex) {
            int sourceIndex = pendingSwapSourceIndex;
            ClearSwapSelection();

            if (targetIndex == sourceIndex) {
                if (feedbackLabel != null) feedbackLabel.text = "Đã hủy đổi vị trí.";
                SelectSlot(targetIndex);
                return;
            }

            if (Provider is PetCommandHudProvider provider
                && provider.TrySwapPetSlots(sourceIndex, targetIndex)) {
                if (feedbackLabel != null) {
                    feedbackLabel.text = $"Đã đổi vị trí {sourceIndex + 1} và {targetIndex + 1}.";
                }

                Provider.SelectPetSlot(targetIndex);
                Refresh();
                return;
            }

            if (feedbackLabel != null) feedbackLabel.text = "Không thể đổi hai vị trí này.";
        }

        void UpdateSwapSelectionVisual() {
            for (int i = 0; i < partyCards.Count; i++) {
                partyCards[i].EnableInClassList("is-swap-source", i == pendingSwapSourceIndex);
            }
        }

        void ClearSwapSelection() {
            pendingSwapSourceIndex = -1;
            UpdateSwapSelectionVisual();
        }

        void RequestPlaceholderAction(string actionName, string message) {
            if (feedbackLabel != null) feedbackLabel.text = message;
            ActionRequested?.Invoke(actionName);
        }

        void ClosePanel() {
            ClearSwapSelection();
            HideRenamePopup();
            MonsterInventoryController inventory = GetComponent<MonsterInventoryController>();
            if (inventory != null) inventory.Close();
        }

        void OpenRenamePopup() {
            PetStatusHudData status = Provider != null ? Provider.GetSelectedPetStatus() : default;
            if (!status.hasPet || CommandProvider == null || renameOverlay == null) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để đổi tên.";
                return;
            }

            renameOriginalName = SafeName(status.displayName);
            if (renameCurrentName != null) renameCurrentName.text = renameOriginalName;
            SetPortrait(renamePortrait, renamePortraitFallback, status.icon, renameOriginalName);
            renameInput?.SetValueWithoutNotify(string.Empty);
            UpdateRenameValidation(false);
            HideTooltip();
            renameOverlay.style.display = DisplayStyle.Flex;
            renameOverlay.BringToFront();
            renameInput?.Focus();
        }

        void HideRenamePopup() {
            if (renameOverlay != null) renameOverlay.style.display = DisplayStyle.None;
            if (renameError != null) renameError.text = string.Empty;
            if (renameInput != null) {
                renameInput.RemoveFromClassList("is-invalid");
                renameInput.Blur();
            }
            renameOriginalName = string.Empty;
        }

        public void CloseRenamePopup() {
            HideRenamePopup();
        }

        void SaveNickname() {
            if (CommandProvider == null) return;
            if (!TryGetValidNickname(out string nickname, out string validationError)) {
                ShowRenameValidationError(validationError);
                return;
            }

            if (!CommandProvider.TryRenameSelectedPet(nickname, out string error)) {
                ShowRenameValidationError(error);
                return;
            }

            HideRenamePopup();
            if (feedbackLabel != null) feedbackLabel.text = $"Đã đổi tên pet thành {nickname}.";
            Refresh();
        }

        void HandleRenameValueChanged(ChangeEvent<string> evt) {
            UpdateRenameValidation(true);
        }

        void UpdateRenameValidation(bool showError) {
            string value = renameInput?.value ?? string.Empty;
            if (renameCounter != null) {
                renameCounter.text = $"{Mathf.Min(value.Length, RenameMaxLength)} / {RenameMaxLength} ký tự";
            }

            if (renamePlaceholder != null) {
                renamePlaceholder.style.display = value.Length == 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }

            bool valid = TryGetValidNickname(out _, out string error);
            renameSaveButton?.SetEnabled(valid);
            if (renameInput != null) renameInput.EnableInClassList("is-invalid", showError && !valid);
            if (renameError != null) renameError.text = showError && !valid ? error : string.Empty;
        }

        bool TryGetValidNickname(out string nickname, out string error) {
            nickname = (renameInput?.value ?? string.Empty).Trim();
            if (nickname.Length == 0) {
                error = "Tên không được để trống.";
                return false;
            }
            if (nickname.Length > RenameMaxLength) {
                error = $"Tên phải từ 1 đến {RenameMaxLength} ký tự.";
                return false;
            }
            if (string.Equals(nickname, renameOriginalName, StringComparison.Ordinal)) {
                error = "Tên mới giống tên hiện tại.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        void ShowRenameValidationError(string error) {
            if (renameInput != null) renameInput.AddToClassList("is-invalid");
            if (renameError != null) renameError.text = string.IsNullOrWhiteSpace(error)
                ? "Tên không hợp lệ."
                : error;
            renameSaveButton?.SetEnabled(false);
        }

        void ToggleFavorite() {
            if (CommandProvider == null || !CommandProvider.TryToggleSelectedPetFavorite(out bool favorite)) {
                if (feedbackLabel != null) feedbackLabel.text = "Chưa chọn pet để đánh dấu yêu thích.";
                return;
            }

            if (feedbackLabel != null) feedbackLabel.text = favorite
                ? "Đã thêm pet vào danh sách yêu thích."
                : "Đã bỏ pet khỏi danh sách yêu thích.";
            Refresh();
        }

        void HandleRenameKeyDown(KeyDownEvent evt) {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) {
                SaveNickname();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape) {
                HideRenamePopup();
                evt.StopPropagation();
            }
        }

        void ShowTooltip(VisualElement anchor, string title, string body) {
            if (tooltip == null || root == null || anchor == null) return;
            if (tooltipTitle != null) tooltipTitle.text = title;
            if (tooltipBody != null) tooltipBody.text = body;

            Rect bounds = anchor.worldBound;
            Vector2 position = root.WorldToLocal(new Vector2(bounds.xMax + 12f, bounds.yMin));
            float rootWidth = root.resolvedStyle.width;
            if (position.x + 320f > rootWidth) position.x = Mathf.Max(12f, position.x - bounds.width - 332f);
            tooltip.style.left = position.x;
            tooltip.style.top = Mathf.Max(12f, position.y);
            tooltip.style.display = DisplayStyle.Flex;
        }

        void HideTooltip() {
            if (tooltip != null) tooltip.style.display = DisplayStyle.None;
        }

        static Label CreateLabel(string text, string className) {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        static VisualElement CreateBar(string labelText, string fillName, string valueClass) {
            var row = new VisualElement();
            row.AddToClassList("pet-card-bar-row");
            row.Add(CreateLabel(labelText, "pet-card-bar-label"));
            var track = new VisualElement();
            track.AddToClassList("pet-card-bar-track");
            var fill = new VisualElement { name = fillName };
            fill.AddToClassList(fillName.Contains("hp") ? "pet-card-hp-fill" : "pet-card-exp-fill");
            track.Add(fill);
            row.Add(track);
            row.Add(CreateLabel("-", valueClass));
            return row;
        }

        static string BuildSkillTooltip(SkillHudData skill) {
            string description = string.IsNullOrWhiteSpace(skill.description) ? "Chưa có mô tả." : skill.description.Trim();
            string cooldown = skill.cooldownSeconds > 0f ? $"Cooldown: {skill.cooldownSeconds:0.#}s" : "Cooldown: -";
            return $"{description}\nCấp kỹ năng: {Mathf.Max(1, skill.skillLevel)}\n{cooldown}";
        }

        static void SetText(VisualElement rootElement, string className, string value) {
            Label label = rootElement?.Q<Label>(className: className);
            if (label != null) label.text = value;
        }

        static void SetNamedText(VisualElement rootElement, string elementName, string value) {
            Label label = rootElement?.Q<Label>(elementName);
            if (label != null) label.text = value ?? string.Empty;
        }

        static void SetDetailStat(VisualElement rootElement, string suffix, float value, float percent, bool appendPercent) {
            string valueText = value > 0f
                ? appendPercent ? $"{value:0.#}%" : Mathf.RoundToInt(value).ToString()
                : "-";
            SetNamedText(rootElement, $"pet-details-stat-{suffix}", valueText);
            SetFill(rootElement?.Q<VisualElement>($"pet-details-stat-{suffix}-fill"), percent);
        }

        static void SetVisible(VisualElement element, bool visible) {
            if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void SetPortrait(VisualElement portrait, Label fallback, Sprite sprite, string name) {
            if (portrait == null) return;
            if (sprite != null) portrait.style.backgroundImage = new StyleBackground(sprite);
            else portrait.style.backgroundImage = StyleKeyword.None;
            portrait.EnableInClassList("has-image", sprite != null);
            if (fallback != null) {
                fallback.text = FirstLetter(name);
                fallback.style.display = sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        static void SetFill(VisualElement fill, float percent) {
            if (fill == null) return;
            fill.style.width = Length.Percent(Mathf.Clamp01(percent) * 100f);
        }

        static string FirstLetter(string value) {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        static string SafeName(string value) {
            return string.IsNullOrWhiteSpace(value) ? "Pet" : value.Trim();
        }

        static string DisplayOrDash(string value) {
            return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        }

        static string CleanPetName(string preferred, string fallback) {
            string value = !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
            return string.IsNullOrWhiteSpace(value)
                ? "Pet"
                : value.Replace("(Clone)", string.Empty).Trim();
        }

        static string FormatElement(PetElement element) {
            switch (element) {
                case PetElement.Nature: return "Thảo";
                case PetElement.Fire: return "Lửa";
                case PetElement.Water: return "Nước";
                case PetElement.Wind: return "Gió";
                case PetElement.Earth: return "Đất";
                case PetElement.Electric: return "Điện";
                case PetElement.Ice: return "Băng";
                case PetElement.Light: return "Ánh sáng";
                case PetElement.Dark: return "Bóng tối";
                default: return "-";
            }
        }

        static string FormatRarity(PetRarity rarity) {
            int stars = Mathf.Clamp((int)rarity, 0, 5);
            if (stars == 0) return "-";
            return new string('★', stars) + $"  ({rarity})";
        }

        static float Percent(float current, float maximum) {
            return maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        }

        static IPetHudDataSource FindPetDataSource(PetController pet) {
            if (pet == null) return null;
            foreach (MonoBehaviour behaviour in pet.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour is IPetHudDataSource source) return source;
            }
            return null;
        }

        static IPetSkillLoadoutDataSource FindPetLoadoutSource(PetController pet) {
            if (pet == null) return null;
            foreach (MonoBehaviour behaviour in pet.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (behaviour is IPetSkillLoadoutDataSource source) return source;
            }
            return null;
        }

        static string FormatPair(float current, float maximum) {
            return maximum > 0f ? $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}" : "- / -";
        }
    }
}
