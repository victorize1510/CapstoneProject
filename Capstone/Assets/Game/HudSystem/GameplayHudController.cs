using System.Collections.Generic;
using System.Linq;
using Capstone.Game.QuestSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class GameplayHudController : MonoBehaviour {
        const string RootName = "GameplayHUDRoot";
        const string LegacyQuestTrackerObjectName = "QuestTrackerHUD";
        const float MinimapSize = 236f;
        const float LeftMargin = 28f;
        const float TopMargin = 28f;

        static readonly string[] SkillKeyLabels = { "Z", "X", "C", "V" };
        static readonly KeyCode[] SkillHotkeys = { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V };
        static Sprite solidSprite;
        static Font cachedFont;

        [Header("References")]
        [SerializeField] Canvas targetCanvas = null;
        [SerializeField] RectTransform minimapPanel = null;
        [SerializeField] MonoBehaviour petHudProvider = null;
        [SerializeField] QuestManager questManager = null;
        [SerializeField] Transform localPlayer = null;

        [Header("Behaviour")]
        [SerializeField] bool buildOnAwake = true;
        [SerializeField] bool autoFindReferences = true;
        [SerializeField] bool positionExistingMinimap = true;
        [SerializeField] bool hideStandaloneQuestTrackerHud = true;
        [SerializeField] bool enableSkillHotkeys = true;
        [SerializeField, Min(0.05f)] float refreshInterval = 0.15f;

        RectTransform root;
        RectTransform minimapPlaceholder;
        RectTransform questList;
        RectTransform petSlotsRoot;
        RectTransform skillBarRoot;
        Text petLevelText;
        Text petNameText;
        Text hpText;
        Text energyText;
        Image hpFill;
        Image energyFill;
        Image petIcon;
        QuestManager subscribedQuestManager;

        readonly List<Button> petSlotButtons = new List<Button>();
        readonly List<Text> petSlotNumbers = new List<Text>();
        readonly List<Image> petSlotIcons = new List<Image>();
        readonly List<Button> skillButtons = new List<Button>();
        readonly List<Text> skillLabels = new List<Text>();
        readonly List<Image> skillIcons = new List<Image>();

        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        float nextRefreshTime;

        void Awake() {
            ResolveReferences();
            if (buildOnAwake) RebuildHud();
        }

        void OnEnable() {
            ResolveReferences();
            HideStandaloneQuestTrackerHud();
            SubscribeProvider();
            SubscribeQuestManager();
            if (root == null && buildOnAwake) RebuildHud();
            RefreshHud(true);
        }

        void OnDisable() {
            UnsubscribeProvider();
            UnsubscribeQuestManager();
        }

        void Update() {
            HandleSkillHotkeys();

            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + refreshInterval;

            ResolveRuntimeReferences();
            RefreshHud(false);
        }

        [ContextMenu("Rebuild Gameplay HUD")]
        public void RebuildHud() {
            ResolveReferences();
            EnsureCanvas();
            EnsureProvider();

            root = EnsureRoot(targetCanvas.transform);
            ClearChildren(root);
            ClearRuntimeBindings();

            if (positionExistingMinimap) PositionMinimap();

            BuildMinimapFallback(root);
            BuildQuestTracker(root);
            BuildPetSlots(root);
            BuildPetStatus(root);
            BuildSkillBar(root);
            BuildTabHint(root);

            RefreshHud(true);
        }

        void ClearRuntimeBindings() {
            minimapPlaceholder = null;
            questList = null;
            petSlotsRoot = null;
            skillBarRoot = null;
            petLevelText = null;
            petNameText = null;
            hpText = null;
            energyText = null;
            hpFill = null;
            energyFill = null;
            petIcon = null;

            petSlotButtons.Clear();
            petSlotNumbers.Clear();
            petSlotIcons.Clear();
            skillButtons.Clear();
            skillLabels.Clear();
            skillIcons.Clear();
        }

        void HideStandaloneQuestTrackerHud() {
            if (!hideStandaloneQuestTrackerHud) return;

            GameObject legacy = GameObject.Find(LegacyQuestTrackerObjectName);
            if (legacy == null || legacy == gameObject || legacy.transform.IsChildOf(transform)) return;

            legacy.SetActive(false);
        }

        public void BindPetProvider(MonoBehaviour providerComponent) {
            UnsubscribeProvider();
            petHudProvider = providerComponent;
            EnsureProvider();
            SubscribeProvider();
            RefreshHud(true);
        }

        void ResolveReferences() {
            EnsureCanvas();
            EnsureProvider();
            ResolveRuntimeReferences();
        }

        void ResolveRuntimeReferences() {
            if (!autoFindReferences) return;

            QuestManager previousQuestManager = questManager;
            if (questManager == null) questManager = FindFirstObjectByType<QuestManager>();
            if (previousQuestManager != questManager) SubscribeQuestManager();

            if (localPlayer == null) localPlayer = FindPlayerTransform();
            if (minimapPanel == null) {
                GameObject minimap = GameObject.Find("MinimapPanel");
                if (minimap != null) minimapPanel = minimap.GetComponent<RectTransform>();
            }
        }

        void EnsureCanvas() {
            if (targetCanvas != null) return;

            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            targetCanvas = canvases.FirstOrDefault(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay);
            if (targetCanvas != null) return;

            GameObject canvasObject = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            ConfigureCanvasScaler(targetCanvas);
        }

        void EnsureProvider() {
            if (petHudProvider is PlaceholderPetHudProvider && FindFirstObjectByType<PetCommandInput>() != null) {
                petHudProvider = null;
            }

            if (Provider != null) return;

            if (petHudProvider == null) petHudProvider = GetComponent<PetCommandHudProvider>();
            if (petHudProvider == null) petHudProvider = FindFirstObjectByType<PetCommandHudProvider>();
            if (petHudProvider == null) petHudProvider = GetComponent<PlaceholderPetHudProvider>();
            if (petHudProvider == null) petHudProvider = gameObject.AddComponent<PetCommandHudProvider>();
        }

        void SubscribeProvider() {
            if (Provider == null) return;

            Provider.HudDataChanged -= HandleProviderChanged;
            Provider.HudDataChanged += HandleProviderChanged;
        }

        void UnsubscribeProvider() {
            if (Provider == null) return;

            Provider.HudDataChanged -= HandleProviderChanged;
        }

        void HandleProviderChanged() {
            RefreshHud(true);
        }

        void SubscribeQuestManager() {
            if (subscribedQuestManager == questManager) return;

            UnsubscribeQuestManager();
            subscribedQuestManager = questManager;
            if (subscribedQuestManager == null) return;

            subscribedQuestManager.QuestsChanged -= HandleQuestDataChanged;
            subscribedQuestManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
            subscribedQuestManager.QuestsChanged += HandleQuestDataChanged;
            subscribedQuestManager.TrackedQuestChanged += HandleTrackedQuestChanged;
        }

        void UnsubscribeQuestManager() {
            if (subscribedQuestManager == null) return;

            subscribedQuestManager.QuestsChanged -= HandleQuestDataChanged;
            subscribedQuestManager.TrackedQuestChanged -= HandleTrackedQuestChanged;
            subscribedQuestManager = null;
        }

        void HandleQuestDataChanged() {
            RefreshHud(true);
        }

        void HandleTrackedQuestChanged(QuestRuntimeState quest) {
            RefreshHud(true);
        }

        static void ConfigureCanvasScaler(Canvas canvas) {
            if (canvas == null) return;

            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        }

        static RectTransform EnsureRoot(Transform canvasTransform) {
            Transform existing = canvasTransform.Find(RootName);
            GameObject rootObject = existing != null
                ? existing.gameObject
                : new GameObject(RootName, typeof(RectTransform));

            rootObject.transform.SetParent(canvasTransform, false);
            RectTransform rect = rootObject.GetComponent<RectTransform>();
            Stretch(rect);
            rect.SetAsLastSibling();
            return rect;
        }

        void PositionMinimap() {
            if (minimapPanel == null) return;

            minimapPanel.SetParent(targetCanvas.transform, false);
            minimapPanel.anchorMin = new Vector2(0f, 1f);
            minimapPanel.anchorMax = new Vector2(0f, 1f);
            minimapPanel.pivot = new Vector2(0f, 1f);
            minimapPanel.anchoredPosition = new Vector2(LeftMargin, -TopMargin);
            minimapPanel.sizeDelta = new Vector2(MinimapSize, MinimapSize);
            minimapPanel.localScale = Vector3.one;
        }

        void BuildMinimapFallback(RectTransform parent) {
            minimapPlaceholder = CreatePanel(parent, "MinimapPlaceholder", new Color(0.02f, 0.10f, 0.11f, 0.82f));
            SetTopLeft(minimapPlaceholder, LeftMargin, TopMargin, MinimapSize, MinimapSize);
            minimapPlaceholder.gameObject.SetActive(minimapPanel == null);

            Image ring = CreateImage(minimapPlaceholder, "MinimapRing", new Color(0.82f, 0.66f, 0.34f, 1f));
            Stretch(ring.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f));

            Text label = CreateText(minimapPlaceholder, "MinimapLabel", "Minimap", 18, FontStyle.Bold, new Color(0.92f, 0.86f, 0.70f, 1f), TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.zero);
        }

        void BuildQuestTracker(RectTransform parent) {
            RectTransform panel = CreatePanel(parent, "QuestTrackerPanel", new Color(0.02f, 0.05f, 0.04f, 0.08f));
            SetTopLeft(panel, LeftMargin, TopMargin + MinimapSize + 18f, 340f, 214f);

            questList = CreateRect(panel, "QuestList");
            Stretch(questList, new Vector2(0f, 0f), new Vector2(0f, 0f));

            VerticalLayoutGroup layout = questList.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
        }

        void BuildPetSlots(RectTransform parent) {
            petSlotsRoot = CreatePanel(parent, "PetSlots", new Color(0.02f, 0.05f, 0.05f, 0.22f));
            SetBottomLeft(petSlotsRoot, LeftMargin, 172f, 60f, 376f);

            VerticalLayoutGroup layout = petSlotsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 8, 8);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = false;

            for (int i = 0; i < 6; i++) {
                int index = i;
                Button button = CreateButton(petSlotsRoot, "PetSlot" + (i + 1), string.Empty);
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(48f, 52f);
                button.onClick.AddListener(() => Provider?.SelectPetSlot(index));

                Text number = CreateText(buttonRect, "Number", (i + 1).ToString(), 14, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
                SetTopLeft(number.rectTransform, 4f, 2f, 20f, 20f);

                Image icon = CreateImage(buttonRect, "Icon", new Color(0.80f, 0.68f, 0.45f, 0.55f));
                SetCenter(icon.rectTransform, 0f, 4f, 28f, 28f);

                petSlotButtons.Add(button);
                petSlotNumbers.Add(number);
                petSlotIcons.Add(icon);
            }
        }

        void BuildPetStatus(RectTransform parent) {
            RectTransform panel = CreatePanel(parent, "PetStatus", new Color(0.015f, 0.08f, 0.10f, 0.74f));
            SetBottomLeft(panel, LeftMargin, 56f, 346f, 98f);

            RectTransform badge = CreatePanel(panel, "LevelBadge", new Color(0.70f, 0.52f, 0.21f, 0.95f));
            SetTopLeft(badge, 12f, 12f, 52f, 52f);
            petLevelText = CreateText(badge, "Level", "Lv. -", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(petLevelText.rectTransform, Vector2.zero, Vector2.zero);

            petIcon = CreateImage(panel, "PetIcon", new Color(0.85f, 0.74f, 0.52f, 0.5f));
            SetTopLeft(petIcon.rectTransform, 72f, 12f, 52f, 52f);

            petNameText = CreateText(panel, "PetName", "No Pet", 16, FontStyle.Bold, new Color(0.94f, 0.88f, 0.72f, 1f), TextAnchor.MiddleLeft);
            SetTopLeft(petNameText.rectTransform, 134f, 8f, 186f, 24f);

            RectTransform hpBar = CreateBar(panel, "HPBar", new Color(0.06f, 0.50f, 0.22f, 1f), out hpFill);
            SetTopLeft(hpBar, 134f, 38f, 188f, 14f);
            hpText = CreateText(hpBar, "HPText", "0 / 0", 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(hpText.rectTransform, Vector2.zero, Vector2.zero);

            RectTransform energyBar = CreateBar(panel, "EnergyBar", new Color(0.10f, 0.44f, 0.86f, 1f), out energyFill);
            SetTopLeft(energyBar, 134f, 62f, 188f, 14f);
            energyText = CreateText(energyBar, "EnergyText", "0 / 0", 11, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            Stretch(energyText.rectTransform, Vector2.zero, Vector2.zero);
        }

        void BuildSkillBar(RectTransform parent) {
            skillBarRoot = CreatePanel(parent, "SkillBar", new Color(0.02f, 0.06f, 0.07f, 0.32f));
            SetBottomCenter(skillBarRoot, 0f, 38f, 360f, 82f);

            HorizontalLayoutGroup layout = skillBarRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < 4; i++) {
                int index = i;
                Button button = CreateButton(skillBarRoot, "Skill" + (i + 1), string.Empty);
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(68f, 68f);
                button.onClick.AddListener(() => Provider?.RequestSkill(index));

                Image icon = CreateImage(rect, "Icon", SkillColor(i));
                Stretch(icon.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));

                Text label = CreateText(rect, "Label", SkillKeyLabel(i), 13, FontStyle.Bold, Color.white, TextAnchor.LowerCenter);
                Stretch(label.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 2f));

                skillButtons.Add(button);
                skillIcons.Add(icon);
                skillLabels.Add(label);
            }
        }

        void BuildTabHint(RectTransform parent) {
            RectTransform hint = CreatePanel(parent, "TabHint", new Color(0.02f, 0.05f, 0.05f, 0.56f));
            SetBottomLeft(hint, LeftMargin, 12f, 142f, 34f);

            Text key = CreateText(hint, "Key", "TAB", 14, FontStyle.Bold, new Color(0.95f, 0.89f, 0.72f, 1f), TextAnchor.MiddleCenter);
            SetTopLeft(key.rectTransform, 8f, 5f, 44f, 24f);

            Text label = CreateText(hint, "Label", "Menu", 14, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
            SetTopLeft(label.rectTransform, 58f, 5f, 70f, 24f);
        }

        void RefreshHud(bool rebuildQuestList) {
            if (root == null) return;

            if (positionExistingMinimap) PositionMinimap();
            if (minimapPlaceholder != null) minimapPlaceholder.gameObject.SetActive(minimapPanel == null);

            RefreshPetStatus();
            RefreshPetSlots();
            RefreshSkills();
            if (rebuildQuestList) RefreshQuestTracker();
        }

        void RefreshPetStatus() {
            if (Provider == null || petLevelText == null) return;

            PetStatusHudData status = Provider.GetSelectedPetStatus();
            petLevelText.text = status.hasPet && status.level > 0 ? status.level.ToString() : "-";
            petNameText.text = status.hasPet ? status.displayName : "No Pet";
            hpText.text = status.hasPet && status.maxHealth > 0f
                ? $"{Mathf.RoundToInt(status.health)} / {Mathf.RoundToInt(status.maxHealth)}"
                : "- / -";
            energyText.text = status.hasPet && status.maxEnergy > 0f
                ? $"{Mathf.RoundToInt(status.energy)} / {Mathf.RoundToInt(status.maxEnergy)}"
                : "- / -";
            SetFill(hpFill, status.HealthPercent);
            SetFill(energyFill, status.EnergyPercent);
            SetOptionalSprite(petIcon, status.icon, new Color(0.85f, 0.74f, 0.52f, 0.5f));
        }

        void RefreshPetSlots() {
            if (Provider == null) return;

            IReadOnlyList<PetSlotHudData> slots = Provider.GetPetSlots();
            for (int i = 0; i < petSlotButtons.Count; i++) {
                PetSlotHudData slot = i < slots.Count ? slots[i] : default;
                petSlotButtons[i].interactable = slot.occupied;
                SetButtonColor(petSlotButtons[i], slot.selected
                    ? new Color(0.10f, 0.47f, 0.34f, 0.95f)
                    : new Color(0.08f, 0.18f, 0.17f, slot.occupied ? 0.9f : 0.42f));
                petSlotNumbers[i].text = (i + 1).ToString();
                SetOptionalSprite(petSlotIcons[i], slot.icon, slot.occupied
                    ? new Color(0.82f, 0.68f, 0.42f, 0.7f)
                    : new Color(0.45f, 0.48f, 0.45f, 0.26f));
            }
        }

        void RefreshSkills() {
            if (Provider == null) return;

            IReadOnlyList<SkillHudData> skills = Provider.GetSkills();
            for (int i = 0; i < skillButtons.Count; i++) {
                SkillHudData skill = i < skills.Count ? skills[i] : default;
                bool active = skill.unlocked && skill.usable;
                skillButtons[i].interactable = active;
                skillLabels[i].text = SkillKeyLabel(i);
                SetButtonColor(skillButtons[i], active
                    ? new Color(0.08f, 0.16f, 0.18f, 0.95f)
                    : new Color(0.05f, 0.06f, 0.06f, 0.46f));
                SetOptionalSprite(skillIcons[i], skill.icon, active ? SkillColor(i) : new Color(0.36f, 0.36f, 0.36f, 0.55f));
            }
        }

        void RefreshQuestTracker() {
            if (questList == null) return;

            ClearChildren(questList);
            List<QuestRuntimeState> quests = GetHudQuests();
            if (quests.Count == 0) {
                Text empty = CreateText(questList, "NoQuest", "", 14, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft);
                empty.text = string.Empty;
                return;
            }

            foreach (QuestRuntimeState quest in quests.Take(1)) {
                CreateQuestEntry(questList, quest);
            }
        }

        void HandleSkillHotkeys() {
            if (!enableSkillHotkeys || Provider == null) return;
            if (Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked) return;

            for (int i = 0; i < SkillHotkeys.Length; i++) {
                if (Input.GetKeyDown(SkillHotkeys[i])) Provider.RequestSkill(i);
            }
        }

        void CreateQuestEntry(RectTransform parent, QuestRuntimeState quest) {
            RectTransform card = CreatePanel(parent, "QuestEntry", new Color(0.02f, 0.05f, 0.05f, 0.38f));
            card.sizeDelta = new Vector2(0f, 96f);

            Text title = CreateText(card, "Title", GetQuestTitle(quest), 16, FontStyle.Bold, new Color(1f, 0.83f, 0.28f, 1f), TextAnchor.UpperLeft);
            SetTopLeft(title.rectTransform, 12f, 8f, 306f, 24f);

            Text objective = CreateText(card, "Objective", GetQuestObjective(quest), 13, FontStyle.Normal, Color.white, TextAnchor.UpperLeft);
            SetTopLeft(objective.rectTransform, 24f, 34f, 288f, 34f);

            Text distance = CreateText(card, "Distance", GetQuestDistance(quest), 12, FontStyle.Normal, new Color(0.82f, 0.90f, 0.84f, 0.9f), TextAnchor.MiddleLeft);
            SetTopLeft(distance.rectTransform, 24f, 70f, 288f, 18f);
        }

        List<QuestRuntimeState> GetHudQuests() {
            if (questManager == null) return new List<QuestRuntimeState>();

            QuestRuntimeState tracked = questManager.GetTrackedQuest();
            IReadOnlyList<QuestRuntimeState> active = questManager.GetActiveQuests();
            return active
                .Where(quest => quest != null && quest.Definition != null)
                .OrderByDescending(quest => tracked != null && quest.QuestId == tracked.QuestId)
                .ThenBy(quest => quest.Definition.QuestType)
                .ThenBy(quest => quest.Definition.Title)
                .ToList();
        }

        string GetQuestDistance(QuestRuntimeState quest) {
            if (quest?.Definition == null) return string.Empty;
            if (localPlayer == null || string.IsNullOrWhiteSpace(quest.Definition.LocationName)) return quest.Definition.LocationName;

            float distance = Vector3.Distance(localPlayer.position, quest.Definition.WorldPosition);
            return quest.Definition.LocationName + "  " + Mathf.RoundToInt(distance) + " m";
        }

        static string GetQuestTitle(QuestRuntimeState quest) {
            if (quest?.Definition == null) return "Quest";
            return string.IsNullOrWhiteSpace(quest.Definition.Title) ? quest.QuestId : quest.Definition.Title;
        }

        static string GetQuestObjective(QuestRuntimeState quest) {
            QuestObjectiveProgress objective = quest?.Objectives?.FirstOrDefault(item => item != null && !item.Optional);
            if (objective == null) return quest?.Definition?.Description ?? string.Empty;

            QuestObjectiveDefinition definition = quest.Definition.Objectives.FirstOrDefault(item =>
                item != null && item.ObjectiveId == objective.ObjectiveId);
            string label = definition != null && !string.IsNullOrWhiteSpace(definition.Title)
                ? definition.Title
                : objective.ObjectiveId;

            return "- " + label + "  " + objective.CurrentAmount + " / " + objective.RequiredAmount;
        }

        static Transform FindPlayerTransform() {
            try {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) return tagged.transform;
            } catch (UnityException) {
            }

            GameObject named = GameObject.Find("Player");
            return named != null ? named.transform : null;
        }

        static RectTransform CreatePanel(Transform parent, string name, Color color) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = SolidSprite;
            image.color = color;
            image.raycastTarget = false;
            return obj.GetComponent<RectTransform>();
        }

        static Image CreateImage(Transform parent, string name, Color color) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.sprite = SolidSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text CreateText(Transform parent, string name, string text, int size, FontStyle style, Color color, TextAnchor alignment) {
            GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Text));
            obj.transform.SetParent(parent, false);
            Text label = obj.GetComponent<Text>();
            label.font = DefaultFont;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = alignment;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        static Button CreateButton(Transform parent, string name, string label) {
            RectTransform rect = CreatePanel(parent, name, new Color(0.08f, 0.16f, 0.17f, 0.9f));
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            SetButtonColor(button, new Color(0.08f, 0.16f, 0.17f, 0.9f));

            if (!string.IsNullOrWhiteSpace(label)) {
                Text text = CreateText(rect, "Text", label, 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            }

            return button;
        }

        static RectTransform CreateBar(Transform parent, string name, Color fillColor, out Image fill) {
            RectTransform bar = CreatePanel(parent, name, new Color(0.01f, 0.02f, 0.025f, 0.86f));
            fill = CreateImage(bar, "Fill", fillColor);
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return bar;
        }

        static RectTransform CreateRect(Transform parent, string name) {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            return obj.GetComponent<RectTransform>();
        }

        static void SetButtonColor(Button button, Color color) {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            if (image != null) image.color = color;

            ColorBlock colors = button.colors;
            colors.normalColor = color;
            colors.highlightedColor = color + new Color(0.10f, 0.10f, 0.08f, 0f);
            colors.pressedColor = color * 0.86f;
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.28f);
            button.colors = colors;
        }

        static void SetOptionalSprite(Image image, Sprite sprite, Color fallbackColor) {
            if (image == null) return;
            image.sprite = sprite != null ? sprite : SolidSprite;
            image.color = sprite != null ? Color.white : fallbackColor;
            image.preserveAspect = sprite != null;
        }

        static void SetFill(Image fill, float percent) {
            if (fill == null) return;
            RectTransform rect = fill.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(percent), 1f);
        }

        static Color SkillColor(int index) {
            switch (index) {
                case 0: return new Color(0.14f, 0.52f, 0.95f, 0.92f);
                case 1: return new Color(0.95f, 0.62f, 0.18f, 0.92f);
                case 2: return new Color(0.25f, 0.68f, 0.25f, 0.92f);
                default: return new Color(0.55f, 0.25f, 0.92f, 0.92f);
            }
        }

        static string SkillKeyLabel(int index) {
            return index >= 0 && index < SkillKeyLabels.Length ? SkillKeyLabels[index] : string.Empty;
        }

        static void ClearChildren(Transform parent) {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) {
                Transform child = parent.GetChild(i);
                if (Application.isPlaying) {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
                else DestroyImmediate(child.gameObject);
            }
        }

        static void Stretch(RectTransform rect) {
            Stretch(rect, Vector2.zero, Vector2.zero);
        }

        static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.localScale = Vector3.one;
        }

        static void SetTopLeft(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetBottomLeft(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetBottomCenter(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static void SetCenter(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one;
        }

        static Sprite SolidSprite {
            get {
                if (solidSprite != null) return solidSprite;

                Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                solidSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                solidSprite.hideFlags = HideFlags.HideAndDontSave;
                return solidSprite;
            }
        }

        static Font DefaultFont {
            get {
                if (cachedFont != null) return cachedFont;

                cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return cachedFont;
            }
        }
    }
}
