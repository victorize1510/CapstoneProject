using System.Collections.Generic;
using Capstone.Game.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    internal static class MapR5View {
        public const float Native=1.2f;
        public static readonly Color DarkTint=new Color(GreenAngularGeometry.Primary.r/GreenAngularGeometry.Panel.r,GreenAngularGeometry.Primary.g/GreenAngularGeometry.Panel.g,GreenAngularGeometry.Primary.b/GreenAngularGeometry.Panel.b);
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly Dictionary<string,Texture2D> textures=new Dictionary<string,Texture2D>();
        public static readonly string[] GroupIcons={"npc","pet","enemy","boss","quest","shop","travel"};
        public static Sprite Sprite(string id) {if(!sprites.TryGetValue(id,out var s)||s==null){s=Resources.Load<Sprite>("UI/MapR5/"+id);sprites[id]=s;}return s;}
        public static Texture2D Texture(string id) {if(!textures.TryGetValue(id,out var t)||t==null){t=Resources.Load<Texture2D>("UI/MapR5/"+id);textures[id]=t;}return t;}
        public static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)=>MapUIFactory.Rect(parent,name,x*Native,y*Native,w*Native,h*Native);
        public static Image Icon(Transform parent,string name,string id,float x,float y,float size) {
            var image=MapUIFactory.Icon(parent,name,Sprite("Icons/"+id),x*Native,y*Native,size*Native,Color.white);
            var r=image.rectTransform;r.pivot=new Vector2(.5f,.5f);r.anchoredPosition+=new Vector2(size*Native/2,-size*Native/2);return image;
        }
        public static TMP_Text Text(Transform parent,WorldMapUITheme theme,string name,string value,float x,float y,float w,float h,float font,bool bold=false,Color? color=null,bool serif=false,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft) {
            var text=MapUIFactory.Text(parent,theme,name,value,x*Native,y*Native,w*Native,h*Native,font*Native,bold,align);
            text.font=serif&&theme.titleFont!=null?theme.titleFont:theme.font;text.color=color??GreenAngularGeometry.Ink;
            // TMP Middle and Geometry use different vertical references. Keep all Map
            // labels centred on their visible glyphs, including Vietnamese accents.
            if(align==TextAlignmentOptions.Center)text.alignment=TextAlignmentOptions.Midline;
            else if(align==TextAlignmentOptions.Left)text.alignment=TextAlignmentOptions.MidlineLeft;
            else if(align==TextAlignmentOptions.Right)text.alignment=TextAlignmentOptions.MidlineRight;
            text.enableAutoSizing=false;text.richText=false;
            text.margin=Vector4.one*(2*Native);return text;
        }
        public static MapR5Graphic Face(RectTransform rect,string shape,string component=null,Color? fill=null,bool border=true) {
            var face=rect.gameObject.AddComponent<MapR5Graphic>();face.Shape=shape;face.color=fill??Color.white;face.Texture=component!=null?Texture("Components/"+component+"__fill"):null;face.Border=border;face.raycastTarget=false;
            if(border&&face.Texture!=null) {
                face.Border=false;var line=MapUIFactory.Rect(rect,"Border",0,0,0,0);MapUIFactory.Stretch(line);
                var edge=line.gameObject.AddComponent<MapR5Graphic>();edge.Shape=shape;edge.color=Color.clear;edge.raycastTarget=false;
            }
            return face;
        }
        public static MapR5Graphic FaceChild(RectTransform parent,string shape,string component,Color? fill=null) {
            var rect=MapUIFactory.Rect(parent,"Face",0,0,0,0);MapUIFactory.Stretch(rect);return Face(rect,shape,component,fill);
        }
        public static Button Button(RectTransform rect,WorldMapUITheme theme,string caption,string icon=null,string shape="parallel",string component="map-filter",float font=20,bool dark=true) {
            var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;
            var contour=rect.gameObject.AddComponent<MapR5Contour>();contour.Shape=shape;
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
            rect.gameObject.AddComponent<MapR5SubmitGuard>();
            var face=FaceChild(rect,shape,component,dark&&component=="map-filter"?DarkTint:Color.white);
            float w=rect.sizeDelta.x/Native,h=rect.sizeDelta.y/Native;
            Image glyph=icon!=null?Icon(rect,"Icon",icon,12,(h-24)/2,24):null;
            var text=Text(rect,theme,"Label",caption,icon!=null?44:8,0,w-(icon!=null?52:16),h,font,true,GreenAngularGeometry.Light,false,TextAlignmentOptions.Center);
            var overlay=MapUIFactory.Rect(rect,"Feedback",0,0,0,0);MapUIFactory.Stretch(overlay);overlay.SetSiblingIndex(1);
            var feedback=Face(overlay,shape,null,Color.clear,false);
            var response=rect.gameObject.AddComponent<MapR5Feedback>();response.CaptionColor=dark?GreenAngularGeometry.Light:GreenAngularGeometry.Ink;response.Bind(button,feedback,glyph,text);return button;
        }
        public static string IconFor(MapMarkerType type) {
            var group=WorldMapUITheme.Group(type);
            return group.HasValue?GroupIcons[(int)group.Value]:type==MapMarkerType.Player?"player":"waypoint";
        }
        public static float MarkerSize(MapMarkerType type) {
            if(type==MapMarkerType.Boss)return 56;
            if(type==MapMarkerType.Pet||type==MapMarkerType.Waypoint||WorldMapUITheme.Group(type)==MapFilterGroup.Quest)return 48;
            return 52;
        }
    }
}
