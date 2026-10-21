using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.UISystem {
    /// <summary>Design package build-design.cjs points()/unionPath(), in top-left logical pixels.
    /// Shared by uGUI meshes, Toolkit meshes, artwork clipping, and hit testing. No nine-slice distortion.</summary>
    public static class GreenAngularGeometry {
        public static readonly Color Primary=Hex("3C4772"), Shell=Hex("66748E"), Panel=Hex("AEB8CD"), Raised=Hex("C1C3D3"), Ink=Hex("202B45"), Muted=Hex("39455E"), Gold=Hex("CCAD83"), Light=Hex("EDE9E1"), Disabled=Hex("9298AA"), Edge=Hex("8996B0"), Lavender=Hex("A596BB"), Hp=Hex("C47D8B"), Exp=Hex("76A6C3");
        public static Color Hex(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
        static readonly Dictionary<(string,float,float,float,int),Vector2[]> polygonCache=new Dictionary<(string,float,float,float,int),Vector2[]>();
        static readonly Dictionary<Vector2[],int[]> triangleCache=new Dictionary<Vector2[],int[]>();
        public static Vector2[] Polygon(string shape,float w,float h,float p=3,int tab=0){
            var key=(shape,w,h,p,tab);if(polygonCache.TryGetValue(key,out var points))return points;
            if(polygonCache.Count>1024)polygonCache.Clear();points=BuildPolygon(shape,w,h,p,tab);polygonCache[key]=points;return points;
        }
        static Vector2[] BuildPolygon(string shape,float w,float h,float p,int tab){
            if(shape=="aperture"){shape="avatar";p=0;}
            if(shape=="union")shape="panel"; // R3: fixed inspector, no tab-dependent geometry.
            float raw=shape=="parallel"||shape=="tab"?Mathf.Tan(Mathf.PI/15)*(h-2*p):shape=="skill-oct"?Mathf.Min(10,Mathf.Max(5,Mathf.Min(w,h)*.13f)):shape=="avatar"?10:w>=650&&h>=400?18:h<=64?8:12;
            float cut=Mathf.Max(0,Mathf.Min(raw,(w-2*p)/4,(h-2*p)/3));
            if(shape=="diamond")return new[]{new Vector2(w/2,p),new Vector2(w-p,h/2),new Vector2(w/2,h-p),new Vector2(p,h/2)};
            if(shape=="parallel")return new[]{new Vector2(p+cut,p),new Vector2(w-p,p),new Vector2(w-p-cut,h-p),new Vector2(p,h-p)};
            if(shape=="tab")return new[]{new Vector2(p+cut,p),new Vector2(w-p-cut,p),new Vector2(w-p,h-p),new Vector2(p,h-p)};
            return new[]{new Vector2(p+cut,p),new Vector2(w-p-cut,p),new Vector2(w-p,p+cut),new Vector2(w-p,h-p-cut),new Vector2(w-p-cut,h-p),new Vector2(p+cut,h-p),new Vector2(p,h-p-cut),new Vector2(p,p+cut)};
        }
        static Vector2 Intersect(Vector2 a,Vector2 u,Vector2 b,Vector2 v){float cross=u.x*v.y-u.y*v.x;var d=b-a;return a+u*((d.x*v.y-d.y*v.x)/cross);}
        public static bool Contains(Vector2[] p,Vector2 q){bool yes=false;for(int i=0,j=p.Length-1;i<p.Length;j=i++)if((p[i].y>q.y)!=(p[j].y>q.y)&&q.x<(p[j].x-p[i].x)*(q.y-p[i].y)/(p[j].y-p[i].y)+p[i].x)yes=!yes;return yes;}
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        public static int[] Triangles(Vector2[] p){
            if(triangleCache.TryGetValue(p,out var triangles))return triangles;
            var result=BuildTriangles(p);if(triangleCache.Count>2048)triangleCache.Clear();triangleCache[p]=result;return result;
        }
        static int[] BuildTriangles(Vector2[] p){
            var ids=new List<int>();for(int i=0;i<p.Length;i++)ids.Add(i);var result=new List<int>();int guard=0;
            while(ids.Count>2&&guard++<128){bool found=false;for(int j=0;j<ids.Count;j++){
                int a=ids[(j+ids.Count-1)%ids.Count],b=ids[j],c=ids[(j+1)%ids.Count];if(Cross(p[a],p[b],p[c])<=0.001f)continue;
                bool inner=false;foreach(int k in ids)if(k!=a&&k!=b&&k!=c&&Cross(p[a],p[b],p[k])>=0&&Cross(p[b],p[c],p[k])>=0&&Cross(p[c],p[a],p[k])>=0){inner=true;break;}
                if(inner)continue;result.Add(a);result.Add(b);result.Add(c);ids.RemoveAt(j);found=true;break;
            }if(!found)break;}return result.ToArray();
        }
        public static Color Shade(Color c,float amount){c.r=Mathf.Clamp01(c.r+amount);c.g=Mathf.Clamp01(c.g+amount);c.b=Mathf.Clamp01(c.b+amount);return c;}
        static readonly Dictionary<(Vector2[],Rect),Vector2[]> clipCache=new Dictionary<(Vector2[],Rect),Vector2[]>();
        public static Vector2[] ClipRect(Vector2[] polygon,Rect r){
            bool contains=true;foreach(var point in polygon)if(point.x<r.xMin||point.x>r.xMax||point.y<r.yMin||point.y>r.yMax){contains=false;break;}
            if(contains)return polygon;
            var key=(polygon,r);if(clipCache.TryGetValue(key,out var cached))return cached;
            var clipped=BuildClipRect(polygon,r);if(clipCache.Count>=512)clipCache.Clear();clipCache[key]=clipped;return clipped;
        }
        static Vector2[] BuildClipRect(Vector2[] polygon,Rect r){var p=new List<Vector2>(polygon);for(int edge=0;edge<4;edge++){var output=new List<Vector2>();if(p.Count==0)break;bool vertical=edge<2;float bound=edge==0?r.xMin:edge==1?r.xMax:edge==2?r.yMin:r.yMax;FuncPointInside inside=new FuncPointInside(vertical,bound,edge==0||edge==2);Vector2 a=p[p.Count-1];foreach(var b in p){bool ai=inside.Test(a),bi=inside.Test(b);if(ai!=bi){float denominator=vertical?b.x-a.x:b.y-a.y;float t=(bound-(vertical?a.x:a.y))/denominator;output.Add(Vector2.LerpUnclamped(a,b,t));}if(bi)output.Add(b);a=b;}p=output;}return p.ToArray();}
        readonly struct FuncPointInside{readonly bool vertical,greater;readonly float bound;public FuncPointInside(bool v,float b,bool g){vertical=v;bound=b;greater=g;}public bool Test(Vector2 p)=>greater?(vertical?p.x:p.y)>=bound:(vertical?p.x:p.y)<=bound;}
    }
}
