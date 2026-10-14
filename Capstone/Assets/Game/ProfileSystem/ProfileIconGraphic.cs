using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.ProfileSystem {
    // Small monochrome UI symbols, independent of font glyph coverage and resolution.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ProfileIconGraphic : MaskableGraphic {
        public enum Symbol { Emblem, Paw, Edit, Camera, Identity, Clock, Location, Calendar, Chart, Leaf, Book, Story, Boss, Swords }
        [SerializeField] Symbol symbol;
        VertexHelper mesh;
        public void Configure(Symbol value, Color tint) { symbol = value; color = tint; raycastTarget = false; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh) {
            vh.Clear(); mesh = vh;
            switch (symbol) {
                case Symbol.Paw:
                    Disc(.5f,.3f,.22f,.17f); Disc(.18f,.57f,.10f,.13f); Disc(.39f,.76f,.10f,.14f);
                    Disc(.64f,.76f,.10f,.14f); Disc(.85f,.57f,.10f,.13f); break;
                case Symbol.Emblem:
                    Polygon(new Vector2(.5f,.12f),new Vector2(.17f,.58f),new Vector2(.43f,.48f),new Vector2(.5f,.88f),new Vector2(.57f,.48f),new Vector2(.83f,.58f)); break;
                case Symbol.Edit:
                    Line(.25f,.25f,.73f,.73f,.16f); Polygon(new Vector2(.13f,.13f),new Vector2(.22f,.37f),new Vector2(.37f,.22f)); break;
                case Symbol.Camera:
                    Box(.12f,.24f,.88f,.74f); Box(.32f,.74f,.55f,.83f); Ring(.5f,.49f,.17f,.04f); Disc(.76f,.65f,.035f,.035f); break;
                case Symbol.Identity:
                    Box(.08f,.17f,.92f,.83f); Disc(.29f,.6f,.11f,.11f); Line(.16f,.34f,.42f,.34f,.13f);
                    Line(.55f,.62f,.8f,.62f); Line(.55f,.43f,.8f,.43f); break;
                case Symbol.Clock:
                    Ring(.5f,.5f,.37f); Line(.5f,.5f,.5f,.77f); Line(.5f,.5f,.7f,.37f); break;
                case Symbol.Location:
                    Ring(.5f,.62f,.24f,.075f); Line(.29f,.5f,.5f,.12f,.09f); Line(.71f,.5f,.5f,.12f,.09f); break;
                case Symbol.Calendar:
                    Box(.16f,.16f,.84f,.8f); Line(.16f,.63f,.84f,.63f); Line(.31f,.71f,.31f,.91f); Line(.69f,.71f,.69f,.91f);
                    for(int x=0;x<3;x++) for(int y=0;y<2;y++) Disc(.32f+x*.18f,.31f+y*.17f,.03f,.03f); break;
                case Symbol.Chart:
                    Line(.22f,.2f,.22f,.47f,.13f); Line(.5f,.2f,.5f,.68f,.13f); Line(.78f,.2f,.78f,.86f,.13f); break;
                case Symbol.Leaf:
                    Polygon(new Vector2(.18f,.28f),new Vector2(.25f,.7f),new Vector2(.84f,.9f),new Vector2(.72f,.36f),new Vector2(.35f,.23f));
                    Line(.16f,.13f,.7f,.74f,.04f); break;
                case Symbol.Book:
                    Box(.12f,.18f,.46f,.79f); Box(.54f,.18f,.88f,.79f); Line(.5f,.14f,.5f,.8f,.04f); break;
                case Symbol.Story:
                    Box(.23f,.19f,.75f,.79f); Ring(.26f,.8f,.10f); Ring(.73f,.2f,.10f);
                    Line(.37f,.58f,.62f,.58f); Line(.37f,.43f,.62f,.43f); break;
                case Symbol.Boss:
                    Disc(.5f,.42f,.26f,.25f); Line(.3f,.59f,.14f,.86f,.10f); Line(.7f,.59f,.86f,.86f,.10f);
                    Ring(.38f,.48f,.045f,.03f); Ring(.62f,.48f,.045f,.03f); break;
                case Symbol.Swords:
                    Line(.22f,.2f,.78f,.82f,.11f); Line(.78f,.2f,.22f,.82f,.11f);
                    Line(.13f,.36f,.36f,.13f); Line(.64f,.13f,.87f,.36f); break;
            }
            mesh = null;
        }
        Vector2 Point(float x, float y) { Rect r=rectTransform.rect; float s=Mathf.Min(r.width,r.height); return r.center+new Vector2(x-.5f,y-.5f)*s; }
        void Triangle(Vector2 a,Vector2 b,Vector2 c) { int i=mesh.currentVertCount; mesh.AddVert(a,color,Vector2.zero); mesh.AddVert(b,color,Vector2.zero); mesh.AddVert(c,color,Vector2.zero); mesh.AddTriangle(i,i+1,i+2); }
        void Polygon(params Vector2[] points) { for(int i=1;i<points.Length-1;i++) Triangle(Point(points[0].x,points[0].y),Point(points[i].x,points[i].y),Point(points[i+1].x,points[i+1].y)); }
        void Box(float x,float y,float right,float top) { Line(x,y,right,y); Line(right,y,right,top); Line(right,top,x,top); Line(x,top,x,y); }
        void Line(float x,float y,float tx,float ty,float width=.055f) { Vector2 a=Point(x,y),b=Point(tx,ty),d=(b-a).normalized; Vector2 n=new Vector2(-d.y,d.x)*Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*width*.5f; Triangle(a-n,a+n,b+n); Triangle(a-n,b+n,b-n); }
        void Disc(float x,float y,float rx,float ry) { const int count=28; for(int i=0;i<count;i++){ float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count; Triangle(Point(x,y),Point(x+Mathf.Cos(a)*rx,y+Mathf.Sin(a)*ry),Point(x+Mathf.Cos(b)*rx,y+Mathf.Sin(b)*ry)); } }
        void Ring(float x,float y,float radius,float width=.055f) { const int count=40; for(int i=0;i<count;i++){ float a=i*Mathf.PI*2/count,b=(i+1)*Mathf.PI*2/count; Line(x+Mathf.Cos(a)*radius,y+Mathf.Sin(a)*radius,x+Mathf.Cos(b)*radius,y+Mathf.Sin(b)*radius,width); } }
    }
}
