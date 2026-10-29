using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.CodexSystem.Editor {
    [InitializeOnLoad]
    public static class DragonCodexSetup {
        public const string Path = "Assets/Game/CodexSystem/Resources/PetCodex/PetCodexCatalog.asset";
        static DragonCodexSetup() { EditorApplication.delayCall += InstallIfMissing; }
        [MenuItem("Game Tools/GameToolThang/Pets/Create or Select Dragon Codex")]
        public static void CreateOrSelect() {
            InstallIfMissing(); Selection.activeObject = AssetDatabase.LoadAssetAtPath<PetCodexCatalog>(Path);
        }
        static void InstallIfMissing() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<PetCodexCatalog>(Path) != null) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += InstallIfMissing; return; }
            string folder = "Assets/Game/HudSystem/Prefabs/";
            var paths = new[] { "Dragon01Gameplay.prefab", "Dragon03Gameplay.prefab", "Dragon02Gameplay.prefab" };
            var ids = new[] { "dragon-dusk", "dragon-darkness", "dragon-nightfall" };
            var definitions = new List<PetCodexCatalogEntry>();
            for (int i = 0; i < paths.Length; i++) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + paths[i]);
                if (prefab == null || prefab.GetComponentInChildren<PetCollectionMetadata>(true) == null) {
                    Debug.LogError("Dragon Codex: missing authored gameplay prefab " + paths[i]); return;
                }
                definitions.Add(new PetCodexCatalogEntry { id = ids[i], number = i + 1, sourcePrefab = prefab });
            }
            const string parent = "Assets/Game/CodexSystem/Resources";
            if (!AssetDatabase.IsValidFolder(parent + "/PetCodex")) AssetDatabase.CreateFolder(parent, "PetCodex");
            var catalog = ScriptableObject.CreateInstance<PetCodexCatalog>(); catalog.species = definitions;
            var stone = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>("Assets/Game/Inventory/DuskStone.asset");
            if (stone != null) catalog.evolutionItems.Add(stone);
            AssetDatabase.CreateAsset(catalog, Path); AssetDatabase.SaveAssetIfDirty(catalog);
            Debug.Log("Dragon Codex created: 01 Dusk, 02 Darkness, 03 Nightfall. Gameplay form order is unchanged.");
        }
    }
}
