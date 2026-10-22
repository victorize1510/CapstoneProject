using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Capstone.Game.UISystem {
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GreenAngularGraphic : MaskableGraphic, ICanvasRaycastFilter, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler {
        public string Shape="panel";
        public Texture SurfaceTexture;
        public Color Fill=GreenAngularGeometry.Panel, Stroke=GreenAngularGeometry.Gold;
        public bool Outline=true, MaskOnly, StateLayer;
        public GreenAngularGraphic StateOverlay;
        bool hover,pressed,focus;float hoverAmount,pressAmount;Coroutine tween;
        public override Texture mainTexture=>SurfaceTexture!=null?SurfaceTexture:s_WhiteTexture;
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera){RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,camera,out var p);var r=rectTransform.rect;return GreenAngularGeometry.Contains(GreenAngularGeometry.Polygon(Shape,r.width,r.height),new Vector2(p.x-r.xMax+r.width,r.yMax-p.y));}
        protected override void OnPopulateMesh(VertexHelper vh){
            vh.Clear();var r=rectTransform.rect;if(r.width<7||r.height<7)return;
            var pts=GreenAngularGeometry.Polygon(Shape,r.width,r.height);var fill=Fill;var b=GetComponent<Selectable>();if(b!=null&&!b.IsInteractable())fill=GreenAngularGeometry.Disabled;
            if(StateOverlay==null&&!StateLayer)fill=Color.Lerp(fill,GreenAngularGeometry.Ink,pressAmount*InteractionFeedback.PressedInkAlpha);
            if(MaskOnly)fill=Color.white;
            if(StateLayer){fill=GreenAngularGeometry.Ink;fill.a=pressAmount*InteractionFeedback.PressedInkAlpha;}
            var tri=GreenAngularGeometry.Triangles(pts);
            for(int i=0;i<pts.Length;i++){var v=UIVertex.simpleVert;v.position=new Vector3(r.xMin+pts[i].x,r.yMax-pts[i].y);v.color=SurfaceTexture!=null?Color.white:StateLayer?fill:GreenAngularGeometry.Shade(fill,Mathf.Lerp(5f/255,-5f/255,pts[i].y/r.height));v.uv0=SurfaceTexture!=null?new Vector2(pts[i].x/r.width,1-pts[i].y/r.height):Vector2.zero;vh.AddVert(v);}
            for(int i=0;i<tri.Length;i+=3)vh.AddTriangle(tri[i],tri[i+2],tri[i+1]);
            if(!MaskOnly&&Outline)Ring(vh,pts,Stroke,1.5f,r);if(!MaskOnly&&hoverAmount>.001f&&(StateOverlay==null||StateLayer))Ring(vh,GreenAngularGeometry.Polygon(Shape,r.width,r.height,6),InteractionFeedback.Edge(hoverAmount),1.5f,r);
            if(!MaskOnly&&focus&&(StateOverlay==null||StateLayer)){var c=GreenAngularGeometry.Gold;for(int side=0;side<2;side++){float x=side==0?10:r.width-10,dir=side==0?1:-1;
                Segment(vh,new Vector2(x+dir*12,10),new Vector2(x,10),c,2,r);Segment(vh,new Vector2(x,10),new Vector2(x,24),c,2,r);
                Segment(vh,new Vector2(x,r.height-24),new Vector2(x,r.height-10),c,2,r);Segment(vh,new Vector2(x,r.height-10),new Vector2(x+dir*12,r.height-10),c,2,r);}}
        }
        static void AddPoint(VertexHelper vh,Vector2 q,Color c,Rect r)=>vh.AddVert(new Vector3(r.xMin+q.x,r.yMax-q.y),c,Vector2.zero);
        static void Ring(VertexHelper vh,Vector2[] pts,Color c,float width,Rect r){for(int i=0;i<pts.Length;i++){Vector2 a=pts[i],b=pts[(i+1)%pts.Length],n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;AddPoint(vh,a+n,c,r);AddPoint(vh,b+n,c,r);AddPoint(vh,b-n,c,r);AddPoint(vh,a-n,c,r);vh.AddTriangle(k,k+2,k+1);vh.AddTriangle(k,k+3,k+2);}}
        static void Segment(VertexHelper vh,Vector2 a,Vector2 b,Color c,float width,Rect r){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;AddPoint(vh,a+n,c,r);AddPoint(vh,b+n,c,r);AddPoint(vh,b-n,c,r);AddPoint(vh,a-n,c,r);vh.AddTriangle(k,k+2,k+1);vh.AddTriangle(k,k+3,k+2);}
        System.Collections.IEnumerator Animate(){float fromHover=hoverAmount,fromPress=pressAmount;float start=Time.unscaledTime;while(true){float dt=Time.unscaledTime-start;hoverAmount=Mathf.Lerp(fromHover,hover?1:0,Mathf.Clamp01(dt/InteractionFeedback.HoverSeconds));pressAmount=Mathf.Lerp(fromPress,pressed?1:0,Mathf.Clamp01(dt/InteractionFeedback.PressSeconds));PaintState();if(dt>=InteractionFeedback.HoverSeconds)break;yield return null;}tween=null;}
        void PaintState(){SetVerticesDirty();if(StateOverlay!=null){StateOverlay.hoverAmount=hoverAmount;StateOverlay.pressAmount=pressAmount;StateOverlay.focus=focus;StateOverlay.SetVerticesDirty();}}
        void Changed(){if(tween!=null)StopCoroutine(tween);if(isActiveAndEnabled)tween=StartCoroutine(Animate());else PaintState();}
        protected override void OnDisable(){if(tween!=null)StopCoroutine(tween);tween=null;hover=pressed=focus=false;hoverAmount=pressAmount=0;PaintState();base.OnDisable();}
        public void OnPointerEnter(PointerEventData e){hover=true;Changed();}public void OnPointerExit(PointerEventData e){hover=pressed=false;Changed();}
        public void OnPointerDown(PointerEventData e){InteractionFeedback.KeyboardNavigation=false;focus=false;pressed=true;Changed();}public void OnPointerUp(PointerEventData e){pressed=false;Changed();}
        public void OnSelect(BaseEventData e){focus=InteractionFeedback.KeyboardNavigation&&!(e is PointerEventData);Changed();}public void OnDeselect(BaseEventData e){focus=false;Changed();}
    }
}
