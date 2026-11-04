using System;
using System.Collections.Generic;
using System.Linq;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    public sealed partial class PetSkillsPanelController {
        enum SkillWorkflowMode { View, ChooseSlot, ChooseSkill, Review }
        SkillWorkflowMode skillWorkflowMode;
        int inspectedEquippedSlot=-1;
        readonly string[] workflowSnapshot=new string[4];
        int workflowSlotCount;
        VisualElement workflowLower,workflowCatalog,workflowPreview;
        ScrollView workflowDetailsScroll;
        CompanionFilterRail workflowRail;
        VisualElement workflowFilters;
        StyleEnum<Position> originalFilterPosition;StyleEnum<Overflow> originalFilterOverflow;
        StyleLength originalFilterLeft,originalFilterTop,originalFilterWidth,originalFilterHeight;
        AngularButton workflowToggle;
        readonly List<(VisualElement node,VisualElement parent,int index)> workflowOriginal=new List<(VisualElement,VisualElement,int)>();
        readonly List<SkillWorkflowPreviewRow> workflowRows=new List<SkillWorkflowPreviewRow>();
        bool workflowAttached;

        void AttachSkillWorkflow(){
            if(workflowAttached)return;
            dialog.AddToClassList("skill-workflow");
            var sheet=Resources.Load<StyleSheet>("UI/CompanionSkillScroll20261101/Workflow");if(sheet!=null&&!dialog.styleSheets.Contains(sheet))dialog.styleSheets.Add(sheet);
            var heading=dialog.Q(className:"skills-section-header");
            if(workflowLower==null){
                workflowToggle=new AngularButton{name="skills-change-mode",text="Đổi kỹ năng",Shape="parallel",Role="primary"};
                workflowToggle.AddToClassList("skill-workflow-toggle");heading.Add(workflowToggle);RegisterButton(workflowToggle,ToggleSkillWorkflow);
                workflowLower=new VisualElement{name="skill-workflow-lower"};workflowLower.AddToClassList("skill-workflow-lower");
                workflowCatalog=new VisualElement{name="skill-workflow-catalog"};workflowCatalog.AddToClassList("skill-workflow-catalog");workflowLower.Add(workflowCatalog);
                workflowDetailsScroll=RuntimeScrollbars.Create();workflowDetailsScroll.name="skill-workflow-details";workflowDetailsScroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;
                workflowPreview=new VisualElement{name="skill-workflow-preview"};workflowPreview.AddToClassList("skill-workflow-preview");
                for(int i=0;i<2;i++){var row=new SkillWorkflowPreviewRow();workflowRows.Add(row);workflowPreview.Add(row);}
                void Remember(VisualElement e){if(e?.parent!=null)workflowOriginal.Add((e,e.parent,e.parent.IndexOf(e)));}
                var filters=dialog.Q(className:"skills-filter-row");workflowFilters=filters;Remember(filters);
                if(filters!=null){originalFilterPosition=filters.style.position;originalFilterOverflow=filters.style.overflow;originalFilterLeft=filters.style.left;originalFilterTop=filters.style.top;originalFilterWidth=filters.style.width;originalFilterHeight=filters.style.height;}
                workflowRail=dialog.Q<CompanionFilterRail>();if(workflowRail==null&&filters!=null)workflowRail=new CompanionFilterRail(filters);
                Remember(dialog.Q<ScrollView>("skills-learned-scroll"));Remember(helperLabel);
                foreach(var child in tooltip.Children().ToArray())if(child is Label&&child!=equipPreview||child.ClassListContains("skills-tooltip-row")||child.ClassListContains("skills-tooltip-divider"))Remember(child);
                // Existing shared binding recognizes this plate/rail and does not create duplicates.
                var scroll=dialog.Q<ScrollView>("skills-learned-scroll");var plate=dialog.Q<AngularSurface>("green-learned-panel");
                if(plate==null)plate=new AngularSurface{name="green-learned-panel",Shape="panel",Role="raised"};
                plate.style.position=Position.Absolute;plate.style.left=plate.style.right=plate.style.top=plate.style.bottom=0;plate.style.width=plate.style.height=StyleKeyword.Auto;
                scroll.hierarchy.Insert(0,plate);plate.AddToClassList("workflow-catalog-plate");
            }
            dialog.Add(workflowLower);
            if(workflowRail!=null){workflowRail.Q<ScrollView>("green-filter-viewport").Add(workflowFilters);workflowFilters.style.position=Position.Relative;workflowFilters.style.left=workflowFilters.style.top=0;workflowFilters.style.width=StyleKeyword.Auto;workflowFilters.style.height=36;workflowFilters.style.overflow=Overflow.Visible;workflowCatalog.Add(workflowRail);}
            var learned=workflowOriginal.Select(x=>x.node).OfType<ScrollView>().FirstOrDefault();if(learned!=null)workflowCatalog.Add(learned);
            workflowLower.Add(tooltip);tooltip.Add(helperLabel);tooltip.Add(workflowDetailsScroll);
            foreach(var entry in workflowOriginal)if(entry.node is Label&&entry.node!=helperLabel||entry.node.ClassListContains("skills-tooltip-row")||entry.node.ClassListContains("skills-tooltip-divider"))workflowDetailsScroll.Add(entry.node);
            tooltip.Add(workflowPreview);tooltip.Add(equipPreview);tooltip.Add(equipConfirm);
            workflowToggle.style.display=DisplayStyle.Flex;equipConfirm.text="Xác nhận đổi";
            heading.Q<Label>(className:"skills-section-title").text="Đang trang bị";
            workflowAttached=true;RuntimeScrollbars.Bind(dialog);
        }
        void DetachSkillWorkflow(){
            if(!workflowAttached)return;
            foreach(var card in equippedSlots)SkillWorkflowFeedback.Stop(card);
            foreach(var card in learnedCards)SkillWorkflowFeedback.Stop(card);
            // Restore original functional nodes so the standalone modal keeps its own layout/behavior.
            foreach(var e in workflowOriginal.OrderBy(x=>x.index))if(!(e.node is CompanionFilterRail)&&e.parent!=null)e.parent.Insert(Mathf.Min(e.index,e.parent.childCount),e.node);
            workflowRail?.RemoveFromHierarchy();
            if(workflowFilters!=null){workflowFilters.style.position=originalFilterPosition;workflowFilters.style.overflow=originalFilterOverflow;workflowFilters.style.left=originalFilterLeft;workflowFilters.style.top=originalFilterTop;workflowFilters.style.width=originalFilterWidth;workflowFilters.style.height=originalFilterHeight;}
            workflowLower.RemoveFromHierarchy();workflowDetailsScroll.RemoveFromHierarchy();workflowPreview.RemoveFromHierarchy();
            dialog.RemoveFromClassList("skill-workflow");workflowToggle.style.display=DisplayStyle.None;workflowAttached=false;
        }
        string SlotId(int index)=>index>=0&&index<equippedSnapshot.Count?equippedSnapshot[index].skillId??string.Empty:string.Empty;
        static string SkillTitle(SkillHudData skill)=>string.IsNullOrWhiteSpace(skill.displayName)?"Trống":VietnameseUI.Skill(skill.displayName);
        void ToggleSkillWorkflow(){
            if(equipSaving)return;
            if(skillWorkflowMode!=SkillWorkflowMode.View){CancelSkillWorkflow(true);return;}
            equipFeedback=null;selectedLearnedIndex=-1;selectedSkillId=null;inspectedEquippedSlot=-1;previewDestination=-1;previewPet=ContextPet;
            skillWorkflowMode=SkillWorkflowMode.ChooseSlot;RefreshSkillWorkflow();
        }
        void SelectWorkflowSlot(int index){
            if(equipSaving||index<0||index>=activeEquippedSlotCount)return;
            equipFeedback=null;inspectedEquippedSlot=index;
            if(skillWorkflowMode==SkillWorkflowMode.View){selectedSkillId=null;selectedLearnedIndex=-1;RefreshSkillWorkflow();return;}
            previewDestination=index;previewPet=ContextPet;destinationSkillId=SlotId(index);workflowSlotCount=activeEquippedSlotCount;
            for(int i=0;i<4;i++)workflowSnapshot[i]=SlotId(i);
            selectedSkillId=null;selectedLearnedIndex=-1;skillWorkflowMode=SkillWorkflowMode.ChooseSkill;RefreshSkillWorkflow();
        }
        void SelectWorkflowSkill(int index){
            if(equipSaving||index<0||index>=learnedSnapshot.Count)return;
            if(skillWorkflowMode==SkillWorkflowMode.ChooseSlot)return;
            equipFeedback=null;inspectedEquippedSlot=-1;selectedLearnedIndex=index;selectedSkillId=learnedSnapshot[index].skillId;
            if(skillWorkflowMode!=SkillWorkflowMode.View)skillWorkflowMode=SkillWorkflowMode.Review;
            RefreshSkillWorkflow();
        }
        bool WorkflowSnapshotMatches(){
            if(previewPet==null||previewPet!=ContextPet||workflowSlotCount!=activeEquippedSlotCount)return false;
            for(int i=0;i<4;i++)if(workflowSnapshot[i]!=SlotId(i))return false;
            return true;
        }
        void CancelSkillWorkflow(bool keepInspection){
            int prior=previewDestination;
            skillWorkflowMode=SkillWorkflowMode.View;selectedLearnedIndex=-1;selectedSkillId=null;previewDestination=-1;destinationSkillId=null;previewPet=null;equipSaving=false;equipFeedback=null;
            inspectedEquippedSlot=keepInspection&&prior>=0&&prior<activeEquippedSlotCount?prior:-1;
            if(workflowAttached)RefreshSkillWorkflow();
        }
        void RefreshSkillWorkflow(){
            if(!IsEmbedded||!workflowAttached)return;
            if(!equipSaving&&skillWorkflowMode>=SkillWorkflowMode.ChooseSkill&&!WorkflowSnapshotMatches()){
                CancelSkillWorkflow(false);equipFeedback="Dữ liệu kỹ năng đã thay đổi. Vui lòng chọn lại.";
            }
            bool editing=skillWorkflowMode!=SkillWorkflowMode.View;
            bool learned=selectedLearnedIndex>=0&&selectedLearnedIndex<learnedSnapshot.Count;
            bool same=learned&&previewDestination>=0&&selectedSkillId==SlotId(previewDestination);
            int other=-1;if(learned&&editing)for(int i=0;i<equippedSnapshot.Count;i++)if(i!=previewDestination&&SlotId(i)==selectedSkillId){other=i;break;}
            workflowToggle.text=editing?"Hủy đổi":"Đổi kỹ năng";
            workflowToggle.SetEnabled(!equipSaving&&LoadoutProvider!=null&&ContextPet!=null&&activeEquippedSlotCount>0);
            workflowToggle.tooltip=LoadoutProvider==null?"Provider hiện tại chỉ hỗ trợ xem kỹ năng.":editing?"Hủy thay đổi chưa xác nhận.":"Chọn ô đang dùng, sau đó chọn kỹ năng thay thế.";
            string step=skillWorkflowMode==SkillWorkflowMode.ChooseSlot?"Bước 1/3 — Chọn ô đang trang bị cần đổi":
                skillWorkflowMode==SkillWorkflowMode.ChooseSkill?$"Bước 2/3 — Chọn kỹ năng thay thế cho Ô {previewDestination+1}":
                skillWorkflowMode==SkillWorkflowMode.Review?"Bước 3/3 — Kiểm tra thay đổi":"Chế độ Xem — Bấm một kỹ năng để xem chi tiết";
            if(helperLabel!=null)helperLabel.text=step;
            for(int i=0;i<equippedSlots.Count;i++){
                var card=equippedSlots[i];bool hadSelection=card.ClassListContains("is-selected");card.RemoveFromClassList("is-selected");
                SkillWorkflowFeedback.Set(card,editing&&(i==previewDestination||i==other)?1:!editing&&i==inspectedEquippedSlot?3:0);
                if(hadSelection&&card is AngularButton angular)angular.Surface.InvalidateFeedback();
            }
            foreach(var card in learnedCards){
                bool hadSelection=card.ClassListContains("is-selected");card.RemoveFromClassList("is-selected");bool chosen=card.userData is int index&&index==selectedLearnedIndex;
                card.SetEnabled(!equipSaving&&skillWorkflowMode!=SkillWorkflowMode.ChooseSlot);
                SkillWorkflowFeedback.Set(card,chosen?(editing?2:3):0);
                if(hadSelection&&card is AngularButton angular)angular.Surface.InvalidateFeedback();
            }
            if(learned)ShowTooltip(dialog,learnedSnapshot[selectedLearnedIndex]);
            else if(inspectedEquippedSlot>=0&&inspectedEquippedSlot<equippedSnapshot.Count&&equippedSnapshot[inspectedEquippedSlot].unlocked)ShowTooltip(dialog,equippedSnapshot[inspectedEquippedSlot]);
            else{
                ResetEmbeddedDetails();tooltipName.text=editing?"Chọn ô cần đổi":"Chi tiết kỹ năng";
                tooltipDescription.text=editing?"Chọn một ô ở hàng Đang trang bị. Chỉ xác nhận mới thay đổi kỹ năng.":"Bấm ô đang trang bị hoặc kỹ năng trong kho để xem. Bấm Đổi kỹ năng khi muốn thay.";
            }
            workflowPreview.style.display=editing&&learned?DisplayStyle.Flex:DisplayStyle.None;
            foreach(var row in workflowRows)row.style.display=DisplayStyle.None;
            if(editing&&learned&&previewDestination>=0){
                var old=previewDestination<equippedSnapshot.Count?equippedSnapshot[previewDestination]:default;
                workflowRows[0].Set(previewDestination,old,learnedSnapshot[selectedLearnedIndex]);
                if(other>=0)workflowRows[1].Set(other,equippedSnapshot[other],old);
            }
            bool hidden=learned&&!MatchesFilter(EffectiveElement(learnedSnapshot[selectedLearnedIndex]));
            equipPreview.text=equipFeedback??(same?$"Kỹ năng này đã ở Ô {previewDestination+1}.":hidden?"Kỹ năng đã chọn đang ngoài bộ lọc.":string.Empty);
            equipPreview.style.display=string.IsNullOrEmpty(equipPreview.text)?DisplayStyle.None:DisplayStyle.Flex;
            equipConfirm.style.display=editing?DisplayStyle.Flex:DisplayStyle.None;
            equipConfirm.SetEnabled(skillWorkflowMode==SkillWorkflowMode.Review&&learned&&!same&&!equipSaving&&previewPet!=null&&previewPet==ContextPet);
            tooltip.style.display=DisplayStyle.Flex;
        }
        void ConfirmSkillWorkflow(){
            if(equipSaving||skillWorkflowMode!=SkillWorkflowMode.Review||previewPet==null||previewPet!=ContextPet||LoadoutProvider==null)return;
            var fresh=LoadoutProvider.SkillsFor(previewPet);bool valid=LoadoutProvider.ContainsPet(previewPet)&&LoadoutProvider.SlotCountFor(previewPet)==workflowSlotCount;
            for(int i=0;i<4;i++){string id=i<fresh.Count?fresh[i].skillId??string.Empty:string.Empty;if(id!=workflowSnapshot[i])valid=false;}
            var learned=LoadoutProvider.LearnedFor(previewPet);int index=-1;for(int i=0;i<learned.Count;i++)if(learned[i].unlocked&&learned[i].skillId==selectedSkillId){index=i;break;}
            if(!valid||index<0||previewDestination<0||selectedSkillId==destinationSkillId){CancelSkillWorkflow(false);equipFeedback="Dữ liệu kỹ năng đã thay đổi. Vui lòng chọn lại.";RefreshSkillWorkflow();return;}
            int destination=previewDestination;equipSaving=true;RefreshSkillWorkflow();
            try{
                if(LoadoutProvider.EquipFor(previewPet,index,destination)){
                    CancelSkillWorkflow(false);inspectedEquippedSlot=destination;Refresh();equipFeedback="Đã đổi kỹ năng và lưu";
                }else equipFeedback="Lưu thất bại; dữ liệu đã được hoàn tác. Bạn có thể thử lại.";
            }finally{equipSaving=false;RefreshSkillWorkflow();}
        }
    }
    sealed class SkillWorkflowPreviewRow:VisualElement {
        readonly Label slot,oldName,newName,oldFallback,newFallback;
        readonly VisualElement oldIcon,newIcon;
        AngularSurface oldSurface,newSurface;
        public SkillWorkflowPreviewRow(){
            AddToClassList("skill-workflow-preview-row");pickingMode=PickingMode.Ignore;
            slot=new Label{pickingMode=PickingMode.Ignore};slot.AddToClassList("workflow-preview-slot");Add(slot);
            oldIcon=Icon(out oldFallback);Add(oldIcon);oldName=Name();Add(oldName);
            var arrow=new Label("→"){pickingMode=PickingMode.Ignore};arrow.AddToClassList("workflow-preview-arrow");Add(arrow);
            newIcon=Icon(out newFallback);Add(newIcon);newName=Name();Add(newName);
        }
        static VisualElement Icon(out Label fallback){var icon=new VisualElement{pickingMode=PickingMode.Ignore};icon.AddToClassList("workflow-preview-icon");fallback=new Label{pickingMode=PickingMode.Ignore};icon.Add(fallback);return icon;}
        static Label Name(){var label=new Label{pickingMode=PickingMode.Ignore};label.AddToClassList("workflow-preview-name");return label;}
        public void Set(int index,SkillHudData from,SkillHudData to){style.display=DisplayStyle.Flex;slot.text="Ô "+(index+1);oldName.text=string.IsNullOrEmpty(from.displayName)?"Trống":VietnameseUI.Skill(from.displayName);newName.text=string.IsNullOrEmpty(to.displayName)?"Trống":VietnameseUI.Skill(to.displayName);oldName.tooltip=oldName.text;newName.tooltip=newName.text;CompanionArt.SetSkillImage(oldIcon,oldFallback,from.icon);CompanionArt.SetSkillImage(newIcon,newFallback,to.icon);
            oldSurface??=oldIcon.Q<AngularSurface>("latest-skill-surface");newSurface??=newIcon.Q<AngularSurface>("latest-skill-surface");
            if(oldSurface!=null&&oldSurface.ArtInset!=3){oldSurface.ArtInset=3;oldSurface.MarkDirtyRepaint();}if(newSurface!=null&&newSurface.ArtInset!=3){newSurface.ArtInset=3;newSurface.MarkDirtyRepaint();}
        }
    }
}
