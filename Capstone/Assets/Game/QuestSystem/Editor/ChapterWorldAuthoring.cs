using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.CreatureSystem;
using Capstone.Game.SaveSystem;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Capstone.Game.QuestSystem.Editor {
    public static class ChapterWorldAuthoring {
        const string ScenePath = "Assets/Scenes/DemoScene 3.unity";
        const string DataFolder = "Assets/Scenes/DemoScene 3";
        const string ArtFolder = "Assets/Game/MapSystem/Sprites/Chapter01";
        const string ZonePrefabFolder = "Assets/Game/CreatureSystem/Runtime/SpawnZones/ApprovedSix";
        internal const string CaveElderName = "Peasant Nolant(Free Version) (1)";
        const float FloorSearch = 11f;

        [MenuItem("Tools/Capstone/Chapter I/Bind Quest and Travel NPCs")]
        public static void BindQuestAndTravelNpcs() {
            var scene = SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != ScenePath || scene.isDirty || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Mở DemoScene 3 đã lưu trong Edit Mode trước khi gắn Trưởng làng.");
            var layout = FindAll<ChapterWorldLayout>(scene).SingleOrDefault();
            if (layout == null) throw new InvalidOperationException("Thiếu Chapter I World trong DemoScene 3.");
            var data = new SerializedObject(layout);
            BindElders(scene, layout, data);
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Không lưu được DemoScene 3.");
        }

        [MenuItem("Tools/Capstone/Chapter I/Build Cave Navigation and Maps")]
        public static void Run() {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath || scene.isDirty || SceneManager.sceneCount != 1)
                throw new InvalidOperationException("Mở DemoScene 3 đã lưu trong Edit Mode trước khi dựng dữ liệu hang.");
            var cave = GameObject.Find("MapCave").transform;
            Bounds caveWorld = BoundsForCave(cave);
            if (!new[] { "Cave_Corridor_A1", "Cave_Floor_Piece_A1 (2)", "Cave_Floor_Piece_A3", "Cave_Floor_Piece_A2" }
                .All(name => cave.Find(name) is Transform model &&
                    NavMesh.SamplePosition(model.position + Vector3.up, out _, FloorSearch, NavMesh.AllAreas))) {
                var surface = cave.GetComponent<NavMeshSurface>() ?? cave.gameObject.AddComponent<NavMeshSurface>();
                surface.collectObjects = CollectObjects.Children;
                surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize = true;
                surface.voxelSize = .18f;
                surface.overrideTileSize = true;
                surface.tileSize = 128;
                surface.buildHeightMesh = true;
                surface.BuildNavMesh();
                if (surface.navMeshData == null) throw new InvalidOperationException("Không tạo được NavMesh hang.");
                PersistNavMesh(surface);
            }

            var portalIn = SnapFloor(cave, "Cave_Corridor_A1");
            var entry = SnapFloor(cave, "Cave_Floor_Piece_A1 (2)");
            var middle = SnapFloor(cave, "Cave_Floor_Piece_A3");
            var deep = SnapFloor(cave, "Cave_Floor_Piece_A2");
            var lair = SnapFloor(cave, "Cave_Corridor_A1_Curved_A (2)");
            VerifyCavePath(portalIn, entry, "Lối dịch chuyển – Cửa hang");
            VerifyCavePath(entry, middle, "Cửa hang – Khu giữa");
            VerifyCavePath(middle, deep, "Khu giữa – Tầng sâu");
            VerifyCavePath(deep, lair, "Tầng sâu – Sào huyệt");

            var triangles = NavMesh.CalculateTriangulation();
            Rect caveRect = new Rect(caveWorld.min.x, caveWorld.min.z, caveWorld.size.x, caveWorld.size.z);
            var caveFaces = SelectFaces(triangles, center => caveRect.Contains(new Vector2(center.x, center.z)));
            var caveAnchor = new[] { portalIn, entry, middle, deep };
            caveFaces = ConnectedFaces(triangles, caveFaces, caveAnchor);
            var grassFaces = SelectFaces(triangles, center => !caveRect.Contains(new Vector2(center.x, center.z)));
            if (caveFaces.Count == 0 || grassFaces.Count == 0) throw new InvalidOperationException("Không có đủ mặt đi được cho hai bản đồ.");
            var caveArt = SaveArt("HangDong", triangles, caveFaces, true, out Vector4 caveBounds);
            var grassArt = SaveArt("DongCo", triangles, grassFaces, false, out Vector4 grassBounds);

            var root = GameObject.Find("Chapter I World") ?? new GameObject("Chapter I World");
            var layout = root.GetComponent<ChapterWorldLayout>() ?? root.AddComponent<ChapterWorldLayout>();
            var entryPoint = Place(root.transform, "loc_cave_entrance_01", entry, middle - entry);
            var duskPoint = Place(root.transform, "loc_cave_dusk_01", deep, deep - middle);
            var bossPoint = Place(root.transform, "loc_cave_boss_01", lair, lair - deep);
            MakeZone(root.transform, "Entrance", entry);
            MakeZone(root.transform, "Mid", middle);
            MakeZone(root.transform, "Deep", deep);
            var assignedBoss = FindAll<DummyEnemy>(scene).Where(enemy => enemy.IsBoss &&
                caveRect.Contains(new Vector2(enemy.transform.position.x, enemy.transform.position.z))).ToArray();

            var data = new SerializedObject(layout);
            data.FindProperty("entryPoint").objectReferenceValue = entryPoint;
            data.FindProperty("duskPoint").objectReferenceValue = duskPoint;
            data.FindProperty("bossPoint").objectReferenceValue = bossPoint;
            data.FindProperty("grassMap").objectReferenceValue = grassArt;
            data.FindProperty("caveMap").objectReferenceValue = caveArt;
            data.FindProperty("grassBounds").vector4Value = grassBounds;
            data.FindProperty("caveBounds").vector4Value = caveBounds;
            data.FindProperty("caveRegionBounds").vector4Value = new Vector4(caveRect.xMin, caveRect.yMin, caveRect.width, caveRect.height);
            if (assignedBoss.Length == 1) data.FindProperty("boss").objectReferenceValue = assignedBoss[0];
            BindElders(scene, layout, data);
            data.ApplyModifiedPropertiesWithoutUndo();
            var saves = FindAll<PlayerSaveController>(scene).SingleOrDefault();
            if (saves != null) {
                var saveSettings = new SerializedObject(saves);
                saveSettings.FindProperty("loadOnStart").boolValue = true;
                saveSettings.FindProperty("seedDragonPreviewPartyOnFreshSave").boolValue = false;
                saveSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Không lưu được DemoScene 3.");
            AssetDatabase.SaveAssets();
            Debug.Log($"[Chapter I] DemoScene 3 saved | grass faces {grassFaces.Count} | cave floor faces {caveFaces.Count} | " +
                $"cave entry {portalIn}, middle {middle}, deep {deep} | boss candidates {assignedBoss.Length}");
        }

        static IEnumerable<T> FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        static Bounds BoundsForCave(Transform cave) {
            var bounds = new Bounds(cave.position, Vector3.zero);
            foreach (var shape in cave.GetComponentsInChildren<Collider>())
                if (shape.enabled && !shape.isTrigger) bounds.Encapsulate(shape.bounds);
            return bounds;
        }

        static void PersistNavMesh(NavMeshSurface surface) {
            string path = DataFolder + "/NavMesh-Cave.asset";
            var data = surface.navMeshData;
            var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if (existing == null) AssetDatabase.CreateAsset(data, path);
            else {
                surface.RemoveData();
                EditorUtility.CopySerialized(data, existing);
                UnityEngine.Object.DestroyImmediate(data);
                surface.navMeshData = existing;
                surface.AddData();
            }
            EditorUtility.SetDirty(surface.navMeshData);
        }

        static Vector3 SnapFloor(Transform cave, string name) {
            var model = cave.Find(name);
            if (model == null) throw new InvalidOperationException($"Thiếu mốc hình học {name}.");
            var reference = model.position;
            if (!NavMesh.SamplePosition(reference + Vector3.up, out var result, FloorSearch, NavMesh.AllAreas))
                throw new InvalidOperationException($"Không có mặt đi được gần {name} ({reference}).");
            return result.position;
        }

        static void BindElders(Scene scene, ChapterWorldLayout layout, SerializedObject data) {
            var cutscene = FindAll<VillageChiefCutscene>(scene).SingleOrDefault();
            var grass = cutscene != null ? cutscene.Chief : null;
            var cave = FindAll<Transform>(scene).SingleOrDefault(actor => actor.name == CaveElderName);
            if (grass == null || cave == null) throw new InvalidOperationException("Thiếu Trưởng làng hoặc " + CaveElderName);
            var rest = new SerializedObject(cutscene).FindProperty("npcFinalPoint").objectReferenceValue as Transform;
            if (rest == null) rest = grass;
            if (!ChapterWorldLayout.TryFindElderArrival(grass, rest.position, rest.rotation, out var grassPoint))
                throw new InvalidOperationException("Không tìm thấy chỗ đứng trước Trưởng làng tại " + rest.position);
            var entry = data.FindProperty("entryPoint").objectReferenceValue as Transform;
            if (!ChapterWorldLayout.TryFindElderArrival(cave, cave.position, cave.rotation, out var cavePoint, entry?.position)) {
                if (!ChapterWorldLayout.TryFindElderArrival(cave, cave.position, cave.rotation, out cavePoint))
                    throw new InvalidOperationException("Không tìm thấy chỗ đứng cạnh Nolant trong hang tại " + cave.position);
                Debug.LogWarning("[Chapter I] Có điểm đến cạnh Nolant, nhưng NavMesh chưa nối từ đó tới cửa hang.");
            }
            data.FindProperty("grassElder").objectReferenceValue = grass;
            data.FindProperty("caveElder").objectReferenceValue = cave;
            SetArrival(layout.transform, data.FindProperty("grassPortal"), "Điểm đến đồng cỏ", grassPoint, rest.position);
            SetArrival(layout.transform, data.FindProperty("cavePortal"), "Điểm đến hang", cavePoint, cave.position);
            Debug.Log($"[Chapter I] Gắn Trưởng làng {grass.name} → {grassPoint}, {cave.name} → {cavePoint}.");
        }

        static void SetArrival(Transform root, SerializedProperty property, string name, Vector3 point, Vector3 lookAt) {
            if (property.objectReferenceValue is Transform existing) existing.name = name;
            property.objectReferenceValue = Place(root, name, point, lookAt - point);
        }

        static void VerifyCavePath(Vector3 from, Vector3 to, string label) {
            var path = new NavMeshPath();
            bool complete = NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path) &&
                path.status == NavMeshPathStatus.PathComplete;
            if (!complete) Debug.LogWarning($"[Chapter I] Đường hang chưa nối được: {label}, {from} → {to}.");
            else Debug.Log($"[Chapter I] Đường hang hợp lệ: {label}.");
        }

        static Transform Place(Transform root, string name, Vector3 point, Vector3 forward) {
            var marker = root.Find(name) ?? new GameObject(name).transform;
            marker.SetParent(root);
            forward.y = 0;
            marker.SetPositionAndRotation(point, forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : Quaternion.identity);
            return marker;
        }

        static void MakeZone(Transform root, string suffix, Vector3 point) {
            string name = "Zone_Cave_" + suffix;
            var child = root.Find(name);
            if (child == null) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ZonePrefabFolder}/{name}.prefab");
                if (prefab == null) throw new InvalidOperationException("Thiếu prefab " + name);
                child = ((GameObject)PrefabUtility.InstantiatePrefab(prefab, root)).transform;
            }
            child.position = point;
            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
        }

        static List<int> SelectFaces(NavMeshTriangulation mesh, Predicate<Vector3> predicate) {
            var selected = new List<int>();
            for (int i = 0; i < mesh.indices.Length; i += 3) {
                var center = (mesh.vertices[mesh.indices[i]] + mesh.vertices[mesh.indices[i + 1]] + mesh.vertices[mesh.indices[i + 2]]) / 3;
                if (predicate(center)) selected.Add(i);
            }
            return selected;
        }

        static List<int> ConnectedFaces(NavMeshTriangulation mesh, List<int> faces, Vector3[] anchors) {
            // Unity duplicates tile-boundary vertices. Weld positions before locating floor components.
            var vertexStart = new Dictionary<Vector3Int, int>();
            var parents = Enumerable.Range(0, faces.Count).ToArray();
            int Root(int index) {
                while (index != parents[index]) {
                    parents[index] = parents[parents[index]];
                    index = parents[index];
                }
                return index;
            }
            for (int i = 0; i < faces.Count; i++) {
                for (int corner = 0; corner < 3; corner++) {
                    var key = Vector3Int.RoundToInt(mesh.vertices[mesh.indices[faces[i] + corner]] * 25f);
                    if (vertexStart.TryGetValue(key, out int other)) parents[Root(i)] = Root(other);
                    else vertexStart[key] = i;
                }
            }
            var roots = new HashSet<int>();
            foreach (var anchor in anchors) {
                int nearest = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < faces.Count; i++)
                    for (int corner = 0; corner < 3; corner++) {
                        float score = (mesh.vertices[mesh.indices[faces[i] + corner]] - anchor).sqrMagnitude;
                        if (score >= best) continue;
                        nearest = i;
                        best = score;
                    }
                if (nearest >= 0) roots.Add(Root(nearest));
            }
            return faces.Where((face, index) => roots.Contains(Root(index))).ToList();
        }

        static Sprite SaveArt(string name, NavMeshTriangulation mesh, List<int> faces, bool cave, out Vector4 bounds) {
            Directory.CreateDirectory(ArtFolder);
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = -min;
            foreach (int face in faces)
                for (int i = 0; i < 3; i++) {
                    Vector3 point = mesh.vertices[mesh.indices[face + i]];
                    min = Vector3.Min(min, point);
                    max = Vector3.Max(max, point);
                }
            float pad = Mathf.Max(max.x - min.x, max.z - min.z) * .035f;
            float width = max.x - min.x + 2 * pad;
            float height = max.z - min.z + 2 * pad;
            bounds = new Vector4(min.x - pad, min.z - pad, width, height);
            float pixelsPerMeter = (cave ? 1024f : 1536f) / Mathf.Max(width, height);
            int w = Mathf.Max(32, Mathf.RoundToInt(width * pixelsPerMeter));
            int h = Mathf.Max(32, Mathf.RoundToInt(height * pixelsPerMeter));
            var floor = new float[w * h];
            Array.Fill(floor, float.NegativeInfinity);
            Vector3 Pixel(Vector3 position) => new Vector3(
                (position.x - min.x + pad) / width * (w - 1), (position.z - min.z + pad) / height * (h - 1), position.y);
            foreach (int i in faces) Raster(Pixel(mesh.vertices[mesh.indices[i]]), Pixel(mesh.vertices[mesh.indices[i + 1]]),
                Pixel(mesh.vertices[mesh.indices[i + 2]]), floor, w, h);
            var above = cave ? new Color32(88, 78, 112, 255) : new Color32(120, 140, 96, 255);
            var below = cave ? new Color32(47, 47, 73, 255) : new Color32(54, 91, 72, 255);
            var border = cave ? new Color32(171, 131, 120, 255) : new Color32(190, 177, 123, 255);
            var ground = cave ? new Color32(21, 23, 38, 255) : new Color32(24, 43, 47, 255);
            var pixels = new Color32[floor.Length];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) {
                    int index = y * w + x;
                    float z = floor[index];
                    float grain = Mathf.PerlinNoise(x * .071f, y * .071f);
                    if (float.IsNegativeInfinity(z)) {
                        pixels[index] = Color32.Lerp(ground, below, grain * .095f);
                        continue;
                    }
                    bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 ||
                        float.IsNegativeInfinity(floor[index - 1]) || float.IsNegativeInfinity(floor[index + 1]) ||
                        float.IsNegativeInfinity(floor[index - w]) || float.IsNegativeInfinity(floor[index + w]);
                    float elevation = Mathf.InverseLerp(min.y, max.y, z);
                    Color32 terrain = Color32.Lerp(below, above, .15f + .65f * elevation + .16f * grain);
                    float contour = Mathf.Repeat(z, cave ? 3f : 7f);
                    pixels[index] = edge ? Color32.Lerp(terrain, border, .62f) : contour < .16f
                        ? Color32.Lerp(terrain, border, .19f) : terrain;
                }
            var image = new Texture2D(w, h, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            string path = $"{ArtFolder}/{name}.png";
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void Raster(Vector3 a, Vector3 b, Vector3 c, float[] floor, int w, int h) {
            float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
            if (Mathf.Abs(denominator) < .001f) return;
            int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x)), 0, w - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.x, b.x, c.x)), 0, w - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(a.y, b.y, c.y)), 0, h - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(a.y, b.y, c.y)), 0, h - 1);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++) {
                    float u = ((b.y - c.y) * (x - c.x) + (c.x - b.x) * (y - c.y)) / denominator;
                    float v = ((c.y - a.y) * (x - c.x) + (a.x - c.x) * (y - c.y)) / denominator;
                    if (u >= -.0001f && v >= -.0001f && u + v <= 1.0001f)
                        floor[y * w + x] = Mathf.Max(floor[y * w + x], u * a.z + v * b.z + (1 - u - v) * c.z);
                }
        }
    }
}
