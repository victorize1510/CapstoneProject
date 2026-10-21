using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Capstone.Game.UISystem.Editor {
    /// <summary>Explicit setup, safe to repeat: updates kit prefabs while retaining their GUIDs.</summary>
    public static class GuiKitSetup {
        public const string PrefabFolder = "Assets/ThangTest/UIThang/GUIArt/Prefabs/";
        [MenuItem("Game Tools/GameToolThang/GUI/Rebuild Shared Kit Prefabs")]
        public static void Rebuild() {
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.NewPreviewScene();
            try {
                var parent = new GameObject("GuiKitPrefabFactory", typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(parent, scene);
                SaveButton(parent.transform, "PrimaryButton", "Xác nhận", GuiButtonKind.Primary, "check", 180, 42);
                SaveButton(parent.transform, "SecondaryButton", "Hủy", GuiButtonKind.Secondary, null, 146, 42);
                SaveButton(parent.transform, "DangerButton", "Xác nhận", GuiButtonKind.Danger, "warning", 180, 42);
                SaveButton(parent.transform, "BackButton", "Trở lại", GuiButtonKind.Secondary, "back", 146, 42);
                SaveButton(parent.transform, "IconButton", "", GuiButtonKind.Secondary, "star", 42, 42);
                SaveFrame(parent.transform, "ContentFrame", "panel", 400, 300);
                SaveFrame(parent.transform, "ItemSlot", "slot", 104, 104);
                SaveFrame(parent.transform, "SelectedSlot", "slot_selected", 104, 104);
                var avatar = SaveFrame(parent.transform, "AvatarRing", "avatar_ring", 128, 128);
                avatar.type = Image.Type.Simple;
                PrefabUtility.SaveAsPrefabAsset(avatar.gameObject, PrefabFolder + "AvatarRing.prefab");
                SaveFrame(parent.transform, "ProgressTrack", "progress_track", 240, 24);
                var popup = GuiKitControls.Frame(parent.transform, "ConfirmationView");
                popup.rectTransform.sizeDelta = new Vector2(480, 228);
                var title = GuiKitControls.Label(popup.transform, "Title", "Xác nhận thao tác", 24);
                title.fontStyle = FontStyle.Bold; Place(title.rectTransform, 24, 20, 432, 40);
                var message = GuiKitControls.Label(popup.transform, "Message", "Nội dung xác nhận được cung cấp bởi màn đang mở.");
                Place(message.rectTransform, 24, 66, 432, 76);
                var cancel = GuiKitControls.Button(popup.transform, "Cancel", "Hủy");
                Place(cancel.GetComponent<RectTransform>(), 112, 164, 146, 42);
                var accept = GuiKitControls.Button(popup.transform, "Accept", "Xác nhận", GuiButtonKind.Primary);
                Place(accept.GetComponent<RectTransform>(), 272, 164, 184, 42);
                PrefabUtility.SaveAsPrefabAsset(popup.gameObject, PrefabFolder + "ConfirmationView.prefab");
                AssetDatabase.SaveAssets();
            } finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        static void SaveButton(Transform parent, string name, string label, GuiButtonKind kind, string icon, int width, int height) {
            var button = GuiKitControls.Button(parent, name, label, kind, icon);
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
            PrefabUtility.SaveAsPrefabAsset(button.gameObject, PrefabFolder + name + ".prefab");
        }
        static Image SaveFrame(Transform parent, string name, string id, int width, int height) {
            var image = GuiKitControls.Frame(parent, name, id);
            image.rectTransform.sizeDelta = new Vector2(width, height);
            PrefabUtility.SaveAsPrefabAsset(image.gameObject, PrefabFolder + name + ".prefab");
            return image;
        }
        static void Place(RectTransform rect, float x, float y, float width, float height) {
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
        }
    }
}
