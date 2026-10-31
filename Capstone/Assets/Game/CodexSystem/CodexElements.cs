using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.CodexSystem {
    public static class CodexArt {
        static readonly Dictionary<string,Texture2D> cache = new Dictionary<string,Texture2D>();
        public static Texture2D Load(string key) { if(!cache.TryGetValue(key,out var value)){value=Resources.Load<Texture2D>("UI/Codex20261026/"+key);cache[key]=value;}return value; }
    }

    /// <summary>The same manifest contour clips fill, artwork, focus and button picking.</summary>
    public sealed class CodexSurface : VisualElement {
        public string Kind="panel";
        public Button Owner;
        public Sprite Art;
        public bool Unknown,ContainArt,ClearFace;
        public float Hover,Press;
        public bool Hovered,Pressed,Focused;
        Vector2 size;string geometryKind;Vector2[] points;int[] indices;
        IVisualElementScheduledItem animation;
        public CodexSurface(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;RegisterCallback<DetachFromPanelEvent>(_=>{animation?.Pause();Hovered=Pressed=Focused=false;Hover=Press=0;});}
        public Vector2[] Points(){
            if(points!=null&&size==layout.size&&geometryKind==Kind)return points;
            size=layout.size;geometryKind=Kind;float w=size.x,h=size.y,i=3;
            bool parallel=Kind=="category"||Kind=="sort"||Kind=="filter"||Kind=="page"||Kind=="action"||Kind=="badge";
            if(Kind=="page-pattern")points=new[]{Vector2.zero,new Vector2(w,0),new Vector2(w,h),new Vector2(0,h)};
            else if(parallel){float cut=Mathf.Tan(12*Mathf.Deg2Rad)*(h-2*i);points=new[]{new Vector2(i+cut,i),new Vector2(w-i,i),new Vector2(w-i-cut,h-i),new Vector2(i,h-i)};}
            else {float cut=Kind=="left"||Kind=="center"||Kind=="right"?18:Kind=="input"?8:12;cut=Mathf.Min(cut,(h-2*i)/2,(w-2*i)/2);points=new[]{new Vector2(i+cut,i),new Vector2(w-i-cut,i),new Vector2(w-i,i+cut),new Vector2(w-i,h-i-cut),new Vector2(w-i-cut,h-i),new Vector2(i+cut,h-i),new Vector2(i,h-i-cut),new Vector2(i,i+cut)};}
            indices=GreenAngularGeometry.Triangles(points);return points;
        }
        public bool Contains(Vector2 p)=>GreenAngularGeometry.Contains(Points(),p);
        public void Animate(){animation?.Pause();float fromHover=Hover,fromPress=Press;double start=Time.realtimeSinceStartupAsDouble;animation=schedule.Execute(()=>{float t=(float)(Time.realtimeSinceStartupAsDouble-start);Hover=Mathf.Lerp(fromHover,Hovered?1:0,Mathf.Clamp01(t/InteractionFeedback.HoverSeconds));Press=Mathf.Lerp(fromPress,Pressed?1:0,Mathf.Clamp01(t/InteractionFeedback.PressSeconds));MarkDirtyRepaint();if(t>=InteractionFeedback.HoverSeconds)animation.Pause();}).Every(16);}
        void Draw(MeshGenerationContext ctx){
            float w=layout.width,h=layout.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<7||h<7)return;var p=Points();
            if(ClearFace){if(Owner==null||Owner.enabledInHierarchy){var wash=InteractionFeedback.Wash(Hover,Press,Focused);if(wash.a>.001f)Paint(ctx,p,null,wash);}return;}
            if(Kind=="page-pattern"){Paint(ctx,p,CodexArt.Load("Art/codex-material"),new Color(1,1,1,.5f));return;}
            bool selected=Owner!=null&&Owner.ClassListContains("is-selected"),disabled=Owner!=null&&!Owner.enabledInHierarchy;
            bool dark=Kind=="left"||Kind=="category"||Kind=="sort"||Kind=="filter"||Kind=="page"||Kind=="action";
            Color fill=selected?GreenAngularGeometry.Lavender:disabled?GreenAngularGeometry.Disabled:dark?GreenAngularGeometry.Primary:GreenAngularGeometry.Panel;
            if(Unknown)fill=GreenAngularGeometry.Shell;Paint(ctx,p,null,fill);
            if(Kind=="left"||Kind=="center"||Kind=="right"||Kind=="shelf"){
                Paint(ctx,p,CodexArt.Load("Art/codex-material"),new Color(1,1,1,dark?.2f:.5f));var quiet=fill;quiet.a=dark?.1f:.55f;Paint(ctx,p,null,quiet);
            }
            if(Art!=null&&(Kind=="card"||Kind=="portrait")){
                Rect r=Kind=="card"?new Rect(3,3,w-6,h-30):new Rect(3,3,w-6,h-6);
                var tint=Unknown?new Color(0,0,0,.20f):Color.white;Sprite(ctx,p,r,Art,tint,ContainArt);
            }
            if(Kind=="card"){var footer=new Color32(193,195,211,240);Paint(ctx,GreenAngularGeometry.ClipRect(p,new Rect(0,h-28,w,28)),null,footer);}
            var pen=ctx.painter2D;pen.BeginPath();pen.MoveTo(p[0]);for(int n=1;n<p.Length;n++)pen.LineTo(p[n]);pen.ClosePath();pen.lineWidth=selected?2:1.2f;pen.strokeColor=selected?GreenAngularGeometry.Gold:GreenAngularGeometry.Edge;pen.Stroke();
            if(Kind=="left"||Kind=="center"||Kind=="right"||Kind=="shelf"){
                pen.BeginPath();pen.MoveTo(new Vector2(w-9,26));pen.LineTo(new Vector2(w-9,h-26));pen.MoveTo(new Vector2(26,h-9));pen.LineTo(new Vector2(w-26,h-9));pen.lineWidth=1;pen.strokeColor=new Color32(204,173,131,170);pen.Stroke();
            }
            if(!disabled){var wash=InteractionFeedback.Wash(Hover,Press,Focused);if(wash.a>.001f)Paint(ctx,p,null,wash);}
            if(Focused&&!disabled){pen.BeginPath();pen.MoveTo(p[0]);for(int n=1;n<p.Length;n++)pen.LineTo(p[n]);pen.ClosePath();pen.lineWidth=2.4f;pen.strokeColor=GreenAngularGeometry.Gold;pen.Stroke();}
        }
        void Paint(MeshGenerationContext ctx,Vector2[] p,Texture texture,Color tint){
            if(p.Length<3)return;var tri=ReferenceEquals(p,points)?indices:GreenAngularGeometry.Triangles(p);if(tri.Length<3)return;
            var uv=new Rect(0,0,1,1);
            if(texture!=null){float image=(float)texture.width/texture.height,box=layout.width/layout.height;if(image>box){uv.width=box/image;uv.x=(1-uv.width)/2;}else{uv.height=image/box;uv.y=(1-uv.height)/2;}}
            var m=ctx.Allocate(p.Length,tri.Length,texture);
            foreach(var v in p){float u=uv.x+v.x/layout.width*uv.width,t=uv.y+(1-v.y/layout.height)*uv.height;m.SetNextVertex(new Vertex{position=new Vector3(v.x,v.y,Vertex.nearZ),tint=tint,uv=new Vector2(u,t)});}
            foreach(int n in tri)m.SetNextIndex((ushort)n);
        }
        static void Sprite(MeshGenerationContext ctx,Vector2[] polygon,Rect target,Sprite sprite,Color tint,bool contain){
            Rect uv=new Rect(sprite.textureRect.x/sprite.texture.width,sprite.textureRect.y/sprite.texture.height,sprite.textureRect.width/sprite.texture.width,sprite.textureRect.height/sprite.texture.height);float image=sprite.textureRect.width/sprite.textureRect.height,box=target.width/target.height;
            if(contain){if(image>box){float height=target.width/image;target.y+=(target.height-height)/2;target.height=height;}else{float width=target.height*image;target.x+=(target.width-width)/2;target.width=width;}}
            else if(image>box){float fraction=box/image;uv.x+=uv.width*(1-fraction)/2;uv.width*=fraction;}else{float fraction=image/box;uv.y+=uv.height*(1-fraction)/2;uv.height*=fraction;}
            var p=GreenAngularGeometry.ClipRect(polygon,target);if(p.Length<3)return;var tri=GreenAngularGeometry.Triangles(p);if(tri.Length<3)return;var m=ctx.Allocate(p.Length,tri.Length,sprite.texture);
            foreach(var v in p){float u=uv.x+(v.x-target.x)/target.width*uv.width,t=uv.y+(1-(v.y-target.y)/target.height)*uv.height;m.SetNextVertex(new Vertex{position=new Vector3(v.x,v.y,Vertex.nearZ),tint=tint,uv=new Vector2(u,t)});}foreach(int n in tri)m.SetNextIndex((ushort)n);
        }
        public static CodexSurface Behind(VisualElement host,string kind){var surface=new CodexSurface{Kind=kind};surface.style.position=Position.Absolute;surface.style.left=surface.style.right=surface.style.top=surface.style.bottom=0;host.Insert(0,surface);return surface;}
    }

    sealed class CodexClickable : Clickable {
        public CodexClickable():base((System.Action)null){}
        public void Submit(EventBase source)=>Invoke(source);
    }
    [UxmlElement]public partial class CodexButton : Button {
        public readonly CodexSurface Surface;
        readonly CodexClickable input=new CodexClickable();
        readonly Label caption;string content="";
        [UxmlAttribute]public string Kind {get=>Surface.Kind;set=>Surface.Kind=value;}
        public override string text {get=>content;set{content=value??"";base.text="";if(caption!=null){caption.text=content;caption.style.display=string.IsNullOrEmpty(content)?DisplayStyle.None:DisplayStyle.Flex;}}}
        public CodexButton(){
            clickable=input;
            style.backgroundColor=Color.clear;style.borderLeftWidth=style.borderRightWidth=style.borderTopWidth=style.borderBottomWidth=0;
            AddToClassList("codex-control");
            Surface=CodexSurface.Behind(this,"action");Surface.Owner=this;
            caption=new Label{pickingMode=PickingMode.Ignore};caption.AddToClassList("codex-button-caption");Add(caption);
            RegisterCallback<PointerEnterEvent>(_=>{Surface.Hovered=true;Surface.Animate();});RegisterCallback<PointerLeaveEvent>(_=>{Surface.Hovered=Surface.Pressed=false;Surface.Animate();});
            RegisterCallback<PointerDownEvent>(_=>{Surface.Focused=false;Surface.Pressed=true;Surface.Animate();});RegisterCallback<PointerUpEvent>(_=>{Surface.Pressed=false;Surface.Animate();});
            RegisterCallback<FocusEvent>(_=>{Surface.Focused=InteractionFeedback.KeyboardNavigation;Surface.MarkDirtyRepaint();});RegisterCallback<BlurEvent>(_=>{Surface.Focused=false;Surface.MarkDirtyRepaint();});
            RegisterCallback<KeyDownEvent>(e=>{
                Surface.Focused=true;Surface.MarkDirtyRepaint();
                if(!enabledInHierarchy||(e.keyCode!=KeyCode.Return&&e.keyCode!=KeyCode.KeypadEnter&&e.keyCode!=KeyCode.Space))return;
                // Use the same Clickable as pointer input; controller callbacks stay guarded and unchanged.
                SubmitKeyboard(e);e.StopImmediatePropagation();
            });
        }
        internal void SubmitKeyboard(EventBase source=null){if(enabledInHierarchy)input.Submit(source);}
        public override bool ContainsPoint(Vector2 p)=>layout.width>=7&&layout.height>=7&&Surface.Contains(p);
    }
}
