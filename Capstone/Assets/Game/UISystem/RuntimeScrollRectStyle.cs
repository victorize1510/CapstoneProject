using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>Event-driven uGUI equivalent of the shared UITK scrollbar preset.</summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeScrollRectStyle : MonoBehaviour {
        ScrollRect scroll;float scale=1;RectTransform viewport;Vector2 originalMin,originalMax;
        bool verticalShown,horizontalShown;
        public void Bind(ScrollRect target,float logicalScale) {
            if(scroll!=null)return;scroll=target;scale=Mathf.Max(.01f,logicalScale);viewport=scroll.viewport;
            if(viewport!=null){originalMin=viewport.offsetMin;originalMax=viewport.offsetMax;}
            if(scroll.vertical){scroll.verticalScrollbar=Create(true);scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;}
            if(scroll.horizontal){scroll.horizontalScrollbar=Create(false);scroll.horizontalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;}
            scroll.onValueChanged.AddListener(OnScroll);RefreshGutter();
        }
        Scrollbar Create(bool vertical) {
            var go=new GameObject(vertical?"SharedVerticalScrollbar":"SharedHorizontalScrollbar",typeof(RectTransform),typeof(Image),typeof(RuntimeThinScrollbar));
            var rect=(RectTransform)go.transform;rect.SetParent(transform,false);
            if(vertical){rect.anchorMin=new Vector2(1,0);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(1,.5f);rect.offsetMin=new Vector2(-16,8)*scale;rect.offsetMax=new Vector2(-4,-8)*scale;}
            else{rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.right;rect.pivot=new Vector2(.5f,0);rect.offsetMin=new Vector2(8,4)*scale;rect.offsetMax=new Vector2(-8,16)*scale;}
            var hit=go.GetComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            var track=new GameObject("Track",typeof(RectTransform),typeof(RuntimeScrollGlyph));track.transform.SetParent(rect,false);GuiKitControls.Stretch((RectTransform)track.transform);
            var trackGlyph=track.GetComponent<RuntimeScrollGlyph>();trackGlyph.Vertical=vertical;trackGlyph.Thickness=2*scale;trackGlyph.color=new Color32(110,126,153,120);trackGlyph.raycastTarget=false;
            var handle=new GameObject("Handle",typeof(RectTransform),typeof(RuntimeScrollGlyph));handle.transform.SetParent(rect,false);GuiKitControls.Stretch((RectTransform)handle.transform);
            var glyph=handle.GetComponent<RuntimeScrollGlyph>();glyph.Vertical=vertical;glyph.Thickness=4*scale;glyph.color=new Color32(164,179,203,255);glyph.raycastTarget=false;
            var bar=go.GetComponent<RuntimeThinScrollbar>();bar.handleRect=(RectTransform)handle.transform;bar.targetGraphic=hit;bar.transition=Selectable.Transition.None;bar.direction=vertical?Scrollbar.Direction.BottomToTop:Scrollbar.Direction.LeftToRight;bar.Configure(glyph,scale,RefreshGutter);return bar;
        }
        void OnScroll(Vector2 _) => RefreshGutter();
        void OnRectTransformDimensionsChange(){if(scroll!=null)RefreshGutter();}
        void RefreshGutter() {
            if(scroll==null||viewport==null||viewport==(RectTransform)transform)return;
            bool v=scroll.verticalScrollbar!=null&&scroll.verticalScrollbar.gameObject.activeSelf;
            bool h=scroll.horizontalScrollbar!=null&&scroll.horizontalScrollbar.gameObject.activeSelf;
            if(v==verticalShown&&h==horizontalShown)return;verticalShown=v;horizontalShown=h;
            viewport.offsetMin=originalMin+new Vector2(0,h?RuntimeScrollbars.Gutter*scale:0);
            viewport.offsetMax=originalMax-new Vector2(v?RuntimeScrollbars.Gutter*scale:0,0);
        }
        void OnDestroy(){if(scroll!=null)scroll.onValueChanged.RemoveListener(OnScroll);}
    }
    public sealed class RuntimeThinScrollbar : Scrollbar {
        RuntimeScrollGlyph glyph;float scale;bool hover,pressed,focused;System.Action visibilityChanged;
        public void Configure(RuntimeScrollGlyph value,float logicalScale,System.Action changed){glyph=value;scale=logicalScale;visibilityChanged=changed;Paint();}
        void Paint(){if(glyph==null)return;glyph.Thickness=(hover||pressed||focused?6:4)*scale;glyph.color=pressed?GreenAngularGeometry.Gold:focused||hover?GreenAngularGeometry.Lavender:new Color32(164,179,203,255);glyph.SetVerticesDirty();}
        public override void OnPointerEnter(PointerEventData e){base.OnPointerEnter(e);hover=true;Paint();}
        public override void OnPointerExit(PointerEventData e){base.OnPointerExit(e);hover=false;Paint();}
        public override void OnPointerDown(PointerEventData e){base.OnPointerDown(e);pressed=true;Paint();}
        public override void OnPointerUp(PointerEventData e){base.OnPointerUp(e);pressed=false;Paint();}
        public override void OnSelect(BaseEventData e){base.OnSelect(e);focused=true;Paint();}
        public override void OnDeselect(BaseEventData e){base.OnDeselect(e);focused=false;Paint();}
        protected override void OnEnable(){base.OnEnable();visibilityChanged?.Invoke();}
        protected override void OnDisable(){hover=pressed=focused=false;base.OnDisable();visibilityChanged?.Invoke();}
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RuntimeScrollGlyph : MaskableGraphic {
        public bool Vertical=true;public float Thickness=4;
        protected override void OnPopulateMesh(VertexHelper vh){vh.Clear();var r=rectTransform.rect;if(Vertical){r.xMin=r.center.x-Thickness*.5f;r.width=Thickness;}else{r.yMin=r.center.y-Thickness*.5f;r.height=Thickness;}vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);vh.AddTriangle(0,1,2);vh.AddTriangle(2,3,0);}
    }
}
