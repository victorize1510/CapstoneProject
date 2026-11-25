using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.UISystem {
    /// <summary>24 October presentation contract shared by Toolkit and the native-1920 Profile adapter.</summary>
    public static class UnifiedPanelHeader {
        public static readonly Rect Ribbon=new Rect(28,24,644,64),Emblem=new Rect(30,13,90,90),Title=new Rect(127,24,560,68),Back=new Rect(1408,34,158,46);
        public const float NativeProfileScale=1.2f,TitleFont=30,BackFont=21;
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static Texture2D Texture(string key){if(!textures.TryGetValue(key,out var image)){image=Resources.Load<Texture2D>("UI/Unified20261024/"+key);textures[key]=image;}return image;}
        public static Color Wash(float hover,float pressed,bool focus,bool disabled,bool selected){if(disabled){var c=GreenAngularGeometry.Disabled;c.a=.78f;return c;}if(selected)return GreenAngularGeometry.Lavender;return InteractionFeedback.Wash(hover,pressed,focus);}
        public static void Place(VisualElement e,Rect r){e.style.position=Position.Absolute;e.style.left=r.x;e.style.top=r.y;e.style.width=r.width;e.style.height=r.height;e.style.minWidth=e.style.minHeight=0;e.style.marginLeft=e.style.marginTop=e.style.marginRight=e.style.marginBottom=0;e.style.flexShrink=0;}
        public static void Page(VisualElement root){root.style.backgroundColor=GreenAngularGeometry.Shell;root.style.backgroundImage=new StyleBackground(Texture("Art/page"));root.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Cover);root.style.unityBackgroundImageTintColor=Color.white;}
        public static VisualElement Attach(VisualElement root,VisualElement canvas,Label title,VisualElement emblem,Button back){
            Page(root);root.AddToClassList("unified-panel");var sheet=Resources.Load<StyleSheet>("UI/Unified20261024/Unified");if(sheet!=null&&!root.styleSheets.Contains(sheet))root.styleSheets.Add(sheet);if(canvas.Q("unified-header")!=null)return canvas.Q("unified-header");
            var layer=new VisualElement{name="unified-header",pickingMode=PickingMode.Ignore};Place(layer,new Rect(0,0,1600,104));canvas.Add(layer);
            var ribbon=new UnifiedPanelSurface{Kind="title-ribbon",name="unified-title-ribbon"};Place(ribbon,Ribbon);layer.Add(ribbon);
            title??=new Label();title.name=string.IsNullOrEmpty(title.name)?"unified-title":title.name;title.RemoveFromHierarchy();layer.Add(title);Place(title,Title);title.pickingMode=PickingMode.Ignore;title.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);title.style.fontSize=TitleFont;title.style.unityFontStyleAndWeight=FontStyle.Bold;title.style.color=GreenAngularGeometry.Ink;title.style.unityTextAlign=TextAnchor.MiddleLeft;title.style.paddingLeft=title.style.paddingTop=title.style.paddingRight=title.style.paddingBottom=0;title.style.whiteSpace=WhiteSpace.NoWrap;title.style.opacity=1;
            emblem??=new VisualElement();emblem.RemoveFromHierarchy();emblem.name=string.IsNullOrEmpty(emblem.name)?"unified-emblem":emblem.name;layer.Add(emblem);Place(emblem,Emblem);emblem.pickingMode=PickingMode.Ignore;emblem.style.backgroundImage=new StyleBackground(Texture("Art/emblem"));emblem.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);emblem.style.unityBackgroundImageTintColor=Color.white;emblem.style.opacity=1;
            if(back!=null){back.RemoveFromHierarchy();layer.Add(back);Place(back,Back);back.text="";back.style.backgroundColor=Color.clear;back.style.backgroundImage=StyleKeyword.None;back.style.borderLeftWidth=back.style.borderTopWidth=back.style.borderRightWidth=back.style.borderBottomWidth=0;back.style.paddingLeft=back.style.paddingRight=back.style.paddingTop=back.style.paddingBottom=0;
                // Keep the original button/surface instances and hit polygon alive for its controller.
                foreach(var child in back.Children()){child.style.opacity=0;child.pickingMode=PickingMode.Ignore;}
                var face=new UnifiedPanelSurface{Kind="back-button",Owner=back,name="unified-back-face"};face.style.position=Position.Absolute;face.style.left=face.style.right=face.style.top=face.style.bottom=0;back.Add(face);
                var icon=new VisualElement{name="unified-back-icon",pickingMode=PickingMode.Ignore};Place(icon,new Rect(16,11,24,24));icon.style.backgroundImage=new StyleBackground(Texture("Icons/back"));icon.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);back.Add(icon);
                var caption=new Label("Trở lại"){name="unified-back-caption",pickingMode=PickingMode.Ignore};Place(caption,new Rect(44,0,106,46));caption.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);caption.style.fontSize=BackFont;caption.style.unityFontStyleAndWeight=FontStyle.Bold;caption.style.color=GreenAngularGeometry.Light;caption.style.unityTextAlign=TextAnchor.MiddleCenter;caption.style.paddingLeft=caption.style.paddingTop=caption.style.paddingRight=caption.style.paddingBottom=0;caption.style.whiteSpace=WhiteSpace.NoWrap;back.Add(caption);
                back.RegisterCallback<PointerEnterEvent>(_=>{face.Hovered=true;face.Animate();});back.RegisterCallback<PointerLeaveEvent>(_=>{face.Hovered=face.Pressed=false;face.Animate();});back.RegisterCallback<PointerDownEvent>(_=>{face.Focused=false;face.Pressed=true;face.Animate();});back.RegisterCallback<PointerUpEvent>(_=>{face.Pressed=false;face.Animate();});back.RegisterCallback<KeyDownEvent>(_=>{face.Focused=true;face.MarkDirtyRepaint();});back.RegisterCallback<FocusEvent>(_=>{face.Focused=InteractionFeedback.KeyboardNavigation;face.MarkDirtyRepaint();});back.RegisterCallback<BlurEvent>(_=>{face.Focused=false;face.MarkDirtyRepaint();});
                back.AddToClassList("unified-back");caption.style.color=StyleKeyword.Null;icon.style.unityBackgroundImageTintColor=StyleKeyword.Null;
            }
            return layer;
        }
    }
    public sealed class UnifiedPanelSurface:VisualElement {
        public string Kind="page-shell";public VisualElement Owner;public bool Hovered,Pressed,Focused,Selected;public float HoverAmount,PressAmount;
        IVisualElementScheduledItem animation;Vector2 size;Vector2[] contour;
        string Shape=>Kind=="title-ribbon"||Kind=="back-button"?"parallel":"panel";
        public UnifiedPanelSurface(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;RegisterCallback<DetachFromPanelEvent>(_=>{animation?.Pause();Hovered=Pressed=Focused=false;HoverAmount=PressAmount=0;});}
        Vector2[] Points(){if(contour==null||size!=layout.size){size=layout.size;contour=GreenAngularGeometry.Polygon(Shape,size.x,size.y,3);}return contour;}
        public void Animate(){animation?.Pause();float h=HoverAmount,p=PressAmount;double start=Time.realtimeSinceStartupAsDouble;animation=schedule.Execute(()=>{float dt=(float)(Time.realtimeSinceStartupAsDouble-start);HoverAmount=Mathf.Lerp(h,Hovered?1:0,Mathf.Clamp01(dt/InteractionFeedback.HoverSeconds));PressAmount=Mathf.Lerp(p,Pressed?1:0,Mathf.Clamp01(dt/InteractionFeedback.PressSeconds));MarkDirtyRepaint();if(dt>=InteractionFeedback.HoverSeconds)animation.Pause();}).Every(16);}
        void Draw(MeshGenerationContext ctx){if(layout.width<2||layout.height<2)return;var points=Points();Paint(ctx,points,UnifiedPanelHeader.Texture("Components/"+Kind+"__fill"),Color.white);Paint(ctx,points,UnifiedPanelHeader.Texture("Components/"+Kind+"__border"),Color.white);
            var wash=UnifiedPanelHeader.Wash(HoverAmount,PressAmount,Focused,Owner!=null&&!Owner.enabledInHierarchy,Selected);if(wash.a>.001f&&(Owner!=null||Selected))Paint(ctx,points,null,wash);
            if(Focused&&(Owner==null||Owner.enabledInHierarchy)){var p=ctx.painter2D;p.BeginPath();p.MoveTo(points[0]);for(int i=1;i<points.Length;i++)p.LineTo(points[i]);p.ClosePath();p.strokeColor=GreenAngularGeometry.Gold;p.lineWidth=2.6f;p.Stroke();}}
        void Paint(MeshGenerationContext ctx,Vector2[] points,Texture texture,Color tint){if(texture==null&&tint==Color.white)return;var indices=GreenAngularGeometry.Triangles(points);var mesh=ctx.Allocate(points.Length,indices.Length,texture);foreach(var p in points)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),uv=new Vector2(p.x/layout.width,1-p.y/layout.height),tint=tint});foreach(int i in indices)mesh.SetNextIndex((ushort)i);}
    }
}
