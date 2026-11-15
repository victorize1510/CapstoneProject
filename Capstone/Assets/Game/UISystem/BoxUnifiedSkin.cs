using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Presentation adapter for the existing responsive BOX tree; no storage or selection ownership.</summary>
    public static class BoxUnifiedSkin {
        sealed class Faces {public BoxUnifiedSurface fill,feedback;}
        static readonly ConditionalWeakTable<VisualElement,Faces> faces=new ConditionalWeakTable<VisualElement,Faces>();
        public static void Attach(VisualElement root) {
            if(root==null||root.ClassListContains("box-unified"))return;
            root.AddToClassList("box-unified");
            var sheet=Resources.Load<StyleSheet>(BoxUnifiedArt.Root+"BoxUnified");if(sheet!=null)root.styleSheets.Add(sheet);
            foreach(var old in root.Query<UiSectionFrame>().ToList())old.RemoveFromHierarchy();
            foreach(var old in root.Query<UiReadingSurface>().ToList())old.RemoveFromHierarchy();
            root.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            foreach(var label in root.Query<Label>().ToList()){label.pickingMode=PickingMode.Ignore;label.style.color=GreenAngularGeometry.Ink;}
            foreach(var label in root.Query<Label>(className:"box-selected-name").ToList())label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            foreach(var label in root.Query<Label>(className:"box-purchase-title").ToList())label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            Group(root,"box-party-section","party-section");Group(root,"box-storage-panel","storage-panel");Group(root,"box-selected-panel","inspector");
            Group(root,"box-meta-item","metadata-card");Group(root,"box-nature-card","personality-card");Group(root,"box-health-card","health-exp-card");
            Group(root,"box-footer","footer");Group(root,"box-purchase-card","purchase-popup");
            Face(root.Q("box-filter-panel"),"filter-popup");Face(root.Q("box-sort-popup"),"sort-popup");
            Face(root.Q("box-selected-portrait"),"portrait-frame");Face(root.Q("box-search-field"),"input");
            foreach(var button in root.Query<Button>().ToList()) {
                if(button.name=="box-back-button")continue;
                string kind=button.ClassListContains("box-primary-action")||button.name=="box-purchase-confirm"?"action":
                    button.ClassListContains("box-page-button")?"page-button":button.name=="box-expand-button"?"expand-button":
                    button.ClassListContains("box-filter-chip")||button.name=="box-filter-toggle"?"filter-button":
                    button.name=="box-sort-button"?"sort-button":button.name=="box-search-field-clear"?"page-button":"secondary-action";
                Face(button,kind);
            }
            Utility(root,"box-page-previous","chevronLeft",true);Utility(root,"box-page-next","chevron",true);
            Utility(root,"box-search-field-clear","close",true);Utility(root,"box-expand-button","plus",true);
            Utility(root,"box-filter-toggle","filter");Utility(root,"box-sort-button","sort");
            Utility(root,"box-add-button","plus");Utility(root,"box-move-button","storage");Utility(root,"box-replace-button","swap");Utility(root,"box-details-button","book");
            var search=root.Q<TextField>("box-search-field");if(search!=null){var icon=Icon("search",GreenAngularGeometry.Ink);icon.AddToClassList("box-search-glyph");search.Add(icon);}
            foreach(var label in root.Query<Label>(className:"box-storage-mark").ToList())LabelIcon(label,"storage");
            foreach(var label in root.Query<Label>(className:"box-selected-element-icon").ToList())LabelIcon(label,"companion");
            LabelIcon(root.Q<Label>("box-selected-favorite"),"favorite");
            foreach(var label in root.Query<Label>(className:"box-feedback").ToList())label.style.color=GreenAngularGeometry.Light;
            foreach(var label in root.Query<Label>(className:"box-footer-summary").ToList())label.style.color=GreenAngularGeometry.Light;
            foreach(var track in root.Query<VisualElement>(className:"box-health-track").ToList())Bar(track,"hp-track");
            Bar(root.Q("box-selected-health-fill"),"hp-fill");Bar(root.Q("box-selected-exp-fill"),"exp-fill");
            RuntimeScrollbars.Bind(root);RuntimeChoiceStyle.Sheet(root);
            RuntimeChoiceGrid.Attach(root.Q("box-filter-panel"),root.Q("box-filter-toggle"));Header(root);
        }
        static void Bar(VisualElement target,string key) {
            if(target==null)return;target.style.backgroundColor=Color.clear;target.style.backgroundImage=new StyleBackground(BoxUnifiedArt.Texture("Components/"+key));
            target.style.unityBackgroundImageTintColor=Color.white;
        }
        static void Header(VisualElement root) {
            var header=root.Q(className:"box-header");if(header==null)return;
            var canvas=new VisualElement{name="box-header-canvas",pickingMode=PickingMode.Ignore};canvas.style.position=Position.Absolute;
            canvas.style.width=1600;canvas.style.height=104;canvas.style.transformOrigin=new TransformOrigin(0,0,0);header.Add(canvas);
            var emblem=root.Q(className:"box-title-mark");if(emblem!=null){emblem.style.rotate=new Rotate(0);emblem.style.backgroundColor=Color.clear;emblem.style.borderTopWidth=emblem.style.borderBottomWidth=emblem.style.borderLeftWidth=emblem.style.borderRightWidth=0;}
            UnifiedPanelHeader.Attach(root,canvas,root.Q<Label>(className:"box-title"),emblem,root.Q<Button>("box-back-button"));
            void Fit(){float s=Mathf.Min(root.layout.width/1600,root.layout.height/900);if(s<=0||float.IsNaN(s))return;
                canvas.style.scale=new Scale(new Vector3(s,s,1));canvas.style.left=(root.layout.width-1600*s)/2;canvas.style.top=0;header.style.height=header.style.minHeight=104*s;}
            root.RegisterCallback<GeometryChangedEvent>(_=>Fit());Fit();
        }
        static void Group(VisualElement root,string cls,string kind){foreach(var host in root.Query<VisualElement>(className:cls).ToList())Face(host,kind);}
        public static void Face(VisualElement host,string kind) {
            if(host==null||faces.TryGetValue(host,out _))return;
            if(kind=="filter-popup"||kind=="sort-popup"){RuntimeChoiceStyle.Panel(host);return;}
            if(host is Button choice&&(host.ClassListContains("box-filter-chip")||host.ClassListContains("box-sort-option"))){
                if(choice is BoxUnifiedButton plain)plain.PlainChoice=true;
                RuntimeChoiceStyle.Row(choice,1,choice.Q<Label>(className:"box-button-caption"),host.ClassListContains("box-filter-chip"));return;
            }
            var state=new Faces{fill=new BoxUnifiedSurface(kind){name="box-unified-fill",Owner=host}};faces.Add(host,state);
            host.style.backgroundImage=StyleKeyword.None;host.style.backgroundColor=Color.clear;
            host.style.borderTopColor=host.style.borderBottomColor=host.style.borderLeftColor=host.style.borderRightColor=Color.clear;
            host.Insert(0,state.fill);
            if(host is Button button){
                if(button is BoxUnifiedButton b)b.Kind=kind;
                state.feedback=new BoxUnifiedSurface(kind,true){name="box-unified-feedback",Owner=host};host.Add(state.feedback);
                state.feedback.Background=state.fill;state.fill.FeedbackSource=state.feedback;
                var caption=button.Q<Label>(className:"box-button-caption");if(caption!=null)caption.style.color=kind=="action"?GreenAngularGeometry.Light:GreenAngularGeometry.Ink;
                button.RegisterCallback<PointerEnterEvent>(_=>{state.feedback.Hovered=true;state.feedback.Animate();});
                button.RegisterCallback<PointerLeaveEvent>(_=>{state.feedback.Hovered=state.feedback.Pressed=false;state.feedback.Animate();});
                button.RegisterCallback<PointerDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=false;state.feedback.Focused=false;state.feedback.Pressed=true;state.feedback.Animate();},TrickleDown.TrickleDown);
                button.RegisterCallback<PointerUpEvent>(_=>{state.feedback.Pressed=false;state.feedback.Animate();});
                button.RegisterCallback<KeyDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=true;state.feedback.Focused=true;state.feedback.MarkDirtyRepaint();},TrickleDown.TrickleDown);
                button.RegisterCallback<FocusEvent>(_=>{state.feedback.Focused=InteractionFeedback.KeyboardNavigation;state.feedback.MarkDirtyRepaint();});
                button.RegisterCallback<BlurEvent>(_=>{state.feedback.Focused=false;state.feedback.MarkDirtyRepaint();});
            }
        }
        public static void RefreshCard(VisualElement card){if(card!=null&&faces.TryGetValue(card,out var pair)){pair.feedback?.MarkDirtyRepaint();pair.fill.MarkDirtyRepaint();}}
        public static void Card(Button card,bool party) {
            Face(card,party?"party-slot":"storage-slot");
            foreach(var label in card.Query<Label>().ToList())label.pickingMode=PickingMode.Ignore;
            foreach(var portrait in card.Query<VisualElement>(className:party?"box-party-portrait":"box-storage-portrait").ToList())portrait.pickingMode=PickingMode.Ignore;
            var favorite=card.Q<Label>(className:party?"box-favorite-badge":"box-storage-favorite");if(favorite!=null)LabelIcon(favorite,"favorite");
        }
        public static void SetPortrait(VisualElement host,Sprite sprite) {
            if(host==null)return;host.style.backgroundImage=StyleKeyword.None;host.pickingMode=PickingMode.Ignore;
            var art=host.Q<BoxUnifiedSurface>("box-portrait-art");
            if(art==null){art=new BoxUnifiedSurface("portrait-frame"){name="box-portrait-art",PortraitOnly=true};host.Insert(host.Q("box-unified-fill")!=null?1:0,art);}
            if(art.Portrait!=sprite){art.Portrait=sprite;art.MarkDirtyRepaint();}
        }
        public static void EmptyParty(Label icon) {
            if(icon==null)return;icon.text=string.Empty;icon.style.backgroundImage=new StyleBackground(BoxUnifiedArt.Texture("Icons/box-empty"));
            icon.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);icon.style.unityBackgroundImageTintColor=Color.white;
        }
        static VisualElement Icon(string key,Color tint) {
            var e=new VisualElement{pickingMode=PickingMode.Ignore};e.AddToClassList("box-utility-glyph");e.style.backgroundImage=new StyleBackground(BoxUnifiedArt.Texture("Icons/"+key));
            e.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);e.style.unityBackgroundImageTintColor=tint;return e;
        }
        static void LabelIcon(Label label,string key) {
            if(label==null)return;label.text=string.Empty;label.style.backgroundImage=new StyleBackground(BoxUnifiedArt.Texture("Icons/"+key));label.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);label.style.unityBackgroundImageTintColor=GreenAngularGeometry.Ink;
        }
        static void Utility(VisualElement root,string id,string key,bool iconOnly=false) {
            var button=root.Q<Button>(id);if(button==null)return;bool primary=button.ClassListContains("box-primary-action");
            var icon=Icon(key,primary?GreenAngularGeometry.Light:GreenAngularGeometry.Ink);button.Add(icon);
            if(iconOnly){button.AddToClassList("box-icon-only");var caption=button.Q<Label>(className:"box-button-caption");if(caption!=null)caption.style.visibility=Visibility.Hidden;}
            else button.AddToClassList("box-has-glyph");
            if(!iconOnly&&button.ClassListContains("box-action-button")){
                button.AddToClassList("box-centered-glyph");var caption=button.Q<Label>(className:"box-button-caption");
                if(caption!=null)button.Insert(button.IndexOf(caption),icon);
            }
        }
    }
}
