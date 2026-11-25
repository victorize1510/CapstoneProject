using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Selection feedback behind glyphs. Two short pulses, cached geometry, no gameplay state.</summary>
    public sealed class SkillWorkflowFeedback:VisualElement {
        static readonly ConditionalWeakTable<VisualElement,SkillWorkflowFeedback> layers=new ConditionalWeakTable<VisualElement,SkillWorkflowFeedback>();
        int role;float pulse;Vector2 size;Vector2[] contour;IVisualElementScheduledItem animation;
        public static void Set(VisualElement owner,int value){if(value==0&&!layers.TryGetValue(owner,out _))return;var layer=layers.GetValue(owner,e=>{var l=new SkillWorkflowFeedback();e.Insert(Mathf.Min(1,e.childCount),l);return l;});layer.SetRole(value);}
        public static void Stop(VisualElement owner){if(layers.TryGetValue(owner,out var layer))layer.SetRole(0);}
        public SkillWorkflowFeedback(){pickingMode=PickingMode.Ignore;name="skill-workflow-feedback";style.position=Position.Absolute;style.left=style.top=style.right=style.bottom=0;generateVisualContent+=Draw;
            RegisterCallback<DetachFromPanelEvent>(_=>{animation?.Pause();pulse=0;});}
        void SetRole(int value){if(role==value)return;role=value;animation?.Pause();pulse=0;style.display=role==0?DisplayStyle.None:DisplayStyle.Flex;MarkDirtyRepaint();
            if(value==1||value==2){double start=Time.realtimeSinceStartupAsDouble;animation=schedule.Execute(()=>{float dt=(float)(Time.realtimeSinceStartupAsDouble-start);pulse=dt<.7f?Mathf.Sin(dt/.35f*Mathf.PI)*Mathf.Sin(dt/.35f*Mathf.PI):0;MarkDirtyRepaint();if(dt>=.7f)animation.Pause();}).Every(16);}}
        void Draw(MeshGenerationContext c){if(role==0||layout.width<2||layout.height<2)return;if(contour==null||size!=layout.size){size=layout.size;contour=GreenAngularGeometry.Polygon("panel",size.x,size.y,5);}
            Color color=role==1?GreenAngularGeometry.Gold:role==2?GreenAngularGeometry.Lavender:GreenAngularGeometry.Exp;var p=c.painter2D;
            p.BeginPath();p.MoveTo(contour[0]);for(int i=1;i<contour.Length;i++)p.LineTo(contour[i]);p.ClosePath();var wash=color;wash.a=role==3?.06f:.12f+pulse*.08f;p.fillColor=wash;p.Fill();
            var halo=color;halo.a=.1f+pulse*.12f;p.strokeColor=halo;p.lineWidth=5+pulse*2;p.Stroke();p.strokeColor=color;p.lineWidth=role==3?1.2f:2;p.Stroke();}
    }
}
