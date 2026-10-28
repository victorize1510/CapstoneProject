using System.Collections.Generic;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.ProfileSystem {
    public sealed partial class ProfilePanelController {
        readonly List<RectTransform> modalCanvases=new List<RectTransform>();
        Text renameCurrentName,renameCounter,avatarCurrentCaption;
        Image renameCurrentAvatar,avatarCurrentImage;
        ProfileR2Graphic nameInputFrame;

        static ProfileR2Graphic Graphic(Transform parent,string name,string shape,Texture texture,Color tint){
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(ProfileR2Graphic));go.transform.SetParent(parent,false);
            var g=go.GetComponent<ProfileR2Graphic>();Stretch(g.rectTransform);g.Shape=shape;g.Texture=texture;g.color=tint;g.Cover=true;g.raycastTarget=false;return g;
        }
        static ProfileR2Graphic Skin(RectTransform host,string component,string shape="panel",bool interactive=true){
            var image=host.GetComponent<Image>();if(image!=null)image.color=Color.clear;
            var outline=host.GetComponent<Outline>();if(outline!=null)outline.enabled=false;
            string unified=component=="summary-panel"?"profile-summary":component=="info-panel"?"profile-info":component=="stats-panel"?"profile-stats":host.name.StartsWith("StatsCard_")?"profile-stat-card":null;
            var fill=Graphic(host,"R2Fill",shape,unified!=null?UnifiedPanelHeader.Texture("Components/"+unified+"__fill"):ProfileR2Art.Texture("Components/"+component+"__fill"),Color.white);fill.transform.SetAsFirstSibling();if(unified!=null)fill.LogicalScale=1.2f;
            if(fill.Texture==null)fill.color=GreenAngularGeometry.Panel;
            var border=Graphic(host,"R2Border",shape,unified!=null?UnifiedPanelHeader.Texture("Components/"+unified+"__border"):ProfileR2Art.Texture("Components/"+component+"__border"),Color.white);border.transform.SetSiblingIndex(1);if(unified!=null)border.LogicalScale=1.2f;
            if(border.Texture==null||Mathf.Abs(border.Texture.width*border.LogicalScale-host.rect.width)>.5f||Mathf.Abs(border.Texture.height*border.LogicalScale-host.rect.height)>.5f){border.Texture=null;border.NoFill=true;border.Edge=true;border.AccentRails=true;}
            if (shape=="panel" && host.GetComponent<Selectable>()==null &&
                (unified!=null || component=="stat-card")) {
                fill.enabled=border.enabled=false;
                UiSectionGraphic.Attach(host, false, 1.2f);
            }
            var hit=host.GetComponent<ProfileR2HitArea>()??host.gameObject.AddComponent<ProfileR2HitArea>();hit.Surface=fill;
            if(interactive&&host.TryGetComponent<Selectable>(out var selectable)){
                selectable.transition=Selectable.Transition.None;
                var feedback=host.GetComponent<ProfileR2Feedback>()??host.gameObject.AddComponent<ProfileR2Feedback>();
                feedback.Layer=Graphic(host,"R2Feedback",shape,null,Color.clear);feedback.Layer.transform.SetSiblingIndex(2);feedback.RefreshDisabled();
            }
            return fill;
        }
        static Image SpriteImage(Transform parent,string name,Sprite sprite){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.sprite=sprite;image.color=Color.white;image.raycastTarget=false;image.preserveAspect=true;return image;}
        static Image CirclePortrait(Transform parent,string name,Sprite sprite,out RectTransform clip){
            var go=new GameObject(name+"Clip",typeof(RectTransform),typeof(CanvasRenderer),typeof(ProfileR2Graphic),typeof(Mask));go.transform.SetParent(parent,false);clip=(RectTransform)go.transform;Stretch(clip);
            var maskGraphic=go.GetComponent<ProfileR2Graphic>();maskGraphic.Shape="circle";maskGraphic.color=Color.white;maskGraphic.raycastTarget=false;go.GetComponent<Mask>().showMaskGraphic=false;
            var image=SpriteImage(clip,name,sprite);Stretch(image.rectTransform);return image;
        }
        static void SkinButton(Button button,string shape="parallel",bool light=false){
            if(button==null)return;bool pet=button.name.StartsWith("TeamPetSlot_");if(pet)shape="panel";Skin((RectTransform)button.transform,pet?"pet-slot":shape=="circle"?"camera-circle":"button",shape);
            foreach(var label in button.GetComponentsInChildren<Text>(true))label.color=light?Forest:GreenAngularGeometry.Light;
            if(light){var fill=button.transform.Find("R2Fill").GetComponent<ProfileR2Graphic>();fill.Texture=null;fill.color=GreenAngularGeometry.Raised;}
        }
        void ApplyR2Skin(){
            root.GetComponent<Image>().color=GreenAngularGeometry.Ink;
            var background=Graphic(root,"R2Background","rect",UnifiedPanelHeader.Texture("Art/page"),Color.white);background.transform.SetAsFirstSibling();
            // The supplied component fills already bake material at 50% and the specified reading surface. Display at 1.
            Skin((RectTransform)designSurface.Find("MainProfilePanel"),"summary-panel");
            Skin((RectTransform)designSurface.Find("PlayerInfoPanel"),"info-panel");
            Skin((RectTransform)designSurface.Find("StatsOverviewPanel"),"stats-panel");
            var header=(RectTransform)designSurface.Find("HeaderBar");header.GetComponent<Image>().color=Color.clear;
            foreach(var label in designSurface.GetComponentsInChildren<Text>(true))if(label.name=="DisplayName"||label.name=="Title")label.font=TrainerMenuArt.Serif;
            displayNameText.horizontalOverflow=HorizontalWrapMode.Overflow;
            foreach(var panel in designSurface.GetComponentsInChildren<RectTransform>(true)){
                if(panel.name.StartsWith("StatsCard_"))Skin(panel,"stat-card");
                if(panel.name=="LevelBadge"){panel.GetComponent<Image>().color=Color.clear;UiReadability.Add(panel).LogicalScale=1.2f;}
                if(panel.name=="IconBadge"||panel.name=="IconCircle"){var image=panel.GetComponent<Image>();if(image!=null)image.color=Color.clear;}
            }
            foreach(var button in designSurface.GetComponentsInChildren<Button>(true))SkinButton(button,button==avatarCameraButton?"circle":button.name=="ChangeNameButton"?"diamond":"parallel");
            var avatarOuter=(RectTransform)designSurface.Find("MainProfilePanel/AvatarSection/AvatarOuter");Skin(avatarOuter,"avatar-circle","circle",false);
            var avatarMask=avatarOuter.Find("AvatarMask").GetComponent<Mask>();avatarMask.showMaskGraphic=false;
            var track=experienceFill.transform.parent.GetComponent<Image>();track.sprite=SolidSprite;track.type=Image.Type.Simple;track.color=GreenAngularGeometry.Edge;
            experienceFill.sprite=SolidSprite;experienceFill.color=GreenAngularGeometry.Exp;
            for(int i=0;i<teamSlots.Length;i++){
                var slot=teamSlots[i];if(slot==null)continue;var rect=(RectTransform)slot.button.transform;
                // The profile's 3x2 slots retain their exact original rects. Only their mask changes.
                var oldMask=rect.GetComponent<RectMask2D>();if(oldMask!=null){oldMask.enabled=false;Destroy(oldMask);}
                var maskObject=new GameObject("PortraitClip",typeof(RectTransform),typeof(ProfileR2Graphic),typeof(Mask));maskObject.transform.SetParent(rect,false);Stretch((RectTransform)maskObject.transform);
                var maskGraphic=maskObject.GetComponent<ProfileR2Graphic>();maskGraphic.color=Color.white;maskGraphic.raycastTarget=false;maskObject.GetComponent<Mask>().showMaskGraphic=false;
                slot.portrait.transform.SetParent(maskObject.transform,false);Stretch(slot.portrait.rectTransform);
                slot.empty.enabled=false;var empty=SpriteImage(slot.empty.transform,"BoxTwoPaws",Resources.Load<Sprite>("UI/Latest20261020/Icons/box-empty-preserved-style"));Stretch(empty.rectTransform);
                slot.outline.enabled=false;
                var feedback=rect.GetComponent<ProfileR2Feedback>();slot.feedback=feedback;feedback.Layer.transform.SetAsLastSibling();
                feedback.KeepDisabledFace=true;
            }
            ApplyUnifiedBack();RefreshR2Skin(provider!=null?provider.GetSnapshot():default);FitR2Modals();
        }
        void FitR2Modals(){if(root==null)return;float scale=Mathf.Min(root.rect.width/1600,root.rect.height/900);foreach(var canvas in modalCanvases)if(canvas!=null)canvas.localScale=Vector3.one*scale;}
        void RefreshR2Skin(PlayerProfileSnapshot snapshot){
            if(displayNameText!=null){
                // Legacy Text allows horizontal overflow even when BestFit is enabled. Bound the one-line maximum using the same font metrics.
                var settings=displayNameText.GetGenerationSettings(Vector2.zero);settings.resizeTextForBestFit=false;settings.fontSize=48;settings.horizontalOverflow=HorizontalWrapMode.Overflow;
                float width=displayNameText.cachedTextGeneratorForLayout.GetPreferredWidth(displayNameText.text,settings)/displayNameText.pixelsPerUnit;
                int fit=width>0?Mathf.Clamp(Mathf.FloorToInt(48*displayNameText.rectTransform.rect.width/width),28,48):48;
                displayNameText.resizeTextMinSize=28;displayNameText.resizeTextMaxSize=fit;displayNameText.fontSize=fit;
            }
            changeAvatarButton?.GetComponent<ProfileR2Feedback>()?.RefreshDisabled();avatarCameraButton?.GetComponent<ProfileR2Feedback>()?.RefreshDisabled();
            if(changeAvatarButton!=null)foreach(var text in changeAvatarButton.GetComponentsInChildren<Text>())text.color=changeAvatarButton.interactable?GreenAngularGeometry.Light:Forest;
        }
        RectTransform Modal(Transform overlay,string name,float w,float h){
            var canvas=new GameObject("R2ModalCanvas",typeof(RectTransform)).GetComponent<RectTransform>();canvas.SetParent(overlay,false);SetCenter(canvas,0,0,1600,900);modalCanvases.Add(canvas);
            var card=CreatePanel(canvas,name,Color.clear,true);SetCenter(card,0,0,w,h);Skin(card,"popup");return card;
        }
        void ModalHeading(RectTransform card,string title,float width,System.Action close){
            var header=CreatePanel(card,"Header",Color.clear,false);SetTopLeft(header,16,16,width-32,56);Skin(header,"button","parallel",false);
            var text=CreateText(header,"Title",title,27,FontStyle.Bold,GreenAngularGeometry.Light,TextAnchor.MiddleLeft);text.font=TrainerMenuArt.Serif;SetTopStretch(text.rectTransform,14,6,68,44);
            var button=CreateOutlinedButton(card,"CloseButton","×",28);SetTopRight(button.GetComponent<RectTransform>(),32,24,40,40);SkinButton(button,"diamond");button.onClick.AddListener(()=>{if(PlayerActionGate.TryAccept(this,"R2Close"))close();});
        }
        void BuildR2NameDialog(){
            nameDialog=CreatePanel(root,"ChangeNameDialogOverlay",new Color(.075f,.11f,.19f,.64f),true);Stretch(nameDialog);
            var card=Modal(nameDialog,"Dialog",720,424);ModalHeading(card,"ĐỔI TÊN NGƯỜI CHƠI",720,HideNameDialog);
            var current=CreatePanel(card,"CurrentIdentity",Color.clear,false);SetTopLeft(current,24,88,672,80);Skin(current,"stat-card");
            renameCurrentAvatar=CirclePortrait(current,"CurrentAvatar",null,out var renameAvatarClip);SetTopLeft(renameAvatarClip,16,10,60,60);
            var caption=CreateText(current,"Caption","Tên hiện tại",15,FontStyle.Normal,Muted,TextAnchor.MiddleLeft);SetTopLeft(caption.rectTransform,100,8,540,24);
            renameCurrentName=CreateText(current,"CurrentName","",25,FontStyle.Bold,Forest,TextAnchor.MiddleLeft);renameCurrentName.font=TrainerMenuArt.Serif;SetTopLeft(renameCurrentName.rectTransform,100,34,540,36);
            var label=CreateText(card,"InputLabel","Tên mới",18,FontStyle.Bold,GreenAngularGeometry.Light,TextAnchor.MiddleLeft);SetTopLeft(label.rectTransform,24,178,400,28);
            renameCounter=CreateText(card,"CharacterCount","0 / 20",16,FontStyle.Normal,GreenAngularGeometry.Light,TextAnchor.MiddleRight);SetTopLeft(renameCounter.rectTransform,556,178,140,28);
            nameInput=CreateInputField(card,"NameInput",24,212,672,56);nameInputFrame=Skin(nameInput.GetComponent<RectTransform>(),"input");nameInput.textComponent.fontSize=20;
            nameInput.onValueChanged.AddListener(value=>{renameCounter.text=(value??"").Length+" / 20";if(nameError!=null)nameError.text=string.Empty;});
            var help=CreateText(card,"NameRules","Từ 2–20 ký tự; khoảng trắng đầu và cuối được bỏ.",16,FontStyle.Normal,GreenAngularGeometry.Light,TextAnchor.MiddleLeft);SetTopLeft(help.rectTransform,24,278,672,28);
            nameError=CreateText(card,"Error","",16,FontStyle.Normal,new Color(.95f,.76f,.74f),TextAnchor.MiddleLeft);SetTopLeft(nameError.rectTransform,24,308,672,28);
            var cancel=CreateOutlinedButton(card,"CancelButton","Hủy",20);SetTopLeft(cancel.GetComponent<RectTransform>(),24,348,328,52);SkinButton(cancel,"parallel",true);cancel.onClick.AddListener(()=>{if(PlayerActionGate.TryAccept(this,"HideNameDialog"))HideNameDialog();});
            var confirm=CreateOutlinedButton(card,"ConfirmButton","Đổi tên",20);SetTopLeft(confirm.GetComponent<RectTransform>(),368,348,328,52);SkinButton(confirm);confirm.onClick.AddListener(ConfirmNameChange);HideNameDialog();
        }
        void BuildR2AvatarDialog(){
            avatarDialog=CreatePanel(root,"AvatarPickerOverlay",new Color(.075f,.11f,.19f,.64f),true);Stretch(avatarDialog);
            var card=Modal(avatarDialog,"AvatarPickerDialog",984,488);ModalHeading(card,"CHỌN ẢNH ĐẠI DIỆN",984,HideAvatarDialog);
            var list=CreatePanel(card,"ChoicesPanel",Color.clear,false);SetTopLeft(list,24,96,536,244);Skin(list,"stat-card");
            var scroll=CreatePanel(list,"AvatarScroll",Color.clear,true);SetInset(scroll,8);avatarScroll=scroll.gameObject.AddComponent<ScrollRect>();avatarScroll.horizontal=false;avatarScroll.vertical=true;avatarScroll.movementType=ScrollRect.MovementType.Clamped;avatarScroll.scrollSensitivity=34;
            var viewport=CreatePanel(scroll,"Viewport",Color.clear,true);Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
            avatarGrid=CreatePanel(viewport,"AvatarGrid",Color.clear,false);avatarGrid.anchorMin=new Vector2(0,1);avatarGrid.anchorMax=Vector2.one;avatarGrid.pivot=new Vector2(.5f,1);avatarGrid.sizeDelta=Vector2.zero;avatarGrid.anchoredPosition=Vector2.zero;avatarScroll.content=avatarGrid;avatarScroll.viewport=viewport;
            var current=CreatePanel(card,"CurrentAvatarPanel",Color.clear,false);SetTopLeft(current,576,96,384,244);Skin(current,"stat-card");
            var caption=CreateText(current,"Caption","ẢNH HIỆN TẠI",15,FontStyle.Bold,Muted,TextAnchor.MiddleCenter);SetTopStretch(caption.rectTransform,12,8,12,30);
            var circle=CreatePanel(current,"CurrentAvatarCircle",Color.clear,false);SetTopLeft(circle,116,46,152,152);Skin(circle,"avatar-circle","circle");
            avatarCurrentImage=CirclePortrait(circle,"CurrentAvatar",null,out _);SetInset(avatarCurrentImage.rectTransform,15);avatarCurrentCaption=CreateText(current,"CurrentAvatarCaption","Đang dùng",18,FontStyle.Bold,Forest,TextAnchor.MiddleCenter);SetBottomStretch(avatarCurrentCaption.rectTransform,12,8,12,30);
            var note=CreateText(card,"ApplyImmediately","Chọn một ảnh sẽ áp dụng ngay và đóng bảng này.",18,FontStyle.Normal,GreenAngularGeometry.Light,TextAnchor.MiddleLeft);SetTopLeft(note.rectTransform,24,354,936,38);
            var close=CreateOutlinedButton(card,"DismissButton","←  Đóng",20);SetTopLeft(close.GetComponent<RectTransform>(),632,412,328,52);SkinButton(close);close.onClick.AddListener(()=>{if(PlayerActionGate.TryAccept(this,"HideAvatarDialog"))HideAvatarDialog();});HideAvatarDialog();
        }
        void RefreshR2PopupIdentity(PlayerProfileSnapshot snapshot){
            if(renameCurrentName!=null)renameCurrentName.text=snapshot.DisplayName??"Tên người chơi";
            if(renameCurrentAvatar!=null){renameCurrentAvatar.sprite=snapshot.Avatar;renameCurrentAvatar.color=snapshot.Avatar!=null?Color.white:Color.clear;}
            if(avatarCurrentImage!=null){avatarCurrentImage.sprite=snapshot.Avatar;avatarCurrentImage.color=snapshot.Avatar!=null?Color.white:Color.clear;}
            if(avatarCurrentCaption!=null)avatarCurrentCaption.text=snapshot.Avatar!=null?"Đang dùng":"Chưa có ảnh đại diện";
        }
        void RebuildR2AvatarGrid(){
            if(avatarGrid==null||provider==null)return;ClearChildren(avatarGrid);int visible=0;
            for(int index=0;index<provider.AvatarOptions.Count;index++){
                var sprite=provider.AvatarOptions[index];if(sprite==null)continue;int chosen=index;bool selected=index==provider.SelectedAvatarIndex;
                var card=CreatePanel(avatarGrid,"AvatarOption_"+index,Color.clear,true);SetTopLeft(card,4+(visible%3)*172,4+(visible/3)*228,160,216);
                var button=card.gameObject.AddComponent<Button>();button.targetGraphic=card.GetComponent<Image>();Skin(card,"stat-card");var feedback=card.GetComponent<ProfileR2Feedback>();feedback.SetSelected(selected);
                if(selected){var fill=card.Find("R2Fill").GetComponent<ProfileR2Graphic>();fill.Texture=null;fill.color=GreenAngularGeometry.Lavender;}
                var circle=CreatePanel(card,"AvatarCircle",Color.clear,false);SetTopLeft(circle,16,18,128,128);Skin(circle,"avatar-circle","circle");var art=CirclePortrait(circle,"Portrait",sprite,out _);SetInset(art.rectTransform,12);
                var label=CreateText(card,"Label","Ảnh "+(visible+1),18,FontStyle.Bold,Forest,TextAnchor.MiddleCenter);SetBottomStretch(label.rectTransform,8,18,8,36);
                if(selected){var mark=CreateText(card,"Selected","✓",22,FontStyle.Bold,GreenAngularGeometry.Primary,TextAnchor.MiddleCenter);SetTopRight(mark.rectTransform,8,5,30,30);}
                button.onClick.AddListener(()=>SelectAvatar(chosen));visible++;
            }
            if(visible==0){var empty=CreateText(avatarGrid,"Empty","Chưa có ảnh đại diện để chọn.",20,FontStyle.Italic,Muted,TextAnchor.MiddleCenter);SetTopStretch(empty.rectTransform,12,70,12,72);}
            int rows=Mathf.Max(1,Mathf.CeilToInt(visible/3f));avatarGrid.sizeDelta=new Vector2(0,8+rows*216+(rows-1)*12);
        }
    }
}
