using System;
using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.QuestSystem.UI {
    public static class QuestSkinArt {
        public const string ResourceRoot="UI/Quest20261023/";
        static readonly Dictionary<string,Texture2D> cache=new Dictionary<string,Texture2D>();
        public static Texture2D Texture(string key){if(!cache.TryGetValue(key,out var texture)){texture=Resources.Load<Texture2D>(ResourceRoot+key);cache[key]=texture;}return texture;}
        public static void Icon(VisualElement host,Sprite sprite,string fallback){
            if(host==null)return;host.style.backgroundImage=StyleKeyword.None;
            var art=host.Q("quest-icon-art");if(art==null){art=new VisualElement{name="quest-icon-art",pickingMode=PickingMode.Ignore};art.AddToClassList("quest-icon-art");host.Add(art);}
            art.style.backgroundImage=sprite!=null?new StyleBackground(sprite):new StyleBackground(Texture("Icons/"+fallback));
            art.style.unityBackgroundImageTintColor=Color.white;
        }
    }
    /// <summary>Quest-local canonical mesh: same contour for texture, molding, state and pointer picking.</summary>
    [UxmlElement]public partial class QuestFrame:VisualElement {
        string kind="panel";VisualElement owner,canvas;Vector2[] points;Vector2 pointSize;string pointShape;
        Texture2D border;string borderKey;IVisualElementScheduledItem animation;
        Vertex[] readingVertices;ushort[] readingIndices;Vector2 readingSize;
        readonly SoftReadingMesh readingMesh=new SoftReadingMesh();
        UiSectionFrameMesh dossierFrame;
        public float HoverAmount,PressAmount;public bool Hovered,Pressed,Focused;
        public Func<float> Fraction;
        [UxmlAttribute]public string Kind{get=>kind;set{kind=value;MarkDirtyRepaint();}}
        public VisualElement StateOwner{get=>owner??this;set=>owner=value;}
        public string Shape=>kind=="canvas"||kind=="header"||kind=="reading"?"rect":kind=="tab"?"tab":kind=="button"||kind=="track"||kind=="pill"||kind=="progress"?"parallel":kind=="icon"?"skill-oct":"panel";
        public QuestFrame(){
            generateVisualContent+=Draw;RegisterCallback<GeometryChangedEvent>(_=>MarkDirtyRepaint());
            RegisterCallback<AttachToPanelEvent>(_=>{canvas=this;while(canvas!=null&&canvas.name!="quest-canvas")canvas=canvas.parent;});
            RegisterCallback<DetachFromPanelEvent>(_=>Stop());
            RegisterCallback<PointerEnterEvent>(_=>{if(kind!="card")return;Hovered=true;Animate();});
            RegisterCallback<PointerLeaveEvent>(_=>{if(kind!="card")return;Hovered=Pressed=false;Animate();});
            RegisterCallback<PointerDownEvent>(_=>{if(kind!="card")return;Pressed=true;Focused=false;Animate();});
            RegisterCallback<PointerUpEvent>(_=>{if(kind!="card")return;Pressed=false;Animate();});
            RegisterCallback<FocusEvent>(_=>{if(kind!="card")return;Focused=InteractionFeedback.KeyboardNavigation;MarkDirtyRepaint();});
            RegisterCallback<BlurEvent>(_=>{Focused=false;MarkDirtyRepaint();});
        }
        public Vector2[] Points(){var size=layout.size;var shape=Shape;if(points!=null&&size==pointSize&&shape==pointShape)return points;pointSize=size;pointShape=shape;
            points=shape=="rect"?new[]{Vector2.zero,new Vector2(size.x,0),size,new Vector2(0,size.y)}:GreenAngularGeometry.Polygon(shape,size.x,size.y,kind=="progress"?1:3);return points;}
        public override bool ContainsPoint(Vector2 p)=>float.IsFinite(layout.width)&&float.IsFinite(layout.height)&&layout.width>=2&&layout.height>=2&&GreenAngularGeometry.Contains(Points(),p);
        public void Stop(){animation?.Pause();Hovered=Pressed=Focused=false;HoverAmount=PressAmount=0;MarkDirtyRepaint();}
        public void Animate(){animation?.Pause();float fromHover=HoverAmount,fromPress=PressAmount;double start=Time.realtimeSinceStartupAsDouble;
            animation=schedule.Execute(()=>{float elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);HoverAmount=Mathf.Lerp(fromHover,Hovered?1:0,Mathf.Clamp01(elapsed/InteractionFeedback.HoverSeconds));PressAmount=Mathf.Lerp(fromPress,Pressed?1:0,Mathf.Clamp01(elapsed/InteractionFeedback.PressSeconds));MarkDirtyRepaint();if(elapsed>=InteractionFeedback.HoverSeconds)animation.Pause();}).Every(16);}
        void Draw(MeshGenerationContext ctx){
            if(kind=="canvas"&&ClassListContains("unified-canvas"))return;
            float w=layout.width,h=layout.height;if(w<2||h<2)return;var p=Points();var host=StateOwner;bool selected=host.ClassListContains("is-selected")||host.ClassListContains("is-tracked-action");
            if(kind=="dossier"||kind=="section"){(dossierFrame??=new UiSectionFrameMesh()).Paint(ctx,w,h,kind=="dossier");return;}
            if(kind=="icon")return; // Keep the original icon art; the decorative dark tile is not a control.
            bool dark=kind=="button"||kind=="track"||kind=="tab"||kind=="pill"||kind=="icon"||kind=="strip"||kind=="keycap";
            if(kind=="reading"||kind=="pill"){readingMesh.Paint(ctx,w,h,kind=="reading"?"rect":"parallel",false);return;}
            if(kind=="progress"){var track=GreenAngularGeometry.Ink;track.a=.18f;Paint(ctx,p,null,w,h,track);float value=Fraction?.Invoke()??0;if(value>0)Paint(ctx,GreenAngularGeometry.ClipRect(p,new Rect(0,0,w*value,h)),null,w,h,GreenAngularGeometry.Primary);return;}
            var baseColor=kind=="canvas"||kind=="header"?GreenAngularGeometry.Shell:dark?GreenAngularGeometry.Primary:kind=="card"||kind=="objective"||kind=="reward"?GreenAngularGeometry.Raised:GreenAngularGeometry.Panel;
            if(selected&&(kind=="tab"||kind=="track"))baseColor=GreenAngularGeometry.Panel;
            Paint(ctx,p,null,w,h,baseColor);
            var material=QuestSkinArt.Texture("Art/quest-material");
            float opacity=kind=="header"?.35f:dark?.18f:.5f;
            Paint(ctx,p,material,w,h,new Color(1,1,1,opacity),!dark&&kind!="header");
            float quiet=kind=="list"?.32f:kind=="detail"?.38f:kind=="card"?.78f:kind=="objective"||kind=="reward"?.8f:dark?.55f:0;
            if(quiet>0){var color=baseColor;color.a=quiet;Paint(ctx,p,null,w,h,color);}
            if(selected&&kind!="tab"){var color=GreenAngularGeometry.Lavender;color.a=.42f;Paint(ctx,p,null,w,h,color);}
            if(kind=="canvas"||kind=="header")return;
            string id=kind=="list"?"list-shell":kind=="detail"?"detail-shell":kind=="card"?"quest-card":kind=="objective"?"objective-row":kind=="reward"?"reward-card":kind=="track"?"track-button":kind=="button"?"action-button":kind=="icon"?"quest-icon-base":kind=="pill"?"type-pill":null;
            if((kind=="list"||kind=="detail")&&Mathf.Abs(h-770)<1)id+="-tracked";
            if(kind=="tab")id=host.name=="quest-tab-progress"?"tab-progress":host.name=="quest-tab-main"?"tab-main":host.name=="quest-tab-side"?"tab-side":host.name=="quest-tab-event"?"tab-event":"tab-other";
            string key=id+":"+Mathf.RoundToInt(w)+":"+Mathf.RoundToInt(h);if(key!=borderKey){borderKey=key;string unified=kind=="list"?"quest-list":kind=="detail"?"quest-detail":kind=="tab"?"quest-tab":null;border=unified!=null?UnifiedPanelHeader.Texture("Components/"+unified+"__border"):id!=null?QuestSkinArt.Texture("Components/"+id+"__border"):null;}
            // Native sizes can vary by a device-pixel rounding step after PanelSettings scaling.
            if(border!=null&&Mathf.Abs(border.width-w)<2.5f&&Mathf.Abs(border.height-h)<2.5f)Paint(ctx,p,border,w,h,Color.white,false,true);
            else{Molding(ctx,w,h);if(kind=="list"||kind=="detail"){var rail=GreenAngularGeometry.Gold;rail.a=.55f;Line(ctx,new Vector2(w-8,23),new Vector2(w-8,h-23),rail,1);Line(ctx,new Vector2(24,h-8),new Vector2(w-24,h-8),rail,1);}}
            if(kind=="card"&&selected)Line(ctx,new Vector2(6,15),new Vector2(6,h-15),GreenAngularGeometry.Gold,3);
            if(kind=="tab"&&selected)Line(ctx,new Vector2(12,h-1),new Vector2(w-12,h-1),GreenAngularGeometry.Gold,2);
            if(!host.enabledInHierarchy){var color=GreenAngularGeometry.Disabled;color.a=.66f;Paint(ctx,p,null,w,h,color);}
            else{var wash=PressAmount>.001f?GreenAngularGeometry.Ink:GreenAngularGeometry.Shell;wash.a=Mathf.Max(HoverAmount*.16f,PressAmount*.12f);if(wash.a>.001f)Paint(ctx,p,null,w,h,wash);}
            if(Focused&&host.enabledInHierarchy)Stroke(ctx,GreenAngularGeometry.Polygon(Shape,w,h,7),GreenAngularGeometry.Gold,1.8f);
        }
        void Paint(MeshGenerationContext ctx,Vector2[] p,Texture texture,float w,float h,Color tint,bool global=false,bool nativeBorder=false){
            if(texture==null&&tint==Color.white)return;var triangles=GreenAngularGeometry.Triangles(p);if(triangles.Length==0)return;
            var origin=global&&canvas!=null?canvas.WorldToLocal(this.LocalToWorld(Vector2.zero)):Vector2.zero;
            Rect uv=new Rect(0,0,1,1);if(texture!=null&&!global&&!nativeBorder){float image=texture.width/(float)texture.height,box=w/h;if(image>box){uv.width=box/image;uv.x=(1-uv.width)/2;}else{uv.height=image/box;uv.y=(1-uv.height)/2;}}
            var mesh=ctx.Allocate(p.Length,triangles.Length,texture);foreach(var q in p)mesh.SetNextVertex(new Vertex{position=new Vector3(q.x,q.y,Vertex.nearZ),tint=tint,uv=global?new Vector2((q.x+origin.x)/1600,1-(q.y+origin.y)/900):new Vector2(uv.x+q.x/w*uv.width,uv.y+(1-q.y/h)*uv.height)});foreach(int i in triangles)mesh.SetNextIndex((ushort)i);
        }
        static void Stroke(MeshGenerationContext ctx,Vector2[] points,Color color,float width){var p=ctx.painter2D;p.BeginPath();p.MoveTo(points[0]);for(int i=1;i<points.Length;i++)p.LineTo(points[i]);p.ClosePath();p.strokeColor=color;p.lineWidth=width;p.Stroke();}
        void Molding(MeshGenerationContext ctx,float w,float h){
            void Band(float a,float b,Color color){var outer=GreenAngularGeometry.Polygon(Shape,w,h,a);var inner=GreenAngularGeometry.Polygon(Shape,w,h,b);var mesh=ctx.Allocate(outer.Length*2,outer.Length*6);for(int i=0;i<outer.Length;i++){mesh.SetNextVertex(new Vertex{position=new Vector3(outer[i].x,outer[i].y,Vertex.nearZ),tint=color});mesh.SetNextVertex(new Vertex{position=new Vector3(inner[i].x,inner[i].y,Vertex.nearZ),tint=color});}for(int i=0;i<outer.Length;i++){ushort a0=(ushort)(i*2),a1=(ushort)(i*2+1),b0=(ushort)(((i+1)%outer.Length)*2),b1=(ushort)(b0+1);mesh.SetNextIndex(a0);mesh.SetNextIndex(b0);mesh.SetNextIndex(a1);mesh.SetNextIndex(a1);mesh.SetNextIndex(b0);mesh.SetNextIndex(b1);}}
            Band(3,3.6f,GreenAngularGeometry.Raised);Band(3.6f,5.2f,GreenAngularGeometry.Shell);Band(5.2f,5.8f,GreenAngularGeometry.Ink);
        }
        static void Line(MeshGenerationContext ctx,Vector2 a,Vector2 b,Color color,float width){var p=ctx.painter2D;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.strokeColor=color;p.lineWidth=width;p.Stroke();}
        void DrawReading(MeshGenerationContext ctx,float w,float h){
            if(readingVertices==null||readingSize!=layout.size){readingSize=layout.size;var vertices=new List<Vertex>();var indices=new List<ushort>();const int nx=5,ny=5;float ex=Mathf.Min(9,w/2),ey=Mathf.Min(9,h/2);float[] xs={0,ex/2,ex,w-ex,w-ex/2,w},ys={0,ey/2,ey,h-ey,h-ey/2,h};
                for(int y=0;y<=ny;y++)for(int x=0;x<=nx;x++){var q=new Vector2(xs[x],ys[y]);float edge=Mathf.Min(Mathf.Min(q.x,w-q.x),Mathf.Min(q.y,h-q.y));var c=GreenAngularGeometry.Panel;c.a=.84f*Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/9));vertices.Add(new Vertex{position=new Vector3(q.x,q.y,Vertex.nearZ),tint=c});}
                for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){ushort a=(ushort)(y*(nx+1)+x),b=(ushort)(a+nx+1);indices.Add(a);indices.Add((ushort)(a+1));indices.Add(b);indices.Add((ushort)(a+1));indices.Add((ushort)(b+1));indices.Add(b);}readingVertices=vertices.ToArray();readingIndices=indices.ToArray();}
            var mesh=ctx.Allocate(readingVertices.Length,readingIndices.Length);foreach(var v in readingVertices)mesh.SetNextVertex(v);foreach(var i in readingIndices)mesh.SetNextIndex(i);
        }
    }
    public sealed class QuestProgressBar:ProgressBar {
        readonly QuestFrame surface;
        public QuestProgressBar(){surface=new QuestFrame{Kind="progress",pickingMode=PickingMode.Ignore};surface.AddToClassList("quest-button-surface");surface.Fraction=()=>highValue>lowValue?Mathf.Clamp01((value-lowValue)/(highValue-lowValue)):0;Insert(0,surface);this.RegisterValueChangedCallback(_=>surface.MarkDirtyRepaint());}
    }
    [UxmlElement]public partial class QuestButton:Button {
        public readonly QuestFrame Surface;readonly Label caption;string logicalText="";
        [UxmlAttribute]public string Kind{get=>Surface.Kind;set=>Surface.Kind=value;}
        public override string text{get=>base.text;set{logicalText=value??"";base.text="";if(caption!=null)caption.text=logicalText;}}
        public QuestButton(){
            Surface=new QuestFrame{name="quest-button-surface",pickingMode=PickingMode.Ignore,StateOwner=this};Surface.Kind="button";Surface.AddToClassList("quest-button-surface");Insert(0,Surface);
            caption=new Label{name="quest-button-caption",pickingMode=PickingMode.Ignore};caption.AddToClassList("quest-button-caption");Add(caption);
            RegisterCallback<PointerEnterEvent>(_=>{Surface.Hovered=true;Surface.Animate();});RegisterCallback<PointerLeaveEvent>(_=>{Surface.Hovered=Surface.Pressed=false;Surface.Animate();});
            RegisterCallback<PointerDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=false;Surface.Focused=false;Surface.Pressed=true;Surface.Animate();},TrickleDown.TrickleDown);RegisterCallback<PointerUpEvent>(_=>{Surface.Pressed=false;Surface.Animate();});
            RegisterCallback<KeyDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=true;Surface.Focused=true;Surface.MarkDirtyRepaint();},TrickleDown.TrickleDown);
            RegisterCallback<NavigationMoveEvent>(_=>InteractionFeedback.KeyboardNavigation=true,TrickleDown.TrickleDown);
            RegisterCallback<FocusEvent>(_=>{Surface.Focused=InteractionFeedback.KeyboardNavigation;Surface.MarkDirtyRepaint();});RegisterCallback<BlurEvent>(_=>{Surface.Focused=false;Surface.MarkDirtyRepaint();});RegisterCallback<DetachFromPanelEvent>(_=>Surface.Stop());
        }
        public override bool ContainsPoint(Vector2 p)=>Surface.ContainsPoint(p);
    }
}
