using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.HudSystem {
    public sealed partial class PetPartyPanelController {
        [Header("Companion UI art (presentation only)")]
        [SerializeField] PetCompanionArtCatalog companionArt;
        VisualElement companionDesign;
        readonly VisualElement[] companionTabs=new VisualElement[3];
        PetSkillsPanelController companionSkills;
        bool preview3D,companionVisible;
        int companionTab;
        readonly Capstone.Game.UISystem.TrainerMenuPresentation companionPresentation=new Capstone.Game.UISystem.TrainerMenuPresentation();
        Capstone.Game.Inventory.MonsterInventoryController visibilityOwner;
        public bool IsPreview3D=>preview3D;
        public int CompanionTab=>companionTab;
        public bool HasPreviewTexture=>portraitPreview.Texture!=null;
        void CacheCompanionPresentation(){
            var nextDesign=panel?.Q("pets-design");if(companionDesign!=nextDesign){swapActions=null;swapConfirm=null;}companionDesign=nextDesign;if(companionDesign==null)return;
            root.UnregisterCallback<PointerDownEvent>(HideTooltipOnPointer,TrickleDown.TrickleDown);root.RegisterCallback<PointerDownEvent>(HideTooltipOnPointer,TrickleDown.TrickleDown);
            root.UnregisterCallback<TooltipEvent>(SuppressNativeTooltip,TrickleDown.TrickleDown);root.RegisterCallback<TooltipEvent>(SuppressNativeTooltip,TrickleDown.TrickleDown);
            selectedName?.UnregisterCallback<PointerEnterEvent>(ShowNameTooltip);selectedName?.RegisterCallback<PointerEnterEvent>(ShowNameTooltip);
            selectedName?.UnregisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);selectedName?.RegisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);
            foreach(var label in companionDesign.Query<Label>().ToList())if(label.ClassListContains("companion-stat-value")||label.ClassListContains("companion-resource-value")){label.UnregisterCallback<PointerEnterEvent>(ShowValueTooltip);label.RegisterCallback<PointerEnterEvent>(ShowValueTooltip);label.UnregisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);label.RegisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);}
            if(tooltip!=null){tooltip.pickingMode=PickingMode.Ignore;foreach(var child in tooltip.Query<VisualElement>().ToList())child.pickingMode=PickingMode.Ignore;}
            if(visibilityOwner!=inventoryOwner){if(visibilityOwner!=null)visibilityOwner.VisibilityChanged-=HandleCompanionVisibility;visibilityOwner=inventoryOwner;if(visibilityOwner!=null)visibilityOwner.VisibilityChanged+=HandleCompanionVisibility;}
            var modalSkin=Resources.Load<StyleSheet>("UI/GreenAngular/GreenAngular");
            if(modalSkin!=null&&!document.rootVisualElement.styleSheets.Contains(modalSkin))document.rootVisualElement.styleSheets.Add(modalSkin);
            companionSkills=GetComponent<PetSkillsPanelController>();
            companionTabs[0]=panel.Q("pets-tab-body-overview");companionTabs[1]=panel.Q("pets-tab-body-skills");companionTabs[2]=panel.Q("pets-tab-body-growth");
            var body=Capstone.Game.UISystem.MenuTypography.Font;
            companionDesign.style.unityFontDefinition=FontDefinition.FromFont(body);
            var serif=Capstone.Game.UISystem.TrainerMenuArt.Serif;
            foreach(var label in companionDesign.Query<Label>().ToList()){
                if(label.ClassListContains("companion-title")||label.ClassListContains("companion-name")||label.ClassListContains("companion-section-label"))
                    label.style.unityFontDefinition=FontDefinition.FromFont(serif);
            }
            foreach(var icon in companionDesign.Query<VisualElement>(className:"companion-icon").ToList())foreach(string cls in icon.GetClasses())if(cls.StartsWith("ci-")){string id=cls.Substring(3);if(id=="illustration")id="portrait";if(id=="details")id="profile";if(icon.parent?.name=="pets-tab-growth")id="grow";icon.style.backgroundImage=new StyleBackground(Capstone.Game.UISystem.InteractionR3Assets.Load("Masks/"+id));}
            var emblem=panel.Q("companion-emblem");if(emblem!=null)emblem.style.backgroundImage=new StyleBackground(CompanionArt.Load("Art/companion-title-emblem"));
            foreach(var corner in companionDesign.Query<VisualElement>(className:"companion-corner").ToList())corner.style.backgroundImage=new StyleBackground(CompanionArt.Load("Art/champagne-leaf-corner"));
            if(companionArt==null)companionArt=Resources.Load<PetCompanionArtCatalog>("UI/Companion/Portraits");
            Capstone.Game.UISystem.GreenAngularCompanionSkin.Bind(document.rootVisualElement,companionDesign);
            if(tooltip!=null)Capstone.Game.UISystem.GreenAngularCompanionSkin.Add(tooltip,"panel","primary");
            FitCompanionLayout();ShowCompanionTab(companionTab,false);
        }
        void RegisterCompanionControls(){
            if(companionDesign==null)return;
            RegisterButton(panel.Q<Button>("pets-tab-overview"),()=>ShowCompanionTab(0));
            RegisterButton(panel.Q<Button>("pets-tab-skills"),()=>ShowCompanionTab(1));
            RegisterButton(panel.Q<Button>("pets-tab-growth"),()=>ShowCompanionTab(2));
            RegisterButton(panel.Q<Button>("pets-skills-details"),()=>ShowCompanionTab(1));
            RegisterButton(panel.Q<Button>("pets-mode-toggle"),()=>SetPreview3D(!preview3D));
            var mode=panel.Q<Button>("pets-mode-toggle");if(mode!=null){mode.focusable=true;mode.tabIndex=0;mode.UnregisterCallback<KeyDownEvent>(HandlePreviewModeKey,TrickleDown.TrickleDown);mode.RegisterCallback<KeyDownEvent>(HandlePreviewModeKey,TrickleDown.TrickleDown);}
            RegisterActionTooltip(panel.Q<Button>("pets-mode-toggle"),"Chế độ hiển thị","Nhấn để chuyển giữa ảnh 2D và bản xem 3D.");
            RegisterButton(panel.Q<Button>("pets-swap-button"),ToggleCompanionSwap);
            RegisterButton(panel.Q<Button>("pets-growth-level"),OpenLevelUpPanel);
            RegisterButton(panel.Q<Button>("pets-growth-heal"),OpenHealPanel);
            RegisterButton(panel.Q<Button>("pets-growth-evolution"),()=>OpenEvolutionPanel());
            RegisterActionTooltip(panel.Q<Button>("pets-growth-level"),"Tăng cấp","Xem trước chỉ số và vật phẩm.");
            RegisterActionTooltip(panel.Q<Button>("pets-growth-heal"),"Hồi phục","Xem trước lượng hồi phục và vật phẩm.");
            RegisterActionTooltip(panel.Q<Button>("pets-growth-evolution"),"Tiến hóa","Xem điều kiện tiến hóa.");
        }
        void FitCompanionLayout(){
            if(companionDesign==null||panel==null)return;
            float width=panel.resolvedStyle.width,height=panel.resolvedStyle.height;
            if(float.IsNaN(width)||float.IsNaN(height)||width<=0||height<=0)return;
            float scale=Mathf.Min(width/1600,height/900);
            companionDesign.style.left=(width-1600*scale)/2;companionDesign.style.top=(height-900*scale)/2;
            companionDesign.style.scale=new Scale(new Vector3(scale,scale,1));
        }
        public void ShowCompanionTab(int index)=>ShowCompanionTab(index,true);
        void ShowCompanionTab(int index,bool refresh){
            index=Mathf.Clamp(index,0,2);if(refresh&&index==companionTab)return;
            int version=++tabTransitionVersion;tabFade?.Pause();VisualElement old=null;foreach(var body in companionTabs)if(body!=null&&body.style.display.value==DisplayStyle.Flex){old=body;break;}
            companionSkills?.CancelEquipPreview();HideTooltip();companionTab=index;
            string[] names={"overview","skills","growth"};for(int i=0;i<3;i++)panel?.Q<Button>("pets-tab-"+names[i])?.EnableInClassList("is-selected",i==index);
            Capstone.Game.UISystem.GreenAngularCompanionSkin.Update(companionDesign,index);
            void Show(){if(version!=tabTransitionVersion)return;
                for(int i=0;i<3;i++)if(companionTabs[i]!=null){companionTabs[i].style.display=i==index?DisplayStyle.Flex:DisplayStyle.None;companionTabs[i].style.opacity=1;}
                companionSkills=GetComponent<PetSkillsPanelController>();
                if(index==1&&companionTabs[1]!=null)companionSkills?.OpenEmbedded(companionTabs[1]);else companionSkills?.CloseEmbedded();
                if(refresh)RefreshCompanionPresentation();
                var next=companionTabs[index];if(next==null||!refresh)return;next.style.opacity=0;double start=Time.realtimeSinceStartupAsDouble;
                tabFade=next.schedule.Execute(()=>{if(version!=tabTransitionVersion)return;float t=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-start)/.1f);next.style.opacity=t;if(t>=1)tabFade?.Pause();}).Every(16);
            }
            if(!refresh||old==null||!companionVisible){Show();return;}
            double begin=Time.realtimeSinceStartupAsDouble;float fromOpacity=old.resolvedStyle.opacity;
            tabFade=old.schedule.Execute(()=>{if(version!=tabTransitionVersion)return;float t=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-begin)/.06f);old.style.opacity=fromOpacity*(1-t);if(t>=1){tabFade?.Pause();Show();}}).Every(16);
        }
        public void SetPreview3D(bool value){
            preview3D=value&&ViewedPet!=null;
            if(!preview3D)portraitPreview.Clear();
            RefreshCompanionPortrait(ViewedPet);
        }
        void HandlePreviewModeKey(KeyDownEvent evt){
            if(evt.keyCode!=KeyCode.Return&&evt.keyCode!=KeyCode.KeypadEnter&&evt.keyCode!=KeyCode.Space)return;
            var button=evt.currentTarget as Button;if(button==null||!button.enabledInHierarchy)return;
            evt.StopImmediatePropagation();evt.PreventDefault();
            if(buttonCallbacks.TryGetValue(button,out var invoke))invoke();
        }
        void UpdateCompanionVisibility(bool visible){
            if(visible==companionVisible)return;companionVisible=visible;
            if(visible){ResolveViewedPet();root?.AddToClassList("companion-context");companionPresentation.SetMenuVisible(true);preview3D=false;ShowCompanionTab(0);FitCompanionLayout();RefreshCompanionPresentation();}
            else StopCompanionPresentation();
        }
        void HandleCompanionVisibility(bool _)=>UpdateCompanionVisibility(inventoryOwner?.IsPetsSectionOpen==true);
        void ReleaseCompanionBindings(){panel?.Q<Button>("pets-mode-toggle")?.UnregisterCallback<KeyDownEvent>(HandlePreviewModeKey,TrickleDown.TrickleDown);if(visibilityOwner!=null)visibilityOwner.VisibilityChanged-=HandleCompanionVisibility;visibilityOwner=null;root?.UnregisterCallback<PointerDownEvent>(HideTooltipOnPointer,TrickleDown.TrickleDown);root?.UnregisterCallback<TooltipEvent>(SuppressNativeTooltip,TrickleDown.TrickleDown);selectedName?.UnregisterCallback<PointerEnterEvent>(ShowNameTooltip);selectedName?.UnregisterCallback<PointerLeaveEvent>(HandleActionTooltipLeave);}
        void StopCompanionPresentation(){++tabTransitionVersion;tabFade?.Pause();foreach(var body in companionTabs)if(body!=null)body.style.opacity=1;CancelViewedInteraction();viewedPet=null;viewedPetId=null;root?.RemoveFromClassList("companion-context");companionPresentation.SetMenuVisible(false);preview3D=false;portraitPreview.Clear();companionSkills?.CloseEmbedded();ClearSwapSelection();}
        void RefreshCompanionPortrait(PetController pet){
            if(companionDesign==null||selectedPortrait==null)return;
            var toggle=panel.Q<Button>("pets-mode-toggle");
            if(toggle!=null){toggle.tooltip=preview3D?"Chuyển sang ảnh 2D":"Chuyển sang xem 3D";actionReasons[toggle]=toggle.tooltip;toggle.EnableInClassList("is-selected",preview3D);}
            var modeText=panel.Q<Label>("pets-preview-mode-text");if(modeText!=null)modeText.text=preview3D?"3D":"2D";
            var modeIcon=panel.Q("pets-preview-mode-icon");if(modeIcon!=null)modeIcon.style.backgroundImage=new StyleBackground(Capstone.Game.UISystem.InteractionR3Assets.Load(preview3D?"Masks/view3d":"Masks/portrait"));
            if(preview3D){
                if(portraitPreview.Texture==null){Capstone.Game.UISystem.AngularSurface.SetArt(selectedPortrait,(Sprite)null);if(selectedPortraitFallback!=null){selectedPortraitFallback.text=pet!=null?"Đang chuẩn bị 3D…":"Chưa có thú để xem 3D";selectedPortraitFallback.style.display=DisplayStyle.Flex;}}
                return;
            }
            var metadata=pet!=null?pet.GetComponentInChildren<PetCollectionMetadata>(true):null;
            var art=companionArt?.Find(metadata)?.largeIllustration;
            Capstone.Game.UISystem.AngularSurface.SetArt(selectedPortrait,art);
            if(selectedPortraitFallback!=null){selectedPortraitFallback.text=pet!=null?"Chưa có ảnh minh họa 2D":"Chọn một thú để xem";selectedPortraitFallback.style.display=art!=null?DisplayStyle.None:DisplayStyle.Flex;}
        }
        void ToggleCompanionSwap(){
            if(pendingSwapSourceIndex>=0){ClearSwapSelection();SetFeedback("Đã hủy đổi vị trí.");return;}
            int index=ViewedSlotIndex;if(index<0)return;
            companionSkills?.CancelEquipPreview();HideTooltip();pendingSwapSourceIndex=index;pendingSwapTargetIndex=-1;swapSourcePet=ViewedPet;swapTargetPet=null;
            UpdateSwapSelectionVisual();SetFeedback("Chọn hoặc kéo thú đến ô đích để xem trước, rồi Xác nhận.");
        }
        void RefreshCompanionPresentation(System.Collections.Generic.IReadOnlyList<PetSlotHudData> slots=null){
            if(companionDesign==null)return;
            var pet=ViewedPet;slots??=Provider?.GetPetSlots();
            panel.Q<Button>("pets-level-up-button")?.SetEnabled(pet!=null);
            panel.Q<Button>("pets-heal-button")?.SetEnabled(pet!=null);
            panel.Q<Button>("pets-details-button")?.SetEnabled(pet!=null);
            panel.Q<Button>("pets-skills-details")?.SetEnabled(pet!=null);
            panel.Q<Button>("pets-mode-toggle")?.SetEnabled(pet!=null);
            panel.Q<Button>("pets-swap-button")?.SetEnabled(pet!=null);
            bool summoned=false;
            for(int i=0;i<partyCards.Count;i++){
                var card=partyCards[i];var slot=slots!=null&&i<slots.Count?slots[i]:default;
                summoned|=CommandProvider?.GetPetControllerAt(i)==ViewedPet&&slot.summoned;
                var badge=card.Q("companion-active");if(badge==null){badge=new Capstone.Game.UISystem.AngularSurface{name="companion-active",Shape="diamond",Role="primary"};badge.AddToClassList("companion-active-badge");var check=new VisualElement{pickingMode=PickingMode.Ignore};check.style.position=Position.Absolute;check.style.left=check.style.top=7;check.style.width=check.style.height=14;check.style.backgroundImage=new StyleBackground(Resources.Load<Sprite>("UI/GreenAngular/Icons/check"));check.style.unityBackgroundImageTintColor=Capstone.Game.UISystem.GreenAngularGeometry.Light;badge.Add(check);card.Add(badge);}
                badge.style.display=slot.summoned?DisplayStyle.Flex:DisplayStyle.None;badge.tooltip="Đang xuất chiến";
                var top=card.Q(className:"pet-card-top");top?.BringToFront();badge.BringToFront();
                var fallback=card.Q<Label>(className:"pet-card-portrait-fallback");if(fallback!=null){fallback.text=string.Empty;fallback.style.display=DisplayStyle.None;}
                var state=card.Q<Label>(className:"pet-card-status");if(state!=null)state.style.display=DisplayStyle.None;
                var favorite=card.Q<Button>("favorite");if(favorite!=null){favorite.text=string.Empty;favorite.style.backgroundImage=new StyleBackground(CompanionArt.Load("Icons/Mask/favorite"));}
                if(slot.occupied){var metadata=CommandProvider?.GetPetControllerAt(i)?.GetComponentInChildren<PetCollectionMetadata>(true);var avatar=companionArt?.Find(metadata)?.avatarCrop;if(avatar!=null)Capstone.Game.UISystem.AngularSurface.SetArt(card.Q("portrait"),avatar);}
                card.tooltip=string.Empty;
            }
            if(favoriteButton!=null)favoriteButton.text=string.Empty;if(renameButton!=null)renameButton.text=string.Empty;
            var swap=panel.Q<Button>("pets-swap-button");if(swap?.Q<Label>("pets-swap-caption")!=null)swap.Q<Label>("pets-swap-caption").text=pendingSwapSourceIndex>=0?"Hủy đổi vị trí":"Đổi vị trí";
            RefreshCompanionPortrait(pet);
            Capstone.Game.UISystem.GreenAngularCompanionSkin.Update(companionDesign,companionTab);
            if(companionTab==2)RefreshCompanionGrowth(pet);
        }
        void RefreshCompanionGrowth(PetController pet){
            bool data=pet!=null&&pet.GetComponentInChildren<PetCollectionMetadata>(true)!=null;
            var level=data?GetComponent<PetLevelUpService>()?.CreatePreview(pet):null;
            var heal=data?GetComponent<PetHealService>()?.CreatePreview(pet):null;
            var evolution=data?GetComponent<PetEvolutionService>()?.CreatePreview(pet):null;
            SetGrowth("level",level!=null?$"Cấp {level.CurrentLevel} → {level.NextLevel}   •   {Capstone.Game.UISystem.VietnameseUI.ItemName(level.ItemName)}: {level.OwnedItemQuantity} / {level.RequiredItemQuantity}\nHP {level.CurrentHealth} → {level.NextHealth}  |  ATK {level.CurrentAttack} → {level.NextAttack}  |  DEF {level.CurrentDefense} → {level.NextDefense}":"—",level?.DisabledReason,level?.ItemIcon,level!=null);
            SetGrowth("heal",heal!=null?$"{heal.ResourceName}: {heal.CurrentValue:0.#} → {heal.ResultValue:0.#}\n{heal.ItemName}: {heal.OwnedQuantity} / {heal.RequiredQuantity}":"—",heal?.DisabledReason,heal?.ItemIcon,heal!=null);
            SetGrowth("evolution",evolution!=null?$"{evolution.CurrentName} → {(evolution.IsConfigured?evolution.TargetName:"—")}\n{(evolution.IsConfigured?$"Cấp {evolution.CurrentLevel}/{evolution.RequiredLevel}  •  {evolution.RequiredItemId}":"Chưa có dạng tiếp theo khả dụng")}":"—",evolution?.DisabledReason,evolution?.TargetIcon,evolution?.IsConfigured==true);
        }
        void SetGrowth(string key,string summary,string reason,Sprite icon,bool available){
            var label=panel.Q<Label>("pets-growth-"+key+"-summary");if(label!=null)label.text=Capstone.Game.UISystem.VietnameseUI.SystemText(summary);
            var why=panel.Q<Label>("pets-growth-"+key+"-reason");if(why!=null)why.text=string.IsNullOrEmpty(reason)?(available?"Mở xem trước để xác nhận.":"Chưa có dữ liệu."):Capstone.Game.UISystem.VietnameseUI.SystemText(reason);
            var image=panel.Q("pets-growth-"+key+"-icon");if(image!=null)image.style.backgroundImage=icon!=null?new StyleBackground(icon):StyleKeyword.None;
            var button=panel.Q<Button>("pets-growth-"+key);button?.SetEnabled(available);if(button!=null){button.tooltip=string.Empty;actionReasons[button]=reason??string.Empty;}
        }
    }
}

