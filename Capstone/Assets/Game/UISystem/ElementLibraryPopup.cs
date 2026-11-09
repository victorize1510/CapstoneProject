using Capstone.Game.HudSystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.UISystem {
    public static class ElementLibraryPopup {
        static System.Action closeCurrent;
        public static bool TryClose(){var action=closeCurrent;if(action==null)return false;closeCurrent=null;action();return true;}
        public static void Open(VisualElement owner,PetElement selected) {
            var root=owner?.panel?.visualTree;if(root==null||root.Q("element-library-modal")!=null)return;
            var overlay=new VisualElement{name="element-library-modal"};ElementUi.Stretch(overlay);overlay.style.backgroundColor=new Color(0,0,0,.65f);overlay.style.alignItems=Align.Center;overlay.style.justifyContent=Justify.Center;
            var card=new VisualElement();card.style.width=Length.Percent(84);card.style.maxWidth=1080;card.style.paddingLeft=card.style.paddingRight=24;card.style.paddingTop=card.style.paddingBottom=20;card.style.backgroundColor=GreenAngularGeometry.Shell;overlay.Add(card);
            var title=new Label("HỆ SINH VẬT & KỸ NĂNG");title.style.fontSize=26;title.style.color=GreenAngularGeometry.Light;title.style.unityFontStyleAndWeight=FontStyle.Bold;card.Add(title);
            var body=new VisualElement();body.style.flexDirection=FlexDirection.Row;body.style.marginTop=16;body.style.marginBottom=16;card.Add(body);
            var grid=new VisualElement();grid.style.width=Length.Percent(64);grid.style.flexDirection=FlexDirection.Row;grid.style.flexWrap=Wrap.Wrap;body.Add(grid);
            var detail=new Label();detail.style.width=Length.Percent(35);detail.style.paddingLeft=18;detail.style.whiteSpace=WhiteSpace.Normal;detail.style.fontSize=19;detail.style.color=GreenAngularGeometry.Light;body.Add(detail);
            void Choose(ElementUi.Token token){detail.text=token.name+"\n\nHệ của kỹ năng độc lập với loại sát thương Vật lý hoặc Phép.\n\nThông tin tương tác hệ\nChưa thiết lập";}
            foreach(var token in ElementUi.All){var item=token;var button=new Button(()=>Choose(item)){text=item.name,name="element-library-"+item.id};button.style.width=Length.Percent(31);button.style.height=110;button.style.marginRight=8;button.style.marginBottom=8;button.style.backgroundColor=Color.clear;button.style.borderLeftWidth=button.style.borderRightWidth=button.style.borderTopWidth=button.style.borderBottomWidth=0;var tone=new ElementTone{Tint=item.color};ElementUi.Stretch(tone);button.Insert(0,tone);tone.RegisterOwner(button);var text=new Label(item.name){pickingMode=PickingMode.Ignore};text.style.color=item.foreground;text.style.fontSize=20;text.style.unityFontStyleAndWeight=FontStyle.Bold;text.style.unityTextAlign=TextAnchor.LowerCenter;ElementUi.Stretch(text);text.style.bottom=8;button.text="";button.Add(text);var glyph=new VisualElement{pickingMode=PickingMode.Ignore};glyph.style.backgroundImage=new StyleBackground(ElementUi.Art(item.id+"-glyph"));glyph.style.backgroundSize=new BackgroundSize(BackgroundSizeType.Contain);glyph.style.width=glyph.style.height=42;glyph.style.alignSelf=Align.Center;glyph.style.marginTop=12;button.Add(glyph);grid.Add(button);}
            Choose(ElementUi.Find(selected)??ElementUi.All[0]);
            void Close(){closeCurrent=null;PanelFocusUtility.Close(overlay);overlay.RemoveFromHierarchy();owner?.Focus();}
            var close=new Button(Close){text="Đóng"};close.style.alignSelf=Align.FlexEnd;close.style.width=140;close.style.height=42;close.style.fontSize=20;card.Add(close);
            overlay.RegisterCallback<KeyDownEvent>(e=>{if(e.keyCode!=KeyCode.Escape)return;Close();e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            overlay.RegisterCallback<NavigationCancelEvent>(e=>{Close();e.StopImmediatePropagation();});
            closeCurrent=Close;overlay.RegisterCallback<DetachFromPanelEvent>(_=>{if(closeCurrent==(System.Action)Close)closeCurrent=null;});
            root.Add(overlay);PanelFocusUtility.Open(overlay);overlay.BringToFront();
        }
    }
}
