using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.MapSystem.UI;
using Capstone.Game.ProfileSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;
using UITK = UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    public static class UiExperienceValidation {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "ui-experience/";
        static readonly List<string> results = new List<string>();
        static bool running;
        const string StartedForValidation = "Capstone.UiExperienceValidation.Started";
        static double nextPoll;
        static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
        static void Invoke(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target,args);
        static void Check(bool success,string label) { if(!success) throw new InvalidOperationException(label); results.Add("PASS "+label); }
        static void Write(string name) { Directory.CreateDirectory(Output); File.WriteAllLines(Output+name,results); }
        static void Key(UITK.VisualElement target,KeyCode key) {
            using(var evt=UITK.KeyDownEvent.GetPooled(new Event { type=EventType.KeyDown,keyCode=key })) { evt.target=target; target.SendEvent(evt); }
        }
        static void Submit(UITK.VisualElement target) {
            using(var evt=UITK.NavigationSubmitEvent.GetPooled()) { evt.target=target; target.SendEvent(evt); }
        }
        static async Task Capture(string name) { Directory.CreateDirectory(Output+"screenshots"); ScreenCapture.CaptureScreenshot(Output+"screenshots/"+name+".png"); await Task.Delay(220); }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate UI Experience in Play Mode")]
        public static async void RunPlay() {
            if(running || !Application.isPlaying) return;
            running=true; results.Clear();
            var menu=Object.FindFirstObjectByType<GameMenuController>();
            var inventory=Object.FindFirstObjectByType<MonsterInventoryController>();
            var profile=Object.FindFirstObjectByType<ProfilePanelController>();
            var maps=Object.FindFirstObjectByType<MapSystemController>();
            try {
                await Task.Delay(500);
                Check(menu!=null && inventory!=null && profile!=null && maps!=null,"TestChar UI owners available");
                var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
                Check(MenuTypography.Font==font,"shared font references existing LiberationSans asset");
                inventory.Close(); profile.Close(); maps.CloseWorldMap(); menu.CloseMenu(); await Task.Delay(220);
                menu.HandleShortcut(KeyCode.Tab);
                Check(menu.IsMenuOpen && InventoryInputController.GameplayInputBlocked && menu.AllowsCameraTracking,"first Tab opens Menu and preserves passive camera tracking");
                menu.HandleShortcut(KeyCode.Tab);
                Check(menu.IsMenuOpen,"same key edge delivered twice in one frame executes once");
                var menuRoot=Field<RectTransform>(menu,"root");
                foreach(var text in menuRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true)) Check(text.font==font || text.font==TrainerMenuArt.Serif,"Trainer uses its scoped serif or shared small-text font: "+text.name);
                var menuBack=menuRoot.Find("TrainerMenuPanel/MenuContent/MenuFooter/BackButton");
                // Footer names can evolve; find the button by its stable component name.
                if(menuBack==null) foreach(var button in menuRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true)) if(button.name=="BackButton") { menuBack=button.transform; break; }
                Check(menuBack.Find("Label").GetComponent<UnityEngine.UI.Text>().text=="Trở lại" && menuBack.Find("TabKeycap/Key").GetComponent<UnityEngine.UI.Text>().text=="Tab","Trainer Back uses v3 caption and the sole Tab keycap");
                await Capture("TrainerMenu"); await Task.Delay(200); menu.HandleShortcut(KeyCode.Tab);
                Check(!menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked,"second Tab closes Menu and restores gameplay");
                var doc=inventory.GetComponent<UITK.UIDocument>(); var root=doc.rootVisualElement;
                var cases=new[] { KeyCode.I,KeyCode.Q,KeyCode.P,KeyCode.M };
                foreach(var key in cases) {
                    await Task.Delay(220); menu.HandleShortcut(key); await Task.Delay(220);
                    bool active=key==KeyCode.I ? inventory.IsBagSectionOpen : key==KeyCode.Q ? inventory.IsQuestSectionOpen : key==KeyCode.P ? inventory.IsPetsSectionOpen : maps.IsWorldMapOpen;
                    Check(active && InventoryInputController.GameplayInputBlocked,"shortcut opens correct panel "+key);
                    if(key!=KeyCode.M) {
                        string id=key==KeyCode.I ? "inventory-panel" : key==KeyCode.Q ? "quest-panel" : "pets-panel";
                        var panel=root.Q(id); var back=panel.Q<UITK.Button>(className:"screen-back"); back.Focus();
                        Check(back.text==VietnameseUI.Back && back.resolvedStyle.unityFont==font && back.resolvedStyle.fontSize==15,"shared font and Back style "+key);
                        var title=panel.Q<UITK.Label>(className:"screen-title"); Check(title.resolvedStyle.unityFont==font && title.resolvedStyle.fontSize==38,"shared title font "+key);
                        var body=panel.Q<UITK.Label>(); Check(body.resolvedStyle.unityFont==font,"panel body inherits shared font "+key);
                    }
                    await Capture(key.ToString()); menu.HandleShortcut(key); await Task.Delay(220);
                    Check(!inventory.IsOpen && !maps.IsWorldMapOpen && !menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked,"same shortcut closes to gameplay "+key);
                }
                menu.HandleShortcut(KeyCode.Tab); await Task.Delay(220); menu.HandleShortcut(KeyCode.I); await Task.Delay(220);
                root.Q("inventory-panel").Q<UITK.Button>(className:"screen-back").Focus(); menu.HandleShortcut(KeyCode.I); await Task.Delay(220);
                Check(menu.IsMenuOpen && !inventory.IsOpen,"same shortcut returns to originating Trainer Menu");
                menu.CloseMenu(); await Task.Delay(220); menu.HandleShortcut(KeyCode.Q); await Task.Delay(220);
                var questBack=root.Q("quest-panel").Q<UITK.Button>(className:"screen-back"); questBack.Focus();
                Key(questBack,KeyCode.Q); menu.HandleShortcut(KeyCode.Q);
                Check(!inventory.IsOpen && !menu.IsMenuOpen,"UI event plus legacy shortcut cannot reopen Quest in same frame");
                await Task.Delay(220); menu.HandleShortcut(KeyCode.I); await Task.Delay(220);
                var search=root.Q<UITK.TextField>("inventory-search");
                if(search==null) search=root.Q("inventory-panel").Q<UITK.TextField>();
                search.Focus(); await Task.Delay(100);
                Check(inventory.GetComponent<InventoryInputController>().IsEditingUiText(),"search focus is recognized as text editing");
                menu.HandleShortcut(KeyCode.I); menu.HandleShortcut(KeyCode.Q); menu.HandleShortcut(KeyCode.P);
                Check(inventory.IsBagSectionOpen,"typing shortcut letters does not close or switch Bag");
                root.Q("inventory-panel").Q<UITK.Button>(className:"screen-back").Focus(); await Task.Delay(220); menu.HandleShortcut(KeyCode.I); await Task.Delay(220);
                menu.OpenMenu(); await Task.Delay(220); menu.OpenProfileDirect(); await Task.Delay(220);
                var profileRoot=Field<RectTransform>(profile,"root");
                foreach(var text in profileRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true)) Check(text.font==font,"Profile font: "+text.name);
                Check(profileRoot.Find("ProfileScreen/HeaderBar/BackButton").GetComponentInChildren<UnityEngine.UI.Text>().text==VietnameseUI.Back,"Profile common Back caption");
                await Capture("Profile"); Invoke(profile,"ShowNameDialog"); await Task.Delay(220);
                menu.HandleShortcut(KeyCode.I); Check(profile.IsOpen && !inventory.IsOpen,"editing player name suppresses Bag shortcut");
                menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(profile.IsOpen && !Field<RectTransform>(profile,"nameDialog").gameObject.activeSelf,"Escape closes rename popup before Profile");
                menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(!profile.IsOpen && menu.IsMenuOpen,"next Escape returns Profile to Menu");
                menu.HandleShortcut(KeyCode.Tab); await Task.Delay(220);
                menu.OpenProfileDirect(); await Task.Delay(220); menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(!profile.IsOpen && !menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked,"direct Profile returns to gameplay using same Back route");
                menu.OpenBoxDirect(); await Task.Delay(220);
                var box=root.Q("box-panel");
                Check(box.Q<UITK.Button>(className:"screen-back").text==VietnameseUI.Back && box.Q<UITK.Label>(className:"screen-title").resolvedStyle.unityFont==font,"Box shares title and Back font");
                await Capture("Box");
                inventory.RequestClose(); await Task.Delay(220); menu.CloseMenu();
                menu.OpenShopFromWorld(); await Task.Delay(220);
                var dropdown=root.Q<UITK.DropdownField>("shop-sort");
                dropdown.Focus(); Submit(dropdown); await Task.Delay(150);
                var popup=root.panel.visualTree.Q(className:"unity-base-dropdown__container-outer"); Check(popup!=null,"native dropdown is visible");
                Check(popup.ClassListContains("menu-native-popup") && popup.resolvedStyle.unityFont==font,"keyboard-opened dropdown inherits shared font and theme");
                var content=popup.Q<UITK.ScrollView>().contentContainer;
                using(var cancel=UITK.NavigationCancelEvent.GetPooled()) { cancel.target=content; content.SendEvent(cancel); }
                menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(inventory.IsShopOpen && root.panel.visualTree.Q(className:"unity-base-dropdown__container-outer")==null,"native cancel plus legacy Escape preserves Shop: shop="+inventory.IsShopOpen+", popup="+(root.panel.visualTree.Q(className:"unity-base-dropdown__container-outer")!=null));
                dropdown.Focus(); Submit(dropdown); await Task.Delay(220); menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(inventory.IsShopOpen && root.panel.visualTree.Q(className:"unity-base-dropdown__container-outer")==null,"legacy Escape first cancels native dropdown without closing screen");
                inventory.RequestClose(); await Task.Delay(220); menu.CloseMenu();
                var mapUI=maps.GetComponent<WorldMapUIController>();
                Check(mapUI.Theme.font.sourceFontFile==font,"Map TMP uses same font source");
                var hud=Object.FindFirstObjectByType<GameplayHudController>(); var hudRoot=Field<RectTransform>(hud,"root");
                var legacyDirections=maps.Minimap.Manager.transform.Find("Minimap Directions");
                Check(legacyDirections==null || !legacyDirections.gameObject.activeSelf,"unused legacy minimap direction labels stay hidden");
                foreach(var text in hudRoot.GetComponentsInChildren<UnityEngine.UI.Text>(true)) {
                    // Imported minimap controls are hidden; validate the generated HUD's own text.
                    if(text.transform.IsChildOf(maps.Minimap.Manager.transform)) continue;
                    Check(text.font==font,"HUD font: "+AnimationUtility.CalculateTransformPath(text.transform,hudRoot)+" ("+(text.font!=null?text.font.name:"null")+")");
                }
                maps.SetUserMinimapVisible(false); maps.SetMinimapVisible(true);
                Check(!maps.Minimap.Camera.gameObject.activeSelf,"hidden minimap stops its camera");
                menu.HandleShortcut(KeyCode.Tab); await Task.Delay(220); menu.HandleShortcut(KeyCode.Tab); await Task.Delay(220);
                Check(!maps.Minimap.Camera.gameObject.activeSelf,"menu open and close cannot override minimap preference");
                menu.HandleShortcut(KeyCode.M); await Task.Delay(220);
                Check(maps.IsWorldMapOpen,"full Map remains available with minimap hidden");
                menu.HandleShortcut(KeyCode.M); await Task.Delay(220);
                maps.SetUserMinimapVisible(Capstone.Game.SettingsSystem.GameSettingsService.Instance.Model.Data.showMinimap);
                Write("runtime-validation.txt"); Debug.Log("UI experience: "+results.Count+" checks passed.");
            } catch(Exception e) { results.Add("FAIL "+e); Write("runtime-validation.txt"); Debug.LogException(e); }
            finally {
                inventory?.Close(); profile?.Close(); maps?.CloseWorldMap(); menu?.CloseMenu();
                maps?.SetUserMinimapVisible(Capstone.Game.SettingsSystem.GameSettingsService.Instance?.Model.Data.showMinimap??true);
                running=false;
                if (SessionState.GetBool(StartedForValidation,false)) { SessionState.SetBool(StartedForValidation,false); EditorApplication.isPlaying=false; }
            }
        }
        [InitializeOnLoadMethod] static void Initialize() { EditorApplication.update-=Poll; EditorApplication.update+=Poll; }
        static void Poll() {
            if(running || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<nextPoll) return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            const string request="Temp/RunUiExperience.request";
            const string start="Temp/StartUiExperience.request";
            if(!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(start) && (DateTime.UtcNow-File.GetLastWriteTimeUtc(start)).TotalSeconds>1) {
                File.Delete(start); SessionState.SetBool(StartedForValidation,true);
                File.WriteAllText(request,"UI validation"); EditorApplication.isPlaying=true; return;
            }
            if(Application.isPlaying && File.Exists(request) && (DateTime.UtcNow-File.GetLastWriteTimeUtc(request)).TotalSeconds>1) { File.Delete(request); RunPlay(); }
        }
    }
}
