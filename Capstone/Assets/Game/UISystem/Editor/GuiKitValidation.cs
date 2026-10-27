using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using Text = UnityEngine.UI.Text;
using Image = UnityEngine.UI.Image;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Isolated art fixture. No gameplay provider, active scene or save changes.</summary>
    public static class GuiKitValidation {
        const string Root = "Assets/ThangTest/UIThang/GUIArt/";
        const string Output = Capstone.Game.UISystem.QaArtifactPaths.Root + "gui-production/stages-1-2/";
        static readonly List<string> results = new List<string>();
        static bool running;
        static double nextPoll;
        static string editorState;
        const string RequestPath = "Temp/RunGuiKit.request";
        [InitializeOnLoadMethod] static void Initialize() {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }
        static void Poll() {
            if (running || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .5;
            if (!File.Exists(RequestPath) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            Directory.CreateDirectory(Output);
            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                WriteEditorState("Waiting for Edit Mode; the user's Play session is preserved.");
                return;
            }
            File.Delete(RequestPath);
            WriteEditorState("Running isolated GUI validation in the open Editor.");
            Run();
        }
        static void WriteEditorState(string value) {
            if (editorState == value) return;
            editorState = value;
            File.WriteAllText(Output + "editor-state.txt", value);
        }
        [Serializable] sealed class Manifest { public AssetEntry[] assets; }
        [Serializable] sealed class AssetEntry { public string id; public string runtime; public string kind; public int width; public int height; public int[] slice; }
        static void Check(bool valid, string label) {
            results.Add((valid ? "PASS " : "FAIL ") + label);
            if (!valid) throw new InvalidOperationException(label);
        }
        [MenuItem("Game Tools/GameToolThang/GUI/Validate Shared GUI Kit")]
        public static void Run() {
            if (running) return;
            running = true;
            results.Clear(); Directory.CreateDirectory(Output);
            try {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "Documentation/asset-manifest.json"));
                Check(manifest.assets.Length == 52, "52 runtime assets available, including shared background");
                foreach (var asset in manifest.assets) {
                    AssetDatabase.ImportAsset(asset.runtime, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(asset.runtime);
                    Check(sprite != null && sprite.rect.width == asset.width && sprite.rect.height == asset.height, "sprite imports with expected size: " + asset.id);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(asset.runtime);
                    Check(!importer.mipmapEnabled && !importer.isReadable && importer.alphaIsTransparency && importer.textureCompression == TextureImporterCompression.Uncompressed, "UI import preset: " + asset.id);
                    Check(sprite.border == new Vector4(asset.slice[0], asset.slice[1], asset.slice[2], asset.slice[3]), "slice border: " + asset.id);
                }
                var font = MenuTypography.Font;
                font.RequestCharactersInTexture("ĐđĂăÂâÊêÔôƠơƯư ÁÀẢÃẠ ẮẰẲẴẶ ẤẦẨẪẬ ÉÈẺẼẸ ẾỀỂỄỆ ÍÌỈĨỊ ÓÒỎÕỌ ỐỒỔỖỘ ỚỜỞỠỢ ÚÙỦŨỤ ỨỪỬỮỰ ÝỲỶỸỴ", 17);
                foreach (char c in "ĐđĂăÂâÊêÔôƠơƯưÁÀẢÃẠẮẰẲẴẶẤẦẨẪẬÉÈẺẼẸẾỀỂỄỆÍÌỈĨỊÓÒỎÕỌỐỒỔỖỘỚỜỞỠỢÚÙỦŨỤỨỪỬỮỰÝỲỶỸỴ")
                    Check(font.HasCharacter(c), "Vietnamese glyph " + c);
                foreach (string name in new[] { "GuiKitGallery", "GuiSearchField", "GuiConfirmation", "GuiScreenHeader", "GuiSlot" })
                    Check(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "UI/" + name + ".uxml") != null, "UXML imports: " + name);
                Check(AssetDatabase.LoadAssetAtPath<StyleSheet>(Root + "UI/GuiKit.uss") != null, "shared USS imports");
                ValidateToolkit();
                GuiKitSetup.Rebuild();
                foreach (var name in new[] { "PrimaryButton", "SecondaryButton", "DangerButton", "BackButton", "IconButton", "ContentFrame", "ItemSlot", "SelectedSlot", "AvatarRing", "ProgressTrack", "ConfirmationView" }) {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GuiKitSetup.PrefabFolder + name + ".prefab");
                    Check(prefab != null, "reusable prefab imports: " + name);
                    foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
                        Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "prefab has no missing scripts: " + name + "/" + child.name);
                }
                RenderUgui();
                Debug.Log("GUI kit: " + results.Count + " checks passed. " + Output);
            } catch (Exception e) { results.Add("ERROR " + e); Debug.LogException(e); }
            finally {
                File.WriteAllLines(Output + "unity-validation.txt", results);
                running = false;
                WriteEditorState(results.Exists(x => x.StartsWith("FAIL ") || x.StartsWith("ERROR "))
                    ? "GUI validation failed; see unity-validation.txt."
                    : "GUI validation complete: " + results.Count + " checks passed.");
            }
        }
        public static void RunBatch() {
            Run();
            if (results.Exists(x => x.StartsWith("FAIL ") || x.StartsWith("ERROR ")))
                throw new InvalidOperationException("GUI validation failed. See " + Output + "unity-validation.txt");
        }
        static void ValidateToolkit() {
            var scene = EditorSceneManager.NewPreviewScene();
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            var utility = typeof(UIDocument).Assembly.GetType("UnityEngine.UIElements.UIElementsRuntimeUtility");
            Check(utility != null, "Unity 6.3 offscreen validation renderer available");
            void Paint() {
                // Editor fixture only. The runtime kit has no dependency on internal APIs.
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                utility.GetMethod("UpdatePanels", flags).Invoke(null, null);
                utility.GetMethod("RepaintPanels", flags).Invoke(null, new object[] { false });
                utility.GetMethod("RenderOffscreenPanels", flags).Invoke(null, null);
            }
            try {
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/ThangTest/UIThang/GDS/Core.UITK/Styles/UnityDefaultRuntimeTheme.tss");
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            var owner = New(scene, "GuiToolkitFixture", typeof(UIDocument));
            var document = owner.GetComponent<UIDocument>();
            document.panelSettings = settings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(GuiKitGalleryWindow.GalleryPath);
            var root = document.rootVisualElement;
            GuiKitGalleryWindow.Populate(root);
            Check(root.panel != null, "UI Toolkit fixture attached to a real panel");
            Check(!root.Q<UnityEngine.UIElements.Button>("gallery-disabled").enabledSelf, "disabled action remains locked");
            Check(root.Q("gallery-icons").childCount == GuiKitGalleryWindow.IconIds.Length, "icon gallery has all semantic icons");
            var search = root.Q<TextField>("gallery-search"); search.value = "Thú đồng hành có tên rất dài";
            Check(root.Q<Label>("gallery-status").text.Contains(search.value), "Vietnamese search field change reaches its view");
            var clear = root.Q<UnityEngine.UIElements.Button>("gallery-clear");
            clear.Focus();
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = clear; clear.SendEvent(submit); }
            Check(search.value == string.Empty, "native submit activates clear-search button");
            Check(root.panel.focusController.focusedElement == search, "clear-search returns focus to text input");
            search.value = "Thú đồng hành có tên rất dài";
            Check(root.Q<DropdownField>("gallery-sort").choices.Count == 3, "dropdown contains localized options");
            foreach (var icon in root.Query(className: "gui-icon").ToList()) Check(icon.pickingMode == PickingMode.Ignore, "decoration does not intercept input");
            foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1366,768), new Vector2Int(1920,1200), new Vector2Int(2560,1080) }) {
                var rt = new RenderTexture(size.x, size.y, 24); rt.Create();
                var prior = RenderTexture.active;
                var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                try {
                    settings.targetTexture = rt;
                    root.style.width = size.x; root.style.height = size.y;
                    RenderTexture.active = rt; GL.Clear(true, true, GuiKitPalette.Background); RenderTexture.active = prior;
                    for (int tick = 0; tick < 4; tick++) Paint();
                    var back = root.Q<UnityEngine.UIElements.Button>("gallery-back");
                    Check(back.worldBound.xMin >= 0 && back.worldBound.xMax <= size.x && back.worldBound.yMax <= size.y, size + " Toolkit Back remains on screen");
                    foreach (var surface in root.Query(className: "gui-surface").ToList())
                        Check(surface.resolvedStyle.width > 100 && surface.worldBound.xMax <= size.x, size + " Toolkit surface fits viewport");
                    Check(root.Q("gallery-status").worldBound.yMax <= size.y + 1, size + " Toolkit footer remains on screen");
                    var button = root.Q<UnityEngine.UIElements.Button>("gallery-primary"); button.Focus(); Paint();
                    Check(root.panel.focusController.focusedElement == button, size + " Toolkit native keyboard focus");
                    RenderTexture.active = rt;
                    texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                    File.WriteAllBytes(Output + "toolkit-" + size.x + "x" + size.y + ".png", texture.EncodeToPNG());
                } finally {
                    settings.targetTexture = null; RenderTexture.active = prior;
                    Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt);
                }
            }
            } finally { EditorSceneManager.ClosePreviewScene(scene); Object.DestroyImmediate(settings); }
        }
        static void RenderUgui() {
            var scene = EditorSceneManager.NewPreviewScene();
            try {
                var obj = New(scene, "GuiKitCanvas", typeof(RectTransform), typeof(Canvas));
                var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
                // Rasterize fixture text above final pixel density to avoid world-space atlas blur.
                canvas.scaleFactor = 2;
                var canvasRect = (RectTransform)obj.transform; canvasRect.sizeDelta = new Vector2(1280, 720);
                var background = GuiKitControls.Background(obj.transform);
                var design = GuiKitControls.Frame(obj.transform, "Design");
                design.sprite = null; design.color = Color.clear;
                design.rectTransform.sizeDelta = new Vector2(1280, 720);
                var header = GuiKitControls.Frame(design.transform, "HeaderPlate");
                header.sprite = null; var headerColor = GuiKitPalette.Background; headerColor.a = .88f; header.color = headerColor;
                Place(header.rectTransform, 24, 14, 1232, 68);
                var heading = GuiKitControls.Label(design.transform, "Heading", "BỘ GUI DÙNG CHUNG • uGUI", 32);
                heading.fontStyle = FontStyle.Bold; Place(heading.rectTransform, 32, 20, 850, 52);
                var back = GuiKitControls.Button(design.transform, "Back", "Trở lại", iconId: "back");
                Place(back.GetComponent<RectTransform>(), 1040, 24, 208, 52);
                var body = GuiKitControls.Frame(design.transform, "Body"); Place(body.rectTransform, 24, 96, 1232, 594);
                var caption = GuiKitControls.Label(body.transform, "Caption", "Khung trắng • xanh lá • viền vàng mảnh • chữ và icon tách riêng", 18);
                Place(caption.rectTransform, 24, 18, 1100, 40);
                int i = 0;
                foreach (var kind in new[] { GuiButtonKind.Primary, GuiButtonKind.Secondary, GuiButtonKind.Danger }) {
                    var button = GuiKitControls.Button(body.transform, "Button" + kind, kind == GuiButtonKind.Primary ? "Xác nhận" : kind == GuiButtonKind.Danger ? "Thả thú" : "Hủy", kind, kind == GuiButtonKind.Primary ? "check" : kind == GuiButtonKind.Danger ? "trash" : "close");
                    Place(button.GetComponent<RectTransform>(), 24 + i++ * 250, 76, 232, 54);
                }
                var locked = GuiKitControls.Button(body.transform, "Locked", "Chưa đủ điều kiện"); locked.interactable = false;
                Place(locked.GetComponent<RectTransform>(), 774, 76, 270, 54);
                var focused = GuiKitControls.Button(body.transform, "Focused", "Đang focus", iconId: "search");
                Place(focused.GetComponent<RectTransform>(), 24, 146, 232, 54);
                focused.OnSelect(new BaseEventData(null));
                Check(focused.transform.Find("Focus").GetComponent<Image>().enabled, "uGUI keyboard focus is visible");
                focused.OnDeselect(new BaseEventData(null));
                Check(!focused.transform.Find("Focus").GetComponent<Image>().enabled, "uGUI focus clears on deselect");
                focused.OnSelect(new BaseEventData(null));
                Check(locked.transform.Find("Label").GetComponent<Text>().color == GuiKitPalette.Muted, "disabled uGUI label retains readable contrast");
                var longText = GuiKitControls.Label(body.transform, "LongText", "Tên sinh vật rất dài: Rồng Bóng Tối Đang Đồng Hành — kiểm tra chữ tiếng Việt và bố cục nhiều dòng.", 18);
                Place(longText.rectTransform, 280, 142, 800, 64);
                for (i = 0; i < GuiKitGalleryWindow.IconIds.Length; i++) {
                    string id = GuiKitGalleryWindow.IconIds[i];
                    var cell = GuiKitControls.Frame(body.transform, "Cell_" + id, i == 0 ? "slot_selected" : "slot");
                    Place(cell.rectTransform, 24 + (i % 13) * 91, 226 + (i / 13) * 108, 84, 100);
                    var icon = GuiKitControls.Icon(cell.transform, "Icon", id); Place(icon.rectTransform, 18, 10, 48, 48);
                    var label = GuiKitControls.Label(cell.transform, "Label", id, 11); label.alignment = TextAnchor.MiddleCenter;
                    Place(label.rectTransform, 5, 60, 74, 28);
                }
                var cameraObj = New(scene, "GuiKitCamera", typeof(Camera));
                var camera = cameraObj.GetComponent<Camera>();
                camera.orthographic = true; camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.transform.position = new Vector3(0, 0, -1000);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = GuiKitPalette.Background;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene); canvas.worldCamera = camera;
                foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(1366,768), new Vector2Int(1920,1200), new Vector2Int(2560,1080) }) {
                    camera.aspect = (float)size.x / size.y;
                    camera.orthographicSize = Mathf.Max(360, 640 / camera.aspect);
                    canvasRect.sizeDelta = new Vector2(camera.orthographicSize * 2 * camera.aspect, camera.orthographicSize * 2);
                    Canvas.ForceUpdateCanvases();
                    Check(!background.raycastTarget && background.rectTransform.rect.width >= canvasRect.rect.width - 1 && background.rectTransform.rect.height >= canvasRect.rect.height - 1, size + " uGUI background covers viewport and ignores clicks");
                    foreach (var label in design.GetComponentsInChildren<Text>())
                        Check(label.preferredHeight <= label.rectTransform.rect.height + 1, size + " text fits: " + label.name);
                    var rt = new RenderTexture(size.x, size.y, 24); rt.Create(); camera.targetTexture = rt;
                    var prior = RenderTexture.active; var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try {
                        camera.Render(); RenderTexture.active = rt;
                        texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                        File.WriteAllBytes(Output + "ugui-" + size.x + "x" + size.y + ".png", texture.EncodeToPNG());
                    } finally { RenderTexture.active = prior; camera.targetTexture = null; Object.DestroyImmediate(texture); rt.Release(); Object.DestroyImmediate(rt); }
                }
                foreach (var image in design.GetComponentsInChildren<Image>())
                    Check(image.GetComponent<GuiKitButton>() != null || !image.raycastTarget, "uGUI decoration ignores raycasts: " + image.name);
            } finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static GameObject New(Scene scene, string name, params Type[] components) {
            var obj = new GameObject(name, components); SceneManager.MoveGameObjectToScene(obj, scene); return obj;
        }
        static void Place(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
