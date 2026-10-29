using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.UISystem;
using Capstone.Game.QuestSystem;
using Capstone.Game.QuestSystem.UI;
using UnityEngine;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.HudSystem {
    public enum SkillBarDisplayMode {
        [InspectorName("Luôn hiện 4 ô")] AlwaysVisible,
        [InspectorName("Chỉ khi thú được triệu hồi")] WhilePetSummoned
    }
    [DisallowMultipleComponent]
    public sealed class GameplayHudController : MonoBehaviour {
        const string RootName = "GameplayHUDRoot";
        [Header("References")]
        [SerializeField] Canvas targetCanvas;
        [SerializeField] RectTransform minimapPanel;
        [SerializeField] MonoBehaviour petHudProvider;
        [SerializeField] QuestManager questManager;
        [SerializeField] Transform localPlayer;
        [SerializeField] Font hudFont;
        [SerializeField] Sprite hudCircleMask;
        [Header("Behaviour")]
        [SerializeField] bool buildOnAwake = true;
        [SerializeField] bool autoFindReferences = true;
        [SerializeField] bool positionExistingMinimap = true;
        [SerializeField] bool disableMinimapHud;
        [SerializeField] bool hideStandaloneQuestTrackerHud = true;
        [SerializeField] bool hideWhileUiOpen = true;
        [SerializeField] bool enableSkillHotkeys = true;
        [Tooltip("Always Visible: luôn hiện 4 ô. While Pet Summoned: chỉ hiện sau khi triệu hồi xong; thu thú thì ẩn. Không thay đổi điều kiện sử dụng kỹ năng.")]
        [InspectorName("Hiển thị 4 ô kỹ năng")]
        [SerializeField] SkillBarDisplayMode skillBarDisplayMode = SkillBarDisplayMode.AlwaysVisible;
        [SerializeField, Min(.05f)] float refreshInterval = .15f;
        [Header("Existing skill bindings")]
        [SerializeField] KeyCode[] skillHotkeys = { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V };

        RectTransform root, minimapView, skillRegion;
        Text petName, petLevel, hpText, mpText, portraitEmpty;
        Image portrait, hpFill, mpFill;
        GameObject activeBadge;
        PetPartyCarouselView carousel;
        GameplayHudQuickItemsView quickItems;
        PetCommandInput commandInput;
        PetCommandInput subscribedCommandInput;
        MinimapController minimapController;
        GameMenuController trainerMenu;
        IPetHudProvider subscribedProvider;
        float nextRefresh, nextLookup;
        readonly List<SkillView> skills = new List<SkillView>(4);
        sealed class SkillView {
            public Button button;
            public Image icon, cooldown;
            public Text empty, countdown, key;
            public CanvasGroup content;
        }
        IPetHudProvider Provider => petHudProvider as IPetHudProvider;
        public PetPartyCarouselView Carousel => carousel;
        public bool PetSkillsAvailable => commandInput != null && commandInput.CanUsePetSkills;
        public bool HasMinimap => minimapPanel != null;
        public Transform PresentationRoot => root;
        public KeyCode GetSkillHotkey(int index) => SkillKey(index);
        public void SetMinimapVisible(bool visible) {
            if (minimapView != null) minimapView.gameObject.SetActive(visible);
            if (minimapPanel != null) minimapPanel.gameObject.SetActive(visible);
        }

        void Awake() { ResolveReferences(); if (buildOnAwake) RebuildHud(); }
        void OnEnable() { ResolveReferences(); SubscribeProvider(); HideExtraHud(); }
        void OnDisable() {
            UnsubscribeProvider();
            if (subscribedCommandInput != null) subscribedCommandInput.PetSlotSummonRequested -= CenterRequestedPet;
            subscribedCommandInput = null;
        }
        void Update() {
            SyncVisibility();
            if (root == null || !root.gameObject.activeSelf) return;
            if (enableSkillHotkeys && PetSkillsAvailable && !InventoryInputController.GameplayInputBlocked) {
                for (int i = 0; i < 4; i++) if (Input.GetKeyDown(SkillKey(i))) RequestSkill(i);
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + refreshInterval;
            if (Time.unscaledTime >= nextLookup) { nextLookup = Time.unscaledTime + 1f; ResolveReferences(); }
            RefreshHud();
        }

        [ContextMenu("Rebuild Gameplay HUD")]
        public void RebuildHud() {
            ResolveReferences();
            if (targetCanvas == null) return;
            V.Font = Capstone.Game.UISystem.MenuTypography.Font ?? hudFont;
            V.CircleMaskOverride = hudCircleMask;
            Transform existing = targetCanvas.transform.Find(RootName);
            root = existing != null ? (RectTransform)existing : V.Rect(targetCanvas.transform, RootName);
            // Preserve the live minimap before clearing generated view children.
            if (minimapPanel != null && minimapPanel.IsChildOf(root))
                minimapPanel.SetParent(targetCanvas.transform, false);
            V.Stretch(root); root.SetAsLastSibling(); V.Clear(root); skills.Clear();
            BuildMinimap(); BuildPetStatus(); BuildCarousel(); BuildSkills(); BuildQuickItems();
            SubscribeProvider(); HideExtraHud(); RefreshHud(); SyncVisibility();
        }
        public void BindPetProvider(MonoBehaviour providerComponent) {
            UnsubscribeProvider(); petHudProvider = providerComponent; ResolveReferences(); SubscribeProvider(); RefreshHud();
        }
        void ResolveReferences() {
            if (trainerMenu == null) trainerMenu = FindFirstObjectByType<GameMenuController>(FindObjectsInactive.Include);
            if (targetCanvas == null) {
                foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) { targetCanvas = canvas; break; }
                }
                if (targetCanvas == null) {
                    var obj = new GameObject("GameplayHUDCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                    targetCanvas = obj.GetComponent<Canvas>(); targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                }
                var scaler = targetCanvas.GetComponent<CanvasScaler>();
                if (scaler == null) scaler = targetCanvas.gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            }
            if (Provider == null) {
                petHudProvider = GetComponent<PetCommandHudProvider>();
                if (petHudProvider == null) petHudProvider = FindFirstObjectByType<PetCommandHudProvider>();
                if (petHudProvider == null) petHudProvider = gameObject.AddComponent<PetCommandHudProvider>();
            }
            if (!autoFindReferences) return;
            if (commandInput == null) commandInput = FindFirstObjectByType<PetCommandInput>();
            if (isActiveAndEnabled && subscribedCommandInput != commandInput) {
                if (subscribedCommandInput != null) subscribedCommandInput.PetSlotSummonRequested -= CenterRequestedPet;
                subscribedCommandInput = commandInput;
                if (subscribedCommandInput != null) subscribedCommandInput.PetSlotSummonRequested += CenterRequestedPet;
            }
            if (localPlayer == null && commandInput != null) localPlayer = commandInput.transform;
            if (minimapController == null) minimapController = FindFirstObjectByType<MinimapController>(FindObjectsInactive.Include);
            if (minimapPanel == null && minimapController != null && minimapController.Manager != null)
                minimapPanel = minimapController.Manager.GetComponent<RectTransform>();
            if (minimapPanel == null) {
                var obj = GameObject.Find("MinimapPanel"); if (obj != null) minimapPanel = obj.GetComponent<RectTransform>();
            }
        }
        void CenterRequestedPet(int slotIndex) { carousel?.Focus(slotIndex); }
        void SubscribeProvider() {
            if (subscribedProvider == Provider) return;
            UnsubscribeProvider(); subscribedProvider = Provider;
            if (subscribedProvider != null) subscribedProvider.HudDataChanged += RefreshHud;
        }
        void UnsubscribeProvider() {
            if (subscribedProvider != null) subscribedProvider.HudDataChanged -= RefreshHud;
            subscribedProvider = null;
        }
        void HideExtraHud() {
            bool hideTracker = Application.isPlaying && Capstone.Game.SettingsSystem.GameSettingsService.Instance != null
                ? !Capstone.Game.SettingsSystem.GameSettingsService.Instance.Model.Data.showQuestTracker : hideStandaloneQuestTrackerHud;
            if (hideTracker) {
                foreach (QuestTrackerHudController tracker in FindObjectsByType<QuestTrackerHudController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (tracker != null) tracker.gameObject.SetActive(false);
                var legacy = GameObject.Find("QuestTrackerHUD"); if (legacy != null) legacy.SetActive(false);
            }
            var compass = GameObject.Find("CompassHUD"); if (compass != null) compass.SetActive(false);
            var compassRoot = GameObject.Find("CompassHUDRoot"); if (compassRoot != null) compassRoot.SetActive(false);
        }
        void BuildMinimap() {
            // Keep the large minimap; allow its corner accents to sit just inside the screen.
            minimapView = V.Rect(root, "MiniMap"); V.Top(minimapView, 10f, 10f, 350f, 350f);
            if (disableMinimapHud) { minimapView.gameObject.SetActive(false); if (minimapPanel != null) minimapPanel.gameObject.SetActive(false); return; }
            if (minimapPanel != null && positionExistingMinimap) {
                minimapPanel.SetParent(minimapView, false); V.Stretch(minimapPanel);
                if (minimapController != null) minimapController.UseGameplayHudPresentation();
            }
            if (minimapPanel == null) {
                var empty = V.Frame(minimapView, "MapUnavailable", HudFrameGraphic.Shape.Circle, V.Paper, Color.clear);
                V.Stretch(empty.rectTransform, 7f);
                Text label = V.Text(empty.transform, "Empty", "—", 32, TextAnchor.MiddleCenter); V.Stretch(label.rectTransform);
            }
            var rim = V.Frame(minimapView, "ThinRim", HudFrameGraphic.Shape.Circle, Color.clear, V.Forest, 1.5f); V.Stretch(rim.rectTransform);
            var inset = V.Frame(minimapView, "InnerRim", HudFrameGraphic.Shape.Circle, Color.clear, V.Sage, 1f); V.Stretch(inset.rectTransform, 7f);
            V.Diamond(minimapView, "NorthAccent", 0, 175, V.Forest);
            V.Diamond(minimapView, "SouthAccent", 0, -175, V.Forest);
            V.Diamond(minimapView, "WestAccent", -175, 0, V.Forest);
            V.Diamond(minimapView, "EastAccent", 175, 0, V.Forest);
            if (Application.isPlaying && Capstone.Game.SettingsSystem.GameSettingsService.Instance != null)
                SetMinimapVisible(Capstone.Game.SettingsSystem.GameSettingsService.Instance.Model.Data.showMinimap);
        }
        void BuildPetStatus() {
            var panel = V.Frame(root, "ActivePetInfo", HudFrameGraphic.Shape.PetPanel, V.Paper, V.Forest, 1.5f, 24f);
            V.Bottom(panel.rectTransform, 8f, 167f, 680f, 156f);
            panel.rectTransform.localScale = Vector3.one * V.PetStatusScale;
            var inner = V.Frame(panel.transform, "InsetRim", HudFrameGraphic.Shape.PetPanel, Color.clear, V.Sage, 1f, 20f); V.Stretch(inner.rectTransform, 9f);
            var portraitFrame = V.Frame(panel.transform, "PortraitFrame", HudFrameGraphic.Shape.Chamfered,
                new Color(.87f, .92f, .87f, 1f), V.Sage, 1f, 16f);
            V.Top(portraitFrame.rectTransform, 26f, 18f, 128f, 120f);
            portrait = V.Image(portraitFrame.transform, "PetPortrait"); V.Stretch(portrait.rectTransform, 7f);
            portraitEmpty = V.Text(portraitFrame.transform, "EmptyPortrait", "—", 34, TextAnchor.MiddleCenter); V.Stretch(portraitEmpty.rectTransform);
            var badge = V.Frame(portraitFrame.transform, "ActiveBadge", HudFrameGraphic.Shape.Chamfered, V.Forest, Color.clear, 0f, 8f);
            V.Bottom(badge.rectTransform, 12f, -6f, 104f, 26f); activeBadge = badge.gameObject;
            Text active = V.Text(badge.transform, "Label", "ACTIVE", 17, TextAnchor.MiddleCenter); active.color = V.Paper; V.Stretch(active.rectTransform);
            petName = V.Text(panel.transform, "PetName", "No Pet", 30); V.Top(petName.rectTransform, 178, 15, 340, 40);
            petName.resizeTextForBestFit = true; petName.resizeTextMinSize = 22; petName.resizeTextMaxSize = 30;
            petLevel = V.Text(panel.transform, "Level", "Cấp —", 27, TextAnchor.MiddleRight); V.Top(petLevel.rectTransform, 526, 15, 112, 40);
            Text hpLabel = V.Text(panel.transform, "HPLabel", "HP", 21); V.Top(hpLabel.rectTransform, 180, 64, 42, 28);
            hpFill = V.Bar(panel.transform, "HPBar", V.Sage, 230, 69, 246);
            hpText = V.Text(panel.transform, "HP", "— / —", 23, TextAnchor.MiddleRight); V.Top(hpText.rectTransform, 487, 62, 151, 32);
            Text mpLabel = V.Text(panel.transform, "MPLabel", "MP", 21); V.Top(mpLabel.rectTransform, 180, 100, 42, 28);
            mpFill = V.Bar(panel.transform, "MPBar", new Color(.48f, .65f, .76f), 230, 105, 246);
            mpText = V.Text(panel.transform, "MP", "— / —", 23, TextAnchor.MiddleRight); V.Top(mpText.rectTransform, 487, 98, 151, 32);
            V.Diamond(panel.transform, "LeftAccent", -340, 0, V.Forest);
            V.Diamond(panel.transform, "RightAccent", 340, 0, V.Forest);
        }
        void BuildCarousel() {
            RectTransform region = V.Rect(root, "PartyCarousel"); V.Bottom(region, 8, 5, V.PetRowWidth, 152);
            carousel = region.gameObject.AddComponent<PetPartyCarouselView>(); carousel.Build(ConfirmPetSlot);
        }
        void ConfirmPetSlot(int index) {
            if (InventoryInputController.GameplayInputBlocked) return;
            if (commandInput != null) commandInput.RequestSummonSlot(index);
        }
        void BuildSkills() {
            RectTransform region = V.Rect(root, "SkillBar");
            skillRegion = region;
            V.Place(region, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0, 5), new Vector2(620, 156));
            region.localScale = Vector3.one * .5f;
            var line = V.Frame(region, "Connector", HudFrameGraphic.Shape.Chamfered, V.Forest, Color.clear, 0, 0);
            V.Bottom(line.rectTransform, 78, 16, 464, 1.4f);
            for (int i = 0; i < 4; i++) {
                int index = i;
                RectTransform slot = V.Rect(region, "SkillSlot" + (i + 1)); V.Bottom(slot, i * 158, 0, 146, 156);
                Button button = V.Button(slot, "Skill", HudFrameGraphic.Shape.Chamfered, 28);
                V.Top((RectTransform)button.transform, 0, 0, 146, 132);
                button.onClick.AddListener(() => RequestSkill(index));
                var inner = V.Frame(button.transform, "InsetRim", HudFrameGraphic.Shape.Chamfered, Color.clear, V.Sage, 1f, 22); V.Stretch(inner.rectTransform, 9);
                var content = V.Rect(button.transform, "Content"); V.Stretch(content, 20);
                var view = new SkillView { button = button, content = content.gameObject.AddComponent<CanvasGroup>() };
                view.icon = V.Image(content, "SkillIcon"); V.Stretch(view.icon.rectTransform);
                view.empty = V.Text(content, "EmptyOrMissingIcon", "—", 22, TextAnchor.MiddleCenter); V.Stretch(view.empty.rectTransform);
                view.cooldown = V.Image(button.transform, "Cooldown"); V.Stretch(view.cooldown.rectTransform, 11);
                view.cooldown.sprite = V.CircleMask; view.cooldown.color = new Color(.1f, .2f, .14f, .62f);
                view.cooldown.type = Image.Type.Filled; view.cooldown.fillMethod = Image.FillMethod.Radial360; view.cooldown.fillOrigin = 2;
                view.countdown = V.Text(button.transform, "CooldownSeconds", "", 30, TextAnchor.MiddleCenter); view.countdown.color = Color.white; V.Stretch(view.countdown.rectTransform);
                view.key = V.Keycap(slot, SkillKeyLabel(i), 49, 0, 48, 38); skills.Add(view);
            }
        }
        void RequestSkill(int index) {
            if (InventoryInputController.GameplayInputBlocked || !PetSkillsAvailable || Provider == null) return;
            IReadOnlyList<SkillHudData> data = Provider.GetSkills();
            if (index >= 0 && index < data.Count && data[index].unlocked && data[index].usable && !data[index].IsCoolingDown)
                Provider.RequestSkill(index);
        }
        void BuildQuickItems() {
            RectTransform region = V.Rect(root, "QuickItems");
            V.Place(region, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8, 8), new Vector2(270, 142));
            region.localScale = Vector3.one * .5f;
            quickItems = GetComponent<GameplayHudQuickItemsView>();
            if (quickItems == null) quickItems = gameObject.AddComponent<GameplayHudQuickItemsView>();
            quickItems.Build(region, petHudProvider as PetCommandHudProvider);
        }
        void RefreshHud() {
            if (root == null || petName == null) return;
            PetStatusHudData status = Provider != null ? Provider.GetSelectedPetStatus() : default;
            petName.text = status.hasPet ? status.displayName : "No Pet";
            petLevel.text = status.hasPet && status.level > 0 ? "Cấp " + status.level : "Cấp —";
            hpText.text = Amount(status.health, status.maxHealth); mpText.text = Amount(status.energy, status.maxEnergy);
            V.Icon(portrait, status.icon); portraitEmpty.gameObject.SetActive(status.icon == null);
            SyncActiveBadge(); V.Fill(hpFill, status.HealthPercent); V.Fill(mpFill, status.EnergyPercent);
            carousel?.Refresh(Provider?.GetPetSlots()); RefreshSkills(); quickItems?.Refresh();
        }
        void RefreshSkills() {
            IReadOnlyList<SkillHudData> data = Provider?.GetSkills();
            for (int i = 0; i < skills.Count; i++) {
                SkillView view = skills[i];
                bool has = data != null && i < data.Count && data[i].unlocked;
                SkillHudData skill = has ? data[i] : default;
                bool ready = PetSkillsAvailable && has && skill.usable && !skill.IsCoolingDown;
                view.button.interactable = ready; view.content.alpha = ready ? 1f : .45f;
                V.Icon(view.icon, skill.icon); view.empty.gameObject.SetActive(skill.icon == null);
                view.empty.text = has ? skill.displayName : "—";
                view.key.text = SkillKeyLabel(i); view.cooldown.fillAmount = skill.CooldownPercent;
                view.cooldown.gameObject.SetActive(has && skill.IsCoolingDown);
                view.countdown.text = skill.cooldownRemainingSeconds > .001f ? Mathf.CeilToInt(skill.cooldownRemainingSeconds).ToString() : "CD";
                view.countdown.gameObject.SetActive(has && skill.IsCoolingDown);
            }
        }
        void SyncVisibility() {
            if (root == null) return;
            PetController activePet = commandInput != null ? commandInput.activePet : null;
            bool skillsVisible = skillBarDisplayMode == SkillBarDisplayMode.AlwaysVisible
                || (activePet != null && activePet.IsSummoned && !activePet.IsSummonPresentationPending);
            if (skillRegion != null && skillRegion.gameObject.activeSelf != skillsVisible) {
                RefreshSkills();
                skillRegion.gameObject.SetActive(skillsVisible);
            }
            SyncActiveBadge();
            bool visible = !hideWhileUiOpen || !InventoryInputController.GameplayInputBlocked
                || (trainerMenu != null && trainerMenu.IsMenuOpen);
            if (root.gameObject.activeSelf != visible) root.gameObject.SetActive(visible);
        }
        void SyncActiveBadge() {
            if (activeBadge == null) return;
            PetController pet = commandInput != null ? commandInput.activePet : null;
            bool visible = pet != null && pet.IsSummoned && !pet.IsSummonPresentationPending;
            if (activeBadge.activeSelf != visible) activeBadge.SetActive(visible);
        }
        KeyCode SkillKey(int index) => skillHotkeys != null && index < skillHotkeys.Length ? skillHotkeys[index] : KeyCode.None;
        string SkillKeyLabel(int index) => SkillKey(index) == KeyCode.None ? "—" : SkillKey(index).ToString().ToUpperInvariant();
        static string Amount(float value, float max) => max > 0 ? $"{Mathf.RoundToInt(value)} / {Mathf.RoundToInt(max)}" : "— / —";
    }
}
