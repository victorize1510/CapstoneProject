using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using V = Capstone.Game.HudSystem.GameplayHudVisuals;

namespace Capstone.Game.HudSystem {
    // R11 source: HUB_R11_Pet_Arc_Extended_2026-11-04. Only separated runtime layers.
    internal static class HubR11Art {
        const string Root = "UI/HubR11/";
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        public static readonly Color Text = new Color32(247,240,223,255), Gold = new Color32(211,189,134,255);
        public static Sprite Load(string path) {
            if (!sprites.TryGetValue(path, out var sprite)) sprites[path] = sprite = Resources.Load<Sprite>(Root + path);
            return sprite;
        }
        public static Image Layer(Transform parent, string path, string name = null) {
            var image = V.Image(parent, name ?? path.Substring(path.LastIndexOf('/')+1));
            image.sprite = Load(path); image.preserveAspect = false; V.Stretch(image.rectTransform); return image;
        }
        public static Text Label(Transform parent,string name,string value,int size,TextAnchor align=TextAnchor.MiddleCenter) {
            var label=V.Text(parent,name,value,size,align); label.color=Text;
            label.fontStyle=FontStyle.Bold; label.gameObject.AddComponent<Outline>().effectColor=new Color(.05f,.09f,.14f,.95f);
            label.GetComponent<Outline>().effectDistance=new Vector2(1.2f,-1.2f); return label;
        }
        public static Text Badge(Transform parent,string key) {
            var rect=V.Rect(parent,"KeyBadge"); V.Top(rect,24,65,32,24);
            Layer(rect,"Components/keycap__fill"); Layer(rect,"Components/keycap__border");
            var label=V.Text(rect,"Binding",key,20,TextAnchor.MiddleCenter);label.fontStyle=FontStyle.Bold;
            label.color=new Color32(23,39,47,255); V.Stretch(label.rectTransform);return label;
        }
        public static HubStateButton Button(Transform parent,string name,string kind,float size=80) {
            var rect=V.Rect(parent,name);rect.sizeDelta=new Vector2(size,size);
            var fill=Layer(rect,kind+"__fill");fill.raycastTarget=true;
            var hit=rect.gameObject.AddComponent<HubHitContour>();
            hit.Kind=kind.Contains("segment")?HubHitContour.Shape.Sector:kind.Contains("skill")?HubHitContour.Shape.Octagon:HubHitContour.Shape.Circle;
            Layer(rect,kind+"__border");
            var state=Layer(rect,kind+"__normal","Interaction");state.color=Color.clear;
            var button=rect.gameObject.AddComponent<HubStateButton>();button.targetGraphic=fill;button.Layer=state;button.Kind=kind;
            button.navigation=new Navigation{mode=Navigation.Mode.Automatic};return button;
        }
        public static RectTransform Mask(Transform parent,string kind,float size) {
            var mask=V.Rect(parent,"ArtworkMask");V.Place(mask,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(size,size));
            // Mask reads the Graphic on its own GameObject, never an Image on a child.
            var image=mask.gameObject.AddComponent<Image>();image.raycastTarget=false;image.sprite=Load(kind+"__mask");image.preserveAspect=false;
            mask.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;return mask;
        }
    }
    public sealed class HubStateButton : Button {
        internal Image Layer; internal string Kind;
        protected override void DoStateTransition(SelectionState state,bool instant) {
            if(Layer==null||string.IsNullOrEmpty(Kind))return;
            string suffix=state==SelectionState.Pressed?"pressed":state==SelectionState.Highlighted?"hover":state==SelectionState.Selected?"focus":state==SelectionState.Disabled?"disabled":"normal";
            Layer.sprite=HubR11Art.Load(Kind+"__"+suffix);
            Layer.color=state==SelectionState.Normal?Color.clear:Color.white;
        }
    }
    public sealed class HubHitContour : MonoBehaviour,ICanvasRaycastFilter {
        public enum Shape {Circle,Octagon,Sector} public Shape Kind;
        public bool IsRaycastLocationValid(Vector2 screen,Camera camera) {
            var rect=(RectTransform)transform;if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screen,camera,out var p))return false;
            var r=rect.rect;p-=r.center;
            if(Kind==Shape.Circle)return p.sqrMagnitude<=r.width*r.width*.25f;
            if(Kind==Shape.Octagon){p=new Vector2(Mathf.Abs(p.x),Mathf.Abs(p.y));return p.x<=r.width/2&&p.y<=r.height/2&&p.x+p.y<=r.width*.88f;}
            p*=96/r.width;p+=new Vector2(48,48);return HubSectorContour.Contains(p);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HubArcGraphic : MaskableGraphic {
        public float Radius=126,Width=6;float amount;public Color Fill=new Color32(212,123,119,255);
        public void SetAmount(float value){value=float.IsNaN(value)?0:Mathf.Clamp01(value);if(Mathf.Approximately(value,amount))return;amount=value;SetVerticesDirty();}
        protected override void OnPopulateMesh(VertexHelper mesh) {mesh.Clear();Arc(mesh,90,0,new Color(.09f,.13f,.22f,.68f));if(amount>0)Arc(mesh,90,90-90*amount,Fill);}
        void Arc(VertexHelper mesh,float start,float end,Color tint){int steps=Mathf.Max(2,Mathf.CeilToInt(Mathf.Abs(end-start)));int offset=mesh.currentVertCount;
            for(int i=0;i<=steps;i++){float a=Mathf.Lerp(start,end,i/(float)steps)*Mathf.Deg2Rad;var v=new Vector2(Mathf.Cos(a),Mathf.Sin(a));mesh.AddVert(v*(Radius-Width/2),tint,Vector2.zero);mesh.AddVert(v*(Radius+Width/2),tint,Vector2.zero);if(i>0){int j=offset+i*2;mesh.AddTriangle(j-2,j-1,j);mesh.AddTriangle(j,j-1,j+1);}}
            Cap(mesh,start,tint);Cap(mesh,end,tint);
        }
        void Cap(VertexHelper mesh,float angle,Color tint){var center=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad))*Radius;int n=mesh.currentVertCount;mesh.AddVert(center,tint,Vector2.zero);for(int i=0;i<=12;i++){float a=i*Mathf.PI/6;mesh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*Width/2,tint,Vector2.zero);if(i>0)mesh.AddTriangle(n,n+i,n+i+1);}}
    }
    // Bend every glyph quad; no bitmap labels and no straight text rotated as one object.
    public sealed class HubCurvedText : Text {
        public float Radius=142;
        protected override void OnPopulateMesh(VertexHelper mesh) {
            base.OnPopulateMesh(mesh);var vertex=default(UIVertex);
            for(int i=0;i+3<mesh.currentVertCount;i+=4){float x=0;for(int j=0;j<4;j++){mesh.PopulateUIVertex(ref vertex,i+j);x+=vertex.position.x;}x/=4;
                float a=(45-x/Radius*Mathf.Rad2Deg)*Mathf.Deg2Rad;var radial=new Vector2(Mathf.Cos(a),Mathf.Sin(a));var tangent=new Vector2(Mathf.Sin(a),-Mathf.Cos(a));
                for(int j=0;j<4;j++){mesh.PopulateUIVertex(ref vertex,i+j);vertex.position=radial*(Radius+vertex.position.y+fontSize*.35f)+tangent*(vertex.position.x-x);mesh.SetUIVertex(vertex,i+j);}
            }
        }
    }
}

