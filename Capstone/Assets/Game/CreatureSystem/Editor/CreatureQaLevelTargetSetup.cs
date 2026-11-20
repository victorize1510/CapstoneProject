using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem.Editor
{
    public static class CreatureQaLevelTargetSetup
    {
        const string Folder = "Assets/Game/CreatureSystem/Runtime/QA";
        const string SceneName = "DemoScene 1 - Creature QA";
        const string RootName = "Creature QA Level Targets";
        const float HalfSize = .4f;

        [MenuItem("Tools/Capstone/Creatures/QA/Create or verify level target prefabs")]
        public static void CreateOrVerifyPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Game/CreatureSystem/Runtime", "QA");
            CreateOrVerify(18, new Color(.08f, .74f, .86f));
            CreateOrVerify(36, new Color(.89f, .39f, .08f));
            Debug.Log("QA Level Targets: verified both cube prefabs, colliders, and training dummy setup.");
        }

        static GameObject CreateOrVerify(int level, Color color)
        {
            string name = $"QA_Level_{level}_Cube";
            string path = $"{Folder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                string materialPath = $"{Folder}/{name}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) throw new InvalidOperationException("Missing URP/Lit shader.");
                    material = new Material(shader) { name = name, color = color };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    cube.name = name;
                    cube.transform.localScale = Vector3.one * (HalfSize * 2);
                    cube.GetComponent<MeshRenderer>().sharedMaterial = material;
                    var target = cube.AddComponent<CreatureQaLevelTarget>();
                    var serializedTarget = new SerializedObject(target);
                    serializedTarget.FindProperty("targetLevel").intValue = level;
                    serializedTarget.ApplyModifiedPropertiesWithoutUndo();
                    var dummy = cube.GetComponent<DummyEnemy>();
                    dummy.trainingDummy = true;
                    dummy.maxHealth = 1;
                    dummy.autoRevive = true;
                    dummy.reviveDelay = 0;
                    dummy.tintFeedback = false;
                    dummy.writeHealthToName = false;
                    prefab = PrefabUtility.SaveAsPrefabAsset(cube, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(cube); }
            }
            var component = prefab != null ? prefab.GetComponent<CreatureQaLevelTarget>() : null;
            if (component == null || component.TargetLevel != level || !prefab.TryGetComponent(out BoxCollider box)
                || box.isTrigger || !prefab.TryGetComponent(out DummyEnemy configuredDummy) || !configuredDummy.IsTrainingDummy
                || prefab.GetComponent<BasicCreatureEnemy>() != null || prefab.GetComponent<CaptureSystem.WildPetCaptureTarget>() != null
                || prefab.GetComponent<MeshRenderer>()?.sharedMaterial == null)
                throw new InvalidOperationException($"Invalid QA level target prefab: {path}");
            return prefab;
        }

        [MenuItem("Tools/Capstone/Creatures/QA/Place level cubes beside Scene view focus")]
        static void PlaceInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.name != SceneName || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Debug.LogWarning($"Open {SceneName} in Edit Mode to place the level cubes.");
                return;
            }
            CreateOrVerifyPrefabs();
            if (scene.GetRootGameObjects().Any(root => root.name == RootName || root.GetComponentInChildren<CreatureQaLevelTarget>(true) != null))
            {
                Debug.LogWarning("QA level cubes already exist. Move the existing instances if needed.");
                return;
            }
            var view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                Debug.LogWarning("Focus the Scene view on a clear patch of ground, then run the placement command.");
                return;
            }

            Physics.SyncTransforms();
            var zones = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<CreatureEncounterZone>(true))
                .Select(zone => zone.GetComponent<BoxCollider>()).Where(box => box != null && box.gameObject.activeInHierarchy).ToArray();
            var horizontal = Vector3.ProjectOnPlane(view.camera.transform.right, Vector3.up).normalized;
            if (!TryFindPosition(scene, view.pivot - horizontal, zones, out var left)
                || !TryFindPosition(scene, view.pivot + horizontal, zones, out var right))
            {
                Debug.LogWarning("No safe adjacent ground outside Spawn Zones at the Scene view focus. Focus an open patch next to the path and retry.");
                return;
            }

            var parent = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(parent, "Place QA level target cubes");
            Place(parent.transform, 18, left);
            Place(parent.transform, 36, right);
            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = parent;
        }

        static bool TryFindPosition(Scene scene, Vector3 point, BoxCollider[] zones, out Vector3 position)
        {
            position = default;
            if (!Physics.Raycast(point + Vector3.up * 5, Vector3.down, out var floor, 15f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) || floor.collider.gameObject.scene != scene || floor.normal.y < .9f
                || floor.collider.GetComponentInParent<PetCommandInput>() != null
                || !NavMesh.SamplePosition(floor.point, out var navPoint, 2f, NavMesh.AllAreas)
                || Mathf.Abs(floor.point.y - navPoint.position.y) > .5f) return false;

            position = floor.point + Vector3.up * (HalfSize + .025f);
            var clearance = Vector3.one * HalfSize;
            var bounds = new Bounds(position, clearance * 2);
            if (zones.Any(zone => zone.bounds.Intersects(bounds))) return false;
            if (Physics.OverlapBox(position, clearance, Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                .Any(collider => collider != floor.collider)) return false;
            return !scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<PetCommandInput>())
                .Any(player => (player.transform.position - floor.point).sqrMagnitude < 4f);
        }

        static void Place(Transform parent, int level, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/QA_Level_{level}_Cube.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.position = position;
            Undo.RegisterCreatedObjectUndo(instance, "Place QA level target cube");
        }
    }
}
