using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.SettingsSystem {
    /// <summary>Paint-only R4 face. The original Slider owns capture, drag, keyboard and value events.</summary>
    public sealed class SettingsSliderFace : VisualElement {
        readonly Slider slider;
        bool hover,pressed,focused;
        static readonly ushort[] Indices={0,1,2,0,2,3};
        public float Normalized=>Mathf.InverseLerp(slider.lowValue,slider.highValue,slider.value);
        public float ThumbLeft=>Mathf.Max(0,contentRect.width-22)*Normalized;
        public SettingsSliderFace(Slider owner){
            slider=owner;name="settings-final-slider-face";pickingMode=PickingMode.Ignore;
            style.position=Position.Absolute;style.left=style.top=style.right=0;style.height=44;
            generateVisualContent+=Draw;
            slider.RegisterValueChangedCallback(_=>MarkDirtyRepaint());
            slider.RegisterCallback<GeometryChangedEvent>(_=>MarkDirtyRepaint());
            slider.RegisterCallback<PointerEnterEvent>(_=>{hover=true;MarkDirtyRepaint();});
            slider.RegisterCallback<PointerLeaveEvent>(_=>{hover=false;MarkDirtyRepaint();});
            slider.RegisterCallback<PointerDownEvent>(_=>{pressed=true;MarkDirtyRepaint();},TrickleDown.TrickleDown);
            slider.RegisterCallback<PointerUpEvent>(_=>{pressed=false;MarkDirtyRepaint();});
            slider.RegisterCallback<PointerCaptureOutEvent>(_=>{pressed=false;MarkDirtyRepaint();});
            slider.RegisterCallback<FocusInEvent>(_=>{focused=true;MarkDirtyRepaint();});
            slider.RegisterCallback<FocusOutEvent>(_=>{focused=false;MarkDirtyRepaint();});
            RegisterCallback<DetachFromPanelEvent>(_=>{hover=pressed=focused=false;});
        }
        public void Refresh()=>MarkDirtyRepaint();
        void Draw(MeshGenerationContext ctx){
            float width=contentRect.width;if(width<22)return;float t=Normalized;
            Strip(ctx,SettingsFinalArt.Texture("Controls/slider-rail"),width,width);
            float fill=t<=0?0:t>=1?width:11+(width-22)*t;
            Strip(ctx,SettingsFinalArt.Texture(slider.enabledInHierarchy?"Controls/slider-fill":"Controls/slider-fill-disabled"),width,fill);
            string state=!slider.enabledInHierarchy?"disabled":pressed?"pressed":focused&&InteractionFeedback.KeyboardNavigation?"focus":hover?"hover":"normal";
            Quad(ctx,SettingsFinalArt.Texture("Controls/slider-thumb-"+state),new Rect((width-22)*t,4,22,32),0,1);
        }
        static void Strip(MeshGenerationContext ctx,Texture2D image,float width,float clip){
            if(image==null||clip<=0)return;
            Segment(0,4,0,4f/image.width);Segment(4,width-4,4f/image.width,1-4f/image.width);Segment(width-4,width,1-4f/image.width,1);
            void Segment(float x0,float x1,float u0,float u1){float end=Mathf.Min(x1,clip);if(end<=x0)return;Quad(ctx,image,new Rect(x0,12,end-x0,14),u0,Mathf.Lerp(u0,u1,(end-x0)/(x1-x0)));}
        }
        static void Quad(MeshGenerationContext ctx,Texture image,Rect r,float u0,float u1){
            if(image==null)return;var mesh=ctx.Allocate(4,6,image);
            mesh.SetNextVertex(new Vertex{position=new Vector3(r.xMin,r.yMin,Vertex.nearZ),uv=new Vector2(u0,1),tint=Color.white});
            mesh.SetNextVertex(new Vertex{position=new Vector3(r.xMax,r.yMin,Vertex.nearZ),uv=new Vector2(u1,1),tint=Color.white});
            mesh.SetNextVertex(new Vertex{position=new Vector3(r.xMax,r.yMax,Vertex.nearZ),uv=new Vector2(u1,0),tint=Color.white});
            mesh.SetNextVertex(new Vertex{position=new Vector3(r.xMin,r.yMax,Vertex.nearZ),uv=new Vector2(u0,0),tint=Color.white});
            foreach(var index in Indices)mesh.SetNextIndex(index);
        }
    }
}
