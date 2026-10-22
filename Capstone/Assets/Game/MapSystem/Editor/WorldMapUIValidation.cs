using System;
using System.Linq;
using Capstone.Game.MapSystem.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem.Editor {
    public static class WorldMapUIValidation {
        [MenuItem("Tools/ToolCuaThang/Game Map/Validate World Map UI in Play Mode")]
        public static void Validate() {
            if (!Application.isPlaying) { Debug.LogWarning("Enter Play Mode before validating World Map UI."); return; }
            var map = UnityEngine.Object.FindFirstObjectByType<WorldMapController>();
            Require(map != null && map.Presentation != null, "map presentation binding missing");
            var ui = map.Presentation; var input = map.GetComponent<MapInputController>();
            bool wasOpen = map.IsOpen;
            if (!wasOpen) { if (input != null) input.OpenMap(); else map.OpenMap(true); }
            Canvas.ForceUpdateCanvases(); ui.FitViewport();
            int checks = 0;
            Action<bool,string> check = (value, message) => { Require(value,message); checks++; };
            var root = map.OverlayRoot;
            try {
                check(root.GetComponentsInChildren<MapFilterToggle>(true).Length == 7, "expected seven category filters");
                check(!root.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("Co-op")), "Co-op remained in UI");
                check(root.GetComponentsInChildren<Button>(true).All(b=>b.targetGraphic != null), "button has no target graphic");
                check(root.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text == "BẢN ĐỒ"), "Vietnamese header missing");
                check(ui.Theme.font != null && ui.Theme.markers.All(s=>s.icon != null), "font or category icon missing");
                Action checkBackdrop = () => {
                    var color = map.MapManager.GetComponent<Image>().color;
                    var expected = ui.Theme.background;
                    check(Mathf.Abs(color.r-expected.r)<.01f && Mathf.Abs(color.g-expected.g)<.01f
                        && Mathf.Abs(color.b-expected.b)<.01f && color.a>.9f, "map backdrop lost the presentation theme");
                };
                checkBackdrop();
                if (input != null) { input.CloseMap(); input.OpenMap(); }
                else { map.CloseMap(); map.OpenMap(true); }
                Canvas.ForceUpdateCanvases(); ui.FitViewport(); checkBackdrop();
                check(Mathf.Abs(map.MapCamera.aspect-ui.ViewportAspect)<.01f, "camera aspect differs from viewport");
                var maskCorners = new Vector3[4]; var markerCorners = new Vector3[4];
                map.MapInteractionRect.GetWorldCorners(maskCorners); ui.MarkerContainer.GetWorldCorners(markerCorners);
                for (int i=0;i<4;i++) check(Vector3.Distance(maskCorners[i],markerCorners[i])<.5f,"marker viewport and map mask differ");
                foreach (var type in new[] { MapMarkerType.QuestAvailable, MapMarkerType.QuestTarget, MapMarkerType.MainQuest, MapMarkerType.SideQuest, MapMarkerType.FastTravel, MapMarkerType.Travel }) {
                    var group = WorldMapUITheme.Group(type).Value; bool previous = ui.IsFilterVisible(group);
                    try { ui.SetFilter(group,false); check(!map.GetComponent<MapMarkerManager>().IsMarkerTypeVisible(type), "group filter did not reach " + type); }
                    finally { ui.SetFilter(group,previous); }
                }
                var view = ui.RenderMarker(new MapMarkerData {id="validation-map-marker",displayName="Validation",type=MapMarkerType.Pet,normalizedPosition=new Vector2(.25f,.75f)});
                check(((RectTransform)view.transform).anchorMin==new Vector2(.25f,.75f),"normalized marker position incorrect");
                ui.RenderMarker(new MapMarkerData {id="validation-map-marker",displayName="Updated",type=MapMarkerType.Shop,normalizedPosition=new Vector2(.8f,.2f),isVisible=false});
                check(view.Data.displayName=="Updated" && !view.gameObject.activeSelf,"marker update ignored new data");
                ui.RemoveMarker("validation-map-marker");
                float zoom = map.MapCamera.orthographicSize; var position = map.MapManager.GetCameraPosition();
                map.Zoom(1); check(map.MapCamera.orthographicSize<zoom,"zoom-in did not change real camera");
                map.MapManager.SetCameraOrthograpicSize(zoom);
                map.CenterOnPlayer(); check(map.MapManager.GetCameraPosition()!=Vector3.zero,"center action lost map camera");
                map.MapManager.SetCameraPosition(position);
                Debug.Log("World Map UI validation passed: " + checks + " checks; real map binding, viewport, buttons, grouped filters, normalized marker updates and camera actions.");
            }
            finally { if (!wasOpen) { if (input!=null) input.CloseMap(); else map.CloseMap(); } }
        }
        static void Require(bool value,string message) { if(!value) throw new InvalidOperationException("World Map UI: " + message); }
    }
}
