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
        [SerializeField] SkillBarDisplayMode skillBarDisplayMode = SkillBarDisplayMode.WhilePetSummoned;
        [SerializeField] PartyPickerMode partyPickerMode = PartyPickerMode.Toggle;
        [SerializeField] KeyCode partyKey = KeyCode.P;
        [SerializeField, Min(.1f)] float partyHoldThreshold = .28f;
        [SerializeField, Min(.05f)] float refreshInterval = .15f;
        [Header("Existing skill bindings")]
        [SerializeField] KeyCode[] skillHotkeys = { KeyCode.Z, KeyCode.X, KeyCode.C, KeyCode.V };

        RectTransform root, minimapView, skillRegion;
        Text petName, petLevel, hpText, mpText, portraitEmpty;
        Image portrait, hpFill, mpFill;
        GameObject activeBadge;
        PetPartyCarouselView carousel;
        GameplayHudQuickItemsView quickItems;
        PetRadialHudView radial; HubCaptureHud captureHud;
        PetStatusHudData confirmedStatus;
        readonly PetSlotHudData[] confirmedSlots=new PetSlotHudData[6];
        bool waitPartyKeyUp;int partyKeyFrame=-1;PartyPickerMode previousPartyMode;
        bool partyPressPending,partyWasOpenAtPress;float partyPressedAt;
        public static GameplayHudController Live {get;private set;}
        public static bool WantsHudCursor => Live!=null&&(Live.radial!=null&&Live.radial.IsOpen||Live.captureHud!=null&&Live.captureHud.IsPickerOpen);
        public PetRadialHudView Radial => radial;
        public KeyCode PartyKey => partyKey;
        public bool IsPartyOpen => radial!=null&&radial.IsOpen;
        PetCommandInput commandInput;
        PetCommandInput subscribedCommandInput;
        Capstone.Game.PetSummon.PetSummonDirector subscribedDirector;
        bool requestAccepted;
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
        void OnEnable() { Live=this; ResolveReferences(); SubscribeProvider(); HideExtraHud(); }
        void OnDisable() {
            if(Live==this)Live=null;
            CancelTransientHud();
            UnsubscribeProvider();
            if (subscribedCommandInput != null) subscribedCommandInput.PetSlotSummonRequested -= CenterRequestedPet;
            subscribedCommandInput = null;
            if(subscribedDirector!=null)subscribedDirector.SequenceFinished-=SummonFinished;
            subscribedDirector=null;
        }
        void Update() {
            SyncVisibility();
            if(previousPartyMode!=partyPickerMode){CancelTransientHud();previousPartyMode=partyPickerMode;}
            if(InventoryInputController.GameplayInputBlocked){partyPressPending=false;waitPartyKeyUp=Input.GetKey(partyKey);radial?.ResetState();}
            else ProcessPartyRelease(Input.GetKey(partyKey),Input.GetKeyUp(partyKey),Time.unscaledTime);
            if (root == null || !root.gameObject.activeSelf) return;
            if(trainerMenu==null&&Input.GetKeyDown(partyKey))HandlePartyKey(partyKey);
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
            var director=commandInput!=null?commandInput.GetComponent<Capstone.Game.PetSummon.PetSummonDirector>():null;
            if(isActiveAndEnabled&&director!=subscribedDirector){if(subscribedDirector!=null)subscribedDirector.SequenceFinished-=SummonFinished;subscribedDirector=director;if(director!=null)director.SequenceFinished+=SummonFinished;}
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
        void CenterRequestedPet(int slotIndex) { requestAccepted=true;partyPressPending=false;radial?.SetOpen(false);waitPartyKeyUp=Input.GetKey(partyKey); }
        void SummonFinished(int index,bool success){if(!success)radial?.Feedback(subscribedDirector?.LastFailureReason??"Chưa thể đổi thú lúc này.");RefreshHud();}
        public bool HandlePartyKey(KeyCode key) {
            if(key!=partyKey||radial==null||InventoryInputController.GameplayInputBlocked)return false;
            if(partyKeyFrame==Time.frameCount)return true;partyKeyFrame=Time.frameCount;
            if(waitPartyKeyUp)return true;
            if(partyPressPending)return true;
            partyPressPending=true;partyWasOpenAtPress=radial.IsOpen;partyPressedAt=Time.unscaledTime;
            radial.SetOpen(true);RefreshHud();return true;
        }
        internal void ProcessPartyRelease(bool pressed,bool up,float time){
            if(pressed&&!up)return;
            waitPartyKeyUp=false;
            if(!partyPressPending)return;
            bool close=partyPickerMode==PartyPickerMode.Hold||time-partyPressedAt>=partyHoldThreshold||partyWasOpenAtPress;
            partyPressPending=false;radial?.SetOpen(!close);RefreshHud();
        }
        public bool TryCloseTransientHud() {
            if(captureHud!=null&&captureHud.IsPickerOpen){captureHud.ClosePicker();return true;}
            if(IsPartyOpen){partyPressPending=false;radial.SetOpen(false);waitPartyKeyUp=Input.GetKey(partyKey);return true;}return false;
        }
        void CancelTransientHud(){partyPressPending=false;radial?.ResetState();captureHud?.CancelPending();waitPartyKeyUp=Input.GetKey(partyKey);}
        void OnApplicationFocus(bool focused){if(!focused)CancelTransientHud();}
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
            // Tracker owns Settings visibility. Do not deactivate its controller: it must be able to show again.
            foreach(var tracker in FindObjectsByType<QuestTrackerHudController>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(tracker!=null){bool show=Capstone.Game.SettingsSystem.GameSettingsService.Instance==null||Capstone.Game.SettingsSystem.GameSettingsService.Instance.Model.Data.showQuestTracker;if(tracker.gameObject.activeSelf!=show)tracker.gameObject.SetActive(show);}
            var compass = GameObject.Find("CompassHUD"); if (compass != null) compass.SetActive(false);
            var compassRoot = GameObject.Find("CompassHUDRoot"); if (compassRoot != null) compassRoot.SetActive(false);
        }
        void BuildMinimap() {
            // Keep the large minimap; allow its corner accents to sit just inside the screen.
            minimapView = V.Rect(root, "MiniMap"); V.Top(minimapView, 24f, 24f, 350f, 350f);
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
            HubR11Art.Layer(minimapView,"Components/minimap-rim__border");
            if (Application.isPlaying && Capstone.Game.SettingsSystem.GameSettingsService.Instance != null)
                SetMinimapVisible(Capstone.Game.SettingsSystem.GameSettingsService.Instance.Model.Data.showMinimap);
        }
        void BuildPetStatus() {
            var region=V.Rect(root,"PetRadial");V.Stretch(region);
            radial=region.gameObject.AddComponent<PetRadialHudView>();radial.Build(ConfirmPetSlot);
        }
        void BuildCarousel() {} // Kept for existing editor tooling; runtime is six pooled radial views.
        void ConfirmPetSlot(int index) {
            if (InventoryInputController.GameplayInputBlocked||commandInput==null) return;
            requestAccepted=false;
            commandInput.RequestSummonSlot(index);
            if(!requestAccepted)radial?.Feedback(subscribedDirector?.LastFailureReason??"Yêu cầu chưa được chấp nhận.");
        }
        void BuildSkills() {
            skillRegion=V.Rect(root,"SkillBar");
            V.Place(skillRegion,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,22),new Vector2(356,106));
            for(int i=0;i<4;i++) {
                int index=i;var slot=V.Rect(skillRegion,"SkillSlot"+(i+1));V.Top(slot,i*92,0,80,106);
                var button=HubR11Art.Button(slot,"Skill","Components/skill-slot");V.Top((RectTransform)button.transform,0,0,80,80);
                button.onClick.AddListener(()=>RequestSkill(index));
                var content=HubR11Art.Mask(button.transform,"Components/skill-slot",80);
                var view=new SkillView{button=button,content=content.gameObject.AddComponent<CanvasGroup>()};
                view.icon=V.Image(content,"SkillIcon");V.Stretch(view.icon.rectTransform,11);
                view.empty=HubR11Art.Label(content,"EmptyOrMissingIcon","—",16);V.Stretch(view.empty.rectTransform,5);
                view.cooldown=V.Image(content,"Cooldown");V.Stretch(view.cooldown.rectTransform);view.cooldown.sprite=V.CircleMask;
                view.cooldown.color=new Color(.07f,.11f,.18f,.72f);view.cooldown.type=Image.Type.Filled;
                view.cooldown.fillMethod=Image.FillMethod.Radial360;view.cooldown.fillOrigin=2;
                view.countdown=HubR11Art.Label(content,"CooldownSeconds","",26);V.Stretch(view.countdown.rectTransform);
                view.key=HubR11Art.Badge(slot,SkillKeyLabel(i));skills.Add(view);
            }
        }
        void BuildQuickItems() {
            var region=V.Rect(root,"QuickItems");V.Place(region,new Vector2(1,0),new Vector2(1,0),new Vector2(-28,22),new Vector2(176,106));
            quickItems=GetComponent<GameplayHudQuickItemsView>()??gameObject.AddComponent<GameplayHudQuickItemsView>();
            quickItems.Build(region,petHudProvider as PetCommandHudProvider);
            captureHud=GetComponent<HubCaptureHud>()??gameObject.AddComponent<HubCaptureHud>();captureHud.Build(root);
        }
        void RequestSkill(int index) {
            if (InventoryInputController.GameplayInputBlocked || !PetSkillsAvailable || Provider == null) return;
            IReadOnlyList<SkillHudData> data = Provider.GetSkills();
            if (index >= 0 && index < data.Count && data[index].unlocked && data[index].usable && !data[index].IsCoolingDown)
                Provider.RequestSkill(index);
        }
        void RefreshHud() {
            if(root==null||radial==null)return;
            var status=Provider!=null?Provider.GetSelectedPetStatus():default;
            var director=commandInput!=null?commandInput.GetComponent<Capstone.Game.PetSummon.PetSummonDirector>():null;
            // Accepted/queued requests never supply identity. Freeze the last confirmed view during a sequence.
            if(director==null||!director.IsSequenceRunning){confirmedStatus=status;var slots=Provider?.GetPetSlots();for(int i=0;i<6;i++)confirmedSlots[i]=slots!=null&&i<slots.Count?slots[i]:default;}
            var hintMode=partyPressPending&&Time.unscaledTime-partyPressedAt>=partyHoldThreshold?PartyPickerMode.Hold:partyPickerMode;
            radial.Refresh(confirmedStatus,confirmedSlots,hintMode,partyKey);RefreshSkills();quickItems?.Refresh();
        }
        void RefreshSkills() {
            IReadOnlyList<SkillHudData> data = Provider?.GetSkills();
            for (int i = 0; i < skills.Count; i++) {
                SkillView view = skills[i];
                bool has = data != null && i < data.Count && data[i].unlocked;
                SkillHudData skill = has ? data[i] : default;
                bool ready = PetSkillsAvailable && has && skill.usable && !skill.IsCoolingDown;
                view.button.interactable = ready; view.content.alpha = 1f;
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
                || (trainerMenu != null && trainerMenu.IsMenuOpen)||captureHud!=null&&(captureHud.IsPickerOpen||captureHud.IsPresenting
                    ||captureHud.OwnsPickerInput&&!InventoryInputController.IsBlockedByOtherThan(captureHud));
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
