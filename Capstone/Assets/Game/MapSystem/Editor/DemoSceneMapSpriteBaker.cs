#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.MapSystem.Editor
{
    public static class DemoSceneMapSpriteBaker
    {
        const string ScenePath = "Assets/Scenes/DemoScene.unity";
        const string OutputFolder = "Assets/Game/MapSystem/Sprites/DemoScene";
        const string WorldMapPath = OutputFolder + "/DemoScene_WorldMap.png";
        const string MinimapPath = OutputFolder + "/DemoScene_Minimap_XRay.png";

        const int WorldMapLongEdge = 2048;
        const int MinimapLongEdge = 1024;
        const float BoundsPadding = 1.02f;

        static readonly string[] AlwaysHiddenTokens =
        {
            "player", "camera", "map icon", "marker", "canvas", "eventsystem",
            "particle", "vfx", "visual effect", "trail", "fog", "sun visual", "moon visual"
        };

        static readonly string[] MinimapHiddenTokens =
        {
            "tree", "grass", "foliage", "plant", "flower", "bush", "shrub",
            "stump", "leaf", "leaves", "branch", "fern", "rock", "stone", "boulder"
        };

        sealed class RendererState
        {
            public Renderer renderer;
            public bool enabled;
            public Material[] sharedMaterials;
        }

        sealed class TerrainState
        {
            public Terrain terrain;
            public bool drawTreesAndFoliage;
            public float treeDistance;
            public float detailObjectDistance;
            public float basemapDistance;
        }

        [MenuItem("Game Tools/Map/Bake DemoScene Map Sprites")]
        public static void BakeFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            try
            {
                BakeDemoSceneSprites();
                EditorUtility.DisplayDialog(
                    "DemoScene Map Sprites",
                    "Da xuat xong:\n" + WorldMapPath + "\n" + MinimapPath,
                    "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("DemoScene Map Sprites", exception.Message, "OK");
            }
        }

        public static void BakeDemoSceneSpritesBatch()
        {
            BakeDemoSceneSprites();
        }

        static void BakeDemoSceneSprites()
        {
            if (!File.Exists(ToAbsolutePath(ScenePath)))
                throw new FileNotFoundException("Khong tim thay DemoScene.", ScenePath);

            EnsureAssetFolder(OutputFolder);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Terrain[] terrains = FindSceneObjects<Terrain>();
            Bounds bounds = CalculateMapBounds(terrains);
            List<RendererState> rendererStates = CaptureRendererStates();
            List<TerrainState> terrainStates = CaptureTerrainStates(terrains);

            bool oldFog = RenderSettings.fog;
            ShadowQuality oldShadows = QualitySettings.shadows;
            Material waterMaterial = CreateWaterBakeMaterial();

            try
            {
                RenderSettings.fog = false;
                QualitySettings.shadows = ShadowQuality.Disable;

                ConfigureRenderers(rendererStates, true, waterMaterial);
                ConfigureTerrains(terrainStates, false);
                Vector2Int worldSize = CalculateOutputSize(bounds, WorldMapLongEdge);
                Texture2D worldMap = RenderTopDown(bounds, worldSize, new Color(0.07f, 0.12f, 0.10f, 1f));
                GradeWorldMap(worldMap);
                SaveTexture(worldMap, WorldMapPath);
                UnityEngine.Object.DestroyImmediate(worldMap);

                ConfigureRenderers(rendererStates, true, waterMaterial);
                ConfigureTerrains(terrainStates, false);
                Vector2Int minimapSize = CalculateOutputSize(bounds, MinimapLongEdge);
                Texture2D minimapSource = RenderTopDown(bounds, minimapSize, new Color(0.025f, 0.055f, 0.065f, 1f));
                Texture2D minimap = BuildXRayMinimap(minimapSource);
                SaveTexture(minimap, MinimapPath);
                UnityEngine.Object.DestroyImmediate(minimapSource);
                UnityEngine.Object.DestroyImmediate(minimap);
            }
            finally
            {
                RestoreRenderers(rendererStates);
                RestoreTerrains(terrainStates);
                RenderSettings.fog = oldFog;
                QualitySettings.shadows = oldShadows;
                if (waterMaterial != null) UnityEngine.Object.DestroyImmediate(waterMaterial);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureSpriteImporter(WorldMapPath);
            ConfigureSpriteImporter(MinimapPath);
            AssetDatabase.SaveAssets();

            Debug.Log(
                $"[DemoSceneMapSpriteBaker] World Map: {WorldMapPath}\n" +
                $"[DemoSceneMapSpriteBaker] Minimap: {MinimapPath}\n" +
                $"[DemoSceneMapSpriteBaker] Bounds: center={bounds.center}, size={bounds.size}");
        }

        static Bounds CalculateMapBounds(Terrain[] terrains)
        {
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (Terrain terrain in terrains)
            {
                if (terrain == null || terrain.terrainData == null) continue;
                Vector3 size = terrain.terrainData.size;
                Bounds terrainBounds = new Bounds(terrain.transform.position + size * 0.5f, size);
                if (!hasBounds)
                {
                    bounds = terrainBounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(terrainBounds);
                }
            }

            if (hasBounds) return bounds;

            foreach (Renderer renderer in FindSceneObjects<Renderer>())
            {
                if (renderer == null || IsAlwaysHidden(renderer.transform)) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!hasBounds) throw new InvalidOperationException("DemoScene khong co Terrain hoac Renderer de bake map.");
            return bounds;
        }

        static Texture2D RenderTopDown(Bounds bounds, Vector2Int outputSize, Color background)
        {
            GameObject cameraObject = new GameObject("__DemoSceneMapBakeCamera")
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            RenderTexture renderTexture = null;
            RenderTexture previousActive = RenderTexture.active;

            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.orthographic = true;
                camera.aspect = (float)outputSize.x / outputSize.y;
                camera.orthographicSize = Mathf.Max(
                    bounds.size.z * 0.5f,
                    bounds.size.x / (2f * camera.aspect)) * BoundsPadding;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.allowHDR = false;
                camera.allowMSAA = true;
                camera.useOcclusionCulling = false;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = Mathf.Max(2000f, bounds.size.y + 1000f);
                camera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));

                float cameraHeight = bounds.max.y + Mathf.Max(250f, bounds.size.y + 100f);
                camera.transform.SetPositionAndRotation(
                    new Vector3(bounds.center.x, cameraHeight, bounds.center.z),
                    Quaternion.Euler(90f, 0f, 0f));

                renderTexture = new RenderTexture(outputSize.x, outputSize.y, 24, RenderTextureFormat.ARGB32)
                {
                    antiAliasing = 4,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderTexture.Create();

                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;

                Texture2D texture = new Texture2D(outputSize.x, outputSize.y, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0f, 0f, outputSize.x, outputSize.y), 0, 0, false);
                texture.Apply(false, false);
                camera.targetTexture = null;
                return texture;
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (renderTexture != null)
                {
                    renderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(renderTexture);
                }
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        static void GradeWorldMap(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color color = pixels[i];
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);

                bool coloredWater = hue >= 0.50f && hue <= 0.69f && saturation > 0.18f && value > 0.72f;
                bool waterHighlight = saturation < 0.14f && value > 0.94f;
                if (coloredWater || waterHighlight)
                {
                    float waterShade = Mathf.Lerp(0.82f, 1.08f, value);
                    pixels[i] = new Color(0.10f * waterShade, 0.43f * waterShade, 0.54f * waterShade, 1f);
                    continue;
                }

                saturation = Mathf.Clamp01(saturation * 0.84f + 0.04f);
                value = Mathf.Clamp01(Mathf.Pow(value, 0.72f) * 1.04f + 0.015f);
                Color graded = Color.HSVToRGB(hue, saturation, value);
                pixels[i] = new Color(graded.r, graded.g, graded.b, 1f);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
        }

        static Texture2D BuildXRayMinimap(Texture2D source)
        {
            int width = source.width;
            int height = source.height;
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] output = new Color32[sourcePixels.Length];
            byte[] categories = new byte[sourcePixels.Length];

            for (int i = 0; i < sourcePixels.Length; i++)
            {
                Color color = sourcePixels[i];
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);

                byte category;
                Color target;
                if (hue >= 0.48f && hue <= 0.68f && saturation > 0.13f)
                {
                    category = 2; // Water
                    target = new Color(0.08f, 0.43f, 0.53f, 1f);
                }
                else if (hue >= 0.07f && hue <= 0.19f && saturation > 0.10f && value > 0.18f)
                {
                    category = 1; // Roads and warm ground
                    target = new Color(0.88f, 0.82f, 0.58f, 1f);
                }
                else if (hue >= 0.18f && hue <= 0.48f && saturation > 0.10f)
                {
                    category = 3; // Vegetation and walkable ground
                    target = new Color(0.07f, 0.24f, 0.20f, 1f);
                }
                else if (saturation < 0.16f)
                {
                    category = 4; // Rock and structures
                    target = new Color(0.20f, 0.29f, 0.31f, 1f);
                }
                else
                {
                    category = 5;
                    target = new Color(0.10f, 0.17f, 0.19f, 1f);
                }

                float shade = Mathf.Lerp(0.82f, 1.12f, value);
                output[i] = new Color(target.r * shade, target.g * shade, target.b * shade, 1f);
                categories[i] = category;
            }

            Color edgeColor = new Color(0.63f, 0.94f, 0.91f, 1f);
            for (int y = 1; y < height - 1; y++)
            {
                int row = y * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int index = row + x;
                    byte category = categories[index];
                    bool boundary = categories[index - 1] != category ||
                                    categories[index + 1] != category ||
                                    categories[index - width] != category ||
                                    categories[index + width] != category;
                    if (!boundary) continue;

                    Color baseColor = output[index];
                    output[index] = Color.Lerp(baseColor, edgeColor, 0.42f);
                }
            }

            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            result.SetPixels32(output);
            result.Apply(false, false);
            return result;
        }

        static List<RendererState> CaptureRendererStates()
        {
            List<RendererState> states = new List<RendererState>();
            foreach (Renderer renderer in FindSceneObjects<Renderer>())
            {
                if (renderer == null) continue;
                states.Add(new RendererState
                {
                    renderer = renderer,
                    enabled = renderer.enabled,
                    sharedMaterials = renderer.sharedMaterials
                });
            }
            return states;
        }

        static List<TerrainState> CaptureTerrainStates(Terrain[] terrains)
        {
            List<TerrainState> states = new List<TerrainState>();
            foreach (Terrain terrain in terrains)
            {
                if (terrain == null) continue;
                states.Add(new TerrainState
                {
                    terrain = terrain,
                    drawTreesAndFoliage = terrain.drawTreesAndFoliage,
                    treeDistance = terrain.treeDistance,
                    detailObjectDistance = terrain.detailObjectDistance,
                    basemapDistance = terrain.basemapDistance
                });
            }
            return states;
        }

        static void ConfigureRenderers(List<RendererState> states, bool minimap, Material waterMaterial)
        {
            foreach (RendererState state in states)
            {
                if (state.renderer == null) continue;
                state.renderer.sharedMaterials = state.sharedMaterials;
                bool hide = IsAlwaysHidden(state.renderer.transform) ||
                            state.renderer is ParticleSystemRenderer ||
                            state.renderer is TrailRenderer ||
                            state.renderer is LineRenderer ||
                            (minimap && ContainsToken(state.renderer.transform, MinimapHiddenTokens));
                state.renderer.enabled = state.enabled && !hide;

                if (!hide && waterMaterial != null && IsWater(state.renderer.transform))
                {
                    Material[] replacements = new Material[Mathf.Max(1, state.sharedMaterials.Length)];
                    for (int i = 0; i < replacements.Length; i++) replacements[i] = waterMaterial;
                    state.renderer.sharedMaterials = replacements;
                }
            }
        }

        static void ConfigureTerrains(List<TerrainState> states, bool includeFoliage)
        {
            foreach (TerrainState state in states)
            {
                if (state.terrain == null) continue;
                state.terrain.drawTreesAndFoliage = includeFoliage;
                state.terrain.treeDistance = includeFoliage ? 5000f : 0f;
                state.terrain.detailObjectDistance = includeFoliage ? 250f : 0f;
                state.terrain.basemapDistance = 5000f;
            }
        }

        static void RestoreRenderers(List<RendererState> states)
        {
            foreach (RendererState state in states)
            {
                if (state.renderer == null) continue;
                state.renderer.enabled = state.enabled;
                state.renderer.sharedMaterials = state.sharedMaterials;
            }
        }

        static void RestoreTerrains(List<TerrainState> states)
        {
            foreach (TerrainState state in states)
            {
                if (state.terrain == null) continue;
                state.terrain.drawTreesAndFoliage = state.drawTreesAndFoliage;
                state.terrain.treeDistance = state.treeDistance;
                state.terrain.detailObjectDistance = state.detailObjectDistance;
                state.terrain.basemapDistance = state.basemapDistance;
            }
        }

        static bool IsAlwaysHidden(Transform transform)
        {
            return ContainsToken(transform, AlwaysHiddenTokens) || transform.gameObject.layer == LayerMask.NameToLayer("UI");
        }

        static bool IsWater(Transform transform)
        {
            string[] waterTokens = { "water", "lake" };
            return ContainsToken(transform, waterTokens);
        }

        static Material CreateWaterBakeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null) return null;

            Material material = new Material(shader)
            {
                name = "__DemoSceneMapWater",
                hideFlags = HideFlags.HideAndDontSave
            };
            Color waterColor = new Color(0.08f, 0.43f, 0.54f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", waterColor);
            if (material.HasProperty("_Color")) material.SetColor("_Color", waterColor);
            return material;
        }

        static bool ContainsToken(Transform transform, string[] tokens)
        {
            for (Transform current = transform; current != null; current = current.parent)
            {
                string lowerName = current.name.ToLowerInvariant();
                foreach (string token in tokens)
                    if (lowerName.Contains(token)) return true;
            }
            return false;
        }

        static Vector2Int CalculateOutputSize(Bounds bounds, int longEdge)
        {
            float aspect = Mathf.Max(0.01f, bounds.size.x / Mathf.Max(0.01f, bounds.size.z));
            if (aspect >= 1f)
                return new Vector2Int(longEdge, Mathf.Max(512, Mathf.RoundToInt(longEdge / aspect)));
            return new Vector2Int(Mathf.Max(512, Mathf.RoundToInt(longEdge * aspect)), longEdge);
        }

        static void SaveTexture(Texture2D texture, string assetPath)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ToAbsolutePath(OutputFolder));
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
        }

        static void ConfigureSpriteImporter(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            importer.SaveAndReimport();
        }

        static void EnsureAssetFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static T[] FindSceneObjects<T>() where T : Component
        {
            List<T> result = new List<T>();
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            foreach (T item in all)
            {
                if (item == null || !item.gameObject.scene.IsValid() || !item.gameObject.scene.isLoaded) continue;
                if ((item.hideFlags & HideFlags.HideAndDontSave) != 0) continue;
                result.Add(item);
            }
            return result.ToArray();
        }
    }
}
#endif
