using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class PetPartyPanelController : MonoBehaviour {
        const int RenameMaxLength = 16;
        const float CriticalRateDisplayMaximum = 100f;
        const float CriticalDamageDisplayMaximum = 100f;

        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] bool autoFindProvider = true;

        readonly List<Button> partyCards = new List<Button>(6);
        readonly List<VisualElement> skillCards = new List<VisualElement>(4);
        readonly Dictionary<Button, Action> buttonCallbacks = new Dictionary<Button, Action>();
        readonly Dictionary<Button, EventCallback<PointerEnterEvent>> tooltipCallbacks = new Dictionary<Button, EventCallback<PointerEnterEvent>>();

        VisualElement root;
        MonsterInventoryController inventoryOwner;
        VisualElement panel;
        VisualElement layoutPanel;
        VisualElement partyRow;
        Label partyTitle;
        readonly PetPortraitPreview portraitPreview = new PetPortraitPreview { BackgroundColor = Color.clear };
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
        Label statMagicAttack;
        Label statMagicDefense;
        Label statCriticalRate;
        Label statCriticalDamage;
        VisualElement statHealthFill;
        VisualElement statAttackFill;
        VisualElement statDefenseFill;
        VisualElement statSpeedFill;
        VisualElement statMagicAttackFill;
        VisualElement statMagicDefenseFill;
        VisualElement statCriticalRateFill;
        VisualElement statCriticalDamageFill;
        VisualElement skillsRow;
        Label feedbackLabel;
        VisualElement tooltip;
        Label tooltipTitle;
        Label tooltipBody;
        Button backButton;
        Button renameButton;
        Button favoriteButton;
        Button evolutionButton;
        Label evolutionSubtitle;
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
        string evolutionSelectionItemId;
        PetEvolutionPanelController evolutionSelectionPanel;

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
            StopCompanionPresentation();
            ReleaseCompanionBindings();
            layoutPanel?.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometry);
            layoutPanel = null;
            portraitPreview.Clear();
            CancelEvolutionItemSelection();
            ClearSwapSelection();
            HideRenamePopup();
            CloseDetails();
            UnsubscribeProvider();
            UnregisterControls();
        }

        void OnDestroy() => portraitPreview.Dispose();
        void LateUpdate() {
            bool visible=inventoryOwner?.IsPetsSectionOpen==true;
            UpdateCompanionVisibility(visible);
            if (!visible || !preview3D) { if(portraitPreview.Texture!=null) portraitPreview.Clear(); return; }
            var pet = ViewedPet;
            if(pet==null){if(portraitPreview.Texture!=null)portraitPreview.Clear();RefreshCompanionPortrait(null);return;}
            if (selectedPortrait != null) portraitPreview.SetViewportSize(selectedPortrait.resolvedStyle.width, selectedPortrait.resolvedStyle.height);
            if (selectedPortrait != null && portraitPreview.Show(pet)) {
                portraitPreview.Update();
                var texture = (RenderTexture)portraitPreview.Texture;
                Capstone.Game.UISystem.AngularSurface.SetArt(selectedPortrait,texture);
                if (selectedPortraitFallback != null) selectedPortraitFallback.style.display = DisplayStyle.None;
            } else {
                portraitPreview.Clear();
                SetPreview3D(false);
                SetFeedback("Không thể tạo bản xem 3D cho thú này. Đã chuyển về ảnh 2D.");
            }
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
            ResolveViewedPet();
            SubscribeProvider();
            EnsurePartyCards();
            EnsureSkillCards();

            IReadOnlyList<PetSlotHudData> slots = Provider?.GetPetSlots();
            int occupiedCount = 0;
            if (slots != null) {
                for (int i = 0; i < slots.Count && i < 6; i++) {
                    if (slots[i].occupied) occupiedCount++;
                }
            }
            if (partyTitle != null) partyTitle.text = evolutionSelectionItemId != null
                ? "CHỌN THÚ ĐỂ TIẾN HÓA"
                : $"ĐỘI HÌNH ({occupiedCount}/6)";
            for (int i = 0; i < partyCards.Count; i++) {
                PetSlotHudData slot = slots != null && i < slots.Count ? slots[i] : default;
                if(CommandProvider!=null)slot.selected=slot.occupied && CommandProvider.GetPetControllerAt(i)==ViewedPet;
                RefreshPartyCard(partyCards[i], i, slot);
            }

            RefreshSelectedPet();
            RefreshSkills();
            if (IsDetailsOpen) RefreshDetails();
            RefreshCompanionPresentation(slots);
        }

        public void BeginEvolutionItemSelection(string itemId, PetEvolutionPanelController evolutionPanel) {
            if (string.IsNullOrWhiteSpace(itemId) || evolutionPanel == null) return;
            ClearSwapSelection();
            evolutionSelectionItemId = itemId;
            evolutionSelectionPanel = evolutionPanel;
            panel?.AddToClassList("is-evolution-selecting");
            SetFeedback("Chọn thú để tiến hóa");
            Refresh();
        }

        public bool HasEvolutionItemSelection => evolutionSelectionItemId != null;

        public void CancelEvolutionItemSelection() {
            evolutionSelectionItemId = null;
            evolutionSelectionPanel = null;
            panel?.RemoveFromClassList("is-evolution-selecting");
            foreach (Button card in partyCards) card.style.opacity = 1f;
            SetFeedback(string.Empty);
        }

        void ResolveReferences() {
            if (inventoryOwner == null) inventoryOwner = GetComponent<MonsterInventoryController>();
            document = document != null ? document : GetComponent<UIDocument>();
            ResolveProvider();
        }

        void ResolveProvider() {
            if (Provider != null || !autoFindProvider) return;

            PetCommandHudProvider concreteProvider = FindFirstObjectByType<PetCommandHudProvider>();
            if (concreteProvider != null) petHudProvider = concreteProvider;
        }

        void OnPanelGeometry(GeometryChangedEvent evt) {
            FitCompanionLayout();
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;

            root = document.rootVisualElement.Q<VisualElement>("monster-inventory-root");
            panel = document.rootVisualElement.Q<VisualElement>("pets-panel");
            if (layoutPanel != panel) {
                layoutPanel?.UnregisterCallback<GeometryChangedEvent>(OnPanelGeometry);
                layoutPanel = panel;
                layoutPanel?.RegisterCallback<GeometryChangedEvent>(OnPanelGeometry);
            }
            partyRow = document.rootVisualElement.Q<VisualElement>("pets-party-row");
            partyTitle = document.rootVisualElement.Q<Label>("pets-party-title");
            selectedPortrait = document.rootVisualElement.Q<VisualElement>("pets-selected-portrait");
            selectedPortraitFallback = document.rootVisualElement.Q<Label>("pets-selected-portrait-fallback");
            foreach (var label in document.rootVisualElement.Query<Label>().ToList()) {
                string key = label.text.Trim().TrimStart('♥', '⚔', '✦', '◆', '◇', '➤', '★', '◉', ' ');
                string tip = Capstone.Game.UISystem.VietnameseUI.StatTooltip(key);
                if (tip.Length > 0) label.tooltip = tip;
            }
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
            statMagicAttack = document.rootVisualElement.Q<Label>("pets-stat-magic-attack");
            statMagicDefense = document.rootVisualElement.Q<Label>("pets-stat-magic-defense");
            statCriticalRate = document.rootVisualElement.Q<Label>("pets-stat-critical-rate");
            statCriticalDamage = document.rootVisualElement.Q<Label>("pets-stat-critical-damage");
            statHealthFill = document.rootVisualElement.Q<VisualElement>("pets-stat-health-fill");
            statAttackFill = document.rootVisualElement.Q<VisualElement>("pets-stat-attack-fill");
            statDefenseFill = document.rootVisualElement.Q<VisualElement>("pets-stat-defense-fill");
            statSpeedFill = document.rootVisualElement.Q<VisualElement>("pets-stat-speed-fill");
            statMagicAttackFill = document.rootVisualElement.Q<VisualElement>("pets-stat-magic-attack-fill");
            statMagicDefenseFill = document.rootVisualElement.Q<VisualElement>("pets-stat-magic-defense-fill");
            statCriticalRateFill = document.rootVisualElement.Q<VisualElement>("pets-stat-critical-rate-fill");
            statCriticalDamageFill = document.rootVisualElement.Q<VisualElement>("pets-stat-critical-damage-fill");
            skillsRow = document.rootVisualElement.Q<VisualElement>("pets-skills-row");
            feedbackLabel = document.rootVisualElement.Q<Label>("pets-feedback-label");
            SetFeedback(feedbackLabel?.text);
            tooltip = document.rootVisualElement.Q<VisualElement>("pets-tooltip");
            tooltipTitle = document.rootVisualElement.Q<Label>("pets-tooltip-title");
            tooltipBody = document.rootVisualElement.Q<Label>("pets-tooltip-body");
            backButton = document.rootVisualElement.Q<Button>("pets-back-button");
            renameButton = document.rootVisualElement.Q<Button>("pets-rename-button");
            favoriteButton = document.rootVisualElement.Q<Button>("pets-favorite-button");
            evolutionButton = document.rootVisualElement.Q<Button>("pets-evolution-button");
            evolutionSubtitle = document.rootVisualElement.Q<Label>("pets-evolution-subtitle");
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
            CacheCompanionPresentation();
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
                RegisterActionTooltip(levelUpButton, "Tăng cấp", "Tăng cấp thú đang chọn.");
            }
            Button healButton = document?.rootVisualElement?.Q<Button>("pets-heal-button");
            RegisterButton(healButton, OpenHealPanel);
            if (healButton != null) {
                RegisterActionTooltip(healButton, "Hồi phục", "Xem vật phẩm và lượng HP/MP có thể hồi phục.");
            }
            RegisterButton(evolutionButton, () => OpenEvolutionPanel());
            if (evolutionButton != null) {
                RegisterActionTooltip(evolutionButton, "Tiến hóa", "Xem điều kiện và tiến hóa thú theo đúng thứ tự dạng.");
            }
            Button detailsButton = document?.rootVisualElement?.Q<Button>("pets-details-button");
            RegisterButton(detailsButton, () => OpenDetails());
            RegisterButton(detailsCloseButton, CloseDetails);
            RegisterButton(detailsEvolutionButton, () => OpenEvolutionPanel(detailsPet));
            RegisterCompanionControls();

            controlsRegistered = true;
        }

        void UnregisterControls() {
            foreach (var pair in buttonCallbacks) pair.Key.clicked -= pair.Value;
            buttonCallbacks.Clear();

            foreach (var pair in tooltipCallbacks) {
                pair.Key.UnregisterCallback(pair.Value);
                pair.Key.UnregisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);
            }
            tooltipCallbacks.Clear();
            if (renameInput != null) {
                renameInput.UnregisterCallback<KeyDownEvent>(HandleRenameKeyDown);
                renameInput.UnregisterValueChangedCallback(HandleRenameValueChanged);
            }

            controlsRegistered = false;
        }

        void RegisterActionTooltip(Button button, string title, string body) {
            if(button==null||tooltipCallbacks.ContainsKey(button))return;
            button.tooltip=string.Empty;
            EventCallback<PointerEnterEvent> callback = _ => ShowTooltip(button, title, actionReasons.TryGetValue(button,out var reason)&&!string.IsNullOrEmpty(reason)?reason:body);
            tooltipCallbacks.Add(button, callback);
            button.RegisterCallback(callback);
            button.RegisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);
        }
        void HandleActionTooltipLeave(PointerLeaveEvent _) => HideTooltip();

        void OpenLevelUpPanel() {
            PetController target = ViewedPet;
            if (target == null) {
                SetFeedback("Chưa chọn thú để tăng cấp.");
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetLevelUpPanel(target);
                return;
            }

            ShowMissingControllerFeedback("Không tìm thấy MonsterInventoryController để mở Cấp Up Panel.");
        }

        void OpenHealPanel() {
            PetController target = ViewedPet;
            if (target == null) {
                SetFeedback("Chưa chọn thú để hồi phục.");
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetHealPanel(target);
                return;
            }

            ShowMissingControllerFeedback("Không thể mở màn hồi phục lúc này.");
        }

        void OpenEvolutionPanel(PetController pet = null) {
            PetController target = pet != null ? pet : ViewedPet;
            if (target == null) {
                SetFeedback("Chưa chọn thú để tiến hóa.");
                return;
            }

            MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
            if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
            if (controller != null) {
                HideTooltip();
                controller.OpenPetEvolutionPanel(target);
                return;
            }

            ShowMissingControllerFeedback("Không tìm thấy MonsterInventoryController để mở Evolution Panel.");
        }

        public bool IsDetailsOpen => detailsOverlay != null
            && detailsOverlay.resolvedStyle.display != DisplayStyle.None;

        public void OpenDetails(PetController targetPet = null) {
            if (detailsOverlay == null) CacheElements();
            PetController target = targetPet != null ? targetPet : ViewedPet;
            if (target == null || detailsOverlay == null) {
                SetFeedback("Chọn thú đồng hành để xem chi tiết.");
                return;
            }

            detailsPet = target;
            HideTooltip();
            HideRenamePopup();
            RefreshDetails();
            detailsOverlay.style.display = DisplayStyle.Flex;
            detailsOverlay.BringToFront();
            Capstone.Game.UISystem.PanelFocusUtility.Open(detailsOverlay);
        }

        public void CloseDetails() {
            if (detailsOverlay != null) detailsOverlay.style.display = DisplayStyle.None;
            Capstone.Game.UISystem.PanelFocusUtility.Close(detailsOverlay);
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
            SetNamedText(documentRoot, "pet-details-gender", Capstone.Game.UISystem.VietnameseUI.Gender(metadata.Gender));
            SetNamedText(documentRoot, "pet-details-element", FormatElements(metadata));
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
            SetNamedText(documentRoot, "pet-details-personality", Capstone.Game.UISystem.VietnameseUI.Personality(metadata.Personality));
            SetNamedText(documentRoot, "pet-details-nature", Capstone.Game.UISystem.VietnameseUI.Nature(metadata.Nature));
            SetNamedText(documentRoot, "pet-details-bst", metadata.BaseStatTotal.ToString());
            RefreshGeneticsTable(documentRoot, metadata);
            string codexState = metadata.CodexRegistered ? "Đã đăng ký" : "Chưa đăng ký";
            SetNamedText(documentRoot, "pet-details-codex-state-basic", codexState);

            PetStatSnapshot scores = metadata.GetStatsAtLevel(level);
            SetDetailStat(documentRoot, "hp", scores.Hp, PetStatCalculator.DisplayRatio(scores.Hp, level, PetCoreStat.Hp), false);
            SetDetailStat(documentRoot, "atk", scores.Attack, PetStatCalculator.DisplayRatio(scores.Attack, level, PetCoreStat.Attack), false);
            SetDetailStat(documentRoot, "magic-atk", scores.MagicAttack, PetStatCalculator.DisplayRatio(scores.MagicAttack, level, PetCoreStat.MagicAttack), false);
            SetDetailStat(documentRoot, "def", scores.Defense, PetStatCalculator.DisplayRatio(scores.Defense, level, PetCoreStat.Defense), false);
            SetDetailStat(documentRoot, "magic-def", scores.MagicDefense, PetStatCalculator.DisplayRatio(scores.MagicDefense, level, PetCoreStat.MagicDefense), false);
            SetDetailStat(documentRoot, "spd", scores.Speed, PetStatCalculator.DisplayRatio(scores.Speed, level, PetCoreStat.Speed), false);
            SetDetailStat(documentRoot, "crit-rate", scores.CriticalRate, scores.CriticalRate / CriticalRateDisplayMaximum, true);
            SetDetailStat(documentRoot, "crit-dmg", scores.CriticalDamage, scores.CriticalDamage / CriticalDamageDisplayMaximum, true);

            RebuildDetailsSkills(equippedSkills, loadout != null
                ? loadout.EquippedSkillSlotCount
                : Mathf.Clamp(equippedSkills?.Count ?? 2, 2, 4));

            SetNamedText(documentRoot, "pet-details-codex-state", codexState);
            SetNamedText(documentRoot, "pet-details-codex-number", DisplayOrDash(metadata.CodexNumber));
            SetNamedText(documentRoot, "pet-details-codex-description",
                Capstone.Game.UISystem.VietnameseUI.PetDescription(metadata.CodexDescription));
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
                CompanionArt.SetSkillImage(icon,fallback,hasSkill?skill.icon:null,!hasSkill);
                card.Add(icon);

                var text = new VisualElement();
                text.AddToClassList("pet-details-skill-text");
                text.Add(CreateLabel(hasSkill ? SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName)) : "Chưa mở", "pet-details-skill-name"));
                text.Add(CreateLabel(hasSkill ? FormatElement(skill.element) : "Hệ: -", "pet-details-skill-element"));
                text.Add(CreateLabel("Lượt dùng: chưa có dữ liệu", "pet-details-skill-pp"));
                card.Add(text);
                detailsSkillsRow.Add(card);
            }
        }

        void RefreshEvolution(VisualElement documentRoot, PetCollectionMetadata metadata, Sprite currentIcon, string currentName) {
            SetPortrait(evolutionCurrentIcon, evolutionCurrentFallback, currentIcon, currentName);
            SetNamedText(documentRoot, "pet-details-evolution-current-name", currentName);

            bool hasNext = metadata.HasNextEvolution;
            if (detailsEvolutionButton != null) detailsEvolutionButton.SetEnabled(hasNext
                && metadata.TryGetNextEvolutionRule(out PetEvolutionRule detailRule)
                && detailRule.Trigger != PetEvolutionTrigger.Item);
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
            bool hasFriendship = metadata.EvolutionFriendshipRequirement > 0;
            PetEvolutionService service = GetComponent<PetEvolutionService>();
            PetEvolutionPreview preview = service?.CreatePreview(detailsPet);
            bool hasLevel = preview?.Rule?.Trigger != PetEvolutionTrigger.Item && preview?.RequiredLevel > 0;
            bool hasExtra = !string.IsNullOrWhiteSpace(metadata.ExtraEvolutionRequirement);

            SetVisible(levelRequirement, hasLevel || (!hasFriendship && !hasExtra));
            SetVisible(friendshipRequirement, hasFriendship);
            SetVisible(extraRequirement, hasExtra);
            if (levelRequirement != null) levelRequirement.text = hasLevel
                ? $"• Cấp {preview.CurrentLevel}/{preview.RequiredLevel}"
                : "• Dùng vật phẩm trong Túi đồ";
            if (friendshipRequirement != null) friendshipRequirement.text = $"• Tình bạn ≥ {metadata.EvolutionFriendshipRequirement}";
            if (extraRequirement != null) {
                extraRequirement.text = "• " + metadata.ExtraEvolutionRequirement.Trim();
            }
        }

        void RegisterButton(Button button, Action callback) {
            if (button == null || callback == null || buttonCallbacks.ContainsKey(button)) return;
            bool previewOnly=button.name.StartsWith("pets-tab-")||button.name.StartsWith("pets-mode-")||button.name=="pets-swap-button"||button.name=="pets-skills-details";
            Action guarded = previewOnly?callback:Capstone.Game.UISystem.PlayerActionGate.Guard(this, "button:" + button.name, callback);
            button.clicked += guarded;
            buttonCallbacks.Add(button, guarded);
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
            if (panel != null && panel.resolvedStyle.display != DisplayStyle.None
                && (inventoryOwner == null || inventoryOwner.IsPetsSectionOpen)) Refresh();
        }

        void EnsurePartyCards() {
            if (partyRow == null || partyCards.Count == 6) return;

            partyRow.Clear();
            partyCards.Clear();

            for (int i = 0; i < 6; i++) {
                int slotIndex = i;
                var card = new Capstone.Game.UISystem.AngularButton { name = $"thú-party-card-{i + 1}", text = string.Empty, userData = i };
                card.AddToClassList("pet-party-card");
                card.RegisterCallback<ClickEvent>(
                    evt => {
                        if (evt.target is VisualElement target && (target.ClassListContains("pet-card-favorite")
                            || target.GetFirstAncestorOfType<Button>()?.ClassListContains("pet-card-favorite") == true)) return;
                        HandlePartyCardClick(slotIndex, evt.clickCount);
                    },
                    TrickleDown.TrickleDown);

                var top = new VisualElement();
                top.AddToClassList("pet-card-top");
                top.Add(CreateLabel((i + 1).ToString(), "pet-card-slot-number"));
                var elementIcons = new VisualElement();
                elementIcons.AddToClassList("pet-card-element-icons");
                elementIcons.Add(CreateLabel(string.Empty, "pet-card-element pet-card-element-primary"));
                elementIcons.Add(CreateLabel(string.Empty, "pet-card-element pet-card-element-secondary"));
                top.Add(elementIcons);
                var favorite = new Button(() => ToggleFavoriteAt(slotIndex)) { name = "favorite", text = "★", tooltip = "Bỏ yêu thích" };
                favorite.style.display = DisplayStyle.None;
                favorite.AddToClassList("pet-card-favorite");
                favorite.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
                top.Add(favorite);
                card.Add(top);

                var content = new VisualElement();
                content.AddToClassList("pet-card-content");
                var portrait = new Capstone.Game.UISystem.AngularSurface { name = "portrait",NoFill=true,NoBorder=true };
                portrait.AddToClassList("pet-card-portrait");
                portrait.Add(CreateLabel("🐾", "pet-card-portrait-fallback"));
                portrait.Add(CreateLabel(string.Empty, "pet-card-level"));
                portrait.Add(CreateLabel(string.Empty, "pet-card-status"));
                content.Add(portrait);

                var info = new VisualElement();
                info.AddToClassList("pet-card-info");
                var nameRow = new VisualElement();
                nameRow.AddToClassList("pet-card-name-row");
                nameRow.Add(CreateLabel("Trống", "pet-card-name"));
                nameRow.Add(CreateLabel(string.Empty, "pet-card-gender"));
                info.Add(nameRow);
                VisualElement hpRow = CreateBar("HP", "pet-card-hp-fill", "pet-card-hp-value");
                Label hpValue = hpRow.Q<Label>(className: "pet-card-hp-value");
                hpValue.RemoveFromHierarchy();
                info.Add(hpRow);
                info.Add(hpValue);
                content.Add(info);
                card.Add(content);
                foreach(var decoration in card.Query<VisualElement>().ToList())if(!(decoration is Button))decoration.pickingMode=PickingMode.Ignore;

                BindDockDrag(card,slotIndex);
                partyCards.Add(card);
                partyRow.Add(card);
            }
        }

        void EnsureSkillCards() {
            if (skillsRow == null || skillCards.Count == 4) return;

            skillsRow.Clear();
            skillCards.Clear();
            for (int i = 0; i < 4; i++) {
                var card = new VisualElement { name = $"thú-kỹ năng-card-{i + 1}" };
                card.AddToClassList("pet-skill-card");
                var icon = new VisualElement { name = "skill-icon" };
                icon.AddToClassList("pet-skill-icon");
                icon.Add(CreateLabel((i + 1).ToString(), "pet-skill-icon-fallback"));
                card.Add(icon);
                var text = new VisualElement();
                text.AddToClassList("pet-skill-text");
                text.Add(CreateLabel("Chưa mở", "pet-skill-name"));
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
            Button favorite = card.Q<Button>("favorite");
            if (favorite != null) {
                favorite.style.display = slot.occupied && slot.favorite ? DisplayStyle.Flex : DisplayStyle.None;
                favorite.SetEnabled(CommandProvider != null && evolutionSelectionItemId == null);
                favorite.text = "★";
                favorite.tooltip = slot.favorite ? "Bỏ yêu thích" : "Thêm vào yêu thích";
                favorite.EnableInClassList("is-favorite", slot.favorite);
            }

            SetText(card, "pet-card-name", slot.occupied ? SafeName(slot.displayName) : "Trống");
            card.Q<Label>(className: "pet-card-name").tooltip = slot.occupied ? SafeName(slot.displayName) : string.Empty;
            SetText(card, "pet-card-level", slot.occupied && slot.level > 0 ? $"Cấp {slot.level}" : string.Empty);
            Label activeStatus = card.Q<Label>(className: "pet-card-status");
            if (activeStatus != null) {
                activeStatus.text = slot.summoned ? "ĐANG DÙNG" : slot.selected ? "ĐANG XEM" : string.Empty;
                activeStatus.style.display = slot.summoned || slot.selected ? DisplayStyle.Flex : DisplayStyle.None;
            }
            PetController slotPet = slot.occupied ? CommandProvider?.GetPetControllerAt(index) : null;
            PetCollectionMetadata metadata = slotPet != null
                ? slotPet.GetComponentInChildren<PetCollectionMetadata>(true)
                : null;
            Label primaryElement = card.Q<Label>(className: "pet-card-element-primary");
            SetElementIcon(primaryElement, metadata != null ? metadata.Element : PetElement.Unknown, slot.occupied);
            Label secondaryElement = card.Q<Label>(className: "pet-card-element-secondary");
            SetElementIcon(secondaryElement, metadata != null ? metadata.SecondaryElement : PetElement.Unknown, slot.occupied);
            if (evolutionSelectionItemId != null) {
                PetEvolutionService service = GetComponent<PetEvolutionService>();
                bool eligible = slotPet != null && service?.CreatePreview(slotPet,
                    PetEvolutionMethod.Item, evolutionSelectionItemId).CanEvolve == true;
                card.style.opacity = eligible ? 1f : 0.35f;
            }
            else card.style.opacity = 1f;
            SetGenderIcon(card.Q<Label>(className: "pet-card-gender"), metadata?.Gender, slot.occupied);
            SetText(card, "pet-card-hp-value", slot.occupied ? FormatPair(slot.health, slot.maxHealth) : string.Empty);
            SetFill(card.Q<VisualElement>("pet-card-hp-fill"), slot.HealthPercent);

            VisualElement portrait = card.Q<VisualElement>("portrait");
            Label fallback = portrait?.Q<Label>(className: "pet-card-portrait-fallback");
            SetPortrait(portrait, fallback, slot.icon, slot.occupied ? SafeName(slot.displayName) : "🐾");
        }

        void RefreshSelectedPet() {
            PetStatusHudData pet = ViewedStatus;
            bool hasPet = pet.hasPet;
            string name = hasPet ? SafeName(pet.displayName) : "Chọn một thú";
            PetController selectedController = hasPet ? ViewedPet : null;
            PetCollectionMetadata metadata = selectedController != null
                ? selectedController.GetComponentInChildren<PetCollectionMetadata>(true)
                : null;

            if (selectedName != null) selectedName.text = name;
            bool favorite = hasPet && CommandProvider != null && ViewedPet?.GetComponentInChildren<PetCollectionMetadata>(true)?.IsFavorite == true;
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
                bool hasNextForm = evolution?.IsConfigured == true
                    && evolution.Rule.Trigger != PetEvolutionTrigger.Item;
                evolutionButton.SetEnabled(hasNextForm);
                if (evolutionSubtitle != null) {
                    evolutionSubtitle.text = hasNextForm
                        ? $"Cấp {evolution.CurrentLevel}/{evolution.RequiredLevel}"
                        : evolution?.IsConfigured == true
                            ? "Dùng vật phẩm trong Túi đồ"
                            : evolution?.DisabledReason ?? "Chưa có dữ liệu tiến hóa.";
                }
            }
            if (selectedSpecies != null) selectedSpecies.text = $"Loài: {(metadata != null ? DisplayOrDash(metadata.Species) : "—")}";
            if (selectedLevel != null) selectedLevel.text = hasPet && pet.level > 0 ? $"Cấp {pet.level}" : "Cấp —";
            if (selectedGender != null) selectedGender.text = $"Giới tính: {(metadata != null ? Capstone.Game.UISystem.VietnameseUI.Gender(metadata.Gender) : "—")}";
            if (selectedRarity != null) selectedRarity.text = $"Độ hiếm: {(metadata != null ? FormatRarity(metadata.Rarity) : "—")}";
            if (selectedElement != null) selectedElement.text = $"Hệ: {(metadata != null ? FormatElements(metadata) : "—")}";
            if (selectedHealthValue != null) selectedHealthValue.text = hasPet ? FormatPair(pet.health, pet.maxHealth) : "— / —";
            if (selectedExperienceValue != null) selectedExperienceValue.text = metadata != null
                ? FormatPair(pet.experience, pet.experienceToNextLevel)
                : "— / —";
            SetFill(selectedHealthFill, hasPet ? pet.HealthPercent : 0f);
            SetFill(selectedExperienceFill, metadata != null
                ? pet.ExperiencePercent
                : 0f);
            // Keep the live preview texture when data events update HP/stats.
            // Replacing it with an icon each refresh causes visible flashes.
            // Large 2D illustration is a separate presentation asset; never enlarge the HUD avatar here.
            RefreshCompanionPortrait(selectedController);

            PetStatSnapshot scores = metadata != null ? metadata.CurrentStats : default;
            if (statHealth != null) statHealth.text = metadata != null ? scores.Hp.ToString() : "—";
            if (statAttack != null) statAttack.text = metadata != null ? scores.Attack.ToString() : "—";
            if (statDefense != null) statDefense.text = metadata != null ? scores.Defense.ToString() : "—";
            if (statSpeed != null) statSpeed.text = metadata != null ? scores.Speed.ToString() : "—";
            if (statMagicAttack != null) statMagicAttack.text = metadata != null ? scores.MagicAttack.ToString() : "—";
            if (statMagicDefense != null) statMagicDefense.text = metadata != null ? scores.MagicDefense.ToString() : "—";
            if (statCriticalRate != null) statCriticalRate.text = metadata != null ? $"{scores.CriticalRate:0.#}%" : "—";
            if (statCriticalDamage != null) statCriticalDamage.text = metadata != null ? $"{scores.CriticalDamage:0.#}%" : "—";

            SetFill(statHealthFill, metadata != null ? PetStatCalculator.DisplayRatio(scores.Hp, pet.level, PetCoreStat.Hp) : 0f);
            SetFill(statAttackFill, metadata != null ? PetStatCalculator.DisplayRatio(scores.Attack, pet.level, PetCoreStat.Attack) : 0f);
            SetFill(statDefenseFill, metadata != null ? PetStatCalculator.DisplayRatio(scores.Defense, pet.level, PetCoreStat.Defense) : 0f);
            SetFill(statSpeedFill, metadata != null ? PetStatCalculator.DisplayRatio(scores.Speed, pet.level, PetCoreStat.Speed) : 0f);
            SetFill(statMagicAttackFill, metadata != null ? PetStatCalculator.DisplayRatio(scores.MagicAttack, pet.level, PetCoreStat.MagicAttack) : 0f);
            SetFill(statMagicDefenseFill, metadata != null ? PetStatCalculator.DisplayRatio(scores.MagicDefense, pet.level, PetCoreStat.MagicDefense) : 0f);
            SetFill(statCriticalRateFill, metadata != null ? scores.CriticalRate / CriticalRateDisplayMaximum : 0f);
            SetFill(statCriticalDamageFill, metadata != null ? scores.CriticalDamage / CriticalDamageDisplayMaximum : 0f);
            if (!hasPet) {
                SetFeedback("Chọn một ô có thú để xem thông tin.");
            }
            else if (feedbackLabel != null
                && feedbackLabel.text == "Chọn một ô có thú để xem thông tin.") {
                SetFeedback(string.Empty);
            }
        }

        void RefreshSkills() {
            bool hasPet = ViewedStatus.hasPet;
            IReadOnlyList<SkillHudData> skills = hasPet ? ViewedSkills : null;
            int slotCount = !hasPet ? 0 : CommandProvider != null
                ? CommandProvider.SlotCountFor(ViewedPet)
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
                SetText(card, "pet-skill-name", available ? SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName)) : "Chưa mở");
                SetText(card, "pet-skill-meta", available ? $"Cấp {Mathf.Max(1, skill.skillLevel)}" : "-");
                VisualElement icon = card.Q<VisualElement>("skill-icon");
                Label fallback = icon?.Q<Label>(className: "pet-skill-icon-fallback");
                CompanionArt.SetSkillImage(icon, fallback, available ? skill.icon : null, !available);

                string title = available ? SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName)) : "Kỹ năng chưa mở";
                string body = available
                    ? BuildSkillTooltip(skill)
                    : "Thú này chưa có kỹ năng ở ô này.";
                card.tooltip = body;
                card.userData = new TooltipData(title, body);
            }
        }

        void SelectSlot(int index) {
            var pet=CommandProvider?.GetPetControllerAt(index);
            if(pet==null || pet==ViewedPet)return;
            CancelViewedInteraction();viewedPet=pet;viewedPetId=PetIdentity(pet);
            SetFeedback(string.Empty);Refresh();
        }

        void HandlePartyCardClick(int index, int clickCount) {
            if (index < 0 || index >= partyCards.Count) return;

            if (evolutionSelectionItemId != null) {
                PetController pet = CommandProvider?.GetPetControllerAt(index);
                PetEvolutionService service = GetComponent<PetEvolutionService>();
                PetEvolutionPreview itemPreview = pet != null ? service?.CreatePreview(pet,
                    PetEvolutionMethod.Item, evolutionSelectionItemId) : null;
                if (itemPreview?.CanEvolve != true) {
                    SetFeedback(itemPreview?.DisabledReason ?? "Ô này chưa có thú phù hợp.");
                    return;
                }
                string itemId = evolutionSelectionItemId;
                CancelEvolutionItemSelection();
                SelectSlot(index);
                MonsterInventoryController controller = GetComponent<MonsterInventoryController>();
                if (controller == null) controller = FindFirstObjectByType<MonsterInventoryController>();
                controller?.OpenBagEvolutionConfirmation(pet, itemId);
                return;
            }

            if(suppressDockClick){suppressDockClick=false;return;}
            if(pendingSwapSourceIndex>=0){TryCompletePartySwap(index);return;}
            SelectSlot(index);
        }

        void TryCompletePartySwap(int targetIndex) {
            if(swapSaving || pendingSwapSourceIndex<0)return;
            pendingSwapTargetIndex=targetIndex==pendingSwapSourceIndex?-1:targetIndex;
            swapTargetPet=pendingSwapTargetIndex>=0?CommandProvider?.GetPetControllerAt(pendingSwapTargetIndex):null;
            UpdateSwapSelectionVisual();
            SetFeedback(pendingSwapTargetIndex>=0?$"Xem trước: ô {pendingSwapSourceIndex+1} ↔ ô {pendingSwapTargetIndex+1}. Chưa đổi vị trí.":"Chọn ô đích để xem trước.");
        }

        void UpdateSwapSelectionVisual() {
            for(int i=0;i<partyCards.Count;i++){
                var card=partyCards[i];card.EnableInClassList("is-swap-source",i==pendingSwapSourceIndex);card.EnableInClassList("is-swap-target",i==pendingSwapTargetIndex);
                var marker=card.Q<Label>("r3-swap-marker");if(marker==null){marker=new Label{name="r3-swap-marker",pickingMode=PickingMode.Ignore};marker.AddToClassList("r3-swap-marker");var arrow=new VisualElement{name="r3-swap-arrow",pickingMode=PickingMode.Ignore};marker.Add(arrow);card.Add(marker);}
                marker.text=i==pendingSwapSourceIndex?"Nguồn":i==pendingSwapTargetIndex?"Đích":"";
                marker.Q("r3-swap-arrow").style.backgroundImage=new StyleBackground(Capstone.Game.UISystem.InteractionR3Assets.Load(i==pendingSwapSourceIndex?"Assets/Interaction/swap-source":"Assets/Interaction/swap-target"));
                marker.style.display=string.IsNullOrEmpty(marker.text)?DisplayStyle.None:DisplayStyle.Flex;
                marker.style.color=i==pendingSwapSourceIndex?Capstone.Game.UISystem.GreenAngularGeometry.Gold:Capstone.Game.UISystem.GreenAngularGeometry.Exp;
                if(card is Capstone.Game.UISystem.AngularButton b)b.Surface.MarkDirtyRepaint();
            }
            EnsureSwapActions();swapConfirm?.SetEnabled(pendingSwapTargetIndex>=0&&!swapSaving);
            if(swapActions!=null)swapActions.style.display=pendingSwapSourceIndex>=0?DisplayStyle.Flex:DisplayStyle.None;
            var begin=panel?.Q("pets-swap-button");if(begin!=null)begin.style.display=pendingSwapSourceIndex>=0?DisplayStyle.None:DisplayStyle.Flex;
        }

        void ClearSwapSelection() {
            ReleaseDockDrag();pendingSwapSourceIndex=pendingSwapTargetIndex=-1;swapSourcePet=swapTargetPet=null;swapSaving=false;
            UpdateSwapSelectionVisual();
        }

        void ShowMissingControllerFeedback(string message) {
            SetFeedback(message);
        }

        void SetFeedback(string message) {
            if (feedbackLabel == null) return;
            feedbackLabel.text = message ?? string.Empty;
            feedbackLabel.style.display = string.IsNullOrWhiteSpace(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        void ClosePanel() {
            CancelEvolutionItemSelection();
            ClearSwapSelection();
            HideRenamePopup();
            MonsterInventoryController inventory = GetComponent<MonsterInventoryController>();
            if (inventory != null) inventory.RequestClose();
        }

        void OpenRenamePopup() {
            PetStatusHudData status = ViewedStatus;
            if (!status.hasPet || CommandProvider == null || renameOverlay == null) {
                SetFeedback("Chọn thú đồng hành để đổi tên.");
                return;
            }

            renameTarget=ViewedPet;
            renameOriginalName = SafeName(status.displayName);
            if (renameCurrentName != null) renameCurrentName.text = renameOriginalName;
            SetPortrait(renamePortrait, renamePortraitFallback, status.icon, renameOriginalName);
            renameInput?.SetValueWithoutNotify(string.Empty);
            UpdateRenameValidation(false);
            HideTooltip();
            renameOverlay.style.display = DisplayStyle.Flex;
            renameOverlay.BringToFront();
            Capstone.Game.UISystem.PanelFocusUtility.Open(renameOverlay);
        }

        void HideRenamePopup() {
            if (renameOverlay != null) renameOverlay.style.display = DisplayStyle.None;
            if (renameError != null) renameError.text = string.Empty;
            if (renameInput != null) {
                renameInput.RemoveFromClassList("is-invalid");
                renameInput.Blur();
            }
            renameOriginalName = string.Empty;
            Capstone.Game.UISystem.PanelFocusUtility.Close(renameOverlay);
        }

        public void CloseRenamePopup() {
            HideRenamePopup();
        }

        void SaveNickname() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "rename")) return;
            if (CommandProvider == null || renameTarget==null || !CommandProvider.ContainsPet(renameTarget)) { ShowRenameValidationError("Thú không còn trong đội hình.");return; }
            if (!TryGetValidNickname(out string nickname, out string validationError)) {
                ShowRenameValidationError(validationError);
                return;
            }

            if (!CommandProvider.RenameFor(renameTarget, nickname, out string error)) {
                ShowRenameValidationError(error);
                return;
            }

            HideRenamePopup();
            SetFeedback($"Đã đổi tên thú thành {nickname}.");
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
            var helper=renameError?.parent?.Q<Label>(className:"ga-rename-helper");
            if(helper!=null)helper.style.display=showError&&!valid?DisplayStyle.None:DisplayStyle.Flex;
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
            var helper=renameError?.parent?.Q<Label>(className:"ga-rename-helper");
            if(helper!=null)helper.style.display=DisplayStyle.None;
            if (renameInput != null) renameInput.AddToClassList("is-invalid");
            if (renameError != null) renameError.text = string.IsNullOrWhiteSpace(error)
                ? "Tên không hợp lệ."
                : error;
            renameSaveButton?.SetEnabled(false);
        }

        void ToggleFavorite() {
            if (CommandProvider == null || !CommandProvider.FavoriteFor(ViewedPet, out bool favorite)) {
                SetFeedback("Chưa chọn thú để đánh dấu yêu thích.");
                return;
            }

            SetFeedback(favorite
                ? "Đã thêm thú vào danh sách yêu thích."
                : "Đã bỏ thú khỏi danh sách yêu thích.");
            Refresh();
        }

        void ToggleFavoriteAt(int slotIndex) {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "favorite:" + slotIndex)) return;
            if (CommandProvider == null || !CommandProvider.TryTogglePetFavoriteAt(slotIndex, out bool favorite)) {
                SetFeedback("Không thể lưu trạng thái yêu thích của thú.");
                return;
            }
            SetFeedback(favorite ? "Đã thêm thú vào danh sách yêu thích." : "Đã bỏ thú khỏi danh sách yêu thích.");
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
            if(tooltip==null||root==null||anchor==null||dragGhost!=null)return;
            tooltipDelay?.Pause();tooltip.style.display=DisplayStyle.None;tooltip.pickingMode=PickingMode.Ignore;
            tooltipDelay=root.schedule.Execute(()=>{
                if(anchor.panel==null || !companionVisible)return;
                if(tooltipTitle!=null)tooltipTitle.text=title;if(tooltipBody!=null)tooltipBody.text=body;
                tooltip.style.width=Mathf.Min(310,Mathf.Max(80,root.resolvedStyle.width-32));
                tooltip.style.maxHeight=Mathf.Max(72,root.resolvedStyle.height-32);
                tooltip.style.visibility=Visibility.Hidden;tooltip.style.display=DisplayStyle.Flex;tooltip.BringToFront();
                tooltip.schedule.Execute(()=>{if(tooltip.resolvedStyle.display==DisplayStyle.None)return;
                    var a=root.WorldToLocal(anchor.worldBound.position);var z=root.WorldToLocal(anchor.worldBound.max);float w=tooltip.resolvedStyle.width,h=tooltip.resolvedStyle.height;
                    float x=Mathf.Clamp((a.x+z.x-w)/2,16,Mathf.Max(16,root.resolvedStyle.width-w-16));float y=a.y-h-10;
                    bool ClearAt(float left,float top){
                        if(top<16||top+h>root.resolvedStyle.height-16)return false;
                        var area=new Rect(left,top,w,h);bool clear=true;
                        panel.Query<Button>().ForEach(button=>{
                            if(!clear)return;
                            for(VisualElement e=button;e!=null;e=e.parent)if(e.resolvedStyle.display==DisplayStyle.None||e.resolvedStyle.visibility==Visibility.Hidden)return;
                            var min=root.WorldToLocal(button.worldBound.min);var max=root.WorldToLocal(button.worldBound.max);
                            if(area.Overlaps(Rect.MinMaxRect(min.x,min.y,max.x,max.y)))clear=false;
                        });
                        return clear;
                    }
                    if(!ClearAt(x,y))y=z.y+10;
                    if(!ClearAt(x,y)){x=24;y=Mathf.Clamp(112,16,Mathf.Max(16,root.resolvedStyle.height-h-16));} // Dedicated hero helper area when both adjacent rows contain controls.
                    tooltip.style.left=x;tooltip.style.top=y;tooltip.style.visibility=Visibility.Visible;
                }).StartingIn(16);
            }).StartingIn(350);
        }

        void HideTooltip() {
            tooltipDelay?.Pause();
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
            string cooldown = skill.cooldownSeconds > 0f ? $"Hồi chiêu: {skill.cooldownSeconds:0.#} giây" : "Hồi chiêu: —";
            return $"{description}\nCấp kỹ năng: {Mathf.Max(1, skill.skillLevel)}\n{cooldown}";
        }

        static void SetText(VisualElement rootElement, string className, string value) {
            Label label = rootElement?.Q<Label>(className: className);
            if (label != null) label.text = value;
        }

        static void SetNamedText(VisualElement rootElement, string elementName, string value) {
            Label label = rootElement?.Q<Label>(elementName);
            if (label != null) { label.text = value ?? string.Empty; label.tooltip = label.text; }
        }

        static string FormatStatValues(PetStatValues values) =>
            $"HP {values.hp} · ATK {values.attack} · DEF {values.defense} · " +
            $"SP.ATK {values.magicAttack} · SP.DEF {values.magicDefense} · SPD {values.speed}";

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
            Capstone.Game.UISystem.AngularSurface.SetArt(portrait,sprite);
            portrait.EnableInClassList("has-image", sprite != null);
            if (fallback != null) {
                fallback.text = name == "🐾" ? name : FirstLetter(name);
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
            return string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();
        }

        static string CleanPetName(string preferred, string fallback) {
            string value = !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
            return string.IsNullOrWhiteSpace(value)
                ? "Pet"
                : value.Replace("(Clone)", string.Empty).Trim();
        }

        static void SetElementIcon(Label label, PetElement element, bool visible) {
            if (label == null) return;
            string[] stateClasses = {
                "is-nature", "is-fire", "is-water", "is-wind", "is-earth",
                "is-electric", "is-ice", "is-light", "is-dark", "is-dragon", "is-unknown"
            };
            for (int i = 0; i < stateClasses.Length; i++) label.RemoveFromClassList(stateClasses[i]);

            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) {
                label.text = string.Empty;
                return;
            }

            switch (element) {
                case PetElement.Nature: label.text = "◆"; label.AddToClassList("is-nature"); break;
                case PetElement.Fire: label.text = "▲"; label.AddToClassList("is-fire"); break;
                case PetElement.Water: label.text = "●"; label.AddToClassList("is-water"); break;
                case PetElement.Wind: label.text = "≈"; label.AddToClassList("is-wind"); break;
                case PetElement.Earth: label.text = "■"; label.AddToClassList("is-earth"); break;
                case PetElement.Electric: label.text = "ϟ"; label.AddToClassList("is-electric"); break;
                case PetElement.Ice: label.text = "✦"; label.AddToClassList("is-ice"); break;
                case PetElement.Light: label.text = "☀"; label.AddToClassList("is-light"); break;
                case PetElement.Dark: label.text = "☾"; label.AddToClassList("is-dark"); break;
                case PetElement.Dragon: label.text = "✥"; label.AddToClassList("is-dragon"); break;
                default: label.text = "?"; label.AddToClassList("is-unknown"); break;
            }
        }

        static void SetGenderIcon(Label label, string value, bool visible) {
            if (label == null) return;
            label.RemoveFromClassList("is-male");
            label.RemoveFromClassList("is-female");
            label.RemoveFromClassList("is-unknown");
            label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) {
                label.text = string.Empty;
                return;
            }

            if (string.IsNullOrWhiteSpace(value)) {
                label.text = "○";
                label.AddToClassList("is-unknown");
                return;
            }
            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "f" || normalized.Contains("female") || normalized.Contains("cái")) {
                label.text = "♀";
                label.AddToClassList("is-female");
                return;
            }
            if (normalized == "m" || normalized.Contains("male") || normalized.Contains("đực")) {
                label.text = "♂";
                label.AddToClassList("is-male");
                return;
            }
            label.text = "○";
            label.AddToClassList("is-unknown");
        }

        static string FormatElements(PetCollectionMetadata metadata) =>
            metadata.SecondaryElement == PetElement.Unknown
                ? FormatElement(metadata.Element)
                : $"{FormatElement(metadata.Element)} / {FormatElement(metadata.SecondaryElement)}";

        static string FormatElement(PetElement element) => Capstone.Game.UISystem.VietnameseUI.Element(element);

        static string FormatRarity(PetRarity rarity) => Capstone.Game.UISystem.VietnameseUI.Rarity(rarity);

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
            return maximum > 0f ? $"{Mathf.RoundToInt(current)} / {Mathf.RoundToInt(maximum)}" : "— / —";
        }
    }
}
