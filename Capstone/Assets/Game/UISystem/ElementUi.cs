using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Capstone.Game.HudSystem;
using Capstone.Game.Skills;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    public static class ElementUi {
        public sealed class Token {
            public readonly string id,name;public readonly PetElement element;public readonly Color color,foreground;
            public Token(string id,string name,PetElement element,string hex,bool light=false){this.id=id;this.name=name;this.element=element;ColorUtility.TryParseHtmlString(hex,out color);ColorUtility.TryParseHtmlString(light?"#F5F0E5":"#24282D",out foreground);}
        }
        public static readonly Token[] All={
            new("fire","Lửa",PetElement.Fire,"#EE9B58"),new("water","Nước",PetElement.Water,"#73B6E2"),
            new("grass","Cỏ",PetElement.Nature,"#9DCD75"),new("electric","Điện",PetElement.Electric,"#F0CD63"),
            new("fighting","Giác đấu",PetElement.Fighting,"#DB8174"),new("flying","Bay",PetElement.Flying,"#A0D5E8"),
            new("normal","Thường",PetElement.Normal,"#C5BCA9"),new("dark","Tối",PetElement.Dark,"#65517D",true),new("dragon","Rồng",PetElement.Dragon,"#655AB2",true)};
        static readonly Dictionary<string,Texture2D> cache=new();
        public static Texture2D Art(string key){if(!cache.TryGetValue(key,out var value))cache[key]=value=Resources.Load<Texture2D>("UI/ElementsR2/"+key);return value;}
        public static Token Find(PetElement element)=>Array.Find(All,t=>t.element==element);
        public static Token Named(string name)=>Array.Find(All,t=>string.Equals(t.name,name,StringComparison.OrdinalIgnoreCase));
        public static string Name(PetElement element)=>Find(element)?.name??"Chưa phân loại";
        public static void Label(Label label,PetElement element){if(label==null)return;var t=Find(element);label.text=t?.name??"Chưa phân loại";label.style.color=t?.foreground??GreenAngularGeometry.Ink;label.style.backgroundImage=t!=null?new StyleBackground(Art(t.id+"-fill")):StyleKeyword.None;label.style.paddingLeft=6;label.style.paddingRight=6;label.style.unityFontStyleAndWeight=FontStyle.Bold;}
        public static void Filter(Button button,Token token,bool selected){
            var face=button.Q<ElementTone>("element-tone");if(face==null){face=new ElementTone{name="element-tone"};button.Insert(Math.Min(1,button.childCount),face);Stretch(face);face.RegisterOwner(button);}
            face.Tint=selected?token.color:Color.Lerp(token.color,new Color32(111,121,139,255),.62f);face.MarkDirtyRepaint();
            var glyph=button.Q("element-glyph");if(glyph==null){glyph=new VisualElement{name="element-glyph",pickingMode=PickingMode.Ignore};button.Add(glyph);glyph.style.position=Position.Absolute;glyph.style.left=8;glyph.style.top=5;glyph.style.width=glyph.style.height=22;glyph.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);}
            glyph.style.backgroundImage=new StyleBackground(Art(token.id+"-glyph"));
            foreach(var label in button.Query<Label>().ToList())label.style.color=token.foreground;
            var caption=button.Q<Label>(className:"angular-button-caption");if(caption!=null){caption.style.color=token.foreground;caption.style.left=32;caption.style.right=4;caption.text=token.name;}
        }
        internal static void Stretch(VisualElement e){e.style.position=Position.Absolute;e.style.left=e.style.right=e.style.top=e.style.bottom=0;}
        public static void Details(VisualElement host,SkillHudData skill){
            var body=host.Q("skill-r2-metadata");if(body==null){body=new VisualElement{name="skill-r2-metadata",pickingMode=PickingMode.Ignore};host.Add(body);body.style.flexShrink=0;body.style.marginTop=6;body.style.marginBottom=6;}
            body.Clear();var data=skill.uiMetadata;
            if(data==null||data.damageKind==SkillDamageLabel.Unspecified)body.Add(Copy("Loại sát thương: chưa thiết lập"));
            else if(data.damageKind!=SkillDamageLabel.None)body.Add(Tag(data.damageKind==SkillDamageLabel.Physical?"Vật lý":"Phép",data.damageKind==SkillDamageLabel.Physical?"#EA7B78":"#75ADEB"));
            if(data?.effects==null||data.effects.Count==0)body.Add(Copy("Chưa có thông tin hiệu ứng"));
            else foreach(var effect in data.effects){var line=new VisualElement{pickingMode=PickingMode.Ignore};line.style.flexDirection=FlexDirection.Row;line.style.flexWrap=Wrap.Wrap;var info=Effect(effect.role,data.damageKind);line.Add(Tag(info.Item1,info.Item2));if(!string.IsNullOrWhiteSpace(effect.description))line.Add(Copy(effect.description));body.Add(line);}
            if(data?.facts!=null)foreach(var fact in data.facts)if(!string.IsNullOrWhiteSpace(fact.label)&&!string.IsNullOrWhiteSpace(fact.value))body.Add(Copy(fact.label+": "+fact.value));
        }
        static Label Copy(string text){var e=new Label(text){pickingMode=PickingMode.Ignore};e.style.whiteSpace=WhiteSpace.Normal;e.style.fontSize=16;e.style.color=GreenAngularGeometry.Ink;e.style.marginTop=3;return e;}
        static VisualElement Tag(string text,string hex){ColorUtility.TryParseHtmlString(hex,out var color);var box=new VisualElement{pickingMode=PickingMode.Ignore};box.style.height=26;box.style.minWidth=90;box.style.marginRight=6;box.style.marginBottom=3;var tone=new ElementTone{Tint=color};Stretch(tone);box.Add(tone);var label=Copy(text);label.style.marginTop=0;label.style.paddingLeft=10;label.style.paddingRight=10;label.style.unityTextAlign=TextAnchor.MiddleCenter;label.style.unityFontStyleAndWeight=FontStyle.Bold;label.style.color=(Color)(hex=="#60568D"?new Color32(245,240,229,255):new Color32(36,40,45,255));box.Add(label);return box;}
        static Tuple<string,string> Effect(SkillEffectLabel e,SkillDamageLabel kind){string[] names={"Sát thương","Buff","Debuff","Hồi máu","Khiên","Khống chế","Choáng","Làm chậm","Trói chân","Câm lặng","Theo thời gian","Hồi theo thời gian","Di chuyển"};string[] colors={kind==SkillDamageLabel.Physical?"#EA7B78":kind==SkillDamageLabel.Magical?"#75ADEB":"#C5BCA9","#B1D578","#BC91DB","#73CCA0","#83CDE3","#E9C06F","#F2D063","#91B8DD","#B7AA79","#60568D","#DE9B75","#A5D5BA","#73C5BF"};int i=Mathf.Clamp((int)e,0,names.Length-1);return Tuple.Create(names[i],colors[i]);}
    }
    public sealed class ElementTone:VisualElement {
        public Color Tint;bool hover,pressed,focus;Button owner;
        public ElementTone(){pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;}
        public void RegisterOwner(Button button){owner=button;button.RegisterCallback<PointerEnterEvent>(_=>{hover=true;MarkDirtyRepaint();});button.RegisterCallback<PointerLeaveEvent>(_=>{hover=pressed=false;MarkDirtyRepaint();});button.RegisterCallback<PointerDownEvent>(_=>{pressed=true;MarkDirtyRepaint();});button.RegisterCallback<PointerUpEvent>(_=>{pressed=false;MarkDirtyRepaint();});button.RegisterCallback<FocusEvent>(_=>{focus=true;MarkDirtyRepaint();});button.RegisterCallback<BlurEvent>(_=>{focus=false;MarkDirtyRepaint();});}
        void Draw(MeshGenerationContext context){float w=layout.width,h=layout.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<2||h<2)return;float cut=Mathf.Min(8,h*.22f);var pts=new[]{new Vector2(cut,0),new Vector2(w-cut,0),new Vector2(w,cut),new Vector2(w,h-cut),new Vector2(w-cut,h),new Vector2(cut,h),new Vector2(0,h-cut),new Vector2(0,cut)};var color=Color.Lerp(Tint,pressed?Color.black:Color.white,pressed?.12f:hover?.05f:0);if(owner!=null&&!owner.enabledInHierarchy)color=Color.Lerp(color,Color.gray,.5f);var mesh=context.Allocate(8,18);foreach(var p in pts)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=GreenAngularGeometry.Shade(color,Mathf.Lerp(3f/255,-3f/255,p.y/h))});for(ushort i=1;i<7;i++){mesh.SetNextIndex(0);mesh.SetNextIndex(i);mesh.SetNextIndex((ushort)(i+1));}var pen=context.painter2D;pen.BeginPath();pen.MoveTo(pts[0]);for(int i=1;i<8;i++)pen.LineTo(pts[i]);pen.ClosePath();pen.lineWidth=focus?2:1;pen.strokeColor=focus?GreenAngularGeometry.Gold:Color.Lerp(Tint,Color.black,.25f);pen.Stroke();}
    }
}
