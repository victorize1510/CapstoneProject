using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Capstone.Game.UISystem;

namespace Capstone.Game.HudSystem {
    public sealed partial class PetPartyPanelController {
        PetController viewedPet,renameTarget,swapSourcePet,swapTargetPet;
        string viewedPetId;
        int pendingSwapTargetIndex=-1,tabTransitionVersion;
        bool swapSaving,suppressDockClick;
        IVisualElementScheduledItem tooltipDelay,tabFade;
        VisualElement swapActions,dragGhost;
        Button swapConfirm,dragOwner;
        int dragPointer=-1;
        Vector2 dragStart;
        bool dragArmed;
        readonly Dictionary<Button,string> actionReasons=new Dictionary<Button,string>();
        internal PetController ViewedPet {get {ResolveViewedPet();return viewedPet;} }
        internal int ViewedSlotIndex {get {var pet=ViewedPet;for(int i=0;i<6;i++)if(pet!=null&&CommandProvider?.GetPetControllerAt(i)==pet)return i;return -1;} }
        PetStatusHudData ViewedStatus=>CommandProvider!=null?CommandProvider.StatusFor(ViewedPet):Provider?.GetSelectedPetStatus()??default;
        IReadOnlyList<SkillHudData> ViewedSkills=>CommandProvider!=null?CommandProvider.SkillsFor(ViewedPet):Provider?.GetSkills();
        static string PetIdentity(PetController pet)=>pet!=null?pet.GetComponentInChildren<PetCollectionMetadata>(true)?.PersistentId:null;
        void ResolveViewedPet(){
            if(CommandProvider==null){
                // A readonly/missing provider cannot keep command targets from its predecessor.
                bool hadContext=viewedPet!=null||!string.IsNullOrEmpty(viewedPetId);
                viewedPet=null;viewedPetId=null;
                if(hadContext)CancelViewedInteraction();
                return;
            }
            for(int i=0;i<6;i++){var pet=CommandProvider.GetPetControllerAt(i);if(pet!=null&&(pet==viewedPet||(!string.IsNullOrEmpty(viewedPetId)&&PetIdentity(pet)==viewedPetId))){bool replaced=!ReferenceEquals(viewedPet,pet);viewedPet=pet;if(replaced)CancelViewedInteraction();return;}}
            if(viewedPet!=null||!string.IsNullOrEmpty(viewedPetId))CancelViewedInteraction();
            viewedPet=CommandProvider.GetSelectedPetController();
            if(viewedPet==null)for(int i=0;i<6;i++){viewedPet=CommandProvider.GetPetControllerAt(i);if(viewedPet!=null)break;}
            viewedPetId=PetIdentity(viewedPet);
        }
        void CancelViewedInteraction(){HideTooltip();ClearSwapSelection();companionSkills?.CancelEquipPreview();}
        void HideTooltipOnPointer(PointerDownEvent _)=>HideTooltip();
        void SuppressNativeTooltip(TooltipEvent e){if(companionVisible&&!IsDetailsOpen)e.StopImmediatePropagation();}
        void ShowNameTooltip(PointerEnterEvent _){ShowTooltip(selectedName,selectedName?.text,"Tên thú đang xem");}
        void ShowValueTooltip(PointerEnterEvent e){if(e.currentTarget is Label label)ShowTooltip(label,"Giá trị đầy đủ",label.text);}
        internal bool TryCancelInteraction(){
            if(pendingSwapSourceIndex>=0||dragGhost!=null){ClearSwapSelection();SetFeedback("Đã hủy đổi vị trí.");return true;}
            return companionSkills?.TryCancelEquipPreview()==true;
        }
        void OnApplicationFocus(bool focus){if(!focus)CancelViewedInteraction();}
        void EnsureSwapActions(){
            var dock=panel?.Q("companion-dock");if(dock==null||swapActions!=null)return;
            swapActions=new VisualElement{name="r3-swap-actions"};swapActions.AddToClassList("r3-swap-actions");
            swapConfirm=new AngularButton(ConfirmPartySwap){name="r3-swap-confirm",text="Xác nhận",Shape="parallel",Role="primary"};
            var cancel=new AngularButton(()=>{ClearSwapSelection();SetFeedback("Đã hủy đổi vị trí.");}){name="r3-swap-cancel",text="Hủy",Shape="parallel",Role="shell"};
            swapConfirm.AddToClassList("companion-button");cancel.AddToClassList("companion-button");((AngularButton)swapConfirm).Surface.SkinTexture=InteractionR3Assets.Texture("Assets/Interaction/cta-confirm");cancel.Surface.SkinTexture=InteractionR3Assets.Texture("Assets/Interaction/cta-cancel");swapActions.Add(swapConfirm);swapActions.Add(cancel);dock.Add(swapActions);swapActions.style.display=DisplayStyle.None;
        }
        void ConfirmPartySwap(){
            if(swapSaving||pendingSwapSourceIndex<0||pendingSwapTargetIndex<0||CommandProvider==null)return;
            int source=pendingSwapSourceIndex,target=pendingSwapTargetIndex;
            if(CommandProvider.GetPetControllerAt(source)!=swapSourcePet||swapSourcePet==null||CommandProvider.GetPetControllerAt(target)!=swapTargetPet){ClearSwapSelection();SetFeedback("Đội hình đã thay đổi. Chọn lại vị trí.");return;}
            swapSaving=true;swapConfirm?.SetEnabled(false);HideTooltip();
            try{
                bool saved=CommandProvider.TrySwapPetSlots(source,target);
                if(saved){ClearSwapSelection();SetFeedback($"Đã đổi ô {source+1} và {target+1}.");Refresh();}
                else{swapSaving=false;UpdateSwapSelectionVisual();SetFeedback("Không thể lưu. Vị trí đã hoàn tác; có thể thử lại.");}
            }finally{swapSaving=false;}
        }
        void BindDockDrag(Button card,int slot){
            card.RegisterCallback<PointerDownEvent>(e=>{
                if(e.button!=0||slot!=pendingSwapSourceIndex||swapSaving)return;
                dragArmed=true;dragStart=companionDesign.WorldToLocal(e.position);dragOwner=card;dragPointer=e.pointerId;HideTooltip();
            },TrickleDown.TrickleDown);
            card.RegisterCallback<PointerMoveEvent>(e=>{
                if(!dragArmed||dragOwner!=card)return;
                var point=companionDesign.WorldToLocal(e.position);
                if(dragGhost==null&&(point-dragStart).sqrMagnitude<64)return;
                if(dragGhost==null){
                    card.CapturePointer(e.pointerId);var ghost=new AngularSurface{name="r3-drag-ghost",Shape="avatar",Role="shell"};
                    ghost.style.position=Position.Absolute;ghost.style.width=210;ghost.style.height=112;ghost.style.opacity=.8f;
                    var source=card.Q<AngularSurface>("portrait");if(source!=null){ghost.Art=source.Art;ghost.ArtUv=source.ArtUv;}
                    dragGhost=ghost;companionDesign.Add(ghost);
                }
                dragGhost.style.left=point.x-105;dragGhost.style.top=point.y-56;e.StopPropagation();
            });
            card.RegisterCallback<PointerUpEvent>(e=>{
                if(!dragArmed||dragOwner!=card)return;
                bool dragged=dragGhost!=null;
                if(dragged){
                    int target=-1;for(int i=0;i<partyCards.Count;i++)if(partyCards[i].ContainsPoint(partyCards[i].WorldToLocal(e.position))){target=i;break;}
                    if(target>=0)TryCompletePartySwap(target);else{pendingSwapTargetIndex=-1;swapTargetPet=null;UpdateSwapSelectionVisual();SetFeedback("Thả ngoài ô: chưa chọn đích.");}
                    suppressDockClick=true;card.schedule.Execute(()=>suppressDockClick=false).StartingIn(100);e.StopPropagation();
                }
                ReleaseDockDrag();
            },TrickleDown.TrickleDown);
            card.RegisterCallback<PointerCaptureOutEvent>(_=>{if(dragOwner==card)ReleaseDockDrag();});
            card.RegisterCallback<PointerCancelEvent>(_=>ReleaseDockDrag());
            card.RegisterCallback<PointerEnterEvent>(_=>{
                var slots=Provider?.GetPetSlots();var data=slots!=null&&slot<slots.Count?slots[slot]:default;
                ShowTooltip(card,data.occupied?data.displayName:"Ô trống",data.occupied?$"Cấp {data.level} · HP {data.health:0}/{data.maxHealth:0}" : "Chọn làm đích trong chế độ Đổi vị trí.");
            });
            card.RegisterCallback<PointerLeaveEvent>(_=>HideTooltip());
        }
        void ReleaseDockDrag(){
            var owner=dragOwner;int pointer=dragPointer;dragOwner=null;dragPointer=-1;dragArmed=false;
            dragGhost?.RemoveFromHierarchy();dragGhost=null;
            if(owner is AngularButton b){b.Surface.Pressed=false;b.Surface.InvalidateFeedback();}
            if(owner!=null&&pointer>=0&&owner.HasPointerCapture(pointer))owner.ReleasePointer(pointer);
        }
    }
}
