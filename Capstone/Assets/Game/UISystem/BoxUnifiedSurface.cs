using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>BOX-only assets. Artwork is covered; only the separate metal frame uses fixed caps.</summary>
    public static class BoxUnifiedArt {
        public const string Root="UI/BoxUnified20261103/";
        [Serializable] public sealed class Component { public string id,shape; public float width,height,corner,inset; }
        [Serializable] sealed class Manifest { public Component[] components; }
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        static readonly Dictionary<string,Component> components=new Dictionary<string,Component>();
        public static Texture2D Texture(string key) {
            if(!textures.TryGetValue(key,out var value)){value=Resources.Load<Texture2D>(Root+key);textures[key]=value;}
            return value;
        }
        public static Component Spec(string key) {
            if(components.Count==0)foreach(var c in JsonUtility.FromJson<Manifest>(Resources.Load<TextAsset>(Root+"components").text).components)components[c.id]=c;
            return components[key];
        }
        public static Vector2[] Contour(Component c,float w,float h,float extra=0) {
            float i=c.inset+extra,l=i,t=i,r=Mathf.Max(i,w-i),b=Mathf.Max(i,h-i);
            if(c.shape=="parallel") {
                float slant=Mathf.Min((b-t)*.2125566f,(r-l)*.3f);
                return new[]{new Vector2(l+slant,t),new Vector2(r,t),new Vector2(r-slant,b),new Vector2(l,b)};
            }
            float k=Mathf.Min(c.corner,Mathf.Min((r-l)/2,(b-t)/2));
            return new[]{new Vector2(l+k,t),new Vector2(r-k,t),new Vector2(r,t+k),new Vector2(r,b-k),new Vector2(r-k,b),new Vector2(l+k,b),new Vector2(l,b-k),new Vector2(l,t+k)};
        }
    }

    public sealed class BoxUnifiedSurface:VisualElement {
        public readonly string Kind;
        public readonly bool Overlay;
        public VisualElement Owner;
        public BoxUnifiedSurface Background,FeedbackSource;
        public Sprite Portrait;
        public bool PortraitOnly,Hovered,Pressed,Focused;
        float hoverAmount,pressAmount;
        Vector2 size;
        Vector2[] contour,insetContour,focusContour,portraitContour;
        Sprite geometryPortrait;
        Rect portraitUv,portraitRect;
        readonly List<Vector2> borderPoints=new List<Vector2>(),borderUvs=new List<Vector2>();
        readonly List<int> borderIndices=new List<int>();
        IVisualElementScheduledItem animation;
        BoxUnifiedArt.Component Spec=>BoxUnifiedArt.Spec(Kind);
        public BoxUnifiedSurface(string kind,bool overlay=false) {
            Kind=kind;Overlay=overlay;pickingMode=PickingMode.Ignore;
            AddToClassList("box-skin-layer");generateVisualContent+=Draw;
            RegisterCallback<GeometryChangedEvent>(_=>{size=Vector2.zero;MarkDirtyRepaint();});
            RegisterCallback<DetachFromPanelEvent>(_=>{animation?.Pause();Hovered=Pressed=Focused=false;hoverAmount=pressAmount=0;});
        }
        public void Animate() {
            animation?.Pause();float h=hoverAmount,p=pressAmount;double start=Time.realtimeSinceStartupAsDouble;
            animation=schedule.Execute(()=>{float dt=(float)(Time.realtimeSinceStartupAsDouble-start);
                hoverAmount=Mathf.Lerp(h,Hovered?1:0,Mathf.Clamp01(dt/.14f));pressAmount=Mathf.Lerp(p,Pressed?1:0,Mathf.Clamp01(dt/.08f));
                MarkDirtyRepaint();Background?.MarkDirtyRepaint();if(dt>=.14f)animation.Pause();}).Every(16);
        }
        void Geometry() {
            if(contour!=null&&size==layout.size)return;size=layout.size;
            contour=BoxUnifiedArt.Contour(Spec,size.x,size.y);insetContour=BoxUnifiedArt.Contour(Spec,size.x,size.y,3);focusContour=BoxUnifiedArt.Contour(Spec,size.x,size.y,6);portraitContour=null;
            borderPoints.Clear();borderUvs.Clear();borderIndices.Clear();
            var c=Spec;
            float cx=c.shape=="parallel"?Mathf.Min(36,c.width/3):c.corner+17;
            float cy=c.shape=="parallel"?c.height/2:c.corner+17;
            float dx=Mathf.Min(cx,size.x/2),dy=Mathf.Min(cy,size.y/2);
            float[] xs={0,dx,size.x-dx,size.x},ys={0,dy,size.y-dy,size.y};
            float[] us={0,cx/c.width,1-cx/c.width,1},vs={0,cy/c.height,1-cy/c.height,1};
            for(int y=0;y<3;y++)for(int x=0;x<3;x++) {
                if(xs[x+1]<=xs[x]||ys[y+1]<=ys[y])continue;
                var clipped=GreenAngularGeometry.ClipRect(contour,Rect.MinMaxRect(xs[x],ys[y],xs[x+1],ys[y+1]));
                if(clipped.Length<3)continue;int offset=borderPoints.Count;
                foreach(var p in clipped){borderPoints.Add(p);borderUvs.Add(new Vector2(Mathf.Lerp(us[x],us[x+1],(p.x-xs[x])/(xs[x+1]-xs[x])),1-Mathf.Lerp(vs[y],vs[y+1],(p.y-ys[y])/(ys[y+1]-ys[y]))));}
                foreach(int index in GreenAngularGeometry.Triangles(clipped))borderIndices.Add(offset+index);
            }
        }
        void Draw(MeshGenerationContext ctx) {
            if(layout.width<2||layout.height<2)return;Geometry();
            if(PortraitOnly){DrawPortrait(ctx);return;}
            if(!Overlay) {
                Paint(ctx,contour,null,GreenAngularGeometry.Panel,new Rect(0,0,1,1),new Rect(0,0,size.x,size.y));
                var texture=BoxUnifiedArt.Texture("Components/"+Kind+"__fill");
                if(texture!=null){var uv=new Rect(0,0,1,1);float image=(float)texture.width/texture.height,aspect=size.x/size.y;
                    if(image>aspect){uv.width=aspect/image;uv.x=(1-uv.width)/2;}else{uv.height=image/aspect;uv.y=(1-uv.height)/2;}
                    Paint(ctx,contour,texture,Color.white,uv,new Rect(0,0,size.x,size.y));}
                if(borderIndices.Count>0){var border=BoxUnifiedArt.Texture("Components/"+Kind+"__border");var mesh=ctx.Allocate(borderPoints.Count,borderIndices.Count,border);
                    for(int i=0;i<borderPoints.Count;i++)mesh.SetNextVertex(new Vertex{position=new Vector3(borderPoints[i].x,borderPoints[i].y,Vertex.nearZ),uv=borderUvs[i],tint=Color.white});
                    foreach(int index in borderIndices)mesh.SetNextIndex((ushort)index);}
                if(Owner is Button){var fillContour=insetContour;
                    if(Owner.ClassListContains("is-selected")){var selection=GreenAngularGeometry.Lavender;selection.a=.28f;Paint(ctx,fillContour,null,selection,default,default);}
                    var feedback=FeedbackSource;
                    if(Owner.enabledInHierarchy&&feedback!=null){var wash=InteractionFeedback.Wash(feedback.hoverAmount,feedback.pressAmount,feedback.Focused);if(wash.a>.001f)Paint(ctx,fillContour,null,wash,default,default);}
                    else if(!Owner.enabledInHierarchy)Paint(ctx,fillContour,null,new Color(.32f,.37f,.46f,.18f),default,default);
                }
                return;
            }
            if(Owner==null)return;
            bool selected=Owner.ClassListContains("is-selected"),source=Owner.ClassListContains("is-swap-source");
            var inner=insetContour;
            if(selected)Stroke(ctx,inner,GreenAngularGeometry.Lavender,2.5f);
            if(source){var p=ctx.painter2D;p.strokeColor=GreenAngularGeometry.Gold;p.lineWidth=3;
                for(int i=0;i<inner.Length;i+=2){var v=inner[i];var a=inner[(i+inner.Length-1)%inner.Length];var b=inner[(i+1)%inner.Length];p.BeginPath();p.MoveTo(v+Vector2.ClampMagnitude(a-v,12));p.LineTo(v);p.LineTo(v+Vector2.ClampMagnitude(b-v,12));p.Stroke();}}
            if(Focused&&Owner.enabledInHierarchy)Stroke(ctx,focusContour,GreenAngularGeometry.Light,1.6f);
        }
        void DrawPortrait(MeshGenerationContext ctx) {
            if(Portrait==null)return;var texture=Portrait.texture;
            if(portraitContour==null||geometryPortrait!=Portrait){geometryPortrait=Portrait;Rect src=Portrait.textureRect;
                portraitUv=new Rect(src.x/texture.width,src.y/texture.height,src.width/texture.width,src.height/texture.height);
                portraitRect=new Rect(5,5,Mathf.Max(0,size.x-10),Mathf.Max(0,size.y-10));float aspect=src.width/src.height;
                if(portraitRect.width/portraitRect.height>aspect){float width=portraitRect.height*aspect;portraitRect.x+=(portraitRect.width-width)/2;portraitRect.width=width;}
                else{float height=portraitRect.width/aspect;portraitRect.y+=(portraitRect.height-height)/2;portraitRect.height=height;}
                portraitContour=GreenAngularGeometry.ClipRect(contour,portraitRect);
            }
            Paint(ctx,portraitContour,texture,Color.white,portraitUv,portraitRect);
        }
        static void Stroke(MeshGenerationContext ctx,Vector2[] points,Color color,float width) {
            var p=ctx.painter2D;p.BeginPath();p.MoveTo(points[0]);for(int i=1;i<points.Length;i++)p.LineTo(points[i]);p.ClosePath();p.strokeColor=color;p.lineWidth=width;p.Stroke();
        }
        static void Paint(MeshGenerationContext ctx,Vector2[] points,Texture texture,Color color,Rect uv,Rect dest) {
            if(points.Length<3)return;var indices=GreenAngularGeometry.Triangles(points);var mesh=ctx.Allocate(points.Length,indices.Length,texture);
            foreach(var p in points)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=color,uv=texture==null?Vector2.zero:new Vector2(uv.x+(p.x-dest.x)/dest.width*uv.width,uv.y+(1-(p.y-dest.y)/dest.height)*uv.height)});
            foreach(int index in indices)mesh.SetNextIndex((ushort)index);
        }
    }

    /// <summary>Still a native Button: the original controller owns its single click callback.</summary>
    [UxmlElement] public partial class BoxUnifiedButton:Button {
        readonly Label caption;
        public string Kind="secondary-action";
        public bool PlainChoice;
        string hitKind;Vector2 hitSize;Vector2[] hitContour;
        public string Caption=>caption?.text??string.Empty;
        public override string text {get=>base.text;set{base.text=string.Empty;if(caption!=null){caption.text=(value??string.Empty).Replace("▾",string.Empty).Trim();caption.style.display=string.IsNullOrEmpty(caption.text)?DisplayStyle.None:DisplayStyle.Flex;}}}
        public BoxUnifiedButton(){caption=new Label{pickingMode=PickingMode.Ignore};caption.AddToClassList("box-button-caption");Add(caption);}
        public override bool ContainsPoint(Vector2 p){if(PlainChoice)return base.ContainsPoint(p);if(hitContour==null||hitSize!=layout.size||hitKind!=Kind){hitSize=layout.size;hitKind=Kind;hitContour=BoxUnifiedArt.Contour(BoxUnifiedArt.Spec(Kind),layout.width,layout.height);}return GreenAngularGeometry.Contains(hitContour,p);}
    }
}
