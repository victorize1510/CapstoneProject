using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Capstone.Game.UISystem {
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GreenAngularGraphic : MaskableGraphic, ICanvasRaycastFilter, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler {
        public string Shape="panel";
        Texture surfaceTexture;
        public Texture SurfaceTexture {get=>surfaceTexture;set{if(surfaceTexture==value)return;surfaceTexture=value;SetMaterialDirty();SetVerticesDirty();}}
        public Color Fill=GreenAngularGeometry.Panel, Stroke=GreenAngularGeometry.Edge;
        public bool Outline=true,MaskOnly,StateLayer,CoverTexture,AccentRails;
        public float FadeLeftStart,FadeLeftEnd;
        public GreenAngularGraphic StateOverlay;
        bool hover,pressed,focus;float hoverAmount,pressAmount;Coroutine tween;
        public override Texture mainTexture=>SurfaceTexture!=null?SurfaceTexture:s_WhiteTexture;
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera){RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screen,camera,out var p);var r=rectTransform.rect;return GreenAngularGeometry.Contains(GreenAngularGeometry.Polygon(Shape,r.width,r.height),new Vector2(p.x-r.xMin,r.yMax-p.y));}
        protected override void OnPopulateMesh(VertexHelper vh){
            vh.Clear();var r=rectTransform.rect;if(r.width<7||r.height<7)return;
            var pts=GreenAngularGeometry.Polygon(Shape,r.width,r.height);var fill=MaskOnly?Color.white:StateLayer?InteractionFeedback.Wash(hoverAmount,pressAmount,focus):Fill;
            if(!StateLayer&&TryGetComponent<Selectable>(out var b)&&!b.IsInteractable())fill=GreenAngularGeometry.Disabled;
            Rect uv=new Rect(0,0,1,1);
            if(CoverTexture&&SurfaceTexture!=null){float imageAspect=(float)SurfaceTexture.width/SurfaceTexture.height,box=r.width/r.height;if(imageAspect>box){uv.width=box/imageAspect;uv.x=(1-uv.width)/2;}else{uv.height=imageAspect/box;uv.y=(1-uv.height)/2;}}
            if(FadeLeftEnd>FadeLeftStart&&SurfaceTexture!=null){
                // Clipped strips preserve smoothstep alpha without changing any control geometry.
                const int strips=16;for(int i=0;i<strips;i++)Paint(vh,GreenAngularGeometry.ClipRect(pts,new Rect(i*r.width/strips,0,r.width/strips,r.height)),r,uv,fill);
            }else Paint(vh,pts,r,uv,fill);
            if(!MaskOnly&&Outline)Ring(vh,pts,Stroke,1.5f,r);
            if(!MaskOnly&&AccentRails&&pts.Length>=4){var c=GreenAngularGeometry.Gold;c.a=.48f;Segment(vh,pts[1]+new Vector2(-4,4),pts[2]+new Vector2(-4,-4),c,1.2f,r);Segment(vh,pts[2]+new Vector2(-4,-4),pts[3]+new Vector2(4,-4),c,1.2f,r);}
            if(!MaskOnly&&(StateOverlay==null||StateLayer)){float amount=Mathf.Max(hoverAmount,focus?1:0);if(amount>.001f)Ring(vh,pts,InteractionFeedback.Edge(amount),2.8f,r);}
        }
        void Paint(VertexHelper vh,Vector2[] pts,Rect r,Rect uv,Color fill){
            if(pts.Length<3)return;int offset=vh.currentVertCount;var tri=GreenAngularGeometry.Triangles(pts);
            foreach(var p in pts){var v=UIVertex.simpleVert;v.position=new Vector3(r.xMin+p.x,r.yMax-p.y);Color tint=SurfaceTexture!=null?Color.white:StateLayer||MaskOnly?fill:GreenAngularGeometry.Shade(fill,Mathf.Lerp(5f/255,-5f/255,p.y/r.height));
                if(SurfaceTexture!=null&&FadeLeftEnd>FadeLeftStart)tint.a=Mathf.SmoothStep(0,1,Mathf.InverseLerp(FadeLeftStart,FadeLeftEnd,p.x/r.width));
                v.color=tint;v.uv0=SurfaceTexture!=null?new Vector2(uv.x+p.x/r.width*uv.width,uv.y+(1-p.y/r.height)*uv.height):Vector2.zero;vh.AddVert(v);
            }for(int i=0;i<tri.Length;i+=3)vh.AddTriangle(offset+tri[i],offset+tri[i+2],offset+tri[i+1]);
        }
        static void AddPoint(VertexHelper vh,Vector2 q,Color c,Rect r)=>vh.AddVert(new Vector3(r.xMin+q.x,r.yMax-q.y),c,Vector2.zero);
        static void Ring(VertexHelper vh,Vector2[] pts,Color c,float width,Rect r){for(int i=0;i<pts.Length;i++)Segment(vh,pts[i],pts[(i+1)%pts.Length],c,width,r);}
        static void Segment(VertexHelper vh,Vector2 a,Vector2 b,Color c,float width,Rect r){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;AddPoint(vh,a+n,c,r);AddPoint(vh,b+n,c,r);AddPoint(vh,b-n,c,r);AddPoint(vh,a-n,c,r);vh.AddTriangle(k,k+2,k+1);vh.AddTriangle(k,k+3,k+2);}
        System.Collections.IEnumerator Animate(){float fromHover=hoverAmount,fromPress=pressAmount,start=Time.unscaledTime;while(true){float dt=Time.unscaledTime-start;hoverAmount=Mathf.Lerp(fromHover,hover?1:0,Mathf.Clamp01(dt/InteractionFeedback.HoverSeconds));pressAmount=Mathf.Lerp(fromPress,pressed?1:0,Mathf.Clamp01(dt/InteractionFeedback.PressSeconds));PaintState();if(dt>=InteractionFeedback.HoverSeconds)break;yield return null;}tween=null;}
        void PaintState(){if(StateOverlay!=null){StateOverlay.hoverAmount=hoverAmount;StateOverlay.pressAmount=pressAmount;StateOverlay.focus=focus;StateOverlay.SetVerticesDirty();}else SetVerticesDirty();}
        void Changed(){if(tween!=null)StopCoroutine(tween);if(isActiveAndEnabled)tween=StartCoroutine(Animate());else PaintState();}
        protected override void OnDisable(){if(tween!=null)StopCoroutine(tween);tween=null;hover=pressed=focus=false;hoverAmount=pressAmount=0;PaintState();base.OnDisable();}
        public void OnPointerEnter(PointerEventData e){if(TryGetComponent<Selectable>(out var b)&&!b.IsInteractable())return;hover=true;Changed();}
        public void OnPointerExit(PointerEventData e){hover=pressed=false;Changed();}
        public void OnPointerDown(PointerEventData e){if(TryGetComponent<Selectable>(out var b)&&!b.IsInteractable())return;InteractionFeedback.KeyboardNavigation=false;focus=false;pressed=true;Changed();}
        public void OnPointerUp(PointerEventData e){pressed=false;Changed();}
        public void OnSelect(BaseEventData e){focus=InteractionFeedback.KeyboardNavigation&&!(e is PointerEventData);Changed();}
        public void OnDeselect(BaseEventData e){focus=false;Changed();}
    }
}
