using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Capstone.Game.MapSystem.UI {
    /// <summary>Raycast and pointer math use the same chamfer as the stencil mask, not the bounding rectangle.</summary>
    public sealed class MapR5Contour : MonoBehaviour, ICanvasRaycastFilter {
        public string Shape="panel";
        public float Padding=3;
        public bool Viewport;
        internal static bool ContainsNormalized(Vector2 point) {
            float x=point.x*1520,y=(1-point.y)*678.689956f;
            return x>=0&&y>=0&&x<=1520&&y<=678.689956f&&x+y>=18&&1520-x+y>=18&&x+678.689956f-y>=18&&1520-x+678.689956f-y>=18;
        }
        public bool IsRaycastLocationValid(Vector2 point,Camera camera) {
            var rect=(RectTransform)transform;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,point,camera,out var local))return false;
            local=new Vector2(local.x-rect.rect.xMin,rect.rect.yMax-local.y);
            var size=rect.rect.size;
            if(Viewport) {
                if(local.x<0||local.y<0||local.x>size.x||local.y>size.y)return false;
                float c=18*size.x/1520;
                return local.x+local.y>=c&&size.x-local.x+local.y>=c&&local.x+size.y-local.y>=c&&size.x-local.x+size.y-local.y>=c;
            }
            return GreenAngularGeometry.Contains(GreenAngularGeometry.Polygon(Shape,size.x/MapR5View.Native,size.y/MapR5View.Native,Padding),local/MapR5View.Native);
        }
    }

}
