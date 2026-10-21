using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Polygon surface shared by fill, textured art, state contours and native panel picking.</summary>
    [UxmlElement] public partial class AngularSurface : VisualElement {
        [UxmlAttribute] public string Shape {get;set;}="panel";
        [UxmlAttribute] public string Role {get;set;}="panel";
        public int ActiveTab;
        public Texture Art,SkinTexture;
        public AngularSurface FeedbackOverlay,FeedbackSource;
        public int StateRevision;
        public Func<Color> SelectionColor;
        public void InvalidateFeedback(){MarkDirtyRepaint();FeedbackOverlay?.MarkDirtyRepaint();}
        public float HoverAmount,PressAmount;
        (string,string,int,int,int) skinKey;
        Texture cachedSkin;
        IVisualElementScheduledItem stateAnimation;
        public void AnimateState(){
            stateAnimation?.Pause();float fromHover=HoverAmount,fromPress=PressAmount;double start=Time.realtimeSinceStartupAsDouble;
            stateAnimation=schedule.Execute(()=>{
                float elapsed=(float)(Time.realtimeSinceStartupAsDouble-start);
                HoverAmount=Mathf.Lerp(fromHover,Hovered?1:0,Mathf.Clamp01(elapsed/InteractionFeedback.HoverSeconds));
                PressAmount=Mathf.Lerp(fromPress,Pressed?1:0,Mathf.Clamp01(elapsed/InteractionFeedback.PressSeconds));
                InvalidateFeedback();if(elapsed>=InteractionFeedback.HoverSeconds)stateAnimation.Pause();
            }).Every(16);
        }
        public Rect ArtUv=new Rect(0,0,1,1);
        public bool ArtCrop=true,IsFocused,Hovered,Pressed,Selected,CapFocus,NoBorder,NoFill;
        public float ArtFadeRight,ArtInset;
        public bool Material=true;
        public Func<AngularSurface,bool> StateSelected;
        public AngularSurface(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;RegisterCallback<GeometryChangedEvent>(_=>MarkDirtyRepaint());}
        public override bool ContainsPoint(Vector2 p)=>GreenAngularGeometry.Contains(GreenAngularGeometry.Polygon(Shape,layout.width,layout.height,3,ActiveTab),p);
        public void Draw(MeshGenerationContext ctx){
            if(FeedbackSource!=null){HoverAmount=FeedbackSource.HoverAmount;PressAmount=FeedbackSource.PressAmount;IsFocused=FeedbackSource.IsFocused;}
            float w=layout.width,h=layout.height;if(w<=0||h<=0)return;var pts=GreenAngularGeometry.Polygon(Shape,w,h,h<12||w<12?0:3,ActiveTab);bool selected=Selected||(StateSelected?.Invoke(this)??false);
            Color fill=Role=="mode"?(selected?GreenAngularGeometry.Lavender:GreenAngularGeometry.Shell):Role=="primary"?GreenAngularGeometry.Primary:Role=="shell"?GreenAngularGeometry.Shell:Role=="raised"?GreenAngularGeometry.Raised:Role=="lavender"?GreenAngularGeometry.Lavender:Role=="hp"?GreenAngularGeometry.Hp:Role=="exp"?GreenAngularGeometry.Exp:Role=="track"?GreenAngularGeometry.Edge:GreenAngularGeometry.Panel;
            if(!enabledInHierarchy)fill=GreenAngularGeometry.Disabled;
            if(enabledInHierarchy)fill=Color.Lerp(fill,GreenAngularGeometry.Ink,PressAmount*InteractionFeedback.PressedInkAlpha);
            var key=(Shape,Role,Mathf.RoundToInt(w),Mathf.RoundToInt(h),ActiveTab);
            if(!key.Equals(skinKey)){skinKey=key;cachedSkin=Material&&!NoFill?LatestMenuCompanionAssets.Surface(Shape,Role,w,h,ActiveTab):null;}
            var skin=SkinTexture!=null?SkinTexture:cachedSkin;
            if(skin!=null)PaintTexture(ctx,pts,skin,w,h,Color.white);
            if((!NoFill&&skin==null)||Art!=null){Rect uv=ArtUv,display=new Rect(ArtInset,ArtInset,w-2*ArtInset,h-2*ArtInset);if(Art!=null){float img=(Art.width*uv.width)/(Art.height*uv.height),box=display.width/display.height;if(ArtCrop){if(img>box){float k=box/img;uv.x+=(uv.width-uv.width*k)/2;uv.width*=k;}else{float k=img/box;uv.y+=(uv.height-uv.height*k)/2;uv.height*=k;}}else{if(img>box){display.height=display.width/img;display.y=(h-display.height)/2;}else{display.width=display.height*img;display.x=(w-display.width)/2;}}}
                var painted=Art!=null?GreenAngularGeometry.ClipRect(pts,display):pts;
                if(Art!=null&&ArtFadeRight>0){float edge=w-ArtFadeRight;Paint(ctx,GreenAngularGeometry.ClipRect(painted,new Rect(0,0,edge,h)),uv,display,fill,h,w);Paint(ctx,GreenAngularGeometry.ClipRect(painted,new Rect(edge,0,ArtFadeRight,h)),uv,display,fill,h,w);}else Paint(ctx,painted,uv,display,fill,h,w);}
            if(Material&&!NoFill&&Art==null&&skin==null&&w>=110&&h>=40){var grain=LatestMenuCompanionAssets.Load("Assets/Art/steel-enamel-r2");if(grain!=null)PaintTexture(ctx,pts,grain.texture,w,h,new Color(1,1,1,.13f));string motif=Role=="shell"?"shell":Role=="primary"?"button":"content";var engraving=LatestMenuCompanionAssets.Load("Assets/Motifs/"+motif);if(engraving!=null)PaintTexture(ctx,pts,engraving.texture,w,h,Color.white);}
            if((!NoBorder&&(skin==null||selected))||(FeedbackSource!=null&&selected))Stroke(ctx,pts,selected?(SelectionColor?.Invoke()??GreenAngularGeometry.Lavender):GreenAngularGeometry.Edge,selected?2.6f:1.35f);
            if(FeedbackOverlay!=null)return; // State contour must be above full-bleed child artwork.
            if(enabledInHierarchy&&HoverAmount>.001f)Stroke(ctx,GreenAngularGeometry.Polygon(Shape,w,h,6),InteractionFeedback.Edge(HoverAmount),1.5f);
            if(enabledInHierarchy&&PressAmount>.001f){var ink=GreenAngularGeometry.Ink;ink.a=PressAmount*InteractionFeedback.PressedInkAlpha;PaintTexture(ctx,pts,null,w,h,ink);}
            if(skin!=null&&!enabledInHierarchy){var overlay=GreenAngularGeometry.Disabled;overlay.a=.3f;PaintTexture(ctx,pts,null,w,h,overlay);}
            if(IsFocused&&enabledInHierarchy){var p=ctx.painter2D;p.strokeColor=GreenAngularGeometry.Gold;p.lineWidth=2;
                foreach(float side in new[]{0f,1f}){float x=side==0?10:w-10,dir=side==0?1:-1;p.BeginPath();p.MoveTo(new Vector2(x+dir*12,10));p.LineTo(new Vector2(x,10));p.LineTo(new Vector2(x,Mathf.Min(23,h/2)));p.Stroke();p.BeginPath();p.MoveTo(new Vector2(x,h-Mathf.Min(23,h/2)));p.LineTo(new Vector2(x,h-10));p.LineTo(new Vector2(x+dir*12,h-10));p.Stroke();}}

        }
        static void PaintTexture(MeshGenerationContext ctx,Vector2[] pts,Texture texture,float w,float h,Color tint){var tri=GreenAngularGeometry.Triangles(pts);if(tri.Length==0)return;var mesh=ctx.Allocate(pts.Length,tri.Length,texture);foreach(var p in pts)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=tint,uv=new Vector2(p.x/w,1-p.y/h)});foreach(int i in tri)mesh.SetNextIndex((ushort)i);}
        static void Stroke(MeshGenerationContext c,Vector2[] pts,Color color,float width){var p=c.painter2D;p.BeginPath();p.MoveTo(pts[0]);for(int i=1;i<pts.Length;i++)p.LineTo(pts[i]);p.ClosePath();p.lineWidth=width;p.strokeColor=color;p.Stroke();}
        void Paint(MeshGenerationContext ctx,Vector2[] pts,Rect uv,Rect display,Color fill,float h,float w){if(pts.Length<3)return;var tri=GreenAngularGeometry.Triangles(pts);if(tri.Length==0)return;var mesh=ctx.Allocate(pts.Length,tri.Length,Art);for(int i=0;i<pts.Length;i++){var p=pts[i];Color tint=Art!=null?Color.white:GreenAngularGeometry.Shade(fill,Mathf.Lerp(5f/255,-5f/255,p.y/h));if(Art!=null&&ArtFadeRight>0)tint.a=Mathf.Clamp01((w-p.x)/ArtFadeRight);mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=tint,uv=new Vector2(uv.x+(p.x-display.x)/display.width*uv.width,uv.y+(1-(p.y-display.y)/display.height)*uv.height)});}foreach(int i in tri)mesh.SetNextIndex((ushort)i);}
        public static void SetArt(VisualElement e,Sprite sprite){if(e is AngularSurface a){var texture=sprite!=null?sprite.texture:null;var rect=sprite!=null?new Rect(sprite.textureRect.x/sprite.texture.width,sprite.textureRect.y/sprite.texture.height,sprite.textureRect.width/sprite.texture.width,sprite.textureRect.height/sprite.texture.height):new Rect(0,0,1,1);if(a.Art==texture&&a.ArtUv==rect)return;a.Art=texture;a.ArtUv=sprite!=null?new Rect(sprite.textureRect.x/sprite.texture.width,sprite.textureRect.y/sprite.texture.height,sprite.textureRect.width/sprite.texture.width,sprite.textureRect.height/sprite.texture.height):new Rect(0,0,1,1);a.MarkDirtyRepaint();e.style.backgroundImage=StyleKeyword.None;}else if(e!=null)e.style.backgroundImage=sprite!=null?new StyleBackground(sprite):StyleKeyword.None;}
        public static void SetArt(VisualElement e,RenderTexture tex){if(e is AngularSurface a){if(a.Art==tex)return;a.Art=tex;a.ArtUv=new Rect(0,0,1,1);a.MarkDirtyRepaint();e.style.backgroundImage=StyleKeyword.None;}else if(e!=null)e.style.backgroundImage=tex!=null?new StyleBackground(Background.FromRenderTexture(tex)):StyleKeyword.None;}
    }
    [UxmlElement] public partial class AngularButton : Button {
        public readonly AngularSurface Surface;
        readonly Label caption;
        // In angular scope the child caption paints above the polygon. The native
        // TextElement remains empty (including its getter), so it generates no glyphs.
        string logicalText=string.Empty;bool angularContext,contextConfigured;
        public override string text { get=>base.text;set{logicalText=value??string.Empty;base.text=angularContext?string.Empty:logicalText;if(caption!=null){caption.text=angularContext?logicalText:string.Empty;caption.style.display=angularContext&&!string.IsNullOrEmpty(logicalText)?DisplayStyle.Flex:DisplayStyle.None;}} }
        [UxmlAttribute] public string Shape {get=>Surface.Shape;set=>Surface.Shape=value;}
        [UxmlAttribute] public string Role {get=>Surface.Role;set=>Surface.Role=value;}
        public AngularButton(){Surface=new AngularSurface{name="angular-surface"};Surface.AddToClassList("angular-button-surface");Surface.style.position=Position.Absolute;Surface.style.left=Surface.style.top=Surface.style.right=Surface.style.bottom=0;Insert(0,Surface);Surface.StateSelected=_=>ClassListContains("is-selected")||ClassListContains("is-swap-source")||ClassListContains("is-swap-target");
            caption=new Label(string.Empty){pickingMode=PickingMode.Ignore};caption.AddToClassList("angular-button-caption");caption.style.position=Position.Absolute;caption.style.left=caption.style.top=caption.style.right=caption.style.bottom=0;caption.style.unityTextAlign=TextAnchor.MiddleCenter;caption.style.display=DisplayStyle.None;Add(caption);
            RegisterCallback<AttachToPanelEvent>(_=>{bool angular=false;for(var p=parent;p!=null;p=p.parent)if(p.ClassListContains("companion-design")||p.ClassListContains("ga-popup")){angular=true;break;}SetAngularContext(angular);InteractionFeedback.Bind(panel?.visualTree);});
            RegisterCallback<PointerEnterEvent>(_=>{Surface.Hovered=true;Surface.AnimateState();});
            RegisterCallback<PointerLeaveEvent>(_=>{Surface.Hovered=Surface.Pressed=false;Surface.AnimateState();});
            RegisterCallback<PointerDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=false;Surface.IsFocused=false;Surface.Pressed=true;Surface.AnimateState();},TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(_=>{Surface.Pressed=false;Surface.AnimateState();});
            RegisterCallback<KeyDownEvent>(_=>{InteractionFeedback.KeyboardNavigation=true;Surface.IsFocused=true;Surface.InvalidateFeedback();},TrickleDown.TrickleDown);
            RegisterCallback<NavigationMoveEvent>(_=>{InteractionFeedback.KeyboardNavigation=true;},TrickleDown.TrickleDown);
            RegisterCallback<FocusEvent>(_=>{Surface.IsFocused=InteractionFeedback.KeyboardNavigation;Surface.InvalidateFeedback();});
            RegisterCallback<BlurEvent>(_=>{Surface.IsFocused=false;Surface.InvalidateFeedback();});
            RegisterCallback<DetachFromPanelEvent>(_=>{Surface.Hovered=Surface.Pressed=Surface.IsFocused=false;Surface.HoverAmount=Surface.PressAmount=0;});
        }
        public void SetAngularContext(bool angular){if(contextConfigured&&angularContext==angular)return;contextConfigured=true;angularContext=angular;base.text=angular?string.Empty:logicalText;caption.text=angular?logicalText:string.Empty;Surface.style.display=angular?DisplayStyle.Flex:DisplayStyle.None;caption.style.display=angular&&!string.IsNullOrEmpty(logicalText)?DisplayStyle.Flex:DisplayStyle.None;}
        public AngularButton(Action click):this(){clicked+=click;}
        public override bool ContainsPoint(Vector2 p)=>Surface.resolvedStyle.display==DisplayStyle.None?base.ContainsPoint(p):GreenAngularGeometry.Contains(GreenAngularGeometry.Polygon(Shape,layout.width,layout.height),p);
    }
}



