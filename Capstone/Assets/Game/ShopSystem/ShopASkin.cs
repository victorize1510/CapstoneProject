using System.Runtime.CompilerServices;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.ShopSystem {
    public static class ShopASkin {
        sealed class Pair {public ShopASurface fill,feedback;}
        static readonly ConditionalWeakTable<VisualElement,Pair> faces=new ConditionalWeakTable<VisualElement,Pair>();
        public static void Attach(VisualElement root){if(root==null||root.ClassListContains("shop-a"))return;root.AddToClassList("shop-a");
            root.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            foreach(var old in root.Query<UiReadingSurface>().ToList())old.RemoveFromHierarchy();foreach(var old in root.Query<UiSectionFrame>().ToList())old.RemoveFromHierarchy();
            foreach(var label in root.Query<Label>().ToList())label.pickingMode=PickingMode.Ignore;
            Face(root.Q(className:"shop-sidebar"),"A-sidebar");Face(root.Q(className:"shop-products"),"A-products");Face(root.Q(className:"shop-details"),"A-details");Face(root.Q(className:"shop-portrait-frame"),"A-portrait");
            Face(root.Q("shop-search-group"),"A-search");Face(root.Q("shop-sort"),"sort-field");Face(root.Q("shop-quantity"),"quantity-face");
            string[] icons={"heal","capture","material","favorite","evolution"};var cats=root.Query<Button>(className:"shop-category").ToList();for(int i=0;i<cats.Count;i++){Face(cats[i],"A-category");var icon=cats[i].Q<Label>(className:"shop-category-icon");icon.text="";Glyph(icon,icons[i]);icon.style.unityBackgroundImageTintColor=GreenAngularGeometry.Ink;}
            Face(root.Q("shop-buy"),"A-buy");Face(root.Q("shop-minus"),"icon-button");Face(root.Q("shop-plus"),"icon-button");Face(root.Q("shop-max"),"max-button");
            Utility(root.Q<Button>("shop-clear"),"close");Utility(root.Q<Button>("shop-plus"),"plus");Utility(root.Q<Button>("shop-minus"),"minus");
            var currency=root.Q(className:"inventory-currency");Face(currency,"quantity-face");if(currency!=null&&faces.TryGetValue(currency,out var currencyFace))currencyFace.fill.FillKind="A-category";
            var back=root.Q<Button>("shop-back");if(back is ShopAButton button)button.Kind="A-buy";
            var mark=root.Q(className:"screen-heading-mark");if(mark!=null){mark.style.rotate=new Rotate(0);mark.style.backgroundColor=Color.clear;mark.style.borderLeftWidth=mark.style.borderTopWidth=mark.style.borderRightWidth=mark.style.borderBottomWidth=0;}
            UnifiedPanelHeader.Attach(root,root,root.Q<Label>(className:"screen-title"),mark,back);
            var heading=root.Q(className:"screen-heading");if(heading!=null)heading.style.display=DisplayStyle.None;
            Glyph(root.Q("shop-search-glyph"),"search");
            foreach(var label in root.Query<Label>(className:"shop-item-name").ToList())label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            RuntimeScrollbars.Bind(root);InteractionFeedback.Bind(root);
            var list=root.Q(className:"shop-sidebar");list?.AddToClassList("runtime-choice-list");if(list!=null)RuntimeChoiceStyle.Sheet(list);
            void Fit(){var parent=root.parent;if(parent==null)return;float w=parent.contentRect.width,h=parent.contentRect.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0)return;float s=w/1600;
                if(Mathf.Abs(root.resolvedStyle.width-1600)>.1f)root.style.width=1600;float logicalHeight=h/s;if(Mathf.Abs(root.resolvedStyle.height-logicalHeight)>.1f)root.style.height=logicalHeight;
                root.style.scale=new Scale(new Vector3(s,s,1));}
            VisualElement watchedParent=null;EventCallback<GeometryChangedEvent> resized=_=>Fit();
            void Watch(){if(watchedParent!=null)watchedParent.UnregisterCallback(resized);watchedParent=root.parent;watchedParent?.RegisterCallback(resized);Fit();}
            root.RegisterCallback<GeometryChangedEvent>(resized);root.RegisterCallback<AttachToPanelEvent>(_=>Watch());
            root.RegisterCallback<DetachFromPanelEvent>(_=>{watchedParent?.UnregisterCallback(resized);watchedParent=null;});Watch();
        }
        static void Glyph(VisualElement e,string name){if(e==null)return;e.style.backgroundImage=new StyleBackground(ShopAArt.Texture("Icons/"+name));e.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);e.style.unityBackgroundImageTintColor=Color.white;e.pickingMode=PickingMode.Ignore;}
        static void Utility(Button b,string name){if(b==null)return;b.text="";var icon=new VisualElement{pickingMode=PickingMode.Ignore};icon.AddToClassList("shop-action-glyph");Glyph(icon,name);b.Add(icon);}
        public static void Face(VisualElement host,string kind){if(host==null||faces.TryGetValue(host,out _))return;
            if(host is Button choice&&host.ClassListContains("shop-category")){
                if(choice is ShopAButton plain)plain.PlainChoice=true;
                choice.RemoveFromClassList("shop-a-button");RuntimeChoiceStyle.Row(choice);
                choice.style.marginBottom=4;
                foreach(var label in choice.Query<Label>().ToList()){label.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);label.style.fontSize=RuntimeChoiceStyle.TextSize;label.style.unityFontStyleAndWeight=FontStyle.Bold;label.style.color=GreenAngularGeometry.Ink;label.style.unityTextAlign=TextAnchor.MiddleLeft;}
                var count=choice.Q<Label>(className:"shop-category-count");if(count!=null)count.style.unityTextAlign=TextAnchor.MiddleRight;
                return;
            }
            var pair=new Pair{fill=new ShopASurface(kind){Owner=host}};faces.Add(host,pair);
            host.style.backgroundColor=Color.clear;host.style.backgroundImage=StyleKeyword.None;host.style.borderTopWidth=host.style.borderBottomWidth=host.style.borderLeftWidth=host.style.borderRightWidth=0;
            host.hierarchy.Insert(0,pair.fill);
            if(host is ShopAButton b)b.Kind=kind;
            if(host is Button||host is BaseField<string>||host is IntegerField||host.name=="shop-search-group"){pair.feedback=new ShopASurface(kind,true){Owner=host,Background=pair.fill};pair.fill.FeedbackSource=pair.feedback;host.hierarchy.Add(pair.feedback);
                host.RegisterCallback<PointerEnterEvent>(_=>{pair.feedback.Hovered=true;pair.feedback.Animate();});host.RegisterCallback<PointerLeaveEvent>(_=>{pair.feedback.Hovered=pair.feedback.Pressed=false;pair.feedback.Animate();});
                host.RegisterCallback<PointerDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=false;pair.feedback.Focused=false;pair.feedback.Pressed=true;pair.feedback.Animate();},TrickleDown.TrickleDown);host.RegisterCallback<PointerUpEvent>(_=>{pair.feedback.Pressed=false;pair.feedback.Animate();});
                host.RegisterCallback<KeyDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=true;pair.feedback.Focused=true;pair.feedback.MarkDirtyRepaint();},TrickleDown.TrickleDown);host.RegisterCallback<FocusInEvent>(_=>{pair.feedback.Focused=InteractionFeedback.KeyboardNavigation;pair.feedback.MarkDirtyRepaint();});host.RegisterCallback<FocusOutEvent>(_=>{pair.feedback.Focused=false;pair.feedback.MarkDirtyRepaint();});}
        }
        public static void Refresh(VisualElement host){if(host!=null&&faces.TryGetValue(host,out var p)){p.fill.MarkDirtyRepaint();p.feedback?.MarkDirtyRepaint();}}
        public static void Suspend(VisualElement root){if(root!=null)foreach(var s in root.Query<ShopASurface>().ToList())s.Stop();}
    }
}
