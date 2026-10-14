using System.IO;
using Capstone.Game.MapSystem.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Capstone.Game.MapSystem.Editor {
    public static class WorldMapUISetup {
        const string Folder = "Assets/Game/MapSystem/Generated/UI";
        const string IconFolder = "Assets/Game/MapSystem/Sprites/MapMinimap/";
        [MenuItem("Tools/ToolCuaThang/Game Map/Remake World Map UI")]
        public static void RemakeScene() {
            if (Application.isPlaying) { Debug.LogWarning("Stop Play Mode before saving the new map UI."); return; }
            var map = Object.FindFirstObjectByType<WorldMapController>(FindObjectsInactive.Include);
            if (map == null) { Debug.LogError("No WorldMapController in this scene."); return; }
            Remake(map); EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
            Debug.Log("World Map UI remade: real map, 7 filters, tracked quest title, region and marker selection. Save the scene to keep it.");
        }
        public static void Remake(WorldMapController map) {
            var canvas = map.OverlayRoot != null ? map.OverlayRoot.GetComponentInParent<Canvas>() : null;
            if (canvas != null) Undo.RegisterFullObjectHierarchyUndo(canvas.gameObject, "Remake World Map UI");
            Undo.RecordObject(map, "Bind World Map UI");
            var ui = map.GetComponent<WorldMapUIController>();
            if (ui == null) ui = Undo.AddComponent<WorldMapUIController>(map.gameObject);
            else Undo.RecordObject(ui, "Configure World Map UI");
            ui.Configure(map, map.OverlayRoot, map.MapInteractionRect, EnsureTheme());
            ui.Rebuild(); EditorUtility.SetDirty(ui); EditorUtility.SetDirty(map);
            foreach (var npc in Object.FindObjectsByType<Capstone.Game.UISystem.NpcPanelInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
                if (npc.gameObject.scene != map.gameObject.scene || npc.GetComponent<MapMarker>() != null) continue;
                var npcData = new SerializedObject(npc);
                bool shop = npcData.FindProperty("targetPanel").enumValueIndex == (int)Capstone.Game.UISystem.NpcPanelInteractable.PanelTarget.Shop;
                var group = shop ? MapFilterGroup.Shop : MapFilterGroup.NPC;
                var style = ui.Theme.markers[(int)group];
                var marker = Undo.AddComponent<MapMarker>(npc.gameObject);
                marker.ConfigureRuntime(shop ? MapMarkerType.Shop : MapMarkerType.NPC,
                    GlobalObjectId.GetGlobalObjectIdSlow(npc.gameObject).ToString(), shop ? "Cửa hàng" : "Kho thú",
                    style.icon.texture, style.color, false, true);
                EditorUtility.SetDirty(marker);
            }
        }
        static WorldMapUITheme EnsureTheme() {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            const string path = Folder + "/WorldMapUITheme.asset";
            var theme = AssetDatabase.LoadAssetAtPath<WorldMapUITheme>(path);
            if (theme != null) {
                theme.roundedBorder = Shape("MapRoundedBorder", false, true);
                theme.circleBorder = Shape("MapCircleBorder", true, true);
                EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets(); return theme;
            }
            theme = ScriptableObject.CreateInstance<WorldMapUITheme>();
            theme.roundedPanel = Shape("MapRoundedPanel", false);
            theme.circle = Shape("MapCircle", true);
            theme.roundedBorder = Shape("MapRoundedBorder", false, true);
            theme.circleBorder = Shape("MapCircleBorder", true, true);
            var sourceFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (sourceFont == null) throw new System.InvalidOperationException("World Map UI requires the project's LiberationSans font.");
            var font = TMP_FontAsset.CreateFontAsset(sourceFont,
                90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new System.InvalidOperationException("Could not create the World Map UI font.");
            font.name = "WorldMapUI Vietnamese";
            AssetDatabase.CreateAsset(font, Folder + "/WorldMapUIFont.asset");
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            font.TryAddCharacters("BẢN ĐỒ Xem khu vực và các điểm đánh dấu quan trọng ← TRỞ LẠI Region: X, Z +−⊙ NPC Pet Enemy Boss Quest Shop Travel Xóa Được Thảo Quý Hiếm 0123456789");
            theme.font = font;
            theme.emblem = Icon("Thang_Icon_Resource"); theme.playerIcon = Icon("Thang_Icon_Player"); theme.waypointIcon = Icon("Thang_Icon_Waypoint");
            string[] names = { "NPC", "Pet", "Enemy", "Boss", "Quest", "Shop", "FastTravel" };
            Color[] colors = { new Color(.24f,.55f,.37f), new Color(.50f,.34f,.22f), new Color(.78f,.14f,.20f), new Color(.65f,.05f,.15f),
                new Color(.95f,.62f,.10f), new Color(.61f,.27f,.82f), new Color(.12f,.58f,.94f) };
            for (int i = 0; i < 7; i++) theme.markers[i] = new WorldMapUITheme.MarkerStyle { icon = Icon("Thang_Icon_" + names[i]), color = colors[i] };
            AssetDatabase.CreateAsset(theme, path); EditorUtility.SetDirty(font); EditorUtility.SetDirty(theme); AssetDatabase.SaveAssets();
            return theme;
        }
        static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + name + ".png");
        static Sprite Shape(string name, bool circle, bool border = false) {
            string path = Folder + "/" + name + ".png";
            if (!File.Exists(path)) {
                const int size = 128; var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                    var p = new Vector2(x + .5f, y + .5f);
                    float distance;
                    if (circle) distance = Vector2.Distance(p, new Vector2(64, 64)) - 63;
                    else { var q = new Vector2(Mathf.Abs(p.x - 64) - 49, Mathf.Abs(p.y - 64) - 49); distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - 14; }
                    float alpha = border ? Mathf.Clamp01(.5f - distance) * Mathf.Clamp01(distance + 2.5f) : Mathf.Clamp01(.5f - distance);
                    texture.SetPixel(x, y, new Color(1, 1, 1, alpha));
                }
                texture.Apply(); File.WriteAllBytes(path, texture.EncodeToPNG()); Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.spriteBorder = circle ? Vector4.zero : new Vector4(16, 16, 16, 16); importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
