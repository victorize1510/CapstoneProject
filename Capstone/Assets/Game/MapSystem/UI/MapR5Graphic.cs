using System.Collections;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    /// <summary>Map-only mesh: logical contours are rebuilt at the destination size, including the hollow map frame.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class MapR5Graphic : MaskableGraphic {
        public string Shape = "panel";
        public Texture2D Texture;
        public bool Border = true, Cutout;
        public Rect Hole = new Rect(16,16,1520,678.689956f);
        public float Padding = 3;
        public Color Edge = GreenAngularGeometry.Edge;
        public float Hover, Press;
        public bool Focused, Selected;
        Vector2 lastSize;
        string lastShape;
        float lastPadding;
        Vector2[] points;
        public override Texture mainTexture => Texture != null ? Texture : s_WhiteTexture;
        Vector2[] Points() {
            var size = rectTransform.rect.size / MapR5View.Native;
            if(points==null||lastSize!=size||lastShape!=Shape||lastPadding!=Padding) {
                lastSize=size;lastShape=Shape;lastPadding=Padding;
                points=Shape=="rect" ? new[]{Vector2.zero,new Vector2(size.x,0),size,new Vector2(0,size.y)} : GreenAngularGeometry.Polygon(Shape,size.x,size.y,Padding);
            }
            return points;
        }
        protected override void OnPopulateMesh(VertexHelper mesh) {
            mesh.Clear();var p=Points();if(p.Length<3||lastSize.x<1||lastSize.y<1)return;
            if(Cutout) {
                var inner=GreenAngularGeometry.Polygon("panel",Hole.width,Hole.height,0);
                for(int i=0;i<p.Length;i++) {int j=(i+1)%p.Length;Quad(mesh,p[i],p[j],inner[j]+Hole.position,inner[i]+Hole.position,color);}
                if(Border)Stroke(mesh,inner,Hole.position,new Color(.125f,.169f,.27f,.55f),1.1f);
            } else Fill(mesh,p,color);
            if(Border)Stroke(mesh,p,Vector2.zero,Edge,1.2f);
            if(Cutout&&Border) {
                Line(mesh,new Vector2(lastSize.x-9,26),new Vector2(lastSize.x-9,lastSize.y-26),new Color(.8f,.68f,.51f,.65f),1);
                Line(mesh,new Vector2(26,lastSize.y-9),new Vector2(lastSize.x-26,lastSize.y-9),new Color(.8f,.68f,.51f,.65f),1);
            }
            if(!Cutout) {
                if(Selected)Fill(mesh,p,new Color(.647f,.588f,.733f,.22f));
                var wash=InteractionFeedback.Wash(Hover,Press,Focused);if(wash.a>.001f)Fill(mesh,p,wash);
                if(Focused)Stroke(mesh,p,Vector2.zero,GreenAngularGeometry.Gold,2);
            }
        }
        void Fill(VertexHelper mesh,Vector2[] p,Color tint) {
            int start=mesh.currentVertCount;foreach(var v in p)mesh.AddVert(Vertex(v,tint));
            for(int i=1;i<p.Length-1;i++)mesh.AddTriangle(start,start+i,start+i+1);
        }
        UIVertex Vertex(Vector2 p,Color tint) {
            Rect rect=rectTransform.rect;var vertex=UIVertex.simpleVert;
            vertex.position=new Vector3(rect.xMin+p.x*MapR5View.Native,rect.yMax-p.y*MapR5View.Native);
            vertex.color=tint;
            float u=p.x/lastSize.x,v=1-p.y/lastSize.y;
            if(Texture!=null) {
                float image=(float)Texture.width/Texture.height,box=lastSize.x/lastSize.y;
                if(image>box)u=.5f+(u-.5f)*box/image;else v=.5f+(v-.5f)*image/box;
            }
            vertex.uv0=new Vector2(u,v);return vertex;
        }
        void Quad(VertexHelper mesh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color tint) {
            int start=mesh.currentVertCount;mesh.AddVert(Vertex(a,tint));mesh.AddVert(Vertex(b,tint));mesh.AddVert(Vertex(c,tint));mesh.AddVert(Vertex(d,tint));
            mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
        }
        void Stroke(VertexHelper mesh,Vector2[] p,Vector2 offset,Color tint,float width) {
            for(int i=0;i<p.Length;i++)Line(mesh,p[i]+offset,p[(i+1)%p.Length]+offset,tint,width);
        }
        void Line(VertexHelper mesh,Vector2 a,Vector2 b,Color tint,float width) {
            var normal=Vector2.Perpendicular((b-a).normalized)*width*.5f;Quad(mesh,a-normal,b-normal,b+normal,a+normal,tint);
        }
    }

}
