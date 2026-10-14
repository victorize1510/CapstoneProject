using UnityEditor;
using UnityEngine;

namespace Capstone.Game.UISystem.Editor {
    public static class MenuTypographySetup {
        public const string AssetPath = "Assets/Game/UISystem/Resources/UI/MenuTypography.asset";
        [InitializeOnLoadMethod] static void Initialize() => EditorApplication.delayCall += EnsureAsset;
        [MenuItem("Game Tools/GameToolThang/HUD/Ensure Shared UI Font")]
        public static void EnsureAsset() {
            if (AssetDatabase.LoadAssetAtPath<MenuTypography>(AssetPath) != null) return;
            var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (font == null) { Debug.LogError("Không tìm thấy LiberationSans.ttf để đồng bộ giao diện."); return; }
            const string root = "Assets/Game/UISystem";
            if (!AssetDatabase.IsValidFolder(root + "/Resources")) AssetDatabase.CreateFolder(root, "Resources");
            if (!AssetDatabase.IsValidFolder(root + "/Resources/UI")) AssetDatabase.CreateFolder(root + "/Resources", "UI");
            var settings = ScriptableObject.CreateInstance<MenuTypography>(); settings.font = font;
            AssetDatabase.CreateAsset(settings, AssetPath); AssetDatabase.SaveAssets();
        }
    }
}
