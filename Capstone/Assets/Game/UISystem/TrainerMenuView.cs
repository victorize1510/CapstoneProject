using System;
using Capstone.Game.ProfileSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.UISystem {
    /// <summary>Green Angular presentation. All routes/data remain owned by GameMenuController.</summary>
    public sealed class TrainerMenuView {
        public const float DesignWidth=1600,DesignHeight=900;
        readonly RectTransform design;
        readonly Text playerName,level,experience,area,playTime,party;
        readonly Image avatar,experienceFill;
        public RectTransform NavigationRoot {get;}
        public Image PetArtwork {get;}
        public GameObject DefaultFocus {get;}
        public TrainerMenuView(RectTransform parent,Sprite petArtwork,Action<string> navigate){
            design=Rect(parent,"TrainerMenuDesign");design.anchorMin=design.anchorMax=design.pivot=new Vector2(.5f,.5f);design.sizeDelta=new Vector2(1600,900);
            var ribbon=Surface(design,"TitleRibbon",28,24,644,64,"parallel",GreenAngularGeometry.Panel);Skin(ribbon,"component_header");
            var crest=Image(design,"TrainerEmblem",Capstone.Game.HudSystem.CompanionArt.Load("Art/companion-title-emblem"),Color.white);Place(crest.rectTransform,28,7,100,100);
            Label(design,"Title","HUẤN LUYỆN VIÊN",30,135,24,510,64,true);
            NavigationRoot=Rect(design,"TrainerMenuPanel");Place(NavigationRoot,744,28,824,844);
            Skin(Surface(NavigationRoot,"MenuShell",0,0,824,844,"panel",GreenAngularGeometry.Shell),"component_shell");
            var summary=Surface(NavigationRoot,"TrainerSummary",24,24,776,116,"panel",GreenAngularGeometry.Panel);Skin(summary,"component_player-info");
            var clip=Surface(summary.transform,"AvatarMask",28,26,64,64,"aperture",Color.white);clip.MaskOnly=true;clip.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            avatar=Image(clip.transform,"Avatar",null,Color.white);GuiKitControls.Stretch(avatar.rectTransform);avatar.preserveAspect=true;
            var frame=Image(summary.transform,"AvatarBorder",LatestMenuCompanionAssets.Load("Assets/Identity/avatar-frame"),Color.white);Place(frame.rectTransform,16,14,88,88);
            playerName=Label(summary.transform,"PlayerName","Người huấn luyện",32,128,8,520,44,true);
            level=Label(summary.transform,"Level","Lv. —",20,130,54,90,26);
            var track=Surface(summary.transform,"ExperienceTrack",230,64,400,12,"parallel",GreenAngularGeometry.Edge);track.Outline=false;
            var fillClip=Surface(track.transform,"FillMask",0,0,400,12,"parallel",Color.white);fillClip.MaskOnly=true;fillClip.gameObject.AddComponent<Mask>().showMaskGraphic=false;
            experienceFill=Image(fillClip.transform,"Fill",null,GreenAngularGeometry.Exp);GuiKitControls.Stretch(experienceFill.rectTransform);
            Label(summary.transform,"ExperienceCaption","EXP",15,130,83,72,24);
            experience=Label(summary.transform,"ExperienceValue","— / —",16,235,83,420,24);experience.alignment=TextAnchor.MiddleRight;
            var pet=Card("Pets",24,156,380,232);DefaultFocus=pet.gameObject;var petMask=ArtMask(pet);
            var stage=Image(petMask.transform,"PetArtBackground",LatestMenuCompanionAssets.Load("Assets/Art/scene-stage-r2"),Color.white);Place(stage.rectTransform,6,6,368,168);
            PetArtwork=Image(petMask.transform,"PetArtwork2D",petArtwork,Color.white);Place(PetArtwork.rectTransform,6,6,368,168);PetArtwork.preserveAspect=true;PetArtwork.enabled=petArtwork!=null;Finish(pet,"Thú đồng hành");
            var bag=Card("Inventory",420,156,380,232);var bagMask=ArtMask(bag);var pack=Image(bagMask.transform,"Backpack",LatestMenuCompanionAssets.Load("Assets/Art/scene-bag-r2"),Color.white);Place(pack.rectTransform,6,6,368,168);Finish(bag,"Túi đồ");
            Secondary("Profile","Hồ sơ","profile",24,404);Secondary("Quest","Nhiệm vụ","quest",420,404);Secondary("Map","Bản đồ","map",24,540);Secondary("Codex","Cẩm nang","book",420,540);
            var info=Surface(NavigationRoot,"QuickInfo",24,676,776,48,"panel",GreenAngularGeometry.Raised);Skin(info,"panel_panel_776_48");
            Symbol(info.transform,"AreaIcon","location",20,12,24,24,GreenAngularGeometry.Ink);area=Label(info.transform,"AreaValue","—",17,54,0,220,48);
            Symbol(info.transform,"TimeIcon","time",300,12,24,24,GreenAngularGeometry.Ink);playTime=Label(info.transform,"TimeValue","—",17,339,0,140,48);
            var paws=Image(info.transform,"PartyIcon",TrainerMenuArt.Polish("icon-party"),GreenAngularGeometry.Ink);Place(paws.rectTransform,540,12,24,24);party=Label(info.transform,"PartyValue","— / 6",17,584,0,140,48);
            ActionButton("Settings","Cài đặt","settings",24,740,GreenAngularGeometry.Shell);
            var back=ActionButton("Back","Trở lại","back",420,740,GreenAngularGeometry.Primary);
            var key=Label(back.transform,"Key","Tab",17,310,25,54,30);key.color=GreenAngularGeometry.Light;key.alignment=TextAnchor.MiddleCenter;
            var keyline=Surface(back.transform,"TabKeycap",310,25,54,30,"panel",Color.clear);keyline.transform.SetSiblingIndex(key.transform.GetSiblingIndex());
            GuiKitButton Card(string id,float x,float y,float w,float h,string shape="panel",Color? fill=null){var g=Surface(NavigationRoot,id+"Button",x,y,w,h,shape,fill??GreenAngularGeometry.Raised);Skin(g,"component_major-card");g.raycastTarget=true;var button=g.gameObject.AddComponent<GuiKitButton>();button.targetGraphic=g;button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>navigate(id));return button;}
            GreenAngularGraphic ArtMask(GuiKitButton b){var m=Surface(b.transform,"ArtMask",0,0,380,232,"panel",Color.white);m.MaskOnly=true;m.gameObject.AddComponent<Mask>().showMaskGraphic=false;return m;}
            void Finish(GuiKitButton button,string text){var cap=Image(button.transform,"CaptionFill",LatestMenuCompanionAssets.Load("Surfaces/major-front"),Color.white);Place(cap.rectTransform,0,0,380,232);FocusLayer(button);var label=Label(button.transform,"Label",text,24,23,178,298,42,true);Symbol(button.transform,"Chevron","chevron",348,188,16,24,GreenAngularGeometry.Light);button.Configure(GuiButtonKind.Secondary,label,null,null,GreenAngularGeometry.Ink);button.transition=Selectable.Transition.None;}
            void Secondary(string id,string caption,string icon,float x,float y){var b=Card(id,x,y,380,120,"parallel");var g=b.GetComponent<GreenAngularGraphic>();g.SurfaceTexture=LatestMenuCompanionAssets.Load("Assets/NavTiles/"+icon)?.texture;g.Outline=false;
                var badge=Image(b.transform,"IconDisc",LatestMenuCompanionAssets.Load("Icons/badge-"+icon),Color.white);Place(badge.rectTransform,25,30,60,60);FocusLayer(b);var text=Label(b.transform,"Label",caption,21,99,39,216,44);text.fontStyle=FontStyle.Bold;Symbol(b.transform,"Chevron","chevron",348,48,16,24,GreenAngularGeometry.Light);b.Configure(GuiButtonKind.Secondary,text,null,null,GreenAngularGeometry.Ink);b.transition=Selectable.Transition.None;}
            GuiKitButton ActionButton(string id,string caption,string icon,float x,float y,Color color){var b=Card(id,x,y,380,80,"parallel",color);Skin(b.GetComponent<GreenAngularGraphic>(),"parallel_"+(id=="Back"?"primary":"shell")+"_380_80");FocusLayer(b);var symbol=Symbol(b.transform,"Icon",icon,20,26,28,28,GreenAngularGeometry.Light);var label=Label(b.transform,"Label",caption,21,70,0,230,80);label.fontStyle=FontStyle.Bold;label.color=GreenAngularGeometry.Light;label.alignment=TextAnchor.MiddleCenter;b.Configure(GuiButtonKind.Secondary,label,symbol,null,GreenAngularGeometry.Light);b.transition=Selectable.Transition.None;return b;}
        }
        static void Skin(GreenAngularGraphic g,string name){g.SurfaceTexture=LatestMenuCompanionAssets.Load("Surfaces/"+name)?.texture;g.Outline=g.SurfaceTexture==null;}
        public void Fit(Vector2 viewport) {
            float scale = Mathf.Min(viewport.x / DesignWidth, viewport.y / DesignHeight);
            if (scale > 0 && !Mathf.Approximately(design.localScale.x, scale)) design.localScale = Vector3.one * scale;
        }
        public void Bind(PlayerProfileSnapshot data, bool hasProvider, int? partyCount) {
            Set(playerName, Ellipsis(playerName, string.IsNullOrWhiteSpace(data.DisplayName) ? "Người huấn luyện" : data.DisplayName));
            Set(level, data.Level > 0 ? "Lv. " + data.Level : "Lv. —");
            Set(experience, data.RequiredExperience > 0 ? data.CurrentExperience.ToString("N0") + " / " + data.RequiredExperience.ToString("N0") : "— / —");
            float progress = data.RequiredExperience > 0 ? Mathf.Clamp01((float)data.CurrentExperience / data.RequiredExperience) : 0;
            experienceFill.fillAmount=progress;var fr=experienceFill.rectTransform;fr.anchorMin=new Vector2(0,0);fr.anchorMax=new Vector2(0,1);fr.pivot=new Vector2(0,.5f);fr.sizeDelta=new Vector2(400*progress,0);fr.anchoredPosition=Vector2.zero;
            if (avatar.sprite != data.Avatar) avatar.sprite = data.Avatar;
            avatar.enabled = data.Avatar != null;
            Set(area, Ellipsis(area, string.IsNullOrWhiteSpace(data.CurrentArea) ? "Chưa rõ khu vực" : data.CurrentArea));
            var time = TimeSpan.FromSeconds(Math.Max(0,data.PlayTimeSeconds));
            Set(playTime, hasProvider ? ((int)time.TotalHours).ToString("00") + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00") : "—");
            Set(party, partyCount.HasValue ? partyCount.Value + " / 6" : "— / 6");
        }
        static string Ellipsis(Text label, string value) {
            var settings = label.GetGenerationSettings(Vector2.zero);
            float width = label.rectTransform.rect.width * label.pixelsPerUnit;
            var generator = label.cachedTextGeneratorForLayout;
            if (generator.GetPreferredWidth(value, settings) <= width) return value;
            int low = 0, high = value.Length;
            while (low < high) {
                int middle = (low + high + 1) / 2;
                if (generator.GetPreferredWidth(value.Substring(0, middle) + "…", settings) <= width) low = middle;
                else high = middle - 1;
            }
            return value.Substring(0, low) + "…";
        }
        static void Set(Text label, string value) { if (label.text != value) label.text = value; }
        static void FocusLayer(GuiKitButton b){var source=b.GetComponent<GreenAngularGraphic>();var overlay=Surface(b.transform,"PolygonStates",0,0,source.rectTransform.rect.width,source.rectTransform.rect.height,source.Shape,Color.clear);overlay.Outline=false;overlay.StateLayer=true;source.StateOverlay=overlay;}
        static RectTransform Rect(Transform parent,string name){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);return r;}
        static GreenAngularGraphic Surface(Transform parent,string name,float x,float y,float w,float h,string shape,Color fill){var g=Rect(parent,name).gameObject.AddComponent<GreenAngularGraphic>();g.Shape=shape;g.Fill=fill;g.raycastTarget=false;Place(g.rectTransform,x,y,w,h);return g;}
        static Image Image(Transform parent,string name,Sprite sprite,Color tint){var i=Rect(parent,name).gameObject.AddComponent<Image>();i.sprite=sprite;i.color=tint;i.raycastTarget=false;return i;}
        static Image Symbol(Transform parent,string name,string icon,float x,float y,float w,float h,Color tint){var i=Image(parent,name,LatestMenuCompanionAssets.Load("Masks/"+icon),tint);i.preserveAspect=true;Place(i.rectTransform,x,y,w,h);return i;}
        static Image Art(Transform parent,string name,string asset,float x,float y,float w,float h){var i=Image(parent,name,Resources.Load<Sprite>("UI/GreenAngular/"+asset),Color.white);i.preserveAspect=true;Place(i.rectTransform,x,y,w,h);return i;}
        static Text Label(Transform parent,string name,string value,int size,float x,float y,float w,float h,bool heading=false){var t=GuiKitControls.Label(parent,name,value,size);t.font=heading?TrainerMenuArt.Serif:MenuTypography.Font;t.color=GreenAngularGeometry.Ink;t.fontStyle=heading?FontStyle.Bold:FontStyle.Normal;t.raycastTarget=false;Place(t.rectTransform,x,y,w,h);return t;}
        static void Place(RectTransform r,float x,float y,float w,float h){r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);}
    }
}





