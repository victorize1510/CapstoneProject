using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Presentation adapters only. No provider, inventory, stat formula or save ownership.</summary>
    public static class GreenAngularCompanionSkin {
        public static void Bind(VisualElement root,VisualElement design){
            if(design==null||design.Q("green-inspector-union")!=null)return;
            var union=new AngularSurface{name="green-inspector-union",Shape="panel",NoBorder=true,SkinTexture=UnifiedPanelHeader.Texture("Components/pet-inspector__fill")};Place(union,704,112,864,604);design.Insert(2,union);
            var frame=new AngularSurface{name="r3-inspector-frame",Shape="panel",NoFill=true,NoBorder=true,Art=UnifiedPanelHeader.Texture("Components/pet-inspector__border"),ArtCrop=false};Place(frame,0,0,864,604);union.Add(frame);
            var tabs=design.Q("companion-tabs");if(tabs!=null){var rail=new AngularSurface{name="r3-navigation-rail",Shape="panel",NoBorder=true,SkinTexture=InteractionR3Assets.Texture("Assets/Interaction/navigation-rail")};Place(rail,0,0,832,44);tabs.Insert(0,rail);
                // Pin every hit region to the manifest, independent of Button's native flex margins.
                string[] navigation={"overview","skills","growth"};for(int i=0;i<navigation.Length;i++){var item=tabs.Q<AngularButton>("pets-tab-"+navigation[i]);if(item!=null)Place(item,i*280,0,272,44);}
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
            var oldDock=dock.Q<AngularSurface>("green-background");if(oldDock!=null){oldDock.NoFill=oldDock.NoBorder=true;oldDock.Material=false;}
            var dockFace=new UnifiedPanelSurface{Kind="pet-dock",name="unified-pet-dock"};dockFace.style.position=Position.Absolute;dockFace.style.left=dockFace.style.right=dockFace.style.top=dockFace.style.bottom=0;dock.Insert(0,dockFace);
            foreach(var module in design.Query<VisualElement>(className:"companion-growth-module").ToList()){
                var oldReading=Add(module,"panel","raised");oldReading.NoFill=oldReading.NoBorder=true;
                UiSectionFrame.Attach(module);
                foreach(var cls in new[]{"companion-growth-copy","companion-growth-reason"})foreach(var content in module.Query<VisualElement>(className:cls).ToList()){content.pickingMode=PickingMode.Ignore;foreach(var leaf in content.Query<VisualElement>().ToList())leaf.pickingMode=PickingMode.Ignore;}
            }
            foreach(var track in design.Query<VisualElement>(className:"companion-bar-track").ToList())Add(track,"parallel","track");
            foreach(var track in design.Query<VisualElement>(className:"companion-stat-track").ToList())Add(track,"parallel","track");
            foreach(var fill in design.Query<VisualElement>(className:"companion-bar-fill").ToList())Add(fill,"parallel",fill.name.Contains("health")?"hp":"exp",true);
            foreach(var fill in design.Query<VisualElement>(className:"companion-stat-fill").ToList())Add(fill,"parallel","shell",true);
            var portrait=design.Q<AngularSurface>("pets-selected-portrait");if(portrait!=null){portrait.NoFill=portrait.NoBorder=true;portrait.ArtCrop=false;portrait.ArtFadeRight=32;}
            foreach(var id in new[]{"level-up-modal-dialog","heal-modal-dialog","pet-details-dialog","pets-rename-dialog"}){
                var dialog=root.Q(id);if(dialog==null)continue;
                // The overlay remains a full-screen input blocker; only the dialog is scaled.
                void Fit(){float w=root.resolvedStyle.width,h=root.resolvedStyle.height;if(w<=0||h<=0||float.IsNaN(w)||float.IsNaN(h))return;float dw=id.StartsWith("level")?1000:id.StartsWith("heal")?704:id.StartsWith("pet-details")?1104:704,dh=id.StartsWith("level")?736:id.StartsWith("heal")?584:id.StartsWith("pet-details")?740:464,s=Mathf.Min(w/1600,h/900);dialog.style.width=dw;dialog.style.height=dh;dialog.style.left=(w-dw*s)/2;dialog.style.top=(h-dh*s)/2;dialog.style.scale=new Scale(new Vector3(s,s,1));}
                root.RegisterCallback<GeometryChangedEvent>(_=>Fit());dialog.RegisterCallback<AttachToPanelEvent>(_=>Fit());Fit();
                if(id=="pet-details-dialog"){var scroll=dialog.Q<ScrollView>(className:"ga-details-scroll");var summary=dialog.Q(className:"ga-details-summary");if(summary!=null&&summary.parent!=dialog)dialog.Add(summary);}
                dialog.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
                var header=dialog.Q(className:"ga-modal-header");
                foreach(var field in dialog.Query<TextField>().ToList())if(field.parent is AngularSurface input){field.RegisterCallback<FocusInEvent>(_=>{input.IsFocused=true;input.MarkDirtyRepaint();});field.RegisterCallback<FocusOutEvent>(_=>{input.IsFocused=false;input.MarkDirtyRepaint();});}
                foreach(var label in dialog.Query<Label>().ToList())if(label.ClassListContains("ga-pet-name")||label.ClassListContains("ga-popup-title"))label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
                // Existing compact preview cards keep all their original bound labels.
                foreach(var cls in new[]{"ga-level-new","ga-level-evolution","ga-details-codex"})foreach(var card in dialog.Query<VisualElement>(className:cls).ToList())Add(card,"panel","raised");
                foreach(var a in dialog.Query<AngularSurface>().ToList()){
                    if(a.Role=="track"||a.name.EndsWith("-fill"))a.NoBorder=true;
                    if(a.ClassListContains("ga-details-basic")||a.ClassListContains("ga-details-stats")||a.ClassListContains("ga-details-advanced")||a.ClassListContains("ga-details-skills")||a.ClassListContains("ga-summary")||a.ClassListContains("ga-details-summary")||a.ClassListContains("ga-level-stats")||a.ClassListContains("ga-level-item")||a.ClassListContains("ga-heal-preview")||a.ClassListContains("ga-heal-item"))a.ReadingOnly=true;
                    if(a.ReadingOnly&&(id=="level-up-modal-dialog"||id=="heal-modal-dialog"))a.NoFill=true;
                }
                // One continuous reading plane avoids bright, disconnected patches behind each row.
                if(id=="level-up-modal-dialog"||id=="heal-modal-dialog"){
                    var reading=new AngularSurface{name="transaction-reading",ReadingOnly=true,HighContrastReading=true,NoBorder=true};
                    reading.style.position=Position.Absolute;reading.style.left=reading.style.right=reading.style.bottom=12;reading.style.top=80;dialog.Insert(0,reading);
                }
            }
            var stage=new UnifiedPanelSurface{Kind="pet-hero-stage",name="unified-pet-stage"};Place(stage,24,112,656,604);design.Insert(1,stage);
            ribbon.style.opacity=0;var oldHeader=design.Q("companion-header");
            UnifiedPanelHeader.Attach(design.parent,design,design.Q<Label>(className:"companion-title"),design.Q("companion-emblem"),design.Q<Button>("pets-back-button"));
            if(oldHeader!=null)oldHeader.style.display=DisplayStyle.None;
        }
        sealed class SlotBinding {
            public AngularButton Button;public VisualElement Empty,Info;public AngularSurface Feedback;
        }
        sealed class Bindings {
            public AngularSurface Union;
            public AngularButton[] Tabs;
            public AngularButton ModeToggle,Details;
            public VisualElement DockHost,SkillHost;
            public int DockChildren=-1;
            public readonly System.Collections.Generic.List<SlotBinding> Slots=new System.Collections.Generic.List<SlotBinding>(6);
            public UQueryState<AngularButton> SkillButtons;
        }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Bindings> bindingCache=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Bindings>();
        static Bindings Cache(VisualElement design)=>bindingCache.GetValue(design,d=>new Bindings {
            Union=d.Q<AngularSurface>("green-inspector-union"),
            Tabs=new[]{d.Q<AngularButton>("pets-tab-overview"),d.Q<AngularButton>("pets-tab-skills"),d.Q<AngularButton>("pets-tab-growth")},
            ModeToggle=d.Q<AngularButton>("pets-mode-toggle"),Details=d.Q<AngularButton>("pets-details-button"),DockHost=d.Q("pets-party-row")
        });
        static SlotBinding BindSlot(AngularButton slot,int index){
            slot.style.position=Position.Absolute;slot.style.left=index*222;slot.style.top=0;slot.Shape="panel";
            var avatar=slot.Q<AngularSurface>("portrait");
            if(avatar!=null){avatar.Shape="panel";if(avatar.Q("r3-dock-readability")==null){var overlay=new AngularSurface{name="r3-dock-readability",Shape="panel",NoFill=true,NoBorder=true,Art=InteractionR3Assets.Texture("Assets/Interaction/dock-readability-overlay")};Place(overlay,0,0,210,112);avatar.Add(overlay);}}
            var content=slot.Q("box-empty-content");
            if(content==null){content=new VisualElement{name="box-empty-content",pickingMode=PickingMode.Ignore};content.AddToClassList("box-empty-slot-content");var glyph=new Label("🐾"){pickingMode=PickingMode.Ignore};glyph.AddToClassList("box-empty-slot-icon");content.Add(glyph);var text=new Label("Trống"){pickingMode=PickingMode.Ignore};text.AddToClassList("box-empty-slot-text");content.Add(text);slot.Add(content);}
            var emptyGlyph=content.Q<Label>(className:"box-empty-slot-icon");if(emptyGlyph!=null){emptyGlyph.text="";emptyGlyph.style.backgroundColor=Color.clear;emptyGlyph.style.backgroundImage=new StyleBackground(Resources.Load<Sprite>("UI/Latest20261020/Icons/box-empty-preserved-style"));emptyGlyph.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);}
            var info=slot.Q(className:"pet-card-info");var level=slot.Q<Label>(className:"pet-card-level");if(level!=null&&info!=null&&level.parent!=info)info.Add(level);
            slot.Query<VisualElement>(className:"pet-card-bar-track").ForEach(track=>Add(track,"parallel","track",true));
            slot.Query<VisualElement>(className:"pet-card-hp-fill").ForEach(fill=>Add(fill,"parallel","hp",true));
            if(slot.Surface.FeedbackOverlay==null){var state=new AngularSurface{name="r3-slot-state",Shape="panel",NoFill=true,NoBorder=true,FeedbackSource=slot.Surface};state.StateSelected=_=>slot.ClassListContains("is-selected")||slot.ClassListContains("is-swap-source")||slot.ClassListContains("is-swap-target");state.SelectionColor=()=>slot.ClassListContains("is-swap-source")?GreenAngularGeometry.Gold:slot.ClassListContains("is-swap-target")?GreenAngularGeometry.Exp:GreenAngularGeometry.Lavender;Place(state,0,0,210,112);(avatar!=null?(VisualElement)avatar:slot).Add(state);slot.Surface.FeedbackOverlay=state;}
            return new SlotBinding{Button=slot,Empty=content,Info=info,Feedback=slot.Surface.FeedbackOverlay};
        }
        static void StyleSkillButton(AngularButton b){
            b.SetAngularContext(true);b.Surface.Role=b.name=="r3-equip-confirm"?"primary":b.ClassListContains("skills-filter-button")&&b.ClassListContains("is-selected")?"filter-selected":"raised";
            if(b.ClassListContains("skills-filter-button"))b.Surface.Material=false;
            b.Surface.Shape=b.ClassListContains("skills-filter-button")||b.name=="r3-equip-confirm"?"parallel":"panel";
        }
        static void BindSkills(Bindings cache,VisualElement skills){
            cache.SkillHost=skills;cache.SkillButtons=skills.Query<AngularButton>().Build();
            if(skills.Q("green-learned-panel")==null){var plate=new AngularSurface{name="green-learned-panel",Role="raised"};Place(plate,0,192,468,217);skills.Insert(0,plate);
                var filters=skills.Q(className:"skills-filter-row");if(filters!=null){var rail=new CompanionFilterRail(filters);Place(rail,0,136,468,44);skills.Add(rail);}}
            var detail=skills.Q(className:"companion-inline-detail");
            Add(detail,"panel","raised");UiPanelSections.Frame(detail);
        }
        public static void Update(VisualElement design,int tab){
            if(design==null)return;var cache=Cache(design);if(cache.Union!=null)cache.Union.ActiveTab=tab;
            for(int i=0;i<cache.Tabs.Length;i++)if(cache.Tabs[i] is AngularButton b){
                bool chosen=i==tab;string shape=i==0?"nav-first":i==2?"nav-last":"nav-middle",role=chosen?"lavender":"shell";
                if(b.Shape!=shape||b.Surface.Role!=role||b.Surface.NoFill==chosen||b.Surface.SkinTexture!=null){b.Shape=shape;b.Surface.NoFill=!chosen;b.Surface.Role=role;b.Surface.SkinTexture=null;b.Surface.MarkDirtyRepaint();}
                b.Surface.NoBorder=true;b.Surface.SelectionOutline=false;
            }
            if(cache.ModeToggle!=null)cache.ModeToggle.Surface.Role="mode";if(cache.Details!=null)cache.Details.Surface.NoFill=cache.Details.Surface.NoBorder=true;
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
        public static AngularSurface Add(VisualElement host,string shape="panel",string role="raised",bool noBorder=false){if(host==null)return null;var old=host.Q<AngularSurface>("green-background");if(old!=null&&old.parent==host)return old;var a=new AngularSurface{name="green-background",Shape=shape,Role=role,NoBorder=noBorder};a.ReadingOnly=host.ClassListContains("companion-content")||host.ClassListContains("ga-details-codex")||host.ClassListContains("companion-inline-detail")||host.ClassListContains("ga-level-new")||host.ClassListContains("ga-level-evolution");if(host.ClassListContains("ui-section"))a.NoFill=a.NoBorder=true;a.style.position=Position.Absolute;a.style.left=a.style.top=a.style.right=a.style.bottom=0;host.Insert(0,a);host.style.backgroundColor=Color.clear;host.style.backgroundImage=StyleKeyword.None;host.style.borderTopWidth=host.style.borderBottomWidth=host.style.borderLeftWidth=host.style.borderRightWidth=0;return a;}
    }
}





