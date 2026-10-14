using Capstone.Game.Inventory;
using Capstone.Game.SaveSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.HudSystem.Editor {
    // Runs once after script import so the catalog references real Unity prefab components.
    [InitializeOnLoad]
    public static class DragonEvolutionProjectSetup {
        const string ScenePath = "Assets/Scenes/TestChar.unity";
        const string Folder = "Assets/Game/HudSystem/Prefabs";
        const string CatalogPath = Folder + "/DragonEvolutionCatalog.asset";
        static readonly string[] FormIds = {
            "01", "02", "03"
        };
        static readonly string[] FormNames = {
            "Dragon Dusk", "Dragon Nightfall", "Dragon Darkness"
        };

        static DragonEvolutionProjectSetup() {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("Game Tools/GameToolThang/Pets/Install Dragon Dusk evolution assets")]
        public static void InstallIfNeeded() {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating) {
                EditorApplication.delayCall += InstallIfNeeded;
                return;
            }
            // The scene may be open in an unsaved editing session. Never save it behind
            // the user's back or overwrite unrelated scene work during script import.
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded && scene.isDirty) {
                Debug.LogWarning("Dragon evolution setup deferred: save TestChar, then run Game Tools/GameToolThang/Pets/Install Dragon Dusk evolution assets.");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<PetPrefabCatalog>(CatalogPath) != null) return;
            MonsterItemDefinition stoneAsset = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(
                "Assets/Game/Inventory/DuskStone.asset");
            if (stoneAsset == null) {
                Debug.LogError("Dusk Stone asset did not import; Dragon evolution setup stopped.");
                return;
            }
            if (!stoneAsset.Stackable || stoneAsset.MaxStackSize != 99 || stoneAsset.StableId != "dusk-stone") {
                Debug.LogError("Dusk Stone must use stable ID dusk-stone and stack size 99.");
                return;
            }

            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try {
                PetController source = FindBasePet(scene);
                if (source == null) throw new System.InvalidOperationException("Dragon Dusk gameplay pet is missing from TestChar.");
                ConfigureSource(scene, source);
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/HudSystem", "Prefabs");

                string path = Folder + "/Dragon01Gameplay.prefab";
                GameObject clone = Object.Instantiate(source.gameObject);
                clone.name = "Dragon Dusk Gameplay";
                clone.transform.SetParent(null);
                clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                clone.transform.localScale = Vector3.one;
                clone.GetComponent<PetController>().owner = null;
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(clone, path);
                Object.DestroyImmediate(clone);
                PetController gameplayPrefab = prefab != null ? prefab.GetComponent<PetController>() : null;
                if (gameplayPrefab == null)
                    throw new System.InvalidOperationException("Could not create gameplay prefab: " + path);

                PetPrefabCatalog catalog = ScriptableObject.CreateInstance<PetPrefabCatalog>();
                var serialized = new SerializedObject(catalog);
                SerializedProperty entries = serialized.FindProperty("entries");
                entries.arraySize = FormIds.Length;
                for (int i = 0; i < FormIds.Length; i++) {
                    SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("definitionId").stringValue = "Dragon Dusk";
                    entry.FindPropertyRelative("formId").stringValue = FormIds[i];
                    entry.FindPropertyRelative("prefab").objectReferenceValue = gameplayPrefab;
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < FormIds.Length; i++)
                    if (!catalog.TryGetPrefab("Dragon Dusk", FormIds[i], out PetController resolved)
                        || resolved != gameplayPrefab)
                        throw new System.InvalidOperationException("Catalog lookup failed for form " + FormIds[i]);
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                PlayerSaveController save = FindInScene<PlayerSaveController>(scene);
                if (save == null) throw new System.InvalidOperationException("PlayerSaveController is missing from TestChar.");
                var saveObject = new SerializedObject(save);
                saveObject.FindProperty("petPrefabCatalog").objectReferenceValue = catalog;
                saveObject.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new System.IO.IOException("Could not save TestChar scene.");
                AssetDatabase.SaveAssets();
                Debug.Log("Dragon Dusk gameplay prefabs and save catalog installed for forms 01/02/03.");
            }
            catch (System.Exception exception) {
                Debug.LogError("Dragon evolution setup failed: " + exception);
            }
            finally {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        static PetController FindBasePet(Scene scene) {
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (PetController pet in root.GetComponentsInChildren<PetController>(true)) {
                    PetCollectionMetadata metadata = pet.GetComponent<PetCollectionMetadata>();
                    if (metadata != null && metadata.EvolutionStage == 0
                        && metadata.DefinitionId == "Dragon Dusk") return pet;
                }
            return null;
        }

        static void ConfigureSource(Scene scene, PetController source) {
            PetCollectionMetadata metadata = source.GetComponent<PetCollectionMetadata>();
            if (source.GetComponent<PetEvolutionVisualController>() == null)
                source.gameObject.AddComponent<PetEvolutionVisualController>();
            var serialized = new SerializedObject(metadata);
            serialized.FindProperty("definitionId").stringValue = "Dragon Dusk";
            serialized.FindProperty("species").stringValue = "Dragon Dusk";
            serialized.FindProperty("element").intValue = (int)PetElement.Dragon;
            serialized.FindProperty("secondaryElement").intValue = (int)PetElement.Dark;
            serialized.FindProperty("codexNumber").stringValue = "01";
            serialized.FindProperty("currentFormId").stringValue = FormIds[0];
            SerializedProperty rules = serialized.FindProperty("evolutionRules");
            rules.arraySize = 2;
            string[] paths = {
                "Assets/UnityAssset/Trung/Monsters Ultimate Pack 05 Cute Series/Dragon Nightfall Cute Series/Prefabs/Dragon Nightfall.prefab",
                "Assets/UnityAssset/Trung/Monsters Ultimate Pack 05 Cute Series/Dragon Darkness Cute Series/Prefabs/Dragon Darkness.prefab"
            };
            for (int i = 0; i < 2; i++) {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
                if (model == null) throw new System.IO.FileNotFoundException("Evolution model missing: " + paths[i]);
                if (model.GetComponentInChildren<Animator>(true) == null)
                    throw new System.InvalidOperationException("Evolution model has no Animator: " + paths[i]);
                SerializedProperty rule = rules.GetArrayElementAtIndex(i);
                rule.FindPropertyRelative("evolutionId").stringValue = "dragon-dusk-stage-" + (i + 1);
                rule.FindPropertyRelative("sourceStage").intValue = i;
                rule.FindPropertyRelative("targetFormId").stringValue = FormIds[i + 1];
                rule.FindPropertyRelative("targetDisplayName").stringValue = FormNames[i + 1];
                rule.FindPropertyRelative("targetSpecies").stringValue = FormNames[i + 1];
                rule.FindPropertyRelative("targetElement").intValue = (int)PetElement.Dragon;
                rule.FindPropertyRelative("targetSecondaryElement").intValue = (int)PetElement.Dark;
                rule.FindPropertyRelative("targetVisualPrefab").objectReferenceValue = model;
                rule.FindPropertyRelative("trigger").intValue = (int)PetEvolutionTrigger.LevelOrItem;
                rule.FindPropertyRelative("requiredLevel").intValue = i == 0 ? 18 : 36;
                rule.FindPropertyRelative("requiredItemId").stringValue = "dusk-stone";
                rule.FindPropertyRelative("requiredItemQuantity").intValue = 1;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();

            InventoryDebugSeeder seeder = FindInScene<InventoryDebugSeeder>(scene);
            MonsterItemDefinition stone = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>(
                "Assets/Game/Inventory/DuskStone.asset");
            if (seeder == null || stone == null) throw new System.InvalidOperationException("Dusk Stone starter seeder is missing.");
            var seedObject = new SerializedObject(seeder);
            seedObject.FindProperty("seedOnStart").boolValue = true;
            seedObject.FindProperty("includeDefaultItemsWithCustom").boolValue = true;
            SerializedProperty seeds = seedObject.FindProperty("customSeedItems");
            seeds.arraySize = 1;
            seeds.GetArrayElementAtIndex(0).FindPropertyRelative("ItemDefinition").objectReferenceValue = stone;
            seeds.GetArrayElementAtIndex(0).FindPropertyRelative("Quantity").intValue = 99;
            seedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static T FindInScene<T>(Scene scene) where T : Component {
            foreach (GameObject root in scene.GetRootGameObjects()) {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        static void SetProperty(Object target, string name, int value) {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetProperty(Object target, string name, string value) {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(name).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
