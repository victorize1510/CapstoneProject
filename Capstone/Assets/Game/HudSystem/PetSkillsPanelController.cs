using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class PetSkillsPanelController : MonoBehaviour {
        [SerializeField] UIDocument document = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] bool autoFindProvider = true;

        readonly List<Button> equippedSlots = new List<Button>(4);
        readonly List<Button> learnedCards = new List<Button>();
        readonly List<SkillHudData> equippedSnapshot = new List<SkillHudData>(4);
        readonly List<SkillHudData> learnedSnapshot = new List<SkillHudData>();
        readonly Dictionary<PetElement, Button> filterButtons = new Dictionary<PetElement, Button>();
        readonly HashSet<PetElement> selectedElements = new HashSet<PetElement>();
        readonly Dictionary<Button, Action> buttonCallbacks = new Dictionary<Button, Action>();

        VisualElement overlay;
        VisualElement dialog;
        VisualElement petPortrait;
        Label petPortraitFallback;
        Label petName;
        Label petLevel;
        Label petElement;
        Label helperLabel;
        Button allFilterButton;
        VisualElement learnedGrid;
        Label emptyLabel;
        VisualElement tooltip;
        Label tooltipName;
        Label tooltipElement;
        Label tooltipLevel;
        Label tooltipCooldown;
        Label tooltipDescription;

        IPetHudProvider subscribedProvider;
        PetElement selectedPetElement;
        int activeEquippedSlotCount;
        int selectedLearnedIndex = -1;
        bool controlsRegistered;

        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        PetCommandHudProvider LoadoutProvider => petHudProvider as PetCommandHudProvider;
        public bool IsOpen => overlay != null && overlay.style.display.value != DisplayStyle.None;

        void OnEnable() {
            ResolveReferences();
            CacheElements();
            RegisterControls();
            SubscribeProvider();
            Close();
        }

        void OnDisable() {
            UnsubscribeProvider();
            UnregisterControls();
        }

        public void Bind(UIDocument targetDocument) {
            document = targetDocument != null ? targetDocument : document;
            ResolveProvider();
            CacheElements();
            RegisterControls();
            SubscribeProvider();
            Close();
        }

        public void Open() {
            if (overlay == null) CacheElements();
            if (overlay == null) return;

            ResolveProvider();
            SubscribeProvider();
            selectedLearnedIndex = -1;
            selectedElements.Clear();
            overlay.style.display = DisplayStyle.Flex;
            Refresh();
            dialog?.Focus();
        }

        public void Close() {
            HideTooltip();
            if (overlay != null) overlay.style.display = DisplayStyle.None;
        }

        public void Refresh() {
            if (overlay == null) CacheElements();
            ResolveProvider();
            SubscribeProvider();

            PetStatusHudData status = Provider != null ? Provider.GetSelectedPetStatus() : default;
            selectedPetElement = LoadoutProvider != null ? LoadoutProvider.GetSelectedPetElement() : PetElement.Unknown;
            activeEquippedSlotCount = LoadoutProvider != null
                ? LoadoutProvider.GetEquippedSkillSlotCount()
                : status.hasPet ? Mathf.Clamp(Provider?.GetSkills()?.Count ?? 2, 2, 4) : 0;
            SetPetSummary(status);

            equippedSnapshot.Clear();
            IReadOnlyList<SkillHudData> equipped = Provider?.GetSkills();
            if (equipped != null) {
                for (int i = 0; i < equipped.Count && i < 4; i++) equippedSnapshot.Add(equipped[i]);
            }

            learnedSnapshot.Clear();
            IReadOnlyList<SkillHudData> learned = LoadoutProvider != null ? LoadoutProvider.GetLearnedSkills() : equipped;
            if (learned != null) {
                for (int i = 0; i < learned.Count; i++) learnedSnapshot.Add(learned[i]);
            }

            RefreshEquippedSlots();
            RefreshFilterState();
            RebuildLearnedGrid();
        }

        void ResolveReferences() {
            document = document != null ? document : GetComponent<UIDocument>();
            ResolveProvider();
        }

        void ResolveProvider() {
            if (Provider != null || !autoFindProvider) return;
            PetCommandHudProvider provider = FindFirstObjectByType<PetCommandHudProvider>();
            if (provider != null) petHudProvider = provider;
        }

        void CacheElements() {
            if (document == null || document.rootVisualElement == null) return;
            VisualElement root = document.rootVisualElement;
            overlay = root.Q<VisualElement>("skills-modal-overlay");
            dialog = root.Q<VisualElement>("skills-modal-dialog");
            petPortrait = root.Q<VisualElement>("skills-pet-portrait");
            petPortraitFallback = root.Q<Label>("skills-pet-portrait-fallback");
            petName = root.Q<Label>("skills-pet-name");
            petLevel = root.Q<Label>("skills-pet-level");
            petElement = root.Q<Label>("skills-pet-element");
            helperLabel = root.Q<Label>("skills-helper-label");
            learnedGrid = root.Q<VisualElement>("skills-learned-grid");
            emptyLabel = root.Q<Label>("skills-empty-label");
            tooltip = root.Q<VisualElement>("skills-hover-tooltip");
            tooltipName = root.Q<Label>("skills-tooltip-name");
            tooltipElement = root.Q<Label>("skills-tooltip-element");
            tooltipLevel = root.Q<Label>("skills-tooltip-level");
            tooltipCooldown = root.Q<Label>("skills-tooltip-cooldown");
            tooltipDescription = root.Q<Label>("skills-tooltip-description");

            equippedSlots.Clear();
            for (int i = 0; i < 4; i++) {
                Button slot = root.Q<Button>($"skills-equipped-slot-{i + 1}");
                if (slot != null) {
                    slot.userData = i;
                    equippedSlots.Add(slot);
                }
            }

            allFilterButton = root.Q<Button>("skills-filter-all");
            filterButtons.Clear();
            AddFilterReference(root, "skills-filter-nature", PetElement.Nature);
            AddFilterReference(root, "skills-filter-fire", PetElement.Fire);
            AddFilterReference(root, "skills-filter-water", PetElement.Water);
            AddFilterReference(root, "skills-filter-wind", PetElement.Wind);
            AddFilterReference(root, "skills-filter-earth", PetElement.Earth);
            AddFilterReference(root, "skills-filter-electric", PetElement.Electric);
            AddFilterReference(root, "skills-filter-ice", PetElement.Ice);
            AddFilterReference(root, "skills-filter-light", PetElement.Light);
            AddFilterReference(root, "skills-filter-dark", PetElement.Dark);
        }

        void AddFilterReference(VisualElement root, string name, PetElement element) {
            Button button = root.Q<Button>(name);
            if (button != null) filterButtons[element] = button;
        }

        void RegisterControls() {
            if (controlsRegistered || overlay == null) return;

            RegisterButton(document.rootVisualElement.Q<Button>("skills-close-button"), Close);
            RegisterButton(allFilterButton, SelectAllElements);
            foreach (KeyValuePair<PetElement, Button> pair in filterButtons) {
                PetElement element = pair.Key;
                RegisterButton(pair.Value, () => ToggleElement(element));
            }

            for (int i = 0; i < equippedSlots.Count; i++) {
                int slotIndex = i;
                Button slot = equippedSlots[i];
                RegisterButton(slot, () => EquipSelectedInto(slotIndex));
                slot.RegisterCallback<PointerEnterEvent>(_ => ShowEquippedTooltip(slotIndex));
                slot.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            }

            controlsRegistered = true;
        }

        void UnregisterControls() {
            foreach (KeyValuePair<Button, Action> pair in buttonCallbacks) pair.Key.clicked -= pair.Value;
            buttonCallbacks.Clear();
            controlsRegistered = false;
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
            if (IsOpen) Refresh();
        }

        void SetPetSummary(PetStatusHudData status) {
            string displayName = status.hasPet && !string.IsNullOrWhiteSpace(status.displayName)
                ? status.displayName.Trim()
                : "Chọn một pet";
            if (petName != null) petName.text = displayName;
            if (petLevel != null) petLevel.text = status.hasPet && status.level > 0 ? $"Lv. {status.level}" : "Lv. -";
            if (petElement != null) petElement.text = FormatElement(selectedPetElement);
            SetPortrait(petPortrait, petPortraitFallback, status.icon, displayName);
        }

        void RefreshEquippedSlots() {
            for (int i = 0; i < equippedSlots.Count; i++) {
                Button slot = equippedSlots[i];
                bool visible = i < activeEquippedSlotCount;
                slot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                if (!visible) continue;
                bool available = i < equippedSnapshot.Count && equippedSnapshot[i].unlocked;
                SkillHudData skill = available ? equippedSnapshot[i] : default;
                slot.EnableInClassList("is-empty", !available);
                slot.userData = i;

                SetText(slot, "equipped-name", available ? SafeName(skill.displayName) : "EMPTY");
                SetText(slot, "equipped-meta", available ? FormatCooldown(skill) : "Chọn skill rồi chọn ô");
                VisualElement icon = slot.Q<VisualElement>("equipped-icon");
                Label fallback = icon?.Q<Label>(className: "skills-icon-fallback");
                SetPortrait(icon, fallback, available ? skill.icon : null, available ? skill.displayName : "+");
            }
        }

        void RebuildLearnedGrid() {
            if (learnedGrid == null) return;
            learnedGrid.Clear();
            learnedCards.Clear();

            for (int i = 0; i < learnedSnapshot.Count; i++) {
                SkillHudData skill = learnedSnapshot[i];
                if (!skill.unlocked) continue;
                if (!MatchesFilter(EffectiveElement(skill))) continue;

                int learnedIndex = i;
                Button card = CreateLearnedCard(skill, learnedIndex);
                learnedCards.Add(card);
                learnedGrid.Add(card);
            }

            bool hasCards = learnedCards.Count > 0;
            if (emptyLabel != null) emptyLabel.style.display = hasCards ? DisplayStyle.None : DisplayStyle.Flex;
        }

        Button CreateLearnedCard(SkillHudData skill, int learnedIndex) {
            var card = new Button { text = string.Empty, userData = learnedIndex };
            card.AddToClassList("skills-learned-card");
            card.EnableInClassList("is-selected", learnedIndex == selectedLearnedIndex);

            var icon = new VisualElement { name = "learned-icon" };
            icon.AddToClassList("skills-learned-icon");
            var fallback = new Label(FirstLetter(skill.displayName));
            fallback.AddToClassList("skills-icon-fallback");
            icon.Add(fallback);
            SetPortrait(icon, fallback, skill.icon, skill.displayName);
            card.Add(icon);

            var copy = new VisualElement();
            copy.AddToClassList("skills-learned-copy");
            copy.Add(CreateLabel(SafeName(skill.displayName), "skills-learned-name"));
            copy.Add(CreateLabel(FormatElement(EffectiveElement(skill)), "skills-learned-element"));
            copy.Add(CreateLabel(FormatCooldown(skill), "skills-learned-meta"));
            card.Add(copy);

            card.clicked += () => SelectLearnedSkill(learnedIndex);
            card.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(card, skill));
            card.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            return card;
        }

        void SelectLearnedSkill(int learnedIndex) {
            if (learnedIndex < 0 || learnedIndex >= learnedSnapshot.Count) return;
            selectedLearnedIndex = selectedLearnedIndex == learnedIndex ? -1 : learnedIndex;
            foreach (Button card in learnedCards) {
                card.EnableInClassList("is-selected", card.userData is int index && index == selectedLearnedIndex);
            }

            if (helperLabel != null) {
                helperLabel.text = selectedLearnedIndex >= 0
                    ? $"Đã chọn {SafeName(learnedSnapshot[selectedLearnedIndex].displayName)}. Chọn một trong {activeEquippedSlotCount} slot để thay."
                    : "Chọn skill đã học, sau đó chọn slot cần thay.";
            }
        }

        void EquipSelectedInto(int equippedSlotIndex) {
            if (selectedLearnedIndex < 0) {
                if (helperLabel != null) helperLabel.text = "Hãy chọn một Learned Skill trước.";
                return;
            }

            TryEquip(selectedLearnedIndex, equippedSlotIndex);
        }

        void TryEquip(int learnedSkillIndex, int equippedSlotIndex) {
            if (LoadoutProvider == null) {
                if (helperLabel != null) helperLabel.text = "Provider hiện tại chỉ cho xem skill, chưa hỗ trợ thay loadout.";
                return;
            }

            string skillName = learnedSkillIndex >= 0 && learnedSkillIndex < learnedSnapshot.Count
                ? SafeName(learnedSnapshot[learnedSkillIndex].displayName)
                : "Skill";
            if (!LoadoutProvider.TryEquipLearnedSkill(learnedSkillIndex, equippedSlotIndex)) {
                if (helperLabel != null) helperLabel.text = "Không thể thay skill vào slot này.";
                return;
            }

            selectedLearnedIndex = -1;
            if (helperLabel != null) helperLabel.text = $"Đã trang bị {skillName} vào slot {equippedSlotIndex + 1}.";
            Refresh();
        }

        void SelectAllElements() {
            selectedElements.Clear();
            RefreshFilterState();
            RebuildLearnedGrid();
        }

        void ToggleElement(PetElement element) {
            if (!selectedElements.Add(element)) selectedElements.Remove(element);
            RefreshFilterState();
            RebuildLearnedGrid();
        }

        void RefreshFilterState() {
            bool allSelected = selectedElements.Count == 0;
            allFilterButton?.EnableInClassList("is-selected", allSelected);
            foreach (KeyValuePair<PetElement, Button> pair in filterButtons) {
                pair.Value.EnableInClassList("is-selected", selectedElements.Contains(pair.Key));
            }
        }

        bool MatchesFilter(PetElement element) {
            return selectedElements.Count == 0 || selectedElements.Contains(element);
        }

        PetElement EffectiveElement(SkillHudData skill) {
            return skill.element != PetElement.Unknown ? skill.element : selectedPetElement;
        }

        void ShowEquippedTooltip(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= equippedSnapshot.Count || slotIndex >= equippedSlots.Count) return;
            SkillHudData skill = equippedSnapshot[slotIndex];
            if (skill.unlocked) ShowTooltip(equippedSlots[slotIndex], skill);
        }

        void ShowTooltip(VisualElement anchor, SkillHudData skill) {
            if (tooltip == null || overlay == null || anchor == null) return;
            if (tooltipName != null) tooltipName.text = SafeName(skill.displayName);
            if (tooltipElement != null) tooltipElement.text = FormatElement(EffectiveElement(skill));
            if (tooltipLevel != null) tooltipLevel.text = Mathf.Max(1, skill.skillLevel).ToString();
            if (tooltipCooldown != null) tooltipCooldown.text = skill.cooldownSeconds > 0f ? $"{skill.cooldownSeconds:0.#} giây" : "-";
            if (tooltipDescription != null) tooltipDescription.text = string.IsNullOrWhiteSpace(skill.description) ? "Chưa có mô tả." : skill.description.Trim();

            Rect anchorBounds = anchor.worldBound;
            Rect overlayBounds = overlay.worldBound;
            const float tooltipWidth = 310f;
            const float tooltipHeight = 240f;
            float x = anchorBounds.xMax - overlayBounds.xMin + 12f;
            if (x + tooltipWidth > overlayBounds.width - 12f) {
                x = anchorBounds.xMin - overlayBounds.xMin - tooltipWidth - 12f;
            }
            float y = Mathf.Clamp(anchorBounds.yMin - overlayBounds.yMin, 12f, Mathf.Max(12f, overlayBounds.height - tooltipHeight - 12f));
            tooltip.style.left = Mathf.Max(12f, x);
            tooltip.style.top = y;
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

        static void SetText(VisualElement root, string name, string value) {
            Label label = root?.Q<Label>(name);
            if (label != null) label.text = value;
        }

        static void SetPortrait(VisualElement target, Label fallback, Sprite sprite, string name) {
            if (target == null) return;
            if (sprite != null) target.style.backgroundImage = new StyleBackground(sprite);
            else target.style.backgroundImage = StyleKeyword.None;
            if (fallback != null) {
                fallback.text = sprite == null ? FirstLetter(name) : string.Empty;
                fallback.style.display = sprite == null ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        static string SafeName(string value) {
            return string.IsNullOrWhiteSpace(value) ? "Skill" : value.Trim();
        }

        static string FirstLetter(string value) {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        static string FormatCooldown(SkillHudData skill) {
            return skill.cooldownSeconds > 0f ? $"CD {skill.cooldownSeconds:0.#}s" : "CD -";
        }

        static string FormatElement(PetElement element) {
            switch (element) {
                case PetElement.Nature: return "Hệ Thảo";
                case PetElement.Fire: return "Hệ Lửa";
                case PetElement.Water: return "Hệ Nước";
                case PetElement.Wind: return "Hệ Gió";
                case PetElement.Earth: return "Hệ Đất";
                case PetElement.Electric: return "Hệ Điện";
                case PetElement.Ice: return "Hệ Băng";
                case PetElement.Light: return "Hệ Ánh sáng";
                case PetElement.Dark: return "Hệ Bóng tối";
                default: return "Hệ -";
            }
        }
    }
}
