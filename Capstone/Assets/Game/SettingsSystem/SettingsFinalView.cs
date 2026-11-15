using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UIElements;

namespace Capstone.Game.SettingsSystem {
    /// <summary>Scoped R3 layout with R4 artwork. Existing controls, IDs and capabilities stay owned by the controller.</summary>
    public sealed class SettingsFinalView {
        static readonly ConditionalWeakTable<VisualElement,SettingsFinalView> views=new ConditionalWeakTable<VisualElement,SettingsFinalView>();
        readonly VisualElement panel,canvas,modal;
        readonly List<VisualElement> groups=new List<VisualElement>();
        readonly List<FinalButton> tabs=new List<FinalButton>();
        readonly List<(Toggle toggle,VisualElement input)> switches=new List<(Toggle,VisualElement)>();
        readonly Dictionary<string,VisualElement> rows=new Dictionary<string,VisualElement>();
        readonly Label helpTitle,helpBody,helpValue,helpExtra,sectionTitle,note,noteHeading;
        readonly VisualElement sectionIcon,noteIcon;
        readonly string[] groupIds={"settings-audio-group","settings-camera-group","settings-interface-group","settings-other-group"};
        readonly string[] headings={"ÂM THANH","ĐIỀU KHIỂN & CAMERA","TRÒ CHƠI & GIAO DIỆN","HỆ THỐNG"};
        readonly string[] firstRows={"settings-master-row","settings-mouse-row","settings-prompts-row","settings-language-row"};
        readonly string[] notes={"Tắt tiếng không thay đổi mức âm lượng đang chọn.\nBật tiếng trở lại để tiếp tục nghe ở mức đó.","Phím điều khiển hiện có nằm trong Hướng dẫn phím.","Ẩn thông tin không thay đổi tiến trình của bạn.","Các tùy chọn cửa sổ được áp dụng ngay khi khả dụng."};
        string helpId="settings-master-row";
        int tab;
        public static SettingsFinalView Attach(VisualElement panel)=>panel==null?null:views.GetValue(panel,p=>new SettingsFinalView(p));
        SettingsFinalView(VisualElement root){
            panel=root;panel.AddToClassList("settings-design");
            // The canonical FINAL frames replace the earlier generic section faces in this panel only.
            foreach(var frame in panel.Query<UiSectionFrame>().ToList())frame.RemoveFromHierarchy();
            var sheet=Resources.Load<StyleSheet>("UI/SettingsFinal20261102/Settings");if(sheet!=null)panel.styleSheets.Add(sheet);
            panel.style.unityFontDefinition=FontDefinition.FromFont(MenuTypography.Font);
            canvas=new VisualElement{name="settings-design-canvas"};canvas.AddToClassList("settings-canvas");panel.Add(canvas);
            Place(canvas,0,0,1600,900);canvas.style.transformOrigin=new TransformOrigin(0,0,0);
            var shell=new UnifiedPanelSurface{Kind="page-shell",name="settings-shell"};Place(shell,24,112,1552,764);canvas.Add(shell);
            UnifiedPanelHeader.Attach(panel,canvas,panel.Q<Label>(className:"screen-title"),panel.Q(className:"screen-heading-mark"),ReplaceButton(panel.Q<Button>("settings-back")));
            panel.Q(className:"screen-header").style.display=DisplayStyle.None;
            var main=new SettingsFace("settings-main");Place(main,40,192,1008,588);canvas.Add(main);
            var help=new SettingsFace("settings-help");Place(help,1064,192,496,588);canvas.Add(help);
            sectionTitle=Label(canvas,"settings-active-heading",headings[0],23,100,211,904,42);sectionIcon=Icon(canvas,"audio",66,218,24);
            var helpHeading=Label(canvas,"settings-help-heading","CHI TIẾT TÙY CHỌN",23,1120,211,412,42);Icon(canvas,"book",1088,218,24);
            helpTitle=Label(canvas,"settings-help-title","",28,1088,270,444,64);
            helpBody=Label(canvas,"settings-help-body","",20,1088,344,444,108);
            Label(canvas,"settings-help-current-label","Đang chọn",18,1088,466,210,32);
            helpValue=Label(canvas,"settings-help-value","",20,1300,466,232,32);helpValue.style.unityTextAlign=TextAnchor.MiddleRight;
            helpExtra=Label(canvas,"settings-help-extra","",18,1088,518,444,148);
            var saved=new FinalButton{ text="Thiết lập được tự lưu"};Place(saved,1088,692,444,64);saved.SetEnabled(false);canvas.Add(saved);saved.style.opacity=1;Icon(saved,"check",18,20,24);
            note=Label(canvas,"settings-tab-note","",18,64,674,948,80);note.AddToClassList("settings-group-note");
            noteHeading=Label(canvas,"settings-note-heading","GHI CHÚ",22,100,630,904,36);noteIcon=Icon(canvas,"book",66,634,24);
            string[] tabLabels={"ÂM THANH","ĐIỀU KHIỂN","GIAO DIỆN","HỆ THỐNG"},tabIcons={"audio","controls","interface","settings"};
            for(int i=0;i<4;i++){
                int index=i;var button=new FinalButton(()=>SelectTab(index),"category-tab"){name="settings-tab-"+new[]{"audio","controls","interface","system"}[i],text=tabLabels[i]};
                Place(button,48+i*380,128,360,48);canvas.Add(button);Icon(button,tabIcons[i],24,12,24);tabs.Add(button);
                var group=panel.Q(groupIds[i]);group.RemoveFromHierarchy();canvas.Add(group);group.AddToClassList("settings-current-group");Place(group,64,270,960,376);groups.Add(group);
                group.Q(className:"settings-section-heading").style.display=DisplayStyle.None;
                foreach(var oldNote in group.Query<Label>(className:"settings-note").ToList())if(oldNote.name!="settings-camera-unavailable"&&oldNote.name!="settings-display-note")oldNote.style.display=DisplayStyle.None;
                foreach(var divider in group.Query<VisualElement>(className:"settings-divider").ToList())divider.style.display=DisplayStyle.None;
                var scroll=group.Q<ScrollView>();RuntimeScrollbars.Apply(scroll);
                var plate=new SettingsFace("settings-group"){name="settings-options-face"};plate.style.position=Position.Absolute;plate.style.left=plate.style.top=plate.style.right=0;plate.style.bottom=0;group.Insert(0,plate);
            }
            panel.Q(className:"settings-body").style.display=DisplayStyle.None;
            var toolbar=new AngularSurface{Shape="panel",Role="primary",Material=true,NoBorder=true};Place(toolbar,48,800,1504,56);canvas.Add(toolbar);
            var footerNote=Label(canvas,"settings-footer-note","Áp dụng tức thời · Tự lưu",17,716,806,490,44);footerNote.style.unityTextAlign=TextAnchor.MiddleCenter;footerNote.style.color=GreenAngularGeometry.Light;
            string[] footerIds={"settings-guide-row","settings-reset-row","settings-quit-row"},footerIcons={"keyboard","reset","exit"};float[] positions={64,388,1224};
            for(int i=0;i<3;i++){
                var row=panel.Q(footerIds[i]);row.RemoveFromHierarchy();canvas.Add(row);row.AddToClassList("settings-footer-action");Place(row,positions[i],806,304,44);
                row.Q<Label>("setting-label").style.display=DisplayStyle.None;
                var button=ReplaceButton(row.Q<Button>("setting-button"));button.Light=i==1;Icon(button,footerIcons[i],20,10,24);
            }
            foreach(string id in new[]{"master","mute","mouse","invert","smoothing","prompts","minimap","quest","save","language","fullscreen","resolution"}){
                string name="settings-"+id+"-row";var row=panel.Q(name);rows[name]=row;
                var content=row.Q(className:"settings-row");content.AddToClassList("final-setting-row");
                var copy=new VisualElement{name="settings-row-copy"};copy.AddToClassList("settings-row-copy");var label=row.Q<Label>("setting-label");label.RemoveFromHierarchy();copy.Add(label);copy.Add(new Label(Description(name)){name="setting-description",pickingMode=PickingMode.Ignore});content.Insert(0,copy);
                string icon=id=="master"?"audio":id=="mouse"?"controls":id=="prompts"?"interaction":id=="minimap"?"map":id=="fullscreen"?"fullscreen":id;Icon(content,icon,14,28,22);
                row.RegisterCallback<PointerEnterEvent>(_=>{ShowHelp(name);Refresh();});row.RegisterCallback<FocusInEvent>(_=>{ShowHelp(name);Refresh();});
                var slider=row.Q<Slider>();if(slider!=null){slider.Add(new SettingsSliderFace(slider));}
                var toggle=row.Q<Toggle>();if(toggle!=null){var input=toggle.Q(className:"unity-toggle__input");toggle.Q("settings-switch-thumb")?.RemoveFromHierarchy();switches.Add((toggle,input));input.Add(new ToggleFocus(toggle));}
                var dropdown=row.Q<DropdownField>();if(dropdown!=null){foreach(var text in dropdown.Query<TextElement>().ToList())text.style.color=GreenAngularGeometry.Ink;var input=dropdown.Q(className:"unity-base-field__input");var face=new SettingsFace("dropdown-face",true);face.style.position=Position.Absolute;face.style.left=face.style.top=face.style.right=face.style.bottom=0;input.Insert(0,face);}
            }
            var empty=panel.Q<Label>("settings-camera-unavailable");empty.text="Camera chưa sẵn sàng\n\nKhu vực này chưa có camera hỗ trợ.\nNhững tùy chọn camera sẽ xuất hiện khi khả dụng.";
            modal=panel.Q("settings-modal");modal.RemoveFromHierarchy();canvas.Add(modal);Place(modal,0,0,1600,900);
            var card=modal.Q(className:"settings-modal-card");var background=new SettingsFace("popup");background.style.position=Position.Absolute;background.style.left=background.style.top=background.style.right=background.style.bottom=0;card.Insert(0,background);
            var modalCancel=ReplaceButton(modal.Q<Button>("settings-modal-cancel"));modalCancel.Light=true;var modalConfirm=ReplaceButton(modal.Q<Button>("settings-modal-confirm"));
            foreach(var button in new[]{modalCancel,modalConfirm}){button.style.width=button.style.minWidth=304;button.style.height=button.style.minHeight=44;button.style.flexGrow=0;button.style.flexShrink=0;button.Q<Label>(className:"angular-button-caption").style.left=12;}
            var close=new FinalButton(null,"popup-close-face"){name="settings-modal-close",text="×"};Place(close,0,0,40,40);close.style.left=StyleKeyword.Auto;close.style.right=16;close.style.top=16;card.Add(close);
            panel.RegisterCallback<GeometryChangedEvent>(_=>Fit());canvas.RegisterCallback<AttachToPanelEvent>(_=>Fit());
            SelectTab(0);Fit();RuntimeScrollbars.Bind(canvas);
        }
        void Fit(){float w=panel.resolvedStyle.width,h=panel.resolvedStyle.height;if(!float.IsFinite(w)||!float.IsFinite(h)||w<=0||h<=0)return;float scale=Mathf.Min(w/1600,h/900);canvas.style.left=(w-1600*scale)/2;canvas.style.top=(h-900*scale)/2;canvas.style.scale=new Scale(new Vector3(scale,scale,1));}
        public void SelectTab(int index){tab=Mathf.Clamp(index,0,3);for(int i=0;i<4;i++){groups[i].style.display=i==tab?DisplayStyle.Flex:DisplayStyle.None;tabs[i].Light=i==tab;}sectionTitle.text=headings[tab];SettingsFinalArt.Icon(sectionIcon,new[]{"audio","controls","interface","settings"}[tab]);note.text=notes[tab];ShowHelp(firstRows[tab]);Refresh();}
        public void Refresh(){
            foreach(var item in switches){string state=!item.toggle.enabledInHierarchy?"disabled":item.toggle.value?"on":"off";item.input.style.backgroundImage=new StyleBackground(SettingsFinalArt.Texture("Components/checkbox-face-"+state));}
            int count=0;foreach(var candidate in rows.Values)if(groups[tab].Contains(candidate)&&candidate.style.display!=DisplayStyle.None)count++;
            // Group height follows only rows left visible by the controller's capability pass.
            float height=Mathf.Max(120,count*(tab==0?114:108)+12);
            if(tab==1&&rows["settings-mouse-row"].style.display==DisplayStyle.None)height=240;
            groups[tab].style.height=height;float end=270+height;noteHeading.style.top=end+20;noteIcon.style.top=end+24;
            noteHeading.style.display=noteIcon.style.display=tab==2?DisplayStyle.None:DisplayStyle.Flex;
            note.style.top=end+(tab==2?24:76);note.style.height=tab==0?72:40;
            var activeHelp=rows.TryGetValue(helpId,out var row)?row:null;
            if(activeHelp!=null&&activeHelp.style.display==DisplayStyle.None){helpTitle.text=tab==1?"Camera chưa sẵn sàng":"Tùy chọn chưa khả dụng";helpBody.text="Những mục này xuất hiện khi khu vực hiện tại hỗ trợ.";helpValue.text="Chưa khả dụng";helpExtra.text="Các nhóm cài đặt khác vẫn hoạt động bình thường.";return;}
            if(helpTitle.text!=Title(helpId))ShowHelp(helpId);
            helpValue.text=activeHelp?.Q<Label>("setting-value")?.text??activeHelp?.Q<DropdownField>()?.value??"Theo tùy chọn";
        }
        public void Modal(bool guide){panel.EnableInClassList("settings-guide-open",guide);}
        void ShowHelp(string id){helpId=id;helpTitle.text=Title(id);helpBody.text=Description(id);helpExtra.text=id=="settings-master-row"||id=="settings-mute-row"?"Khi tắt tiếng\n\nMức âm lượng đang chọn vẫn được giữ. Bật tiếng lại để tiếp tục nghe ở mức đó.":id=="settings-mouse-row"?"Giá trị tham chiếu\n\n50% tương ứng camera gốc. Đảo Y chỉ đổi chiều nhìn dọc; làm mượt điều chỉnh chuyển động camera.":id=="settings-language-row"?"Tùy chọn khả dụng\n\nTrò chơi hiện hỗ trợ tiếng Việt. Các độ phân giải được lấy từ thiết bị hiện tại.":"Hiển thị và dữ liệu\n\nCác thay đổi áp dụng tức thời. Ẩn thông tin giao diện không thay đổi tiến trình hoặc tắt tính năng trò chơi.";}
        static string Title(string id){switch(id){case "settings-master-row":return "Tổng âm lượng";case "settings-mute-row":return "Tắt tất cả âm thanh";case "settings-mouse-row":return "Độ nhạy chuột";case "settings-invert-row":return "Đảo ngược trục Y";case "settings-smoothing-row":return "Làm mượt camera";case "settings-prompts-row":return "Gợi ý tương tác";case "settings-minimap-row":return "Bản đồ mini";case "settings-quest-row":return "Nhiệm vụ đang theo dõi";case "settings-save-row":return "Thông báo lưu game";case "settings-language-row":return "Ngôn ngữ";case "settings-fullscreen-row":return "Toàn màn hình";default:return "Độ phân giải";}}
        static string Description(string id){switch(id){case "settings-master-row":return "Điều chỉnh toàn bộ âm thanh trong trò chơi.";case "settings-mute-row":return "Giữ nguyên mức âm lượng đã chọn.";case "settings-mouse-row":return "Tốc độ xoay góc nhìn theo thao tác chuột.";case "settings-invert-row":return "Đổi chiều nhìn lên và xuống.";case "settings-smoothing-row":return "Chuyển động camera êm hơn.";case "settings-prompts-row":return "Gợi ý khi đứng gần đối tượng có thể tương tác.";case "settings-minimap-row":return "Hiển thị minimap trên HUD; không tắt bản đồ thế giới.";case "settings-quest-row":return "Chỉ thay đổi hiển thị trên HUD, không bỏ theo dõi nhiệm vụ.";case "settings-save-row":return "Hiện thông báo sau khi lưu thành công; không tắt lưu game.";case "settings-language-row":return "Trò chơi hiện hỗ trợ tiếng Việt.";case "settings-fullscreen-row":return "Chế độ cửa sổ của bản game PC.";default:return "Danh sách lấy từ màn hình hiện tại.";}}
        static FinalButton ReplaceButton(Button old){if(old is FinalButton final)return final;var button=new FinalButton{name=old.name,text=old.text};foreach(var cls in old.GetClasses())button.AddToClassList(cls);var parent=old.parent;int index=parent.IndexOf(old);old.RemoveFromHierarchy();parent.Insert(index,button);return button;}
        static Label Label(VisualElement parent,string name,string text,int font,float x,float y,float w,float h){var label=new Label(text){name=name,pickingMode=PickingMode.Ignore};Place(label,x,y,w,h);label.style.fontSize=font;label.style.color=GreenAngularGeometry.Ink;label.style.whiteSpace=WhiteSpace.Normal;parent.Add(label);return label;}
        static VisualElement Icon(VisualElement parent,string key,float x,float y,float size){var icon=new VisualElement{pickingMode=PickingMode.Ignore,userData=key};icon.AddToClassList("settings-function-icon");Place(icon,x,y,size,size);SettingsFinalArt.Icon(icon,parent is FinalButton button&&!button.Light?key+"-light":key);parent.Add(icon);return icon;}
        static void Place(VisualElement e,float x,float y,float w,float h)=>UnifiedPanelHeader.Place(e,new Rect(x,y,w,h));
        sealed class FinalButton : AngularButton {
            readonly SettingsFace face;public bool Light {get=>face.Light;set{face.Light=value;face.MarkDirtyRepaint();var label=this.Q<Label>(className:"angular-button-caption");if(label!=null)label.style.color=value?GreenAngularGeometry.Ink:GreenAngularGeometry.Light;foreach(var icon in Children())if(icon.ClassListContains("settings-function-icon")&&icon.userData is string key)SettingsFinalArt.Icon(icon,value?key:key+"-light");}}
            public FinalButton(Action action=null,string kind="footer-button"):base(){if(action!=null)clicked+=PlayerActionGate.Guard(this,"settings-tab",action);style.backgroundColor=Color.clear;style.backgroundImage=StyleKeyword.None;style.borderLeftWidth=style.borderTopWidth=style.borderRightWidth=style.borderBottomWidth=0;style.borderTopLeftRadius=style.borderTopRightRadius=style.borderBottomLeftRadius=style.borderBottomRightRadius=0;face=new SettingsFace(kind,kind=="popup-close-face");Insert(0,face);face.style.position=Position.Absolute;face.style.left=face.style.top=face.style.right=face.style.bottom=0;Shape=kind=="category-tab"?"tab":kind=="popup-close-face"?"panel":"parallel";Surface.Shape=Shape;Surface.Material=false;Surface.NoFill=Surface.NoBorder=true;RegisterCallback<AttachToPanelEvent>(_=>SetAngularContext(true));}
        }
        sealed class ToggleFocus : VisualElement {
            bool focused;readonly Toggle toggle;
            public ToggleFocus(Toggle owner){toggle=owner;pickingMode=PickingMode.Ignore;style.position=Position.Absolute;style.left=style.top=0;style.width=style.height=40;owner.RegisterCallback<FocusInEvent>(_=>{focused=true;MarkDirtyRepaint();});owner.RegisterCallback<FocusOutEvent>(_=>{focused=false;MarkDirtyRepaint();});generateVisualContent+=Draw;}
            void Draw(MeshGenerationContext ctx){if(!focused||!toggle.enabledInHierarchy||!InteractionFeedback.KeyboardNavigation)return;var pts=GreenAngularGeometry.Polygon("panel",40,40,4);var p=ctx.painter2D;p.BeginPath();p.MoveTo(pts[0]);for(int i=1;i<pts.Length;i++)p.LineTo(pts[i]);p.ClosePath();p.lineWidth=2;p.strokeColor=GreenAngularGeometry.Gold;p.Stroke();}
        }
        sealed class SettingsFace : VisualElement {
            readonly string kind;readonly bool single;public bool Light;
            public SettingsFace(string id,bool one=false){kind=id;single=one;pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;}
            void Draw(MeshGenerationContext ctx){float w=layout.width,h=layout.height;if(w<2||h<2)return;var shape=kind=="category-tab"?"tab":kind=="footer-button"?"parallel":"panel";var pts=GreenAngularGeometry.Polygon(shape,w,h,3);Paint(ctx,pts,SettingsFinalArt.Texture("Components/"+kind+(single?"":"__fill")),Color.white,true);if(Light){var tint=Color.Lerp(GreenAngularGeometry.Raised,GreenAngularGeometry.Lavender,.2f);tint.a=.91f;Paint(ctx,pts,null,tint);}if(!single){var border=SettingsFinalArt.Texture("Components/"+kind+"__border");if(border!=null&&Mathf.Abs(border.width-w)<1.5f&&Mathf.Abs(border.height-h)<1.5f)Paint(ctx,pts,border,Color.white);else{var p=ctx.painter2D;p.BeginPath();p.MoveTo(pts[0]);for(int i=1;i<pts.Length;i++)p.LineTo(pts[i]);p.ClosePath();p.lineWidth=1.3f;p.strokeColor=GreenAngularGeometry.Edge;p.Stroke();}}}
            void Paint(MeshGenerationContext ctx,Vector2[] pts,Texture image,Color tint,bool cover=false){if(image==null&&tint==Color.white)return;float sx=1,sy=1;if(cover&&image!=null){float ratio=(float)image.width/image.height,box=layout.width/layout.height;if(ratio>box)sx=box/ratio;else sy=ratio/box;}var indices=GreenAngularGeometry.Triangles(pts);var mesh=ctx.Allocate(pts.Length,indices.Length,image);foreach(var p in pts)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),uv=new Vector2((1-sx)*.5f+p.x/layout.width*sx,(1-sy)*.5f+(1-p.y/layout.height)*sy),tint=tint});foreach(int i in indices)mesh.SetNextIndex((ushort)i);}
        }
    }
}
