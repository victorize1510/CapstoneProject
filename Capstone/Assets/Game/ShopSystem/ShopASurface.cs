using System;
using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.ShopSystem {
    // Only A and shared exports are loaded here. Materials cover; metal caps retain their native angle.
    public static class ShopAArt {
        public const string Root="UI/ShopA20261104/";
        [Serializable] public sealed class Component { public string id,shape; public float width,height,corner,inset; }
        [Serializable] sealed class Manifest { public Component[] components; }
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Component> components=new Dictionary<string,Component>();
        public static Texture2D Texture(string path) { if(!textures.TryGetValue(path,out var t)){t=Resources.Load<Texture2D>(Root+path);textures[path]=t;}return t; }
        public static Component Spec(string id) { if(components.Count==0)foreach(var c in JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>(Root+"components").text).components)components[c.id]=c;return components[id]; }
        public static Vector2[] Contour(Component c,float w,float h,float extra=0) {
            float p=c.inset/1.2f+extra,r=Mathf.Max(p,w-p),b=Mathf.Max(p,h-p);
            if(c.shape=="parallel"){float k=Mathf.Min((b-p)*.2125566f,(r-p)*.3f);return new[]{new Vector2(p+k,p),new Vector2(r,p),new Vector2(r-k,b),new Vector2(p,b)};}
            float cut=Mathf.Min(c.corner/1.2f,Mathf.Min((r-p)/2,(b-p)/2));
            return new[]{new Vector2(p+cut,p),new Vector2(r-cut,p),new Vector2(r,p+cut),new Vector2(r,b-cut),new Vector2(r-cut,b),new Vector2(p+cut,b),new Vector2(p,b-cut),new Vector2(p,p+cut)};
        }
    }
    public sealed class ShopASurface:VisualElement {
        public readonly string Kind;public readonly bool Overlay;
        public VisualElement Owner;public ShopASurface Background,FeedbackSource;public string FillKind;
        public bool Hovered,Pressed,Focused;
        float hoverAmount,pressAmount;Vector2 size;Vector2[] contour,inner;
        readonly List<Vector2> vertices=new List<Vector2>(),uvs=new List<Vector2>();readonly List<int> indices=new List<int>();
        IVisualElementScheduledItem animation;
        public ShopASurface(string kind,bool overlay=false){Kind=kind;Overlay=overlay;pickingMode=PickingMode.Ignore;AddToClassList("shop-a-layer");generateVisualContent+=Draw;RegisterCallback<DetachFromPanelEvent>(_=>Stop());}
        public void Stop(){animation?.Pause();Hovered=Pressed=Focused=false;hoverAmount=pressAmount=0;MarkDirtyRepaint();Background?.MarkDirtyRepaint();}
        public void Animate(){animation?.Pause();float h=hoverAmount,p=pressAmount;double start=Time.realtimeSinceStartupAsDouble;
            animation=schedule.Execute(()=>{float dt=(float)(Time.realtimeSinceStartupAsDouble-start);hoverAmount=Mathf.Lerp(h,Hovered?1:0,Mathf.Clamp01(dt/.14f));pressAmount=Mathf.Lerp(p,Pressed?1:0,Mathf.Clamp01(dt/.08f));MarkDirtyRepaint();Background?.MarkDirtyRepaint();if(dt>=.14f)animation.Pause();}).Every(16);}
        void Geometry(){if(contour!=null&&size==layout.size)return;size=layout.size;var c=ShopAArt.Spec(Kind);contour=ShopAArt.Contour(c,size.x,size.y);inner=ShopAArt.Contour(c,size.x,size.y,3);
            vertices.Clear();uvs.Clear();indices.Clear();float cx=c.shape=="parallel"?Mathf.Min(36,c.width/3):c.corner+17,cy=c.shape=="parallel"?c.height/2:c.corner+17;
            float dx=Mathf.Min(cx/1.2f,size.x/2),dy=Mathf.Min(cy/1.2f,size.y/2);float[] xs={0,dx,size.x-dx,size.x},ys={0,dy,size.y-dy,size.y},us={0,cx/c.width,1-cx/c.width,1},vs={0,cy/c.height,1-cy/c.height,1};
            for(int y=0;y<3;y++)for(int x=0;x<3;x++){if(xs[x+1]<=xs[x]||ys[y+1]<=ys[y])continue;var pts=GreenAngularGeometry.ClipRect(contour,Rect.MinMaxRect(xs[x],ys[y],xs[x+1],ys[y+1]));if(pts.Length<3)continue;int start=vertices.Count;
                foreach(var q in pts){vertices.Add(q);uvs.Add(new Vector2(Mathf.Lerp(us[x],us[x+1],(q.x-xs[x])/(xs[x+1]-xs[x])),1-Mathf.Lerp(vs[y],vs[y+1],(q.y-ys[y])/(ys[y+1]-ys[y]))));}foreach(int i in GreenAngularGeometry.Triangles(pts))indices.Add(start+i);}
        }
        void Draw(MeshGenerationContext ctx){if(layout.width<2||layout.height<2)return;Geometry();bool selected=Owner?.ClassListContains("is-selected")==true;
            if(!Overlay){var texture=ShopAArt.Texture("Components/"+(FillKind??(Kind=="A-category"&&!selected?"A-search":Kind))+"__fill");Paint(ctx,contour,texture,Color.white,true);
                if(Kind=="A-sidebar")DrawVignette(ctx);
                var border=ShopAArt.Texture("Components/"+Kind+"__border");if(border!=null&&indices.Count>0){var mesh=ctx.Allocate(vertices.Count,indices.Count,border);for(int i=0;i<vertices.Count;i++)mesh.SetNextVertex(new Vertex{position=new Vector3(vertices[i].x,vertices[i].y,Vertex.nearZ),uv=uvs[i],tint=Color.white});foreach(int i in indices)mesh.SetNextIndex((ushort)i);}
                if(selected&&Kind!="A-category")Paint(ctx,inner,null,new Color(.65f,.59f,.74f,.34f));
                if(Owner!=null){var state=FeedbackSource;if(!Owner.enabledInHierarchy)Paint(ctx,inner,null,new Color(.40f,.43f,.52f,.38f));else if(state!=null){var wash=InteractionFeedback.Wash(state.hoverAmount,state.pressAmount,state.Focused);if(wash.a>.001f)Paint(ctx,inner,null,wash);}}return;}
            if(selected)Stroke(ctx,inner,Kind=="A-category"?GreenAngularGeometry.Gold:GreenAngularGeometry.Lavender,2.1f);
            if(Focused&&Owner?.enabledInHierarchy==true)Stroke(ctx,inner,GreenAngularGeometry.Gold,1.8f);
        }
        void DrawVignette(MeshGenerationContext ctx){var t=ShopAArt.Texture("Art/trader-vignette");if(t==null)return;float width=size.x-6,h=Mathf.Min(size.y*.30f,width*t.height/t.width);float w=h*t.width/t.height;var dest=new Rect((size.x-w)/2,size.y-h-5,w,h);var pts=GreenAngularGeometry.ClipRect(contour,dest);Paint(ctx,pts,t,new Color(1,1,1,.38f),false,dest);}
        void Paint(MeshGenerationContext ctx,Vector2[] pts,Texture t,Color color,bool cover=false,Rect dest=default){if(pts.Length<3||(t==null&&color==Color.white))return;if(dest.width<=0)dest=new Rect(0,0,size.x,size.y);var uv=new Rect(0,0,1,1);
            if(cover&&t!=null){float image=(float)t.width/t.height,box=dest.width/dest.height;if(image>box){uv.width=box/image;uv.x=(1-uv.width)/2;}else{uv.height=image/box;uv.y=(1-uv.height)/2;}}
            var tris=GreenAngularGeometry.Triangles(pts);var m=ctx.Allocate(pts.Length,tris.Length,t);foreach(var p in pts)m.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=color,uv=t==null?Vector2.zero:new Vector2(uv.x+(p.x-dest.x)/dest.width*uv.width,uv.y+(1-(p.y-dest.y)/dest.height)*uv.height)});foreach(int i in tris)m.SetNextIndex((ushort)i);}
        static void Stroke(MeshGenerationContext ctx,Vector2[] pts,Color color,float width){var p=ctx.painter2D;p.BeginPath();p.MoveTo(pts[0]);for(int i=1;i<pts.Length;i++)p.LineTo(pts[i]);p.ClosePath();p.strokeColor=color;p.lineWidth=width;p.Stroke();}
    }
    [UxmlElement] public partial class ShopAButton:Button {
        readonly Label caption;public string Kind="icon-button";public bool PlainChoice;Vector2 hitSize;string hitKind;Vector2[] hit;
        public override string text {get=>base.text;set{base.text=string.Empty;if(caption!=null){caption.text=value??string.Empty;caption.style.display=string.IsNullOrEmpty(value)?DisplayStyle.None:DisplayStyle.Flex;}}}
        public ShopAButton(){AddToClassList("shop-a-button");caption=new Label{pickingMode=PickingMode.Ignore};caption.AddToClassList("shop-a-caption");Add(caption);}
        public override bool ContainsPoint(Vector2 p){if(PlainChoice)return base.ContainsPoint(p);if(hit==null||hitSize!=layout.size||hitKind!=Kind){hitSize=layout.size;hitKind=Kind;hit=ShopAArt.Contour(ShopAArt.Spec(Kind),layout.width,layout.height);}return GreenAngularGeometry.Contains(hit,p);}
    }
}
