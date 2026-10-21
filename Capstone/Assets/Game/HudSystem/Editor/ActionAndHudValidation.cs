using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UITK = UnityEngine.UIElements;

namespace Capstone.Game.HudSystem.Editor {
    public static class ActionAndHudValidation {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly List<string> results = new List<string>();
        const string Output = "Temp/AntiSpamHud";

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Input And HUD Layout")]
        public static void Validate() {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before validating.");
            results.Clear(); Directory.CreateDirectory(Output);
            Scene scene = EditorSceneManager.NewPreviewScene();
            try {
                ValidateTiming();
                ValidateUiAndDoubleClick(scene);
                ValidateHud(scene);
                File.WriteAllLines(Output + "/validation.txt", results);
                Debug.Log($"Input/HUD validation: {results.Count} PASS, 0 FAIL. No save data changed. Previews: {Output}");
            } catch (Exception error) {
                results.Add("FAIL " + error); File.WriteAllLines(Output + "/validation.txt", results); throw;
            } finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        static void ValidateTiming() {
            double now = 100;
            var gate = new ActionRepeatGate(() => now);
            Check(gate.TryAccept("summon:1"), "first command runs immediately");
            Check(!gate.TryAccept("summon:1"), "mouse/key repeat of same command is rejected");
            now += .149;
            Check(!gate.TryAccept("summon:1"), "repeat before 150ms is rejected");
            Check(gate.TryAccept("summon:2"), "a different queued pet request is independent");
            now = 100.15;
            Check(gate.TryAccept("summon:1"), "command is accepted at 150ms without extending the deadline on rejection");
            Check(gate.TryBegin("heal", out var operation), "resource operation starts once");
            now += 1;
            Check(!gate.TryBegin("heal", out _), "busy resource operation stays locked beyond 150ms");
            operation.Dispose(); operation.Dispose();
            Check(gate.TryBegin("heal", out operation), "completed operation releases its busy lock");
            operation.Dispose();
            int invoked = 0;
            Action action = gate.Guard("button", () => invoked++);
            action(); action();
            Check(invoked == 1, "button wrapper discards duplicates without deferring them");
            now += .15; action();
            Check(invoked == 2, "button responds again after the short interval");
            Action failing = gate.Guard("failure", () => throw new InvalidOperationException());
            try { failing(); } catch (InvalidOperationException) { }
            now += 1;
            Check(gate.TryAccept("failure"), "exception releases the running guard");
        }

        static void ValidateUiAndDoubleClick(Scene scene) {
            var obj = NewObject(scene, "UI validation"); obj.SetActive(false);
            var provider = obj.AddComponent<PlaceholderPetHudProvider>();
            var party = obj.AddComponent<PetPartyPanelController>();
            Set(party, "petHudProvider", provider); Set(party, "autoFindProvider", false);
            var cards = (List<UITK.Button>)Get(party, "partyCards");
            for (int i = 0; i < 6; i++) cards.Add(new UITK.Button());
            Invoke(party, "HandlePartyCardClick", 0, 1);
            Invoke(party, "HandlePartyCardClick", 0, 2);
            Check((int)Get(party, "pendingSwapSourceIndex") == 0, "PETS double-click still selects the swap source");
            var button = new UITK.Button { name = "test-button" };
            int requests = 0;
            Invoke(party, "RegisterButton", button, (Action)(() => requests++));
            var callbacks = (Dictionary<UITK.Button, Action>)Get(party, "buttonCallbacks");
            callbacks[button](); callbacks[button]();
            Check(requests == 1, "actual PETS button registration rejects repeated callbacks");
            UnityEngine.Object.DestroyImmediate(obj);
        }

        static void ValidateHud(Scene scene) {
            var source = Resources.FindObjectsOfTypeAll<RectTransform>()
                .First(rect => rect.name == "GameplayHUDRoot" && rect.gameObject.scene == SceneManager.GetActiveScene());
            Check(Mathf.Approximately(source.Find("ActivePetInfo").localScale.x, .65f)
                && Mathf.Approximately(((RectTransform)source.Find("PartyCarousel")).sizeDelta.x, 442f),
                "open TestChar scene has the updated saved HUD layout");
            Color originalPaper = new Color(.97f, .98f, .95f, .94f);
            Check(source.Find("ActivePetInfo").GetComponent<HudFrameGraphic>().color == originalPaper,
                "saved status frame keeps its original color and opacity");
            var obj = NewObject(scene, "HUD validation"); obj.SetActive(false);
            var canvasObj = NewObject(scene, "Preview canvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasObj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var canvasRect = (RectTransform)canvas.transform;
            var rootObj = NewObject(scene, "GameplayHUDRoot", typeof(RectTransform));
            var root = (RectTransform)rootObj.transform; root.SetParent(canvasRect, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one; root.sizeDelta = Vector2.zero;
            var hud = obj.AddComponent<GameplayHudController>();
            Set(hud, "root", root);
            var visualType = typeof(GameplayHudController).Assembly.GetType("Capstone.Game.HudSystem.GameplayHudVisuals");
            var fontField = visualType.GetField("Font"); var oldFont = fontField.GetValue(null);
            fontField.SetValue(null, AssetDatabase.LoadAssetAtPath<Font>("Assets/ThangTest/UIThang/GDS/Common/Fonts/Fontin-SmallCaps.ttf"));
            try {
                Invoke(hud, "BuildPetStatus"); Invoke(hud, "BuildCarousel"); Invoke(hud, "BuildSkills");
                var quick = UnityEngine.Object.Instantiate(source.Find("QuickItems").gameObject, root, false);
                quick.name = "QuickItems";
                var provider = obj.AddComponent<PlaceholderPetHudProvider>();
                Sprite portrait = source.Find("ActivePetInfo/PortraitFrame/PetPortrait").GetComponent<Image>().sprite;
                Set(provider, "petSlots", Enumerable.Range(0, 6).Select(i => new PetSlotHudData {
                    occupied = i < 3, selected = i == 1, summoned = i == 1, icon = i < 3 ? portrait : null,
                    displayName = "Dragon Darkness", level = 2
                }).ToList());
                Set(provider, "selectedSlotIndex", 1); Set(hud, "petHudProvider", provider);
                Invoke(hud, "RefreshHud"); root.Find("SkillBar").gameObject.SetActive(true);
                foreach (Button button in root.Find("SkillBar").GetComponentsInChildren<Button>()) button.interactable = true;
                Invoke(hud.Carousel, "Layout");
                InvokeStatic(typeof(GameplayHudValidation), "ValidateCarouselPresentation", hud.Carousel);
                Check(true, "five visible cards preserve overlap, draw order, centering and symmetric fade");
                Check(Mathf.Approximately(root.Find("ActivePetInfo").localScale.x, .65f), "pet status scale is 65%");
                Check(Mathf.Approximately(root.Find("SkillBar").localScale.x, .5f)
                    && Mathf.Approximately(root.Find("QuickItems").localScale.x, .5f), "skills and quick items remain half scale");
                var frame = root.Find("ActivePetInfo").GetComponent<HudFrameGraphic>();
                Check(frame.color == originalPaper, "outer status decoration keeps its original color and opacity");
                Check(Mathf.Approximately(root.Find("ActivePetInfo/PetName").GetComponent<Text>().color.a, 1f)
                    && Mathf.Approximately(root.Find("ActivePetInfo/HPBar/Fill").GetComponent<Image>().color.a, 1f), "text and HP content remain opaque");
                Check(Mathf.Approximately(((RectTransform)source.Find("MiniMap")).rect.width, 350f), "saved minimap size stays unchanged");
                var menuRoot = NewObject(scene, "Menu validation").AddComponent<RectTransform>();
                menuRoot.SetParent(canvasRect, false); GuiKitControls.Stretch(menuRoot);
                var menu = new TrainerMenuView(menuRoot, null, _ => { });
                var cameraObj = NewObject(scene, "Preview camera", typeof(Camera));
                var camera = cameraObj.GetComponent<Camera>(); camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.16f, .22f, .2f);
                camera.nearClipPlane = .1f; camera.farClipPlane = 2000;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.transform.position = new Vector3(0, 0, -1000);
                canvas.worldCamera = camera;
                foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1366, 768) }) {
                    float scale = Mathf.Sqrt((size.x / 1920f) * (size.y / 1080f));
                    canvasRect.sizeDelta = new Vector2(size.x / scale, size.y / scale); canvasRect.localScale = Vector3.one * scale;
                    Canvas.ForceUpdateCanvases(); menu.Fit(canvasRect.rect.size);
                    Rect status = Bounds(root.Find("ActivePetInfo") as RectTransform, root);
                    Rect row = Bounds(root.Find("PartyCarousel") as RectTransform, root);
                    Check(Mathf.Abs(status.width - 442) < .01f && Mathf.Abs(status.xMin - row.xMin) < .01f
                        && Mathf.Abs(status.xMax - row.xMax) < .01f, size + ": pet row matches both status edges");
                    Check(menu.NavigationRoot.anchoredPosition.x >= TrainerMenuView.DesignWidth * .5f, size + ": Menu v3 leaves the left world visible");
                    camera.orthographicSize = size.y * .5f; camera.aspect = (float)size.x / size.y;
                    var rt = new RenderTexture(size.x, size.y, 24); rt.Create(); camera.targetTexture = rt;
                    var previous = RenderTexture.active; var texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try {
                        camera.Render(); RenderTexture.active = rt;
                        texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); texture.Apply();
                        File.WriteAllBytes(Output + $"/hud-{size.x}x{size.y}.png", texture.EncodeToPNG());
                    } finally {
                        RenderTexture.active = previous; camera.targetTexture = null;
                        UnityEngine.Object.DestroyImmediate(texture); rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
                    }
                }
            } finally { fontField.SetValue(null, oldFont); }
        }

        static Rect Bounds(RectTransform rect, RectTransform root) {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            Vector3 a = root.InverseTransformPoint(corners[0]), b = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(a.x, a.y, b.x, b.y);
        }
        static GameObject NewObject(Scene scene, string name, params Type[] components) {
            var obj = new GameObject(name, components); SceneManager.MoveGameObjectToScene(obj, scene); return obj;
        }
        static void Check(bool condition, string message) {
            if (!condition) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
        static object Get(object obj, string name) => obj.GetType().GetField(name, Private).GetValue(obj);
        static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
        static object Invoke(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, Private).Invoke(obj, args);
        static void InvokeStatic(Type type, string name, params object[] args) => type.GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
    }
}
