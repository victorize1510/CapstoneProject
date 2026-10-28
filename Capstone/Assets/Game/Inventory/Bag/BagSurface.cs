using System;
using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.Inventory.Bag {
    public static class BagArt {
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static Texture2D Texture(string path){if(!textures.TryGetValue(path,out var texture)){texture=Resources.Load<Texture2D>("UI/BAG20261022/"+path);textures[path]=texture;}return texture;}
        public static void Icon(VisualElement element,string key){if(element==null)return;element.style.backgroundImage=new StyleBackground(Texture("Icons/"+key));element.pickingMode=PickingMode.Ignore;}
    }
    /// <summary>BAG-local state tokens and manifest layers; the shared Menu/Companion renderer is untouched.</summary>
    [UxmlElement] public partial class BagSurface:VisualElement {
        string kind="browser";Texture2D fill,motif,border;
        [UxmlAttribute]public string Kind{get=>kind;set{kind=value;fill=motif=border=null;MarkDirtyRepaint();}}
        public bool Hovered,Pressed,Focused;public float HoverAmount,PressAmount;
        public Func<bool> IsSelected;
        public PanelMaterialField Background;
        public bool ClearFace;
        IVisualElementScheduledItem animation;
        public string Shape=>kind.StartsWith("action")?"parallel":"panel";
        Vector2[] points;float pointWidth,pointHeight;string pointKind;
        Vector2[] Points(){float w=layout.width,h=layout.height;if(points!=null&&w==pointWidth&&h==pointHeight&&pointKind==kind)return points;pointWidth=w;pointHeight=h;pointKind=kind;float p=3,c=12;
            if(kind=="shell"||kind=="browser"||kind.StartsWith("popup"))c=18;
            if(kind=="category-row"||kind=="search"||kind=="sort"||kind=="filter"||kind=="view-toggle"||kind=="integer-field")c=8;
            c=Mathf.Min(c,Mathf.Min((h-2*p)/3,(w-2*p)/3));
            points=Shape=="parallel"?new[]{new Vector2(p+(h-2*p)*.212556f,p),new Vector2(w-p,p),new Vector2(w-p-(h-2*p)*.212556f,h-p),new Vector2(p,h-p)}:new[]{new Vector2(p+c,p),new Vector2(w-p-c,p),new Vector2(w-p,p+c),new Vector2(w-p,h-p-c),new Vector2(w-p-c,h-p),new Vector2(p+c,h-p),new Vector2(p,h-p-c),new Vector2(p,p+c)};return points;
        }
        public BagSurface(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;RegisterCallback<DetachFromPanelEvent>(_=>Stop());}
        public override bool ContainsPoint(Vector2 p)=>GreenAngularGeometry.Contains(Points(),p);
        public void Stop(){animation?.Pause();Hovered=Pressed=Focused=false;HoverAmount=PressAmount=0;MarkDirtyRepaint();}
        public void Animate(){animation?.Pause();float h=HoverAmount,p=PressAmount;double start=Time.realtimeSinceStartupAsDouble;animation=schedule.Execute(()=>{float dt=(float)(Time.realtimeSinceStartupAsDouble-start);HoverAmount=Mathf.Lerp(h,Hovered?1:0,Mathf.Clamp01(dt/InteractionFeedback.HoverSeconds));PressAmount=Mathf.Lerp(p,Pressed?1:0,Mathf.Clamp01(dt/InteractionFeedback.PressSeconds));MarkDirtyRepaint();if(dt>=InteractionFeedback.HoverSeconds)animation.Pause();}).Every(16);}
        void Draw(MeshGenerationContext ctx){
            float w=layout.width,h=layout.height;if(w<=0||h<=0)return;
            if(ClearFace){var wash=InteractionFeedback.Wash(HoverAmount,PressAmount,Focused);if(wash.a>.001f)Paint(ctx,Points(),null,w,h,wash);return;}
            if(Background==null){fill??=BagArt.Texture("Components/"+kind+"__fill");motif??=BagArt.Texture("Components/"+kind+"__motif");}border??=BagArt.Texture("Components/"+kind+"__border");
            var points=Points();bool dark=kind=="category-panel"||kind=="category-row"||kind=="action-primary"||kind=="view-toggle"||kind=="tooltip";
            bool native=fill!=null&&Mathf.Abs(fill.width-w)<1&&Mathf.Abs(fill.height-h)<1;
            if(Background!=null)Background.Draw(ctx,points);
            else if(native)Paint(ctx,points,fill,w,h,Color.white);
            else {Paint(ctx,points,null,w,h,kind=="action-danger"?GreenAngularGeometry.Hex("815C71"):dark?GreenAngularGeometry.Primary:GreenAngularGeometry.Panel);var grain=LatestMenuCompanionAssets.Load("Assets/Art/steel-enamel-r2");if(grain!=null)Paint(ctx,points,grain.texture,w,h,new Color(1,1,1,.13f));}
            if(IsSelected?.Invoke()==true)Paint(ctx,points,null,w,h,GreenAngularGeometry.Lavender);
            if(Background==null)Paint(ctx,points,motif,w,h,Color.white,kind=="item-row"||kind=="item-square");
            if(border!=null&&Mathf.Abs(border.width-w)<1&&Mathf.Abs(border.height-h)<1)Paint(ctx,points,border,w,h,Color.white);
            else {var edge=ctx.painter2D;edge.BeginPath();edge.MoveTo(points[0]);for(int i=1;i<points.Length;i++)edge.LineTo(points[i]);edge.ClosePath();edge.lineWidth=1.35f;edge.strokeColor=GreenAngularGeometry.Edge;edge.Stroke();}
            if(!enabledInHierarchy){var disabled=GreenAngularGeometry.Disabled;disabled.a=.75f;Paint(ctx,points,null,w,h,disabled);}
            else {float alpha=Mathf.Max(HoverAmount*(dark ? .16f : .10f),Focused ? .16f : 0);var wash=PressAmount>.001f?GreenAngularGeometry.Ink:dark?GreenAngularGeometry.Shell:GreenAngularGeometry.Primary;wash.a=Mathf.Max(alpha,PressAmount*InteractionFeedback.PressedInkAlpha);if(wash.a>.001f)Paint(ctx,points,null,w,h,wash);}
            if(Focused){var p=ctx.painter2D;p.BeginPath();p.MoveTo(points[0]);foreach(var pt in points)p.LineTo(pt);p.ClosePath();p.lineWidth=2;p.strokeColor=GreenAngularGeometry.Ink;p.Stroke();}
        }
        static void Paint(MeshGenerationContext ctx,Vector2[] points,Texture texture,float w,float h,Color tint,bool cover=false){
            if(texture==null&&tint==Color.white)return;var triangles=GreenAngularGeometry.Triangles(points);if(triangles.Length==0)return;
            var uv=new Rect(0,0,1,1);if(cover&&texture!=null){float image=texture.width/(float)texture.height,box=w/h;if(image>box){uv.width=box/image;uv.x=(1-uv.width)/2;}else{uv.height=image/box;uv.y=(1-uv.height)/2;}}
            var mesh=ctx.Allocate(points.Length,triangles.Length,texture);foreach(var p in points)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),uv=new Vector2(uv.x+p.x/w*uv.width,uv.y+(1-p.y/h)*uv.height),tint=tint});foreach(int i in triangles)mesh.SetNextIndex((ushort)i);
        }
    }
    [UxmlElement] public partial class BagButton:Button {
        public readonly BagSurface Surface;readonly Label caption;string logicalText="";
        [UxmlAttribute]public string Kind{get=>Surface.Kind;set=>Surface.Kind=value;}
        public override string text{get=>base.text;set{logicalText=value??"";base.text="";if(caption!=null){caption.text=logicalText;caption.style.display=string.IsNullOrEmpty(logicalText)?DisplayStyle.None:DisplayStyle.Flex;}}}
        public BagButton(){
            BagTooltip.Bind(this);
            AddToClassList("bag-button");Surface=new BagSurface{name="bag-surface"};Surface.AddToClassList("bag-button-surface");Insert(0,Surface);Surface.IsSelected=()=>ClassListContains("is-selected");
            caption=new Label{pickingMode=PickingMode.Ignore,name="bag-caption"};caption.AddToClassList("bag-button-caption");caption.style.display=DisplayStyle.None;Add(caption);
            RegisterCallback<PointerEnterEvent>(_=>{Surface.Hovered=true;Surface.Animate();});RegisterCallback<PointerLeaveEvent>(_=>{Surface.Hovered=Surface.Pressed=false;Surface.Animate();});
            RegisterCallback<PointerDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=false;Surface.Focused=false;Surface.Pressed=true;Surface.Animate();},TrickleDown.TrickleDown);RegisterCallback<PointerUpEvent>(_=>{Surface.Pressed=false;Surface.Animate();});
            RegisterCallback<KeyDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=true;Surface.Focused=true;Surface.MarkDirtyRepaint();},TrickleDown.TrickleDown);
            RegisterCallback<FocusEvent>(_=>{Surface.Focused=InteractionFeedback.KeyboardNavigation;Surface.MarkDirtyRepaint();});RegisterCallback<BlurEvent>(_=>{Surface.Focused=false;Surface.MarkDirtyRepaint();});RegisterCallback<DetachFromPanelEvent>(_=>Surface.Stop());
        }
        public BagButton(Action clicked):this(){this.clicked+=clicked;}
        public override bool ContainsPoint(Vector2 point)=>Surface.ContainsPoint(point);
    }
}
