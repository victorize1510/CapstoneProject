using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>One plain runtime dropdown presentation. Existing fields still own choices, value and callbacks.</summary>
    public static class RuntimeChoiceMenus {
        sealed class Binding {
            public DropdownField Field;
            public VisualElement PanelRoot;
            public EventCallback<PointerDownEvent> Pointer;
            public EventCallback<NavigationSubmitEvent> Submit;
            public EventCallback<DetachFromPanelEvent> Detach;
            public EventCallback<GeometryChangedEvent> Geometry;
            public int OpenedFrame=-1;
        }
        sealed class Popup { public VisualElement Overlay,View;public DropdownField Owner; }
        static readonly ConditionalWeakTable<DropdownField,Binding> bindings=new ConditionalWeakTable<DropdownField,Binding>();
        static readonly ConditionalWeakTable<VisualElement,Popup> popups=new ConditionalWeakTable<VisualElement,Popup>();
        static int consumedFrame=-1;
        public static void BindAll(VisualElement root){if(root==null)return;
            foreach(var field in root.Query<DropdownField>().ToList())Bind(field);
            if(root.ClassListContains("runtime-choice-bound"))return;root.AddToClassList("runtime-choice-bound");
            root.RegisterCallback<AttachToPanelEvent>(e=>{if(e.target is DropdownField field)Bind(field);},TrickleDown.TrickleDown);
            root.RegisterCallback<DetachFromPanelEvent>(_=>Close(root,false));
        }
        public static void Bind(DropdownField field){if(field==null||bindings.TryGetValue(field,out _))return;
            var state=new Binding{Field=field};bindings.Add(field,state);
            state.Pointer=e=>{if(e.button!=0)return;e.StopImmediatePropagation();Open(field);};
            state.Submit=e=>{e.StopImmediatePropagation();Open(field);};state.Detach=_=>CloseFor(field,false);
            state.Geometry=_=>{if(field.layout.width<1||field.layout.height<1)CloseFor(field,false);};
            field.RegisterCallback(state.Pointer,TrickleDown.TrickleDown);field.RegisterCallback(state.Submit,TrickleDown.TrickleDown);field.RegisterCallback(state.Detach);field.RegisterCallback(state.Geometry);
        }
        public static void Unbind(DropdownField field){if(field==null||!bindings.TryGetValue(field,out var state))return;CloseFor(field,false);
            field.UnregisterCallback(state.Pointer,TrickleDown.TrickleDown);field.UnregisterCallback(state.Submit,TrickleDown.TrickleDown);field.UnregisterCallback(state.Detach);field.UnregisterCallback(state.Geometry);bindings.Remove(field);
        }
        public static bool OwnsKey(VisualElement root)=>consumedFrame==Time.frameCount||(root?.panel?.visualTree is VisualElement p&&popups.TryGetValue(p,out var state)&&state.Overlay!=null);
        public static void CloseFor(DropdownField field,bool focus=true){if(field==null)return;bindings.TryGetValue(field,out var binding);var root=field.panel?.visualTree??binding?.PanelRoot;if(root!=null&&popups.TryGetValue(root,out var p)&&ReferenceEquals(p.Owner,field))Close(p,focus);}
        public static void Close(VisualElement root,bool focus=true){var panel=root?.panel?.visualTree;if(panel!=null&&popups.TryGetValue(panel,out var p))Close(p,focus);}
        static void Close(Popup state,bool focus){if(state.Overlay==null)return;consumedFrame=Time.frameCount;var owner=state.Owner;
            PanelFocusUtility.Close(state.View);state.Overlay.RemoveFromHierarchy();state.Overlay=state.View=null;state.Owner=null;if(focus&&owner?.panel!=null)owner.Focus();
            if(owner!=null&&bindings.TryGetValue(owner,out var binding))binding.PanelRoot=null;
        }
        static bool Visible(VisualElement field){for(var p=field;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None||p.resolvedStyle.visibility==Visibility.Hidden)return false;return true;}
        public static void Open(DropdownField field){if(field?.panel==null||!field.enabledInHierarchy||field.choices.Count==0||!Visible(field))return;Bind(field);
            var binding=bindings.GetValue(field,_=>throw new InvalidOperationException());if(binding.OpenedFrame==Time.frameCount)return;binding.OpenedFrame=Time.frameCount;
            var root=field.panel.visualTree;var state=popups.GetValue(root,_=>new Popup());Close(state,false);state.Owner=field;binding.PanelRoot=root;consumedFrame=Time.frameCount;
            var overlay=new VisualElement{name="runtime-choice-overlay"};overlay.style.position=Position.Absolute;overlay.style.left=overlay.style.right=overlay.style.top=overlay.style.bottom=0;state.Overlay=overlay;
            string prefix=field.name=="shop-sort"?"shop-sort":field.name.StartsWith("inventory-")?"bag-choice":"runtime-choice";
            var view=new VisualElement{name=prefix+"-popup"};view.AddToClassList("unity-base-dropdown__container-outer");RuntimeChoiceStyle.Panel(view);state.View=view;
            var input=field.Q(className:"unity-base-field__input")??field;float scale=Mathf.Max(.1f,root.layout.width/1600f);var anchor=root.WorldToLocal(input.worldBound.position);
            float width=Mathf.Min(Mathf.Max(260,input.worldBound.width/scale),(root.layout.width-24*scale)/scale);
            float below=root.layout.height-anchor.y-input.worldBound.height-20*scale,above=anchor.y-20*scale;
            float maxHeight=Mathf.Min(360,Mathf.Max(below,above)/scale),height=Mathf.Min(field.choices.Count*46+12,Mathf.Max(60,maxHeight));
            float top=below>=height*scale?anchor.y+input.worldBound.height+8*scale:anchor.y-height*scale-8*scale;
            view.style.position=Position.Absolute;view.style.left=Mathf.Clamp(anchor.x,12*scale,Mathf.Max(12*scale,root.layout.width-width*scale-12*scale));view.style.top=Mathf.Max(12*scale,top);
            view.style.width=width;view.style.height=height;view.style.minWidth=0;view.style.maxWidth=view.style.maxHeight=StyleKeyword.None;view.style.transformOrigin=new TransformOrigin(0,0,0);view.style.scale=new Scale(new Vector3(scale,scale,1));
            view.style.paddingLeft=view.style.paddingRight=view.style.paddingTop=view.style.paddingBottom=6;
            var scroll=RuntimeScrollbars.Create();scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;scroll.style.flexGrow=1;scroll.style.minHeight=0;view.Add(scroll);
            var options=field.choices.ToArray();var buttons=new Button[options.Length];int focused=Mathf.Max(0,field.choices.IndexOf(field.value));
            void Focus(int i){focused=(i+buttons.Length)%buttons.Length;InteractionFeedback.KeyboardNavigation=true;buttons[focused].Focus();scroll.ScrollTo(buttons[focused]);}
            void Commit(int i){string value=options[i];bool valid=field.enabledInHierarchy&&field.choices.Contains(value);Close(state,true);if(valid)field.value=value;}
            for(int i=0;i<buttons.Length;i++){int index=i;var row=new Button(()=>Commit(index)){name=prefix+"-"+(prefix=="shop-sort"?"choice-":"")+i,text=options[i],tooltip=options[i]};RuntimeChoiceStyle.Row(row);row.style.marginBottom=2;row.EnableInClassList("is-selected",options[i]==field.value);scroll.Add(row);buttons[i]=row;}
            view.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode==KeyCode.Escape){Close(state,true);e.StopImmediatePropagation();}
                else if(e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.DownArrow||e.keyCode==KeyCode.Tab){Focus(focused+(e.keyCode==KeyCode.UpArrow||e.shiftKey?-1:1));e.StopImmediatePropagation();}
                else if(e.keyCode==KeyCode.Home||e.keyCode==KeyCode.End){Focus(e.keyCode==KeyCode.Home?0:buttons.Length-1);e.StopImmediatePropagation();}
                else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter||e.keyCode==KeyCode.Space){Commit(focused);e.StopImmediatePropagation();}},TrickleDown.TrickleDown);
            overlay.RegisterCallback<NavigationCancelEvent>(e=>{Close(state,true);e.StopImmediatePropagation();});
            overlay.RegisterCallback<PointerDownEvent>(e=>{if(ReferenceEquals(e.target,overlay)){Close(state,true);e.StopImmediatePropagation();}});
            overlay.Add(view);root.Add(overlay);overlay.BringToFront();PanelFocusUtility.Open(view);view.schedule.Execute(()=>{if(state.View==view)Focus(focused);});
        }
    }

    public static class RuntimeChoiceStyle {
        public const float TextSize=18,RowHeight=44,TileWidth=128,Gap=8;
        public static readonly Color PanelColor=new Color32(185,194,210,255),TextColor=GreenAngularGeometry.Ink;
        static StyleSheet sheet;
        public static void Sheet(VisualElement host){sheet??=Resources.Load<StyleSheet>("UI/RuntimeChoices20261104/Choices");if(sheet!=null&&!host.styleSheets.Contains(sheet))host.styleSheets.Add(sheet);}
        public static void Panel(VisualElement host){if(host==null)return;Sheet(host);host.AddToClassList("runtime-choice-panel");host.style.backgroundImage=StyleKeyword.None;host.style.backgroundColor=PanelColor;
            host.style.borderLeftWidth=host.style.borderRightWidth=host.style.borderTopWidth=host.style.borderBottomWidth=1;host.style.borderLeftColor=host.style.borderRightColor=host.style.borderTopColor=host.style.borderBottomColor=GreenAngularGeometry.Edge;
            host.style.borderTopLeftRadius=host.style.borderTopRightRadius=host.style.borderBottomLeftRadius=host.style.borderBottomRightRadius=6;host.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);}
        public static void Row(Button row,float scale=1,Label caption=null,bool tile=false){if(row==null)return;Sheet(row);row.AddToClassList("runtime-choice-row");row.EnableInClassList("runtime-choice-tile",tile);
            row.style.marginLeft=row.style.marginRight=row.style.marginTop=row.style.marginBottom=0;row.style.minWidth=0;row.style.flexShrink=0;row.style.height=row.style.minHeight=RowHeight*scale;
            row.style.paddingLeft=row.style.paddingRight=12*scale;row.style.paddingTop=row.style.paddingBottom=0;row.style.borderLeftWidth=row.style.borderRightWidth=row.style.borderTopWidth=row.style.borderBottomWidth=tile?1:0;
            row.style.borderTopLeftRadius=row.style.borderTopRightRadius=row.style.borderBottomLeftRadius=row.style.borderBottomRightRadius=4*scale;row.style.fontSize=TextSize*scale;row.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);row.style.unityFontStyleAndWeight=FontStyle.Bold;
            row.style.unityTextAlign=tile?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;row.style.whiteSpace=WhiteSpace.NoWrap;row.style.textOverflow=TextOverflow.Ellipsis;row.style.overflow=Overflow.Hidden;row.style.opacity=1;
            if(caption!=null){caption.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);caption.style.fontSize=TextSize*scale;caption.style.unityFontStyleAndWeight=FontStyle.Bold;caption.style.color=TextColor;caption.style.unityTextAlign=tile?TextAnchor.MiddleCenter:TextAnchor.MiddleLeft;caption.style.whiteSpace=WhiteSpace.NoWrap;caption.style.textOverflow=TextOverflow.Ellipsis;caption.pickingMode=PickingMode.Ignore;if(string.IsNullOrEmpty(row.tooltip))row.tooltip=caption.text;}
        }
        public static void Category(Button row){
            Row(row);row.AddToClassList("runtime-choice-solid");row.AddToClassList("runtime-choice-list");
            row.style.backgroundColor=StyleKeyword.Null;row.style.backgroundImage=StyleKeyword.None;
            foreach(var label in row.Query<Label>().ToList()){label.style.fontSize=TextSize;label.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);label.style.unityFontStyleAndWeight=FontStyle.Bold;label.style.color=TextColor;label.pickingMode=PickingMode.Ignore;}
        }
        public static float LocalScale(VisualElement host){float global=(host.panel?.visualTree.layout.width??1600)/1600f;float transform=host.layout.width>0?Mathf.Abs(host.worldBound.width/host.layout.width):1;return Mathf.Max(.1f,global/Mathf.Max(.1f,transform));}
        public static void UguiRow(UnityEngine.UI.Button button,TMPro.TMP_Text label,float scale){
            if(button.targetGraphic is UnityEngine.UI.Image image){image.sprite=null;image.color=Color.white;image.raycastTarget=true;}
            button.transition=UnityEngine.UI.Selectable.Transition.ColorTint;var c=button.colors;c.normalColor=PanelColor;c.highlightedColor=new Color32(210,216,227,255);c.selectedColor=GreenAngularGeometry.Lavender;c.pressedColor=new Color32(135,150,174,255);c.disabledColor=new Color32(168,177,192,255);c.colorMultiplier=1;c.fadeDuration=.14f;button.colors=c;
            label.fontSize=TextSize*scale;label.fontStyle=TMPro.FontStyles.Bold;label.enableAutoSizing=false;label.color=TextColor;label.raycastTarget=false;
        }
        public static void FitList(VisualElement popup,VisualElement anchor){if(popup==null||anchor?.panel==null||popup.parent==null)return;float s=LocalScale(popup.parent);Panel(popup);
            var rows=popup.Query<Button>().ToList();foreach(var row in rows)Row(row,s,row.Q<Label>(className:"box-button-caption"));float w=Mathf.Min(300*s,popup.parent.layout.width-24*s),h=Mathf.Min(320*s,rows.Count*46*s+16*s);PlacePopover(popup,anchor,w,h,s);popup.style.paddingLeft=popup.style.paddingRight=popup.style.paddingTop=popup.style.paddingBottom=8*s;
        }
        internal static void PlacePopover(VisualElement popup,VisualElement anchor,float width,float height,float scale){var parent=popup.parent;var p=parent.WorldToLocal(anchor.worldBound.position);float worldScale=anchor.layout.height>0?anchor.worldBound.height/anchor.layout.height:1;float parentScale=parent.layout.width>0?parent.worldBound.width/parent.layout.width:1;float ah=anchor.layout.height*worldScale/Mathf.Max(.1f,parentScale);
            popup.style.position=Position.Absolute;popup.style.right=popup.style.bottom=StyleKeyword.Auto;popup.style.left=Mathf.Clamp(p.x,12*scale,Mathf.Max(12*scale,parent.layout.width-width-12*scale));popup.style.top=p.y+ah+8*scale;popup.style.width=width;popup.style.height=height;popup.style.marginTop=popup.style.marginRight=popup.style.marginLeft=popup.style.marginBottom=0;
        }
    }
}
