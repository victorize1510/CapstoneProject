using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;
namespace Capstone.Game.UISystem {
    /// <summary>Equal-sized balanced rows. Keeps original buttons, parent and controller callbacks.</summary>
    public sealed class RuntimeChoiceGrid {
        static readonly ConditionalWeakTable<VisualElement,RuntimeChoiceGrid> grids=new ConditionalWeakTable<VisualElement,RuntimeChoiceGrid>();
        readonly VisualElement host,anchor;readonly List<Button> buttons;bool pending;
        public static void Attach(VisualElement host,VisualElement anchor){if(host==null||anchor==null)return;grids.GetValue(host,h=>new RuntimeChoiceGrid(h,anchor));}
        public static void Refresh(VisualElement host){if(host!=null&&grids.TryGetValue(host,out var grid))grid.Fit();}
        RuntimeChoiceGrid(VisualElement host,VisualElement anchor){this.host=host;this.anchor=anchor;buttons=host.Query<Button>().ToList();RuntimeChoiceStyle.Panel(host);
            host.RegisterCallback<GeometryChangedEvent>(_=>Queue());anchor.RegisterCallback<GeometryChangedEvent>(_=>Queue());host.RegisterCallback<AttachToPanelEvent>(_=>Queue());Queue();}
        void Queue(){if(pending||host.panel==null)return;pending=true;host.schedule.Execute(()=>{pending=false;Fit();});}
        void Fit(){if(host.parent==null||buttons.Count==0||anchor.panel==null)return;float s=RuntimeChoiceStyle.LocalScale(host.parent);
            // Snap dimensions and spacing before Yoga rounds each edge independently.
            // Fractional widths otherwise alternate by one pixel across equal columns.
            float pixels=host.parent.layout.width>0?Mathf.Abs(host.parent.worldBound.width/host.parent.layout.width):1;
            float panelWidth=host.panel?.visualTree.layout.width??Screen.width;
            pixels*=panelWidth>0?Screen.width/panelWidth:1;
            pixels=Mathf.Max(.1f,pixels);
            float Snap(float value)=>Mathf.Round(value*pixels)/pixels;
            float gap=Snap(RuntimeChoiceStyle.Gap*s),pad=Snap(12*s),min=Snap(RuntimeChoiceStyle.TileWidth*s),rowHeight=Snap(RuntimeChoiceStyle.RowHeight*s),available=host.parent.layout.width-24*s;
            int fit=Mathf.Max(1,Mathf.FloorToInt((available-2*pad+gap)/(min+gap))),columns=Mathf.Min(3,fit,buttons.Count);
            if(buttons.Count>=8&&buttons.Count%4==0&&fit>=4)columns=4;
            if(buttons.Count>=10&&buttons.Count%5==0&&fit>=5)columns=5;
            if(buttons.Count==4&&fit>=2)columns=2;
            int rows=Mathf.CeilToInt(buttons.Count/(float)columns);float tile=min,width=columns*tile+(columns-1)*gap+2*pad,height=rows*rowHeight+(rows-1)*gap+2*pad;
            RuntimeChoiceStyle.PlacePopover(host,anchor,width,height,s);host.style.paddingLeft=host.style.paddingRight=host.style.paddingTop=host.style.paddingBottom=0;
            int index=0;for(int y=0;y<rows;y++){int count=buttons.Count/rows+(y<buttons.Count%rows?1:0);float left=Snap((width-count*tile-(count-1)*gap)/2);
                for(int x=0;x<count;x++){var b=buttons[index++];var caption=b.Q<Label>(className:"box-button-caption");RuntimeChoiceStyle.Row(b,s,caption,true);b.style.position=Position.Absolute;b.style.left=left+x*(tile+gap);b.style.top=pad+y*(rowHeight+gap);b.style.width=tile;b.style.height=b.style.minHeight=rowHeight;}}
        }
    }
}
