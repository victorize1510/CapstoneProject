using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Presentation adapters only. No provider, inventory, stat formula or save ownership.</summary>
    public static class GreenAngularCompanionSkin {
        public static void Bind(VisualElement root,VisualElement design){
            if(design==null||design.Q("green-inspector-union")!=null)return;
            var union=new AngularSurface{name="green-inspector-union",Shape="panel",NoBorder=true,SkinTexture=InteractionR3Assets.Texture("Assets/Panels/inspector-fixed")};Place(union,704,96,864,620);design.Insert(2,union);
            var frame=new AngularSurface{name="r3-inspector-frame",Shape="panel",NoFill=true,NoBorder=true,Art=InteractionR3Assets.Texture("Frames/inspector-fixed")};Place(frame,0,0,864,620);union.Add(frame);
            var tabs=design.Q("companion-tabs");if(tabs!=null){var rail=new AngularSurface{name="r3-navigation-rail",Shape="panel",NoBorder=true,SkinTexture=InteractionR3Assets.Texture("Assets/Interaction/navigation-rail")};Place(rail,0,0,832,48);tabs.Insert(0,rail);
                // Pin every hit region to the manifest, independent of Button's native flex margins.
                string[] navigation={"overview","skills","growth"};for(int i=0;i<navigation.Length;i++){var item=tabs.Q<AngularButton>("pets-tab-"+navigation[i]);if(item!=null)Place(item,i*280,0,272,48);}
            }
            var ribbon=new AngularSurface{name="green-title-ribbon",Shape="parallel"};Place(ribbon,28,24,644,64);design.Insert(0,ribbon);
            
            foreach(var label in design.Query<Label>().ToList())if(label.ClassListContains("companion-title")||label.ClassListContains("companion-name")||label.ClassListContains("companion-section-label"))label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            foreach(var icon in design.Query<VisualElement>(className:"companion-icon").ToList())foreach(string cls in icon.GetClasses())if(cls.StartsWith("ci-")){
                string id=cls.Substring(3);if(icon.parent?.name=="pets-tab-growth")id="grow";if(id=="illustration")id="portrait";if(id=="view3d")id="view3d";if(id=="evolve")id="evolve";if(id=="details")id="profile";
                icon.style.backgroundImage=new StyleBackground(InteractionR3Assets.Load("Masks/"+id));
            }
            var emblem=design.Q("companion-emblem");if(emblem!=null)emblem.style.backgroundImage=new StyleBackground(Capstone.Game.HudSystem.CompanionArt.Load("Art/companion-title-emblem"));
            var sanctuary=design.Q(className:"companion-sanctuary");if(sanctuary!=null)sanctuary.style.backgroundImage=new StyleBackground(Capstone.Game.HudSystem.CompanionArt.Load("Art/companion-title-emblem"));
            Add(design.Q("companion-dock"),"panel","shell");Add(design.Q("companion-stats"));Add(design.Q("companion-skill-summary"));
            var dock=design.Q("companion-dock");var dockTitle=new AngularSurface{Shape="tab",Role="primary"};Place(dockTitle,16,-29,150,29);dock.Insert(1,dockTitle);
            foreach(var module in design.Query<VisualElement>(className:"companion-growth-module").ToList())Add(module,"panel","raised");
            foreach(var track in design.Query<VisualElement>(className:"companion-bar-track").ToList())Add(track,"parallel","track");
            foreach(var track in design.Query<VisualElement>(className:"companion-stat-track").ToList())Add(track,"parallel","track");
            foreach(var fill in design.Query<VisualElement>(className:"companion-bar-fill").ToList())Add(fill,"parallel",fill.name.Contains("health")?"hp":"exp",true);
            foreach(var fill in design.Query<VisualElement>(className:"companion-stat-fill").ToList())Add(fill,"parallel","shell",true);
            var portrait=design.Q<AngularSurface>("pets-selected-portrait");if(portrait!=null){portrait.NoFill=portrait.NoBorder=true;portrait.ArtCrop=false;portrait.ArtFadeRight=32;}
            foreach(var id in new[]{"level-up-modal-dialog","heal-modal-dialog","pet-details-dialog","pets-rename-dialog"}){
                var dialog=root.Q(id);if(dialog==null)continue;
                // The overlay remains a full-screen input blocker; only the dialog is scaled.
                void Fit(){float w=root.resolvedStyle.width,h=root.resolvedStyle.height;if(w<=0||h<=0||float.IsNaN(w)||float.IsNaN(h))return;float dw=id.StartsWith("level")?1000:id.StartsWith("heal")?704:id.StartsWith("pet-details")?1104:704,dh=id.StartsWith("level")?696:id.StartsWith("heal")?564:id.StartsWith("pet-details")?740:424,s=Mathf.Min(w/1600,h/900);dialog.style.width=dw;dialog.style.height=dh;dialog.style.left=(w-dw*s)/2;dialog.style.top=(h-dh*s)/2;dialog.style.scale=new Scale(new Vector3(s,s,1));}
                root.RegisterCallback<GeometryChangedEvent>(_=>Fit());dialog.RegisterCallback<AttachToPanelEvent>(_=>Fit());Fit();
                if(id=="pet-details-dialog"){var scroll=dialog.Q<ScrollView>(className:"ga-details-scroll");var summary=dialog.Q(className:"ga-details-summary");if(summary!=null&&summary.parent!=dialog)dialog.Add(summary);}
                dialog.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
                var header=dialog.Q(className:"ga-modal-header");
                foreach(var field in dialog.Query<TextField>().ToList())if(field.parent is AngularSurface input){field.RegisterCallback<FocusInEvent>(_=>{input.IsFocused=true;input.MarkDirtyRepaint();});field.RegisterCallback<FocusOutEvent>(_=>{input.IsFocused=false;input.MarkDirtyRepaint();});}
                foreach(var label in dialog.Query<Label>().ToList())if(label.ClassListContains("ga-pet-name")||label.ClassListContains("ga-popup-title"))label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
                // Existing compact preview cards keep all their original bound labels.
                foreach(var cls in new[]{"ga-level-new","ga-level-evolution","ga-details-codex"})foreach(var card in dialog.Query<VisualElement>(className:cls).ToList())Add(card,"panel","raised");
                foreach(var a in dialog.Query<AngularSurface>().ToList())if(a.Role=="track"||a.name.EndsWith("-fill"))a.NoBorder=true;
            }
        }
        sealed class SlotBinding {
            public AngularButton Button;public VisualElement Empty,Info;public AngularSurface Feedback;
        }
        sealed class Bindings {
            public AngularSurface Union;
            public AngularButton[] Tabs;
            public AngularButton Mode2D,Mode3D,Details;
            public VisualElement DockHost,SkillHost;
            public int DockChildren=-1;
            public readonly System.Collections.Generic.List<SlotBinding> Slots=new System.Collections.Generic.List<SlotBinding>(6);
            public UQueryState<AngularButton> SkillButtons;
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Bindings> bindingCache=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Bindings>();
        static Bindings Cache(VisualElement design)=>bindingCache.GetValue(design,d=>new Bindings {
            Union=d.Q<AngularSurface>("green-inspector-union"),
            Tabs=new[]{d.Q<AngularButton>("pets-tab-overview"),d.Q<AngularButton>("pets-tab-skills"),d.Q<AngularButton>("pets-tab-growth")},
            Mode2D=d.Q<AngularButton>("pets-mode-2d"),Mode3D=d.Q<AngularButton>("pets-mode-3d"),Details=d.Q<AngularButton>("pets-details-button"),DockHost=d.Q("pets-party-row")
        });
        static SlotBinding BindSlot(AngularButton slot,int index){
            slot.style.position=Position.Absolute;slot.style.left=index*222;slot.style.top=0;slot.Shape="panel";
            var avatar=slot.Q<AngularSurface>("portrait");
            if(avatar!=null){avatar.Shape="panel";if(avatar.Q("r3-dock-readability")==null){var overlay=new AngularSurface{name="r3-dock-readability",Shape="panel",NoFill=true,NoBorder=true,Art=InteractionR3Assets.Texture("Assets/Interaction/dock-readability-overlay")};Place(overlay,0,0,210,112);avatar.Add(overlay);}}
            var content=slot.Q("box-empty-content");
            if(content==null){content=new VisualElement{name="box-empty-content",pickingMode=PickingMode.Ignore};content.AddToClassList("box-empty-slot-content");var glyph=new Label("🐾"){pickingMode=PickingMode.Ignore};glyph.AddToClassList("box-empty-slot-icon");content.Add(glyph);var text=new Label("Trống"){pickingMode=PickingMode.Ignore};text.AddToClassList("box-empty-slot-text");content.Add(text);slot.Add(content);}
            var info=slot.Q(className:"pet-card-info");var level=slot.Q<Label>(className:"pet-card-level");if(level!=null&&info!=null&&level.parent!=info)info.Add(level);
            slot.Query<VisualElement>(className:"pet-card-bar-track").ForEach(track=>Add(track,"parallel","track",true));
            slot.Query<VisualElement>(className:"pet-card-hp-fill").ForEach(fill=>Add(fill,"parallel","hp",true));
            if(slot.Surface.FeedbackOverlay==null){var state=new AngularSurface{name="r3-slot-state",Shape="panel",NoFill=true,NoBorder=true,FeedbackSource=slot.Surface};state.StateSelected=_=>slot.ClassListContains("is-selected")||slot.ClassListContains("is-swap-source")||slot.ClassListContains("is-swap-target");state.SelectionColor=()=>slot.ClassListContains("is-swap-source")?GreenAngularGeometry.Gold:slot.ClassListContains("is-swap-target")?GreenAngularGeometry.Exp:GreenAngularGeometry.Lavender;Place(state,0,0,210,112);slot.Add(state);slot.Surface.FeedbackOverlay=state;}
            return new SlotBinding{Button=slot,Empty=content,Info=info,Feedback=slot.Surface.FeedbackOverlay};
        }
        static void StyleSkillButton(AngularButton b){
            b.SetAngularContext(true);b.Surface.Role=b.name=="r3-equip-confirm"?"primary":"raised";
            b.Surface.Shape=b.ClassListContains("skills-filter-button")||b.name=="r3-equip-confirm"?"parallel":"panel";
        }
        static void BindSkills(Bindings cache,VisualElement skills){
            cache.SkillHost=skills;cache.SkillButtons=skills.Query<AngularButton>().Build();
            if(skills.Q("green-learned-panel")==null){var plate=new AngularSurface{name="green-learned-panel",Role="raised"};Place(plate,0,180,468,229);skills.Insert(0,plate);
                var filters=skills.Q(className:"skills-filter-row");if(filters!=null){var viewport=new ScrollView(ScrollViewMode.Horizontal){name="green-filter-viewport"};viewport.horizontalScrollerVisibility=ScrollerVisibility.Hidden;viewport.verticalScrollerVisibility=ScrollerVisibility.Hidden;Place(viewport,0,146,468,34);skills.Add(viewport);viewport.Add(filters);filters.style.position=Position.Relative;filters.style.left=filters.style.top=0;filters.style.width=StyleKeyword.Auto;filters.style.height=34;filters.style.overflow=Overflow.Visible;}}
            Add(skills.Q(className:"companion-inline-detail"),"panel","raised");
        }
        public static void Update(VisualElement design,int tab){
            if(design==null)return;var cache=Cache(design);if(cache.Union!=null)cache.Union.ActiveTab=tab;
            for(int i=0;i<cache.Tabs.Length;i++)if(cache.Tabs[i] is AngularButton b){b.Shape="parallel";b.Surface.NoBorder=b.Surface.NoFill=true;b.Surface.Role="shell";var texture=i==tab?InteractionR3Assets.Texture("Assets/Interaction/navigation-selected"):null;if(b.Surface.SkinTexture!=texture){b.Surface.SkinTexture=texture;b.Surface.MarkDirtyRepaint();}}
            if(cache.Mode2D!=null)cache.Mode2D.Surface.Role="mode";if(cache.Mode3D!=null)cache.Mode3D.Surface.Role="mode";if(cache.Details!=null)cache.Details.Surface.NoFill=cache.Details.Surface.NoBorder=true;
            if(cache.DockHost!=null&&cache.DockChildren!=cache.DockHost.childCount){cache.DockChildren=cache.DockHost.childCount;cache.Slots.Clear();foreach(var child in cache.DockHost.Children())if(child is AngularButton slot)cache.Slots.Add(BindSlot(slot,cache.Slots.Count));}
            foreach(var bound in cache.Slots){var slot=bound.Button;bool empty=slot.ClassListContains("is-empty");bound.Empty.style.display=empty?DisplayStyle.Flex:DisplayStyle.None;if(bound.Info!=null)bound.Info.style.display=empty?DisplayStyle.None:DisplayStyle.Flex;
                if(bound.Feedback.parent!=null&&bound.Feedback.parent.IndexOf(bound.Feedback)!=bound.Feedback.parent.childCount-1)bound.Feedback.BringToFront();
                int revision=(slot.ClassListContains("is-selected")?1:0)|(slot.ClassListContains("is-swap-source")?2:0)|(slot.ClassListContains("is-swap-target")?4:0);
                if(bound.Feedback.StateRevision!=revision){bound.Feedback.StateRevision=revision;bound.Feedback.MarkDirtyRepaint();}
            }
            var skills=cache.SkillHost;
            if(skills==null||!skills.ClassListContains("companion-embedded-skills")||!design.Contains(skills))skills=design.Q(className:"companion-embedded-skills");
            if(skills!=null){if(cache.SkillHost!=skills)BindSkills(cache,skills);cache.SkillButtons.ForEach(StyleSkillButton);}else cache.SkillHost=null;
        }

        static void Place(VisualElement e,float x,float y,float w,float h){e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=w;e.style.height=h;}
        static void Leaf(VisualElement parent,float x,float y,float size){var leaf=new VisualElement{pickingMode=PickingMode.Ignore};Place(leaf,x,y,size,size);leaf.style.backgroundImage=new StyleBackground(Resources.Load<Sprite>("UI/GreenAngular/Art/corner_leaf"));parent.Add(leaf);}
        public static AngularSurface Add(VisualElement host,string shape="panel",string role="raised",bool noBorder=false){if(host==null)return null;var old=host.Q<AngularSurface>("green-background");if(old!=null&&old.parent==host)return old;var a=new AngularSurface{name="green-background",Shape=shape,Role=role,NoBorder=noBorder};a.style.position=Position.Absolute;a.style.left=a.style.top=a.style.right=a.style.bottom=0;host.Insert(0,a);host.style.backgroundColor=Color.clear;host.style.backgroundImage=StyleKeyword.None;host.style.borderTopWidth=host.style.borderBottomWidth=host.style.borderLeftWidth=host.style.borderRightWidth=0;return a;}
    }
}





