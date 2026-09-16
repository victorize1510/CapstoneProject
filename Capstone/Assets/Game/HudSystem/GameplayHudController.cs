using System.Collections.Generic;
using System.Linq;
using Capstone.Game.Inventory;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class GameplayHudController : MonoBehaviour {
        const string RootName = "GameplayHUDRoot";
        const string LegacyQuestTrackerObjectName = "QuestTrackerHUD";
        const float MinimapSize = 304f;
        const float LeftMargin = 32f;
        const float TopMargin = 28f;

        static readonly string[] SkillKeyLabels = { "Z", "X", "C", "V" };
        static readonly KeyCode[] SkillHotkeys = { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V };
        static readonly Color Paper = new Color(0.985f, 0.99f, 0.965f, 0.94f);
        static readonly Color Forest = new Color(0.12f, 0.31f, 0.16f, 1f);
        static readonly Color Green = new Color(0.28f, 0.57f, 0.29f, 1f);
        static readonly Color PaleGreen = new Color(0.86f, 0.93f, 0.83f, 0.94f);
        static readonly Color BorderGreen = new Color(0.27f, 0.55f, 0.29f, 0.95f);
        static readonly Color MutedGreen = new Color(0.35f, 0.46f, 0.36f, 1f);
        static readonly Color ActiveGold = new Color(0.88f, 0.67f, 0.10f, 1f);
        static readonly Color ManaBlue = new Color(0.33f, 0.64f, 0.91f, 1f);
        static Sprite solidSprite;
        static Sprite roundedSprite;
        static Sprite circleSprite;
        static Sprite circleRingSprite;
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
        [SerializeField] bool disableMinimapHud;
        [SerializeField] bool hideStandaloneQuestTrackerHud = true;
        [SerializeField] bool hideWhileUiOpen = true;
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
        Text expText;
        Image hpFill;
        Image expFill;
        QuestManager subscribedQuestManager;

        readonly List<Button> petSlotButtons = new List<Button>();
        readonly List<Text> petSlotNumbers = new List<Text>();
        readonly List<Image> petSlotIcons = new List<Image>();
        readonly List<Image> petSlotStateStrips = new List<Image>();
        readonly List<Text> petSlotStateLabels = new List<Text>();
        readonly List<Text> petSlotLevelLabels = new List<Text>();
        readonly List<RectTransform> petSlotStateBadges = new List<RectTransform>();
        readonly List<Outline> petSlotOutlines = new List<Outline>();
        readonly List<Button> skillButtons = new List<Button>();
        readonly List<Text> skillLabels = new List<Text>();
        readonly List<Text> skillNameLabels = new List<Text>();
        readonly List<Image> skillIcons = new List<Image>();
        readonly List<Image> skillCooldownFills = new List<Image>();
        readonly List<Image> skillCooldownSweeps = new List<Image>();
        readonly List<Text> skillCooldownTexts = new List<Text>();

        RectTransform skillTooltip;
        Text skillTooltipTitle;
        Text skillTooltipDescription;
        Text skillTooltipCooldown;
        int hoveredSkillIndex = -1;

        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        float nextRefreshTime;
        GameObject minimapCameraObject;
        float nextMinimapLookupTime;

        const float MinimapLookupRetryInterval = 1f;

        void Awake() {
            ResolveReferences();
            DisableMinimapHud();
            if (buildOnAwake) RebuildHud();
        }

        void OnEnable() {
            ResolveReferences();
            DisableMinimapHud();
            HideStandaloneQuestTrackerHud();
            SubscribeProvider();
            SubscribeQuestManager();
            if (root == null && buildOnAwake) RebuildHud();
            SyncGameplayHudVisibility();
            RefreshHud(true);
        }

        void OnDisable() {
            UnsubscribeProvider();
            UnsubscribeQuestManager();
        }

        void Update() {
            SyncGameplayHudVisibility();
            if (root != null && !root.gameObject.activeSelf) return;

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
            BuildPetStatus(root);
            BuildPetSlots(root);
            BuildSkillBar(root);
            BuildSkillTooltip(root);
            BuildQuickItems(root);

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
            expText = null;
            hpFill = null;
            expFill = null;

            petSlotButtons.Clear();
            petSlotNumbers.Clear();
            petSlotIcons.Clear();
            petSlotStateStrips.Clear();
            petSlotStateLabels.Clear();
            petSlotLevelLabels.Clear();
            petSlotStateBadges.Clear();
            petSlotOutlines.Clear();
            skillButtons.Clear();
            skillLabels.Clear();
            skillNameLabels.Clear();
            skillIcons.Clear();
            skillCooldownFills.Clear();
            skillCooldownSweeps.Clear();
            skillCooldownTexts.Clear();
            skillTooltip = null;
            skillTooltipTitle = null;
            skillTooltipDescription = null;
            skillTooltipCooldown = null;
            hoveredSkillIndex = -1;
        }

        void HideStandaloneQuestTrackerHud() {
            if (!hideStandaloneQuestTrackerHud) return;

            GameObject legacy = GameObject.Find(LegacyQuestTrackerObjectName);
            if (legacy != null && legacy != gameObject && !legacy.transform.IsChildOf(transform)) {
                legacy.SetActive(false);
            }

            foreach (QuestTrackerHudController controller in FindObjectsByType<QuestTrackerHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (controller == null || controller.gameObject == gameObject || controller.transform.IsChildOf(transform)) continue;
                controller.gameObject.SetActive(false);
            }
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
            if (!disableMinimapHud && minimapPanel == null) {
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
            if (disableMinimapHud) {
                DisableMinimapHud();
                return;
            }

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
            if (disableMinimapHud) {
                minimapPlaceholder = null;
                return;
            }

            minimapPlaceholder = CreatePanel(parent, "MinimapPlaceholder", new Color(0.92f, 0.96f, 0.90f, 0.90f));
            SetTopLeft(minimapPlaceholder, LeftMargin, TopMargin, MinimapSize, MinimapSize);
            Image placeholderImage = minimapPlaceholder.GetComponent<Image>();
            placeholderImage.sprite = CircleSprite;
            placeholderImage.type = Image.Type.Simple;
            minimapPlaceholder.gameObject.SetActive(minimapPanel == null);

            Text arrow = CreateText(minimapPlaceholder, "PlayerArrow", "▲", 24, FontStyle.Bold, BorderGreen, TextAnchor.MiddleCenter);
            SetCenter(arrow.rectTransform, 0f, 0f, 44f, 44f);

            Image ring = CreateImage(parent, "MinimapFrame", BorderGreen);
            ring.sprite = CircleRingSprite;
            ring.type = Image.Type.Simple;
            SetTopLeft(ring.rectTransform, LeftMargin - 3f, TopMargin - 3f, MinimapSize + 6f, MinimapSize + 6f);
        }

        void BuildQuestTracker(RectTransform parent) {
            RectTransform panel = CreatePanel(parent, "QuestTrackerPanel", new Color(0.02f, 0.05f, 0.04f, 0.08f));
            float top = disableMinimapHud ? 96f : TopMargin + MinimapSize + 18f;
            float height = disableMinimapHud ? 174f : 214f;
            SetTopLeft(panel, LeftMargin, top, 350f, height);

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
            petSlotsRoot = CreatePanel(parent, "PetSlots", Color.clear);
            SetBottomLeft(petSlotsRoot, LeftMargin, 30f, 520f, 104f);

            HorizontalLayoutGroup layout = petSlotsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < 6; i++) {
                int index = i;
                Button button = CreateButton(petSlotsRoot, "PetSlot" + (i + 1), string.Empty);
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.sizeDelta = new Vector2(80f, 104f);
                Outline outline = AddOutline(button.gameObject, BorderGreen, 1f);
                button.onClick.AddListener(() => Provider?.SelectPetSlot(index));

                Text number = CreateText(buttonRect, "Number", (i + 1).ToString(), 13, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                SetTopLeft(number.rectTransform, 5f, 5f, 20f, 18f);

                Text level = CreateText(buttonRect, "Level", string.Empty, 10, FontStyle.Bold, MutedGreen, TextAnchor.MiddleRight);
                SetTopLeft(level.rectTransform, 38f, 5f, 35f, 18f);

                Image icon = CreateImage(buttonRect, "Icon", PaleGreen);
                SetTopLeft(icon.rectTransform, 10f, 21f, 60f, 54f);

                RectTransform hpBar = CreateBar(buttonRect, "HPBar", Green, out Image stateStrip);
                SetTopLeft(hpBar, 9f, 77f, 62f, 6f);

                RectTransform activeBadge = CreatePanel(buttonRect, "ActiveBadge", ActiveGold);
                SetBottomLeft(activeBadge, 8f, 2f, 64f, 17f);
                Text stateLabel = CreateText(activeBadge, "State", "ACTIVE", 9, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(stateLabel.rectTransform);
                activeBadge.gameObject.SetActive(false);

                petSlotButtons.Add(button);
                petSlotNumbers.Add(number);
                petSlotIcons.Add(icon);
                petSlotStateStrips.Add(stateStrip);
                petSlotStateLabels.Add(stateLabel);
                petSlotLevelLabels.Add(level);
                petSlotStateBadges.Add(activeBadge);
                petSlotOutlines.Add(outline);
            }
        }

        void BuildPetStatus(RectTransform parent) {
            RectTransform panel = CreatePanel(parent, "PetStatus", Paper);
            SetBottomLeft(panel, LeftMargin, 150f, 520f, 112f);
            AddOutline(panel.gameObject, BorderGreen, 1f);

            petNameText = CreateText(panel, "PetName", "No Pet", 21, FontStyle.Bold, Forest, TextAnchor.MiddleLeft);
            SetTopLeft(petNameText.rectTransform, 16f, 9f, 350f, 28f);

            petLevelText = CreateText(panel, "Level", "Lv. -", 16, FontStyle.Normal, MutedGreen, TextAnchor.MiddleRight);
            SetTopLeft(petLevelText.rectTransform, 382f, 10f, 120f, 26f);

            Text hpLabel = CreateText(panel, "HPLabel", "HP", 13, FontStyle.Bold, MutedGreen, TextAnchor.MiddleLeft);
            SetTopLeft(hpLabel.rectTransform, 16f, 43f, 34f, 20f);
            RectTransform hpBar = CreateBar(panel, "HPBar", Green, out hpFill);
            SetTopLeft(hpBar, 52f, 48f, 342f, 11f);
            hpText = CreateText(panel, "HPText", "0 / 0", 13, FontStyle.Normal, MutedGreen, TextAnchor.MiddleRight);
            SetTopLeft(hpText.rectTransform, 404f, 42f, 98f, 22f);

            Text manaLabel = CreateText(panel, "MPLabel", "MP", 13, FontStyle.Bold, MutedGreen, TextAnchor.MiddleLeft);
            SetTopLeft(manaLabel.rectTransform, 16f, 76f, 34f, 20f);
            RectTransform manaBar = CreateBar(panel, "MPBar", ManaBlue, out expFill);
            SetTopLeft(manaBar, 52f, 81f, 342f, 11f);
            expText = CreateText(panel, "MPText", "0 / 0", 13, FontStyle.Normal, MutedGreen, TextAnchor.MiddleRight);
            SetTopLeft(expText.rectTransform, 404f, 75f, 98f, 22f);
        }

        void BuildSkillBar(RectTransform parent) {
            skillBarRoot = CreatePanel(parent, "SkillBar", Color.clear);
            SetBottomCenter(skillBarRoot, 0f, 30f, 368f, 104f);

            HorizontalLayoutGroup layout = skillBarRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            for (int i = 0; i < 4; i++) {
                int index = i;
                RectTransform slot = CreateRect(skillBarRoot, "SkillSlot" + (i + 1));
                slot.sizeDelta = new Vector2(80f, 104f);

                Button button = CreateButton(slot, "Skill" + (i + 1), string.Empty);
                RectTransform rect = button.GetComponent<RectTransform>();
                SetTopLeft(rect, 3f, 0f, 74f, 74f);
                AddOutline(button.gameObject, BorderGreen, 1f);
                button.onClick.AddListener(() => Provider?.RequestSkill(index));

                Image icon = CreateImage(rect, "Icon", PaleGreen);
                Stretch(icon.rectTransform, new Vector2(9f, 9f), new Vector2(-9f, -9f));

                Text skillName = CreateText(rect, "SkillName", string.Empty, 11, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                Stretch(skillName.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));

                Image cooldownFill = CreateImage(rect, "CooldownFill", new Color(0.06f, 0.10f, 0.07f, 0.62f));
                cooldownFill.sprite = RoundedSprite;
                cooldownFill.type = Image.Type.Sliced;
                Stretch(cooldownFill.rectTransform, new Vector2(9f, 9f), new Vector2(-9f, -9f));
                SetTopFill(cooldownFill, 0f);

                Image cooldownSweep = CreateImage(rect, "CooldownSweep", new Color(0.72f, 0.91f, 0.74f, 0.82f));
                RectTransform sweepRect = cooldownSweep.rectTransform;
                sweepRect.anchorMin = new Vector2(0f, 1f);
                sweepRect.anchorMax = new Vector2(1f, 1f);
                sweepRect.pivot = new Vector2(0.5f, 0.5f);
                sweepRect.offsetMin = new Vector2(10f, -12f);
                sweepRect.offsetMax = new Vector2(-10f, -8f);
                cooldownSweep.gameObject.SetActive(false);

                Text cooldownText = CreateText(rect, "CooldownText", string.Empty, 18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
                Stretch(cooldownText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f));

                RectTransform keyCap = CreatePanel(slot, "KeyCap", Paper);
                SetBottomLeft(keyCap, 22f, 0f, 36f, 24f);
                AddOutline(keyCap.gameObject, BorderGreen, 1f);
                Text label = CreateText(keyCap, "Label", SkillKeyLabel(i), 13, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                Stretch(label.rectTransform);

                AddSkillHoverEvents(button, index);

                skillButtons.Add(button);
                skillIcons.Add(icon);
                skillNameLabels.Add(skillName);
                skillLabels.Add(label);
                skillCooldownFills.Add(cooldownFill);
                skillCooldownSweeps.Add(cooldownSweep);
                skillCooldownTexts.Add(cooldownText);
            }
        }

        void BuildQuickItems(RectTransform parent) {
            RectTransform quickItems = CreatePanel(parent, "QuickItems", Color.clear);
            SetBottomRight(quickItems, 32f, 30f, 108f, 80f);

            HorizontalLayoutGroup layout = quickItems.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            string[] keys = { "R", "T" };
            Color[] accents = {
                new Color(0.78f, 0.30f, 0.29f, 0.80f),
                new Color(0.31f, 0.58f, 0.86f, 0.80f)
            };

            for (int i = 0; i < keys.Length; i++) {
                RectTransform slot = CreateRect(quickItems, "QuickItem" + keys[i]);
                slot.sizeDelta = new Vector2(46f, 80f);

                Image item = CreateImage(slot, "Icon", Paper);
                item.sprite = CircleSprite;
                item.type = Image.Type.Simple;
                SetTopLeft(item.rectTransform, 4f, 2f, 38f, 38f);
                AddOutline(item.gameObject, BorderGreen, 1f);

                Image placeholder = CreateImage(item.rectTransform, "Placeholder", accents[i]);
                placeholder.sprite = CircleSprite;
                placeholder.type = Image.Type.Simple;
                SetCenter(placeholder.rectTransform, 0f, 0f, 15f, 15f);

                RectTransform keyCap = CreatePanel(slot, "KeyCap", Paper);
                SetBottomLeft(keyCap, 7f, 0f, 32f, 22f);
                AddOutline(keyCap.gameObject, BorderGreen, 1f);
                Text key = CreateText(keyCap, "Label", keys[i], 12, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                Stretch(key.rectTransform);
            }
        }

        void BuildSkillTooltip(RectTransform parent) {
            skillTooltip = CreatePanel(parent, "SkillTooltip", new Color(0.07f, 0.16f, 0.09f, 0.94f));
            SetBottomCenter(skillTooltip, 0f, 146f, 282f, 104f);
            AddOutline(skillTooltip.gameObject, new Color(0.68f, 0.82f, 0.64f, 0.9f), 1f);
            skillTooltip.gameObject.SetActive(false);

            skillTooltipTitle = CreateText(skillTooltip, "Title", string.Empty, 15, FontStyle.Bold, Color.white, TextAnchor.UpperLeft);
            SetTopLeft(skillTooltipTitle.rectTransform, 14f, 10f, 254f, 24f);

            skillTooltipDescription = CreateText(skillTooltip, "Description", string.Empty, 12, FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f, 1f), TextAnchor.UpperLeft);
            SetTopLeft(skillTooltipDescription.rectTransform, 14f, 36f, 254f, 42f);

            skillTooltipCooldown = CreateText(skillTooltip, "Cooldown", string.Empty, 11, FontStyle.Bold, new Color(0.78f, 0.96f, 1f, 1f), TextAnchor.LowerLeft);
            SetTopLeft(skillTooltipCooldown.rectTransform, 14f, 78f, 254f, 18f);
        }

        void AddSkillHoverEvents(Button button, int index) {
            if (button == null) return;

            EventTrigger trigger = button.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = button.gameObject.AddComponent<EventTrigger>();
            if (trigger.triggers == null) trigger.triggers = new List<EventTrigger.Entry>();

            EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowSkillTooltip(index));
            trigger.triggers.Add(enter);

            EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => HideSkillTooltip(index));
            trigger.triggers.Add(exit);
        }

        void ShowSkillTooltip(int index) {
            hoveredSkillIndex = index;
            RefreshSkillTooltip();
        }

        void HideSkillTooltip(int index) {
            if (hoveredSkillIndex != index && index >= 0) return;
            hoveredSkillIndex = -1;
            if (skillTooltip != null) skillTooltip.gameObject.SetActive(false);
        }

        void RefreshSkillTooltip() {
            if (skillTooltip == null || Provider == null || hoveredSkillIndex < 0) return;

            IReadOnlyList<SkillHudData> skills = Provider.GetSkills();
            if (hoveredSkillIndex >= skills.Count || !skills[hoveredSkillIndex].unlocked) {
                skillTooltip.gameObject.SetActive(false);
                return;
            }

            SkillHudData skill = skills[hoveredSkillIndex];
            string title = string.IsNullOrWhiteSpace(skill.displayName)
                ? "Skill " + SkillKeyLabel(hoveredSkillIndex)
                : skill.displayName;
            if (skill.skillLevel > 0) {
                title += "  Lv. " + skill.skillLevel;
            }

            string description = string.IsNullOrWhiteSpace(skill.description)
                ? "No description."
                : skill.description;

            skillTooltipTitle.text = title;
            skillTooltipDescription.text = description;
            if (skill.IsCoolingDown && skill.cooldownRemainingSeconds > 0.001f) {
                skillTooltipCooldown.text = "Cooldown: " + skill.cooldownRemainingSeconds.ToString("0.0") + "s";
            }
            else if (skill.cooldownSeconds > 0.001f) {
                skillTooltipCooldown.text = "Cooldown: " + skill.cooldownSeconds.ToString("0.#") + "s";
            }
            else {
                skillTooltipCooldown.text = "Ready";
            }

            PositionSkillTooltip(hoveredSkillIndex);
            skillTooltip.gameObject.SetActive(true);
        }

        void PositionSkillTooltip(int index) {
            if (skillTooltip == null) return;

            float x = -144f + Mathf.Clamp(index, 0, 3) * 96f;
            SetBottomCenter(skillTooltip, x, 146f, 282f, 104f);
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

            DisableMinimapHud();
            SyncGameplayHudVisibility();
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
            petLevelText.text = status.hasPet && status.level > 0 ? "Lv. " + status.level : "Lv. -";
            petNameText.text = status.hasPet ? status.displayName : "No Pet";
            hpText.text = status.hasPet && status.maxHealth > 0f
                ? $"{Mathf.RoundToInt(status.health)} / {Mathf.RoundToInt(status.maxHealth)}"
                : "- / -";
            expText.text = status.hasPet && status.maxEnergy > 0f
                ? $"{Mathf.RoundToInt(status.energy)} / {Mathf.RoundToInt(status.maxEnergy)}"
                : "- / -";
            SetFill(hpFill, status.HealthPercent);
            SetFill(expFill, status.EnergyPercent);
        }

        void RefreshPetSlots() {
            for (int i = 0; i < petSlotButtons.Count; i++) {
                petSlotNumbers[i].text = (i + 1).ToString();
            }

            if (Provider == null) return;

            IReadOnlyList<PetSlotHudData> slots = Provider.GetPetSlots();
            for (int i = 0; i < petSlotButtons.Count; i++) {
                PetSlotHudData slot = i < slots.Count ? slots[i] : default;
                petSlotButtons[i].interactable = slot.occupied;

                Color buttonColor;
                Color numberColor;
                bool active = slot.occupied && slot.selected;
                bool defeated = slot.occupied && slot.maxHealth > 0f && slot.health <= 0f;

                if (!slot.occupied) {
                    buttonColor = new Color(Paper.r, Paper.g, Paper.b, 0.52f);
                    numberColor = new Color(Forest.r, Forest.g, Forest.b, 0.48f);
                }
                else {
                    buttonColor = defeated
                        ? new Color(0.86f, 0.87f, 0.83f, 0.76f)
                        : Paper;
                    numberColor = Forest;
                }

                SetButtonColor(petSlotButtons[i], buttonColor);
                petSlotNumbers[i].color = numberColor;
                SetOptionalSprite(petSlotIcons[i], slot.icon, slot.occupied
                    ? PaleGreen
                    : new Color(PaleGreen.r, PaleGreen.g, PaleGreen.b, 0.35f));

                if (i < petSlotStateStrips.Count) {
                    petSlotStateStrips[i].color = defeated
                        ? new Color(0.55f, 0.58f, 0.55f, 0.75f)
                        : Green;
                    SetFill(petSlotStateStrips[i], slot.occupied ? slot.HealthPercent : 0f);
                }
                if (i < petSlotLevelLabels.Count) {
                    petSlotLevelLabels[i].text = slot.occupied && slot.level > 0 ? "Lv." + slot.level : string.Empty;
                }
                if (i < petSlotStateBadges.Count) petSlotStateBadges[i].gameObject.SetActive(active);
                if (i < petSlotOutlines.Count) {
                    SetOutline(petSlotOutlines[i], active ? ActiveGold : BorderGreen, active ? 2f : 1f);
                }
                if (i < petSlotStateLabels.Count) {
                    petSlotStateLabels[i].text = active ? "ACTIVE" : string.Empty;
                }
            }
        }

        void RefreshSkills() {
            for (int i = 0; i < skillButtons.Count; i++) {
                skillLabels[i].text = SkillKeyLabel(i);
            }

            if (Provider == null) {
                HideSkillTooltip(hoveredSkillIndex);
                return;
            }

            IReadOnlyList<SkillHudData> skills = Provider.GetSkills();
            int slotCount = Provider is PetCommandHudProvider commandProvider
                ? commandProvider.GetEquippedSkillSlotCount()
                : skills.Count > 0 ? Mathf.Clamp(skills.Count, 2, 4) : 0;
            for (int i = 0; i < skillButtons.Count; i++) {
                bool visible = i < slotCount;
                skillButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;

                bool hasSkill = i < skills.Count && skills[i].unlocked;
                SkillHudData skill = hasSkill ? skills[i] : default;
                bool coolingDown = hasSkill && skill.IsCoolingDown;
                bool active = hasSkill && skill.usable && !coolingDown;
                skillButtons[i].interactable = active;
                SetButtonColor(skillButtons[i], hasSkill
                    ? active
                        ? Paper
                        : new Color(0.86f, 0.88f, 0.84f, 0.88f)
                    : new Color(0.90f, 0.92f, 0.88f, 0.52f));
                SetOptionalSprite(skillIcons[i], skill.icon, hasSkill
                    ? active ? SkillColor(i) : new Color(0.42f, 0.42f, 0.42f, 0.72f)
                    : new Color(PaleGreen.r, PaleGreen.g, PaleGreen.b, 0.38f));

                if (i < skillNameLabels.Count) {
                    bool needsFallbackLabel = hasSkill && skill.icon == null;
                    skillNameLabels[i].text = needsFallbackLabel ? ShortSkillName(skill.displayName, i) : string.Empty;
                    skillNameLabels[i].color = active
                        ? Forest
                        : new Color(MutedGreen.r, MutedGreen.g, MutedGreen.b, 0.62f);
                }

                if (i < skillLabels.Count) {
                    skillLabels[i].color = hasSkill ? Forest : new Color(Forest.r, Forest.g, Forest.b, 0.45f);
                }

                float cooldownPercent = coolingDown ? skill.CooldownPercent : 0f;
                if (i < skillCooldownFills.Count) SetTopFill(skillCooldownFills[i], cooldownPercent);
                if (i < skillCooldownSweeps.Count) SetCooldownSweep(skillCooldownSweeps[i], cooldownPercent);
                if (i < skillCooldownTexts.Count) {
                    string cooldownText = string.Empty;
                    if (coolingDown && skill.cooldownRemainingSeconds > 0.001f) {
                        cooldownText = Mathf.CeilToInt(skill.cooldownRemainingSeconds).ToString();
                    }
                    else if (coolingDown) {
                        cooldownText = "CD";
                    }

                    skillCooldownTexts[i].text = cooldownText;
                    skillCooldownTexts[i].gameObject.SetActive(coolingDown);
                }
            }

            RefreshSkillTooltip();
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

        void SyncGameplayHudVisibility() {
            if (root == null) return;

            bool shouldShow = !hideWhileUiOpen || !InventoryInputController.GameplayInputBlocked;
            if (root.gameObject.activeSelf != shouldShow) root.gameObject.SetActive(shouldShow);
            if (!disableMinimapHud && minimapPanel != null && minimapPanel.gameObject.activeSelf != shouldShow) {
                minimapPanel.gameObject.SetActive(shouldShow);
            }
        }

        void DisableMinimapHud() {
            if (!disableMinimapHud) return;

            bool needsLookup = minimapPanel == null || minimapCameraObject == null;
            if (needsLookup && Time.unscaledTime >= nextMinimapLookupTime) {
                nextMinimapLookupTime = Time.unscaledTime + MinimapLookupRetryInterval;

                if (minimapPanel == null) {
                    GameObject minimap = GameObject.Find("MinimapPanel");
                    if (minimap != null) minimapPanel = minimap.GetComponent<RectTransform>();
                }

                if (minimapCameraObject == null) {
                    minimapCameraObject = GameObject.Find("Minimap Camera");
                }
            }

            if (minimapPanel != null && minimapPanel.gameObject.activeSelf) {
                minimapPanel.gameObject.SetActive(false);
            }

            if (minimapCameraObject != null && minimapCameraObject.activeSelf) {
                minimapCameraObject.SetActive(false);
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
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
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
            RectTransform rect = CreatePanel(parent, name, Paper);
            Image image = rect.GetComponent<Image>();
            image.raycastTarget = true;

            Button button = rect.gameObject.AddComponent<Button>();
            SetButtonColor(button, Paper);

            if (!string.IsNullOrWhiteSpace(label)) {
                Text text = CreateText(rect, "Text", label, 14, FontStyle.Bold, Forest, TextAnchor.MiddleCenter);
                Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            }

            return button;
        }

        static RectTransform CreateBar(Transform parent, string name, Color fillColor, out Image fill) {
            RectTransform bar = CreatePanel(parent, name, new Color(0.80f, 0.86f, 0.79f, 0.72f));
            fill = CreateImage(bar, "Fill", fillColor);
            fill.sprite = RoundedSprite;
            fill.type = Image.Type.Sliced;
            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.pivot = new Vector2(0f, 0.5f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return bar;
        }

        static Outline AddOutline(GameObject target, Color color, float width) {
            if (target == null) return null;
            Outline outline = target.GetComponent<Outline>();
            if (outline == null) outline = target.AddComponent<Outline>();
            SetOutline(outline, color, width);
            return outline;
        }

        static void SetOutline(Outline outline, Color color, float width) {
            if (outline == null) return;
            outline.effectColor = color;
            outline.effectDistance = new Vector2(width, -width);
            outline.useGraphicAlpha = true;
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

        static void SetTopFill(Image fill, float percent) {
            if (fill == null) return;

            percent = Mathf.Clamp01(percent);
            fill.gameObject.SetActive(percent > 0.001f);

            RectTransform rect = fill.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f - percent);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
        }

        static void SetCooldownSweep(Image sweep, float percent) {
            if (sweep == null) return;

            percent = Mathf.Clamp01(percent);
            bool show = percent > 0.001f && percent < 0.999f;
            sweep.gameObject.SetActive(show);
            if (!show) return;

            RectTransform rect = sweep.rectTransform;
            float y = Mathf.Lerp(8f, 60f, percent);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(10f, y);
            rect.offsetMax = new Vector2(-10f, y + 4f);
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

        static string ShortSkillName(string skillName, int index) {
            if (string.IsNullOrWhiteSpace(skillName)) return "S" + (index + 1);

            string trimmed = skillName.Trim();
            if (trimmed.Length <= 5) return trimmed;

            string[] words = trimmed.Split(' ');
            if (words.Length > 1) {
                string result = string.Empty;
                for (int i = 0; i < words.Length && result.Length < 4; i++) {
                    if (!string.IsNullOrWhiteSpace(words[i])) result += char.ToUpperInvariant(words[i][0]);
                }

                if (!string.IsNullOrWhiteSpace(result)) return result;
            }

            return trimmed.Substring(0, Mathf.Min(5, trimmed.Length));
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

        static void SetBottomRight(RectTransform rect, float right, float bottom, float width, float height) {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-right, bottom);
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

        static Sprite RoundedSprite {
            get {
                if (roundedSprite != null) return roundedSprite;
                roundedSprite = CreateRoundedSprite();
                return roundedSprite;
            }
        }

        static Sprite CircleSprite {
            get {
                if (circleSprite != null) return circleSprite;
                circleSprite = CreateCircleSprite(false);
                return circleSprite;
            }
        }

        static Sprite CircleRingSprite {
            get {
                if (circleRingSprite != null) return circleRingSprite;
                circleRingSprite = CreateCircleSprite(true);
                return circleRingSprite;
            }
        }

        static Sprite CreateRoundedSprite() {
            const int size = 64;
            const float radius = 12f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = "GameplayHudRounded",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float center = (size - 1f) * 0.5f;
            float straight = center - radius;
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float dx = Mathf.Max(Mathf.Abs(x - center) - straight, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y - center) - straight, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                size,
                0,
                SpriteMeshType.FullRect,
                new Vector4(14f, 14f, 14f, 14f));
            sprite.name = "GameplayHudRounded";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Sprite CreateCircleSprite(bool ringOnly) {
            const int size = 128;
            float center = (size - 1f) * 0.5f;
            float outerRadius = center - 1f;
            float innerRadius = outerRadius - 1.4f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) {
                name = ringOnly ? "GameplayHudCircleRing" : "GameplayHudCircle",
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float outerAlpha = Mathf.Clamp01(outerRadius + 0.5f - distance);
                    float alpha = outerAlpha;
                    if (ringOnly) {
                        float innerAlpha = Mathf.Clamp01(innerRadius + 0.5f - distance);
                        alpha = Mathf.Clamp01(outerAlpha - innerAlpha);
                    }
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
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
