using System.Collections;
using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.ProfileSystem {
    public static class ProfileR2Art {
        const string Root="UI/ProfileR2/";
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        public static Texture2D Texture(string key){if(!textures.TryGetValue(key,out var t)){t=Resources.Load<Texture2D>(Root+key);textures[key]=t;}return t;}
        public static Sprite Sprite(string key){if(!sprites.TryGetValue(key,out var s)){s=Resources.Load<Sprite>(Root+key);sprites[key]=s;}return s;}
    }
    /// <summary>Profile-local textured contour. Uses shared polygon geometry without modifying other screens.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProfileR2Graphic:MaskableGraphic {
        public string Shape="panel";public Texture Texture;public bool Cover,NoFill,Edge,AccentRails;public float LogicalScale=1;
        public Color EdgeColor=GreenAngularGeometry.Edge;public float EdgeWidth=1.35f;
        public override Texture mainTexture=>Texture!=null?Texture:s_WhiteTexture;
        Vector2[] circle,rectangle,scaledContour;Vector2 circleSize,rectangleSize,scaledSize;float cachedLogicalScale;string scaledShape;
        public Vector2[] Contour(){var r=rectTransform.rect;
            if(Shape=="rect"){if(rectangle==null||rectangleSize!=r.size){rectangleSize=r.size;rectangle=new[]{Vector2.zero,new Vector2(r.width,0),r.size,new Vector2(0,r.height)};}return rectangle;}
            if(Shape!="circle"){if(LogicalScale==1)return GreenAngularGeometry.Polygon(Shape,r.width,r.height,3);if(scaledContour==null||scaledSize!=r.size||cachedLogicalScale!=LogicalScale||scaledShape!=Shape){scaledSize=r.size;cachedLogicalScale=LogicalScale;scaledShape=Shape;var logical=GreenAngularGeometry.Polygon(Shape,r.width/LogicalScale,r.height/LogicalScale,3);scaledContour=new Vector2[logical.Length];for(int i=0;i<logical.Length;i++)scaledContour[i]=logical[i]*LogicalScale;}return scaledContour;}
            if(circle!=null&&circleSize==r.size)return circle;circleSize=r.size;circle=new Vector2[128];
            for(int i=0;i<circle.Length;i++){float a=i*Mathf.PI*2/circle.Length;circle[i]=r.size*.5f+new Vector2(Mathf.Cos(a)*(r.width*.5f-3),Mathf.Sin(a)*(r.height*.5f-3));}return circle;
        }
        protected override void OnPopulateMesh(VertexHelper vh){
            vh.Clear();var r=rectTransform.rect;if(r.width<7||r.height<7)return;var p=Contour();
            if(!NoFill){Rect uv=new Rect(0,0,1,1);if(Cover&&Texture!=null){float image=Texture.width/(float)Texture.height,box=r.width/r.height;if(image>box){uv.width=box/image;uv.x=(1-uv.width)/2;}else{uv.height=image/box;uv.y=(1-uv.height)/2;}}
                for(int i=0;i<p.Length;i++){var v=UIVertex.simpleVert;v.position=new Vector3(r.xMin+p[i].x,r.yMax-p[i].y);v.color=color;v.uv0=new Vector2(uv.x+p[i].x/r.width*uv.width,uv.y+(1-p[i].y/r.height)*uv.height);vh.AddVert(v);}
                for(int i=1;i<p.Length-1;i++)vh.AddTriangle(0,i+1,i);
            }
            if(Edge){for(int i=0;i<p.Length;i++)Line(vh,p[i],p[(i+1)%p.Length],EdgeColor,EdgeWidth,r);
                if(AccentRails&&Shape!="circle"){var c=GreenAngularGeometry.Gold;c.a=.48f;Line(vh,new Vector2(r.width-7,22),new Vector2(r.width-7,r.height-22),c,.8f,r);Line(vh,new Vector2(22,r.height-7),new Vector2(r.width-22,r.height-7),c,.8f,r);}}
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,Color tint,float width,Rect r){var n=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int k=vh.currentVertCount;foreach(var p in new[]{a+n,b+n,b-n,a-n})vh.AddVert(new Vector3(r.xMin+p.x,r.yMax-p.y),tint,Vector2.zero);vh.AddTriangle(k,k+2,k+1);vh.AddTriangle(k,k+3,k+2);}
    }
    public sealed class ProfileR2HitArea:MonoBehaviour,ICanvasRaycastFilter {
        public ProfileR2Graphic Surface;
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera){if(Surface==null)return true;var rect=Surface.rectTransform;RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,camera,out var p);return GreenAngularGeometry.Contains(Surface.Contour(),new Vector2(p.x-rect.rect.xMin,rect.rect.yMax-p.y));}
    }
    public sealed class ProfileR2Feedback:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler,ISelectHandler,IDeselectHandler {
        public ProfileR2Graphic Layer;public bool Selected,KeepDisabledFace;Selectable control;bool hover,pressed,focus,disabled;float h,p;Coroutine tween;
        void Awake(){control=GetComponent<Selectable>();}
        public void SetSelected(bool value){if(Selected==value)return;Selected=value;Paint();}
        public void RefreshDisabled(){bool next=control!=null&&!control.IsInteractable();if(next==disabled)return;disabled=next;Paint();}
        void Paint(){if(Layer==null)return;var wash=disabled&&!KeepDisabledFace?new Color(.57f,.60f,.67f,.48f):InteractionFeedback.Wash(h,p,focus);Layer.color=wash;Layer.Edge=Selected||focus;Layer.EdgeWidth=focus?2.6f:2;Layer.EdgeColor=GreenAngularGeometry.Gold;Layer.SetVerticesDirty();}
        void Change(){if(tween!=null)StopCoroutine(tween);if(isActiveAndEnabled)tween=StartCoroutine(Animate());else Paint();}
        IEnumerator Animate(){float h0=h,p0=p,start=Time.unscaledTime;do{float dt=Time.unscaledTime-start;h=Mathf.Lerp(h0,hover?1:0,Mathf.Clamp01(dt/InteractionFeedback.HoverSeconds));p=Mathf.Lerp(p0,pressed?1:0,Mathf.Clamp01(dt/InteractionFeedback.PressSeconds));Paint();yield return null;}while(Time.unscaledTime-start<InteractionFeedback.HoverSeconds);h=hover?1:0;p=pressed?1:0;Paint();tween=null;}
        public void OnPointerEnter(PointerEventData e){RefreshDisabled();if(disabled)return;hover=true;Change();}
        public void OnPointerExit(PointerEventData e){hover=pressed=false;Change();}
        public void OnPointerDown(PointerEventData e){RefreshDisabled();if(disabled)return;focus=false;pressed=true;Change();}
        public void OnPointerUp(PointerEventData e){pressed=false;Change();}
        public void OnSelect(BaseEventData e){focus=true;Change();}
        public void OnDeselect(BaseEventData e){focus=false;Change();}
        void OnEnable(){control=GetComponent<Selectable>();RefreshDisabled();Paint();}
        void OnDisable(){if(tween!=null)StopCoroutine(tween);tween=null;hover=pressed=focus=false;h=p=0;Paint();}
    }
}
