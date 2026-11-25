using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Decorative material, clipped to its host's existing contour. No picking, timers or data binding.</summary>
    public sealed class PanelMaterialField {
        public readonly struct ReadPlate {
            public readonly Rect Bounds; public readonly float Opacity, Feather;
            public ReadPlate(Rect bounds, float opacity, float feather=32) { Bounds=bounds; Opacity=opacity; Feather=feather; }
            public float Alpha(Vector2 p) {
                float edge=Mathf.Min(Mathf.Min(p.x-Bounds.xMin,Bounds.xMax-p.x),Mathf.Min(p.y-Bounds.yMin,Bounds.yMax-p.y));
                return Opacity*Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/Feather));
            }
        }
        public const string ResourceRoot="UI/Backgrounds20261023/";
        // Editor verification can render the unchanged original skin for an actual runtime A/B baseline.
        internal static bool UseUpdatedMaterials { get; set; } = true;
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        readonly Texture2D texture; readonly Vector2 fieldSize, origin;
        readonly Color baseColor; readonly bool rightAligned; readonly ReadPlate[] plates;
        Vector2[] contour; Vertex[] readVertices; ushort[] readIndices;
        public PanelMaterialField(string resource, Vector2 fieldSize, Vector2 origin, Color baseColor, bool rightAligned=false, params ReadPlate[] plates) {
            if(!textures.TryGetValue(resource,out texture)){texture=Resources.Load<Texture2D>(ResourceRoot+resource);textures[resource]=texture;}
            this.fieldSize=fieldSize;this.origin=origin;this.baseColor=baseColor;this.rightAligned=rightAligned;this.plates=plates;
        }
        public void Draw(MeshGenerationContext ctx,Vector2[] points) {
            if(points.Length<3)return;
            Paint(ctx,points,null,baseColor,false);
            // Use the unmodified material at alpha .5 exactly once. Never reduce host or content opacity.
            if(texture!=null)Paint(ctx,points,texture,new Color(1,1,1,.5f),true);
            if(plates.Length==0)return;
            if(contour!=points){contour=points;BuildReadMesh(points);}
            if(readIndices.Length==0)return;
            var mesh=ctx.Allocate(readVertices.Length,readIndices.Length);
            foreach(var vertex in readVertices)mesh.SetNextVertex(vertex);
            foreach(var index in readIndices)mesh.SetNextIndex(index);
        }
        // Shared shell UVs for BAG; xMaxYMid cover for PET. Circles preserve their aspect ratio.
        Vector2 Uv(Vector2 p) {
            float scale=Mathf.Max(fieldSize.x/texture.width,fieldSize.y/texture.height);
            var imageSize=new Vector2(texture.width*scale,texture.height*scale);
            var offset=new Vector2(rightAligned?fieldSize.x-imageSize.x:(fieldSize.x-imageSize.x)*.5f,(fieldSize.y-imageSize.y)*.5f);
            var uv=(p+origin-offset)/imageSize;return new Vector2(uv.x,1-uv.y);
        }
        void Paint(MeshGenerationContext ctx,Vector2[] points,Texture tex,Color tint,bool useUv) {
            var mesh=ctx.Allocate(points.Length,(points.Length-2)*3,tex);
            foreach(var p in points)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=tint,uv=useUv?Uv(p):Vector2.zero});
            for(int i=1;i<points.Length-1;i++){mesh.SetNextIndex(0);mesh.SetNextIndex((ushort)i);mesh.SetNextIndex((ushort)(i+1));}
        }
        void BuildReadMesh(Vector2[] points) {
            var vertices=new List<Vertex>();var indices=new List<ushort>();
            float width=0,height=0;foreach(var p in points){width=Mathf.Max(width,p.x);height=Mathf.Max(height,p.y);}
            const float step=16;
            for(float y=0;y<height;y+=step)for(float x=0;x<width;x+=step){
                var clipped=GreenAngularGeometry.ClipRect(points,new Rect(x,y,Mathf.Min(step,width-x),Mathf.Min(step,height-y)));
                if(clipped.Length<3)continue;
                float maxAlpha=0;foreach(var p in clipped)foreach(var plate in plates)maxAlpha=Mathf.Max(maxAlpha,plate.Alpha(p));
                if(maxAlpha<=0)continue;
                ushort start=(ushort)vertices.Count;
                foreach(var p in clipped){float alpha=0;foreach(var plate in plates)alpha=Mathf.Max(alpha,plate.Alpha(p));var color=baseColor;color.a=alpha;vertices.Add(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=color});}
                for(int i=1;i<clipped.Length-1;i++){indices.Add(start);indices.Add((ushort)(start+i));indices.Add((ushort)(start+i+1));}
            }
            readVertices=vertices.ToArray();readIndices=indices.ToArray();
        }
        public static PanelMaterialField Bag(string part) {
            var field=new Vector2(1552,748);
            if(part=="shell")return new PanelMaterialField("BAG_Material_1600x900",field,Vector2.zero,GreenAngularGeometry.Shell);
            if(part=="browser")return new PanelMaterialField("BAG_Material_1600x900",field,new Vector2(288,24),GreenAngularGeometry.Panel,false,
                new ReadPlate(new Rect(8,8,768,112),.82f),new ReadPlate(new Rect(8,526,768,94),.82f));
            if(part=="inspector")return new PanelMaterialField("BAG_Material_1600x900",field,new Vector2(1088,24),GreenAngularGeometry.Panel,false,
                new ReadPlate(new Rect(8,186,424,338),.88f,40));
            return null;
        }
        public static void AddPet(AngularSurface union) {
            var field=new PanelMaterialField("PET_Material_1600x900",new Vector2(864,620),Vector2.zero,GreenAngularGeometry.Panel,true,
                new ReadPlate(new Rect(10,62,844,90),.86f),new ReadPlate(new Rect(10,140,844,100),.8f));
            var layer=new VisualElement{name="pet-material-20261023",pickingMode=PickingMode.Ignore,focusable=false};
            layer.style.position=Position.Absolute;layer.style.left=layer.style.right=layer.style.top=layer.style.bottom=0;
            layer.generateVisualContent+=ctx=>{if(layer.layout.width>0&&layer.layout.height>0)field.Draw(ctx,GreenAngularGeometry.Polygon("panel",layer.layout.width,layer.layout.height,3));};
            union.Insert(0,layer);
        }
    }
}
