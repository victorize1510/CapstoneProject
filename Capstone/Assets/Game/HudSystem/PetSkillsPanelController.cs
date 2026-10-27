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
        readonly Dictionary<Button, EventCallback<KeyDownEvent>> filterKeyCallbacks = new Dictionary<Button, EventCallback<KeyDownEvent>>();
        readonly Dictionary<Button, EventCallback<PointerDownEvent>> filterFocusCallbacks = new Dictionary<Button, EventCallback<PointerDownEvent>>();
        readonly Dictionary<Button, EventCallback<PointerEnterEvent>> equippedTooltipCallbacks = new Dictionary<Button, EventCallback<PointerEnterEvent>>();

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
        VisualElement embeddedHost;
        PetController embeddedPet,previewPet;
        string selectedSkillId,destinationSkillId,equipFeedback;
        int previewDestination=-1;
        bool equipSaving;
        Button equipConfirm;
        Label equipPreview;
        readonly HashSet<string> aliveLearnedIds=new HashSet<string>();
        readonly List<string> removedLearnedIds=new List<string>();
        readonly Dictionary<string,Button> learnedById=new Dictionary<string,Button>();
        readonly Dictionary<string,SkillHudData> renderedLearned=new Dictionary<string,SkillHudData>();
        VisualElement learnedGridOwner;
        bool learnedGridInitialized;
        PetController ContextPet=>IsEmbedded?GetComponent<PetPartyPanelController>()?.ViewedPet:LoadoutProvider?.GetSelectedPetController();
        public bool IsEmbedded=>embeddedHost!=null;
        public void OpenEmbedded(VisualElement host){
            if(host==null||(IsEmbedded&&embeddedHost==host))return;
            Close();if(dialog==null||tooltip==null)return;
            embeddedHost=host;dialog.AddToClassList("companion-embedded-skills");host.Add(dialog);
            var equippedTitle=dialog.Q<Label>(className:"skills-section-title");if(equippedTitle!=null)equippedTitle.text="Đang dùng";
            var learnedTitle=dialog.Q<Label>(className:"skills-learned-title");if(learnedTitle!=null)learnedTitle.text="Đã học";
            tooltip.style.left=StyleKeyword.Null;tooltip.style.top=StyleKeyword.Null;
            tooltip.AddToClassList("companion-inline-detail");dialog.Add(tooltip);tooltip.style.display=DisplayStyle.Flex;
            selectedLearnedIndex=-1;renderedLearned.Clear();Refresh();EnsureEquipAction();if(equipConfirm!=null)equipConfirm.style.display=DisplayStyle.Flex;if(equipPreview!=null)equipPreview.style.display=DisplayStyle.Flex;
            ResetEmbeddedDetails();
        }
        public void CloseEmbedded(){
            if(!IsEmbedded)return;
            CancelEquipPreview();embeddedHost=null;embeddedPet=null;
            foreach(var button in dialog.Query<Capstone.Game.UISystem.AngularButton>().ToList())button.SetAngularContext(false);
            foreach(var image in dialog.Query<VisualElement>(className:"companion-skill-art").ToList())image.RemoveFromClassList("companion-skill-art");
            foreach(var fallback in dialog.Query<Label>(className:"companion-skill-fallback").ToList()){fallback.RemoveFromClassList("companion-skill-fallback");fallback.style.backgroundImage=StyleKeyword.Null;}
            dialog.RemoveFromClassList("companion-embedded-skills");overlay.Add(dialog);
            if(equipConfirm!=null)equipConfirm.style.display=DisplayStyle.None;if(equipPreview!=null)equipPreview.style.display=DisplayStyle.None;
            tooltip.RemoveFromClassList("companion-inline-detail");overlay.Add(tooltip);tooltip.style.display=DisplayStyle.None;
        }

        void ResetEmbeddedDetails(){
            if(tooltipName!=null)tooltipName.text="Chi tiết kỹ năng";
            if(tooltipDescription!=null)tooltipDescription.text="Chọn kỹ năng đã học, sau đó chọn ô cần thay.";
            if(tooltipElement!=null)tooltipElement.text="—";
            if(tooltipLevel!=null)tooltipLevel.text="—";
            if(tooltipCooldown!=null)tooltipCooldown.text="—";
        }

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
            CloseEmbedded();
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
            CloseEmbedded();
            if (overlay == null) CacheElements();
            if (overlay == null) return;

            ResolveProvider();
            SubscribeProvider();
            selectedLearnedIndex = -1;
            selectedElements.Clear();
            overlay.style.display = DisplayStyle.Flex;
            Refresh();
            Capstone.Game.UISystem.PanelFocusUtility.Open(overlay);
        }

        public void Close() {
            CloseEmbedded();
            HideTooltip();
            if (overlay != null) overlay.style.display = DisplayStyle.None;
            Capstone.Game.UISystem.PanelFocusUtility.Close(overlay);
        }

        public void Refresh() {
            if (overlay == null) CacheElements();
            ResolveProvider();
            SubscribeProvider();

            PetStatusHudData status = IsEmbedded&&LoadoutProvider!=null?LoadoutProvider.StatusFor(ContextPet):Provider != null ? Provider.GetSelectedPetStatus() : default;
            if(IsEmbedded&&embeddedPet!=ContextPet){
                embeddedPet=ContextPet;CancelEquipPreview();
                ResetEmbeddedDetails();
            }
            selectedPetElement = LoadoutProvider != null ? LoadoutProvider.ElementFor(ContextPet) : PetElement.Unknown;
            activeEquippedSlotCount = LoadoutProvider != null
                ? LoadoutProvider.SlotCountFor(ContextPet)
                : status.hasPet ? Mathf.Clamp(Provider?.GetSkills()?.Count ?? 2, 2, 4) : 0;
            SetPetSummary(status);

            equippedSnapshot.Clear();
            IReadOnlyList<SkillHudData> equipped = status.hasPet ? LoadoutProvider!=null?LoadoutProvider.SkillsFor(ContextPet):Provider?.GetSkills() : null;
            if (equipped != null) {
                for (int i = 0; i < equipped.Count && i < 4; i++) equippedSnapshot.Add(equipped[i]);
            }

            learnedSnapshot.Clear();
            IReadOnlyList<SkillHudData> learned = !status.hasPet ? null : LoadoutProvider != null ? LoadoutProvider.LearnedFor(ContextPet) : equipped;
            if (learned != null) {
                for (int i = 0; i < learned.Count; i++) learnedSnapshot.Add(learned[i]);
            }

            if(!string.IsNullOrEmpty(selectedSkillId)){
                selectedLearnedIndex=learnedSnapshot.FindIndex(s=>s.skillId==selectedSkillId);
                if(selectedLearnedIndex<0)CancelEquipPreview();
            }

            RefreshEquippedSlots();
            RefreshFilterState();
            RebuildLearnedGrid();RefreshEquipPreview();
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
            if(learnedGridOwner!=learnedGrid){learnedGridOwner=learnedGrid;learnedGridInitialized=false;learnedById.Clear();renderedLearned.Clear();}
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
                EventCallback<PointerEnterEvent> callback = _ => ShowEquippedTooltip(slotIndex);
                equippedTooltipCallbacks.Add(slot, callback);
                slot.RegisterCallback(callback);
                slot.RegisterCallback<PointerLeaveEvent>(HandleTooltipLeave);
            }

            controlsRegistered = true;
        }

        void HandleTooltipLeave(PointerLeaveEvent _) => HideTooltip();

        void UnregisterControls() {
            foreach(var pair in filterKeyCallbacks)pair.Key.UnregisterCallback(pair.Value,TrickleDown.TrickleDown);
            foreach(var pair in filterFocusCallbacks)pair.Key.UnregisterCallback(pair.Value);
            filterKeyCallbacks.Clear();filterFocusCallbacks.Clear();
            foreach (var pair in equippedTooltipCallbacks) {
                pair.Key.UnregisterCallback(pair.Value);
                pair.Key.UnregisterCallback<PointerLeaveEvent>(HandleTooltipLeave);
            }
            equippedTooltipCallbacks.Clear();
            foreach (KeyValuePair<Button, Action> pair in buttonCallbacks) pair.Key.clicked -= pair.Value;
            buttonCallbacks.Clear();
            controlsRegistered = false;
        }

        void RegisterButton(Button button, Action callback) {
            if (button == null || callback == null || buttonCallbacks.ContainsKey(button)) return;
            Action guarded = Capstone.Game.UISystem.PlayerActionGate.Guard(this, "button:" + button.name, callback);
            button.clicked += guarded;
            buttonCallbacks.Add(button, guarded);
            if(button.ClassListContains("skills-filter-button")){
                button.focusable=true;
                EventCallback<PointerDownEvent> focus=_=>button.Focus();
                EventCallback<KeyDownEvent> submit=evt=>{
                    if(!button.enabledInHierarchy||(evt.keyCode!=KeyCode.Return&&evt.keyCode!=KeyCode.KeypadEnter&&evt.keyCode!=KeyCode.Space))return;
                    guarded();evt.StopImmediatePropagation();
                };
                button.RegisterCallback(focus);button.RegisterCallback(submit,TrickleDown.TrickleDown);
                filterFocusCallbacks.Add(button,focus);filterKeyCallbacks.Add(button,submit);
            }
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
            if (IsOpen || IsEmbedded) Refresh();
        }

        void SetPetSummary(PetStatusHudData status) {
            string displayName = status.hasPet && !string.IsNullOrWhiteSpace(status.displayName)
                ? status.displayName.Trim()
                : "Chọn một thú";
            if (petName != null) petName.text = displayName;
            if (petLevel != null) petLevel.text = status.hasPet && status.level > 0 ? $"Cấp {status.level}" : "Cấp -";
            if (petElement != null) petElement.text = FormatElement(selectedPetElement);
            SetPortrait(petPortrait, petPortraitFallback, status.icon, displayName);
        }

        void RefreshEquippedSlots() {
            for (int i = 0; i < equippedSlots.Count; i++) {
                Button slot = equippedSlots[i];
                bool visible = i < activeEquippedSlotCount;
                slot.style.display = visible || IsEmbedded ? DisplayStyle.Flex : DisplayStyle.None;
                slot.SetEnabled(visible);
                if (!visible) {
                    if(IsEmbedded){SetText(slot,"skills-equipped-name","Chưa mở");SetText(slot,"skills-equipped-meta","Theo số ô của thú");var locked=slot.Q<VisualElement>(className:"skills-equipped-icon");CompanionArt.SetSkillImage(locked,locked?.Q<Label>(),null,true);}
                    continue;
                }
                bool available = i < equippedSnapshot.Count && equippedSnapshot[i].unlocked;
                SkillHudData skill = available ? equippedSnapshot[i] : default;
                slot.EnableInClassList("is-empty", !available);
                slot.userData = i;

                SetText(slot, "skills-equipped-name", available ? SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName)) : "Trống");
                SetText(slot, "skills-equipped-meta", available ? FormatCooldown(skill) : "Chọn kỹ năng rồi chọn ô");
                VisualElement icon = slot.Q<VisualElement>(className: "skills-equipped-icon");
                Label fallback = icon?.Q<Label>(className: "skills-icon-fallback");
                if(IsEmbedded)CompanionArt.SetSkillImage(icon,fallback,available?skill.icon:null,!available);
                else SetPortrait(icon, fallback, available ? skill.icon : null, available ? skill.displayName : "+");
            }
        }

        void RebuildLearnedGrid() {
            if(learnedGrid==null)return;
            if(!learnedGridInitialized){learnedGrid.Clear();learnedGridInitialized=true;}
            var alive=aliveLearnedIds;alive.Clear();learnedCards.Clear();int visible=0;
            for(int i=0;i<learnedSnapshot.Count;i++){
                var skill=learnedSnapshot[i];string key=string.IsNullOrEmpty(skill.skillId)?"missing-id:"+i:skill.skillId;
                if(!alive.Add(key))continue;
                if(!learnedById.TryGetValue(key,out var card)){card=CreateLearnedCard(skill,i);learnedById.Add(key,card);learnedGrid.Add(card);}
                if(!(card.userData is int oldIndex)||oldIndex!=i)card.userData=i;card.EnableInClassList("is-selected",skill.skillId==selectedSkillId&&!string.IsNullOrEmpty(selectedSkillId));
                bool show=skill.unlocked&&MatchesFilter(EffectiveElement(skill));card.style.display=show?DisplayStyle.Flex:DisplayStyle.None;
                if(show)visible++;learnedCards.Add(card);
                if(!renderedLearned.TryGetValue(key,out var old)||old.displayName!=skill.displayName||old.element!=skill.element||old.icon!=skill.icon||old.cooldownSeconds!=skill.cooldownSeconds||old.skillLevel!=skill.skillLevel){
                    SetText(card,"skills-learned-name",SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName)));
                    SetText(card,"skills-learned-element",FormatElement(EffectiveElement(skill)));SetText(card,"skills-learned-meta",FormatCooldown(skill));
                    var icon=card.Q("learned-icon");var fallback=icon?.Q<Label>();if(IsEmbedded)CompanionArt.SetSkillImage(icon,fallback,skill.icon);else SetPortrait(icon,fallback,skill.icon,skill.displayName);
                    renderedLearned[key]=skill;
                }
            }
            var removed=removedLearnedIds;removed.Clear();foreach(var pair in learnedById)if(!alive.Contains(pair.Key)){pair.Value.RemoveFromHierarchy();removed.Add(pair.Key);}
            foreach(var id in removed){learnedById.Remove(id);renderedLearned.Remove(id);}
            if(emptyLabel!=null)emptyLabel.style.display=visible>0?DisplayStyle.None:DisplayStyle.Flex;
        }

        Button CreateLearnedCard(SkillHudData skill, int learnedIndex) {
            var card = new Capstone.Game.UISystem.AngularButton { text = string.Empty, userData = learnedIndex, Role="raised" };
            card.AddToClassList("skills-learned-card");
            card.EnableInClassList("is-selected", learnedIndex == selectedLearnedIndex);

            var icon = new VisualElement { name = "learned-icon" };
            icon.AddToClassList("skills-learned-icon");
            var fallback = new Label(FirstLetter(skill.displayName));
            fallback.AddToClassList("skills-icon-fallback");
            icon.Add(fallback);
            if(IsEmbedded)CompanionArt.SetSkillImage(icon,fallback,skill.icon);
            else SetPortrait(icon, fallback, skill.icon, skill.displayName);
            card.Add(icon);

            var copy = new VisualElement();
            copy.AddToClassList("skills-learned-copy");
            copy.Add(CreateLabel(SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName)), "skills-learned-name"));
            copy.Add(CreateLabel(FormatElement(EffectiveElement(skill)), "skills-learned-element"));
            copy.Add(CreateLabel(FormatCooldown(skill), "skills-learned-meta"));
            card.Add(copy);

            card.clicked += () => {if(card.userData is int index)SelectLearnedSkill(index);};
            card.RegisterCallback<PointerEnterEvent>(_ => {if(card.userData is int index&&index<learnedSnapshot.Count)ShowTooltip(card, learnedSnapshot[index]);});
            card.RegisterCallback<PointerLeaveEvent>(_ => HideTooltip());
            return card;
        }

        void SelectLearnedSkill(int learnedIndex) {
            if (learnedIndex < 0 || learnedIndex >= learnedSnapshot.Count) return;
            equipFeedback=null;
            selectedLearnedIndex = selectedLearnedIndex == learnedIndex ? -1 : learnedIndex;
            selectedSkillId=selectedLearnedIndex>=0?learnedSnapshot[selectedLearnedIndex].skillId:null;previewPet=ContextPet;
            if(IsEmbedded&&selectedLearnedIndex>=0)ShowTooltip(dialog,learnedSnapshot[selectedLearnedIndex]);
            else if(IsEmbedded)ResetEmbeddedDetails();
            foreach (Button card in learnedCards) {
                card.EnableInClassList("is-selected", card.userData is int index && index == selectedLearnedIndex);
                if(card is Capstone.Game.UISystem.AngularButton angular)angular.Surface.MarkDirtyRepaint();
            }

            RefreshEquipPreview();
            if (helperLabel != null) {
                helperLabel.text = selectedLearnedIndex >= 0
                    ? $"Đã chọn {SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(learnedSnapshot[selectedLearnedIndex].displayName))}. Chọn một trong {activeEquippedSlotCount} ô để thay."
                    : "Chọn kỹ năng đã học, sau đó chọn ô cần thay.";
            }
        }

        void EquipSelectedInto(int equippedSlotIndex) {
            if(equippedSlotIndex<0||equippedSlotIndex>=activeEquippedSlotCount)return;
            equipFeedback=null;
            if(!IsEmbedded){if(selectedLearnedIndex>=0)TryEquip(selectedLearnedIndex,equippedSlotIndex);return;}
            previewDestination=equippedSlotIndex;previewPet=ContextPet;
            destinationSkillId=equippedSlotIndex<equippedSnapshot.Count?equippedSnapshot[equippedSlotIndex].skillId:string.Empty;
            RefreshEquipPreview();
        }

        void TryEquip(int learnedSkillIndex, int equippedSlotIndex) {
            if (LoadoutProvider == null) {
                if (helperLabel != null) helperLabel.text = "Provider hiện tại chỉ cho xem kỹ năng, chưa hỗ trợ thay loadout.";
                return;
            }

            string skillName = learnedSkillIndex >= 0 && learnedSkillIndex < learnedSnapshot.Count
                ? SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(learnedSnapshot[learnedSkillIndex].displayName))
                : "Kỹ năng";
            if (!LoadoutProvider.TryEquipLearnedSkill(learnedSkillIndex, equippedSlotIndex)) {
                if (helperLabel != null) helperLabel.text = "Không thể thay kỹ năng vào ô này.";
                return;
            }

            selectedLearnedIndex = -1;
            if (helperLabel != null) helperLabel.text = $"Đã trang bị {skillName} vào ô {equippedSlotIndex + 1}.";
            Refresh();
        }


        internal bool HasEquipPreview=>previewDestination>=0||!string.IsNullOrEmpty(selectedSkillId);
        internal bool TryCancelEquipPreview(){if(!HasEquipPreview)return false;CancelEquipPreview();return true;}
        internal void CancelEquipPreview(){
            selectedLearnedIndex=-1;selectedSkillId=null;previewDestination=-1;destinationSkillId=null;previewPet=null;equipSaving=false;equipFeedback=null;
            foreach(var card in learnedCards)card.EnableInClassList("is-selected",false);
            foreach(var card in equippedSlots)card.EnableInClassList("is-selected",false);
            if(equipPreview!=null)equipPreview.text="Chọn kỹ năng và ô đích.";equipConfirm?.SetEnabled(false);
            if(IsEmbedded)ResetEmbeddedDetails();
        }
        void EnsureEquipAction(){
            if(tooltip==null||equipConfirm!=null)return;
            if(tooltipDescription!=null){var scroll=new ScrollView(ScrollViewMode.Vertical){name="r3-skill-description-scroll"};scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;scroll.verticalScrollerVisibility=ScrollerVisibility.Auto;int index=tooltip.IndexOf(tooltipDescription);tooltip.Insert(index,scroll);scroll.Add(tooltipDescription);}
            equipPreview=new Label{name="r3-equip-preview",pickingMode=PickingMode.Ignore};equipPreview.AddToClassList("r3-equip-preview");tooltip.Add(equipPreview);
            var button=new Capstone.Game.UISystem.AngularButton(ConfirmEquipPreview){name="r3-equip-confirm",text="Trang bị",Shape="parallel",Role="primary"};button.Surface.SkinTexture=Capstone.Game.UISystem.InteractionR3Assets.Texture("Assets/Interaction/cta-equip");equipConfirm=button;tooltip.Add(equipConfirm);equipConfirm.SetEnabled(false);
        }
        void RefreshEquipPreview(){
            if(!IsEmbedded)return;EnsureEquipAction();
            bool learned=selectedLearnedIndex>=0&&selectedLearnedIndex<learnedSnapshot.Count&&!string.IsNullOrEmpty(selectedSkillId);
            bool destination=previewDestination>=0&&previewDestination<activeEquippedSlotCount;
            bool same=learned&&destination&&destinationSkillId==selectedSkillId;
            bool stale=destination&&(previewDestination<equippedSnapshot.Count?equippedSnapshot[previewDestination].skillId:string.Empty)!=destinationSkillId;
            for(int i=0;i<equippedSlots.Count;i++)equippedSlots[i].EnableInClassList("is-selected",i==previewDestination);
            string next=learned?Capstone.Game.UISystem.VietnameseUI.Skill(learnedSnapshot[selectedLearnedIndex].displayName):"kỹ năng";
            string old=destination&&previewDestination<equippedSnapshot.Count?Capstone.Game.UISystem.VietnameseUI.Skill(equippedSnapshot[previewDestination].displayName):"Trống";
            if(equipPreview!=null)equipPreview.text=equipFeedback??(stale?"Ô đã đổi; chọn lại ô đích.":same?"Kỹ năng đã ở ô này.":learned&&destination?$"Ô {previewDestination+1}: {old} → {next}":"Chọn kỹ năng và ô đích.");
            equipConfirm?.SetEnabled(learned&&destination&&!same&&!stale&&!equipSaving&&previewPet!=null&&previewPet==ContextPet);
        }
        void ConfirmEquipPreview(){
            if(equipSaving||previewPet==null||previewPet!=ContextPet||LoadoutProvider==null||!LoadoutProvider.ContainsPet(previewPet))return;
            var fresh=LoadoutProvider.LearnedFor(previewPet);int index=-1;for(int i=0;i<fresh.Count;i++)if(fresh[i].skillId==selectedSkillId&&fresh[i].unlocked){index=i;break;}
            var equipped=LoadoutProvider.SkillsFor(previewPet);string current=previewDestination>=0&&previewDestination<equipped.Count?equipped[previewDestination].skillId:string.Empty;
            if(index<0||previewDestination<0||previewDestination>=LoadoutProvider.SlotCountFor(previewPet)||current!=destinationSkillId||current==selectedSkillId){CancelEquipPreview();if(equipPreview!=null)equipPreview.text="Dữ liệu đã đổi; chọn lại kỹ năng và ô.";return;}
            equipSaving=true;equipConfirm?.SetEnabled(false);
            try{
                if(LoadoutProvider.EquipFor(previewPet,index,previewDestination)){CancelEquipPreview();Refresh();equipFeedback="Đã trang bị và lưu.";}
                else{equipFeedback="Lưu thất bại; đã hoàn tác. Thử lại.";}
            }finally{equipSaving=false;RefreshEquipPreview();}
        }

        void SelectAllElements() {
            selectedElements.Clear();
            RefreshFilterState();
            RebuildLearnedGrid();
        }

        void ToggleElement(PetElement element) {
            if (!filterButtons.ContainsKey(element)) return;
            if (!selectedElements.Add(element)) selectedElements.Remove(element);
            // Empty is the canonical unfiltered state, including selecting every type.
            if (selectedElements.Count == filterButtons.Count) selectedElements.Clear();
            RefreshFilterState();
            RebuildLearnedGrid();
        }

        void RefreshFilterState() {
            bool allSelected = selectedElements.Count == 0;
            SetFilterSelected(allFilterButton, allSelected, "Hiện kỹ năng của tất cả các hệ");
            foreach (KeyValuePair<PetElement, Button> pair in filterButtons) {
                bool selected = selectedElements.Contains(pair.Key);
                SetFilterSelected(pair.Value, selected, selected ? "Đang lọc hệ này · Bấm để bỏ chọn" : "Bấm để thêm hệ này vào bộ lọc");
            }
        }

        static void SetFilterSelected(Button button, bool selected, string hint) {
            if (button == null) return;
            button.EnableInClassList("is-selected", selected);
            button.tooltip = hint;
            var mark = button.Q<Label>("filter-selection-mark");
            if (mark == null) {
                mark = new Label { name = "filter-selection-mark", pickingMode = PickingMode.Ignore };
                mark.AddToClassList("filter-selection-mark");
                button.Add(mark);
            }
            mark.text = selected ? "✓" : "";
            if (button is Capstone.Game.UISystem.AngularButton angular) {
                string role = selected ? "filter-selected" : "raised";
                if(angular.Surface.Role!=role || angular.Surface.Material){
                    angular.Surface.Role = role;
                    angular.Surface.Material = false;
                    angular.Surface.MarkDirtyRepaint();
                }
                var caption=angular.Q<Label>(className:"angular-button-caption");
                if(caption!=null){caption.style.left=24;caption.style.right=6;}
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
            // Embedded details follow the clicked selection, not incidental pointer hover.
            if(IsEmbedded&&anchor!=dialog)return;
            if (tooltipName != null) tooltipName.text = SafeName(Capstone.Game.UISystem.VietnameseUI.Skill(skill.displayName));
            if (tooltipElement != null) tooltipElement.text = FormatElement(EffectiveElement(skill));
            if (tooltipLevel != null) tooltipLevel.text = Mathf.Max(1, skill.skillLevel).ToString();
            if (tooltipCooldown != null) tooltipCooldown.text = skill.cooldownSeconds > 0f ? $"{skill.cooldownSeconds:0.#} giây" : "-";
            if (tooltipDescription != null) tooltipDescription.text = Capstone.Game.UISystem.VietnameseUI.SkillDescription(skill.description);

            if(IsEmbedded){tooltip.style.display=DisplayStyle.Flex;return;}

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
            if(IsEmbedded)return;
            if (tooltip != null) tooltip.style.display = DisplayStyle.None;
        }

        static Label CreateLabel(string text, string className) {
            var label = new Label(text);
            label.AddToClassList(className);
            return label;
        }

        static void SetText(VisualElement root, string className, string value) {
            Label label = root?.Q<Label>(className: className);
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
            return string.IsNullOrWhiteSpace(value) ? "Kỹ năng" : value.Trim();
        }

        static string FirstLetter(string value) {
            return string.IsNullOrWhiteSpace(value) ? "?" : value.Trim().Substring(0, 1).ToUpperInvariant();
        }

        static string FormatCooldown(SkillHudData skill) {
            return skill.cooldownSeconds > 0f ? $"Hồi chiêu: {skill.cooldownSeconds:0.#} giây" : "Hồi chiêu: —";
        }

        static string FormatElement(PetElement element) => Capstone.Game.UISystem.VietnameseUI.Element(element);
    }
}
