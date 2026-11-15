using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Runtime-only scrollbar presentation. Never binds sliders or world-map zoom.</summary>
    public static class RuntimeScrollbars {
        public const float HitWidth=12, Gutter=16, Edge=4, EndInset=8;
        public static ScrollView Create(ScrollViewMode mode=ScrollViewMode.Vertical) {
            var scroll=new ScrollView(mode);Bind(scroll);Apply(scroll);return scroll;
        }
        public static void Bind(VisualElement root) {
            if(root==null)return;
            if(root.ClassListContains("runtime-scrollbars"))return;
            root.AddToClassList("runtime-scrollbars");
            var sheet=Resources.Load<StyleSheet>("UI/CompanionSkillScroll20261101/Scrollbars");
            if(sheet!=null)root.styleSheets.Add(sheet);
            foreach(var scroll in root.Query<ScrollView>().ToList())Apply(scroll);
            root.RegisterCallback<AttachToPanelEvent>(e=>{if(e.target is ScrollView scroll)Apply(scroll);},TrickleDown.TrickleDown);
        }
        public static void Apply(ScrollView scroll) {
            if(scroll==null||scroll.ClassListContains("runtime-scroll"))return;
            scroll.AddToClassList("runtime-scroll");
            if(scroll.verticalScrollerVisibility!=ScrollerVisibility.Hidden)scroll.verticalScrollerVisibility=ScrollerVisibility.Auto;
            if(scroll.horizontalScrollerVisibility!=ScrollerVisibility.Hidden)scroll.horizontalScrollerVisibility=ScrollerVisibility.Auto;
            Setup(scroll.verticalScroller,true);Setup(scroll.horizontalScroller,false);
            bool scheduled=false;
            void Fit() {
                scheduled=false;if(scroll.panel==null)return;
                bool vertical=scroll.verticalScrollerVisibility!=ScrollerVisibility.Hidden&&scroll.verticalScroller.resolvedStyle.display!=DisplayStyle.None;
                bool horizontal=scroll.horizontalScrollerVisibility!=ScrollerVisibility.Hidden&&scroll.horizontalScroller.resolvedStyle.display!=DisplayStyle.None;
                float right=vertical?Gutter:0,bottom=horizontal?Gutter:0;
                if(!Mathf.Approximately(scroll.contentViewport.resolvedStyle.marginRight,right))scroll.contentViewport.style.marginRight=right;
                if(!Mathf.Approximately(scroll.contentViewport.resolvedStyle.marginBottom,bottom))scroll.contentViewport.style.marginBottom=bottom;
            }
            void Queue() {if(scheduled||scroll.panel==null)return;scheduled=true;scroll.schedule.Execute(Fit);}
            scroll.RegisterCallback<GeometryChangedEvent>(_=>Queue());
            scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_=>Queue());
            scroll.verticalScroller.RegisterCallback<GeometryChangedEvent>(_=>Queue());
            scroll.horizontalScroller.RegisterCallback<GeometryChangedEvent>(_=>Queue());
            scroll.RegisterCallback<AttachToPanelEvent>(_=>Queue());
            scroll.RegisterCallback<DetachFromPanelEvent>(_=>scheduled=false);
            Queue();
        }
        static void Setup(Scroller scroller,bool vertical) {
            scroller.AddToClassList(vertical?"runtime-scroll-vertical":"runtime-scroll-horizontal");
            scroller.style.position=Position.Absolute;
            scroller.style.marginLeft=scroller.style.marginTop=scroller.style.marginRight=scroller.style.marginBottom=0;
            scroller.style.paddingLeft=scroller.style.paddingTop=scroller.style.paddingRight=scroller.style.paddingBottom=0;
            scroller.style.backgroundColor=Color.clear;
            if(vertical){scroller.style.right=Edge;scroller.style.top=EndInset;scroller.style.bottom=EndInset;scroller.style.left=StyleKeyword.Auto;scroller.style.height=StyleKeyword.Auto;scroller.style.width=scroller.style.minWidth=scroller.style.maxWidth=HitWidth;}
            else{scroller.style.bottom=Edge;scroller.style.left=EndInset;scroller.style.right=EndInset;scroller.style.top=StyleKeyword.Auto;scroller.style.width=StyleKeyword.Auto;scroller.style.height=scroller.style.minHeight=scroller.style.maxHeight=HitWidth;}
            scroller.lowButton.style.display=scroller.highButton.style.display=DisplayStyle.None;
            scroller.RegisterCallback<FocusInEvent>(_=>scroller.AddToClassList("runtime-scroll-focus"));
            scroller.RegisterCallback<FocusOutEvent>(_=>scroller.RemoveFromClassList("runtime-scroll-focus"));
            scroller.RegisterCallback<PointerDownEvent>(_=>scroller.AddToClassList("runtime-scroll-pressed"));
            scroller.RegisterCallback<PointerUpEvent>(_=>scroller.RemoveFromClassList("runtime-scroll-pressed"));
            scroller.RegisterCallback<PointerCaptureOutEvent>(_=>scroller.RemoveFromClassList("runtime-scroll-pressed"));
        }
        public static void Apply(ScrollRect scroll,float logicalScale=1) {
            if(scroll==null)return;
            var view=scroll.GetComponent<RuntimeScrollRectStyle>();
            if(view==null)view=scroll.gameObject.AddComponent<RuntimeScrollRectStyle>();
            view.Bind(scroll,logicalScale);
        }
    }
}
