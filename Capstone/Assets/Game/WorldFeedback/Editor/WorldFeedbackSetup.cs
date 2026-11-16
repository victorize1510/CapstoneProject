using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.CaptureSystem;
using Capstone.Game.Inventory;
using Capstone.Game.UISystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace Capstone.Game.WorldFeedback.Editor {
    [InitializeOnLoad]
    public static class WorldFeedbackSetup {
        public const string Root = "Assets/Game/WorldFeedback/";
        // Thư mục QA, thử lần lượt:
        //   1. QaArtifactPaths.Root (ổ E: của máy gốc) nếu ổ đó tồn tại
        //   2. C:/UnityWork/CapstoneProjectData/Artifacts/
        //   3. <project>/Library/QaArtifacts/ (Git bỏ qua, Unity không xóa khi đóng editor)
        public static readonly string Qa = ResolveQa("pve-model-camera-feedback-2026-11-10/");
        static string ResolveQa(string task) {
            string[] roots = {
                QaArtifactPaths.Root,
                "C:/UnityWork/CapstoneProjectData/Artifacts/",
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "QaArtifacts"))
            };
            foreach (string r in roots) {
                if (string.IsNullOrEmpty(r)) continue;
                string dir = r.Replace('\\', '/'); if (!dir.EndsWith("/")) dir += "/"; dir += task;
                try {
                    string drive = Path.GetPathRoot(Path.GetFullPath(dir));
                    if (string.IsNullOrEmpty(drive) || !Directory.Exists(drive)) continue;
                    Directory.CreateDirectory(dir);
                    return dir;
                } catch (Exception) { }
            }
            Debug.LogWarning("[WorldFeedback] Không tạo được thư mục QA nào.");
            return roots[2].Replace('\\', '/') + "/" + task;
        }
        public const string CommonBall = "Assets/Game/PetSummon/Prefabs/BallGreenClose.prefab"; // Existing GUID/physics contract.
        public const string SafeVersion = "safe-copy-scene-2026-11-11-v2";
        static double nextPoll;
        static WorldFeedbackSetup() {
            EditorApplication.update += Poll;
            try { Directory.CreateDirectory(Qa); File.WriteAllText(Qa + "setup-loaded-version.txt", SafeVersion); }
            catch (Exception e) { Debug.LogWarning("[WorldFeedback] Bỏ qua ghi file QA: " + e.Message); }
        }
        static void Poll() {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + 1;
            try { if (!File.Exists(Qa + "setup.request") || EditorApplication.isPlayingOrWillChangePlaymode) return; } catch (Exception) { return; }
            if (File.ReadAllText(Qa + "setup.request").Trim() != SafeVersion) return;
            File.Delete(Qa + "setup.request");
            try { Build(); } catch (Exception e) { File.WriteAllText(Qa + "setup-error.txt", e.ToString()); Debug.LogException(e); }
        }
        [MenuItem("Tools/Capstone/Advanced/World Feedback/1. Apply models and presentation")]
        public static void Build() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before applying authoring assets.");
            Directory.CreateDirectory(Qa); Directory.CreateDirectory(Root + "Prefabs"); Directory.CreateDirectory(Root + "Materials");
            AssetDatabase.Refresh();
            ConfigureImporters();
            var imported = new List<string>();
            foreach (string id in new[] { "BallCommon", "BallRare", "BallUltra", "RecoveryTable" }) {
                string materialPath = Root + "Materials/" + id + ".mat";
                var material = BuildMaterial(id, materialPath);
                bool common = id == "BallCommon", table = id == "RecoveryTable";
                string path = common ? CommonBall : Root + "Prefabs/" + id + ".prefab";
                BuildPrefab(id, path, material, table, common); imported.Add(path);
            }
            var capture = AssetDatabase.LoadAssetAtPath<CaptureSettings>("Assets/Resources/Capture/CaptureSettings.asset");
            if (capture != null) { capture.ballVisual = AssetDatabase.LoadAssetAtPath<GameObject>(CommonBall); EditorUtility.SetDirty(capture); AssetDatabase.SaveAssetIfDirty(capture); }
            var item = AssetDatabase.LoadAssetAtPath<MonsterItemDefinition>("Assets/Resources/Capture/BasicCaptureBall.asset");
            if (item != null) {
                var serialized = new SerializedObject(item); serialized.FindProperty("Icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/BallCommon/BallCommon_Icon.png");
                serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(item);
            }
            ApplyOpenScene();
            File.WriteAllLines(Qa + "authoring-files.txt", imported);
            File.WriteAllText(Qa + "setup-status.txt", "APPLIED " + DateTime.UtcNow.ToString("O"));
            if (File.Exists(Qa + "setup-error.txt")) File.Delete(Qa + "setup-error.txt");
            Debug.Log("[WorldFeedback] Three ball prefabs, six-socket recovery table and main-camera visibility configured.");
        }
        static Material BuildMaterial(string id, string path) {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = id }; AssetDatabase.CreateAsset(material, path); }
            string art = Root + "Art/" + id + "/" + id;
            string packed = Root + "Materials/" + id + "_MetallicSmoothness.png";
            Pack(art + "_metallic.png", art + "_roughness.png", packed, id == "RecoveryTable" ? 2048 : 1024);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(art + ".png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(art + "_normal.png"));
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packed));
            material.SetColor("_BaseColor", Color.white); material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Smoothness", .8f); material.SetFloat("_Metallic", 1); material.SetFloat("_BumpScale", .8f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP"); material.DisableKeyword("_EMISSION");
            material.enableInstancing = true; EditorUtility.SetDirty(material); AssetDatabase.SaveAssetIfDirty(material); return material;
        }
        static void ConfigureImporters() {
            foreach (var path in Directory.GetFiles(Root + "Art", "*", SearchOption.AllDirectories)) {
                string asset = path.Replace('\\', '/');
                if (asset.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) {
                    var model = AssetImporter.GetAtPath(asset) as ModelImporter; if (model == null) continue;
                    model.materialImportMode = ModelImporterMaterialImportMode.None; model.importAnimation = false;
                    model.animationType = ModelImporterAnimationType.None; model.importNormals = ModelImporterNormals.Import;
                    model.importTangents = ModelImporterTangents.CalculateMikk; model.isReadable = false; model.addCollider = false;
                    model.SaveAndReimport();
                } else if (asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
                    var texture = AssetImporter.GetAtPath(asset) as TextureImporter; if (texture == null) continue;
                    bool icon = asset.EndsWith("_Icon.png", StringComparison.Ordinal), normal = asset.EndsWith("_normal.png", StringComparison.Ordinal);
                    bool scalar = asset.EndsWith("_metallic.png", StringComparison.Ordinal) || asset.EndsWith("_roughness.png", StringComparison.Ordinal);
                    texture.textureType = icon ? TextureImporterType.Sprite : normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    texture.spriteImportMode = SpriteImportMode.Single; texture.sRGBTexture = !normal && !scalar;
                    texture.alphaIsTransparency = icon; texture.mipmapEnabled = !icon; texture.isReadable = false;
                    texture.wrapMode = TextureWrapMode.Clamp; texture.maxTextureSize = icon ? 256 : asset.Contains("RecoveryTable/") ? 2048 : 1024;
                    texture.textureCompression = icon ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
                    texture.SaveAndReimport();
                }
            }
        }
        static void Pack(string metalPath, string roughPath, string output, int maxSize) {
            if (!File.Exists(metalPath) || !File.Exists(roughPath)) throw new FileNotFoundException("Missing PBR source maps.");
            if (File.Exists(output) && File.GetLastWriteTimeUtc(output) >= File.GetLastWriteTimeUtc(metalPath)
                && File.GetLastWriteTimeUtc(output) >= File.GetLastWriteTimeUtc(roughPath)) return;
            var metal = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var rough = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Texture2D result = null;
            try {
                metal.LoadImage(File.ReadAllBytes(metalPath)); rough.LoadImage(File.ReadAllBytes(roughPath));
                int size = Mathf.Min(maxSize, Mathf.Min(metal.width, rough.width));
                result = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                    float u = (x + .5f) / size, v = (y + .5f) / size;
                    byte m = (byte)Mathf.RoundToInt(metal.GetPixelBilinear(u, v).r * 255);
                    byte smooth = (byte)Mathf.RoundToInt((1 - rough.GetPixelBilinear(u, v).r) * 255);
                    pixels[y * size + x] = new Color32(m, m, m, smooth);
                }
                result.SetPixels32(pixels); result.Apply(); File.WriteAllBytes(output, result.EncodeToPNG());
                AssetDatabase.ImportAsset(output);
                var importer = (TextureImporter)AssetImporter.GetAtPath(output); importer.sRGBTexture = false;
                importer.maxTextureSize = maxSize; importer.mipmapEnabled = true; importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            } finally { Object.DestroyImmediate(metal); Object.DestroyImmediate(rough); if (result != null) Object.DestroyImmediate(result); }
        }
        static void BuildPrefab(string id, string path, Material material, bool table, bool common) {
            bool loaded = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = loaded ? PrefabUtility.LoadPrefabContents(path) : new GameObject(id);
            try {
                root.name = id;
                var oldVisual = root.transform.Find("Visual"); if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);
                var oldCollision = root.transform.Find("Collision"); if (oldCollision != null) Object.DestroyImmediate(oldCollision.gameObject);
                var visual = new GameObject("Visual"); visual.transform.SetParent(root.transform, false);
                var lods = new LOD[3];
                for (int i = 0; i < 3; i++) {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/" + id + "/" + id + "_LOD" + i + ".fbx");
                    if (source == null) throw new FileNotFoundException("Optimized mesh missing: " + id);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(source); instance.name = "LOD" + i; instance.transform.SetParent(visual.transform, false);
                    var renderers = instance.GetComponentsInChildren<Renderer>(true);
                    foreach (var renderer in renderers) renderer.sharedMaterial = material;
                    lods[i] = new LOD(i == 0 ? .16f : i == 1 ? .045f : .008f, renderers);
                }
                if (!table) visual.transform.localScale = Vector3.one * .5f;
                var group = root.GetComponent<LODGroup>(); if (group == null) group = root.AddComponent<LODGroup>();
                group.SetLODs(lods); group.RecalculateBounds(); group.fadeMode = LODFadeMode.None;
                if (table) {
                    // Static, simplified collision mesh keeps the six recesses and leg gaps; no giant enclosing BoxCollider.
                    var collision = new GameObject("Collision"); collision.transform.SetParent(root.transform, false);
                    var low = visual.transform.Find("LOD2");
                    foreach (var filter in low.GetComponentsInChildren<MeshFilter>(true)) {
                        var colliderObject = new GameObject("Solid"); colliderObject.transform.SetParent(collision.transform, false);
                        colliderObject.transform.SetPositionAndRotation(filter.transform.position, filter.transform.rotation);
                        colliderObject.transform.localScale = filter.transform.lossyScale;
                        colliderObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                    Bounds bounds = BoundsOf(lods[0].renderers);
                    var obstacle = root.GetComponent<NavMeshObstacle>(); if (obstacle == null) obstacle = root.AddComponent<NavMeshObstacle>();
                    obstacle.shape = NavMeshObstacleShape.Box; obstacle.center = root.transform.InverseTransformPoint(bounds.center);
                    obstacle.size = bounds.size; obstacle.carving = true;
                    var set = root.GetComponent<CameraOccluder>(); if (set == null) set = root.AddComponent<CameraOccluder>();
                    set.renderers = lods.SelectMany(l => l.renderers).ToArray();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
            } finally { if (loaded) PrefabUtility.UnloadPrefabContents(root); else Object.DestroyImmediate(root); }
        }
        static Bounds BoundsOf(Renderer[] renderers) {
            var bounds = renderers[0].bounds; for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds); return bounds;
        }
        static void ApplyOpenScene() {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.name != "DemoScene") return;
            // Preserve even unsaved user edits before adding the single authored prop.
            Directory.CreateDirectory(Qa + "before");
            EditorSceneManager.SaveScene(scene, Qa + "before/DemoScene-unsaved-before.unity", true);
            foreach (var camera in Object.FindObjectsByType<BasicCameraFollow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (camera.GetComponent<CameraVisibility>() == null) Undo.AddComponent<CameraVisibility>(camera.gameObject);
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "PetRecoveryTable_VisualOnly");
            if (existing == null) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/RecoveryTable.prefab");
                var table = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene); table.name = "PetRecoveryTable_VisualOnly";
                var shop = GameObject.Find("ShopNPC"); var player = Object.FindFirstObjectByType<PetCommandInput>();
                var anchor = shop != null ? shop.transform : player != null ? player.transform : null;
                Vector3 home = anchor != null ? anchor.position : Vector3.zero;
                var offsets = new[] { new Vector3(3.5f,0,2),new Vector3(-3.5f,0,2),new Vector3(3.5f,0,-2),new Vector3(-3.5f,0,-2),new Vector3(5,0,0),new Vector3(0,0,5) };
                table.SetActive(false); bool placed = false;
                foreach (var offset in offsets) {
                    Vector3 point = home + offset;
                    if (NavMesh.SamplePosition(point, out var nav, 2.5f, NavMesh.AllAreas)) point = nav.position;
                    else if (Physics.Raycast(point + Vector3.up * 4, Vector3.down, out var floor, 10, ~0, QueryTriggerInteraction.Ignore)) point = floor.point;
                    var touching = Physics.OverlapBox(point + Vector3.up * .65f, new Vector3(1.9f, .5f, 1.9f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                    if (touching.Any(c => c != null && !(c is TerrainCollider) && c.bounds.max.y > point.y + .15f)) continue;
                    table.transform.position = point; placed = true; break;
                }
                if (!placed) { Object.DestroyImmediate(table); throw new InvalidOperationException("No clear 2m table placement near Shop. No existing scene objects were moved."); }
                table.transform.rotation = shop != null ? shop.transform.rotation : Quaternion.identity;
                table.SetActive(true); Undo.RegisterCreatedObjectUndo(table, "Place visual recovery table");
                File.WriteAllText(Qa + "table-placement.txt", table.transform.position + "\n" + table.transform.rotation.eulerAngles);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            // The open scene already contains user edits. Keep it dirty/reviewable and save a copy only.
            // Persisting the original scene is an explicit final handoff action, never an automatic setup side effect.
            Directory.CreateDirectory(Qa + "after");
            EditorSceneManager.SaveScene(scene, Qa + "after/DemoScene-with-feedback.unity", true);
        }
    }

}