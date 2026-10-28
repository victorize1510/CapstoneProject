using System;
using System.Runtime.CompilerServices;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Bag {
    /// <summary>Presentation for existing DropdownField choices/value; no reflection into Unity's internal menu factory.</summary>
    static class BagChoicePopup {
        sealed class State {public VisualElement Overlay;public DropdownField Owner;}
        static readonly ConditionalWeakTable<VisualElement,State> states=new ConditionalWeakTable<VisualElement,State>();
        static int consumedFrame=-1;
        public static bool OwnsKey(VisualElement documentRoot)=>consumedFrame==Time.frameCount || (documentRoot?.panel?.visualTree is VisualElement root && states.TryGetValue(root,out var state)&&state.Overlay!=null);
        public static void Close(VisualElement documentRoot){var root=documentRoot?.panel?.visualTree;if(root!=null&&states.TryGetValue(root,out var state))Close(state);}
        static void Close(State state){consumedFrame=Time.frameCount;var owner=state.Owner;state.Overlay?.RemoveFromHierarchy();state.Overlay=null;state.Owner=null;owner?.Focus();}
        public static void Bind(DropdownField field){
            if(field==null)return;
            field.RegisterCallback<PointerDownEvent>(e=>{if(e.button!=0)return;e.StopImmediatePropagation();Show(field);},TrickleDown.TrickleDown);
            field.RegisterCallback<NavigationSubmitEvent>(e=>{e.StopImmediatePropagation();Show(field);},TrickleDown.TrickleDown);
            field.RegisterCallback<DetachFromPanelEvent>(_=>Close(field));
        }
        static void Show(DropdownField field){
            var root=field.panel?.visualTree;if(root==null||!field.enabledInHierarchy||field.choices.Count==0)return;
            var state=states.GetValue(root,_=>new State());if(state.Overlay!=null)Close(state);
            state.Owner=field;consumedFrame=Time.frameCount;
            var overlay=new VisualElement{name="bag-choice-overlay"};BagPresentation.Stretch(overlay);state.Overlay=overlay;
            var sheet=Resources.Load<StyleSheet>("UI/BAG20261022/Bag");if(sheet!=null)overlay.styleSheets.Add(sheet);overlay.AddToClassList("bag-modal");overlay.style.backgroundColor=Color.clear;
            var view=new VisualElement{name="bag-choice-popup"};view.AddToClassList("unity-base-dropdown__container-outer");view.AddToClassList("bag-choice-popup");view.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            float s=Mathf.Abs(field.worldBound.width/field.layout.width),height=field.choices.Count*48+24,w=320*s,h=height*s;var p=root.WorldToLocal(field.worldBound.position);
            float x=Mathf.Clamp(p.x,16,Mathf.Max(16,root.resolvedStyle.width-w-16)),y=p.y+field.worldBound.height+8;
            if(y+h>root.resolvedStyle.height-16)y=p.y-h-8;
            BagPresentation.Place(view,x,Mathf.Max(16,y),320,height);view.style.scale=new Scale(new Vector3(s,s,1));view.style.transformOrigin=new TransformOrigin(0,0,0);
            var surface=new BagSurface{Kind="dropdown"};BagPresentation.Stretch(surface);view.Add(surface);
            var scroll=new ScrollView(ScrollViewMode.Vertical);BagPresentation.Place(scroll,12,12,296,height-24);scroll.horizontalScrollerVisibility=ScrollerVisibility.Hidden;scroll.verticalScrollerVisibility=ScrollerVisibility.Auto;view.Add(scroll);
            var buttons=new BagButton[field.choices.Count];int focused=Mathf.Max(0,field.choices.IndexOf(field.value));
            void Focus(int next){focused=(next+buttons.Length)%buttons.Length;InteractionFeedback.KeyboardNavigation=true;buttons[focused].Focus();}
            for(int i=0;i<buttons.Length;i++){
                int index=i;var button=new BagButton(()=>{string value=field.choices[index];Close(state);field.value=value;}){Kind="item-row",name="bag-choice-"+i,text=field.choices[i]};button.EnableInClassList("is-selected",i==focused);button.AddToClassList("bag-choice-row");button.style.width=296;button.style.height=44;button.style.marginBottom=4;scroll.Add(button);buttons[i]=button;
            }
            scroll.contentContainer.RegisterCallback<NavigationCancelEvent>(e=>{Close(state);e.StopImmediatePropagation();});
            view.RegisterCallback<KeyDownEvent>(e=>{
                if(e.keyCode==KeyCode.DownArrow||e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.Tab){Focus(focused+(e.keyCode==KeyCode.UpArrow||e.shiftKey?-1:1));e.StopImmediatePropagation();}
                else if(e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){string value=field.choices[focused];Close(state);field.value=value;e.StopImmediatePropagation();}
            },TrickleDown.TrickleDown);
            overlay.RegisterCallback<PointerDownEvent>(e=>{if(ReferenceEquals(e.target,overlay)){Close(state);e.StopImmediatePropagation();}});
            overlay.Add(view);root.Add(overlay);view.BringToFront();view.schedule.Execute(()=>buttons[focused].Focus());
        }
    }
}
