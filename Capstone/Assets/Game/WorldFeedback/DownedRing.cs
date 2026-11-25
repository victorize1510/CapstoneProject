using UnityEngine;
using UnityEngine.UI;
namespace Capstone.Game.WorldFeedback {
    // Phan bi xoa di tu 12 gio theo chieu kim dong ho. Khong co timer rieng.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DownedRing : MaskableGraphic {
        float remaining=1;
        public void SetRemaining(float value){value=Mathf.Clamp01(value);if(Mathf.Abs(value-remaining)<.0001f)return;remaining=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper vh){
            vh.Clear();float radius=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;
            int segments=Mathf.CeilToInt(64*remaining);if(segments==0)return;
            for(int i=0;i<=segments;i++){
                float angle=Mathf.Lerp(1-remaining,1,i/(float)segments)*Mathf.PI*2;
                var direction=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle));
                vh.AddVert(direction*radius,color,Vector2.zero);vh.AddVert(direction*Mathf.Max(0,radius-3),color,Vector2.zero);
                if(i>0){int n=i*2;vh.AddTriangle(n-2,n-1,n);vh.AddTriangle(n,n-1,n+1);}
            }
        }
    }
}
