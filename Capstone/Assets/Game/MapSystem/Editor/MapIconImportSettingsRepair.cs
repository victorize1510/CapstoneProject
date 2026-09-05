using UnityEditor;
using UnityEngine;

namespace Capstone.Game.MapSystem.Editor {
    static class MapIconImportSettingsRepair {
        const string IconFolder = "Assets/Game/MapSystem/Sprites/MapMinimap";
        const string AutoRepairSessionKey = "Capstone.Game.MapSystem.MapIconImportSettingsRepair.AutoRan";

        [InitializeOnLoadMethod]
        static void ScheduleAutoRepair() {
            if (SessionState.GetBool(AutoRepairSessionKey, false)) return;

            SessionState.SetBool(AutoRepairSessionKey, true);
            EditorApplication.delayCall += RepairIconImports;
        }

        [MenuItem("Tools/ToolCuaThang/Game Map/Repair Map Icon Imports")]
        public static void RepairIconImports() {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder });
            bool changedAny = false;

            try {
                AssetDatabase.StartAssetEditing();
                foreach (string guid in guids) {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (!System.IO.Path.GetFileNameWithoutExtension(path).StartsWith("Thang_Icon", System.StringComparison.OrdinalIgnoreCase)) continue;

                    TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) continue;

                    bool changed = false;
                    if (importer.textureType != TextureImporterType.Sprite) {
                        importer.textureType = TextureImporterType.Sprite;
                        changed = true;
                    }
                    if (importer.spriteImportMode != SpriteImportMode.Single) {
                        importer.spriteImportMode = SpriteImportMode.Single;
                        changed = true;
                    }
                    if (!importer.alphaIsTransparency) {
                        importer.alphaIsTransparency = true;
                        changed = true;
                    }
                    if (importer.mipmapEnabled) {
                        importer.mipmapEnabled = false;
                        changed = true;
                    }
                    if (importer.wrapMode != TextureWrapMode.Clamp) {
                        importer.wrapMode = TextureWrapMode.Clamp;
                        changed = true;
                    }
                    if (importer.filterMode != FilterMode.Bilinear) {
                        importer.filterMode = FilterMode.Bilinear;
                        changed = true;
                    }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) {
                        importer.textureCompression = TextureImporterCompression.Uncompressed;
                        changed = true;
                    }
                    if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) {
                        importer.spritePixelsPerUnit = 100f;
                        changed = true;
                    }

                    if (!changed) continue;
                    importer.SaveAndReimport();
                    changedAny = true;
                }
            } finally {
                AssetDatabase.StopAssetEditing();
            }

            if (changedAny) AssetDatabase.Refresh();
        }
    }
}
