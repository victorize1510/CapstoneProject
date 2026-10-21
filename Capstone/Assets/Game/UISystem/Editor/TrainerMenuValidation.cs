using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using Capstone.Game.ProfileSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Menu-only fixtures and non-consuming routing checks. Never saves a scene or uses items.</summary>
    public static class TrainerMenuValidation {
        const string Output = "../artifacts/trainer-menu-v4/";
        const string Request = "Temp/RunTrainerMenu.request";
        const string StartedPlay = "TrainerMenuValidation.StartedPlay";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> results = new List<string>();
        static bool running;
        static double nextPoll;
        [InitializeOnLoadMethod] static void Initialize() {
            EditorApplication.update -= Poll; EditorApplication.update += Poll;
        }
        static void Poll() {
            if (running || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            string command = File.ReadAllText(Request).Trim();
            if (command == "play") {
                if (!EditorApplication.isPlaying) {
                    if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                    SessionState.SetBool(StartedPlay, true); EditorApplication.EnterPlaymode(); return;
                }
                if (Object.FindFirstObjectByType<GameMenuController>() == null) return;
                File.Delete(Request); RunPlay();
            } else if (!EditorApplication.isPlayingOrWillChangePlaymode) {
                File.Delete(Request); Run();
            }
        }
        static void Check(bool valid, string message) {
            results.Add((valid ? "PASS " : "FAIL ") + message);
            if (!valid) throw new InvalidOperationException(message);
        }
        static void Finish(string name) {
            Directory.CreateDirectory(Output); File.WriteAllLines(Output + name + ".txt", results);
            running = false;
            Debug.Log("Trainer Menu " + name + ": " + results.Count(x => x.StartsWith("PASS")) + " checks; failures=" + results.Count(x => !x.StartsWith("PASS")));
        }
        [MenuItem("Game Tools/GameToolThang/HUD/Validate Trainer Menu V4 Layout")]
        public static void Run() {
            if (running) return;
            running = true; results.Clear(); Directory.CreateDirectory(Output);
            var scene = EditorSceneManager.NewPreviewScene();
            try {
                // New PNGs can be discovered before an updated postprocessor assembly is loaded.
                // Reimport this small, explicit set; never invalidate unrelated project art.
                foreach (string path in Directory.GetFiles(TrainerMenuImportPostprocessor.Root + "Polish/", "*.png"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                foreach (string path in Directory.GetFiles(TrainerMenuImportPostprocessor.Root + "V4/", "*.png"))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var files = Directory.GetFiles(TrainerMenuImportPostprocessor.Root, "*.png");
                Check(files.Length == 9, "nine standalone handoff assets, no flattened concept");
                foreach (string path in files) {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Check(sprite != null, "Sprite imports: " + Path.GetFileName(path));
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    Check(!importer.mipmapEnabled && !importer.isReadable && importer.alphaIsTransparency, "UI alpha/no-mip/no-CPU-copy: " + Path.GetFileName(path));
                }
                Check(TrainerMenuArt.Serif != null, "Menu-only Noto Serif imports");
                foreach (char c in "ĐđĂăÂâÊêÔôƠơƯưÁÀẢÃẠẮẰẲẴẶẤẦẨẪẬÉÈẺẼẸẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÝỲỶỸỴ")
                    Check(TrainerMenuArt.Serif.HasCharacter(c), "Serif Vietnamese glyph " + c);
                foreach (string name in new[] { "10-v4-unified-shell-border", "11-v4-navigation-border" }) {
                    var sprite = TrainerMenuArt.Load("V4/"+name);
                    Check(sprite != null && sprite.border.x > 0, "cropped thin frame with explicit slice corners " + name);
                }
                Check(TrainerMenuArt.Polish("surface").border == Vector4.one * 16 && TrainerMenuArt.Polish("focus").border == Vector4.one * 16, "native surface/focus slice corners stay proportional");
                var go = New(scene, "MenuFixture", typeof(RectTransform), typeof(Canvas));
                var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.scaleFactor = 2;
                var rect = (RectTransform)go.transform; rect.sizeDelta = new Vector2(1600, 900);
                // Fixture backdrop only; the runtime Menu shows the actual game world.
                GuiKitControls.Background(rect, "FixtureBackdrop");
                var actions = new List<string>();
                var view = new TrainerMenuView(rect, null, actions.Add);
                var shell = go.GetComponentsInChildren<Image>().Single(x=>x.name=="MenuShell");
                Check(shell.rectTransform.rect.size==new Vector2(865,891), "V4 continuous shell encloses profile through footer");
                foreach(string name in new[] {"MenuShell","TrainerSummary","PetsButton","InventoryButton","ProfileButton","QuestButton","MapButton","CodexButton","QuickInfo"}) {
                    var image=go.GetComponentsInChildren<Image>().Single(x=>x.name==name);
                    Check(image.color.r<.7f && image.color.b>.5f && image.color.a>.9f,"V4 mineral/jade surface, no ivory: "+name);
                }
                Check(!view.PetArtwork.enabled && view.PetArtwork.sprite == null, "user-assigned 2D pet artwork starts empty");
                Check(go.GetComponentsInChildren<Button>().Length == 9, "eight navigation actions plus existing profile edit entry; no Box");
                Check(!go.GetComponentsInChildren<Transform>().Any(x => x.name.Contains("Box") || x.name.Contains("Upload")), "no Box/upload/3D in Menu");
                Check(go.GetComponentsInChildren<Text>().Count(x => x.text == "Tab") == 1, "one visible Tab keycap");
                Check(!go.GetComponentsInChildren<Text>().Any(x => new[] { "P", "I", "Q", "M", "Esc" }.Contains(x.text)), "other keycaps hidden without changing bindings");
                foreach (var button in go.GetComponentsInChildren<Button>()) button.onClick.Invoke();
                Check(string.Join(",", actions) == "EditName,Pets,Inventory,Profile,Quest,Map,Codex,Settings,Back", "all nine buttons dispatch their own route once");
                foreach (string id in new[] { "Pets", "Inventory", "Profile", "Quest", "Map", "Codex" }) {
                    var card = go.GetComponentsInChildren<Button>().Single(x => x.name == id + "Button");
                    Check(card.GetComponent<Image>().sprite.name == "surface", "borderless surface below the sole illustrated frame " + id);
                    Check(card.transform.Find("IllustratedFrame") != null && card.GetComponentsInChildren<Image>().Count(x => x.name == "IllustratedFrame") == 1, "one main frame " + id);
                    if (id != "Pets" && id != "Inventory") {
                        Check(((RectTransform)card.transform).rect.height >= 105, "V4 secondary height >=105 " + id);
                        var art = card.transform.Find("Vignette").GetComponent<TrainerMenuVignette>();
                        Check(art.color.a >= .9f && art.sprite.name.StartsWith("TrainerMenuCrop_"), "readable cropped vignette " + id);
                    }
                }
                foreach (var graphic in go.GetComponentsInChildren<Graphic>())
                    Check(graphic.GetComponent<Button>() != null || !graphic.raycastTarget, "art/text never intercepts input: " + graphic.name);
                var camera = New(scene, "MenuFixtureCamera", typeof(Camera)).GetComponent<Camera>();
                camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene); canvas.worldCamera = camera;
                foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) }) {
                    camera.aspect = (float)size.x / size.y; camera.orthographicSize = 450;
                    rect.sizeDelta = new Vector2(900 * camera.aspect, 900); view.Fit(rect.rect.size);
                    foreach (bool longName in new[] { false, true }) {
                        view.Bind(longName ? LongSnapshot() : default, longName, longName ? (int?)3 : null);
                        Canvas.ForceUpdateCanvases();
                        foreach (var label in go.GetComponentsInChildren<Text>()) {
                            Check(label.preferredHeight <= label.rectTransform.rect.height + 1, size + " text height fits " + label.name);
                            Check(label.preferredWidth <= label.rectTransform.rect.width + 2, size + " text width fits " + label.name);
                            Check(label.color.a > .99f, "opaque foreground text " + label.name);
                        }
                        var corners = new Vector3[4];
                        foreach (var button in go.GetComponentsInChildren<Button>()) {
                            var buttonRect = (RectTransform)button.transform; buttonRect.GetWorldCorners(corners);
                            Check(corners.All(x => rect.rect.Contains(rect.InverseTransformPoint(x))), size + " button inside viewport " + button.name);
                            Check(buttonRect.rect.height * view.NavigationRoot.lossyScale.y * size.y / 900 >= 40, size + " hit target >=40px " + button.name);
                        }
                        Check(go.GetComponentsInChildren<Text>().First(x => x.name == "Level").text == "Lv. —", "missing level uses em dash");
                        Check(go.GetComponentsInChildren<Text>().First(x => x.name == "ExperienceValue").text == "— / —", "missing EXP uses em dash");
                        Render(camera, size, "layout-" + size.x + "x" + size.y + (longName ? "-long" : "-empty"));
                        if (size.x == 1920 && !longName) RenderStates(go, camera, size);
                    }
                }
            } catch (Exception e) { results.Add("ERROR " + e); Debug.LogException(e); }
            finally { EditorSceneManager.ClosePreviewScene(scene); Finish("layout-validation"); }
        }
        static void RenderStates(GameObject root, Camera camera, Vector2Int size) {
            var pet = root.GetComponentsInChildren<GuiKitButton>().Single(x => x.name == "PetsButton");
            var stateType = typeof(Selectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
            var transition = typeof(GuiKitButton).GetMethod("DoStateTransition", Private);
            foreach (string state in new[] { "Normal", "Highlighted", "Pressed", "Selected", "Disabled" }) {
                pet.interactable = state != "Disabled";
                if (state == "Selected") pet.OnSelect(new BaseEventData(null)); else pet.OnDeselect(new BaseEventData(null));
                transition.Invoke(pet, new[] { Enum.Parse(stateType,state), (object)true });
                Canvas.ForceUpdateCanvases();
                Check(pet.transform.Find("Focus").GetComponent<Image>().enabled == (state == "Selected"), "thin focus belongs to selected state " + state);
                Check(pet.transform.Find("Label").GetComponent<Text>().color.a > .99f, "foreground remains readable " + state);
                Render(camera,size,"state-" + state.ToLowerInvariant());
            }
            pet.interactable = true; pet.OnDeselect(new BaseEventData(null));
            transition.Invoke(pet,new[] { Enum.Parse(stateType,"Normal"), (object)true });
        }
        static PlayerProfileSnapshot LongSnapshot() => new PlayerProfileSnapshot(
            "Người huấn luyện với tên rất dài để kiểm tra bố cục không tràn khỏi khung thông tin", "", 0, 0, 0, null,
            3600000, "", "Khu vực có tên dài vượt quá chiều rộng cho phép của thanh thông tin", 0, 0, 0, 0, 0, 0, 0, 0, null, 0);
        static GameObject New(Scene scene, string name, params Type[] components) {
            var go = new GameObject(name, components); SceneManager.MoveGameObjectToScene(go, scene); return go;
        }
        static void Render(Camera camera, Vector2Int size, string name) {
            var rt = new RenderTexture(size.x, size.y, 24); rt.Create(); camera.targetTexture = rt;
            var previous = RenderTexture.active; var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            try {
                camera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG());
            } finally { RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); }
        }
        [MenuItem("Game Tools/GameToolThang/HUD/Validate Trainer Menu V4 in Play Mode")]
        public static async void RunPlay() {
            if (running || !Application.isPlaying) return;
            running = true; results.Clear(); Directory.CreateDirectory(Output);
            var menu = Object.FindFirstObjectByType<GameMenuController>();
            var inventory = Object.FindFirstObjectByType<MonsterInventoryController>();
            var profile = Object.FindFirstObjectByType<ProfilePanelController>();
            var map = Object.FindFirstObjectByType<MapInputController>();
            var hud = Object.FindFirstObjectByType<GameplayHudController>();
            var maps = Object.FindFirstObjectByType<MapSystemController>();
            var hudGroup = hud != null && hud.PresentationRoot != null ? hud.PresentationRoot.GetComponent<CanvasGroup>() : null;
            bool addedGroup = hudGroup == null && hud != null && hud.PresentationRoot != null;
            if (addedGroup) hudGroup = hud.PresentationRoot.gameObject.AddComponent<CanvasGroup>();
            float priorAlpha = hudGroup != null ? hudGroup.alpha : 1;
            var worldLabel = new GameObject("MenuVisibilityFixture",typeof(TextMesh));
            var labelRenderer = worldLabel.GetComponent<Renderer>();
            try {
                await Task.Delay(1500);
                Check(menu != null && inventory != null && profile != null && map != null, "real scene UI owners exist");
                inventory.Close(); profile.Close(); map.CloseMap(); menu.CloseMenu(); await Task.Delay(250);
                bool cursorVisible = Cursor.visible; CursorLockMode cursorLock = Cursor.lockState;
                if (hudGroup != null) hudGroup.alpha = .63f;
                menu.HandleShortcut(KeyCode.Tab); await Task.Delay(200);
                Check(menu.IsMenuOpen && InventoryInputController.GameplayInputBlocked && menu.AllowsCameraTracking, "Tab opens Menu, blocks actions, keeps passive camera tracking");
                Check(Cursor.visible && Cursor.lockState == CursorLockMode.None, "Menu frees the cursor");
                var root = (RectTransform)typeof(GameMenuController).GetField("root", Private).GetValue(menu);
                Check(root.Find("TrainerMenuDesign") != null, "live scene uses V4 view");
                Check(root.GetComponentsInChildren<Button>().Count(x => x.name != "CloseButton" && x.transform.IsChildOf(root.Find("TrainerMenuDesign"))) == 9, "live Menu has eight navigation actions and profile edit");
                Canvas.ForceUpdateCanvases();
                foreach (var target in root.Find("TrainerMenuDesign").GetComponentsInChildren<Button>()) {
                    var rect=(RectTransform)target.transform; var canvas=rect.GetComponentInParent<Canvas>();
                    var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                    var position=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(rect.rect.center));
                    var hits=new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position=position },hits);
                    Check(hits.Count>0 && hits[0].gameObject.transform.IsChildOf(target.transform),"real EventSystem top raycast hits "+target.name+" at "+Screen.width+"x"+Screen.height);
                }
                Check(hudGroup != null && hudGroup.alpha == 0 && !hudGroup.blocksRaycasts, "gameplay HUD visually hidden without disabling its components");
                Check(labelRenderer.forceRenderingOff, "world label renderers suppressed");
                Check(maps.Minimap.Camera == null || !maps.Minimap.Camera.gameObject.activeSelf, "minimap camera hidden by existing visibility system");
                ScreenCapture.CaptureScreenshot(Output + "live-menu.png"); await Task.Delay(250);
                menu.HandleShortcut(KeyCode.Tab); await Task.Delay(200);
                Check(!menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked, "second Tab restores gameplay");
                Check(Cursor.visible == cursorVisible && Cursor.lockState == cursorLock, "Menu restores prior cursor visibility/lock");
                Check(Mathf.Approximately(hudGroup.alpha,.63f) && !labelRenderer.forceRenderingOff, "original HUD alpha and world label rendering restored");
                hudGroup.alpha = priorAlpha;
                foreach (string id in new[] { "Pets", "Inventory", "Profile", "Quest", "Map", "Codex", "Settings" }) {
                    menu.OpenMenu(); await Task.Delay(200);
                    var button = root.GetComponentsInChildren<Button>().Single(x => x.name == id + "Button");
                    EventSystem.current?.SetSelectedGameObject(button.gameObject);
                    if (id == "Inventory") ExecuteEvents.Execute(button.gameObject,new BaseEventData(EventSystem.current),ExecuteEvents.submitHandler);
                    else ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },ExecuteEvents.pointerClickHandler);
                    await Task.Delay(350);
                    bool opened = id == "Pets" ? inventory.IsPetsSectionOpen : id == "Inventory" ? inventory.IsBagSectionOpen
                        : id == "Profile" ? profile.IsOpen : id == "Quest" ? inventory.IsQuestSectionOpen
                        : id == "Map" ? map.IsOpen : id == "Codex" ? inventory.IsCodexOpen : inventory.IsOpen;
                    Check(opened && !menu.IsMenuOpen && InventoryInputController.GameplayInputBlocked, "button opens real destination " + id);
                    menu.HandleShortcut(KeyCode.Escape); await Task.Delay(300);
                    Check(menu.IsMenuOpen, "Esc returns to originating Menu " + id);
                    Check(EventSystem.current == null || EventSystem.current.currentSelectedGameObject == button.gameObject, "focus restored " + id);
                    menu.CloseMenu(); await Task.Delay(200);
                }
                menu.OpenMenu(); await Task.Delay(200);
                var edit = root.GetComponentsInChildren<Button>().Single(x => x.name == "EditNameButton");
                EventSystem.current?.SetSelectedGameObject(edit.gameObject); edit.onClick.Invoke(); await Task.Delay(250);
                var nameDialog = (RectTransform)typeof(ProfilePanelController).GetField("nameDialog",Private).GetValue(profile);
                Check(profile.IsOpen && nameDialog.gameObject.activeSelf, "edit pencil opens the existing Profile rename modal");
                menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(profile.IsOpen && !nameDialog.gameObject.activeSelf, "first Esc closes rename modal only");
                menu.HandleShortcut(KeyCode.Escape); await Task.Delay(220);
                Check(menu.IsMenuOpen, "second Esc returns to Menu without editing data");
                menu.CloseMenu(); await Task.Delay(200);
                bool preference = (bool)typeof(MapSystemController).GetField("minimapPreference",Private).GetValue(maps);
                try {
                    maps.SetUserMinimapVisible(false); menu.OpenMenu(); await Task.Delay(200); menu.CloseMenu(); await Task.Delay(200);
                    Check(!maps.Minimap.Camera.gameObject.activeSelf, "Menu close respects an already-hidden minimap preference");
                } finally { maps.SetUserMinimapVisible(preference); }
                foreach (KeyCode key in new[] { KeyCode.P, KeyCode.I, KeyCode.Q, KeyCode.M }) {
                    menu.HandleShortcut(key); await Task.Delay(220);
                    Check(!menu.IsMenuOpen && InventoryInputController.GameplayInputBlocked, "existing shortcut opens destination " + key);
                    menu.HandleShortcut(key); await Task.Delay(220);
                    Check(!menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked, "same shortcut closes to gameplay " + key);
                }
                for (int i = 0; i < 3; i++) {
                    menu.OpenMenu(); await Task.Delay(180);
                    root.GetComponentsInChildren<Button>().Single(x => x.name == "BackButton" && x.transform.IsChildOf(root.Find("TrainerMenuDesign"))).onClick.Invoke();
                    await Task.Delay(180); Check(!menu.IsMenuOpen && !InventoryInputController.GameplayInputBlocked, "Back cycle releases lock " + i);
                }
            } catch (Exception e) { results.Add("ERROR " + e); Debug.LogException(e); }
            finally {
                inventory?.Close(); profile?.Close(); map?.CloseMap(); menu?.CloseMenu(); Finish("play-validation");
                if (hudGroup != null) hudGroup.alpha = priorAlpha;
                Object.Destroy(worldLabel);
                if (SessionState.GetBool(StartedPlay, false)) { SessionState.SetBool(StartedPlay, false); EditorApplication.ExitPlaymode(); }
            }
        }
    }
}
