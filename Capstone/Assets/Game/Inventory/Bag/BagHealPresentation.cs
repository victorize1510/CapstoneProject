using System;
using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Bag {
    /// <summary>Temporarily presents the existing heal bindings in BAG. Restores the Companion tree on close.</summary>
    sealed class BagHealPresentation : IDisposable {
        readonly VisualElement overlay, original, card;
        readonly List<Action> restore = new List<Action>();
        readonly BagButton confirm;
        public BagHealPresentation(VisualElement overlay, Action cancel, Action heal) {
            this.overlay=overlay; original=overlay.Q("heal-modal-dialog");
            original.style.display=DisplayStyle.None;
            card=new VisualElement{name="bag-heal-dialog"};overlay.Add(card);
            BagPresentation.Modal(overlay,card,"popup-heal-reference",792,608);
            Surface("action-primary",12,12,768,56);
            Label("HỒI PHỤC",24,12,744,56,"bag-modal-title");
            Surface("detail-art",24,88,744,88);
            Move("heal-pet-portrait",32,96,68,68);
            Move("heal-pet-name",124,98,620,34,"bag-modal-item-name");
            Label("Thú đang xuất chiến",124,138,620,26);
            Surface("detail-art",24,200,744,108);
            Move("heal-resource-title",40,210,704,26);
            Move("heal-current-health",40,244,240,30);
            Move("heal-health-arrow",286,244,48,30);
            Move("heal-result-health",342,244,250,30);
            Move("heal-restored-amount",598,244,146,30);
            // The existing track owns percentage-sized current/recovery fills from PetHealPreview.
            var track=original.Q(className:"ga-heal-track");MoveElement(track,40,282,704,10);
            if(track!=null)foreach(var fill in track.Query<AngularSurface>().ToList()){
                if(ReferenceEquals(fill,track))continue;
                var position=fill.style.position;var left=fill.style.left;var top=fill.style.top;var height=fill.style.height;bool material=fill.Material,noBorder=fill.NoBorder;
                restore.Add(()=>{fill.style.position=position;fill.style.left=left;fill.style.top=top;fill.style.height=height;fill.Material=material;fill.NoBorder=noBorder;});
                fill.style.position=Position.Absolute;fill.style.left=fill.style.top=0;fill.style.height=10;fill.Material=false;fill.NoBorder=true;
            }
            Surface("action-primary",24,330,744,28);
            Label("VẬT PHẨM",32,330,728,28,"bag-modal-note");
            Surface("detail-art",24,370,744,88);
            Move("heal-item-icon",32,378,68,68);
            Move("heal-item-name",124,380,620,32,"bag-modal-item-name");
            Move("heal-item-effect",124,418,398,28);
            Move("heal-item-count",540,418,204,28);
            Surface("action-primary",24,474,744,36);
            Move("heal-feedback",40,474,712,36,"bag-modal-note");
            var close=new BagButton(cancel){text="×",Kind="action-primary",name="bag-heal-close"};BagPresentation.Place(close,724,20,40,40);card.Add(close);
            var back=new BagButton(cancel){text="Hủy",Kind="action-small",name="bag-heal-cancel"};BagPresentation.Place(back,24,536,356,48);card.Add(back);
            confirm=new BagButton(heal){text="Hồi phục",Kind="action-primary",name="bag-heal-confirm"};confirm.AddToClassList("bag-dark");BagPresentation.Place(confirm,404,536,364,48);card.Add(confirm);
        }
        void Surface(string kind,float x,float y,float w,float h){var e=new BagSurface{Kind=kind};BagPresentation.Place(e,x,y,w,h);card.Add(e);}
        void Label(string text,float x,float y,float w,float h,string cls=null){var e=new Label(text){pickingMode=PickingMode.Ignore};if(cls!=null)e.AddToClassList(cls);if(cls=="bag-modal-title")e.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);BagPresentation.Place(e,x,y,w,h);card.Add(e);}
        void Move(string id,float x,float y,float w,float h,string cls=null)=>MoveElement(original.Q(id),x,y,w,h,cls);
        void MoveElement(VisualElement e,float x,float y,float w,float h,string cls=null){
            if(e==null)return;
            var parent=e.parent;int index=parent.IndexOf(e);var s=e.style;
            var position=s.position;var left=s.left;var right=s.right;var top=s.top;var bottom=s.bottom;var width=s.width;var height=s.height;
            var ml=s.marginLeft;var mr=s.marginRight;var mt=s.marginTop;var mb=s.marginBottom;var pl=s.paddingLeft;var pr=s.paddingRight;var pt=s.paddingTop;var pb=s.paddingBottom;var shrink=s.flexShrink;
            var font=s.fontSize;var align=s.unityTextAlign;var wrap=s.whiteSpace;var overflow=s.overflow;var textOverflow=s.textOverflow;
            restore.Add(()=>{e.RemoveFromHierarchy();parent.Insert(Mathf.Min(index,parent.childCount),e);s.position=position;s.left=left;s.right=right;s.top=top;s.bottom=bottom;s.width=width;s.height=height;s.marginLeft=ml;s.marginRight=mr;s.marginTop=mt;s.marginBottom=mb;s.paddingLeft=pl;s.paddingRight=pr;s.paddingTop=pt;s.paddingBottom=pb;s.flexShrink=shrink;s.fontSize=font;s.unityTextAlign=align;s.whiteSpace=wrap;s.overflow=overflow;s.textOverflow=textOverflow;if(cls!=null)e.RemoveFromClassList(cls);});
            if(e is AngularSurface surface){bool material=surface.Material,noBorder=surface.NoBorder,noFill=surface.NoFill,crop=surface.ArtCrop;var skin=surface.SkinTexture;surface.Material=false;surface.NoBorder=true;surface.NoFill=true;surface.SkinTexture=null;surface.ArtCrop=false;restore.Add(()=>{surface.Material=material;surface.NoBorder=noBorder;surface.NoFill=noFill;surface.SkinTexture=skin;surface.ArtCrop=crop;surface.MarkDirtyRepaint();});}
            card.Add(e);BagPresentation.Place(e,x,y,w,h);e.style.right=e.style.bottom=StyleKeyword.Auto;
            if(cls!=null)e.AddToClassList(cls);
            e.style.fontSize=cls=="bag-modal-item-name"?26:18;e.style.unityTextAlign=TextAnchor.MiddleLeft;e.style.whiteSpace=WhiteSpace.NoWrap;e.style.overflow=Overflow.Hidden;e.style.textOverflow=TextOverflow.Ellipsis;
        }
        public void Refresh(bool canHeal){confirm.SetEnabled(canHeal);foreach(var e in card.Query<Label>().ToList())if(!string.IsNullOrEmpty(e.text))e.tooltip=e.text;}
        public void Dispose(){BagTooltip.HideAll(overlay);for(int i=restore.Count-1;i>=0;i--)restore[i]();restore.Clear();card.RemoveFromHierarchy();overlay.RemoveFromClassList("bag-modal");original.style.display=StyleKeyword.Null;}
    }
}
