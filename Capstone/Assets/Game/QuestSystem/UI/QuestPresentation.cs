using System.Runtime.CompilerServices;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.QuestSystem.UI {
    /// <summary>View-only Quest skin. No manager, reward transaction, tracker, map or save ownership.</summary>
    public sealed class QuestPresentation {
        static readonly ConditionalWeakTable<VisualElement,QuestPresentation> instances=new ConditionalWeakTable<VisualElement,QuestPresentation>();
        readonly VisualElement root,canvas;readonly ScrollView detail,list;readonly VisualElement content,empty,listEmpty;readonly UIDocument document;
        bool foreground;float previousPanelOrder,previousDocumentOrder;PanelSettings foregroundPanel;
        QuestPresentation(VisualElement panel,UIDocument document){this.document=document;root=panel;canvas=panel.Q("quest-canvas");detail=panel.Q<ScrollView>("quest-detail-scroll");list=panel.Q<ScrollView>("quest-list-scroll");content=panel.Q("quest-detail-content");empty=panel.Q("quest-detail-empty-wrapper");listEmpty=panel.Q("quest-list-empty-hint");
            canvas.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            foreach(var label in panel.Query<Label>().ToList())if(label.name=="quest-title-label"||label.name=="quest-detail-title")label.style.unityFontDefinition=FontDefinition.FromFont(TrainerMenuArt.Serif);
            var mark=panel.Q(className:"quest-header-mark");if(mark!=null)mark.style.backgroundImage=new StyleBackground(QuestSkinArt.Texture("Icons/quest"));
            foreach(var emblem in panel.Query<QuestFrame>().ToList())if(emblem.ClassListContains("quest-empty-emblem")||emblem.name=="quest-list-seal")QuestSkinArt.Icon(emblem,null,"quest");
            foreach(var heading in panel.Query<Label>(className:"quest-section-title").ToList()){var icon=new VisualElement{pickingMode=PickingMode.Ignore};icon.AddToClassList("quest-section-glyph");string key=heading.parent?.name=="quest-objective-heading"?"quest":heading.name=="quest-rewards-title"?"favorite":"book";icon.style.backgroundImage=new StyleBackground(QuestSkinArt.Texture("Icons/"+key));heading.Add(icon);}
            list.horizontalScrollerVisibility=ScrollerVisibility.Hidden;list.verticalScrollerVisibility=ScrollerVisibility.Auto;detail.horizontalScrollerVisibility=ScrollerVisibility.Hidden;detail.verticalScrollerVisibility=ScrollerVisibility.Auto;
            ApplyUnifiedHeader();detail.contentViewport.RegisterCallback<GeometryChangedEvent>(_=>FitContent());list.contentContainer.RegisterCallback<GeometryChangedEvent>(_=>FitSeal());list.contentViewport.RegisterCallback<GeometryChangedEvent>(_=>FitSeal());root.RegisterCallback<GeometryChangedEvent>(_=>Fit());canvas.RegisterCallback<AttachToPanelEvent>(_=>Fit());Fit();InteractionFeedback.Bind(panel.panel?.visualTree);
        }
        public static QuestPresentation Attach(VisualElement root,UIDocument document)=>root?.Q("quest-canvas")!=null?instances.GetValue(root,p=>new QuestPresentation(p,document)):null;
        void ApplyUnifiedHeader(){
            var old=root.Q(className:"quest-journal-header");var tabs=root.Q("quest-tabs");
            var header=UnifiedPanelHeader.Attach(root,canvas,root.Q<Label>("quest-title-label"),null,root.Q<Button>("quest-back-button"));
            old.style.display=DisplayStyle.None;canvas.AddToClassList("unified-canvas");
            var shell=new UnifiedPanelSurface{Kind="page-shell",name="unified-quest-shell"};UnifiedPanelHeader.Place(shell,new Rect(24,112,1552,764));canvas.Insert(0,shell);
            tabs.RemoveFromHierarchy();canvas.Add(tabs);UnifiedPanelHeader.Place(tabs,new Rect(80,128,1444,48));
            string[] ids={"progress","main","side","event","other"};for(int i=0;i<ids.Length;i++)UnifiedPanelHeader.Place(tabs.Q("quest-tab-"+ids[i]),new Rect(i*291,0,280,48));
            UnifiedPanelHeader.Place(tabs.Q(className:"quest-q-key"),new Rect(-40,11,26,26));UnifiedPanelHeader.Place(tabs.Q(className:"quest-e-key"),new Rect(1454,11,26,26));
            var listColumn=root.Q("quest-list-column");listColumn.RemoveFromHierarchy();canvas.Add(listColumn);UnifiedPanelHeader.Place(listColumn,new Rect(40,192,248,664));
            var detailColumn=root.Q("quest-detail-column");detailColumn.RemoveFromHierarchy();canvas.Add(detailColumn);UnifiedPanelHeader.Place(detailColumn,new Rect(304,192,1256,664));
            var strip=root.Q("quest-tracked-strip");strip.RemoveFromHierarchy();header.Add(strip);UnifiedPanelHeader.Place(strip,new Rect(728,38,620,36));header.BringToFront();
        }
        // Quest owns only its temporary foreground ordering. Tracker state and assets are untouched.
        public void SetForeground(bool visible){if(foreground==visible)return;foreground=visible;
            if(!visible){if(foregroundPanel!=null)foregroundPanel.sortingOrder=previousPanelOrder;if(document!=null)document.sortingOrder=previousDocumentOrder;foregroundPanel=null;return;}
            if(document==null||document.panelSettings==null)return;foregroundPanel=document.panelSettings;previousPanelOrder=foregroundPanel.sortingOrder;previousDocumentOrder=document.sortingOrder;
            foreach(var tracker in Object.FindObjectsByType<QuestTrackerHudController>(FindObjectsInactive.Include,FindObjectsSortMode.None)){var hud=tracker.GetComponent<UIDocument>();if(hud==null||hud.panelSettings==null)continue;
                if(hud.panelSettings==foregroundPanel)document.sortingOrder=Mathf.Max(document.sortingOrder,hud.sortingOrder+1);
                else foregroundPanel.sortingOrder=Mathf.Max(foregroundPanel.sortingOrder,hud.panelSettings.sortingOrder+1);
            }
        }
        void Fit(){float w=root.resolvedStyle.width,h=root.resolvedStyle.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0)return;float s=Mathf.Min(w/1600,h/900);canvas.style.left=(w-1600*s)/2;canvas.style.top=(h-900*s)/2;canvas.style.scale=new Scale(new Vector3(s,s,1));FitContent();}
        void FitContent(){float h=detail.contentViewport.layout.height;if(float.IsFinite(h)&&h>0&&Mathf.Abs(content.style.minHeight.value.value-h)>.1f)content.style.minHeight=h;}
        void FitSeal(){var seal=root.Q("quest-list-seal");if(seal==null)return;seal.style.display=list.contentContainer.layout.height+160>list.contentViewport.layout.height?DisplayStyle.None:DisplayStyle.Flex;}
        public void Refresh(QuestRuntimeState selected){bool hasData=selected?.Definition!=null;empty.style.display=hasData?DisplayStyle.None:DisplayStyle.Flex;listEmpty.style.display=hasData?DisplayStyle.None:DisplayStyle.Flex;list.style.display=hasData?DisplayStyle.Flex:DisplayStyle.None;foreach(var value in root.Query<Label>(className:"quest-basic-info-value").ToList())value.tooltip=value.text;Invalidate();}
        public void Invalidate(){foreach(var frame in root.Query<QuestFrame>().ToList()){frame.MarkDirtyRepaint();if(frame.Kind=="card"){var pin=frame.Q("quest-tracked-pin");if(pin!=null)pin.style.display=frame.ClassListContains("is-tracked")?DisplayStyle.Flex:DisplayStyle.None;}}FitContent();}
        public static void AddTrackedPin(VisualElement card){var pin=new VisualElement{name="quest-tracked-pin",pickingMode=PickingMode.Ignore};pin.AddToClassList("quest-tracked-pin");pin.style.backgroundImage=new StyleBackground(QuestSkinArt.Texture("Icons/location"));pin.style.display=card.ClassListContains("is-tracked")?DisplayStyle.Flex:DisplayStyle.None;card.Add(pin);}
    }
}
