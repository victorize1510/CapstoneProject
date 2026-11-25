using System.Runtime.CompilerServices;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.Inventory.Bag {
    /// <summary>One non-picking tooltip per panel, above/below its owner with a clamped viewport margin.</summary>
    static class BagTooltip {
        sealed class Tip {
            public readonly BagSurface Surface=new BagSurface{Kind="tooltip",name="bag-tooltip"};
            public readonly Label Text=new Label{pickingMode=PickingMode.Ignore};
            public VisualElement Owner;
            public Tip(){Surface.style.display=DisplayStyle.None;Surface.pickingMode=PickingMode.Ignore;Text.style.color=GreenAngularGeometry.Light;Text.style.fontSize=18;Text.style.whiteSpace=WhiteSpace.Normal;Text.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);BagPresentation.Place(Text,16,12,288,88);Surface.Add(Text);}
        }
        static readonly ConditionalWeakTable<VisualElement,Tip> tips=new ConditionalWeakTable<VisualElement,Tip>();
        public static void HideAll(VisualElement root){var panelRoot=root?.panel?.visualTree;if(panelRoot!=null&&tips.TryGetValue(panelRoot,out var tip)){tip.Surface.style.display=DisplayStyle.None;tip.Owner=null;}}
        public static void Bind(BagButton button){
            IVisualElementScheduledItem delay=null;
            void Hide(){delay?.Pause();var root=button.panel?.visualTree;if(root!=null&&tips.TryGetValue(root,out var t)&&t.Owner==button){t.Surface.style.display=DisplayStyle.None;t.Owner=null;}}
            button.RegisterCallback<TooltipEvent>(e=>e.StopImmediatePropagation());
            button.RegisterCallback<PointerEnterEvent>(_=>{delay?.Pause();delay=button.schedule.Execute(()=>Show(button)).StartingIn(350);});
            button.RegisterCallback<PointerLeaveEvent>(_=>Hide());button.RegisterCallback<PointerDownEvent>(_=>Hide(),TrickleDown.TrickleDown);button.RegisterCallback<DetachFromPanelEvent>(_=>Hide());
        }
        static void Show(BagButton button){
            var root=button.panel?.visualTree;if(root==null||string.IsNullOrWhiteSpace(button.tooltip)||!button.visible)return;
            for(var p=button.parent;p!=null;p=p.parent)if(p.resolvedStyle.display==DisplayStyle.None)return;
            var tip=tips.GetValue(root,_=>new Tip());if(tip.Surface.parent==null)root.Add(tip.Surface);tip.Owner=button;tip.Text.text=button.tooltip;
            float rw=root.resolvedStyle.width,rh=root.resolvedStyle.height,s=Mathf.Min(rw/1600,rh/900);var b=button.worldBound;var p0=root.WorldToLocal(b.position);float w=320*s,h=112*s,x=Mathf.Clamp(p0.x+(b.width-w)/2,16,rw-w-16),y=p0.y-h-10;
            if(y<16)y=p0.y+b.height+10;y=Mathf.Clamp(y,16,rh-h-16);BagPresentation.Place(tip.Surface,x,y,320,112);tip.Surface.style.scale=new Scale(new Vector3(s,s,1));tip.Surface.style.transformOrigin=new TransformOrigin(0,0,0);tip.Surface.style.display=DisplayStyle.Flex;tip.Surface.BringToFront();
        }
    }
}
