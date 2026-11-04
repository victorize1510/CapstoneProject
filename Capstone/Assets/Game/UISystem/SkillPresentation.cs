using System.Globalization;
using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    /// <summary>Shared management-panel skill views; combat HUD is deliberately independent.</summary>
    public static class SkillPresentation {
        public static void Bind(VisualElement root) {
            if(root==null||root.ClassListContains("skill-presentation"))return;
            root.AddToClassList("skill-presentation");
            var sheet=Resources.Load<StyleSheet>("UI/CompanionSkillScroll20261101/Companion");
            if(sheet!=null)root.styleSheets.Add(sheet);
        }
        public static void Compact(VisualElement card,string nameClass) {
            card.AddToClassList("compact-skill-card");
            if(card is AngularButton button)button.SetAngularContext(true);
            var old=card.Q<Label>(className:nameClass);
            if(old==null||old is SkillNameLabel)return;
            var label=new SkillNameLabel{name=old.name,pickingMode=PickingMode.Ignore};
            foreach(var cls in old.GetClasses())label.AddToClassList(cls);
            var parent=old.parent;int index=parent.IndexOf(old);string value=old.text;old.RemoveFromHierarchy();parent.Insert(index,label);label.SetValue(value);
        }
        public static void SetName(Label label,string value) {if(label is SkillNameLabel skill)skill.SetValue(value);else if(label!=null)label.text=value;}
    }
    public sealed class SkillListRow : AngularButton {
        public readonly VisualElement Icon;
        public readonly Label Fallback,Element;
        public readonly SkillNameLabel NameLabel;
        System.Action selection;
        public void BindSelection(System.Action action){selection=PlayerActionGate.Guard(this,"select-skill",action);}
        public SkillListRow() {
            text=string.Empty;Role="raised";Shape="panel";
            AddToClassList("skills-learned-card");AddToClassList("skill-list-row");
            Icon=new VisualElement{name="learned-icon",pickingMode=PickingMode.Ignore};Icon.AddToClassList("skills-learned-icon");
            Fallback=new Label{pickingMode=PickingMode.Ignore};Fallback.AddToClassList("skills-icon-fallback");Icon.Add(Fallback);Add(Icon);
            var copy=new VisualElement{pickingMode=PickingMode.Ignore};copy.AddToClassList("skills-learned-copy");
            NameLabel=new SkillNameLabel{pickingMode=PickingMode.Ignore};NameLabel.AddToClassList("skills-learned-name");copy.Add(NameLabel);Add(copy);
            Element=new Label{pickingMode=PickingMode.Ignore};Element.AddToClassList("skills-learned-element");Add(Element);
            RegisterCallback<AttachToPanelEvent>(_=>SetAngularContext(true));
            clicked+=()=>selection?.Invoke();
            RegisterCallback<PointerDownEvent>(_=>Focus(),TrickleDown.TrickleDown);
            RegisterCallback<KeyDownEvent>(e=>{if(!enabledInHierarchy||(e.keyCode!=KeyCode.Return&&e.keyCode!=KeyCode.KeypadEnter&&e.keyCode!=KeyCode.Space))return;selection?.Invoke();e.StopImmediatePropagation();},TrickleDown.TrickleDown);
        }
        public void SetPresentation(string name,string element,Sprite icon){NameLabel.SetValue(name);Element.text=element;tooltip=name;CompanionArt.SetSkillImage(Icon,Fallback,icon);var face=Icon.Q<AngularSurface>("latest-skill-surface");if(face!=null)face.SkinTexture=LatestMenuCompanionAssets.Load("Surfaces/skill-oct_primary_56_56")?.texture;}
    }
    /// <summary>Two lines at the original font size; elision preserves Vietnamese graphemes.</summary>
    public sealed class SkillNameLabel : Label {
        string source=string.Empty;float measuredWidth=-1;bool dirty=true;
        public SkillNameLabel(){RegisterCallback<GeometryChangedEvent>(_=>Fit());RegisterCallback<AttachToPanelEvent>(_=>schedule.Execute(Fit));}
        public void SetValue(string value){value=value??string.Empty;if(source==value&&!dirty)return;source=value;tooltip=value;dirty=true;Fit();}
        void Fit(){float width=contentRect.width;if(panel==null||width<1)return;if(!dirty&&Mathf.Abs(width-measuredWidth)<.1f)return;dirty=false;measuredWidth=width;
            float height=resolvedStyle.fontSize*2.5f;
            float Measure(string value)=>MeasureTextSize(value,width,MeasureMode.Exactly,0,MeasureMode.Undefined).y;
            if(Measure(source)<=height+.1f){text=source;return;}
            int[] starts=StringInfo.ParseCombiningCharacters(source);int low=0,high=starts.Length;
            while(low<high){int middle=(low+high+1)/2,end=middle==starts.Length?source.Length:starts[middle];if(Measure(source.Substring(0,end)+"…")<=height+.1f)low=middle;else high=middle-1;}
            text=source.Substring(0,low==starts.Length?source.Length:starts[low])+"…";
        }
    }
}
