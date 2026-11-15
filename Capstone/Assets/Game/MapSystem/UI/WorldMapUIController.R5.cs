using Capstone.Game.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.UI {
    public sealed partial class WorldMapUIController {
        RectTransform r5Filters,r5Compact,r5Unavailable,r5Selection,r5NoDataBackdrop;
        TMP_Text r5Category;
        Image r5SelectedIcon;
        readonly Button[] r5Tools=new Button[3];
        Button r5Clear;
        RawImage r5Display;
        bool r5Collapsed;
        bool? r5Ready;
        public bool MapSourceReady => worldMap!=null&&worldMap.MapCamera!=null&&worldMap.MapCamera.isActiveAndEnabled
            &&worldMap.MapCamera.targetTexture!=null&&worldMap.MapCamera.targetTexture.IsCreated()
            &&r5Display!=null&&r5Display.enabled&&r5Display.gameObject.activeInHierarchy&&r5Display.texture==worldMap.MapCamera.targetTexture;
        internal bool BlocksMapPointer => clusters?.IsPopupOpen==true;

        void BuildR5() {
            if(theme==null||worldMap==null||overlayRoot==null||mapViewport==null)return;
            RestoreIcons();clusters?.Dispose();sourceViews.Clear();dataViews.Clear();selectedId=null;fittedViewportSize=Vector2.zero;r5Ready=null;
            mapSystem=GetComponent<MapSystemController>();
            if(quests==null)quests=FindFirstObjectByType<QuestSystem.QuestManager>();
            for(int i=overlayRoot.childCount-1;i>=0;i--){var old=overlayRoot.GetChild(i).gameObject;old.SetActive(false);if(Application.isPlaying)Destroy(old);else DestroyImmediate(old);}
            var oldMarkers=mapViewport.Find("MarkerContainer");if(oldMarkers!=null){oldMarkers.gameObject.SetActive(false);if(Application.isPlaying)Destroy(oldMarkers.gameObject);else DestroyImmediate(oldMarkers.gameObject);}
            if(overlayRoot.TryGetComponent(out Image surface)){surface.color=Color.clear;surface.raycastTarget=false;}
            if(overlayRoot.TryGetComponent(out Outline outline))outline.enabled=false;
            MapUIFactory.Stretch(overlayRoot);
            var page=mapViewport.parent.Find("MapR5Page");
            if(page==null){var pageRect=MapUIFactory.Rect(mapViewport.parent,"MapR5Page",0,0,0,0);MapUIFactory.Stretch(pageRect);var art=MapR5View.Face(pageRect,"rect",null,Color.white,false);art.Texture=MapR5View.Texture("Art/page");pageRect.SetAsFirstSibling();}
            screen=MapUIFactory.Rect(overlayRoot,"MapScreen",0,0,1920,1080);screen.anchorMin=screen.anchorMax=screen.pivot=new Vector2(.5f,.5f);screen.anchoredPosition=Vector2.zero;
            var header=MapR5View.Rect(screen,"TopHeader",0,0,1600,104);
            var ribbon=MapR5View.Rect(header,"TitleRibbon",28,24,644,64);var ribbonArt=MapR5View.Face(ribbon,"parallel",null,Color.white,false);ribbonArt.Texture=UnifiedPanelHeader.Texture("Components/title-ribbon__fill");
            var ribbonEdge=MapUIFactory.Rect(ribbon,"RibbonEdge",0,0,0,0);MapUIFactory.Stretch(ribbonEdge);var ribbonLine=MapR5View.Face(ribbonEdge,"parallel",null,Color.white,false);ribbonLine.Texture=UnifiedPanelHeader.Texture("Components/title-ribbon__border");
            MapR5View.Icon(header,"Emblem","compass",30,13,90);
            var title=MapR5View.Text(header,theme,"Title","BẢN ĐỒ",127,24,560,68,30,true,null,true);
            title.margin=new Vector4(0,0,4,4)*MapR5View.Native; // centre on the 64px ribbon within the shared 68px title rect
            MapR5View.Text(header,theme,"Subtitle","Khám phá · Điểm đánh dấu",744,34,288,46,17,false,GreenAngularGeometry.Light);
            var coordinates=MapR5View.Rect(header,"RegionPanel",1044,34,330,46);MapR5View.Face(coordinates,"parallel","map-filter",MapR5View.DarkTint);
            MapR5View.Icon(coordinates,"LocationIcon","coordinates",16,10,25);
            MapR5View.Text(coordinates,theme,"RegionCaption","VỊ TRÍ NGƯỜI CHƠI",56,1,264,20,14,false,GreenAngularGeometry.Light);
            region=coordinates.gameObject.AddComponent<RegionDisplayPanel>();region.Bind(MapR5View.Text(coordinates,theme,"Region","X —   ·   Z —",56,20,264,24,17,false,GreenAngularGeometry.Light));
            var back=MapR5View.Button(MapR5View.Rect(header,"BackButton",1408,34,158,46),theme,"Trở lại","back",font:21);
            back.GetComponentInChildren<TMP_Text>().color=GreenAngularGeometry.Light;back.gameObject.AddComponent<MapActionButton>().Bind(this,"map-back",worldMap.RequestBackFromUI);
            var frame=MapR5View.Rect(screen,"MapFrame",24,112,1552,706);var frameArt=MapR5View.Face(frame,"panel","map-shell");frameArt.Cutout=true;
            var frameBorder=frame.Find("Border")?.GetComponent<MapR5Graphic>();if(frameBorder!=null)frameBorder.Cutout=true;
            var maskImage=mapViewport.GetComponent<Image>();if(maskImage==null)maskImage=mapViewport.gameObject.AddComponent<Image>();
            maskImage.sprite=MapR5View.Sprite("Components/map-viewport__mask");maskImage.type=Image.Type.Simple;maskImage.preserveAspect=false;maskImage.color=Color.white;maskImage.raycastTarget=false;
            var stencil=mapViewport.GetComponent<Mask>();if(stencil==null)stencil=mapViewport.gameObject.AddComponent<Mask>();stencil.showMaskGraphic=false;
            if(mapViewport.TryGetComponent(out RectMask2D rectangularMask))rectangularMask.enabled=false;
            var contour=mapViewport.GetComponent<MapR5Contour>();if(contour==null)contour=mapViewport.gameObject.AddComponent<MapR5Contour>();contour.Viewport=true;
            r5Display=mapViewport.Find("Map Display")?.GetComponent<RawImage>();
            markerContainer=MapUIFactory.Rect(mapViewport,"MarkerContainer",0,0,0,0);MapUIFactory.Stretch(markerContainer);
            clusters=new MapMarkerClusters(markerContainer,theme,SelectMarker,screen);
            BuildR5Compass("N",786,112);BuildR5Compass("E",1560,452);BuildR5Compass("S",786,802);BuildR5Compass("W",12,452);
            var tracked=MapR5View.Rect(screen,"TrackedQuestOverlay",56,152,344,64);MapR5View.Face(tracked,"panel","map-cluster-popup",MapR5View.DarkTint);
            MapR5View.Icon(tracked,"QuestIcon","quest",14,11,39);MapR5View.Text(tracked,theme,"Caption","ĐANG THEO DÕI",66,5,262,22,14,true,GreenAngularGeometry.Gold);
            questBanner=tracked.gameObject.AddComponent<TrackedQuestBanner>();questBanner.Bind(quests,MapR5View.Text(tracked,theme,"TrackedQuestTitle","",66,27,262,29,20,true,GreenAngularGeometry.Light));
            var helper=MapR5View.Rect(screen,"MapHelp",56,772,662,30);MapR5View.Face(helper,"parallel",null,GreenAngularGeometry.Primary,false);
            MapR5View.Text(helper,theme,"Help","Kéo bản đồ · Lăn chuột thu/phóng · Chuột phải đặt dấu",8,0,646,30,16,false,GreenAngularGeometry.Light,false,TextAlignmentOptions.Center);
            for(int i=0;i<3;i++){
                string[] ids={"ZoomIn","ZoomOut","Center"},icons={"zoom-in","zoom-out","center-player"};
                var button=MapR5View.Button(MapR5View.Rect(screen,ids[i],1492,570+i*68,56,56),theme,"",null,"diamond","map-tool");
                var glyph=MapR5View.Icon(button.transform,"ToolGlyph",icons[i],12,12,32);button.GetComponent<MapR5Feedback>().Glyph=glyph;r5Tools[i]=button;
            }
            r5Tools[0].gameObject.AddComponent<MapActionButton>().Bind(this,"map-zoom-in",()=>worldMap.Zoom(1));
            r5Tools[1].gameObject.AddComponent<MapActionButton>().Bind(this,"map-zoom-out",()=>worldMap.Zoom(-1));
            r5Tools[2].gameObject.AddComponent<MapActionButton>().Bind(this,"map-center",worldMap.CenterOnPlayer);
            r5Filters=MapR5View.Rect(screen,"BottomFilterBar",0,0,1600,900);
            for(int i=0;i<7;i++)BuildR5Filter(i);
            var collapse=MapR5View.Button(MapR5View.Rect(r5Filters,"CollapseFilters",1284,832,52,44),theme,"");
            collapse.GetComponent<MapR5Feedback>().Glyph=MapR5View.Icon(collapse.transform,"CollapseGlyph","collapse",14,10,24);
            collapse.gameObject.AddComponent<MapActionButton>().Bind(this,"map-collapse",()=>SetFilterDockCollapsed(true));
            r5Compact=MapR5View.Rect(screen,"CompactFilterBar",0,0,1600,900);
            var expand=MapR5View.Button(MapR5View.Rect(r5Compact,"ExpandFilters",24,832,250,44),theme,"Bộ lọc · 7 nhóm","filter",font:19);
            expand.GetComponentInChildren<TMP_Text>().rectTransform.sizeDelta-=new Vector2(24*MapR5View.Native,0);
            MapR5View.Icon(expand.transform,"ExpandGlyph","expand",214,10,24);expand.gameObject.AddComponent<MapActionButton>().Bind(this,"map-expand",()=>SetFilterDockCollapsed(false));
            for(int i=0;i<7;i++)MapR5View.Icon(r5Compact,"Compact_"+i,MapR5View.GroupIcons[i],292+i*36,841,26).color=GreenAngularGeometry.FilterGlyph(visibleGroups[i]);
            SetFilterDockCollapsed(r5Collapsed);
            r5Clear=MapR5View.Button(MapR5View.Rect(screen,"ClearWaypointButton",1352,832,224,44),theme,"Xóa dấu","clear-waypoint");
            r5Clear.gameObject.AddComponent<MapActionButton>().Bind(this,"map-clear",worldMap.ClearWaypoint);
            r5Selection=MapR5View.Rect(screen,"SelectedMarker",56,665,382,90);MapR5View.Face(r5Selection,"panel","map-cluster-popup");
            r5SelectedIcon=MapR5View.Icon(r5Selection,"SelectedIcon","quest",16,18,48);
            r5Category=MapR5View.Text(r5Selection,theme,"Category","",82,8,288,24,14,false,GreenAngularGeometry.Muted);
            selectedLabel=MapR5View.Text(r5Selection,theme,"Name","",82,32,288,45,25,true,null,true);r5Selection.gameObject.SetActive(false);
            markerTooltip=MapR5View.Rect(screen,"MarkerTooltip",0,0,300,78);markerTooltip.anchorMin=markerTooltip.anchorMax=new Vector2(.5f,.5f);markerTooltip.pivot=new Vector2(0,1);
            MapR5View.Face(markerTooltip,"panel","map-cluster-popup");markerTooltipText=MapR5View.Text(markerTooltip,theme,"Text","",14,9,272,60,16);HideMarkerTooltip();
            r5NoDataBackdrop=MapR5View.Rect(screen,"UnavailableViewport",40,128,1520,678.689956f);r5NoDataBackdrop.SetSiblingIndex(1);
            var noDataArt=MapR5View.Face(r5NoDataBackdrop,"panel",null,Color.white,false);noDataArt.Padding=0;noDataArt.Texture=MapR5View.Texture("Art/page");
            r5Unavailable=MapR5View.Rect(screen,"MapUnavailable",500,334,600,258);MapR5View.Face(r5Unavailable,"panel","map-cluster-popup");
            var badge=MapR5View.Rect(r5Unavailable,"MapBadge",254,22,92,92);MapR5View.Face(badge,"skill-oct","map-cluster");MapR5View.Icon(badge,"MapGlyph","map",20,18,52);
            MapR5View.Text(r5Unavailable,theme,"EmptyTitle","Chưa có dữ liệu bản đồ",20,116,560,45,30,true,null,true,TextAlignmentOptions.Center);
            MapR5View.Text(r5Unavailable,theme,"EmptyHint","Bản đồ sẽ hiện khi nguồn hiển thị sẵn sàng.",20,162,560,32,19,false,GreenAngularGeometry.Muted,false,TextAlignmentOptions.Center);
            MapR5View.Text(r5Unavailable,theme,"EmptyDetail","Các điểm đánh dấu chỉ hiển thị trên bản đồ hợp lệ.",20,197,560,30,17,false,GreenAngularGeometry.Muted,false,TextAlignmentOptions.Center);
            worldMap.UsePresentation(this,overlayRoot);FitR5Viewport();nextMarkerRefresh=0;
        }
        void BuildR5Compass(string value,float x,float y) {
            var rect=MapR5View.Rect(screen,"Compass"+value,x,y,28,28);MapR5View.Face(rect,"diamond",null,GreenAngularGeometry.Primary);
            MapR5View.Text(rect,theme,"Direction",value,0,0,28,28,15,false,GreenAngularGeometry.Light,false,TextAlignmentOptions.Center);
        }
        void BuildR5Filter(int index) {
            var rect=MapR5View.Rect(r5Filters,"Filter"+(MapFilterGroup)index,24+index*180,832,172,44);
            var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;
            rect.gameObject.AddComponent<MapR5Contour>().Shape="parallel";
            var toggle=rect.gameObject.AddComponent<Toggle>();toggle.targetGraphic=hit;toggle.transition=Selectable.Transition.None;toggle.toggleTransition=Toggle.ToggleTransition.None;
            rect.gameObject.AddComponent<MapR5SubmitGuard>();
            var baseFace=MapR5View.FaceChild(rect,"parallel","map-filter");
            var wash=MapUIFactory.Rect(rect,"Feedback",0,0,0,0);MapUIFactory.Stretch(wash);var overlay=MapR5View.Face(wash,"parallel",null,Color.clear,false);
            var icon=MapR5View.Icon(rect,"Icon",MapR5View.GroupIcons[index],13,8,28);
            var caption=MapR5View.Text(rect,theme,"Label",FilterName((MapFilterGroup)index),42,0,118,44,17,true);
            toggle.graphic=null; // The full surface communicates on/off; no tick glyph.
            rect.gameObject.AddComponent<MapFilterToggle>().Bind((MapFilterGroup)index,visibleGroups[index],SetFilter);
            var feedback=rect.gameObject.AddComponent<MapR5Feedback>();feedback.BaseFace=baseFace;feedback.Bind(toggle,overlay,icon,caption);filterToggles[index]=toggle;
        }
        public void SetFilterDockCollapsed(bool value) {
            r5Collapsed=value;if(r5Filters!=null)r5Filters.gameObject.SetActive(!value);if(r5Compact!=null)r5Compact.gameObject.SetActive(value);
        }
        void FitR5Viewport() {
            if(screen==null||mapViewport==null)return;
            Vector2 size=overlayRoot.rect.size;if(size==fittedViewportSize)return;
            float scale=Mathf.Min(size.x/1920,size.y/1080);if(scale<=0)return;fittedViewportSize=size;screen.localScale=Vector3.one*scale;
            const float n=MapR5View.Native;
            var center=screen.TransformPoint(new Vector3(-960+(40+760)*n,540-(128+678.689956f/2)*n));
            var worldSize=screen.TransformVector(new Vector3(1520*n,678.689956f*n,0));
            var localSize=mapViewport.parent.InverseTransformVector(worldSize);
            mapViewport.anchorMin=mapViewport.anchorMax=mapViewport.pivot=new Vector2(.5f,.5f);mapViewport.localScale=Vector3.one;
            mapViewport.sizeDelta=new Vector2(Mathf.Abs(localSize.x),Mathf.Abs(localSize.y));mapViewport.position=center;
            if(markerContainer!=null)MapUIFactory.Stretch(markerContainer);
        }
        bool UpdateR5Availability() {
            bool ready=MapSourceReady;
            if(r5Ready!=ready){r5Ready=ready;markerContainer.gameObject.SetActive(ready);r5Unavailable.gameObject.SetActive(!ready);r5NoDataBackdrop.gameObject.SetActive(!ready);screen.Find("MapHelp").gameObject.SetActive(ready);
                questBanner?.SetMapReady(ready);foreach(var tool in r5Tools){tool.interactable=ready;tool.GetComponent<MapR5Feedback>().Refresh();}
                if(!ready){clusters?.Close();HideMarkerTooltip();r5Selection.gameObject.SetActive(false);}}
            return ready;
        }
        void UpdateSelectedCard(MapMarkerData data) {
            var group=WorldMapUITheme.Group(data.type);string label=group.HasValue?FilterName(group.Value):data.type==MapMarkerType.Player?"Người chơi":"Điểm đánh dấu";
            r5Category.text="ĐIỂM ĐÃ CHỌN · "+label.ToUpperInvariant();r5SelectedIcon.sprite=MapR5View.Sprite("Icons/"+MapR5View.IconFor(data.type));
        }
        MapMarkerView CreateR5Marker(MapMarkerData data) {
            float size=MapR5View.MarkerSize(data.type);
            var rect=MapR5View.Rect(markerContainer,"Marker_"+data.id,0,0,size,size);rect.pivot=new Vector2(.5f,.5f);
            var hit=rect.gameObject.AddComponent<Image>();hit.color=Color.clear;var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=hit;button.transition=Selectable.Transition.None;
            rect.gameObject.AddComponent<MapR5SubmitGuard>();
            float effect=size*96/44;float margin=(effect-size)/2;
            var selection=MapUIFactory.Icon(rect,"Selection",MapR5View.Sprite("Effects/marker-selected"),-margin*MapR5View.Native,-margin*MapR5View.Native,effect*MapR5View.Native,Color.white);selection.enabled=false;
            var hover=MapUIFactory.Icon(rect,"Hover",MapR5View.Sprite("Effects/marker-hover"),-margin*MapR5View.Native,-margin*MapR5View.Native,effect*MapR5View.Native,Color.white);hover.enabled=false;
            var focus=MapUIFactory.Icon(rect,"Focus",MapR5View.Sprite("Effects/marker-focus"),-margin*MapR5View.Native,-margin*MapR5View.Native,effect*MapR5View.Native,Color.white);focus.enabled=false;
            MapR5View.Icon(rect,"Icon",MapR5View.IconFor(data.type),0,0,size);
            if(data.type==MapMarkerType.Player||data.type==MapMarkerType.Waypoint){float w=data.type==MapMarkerType.Player?58:156;var tag=MapR5View.Rect(rect,"MarkerTag",(size-w)/2,size+5,w,30);MapR5View.Face(tag,"parallel",null,GreenAngularGeometry.Primary,false);MapR5View.Text(tag,theme,"Caption",data.type==MapMarkerType.Player?"Bạn":"Điểm đánh dấu",4,0,w-8,30,16,false,GreenAngularGeometry.Light,false,TextAlignmentOptions.Center);}
            var view=rect.gameObject.AddComponent<MapMarkerView>();view.Bind(data,selection,SelectMarker,HoverMarker);return view;
        }
        void ApplyR5MarkerAppearance(MapMarkerView view) {
            view.transform.Find("Icon").GetComponent<Image>().sprite=MapR5View.Sprite("Icons/"+MapR5View.IconFor(view.Data.type));
        }
        void RecoverR5KeyboardSubmit() {
            bool pressed=false;
#if ENABLE_LEGACY_INPUT_MANAGER
            pressed=Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||Input.GetKeyDown(KeyCode.Space);
#endif
#if ENABLE_INPUT_SYSTEM
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            pressed|=keyboard!=null&&(keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame||keyboard.spaceKey.wasPressedThisFrame);
#endif
            if(!pressed)return;
            var selected=UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
            var root=NavigationRoot;
            if(selected!=null&&root!=null&&selected.transform.IsChildOf(root))selected.GetComponent<MapR5SubmitGuard>()?.SubmitIfMissing();
        }
    }
}
