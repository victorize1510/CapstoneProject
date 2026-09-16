using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.PetSummon.Editor
{
    [InitializeOnLoad]
    public static class PetSummonPhaseOneSetup
    {
        const string RootFolder = "Assets/Game/PetSummon";
        const string PrefabFolder = RootFolder + "/Prefabs";
        const string MaterialFolder = RootFolder + "/Materials";
        const string BallPrefabPath = PrefabFolder + "/BallGreenClose.prefab";
        const string BallMaterialPath = MaterialFolder + "/BallGreenClose.mat";
        const string PlayerPrefabPath = "Assets/Prefabs/Thắng/Player_GenderSwitch.prefab";
        const string BallArtFolder = RootFolder + "/Art/BallGreenClose";
        const string BallModelPath = BallArtFolder + "/Meshy_AI_Emerald_Orb_with_Gold_0721122943_texture.fbx";
        const string BaseColorPath = BallArtFolder + "/Meshy_AI_Emerald_Orb_with_Gold_0721122943_texture.png";
        const string NormalPath = BallArtFolder + "/Meshy_AI_Emerald_Orb_with_Gold_0721122943_texture_normal.png";
        const string MetallicPath = BallArtFolder + "/Meshy_AI_Emerald_Orb_with_Gold_0721122943_texture_metallic.png";
        const string RoughnessPath = BallArtFolder + "/Meshy_AI_Emerald_Orb_with_Gold_0721122943_texture_roughness.png";
        const string PackedMetallicSmoothnessPath = MaterialFolder + "/BallGreenClose_MetallicSmoothness.png";

        static bool attemptedThisDomain;

        static PetSummonPhaseOneSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/Capstone/Pet Summon/Run Phase 1 Setup")]
        public static void RunFromMenu()
        {
            RunSetup(true);
        }

        public static void RunFromCommandLine()
        {
            RunSetup(true);
        }

        static void TryAutoSetup()
        {
            if (attemptedThisDomain || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            attemptedThisDomain = true;
            if (!IsSetupComplete())
            {
                RunSetup(false);
            }
        }

        static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            attemptedThisDomain = false;
            EditorApplication.delayCall += TryAutoSetup;
        }

        static bool IsSetupComplete()
        {
            GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (ballPrefab == null || playerPrefab == null)
            {
                return false;
            }

            PetSummonHandSocket handSocket = playerPrefab.GetComponent<PetSummonHandSocket>();
            return handSocket != null && handSocket.IsConfigured;
        }

        static void RunSetup(bool logSuccess)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PetSummon] Phase 1 setup waits until Unity returns to Edit Mode.");
                return;
            }

            try
            {
                EnsureFolder(RootFolder);
                EnsureFolder(PrefabFolder);
                EnsureFolder(MaterialFolder);

                Material ballMaterial = EnsureBallMaterial();
                GameObject ballPrefab = EnsureBallPrefab(ballMaterial);
                ConfigurePlayerPrefab(ballPrefab);
                AssetDatabase.SaveAssets();

                if (logSuccess)
                {
                    Debug.Log("[PetSummon] Phase 1 ready: BallGreenClose prefab and Boy/Vexa hand sockets are configured.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    throw;
                }
            }
        }

        static Material EnsureBallMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(BallMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }

                if (shader == null)
                {
                    throw new InvalidOperationException("No compatible Lit shader was found for BallGreenClose.");
                }

                material = new Material(shader) { name = "BallGreenClose" };
                AssetDatabase.CreateAsset(material, BallMaterialPath);
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D metallic = EnsurePackedMetallicSmoothness();

            SetTextureIfSupported(material, "_BaseMap", baseColor);
            SetTextureIfSupported(material, "_MainTex", baseColor);
            SetTextureIfSupported(material, "_BumpMap", normal);
            SetTextureIfSupported(material, "_MetallicGlossMap", metallic);
            SetFloatIfSupported(material, "_Metallic", 0.85f);
            SetFloatIfSupported(material, "_Smoothness", 0.68f);
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Texture2D EnsurePackedMetallicSmoothness()
        {
            string metallicFullPath = AssetPathToFullPath(MetallicPath);
            string roughnessFullPath = AssetPathToFullPath(RoughnessPath);
            string packedFullPath = AssetPathToFullPath(PackedMetallicSmoothnessPath);
            if (!File.Exists(metallicFullPath) || !File.Exists(roughnessFullPath))
            {
                return AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
            }

            bool needsRefresh = !File.Exists(packedFullPath)
                || File.GetLastWriteTimeUtc(packedFullPath) < File.GetLastWriteTimeUtc(metallicFullPath)
                || File.GetLastWriteTimeUtc(packedFullPath) < File.GetLastWriteTimeUtc(roughnessFullPath);
            if (needsRefresh)
            {
                Texture2D metallicSource = LoadPng(metallicFullPath);
                Texture2D roughnessSource = LoadPng(roughnessFullPath);
                try
                {
                    int width = Mathf.Min(metallicSource.width, roughnessSource.width);
                    int height = Mathf.Min(metallicSource.height, roughnessSource.height);
                    Color[] metallicPixels = metallicSource.GetPixels(0, 0, width, height);
                    Color[] roughnessPixels = roughnessSource.GetPixels(0, 0, width, height);
                    Color[] packedPixels = new Color[metallicPixels.Length];
                    for (int i = 0; i < packedPixels.Length; i++)
                    {
                        float metallic = metallicPixels[i].grayscale;
                        float smoothness = 1f - roughnessPixels[i].grayscale;
                        packedPixels[i] = new Color(metallic, metallic, metallic, smoothness);
                    }

                    Texture2D packed = new Texture2D(width, height, TextureFormat.RGBA32, true, true);
                    packed.SetPixels(packedPixels);
                    packed.Apply(true, false);
                    File.WriteAllBytes(packedFullPath, packed.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(packed);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(metallicSource);
                    UnityEngine.Object.DestroyImmediate(roughnessSource);
                }

                AssetDatabase.ImportAsset(PackedMetallicSmoothnessPath, ImportAssetOptions.ForceSynchronousImport);
                TextureImporter importer = AssetImporter.GetAtPath(PackedMetallicSmoothnessPath) as TextureImporter;
                if (importer != null)
                {
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.SaveAndReimport();
                }
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(PackedMetallicSmoothnessPath);
        }

        static string AssetPathToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException("Unity project root could not be resolved.");
            }

            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static Texture2D LoadPng(string fullPath)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!texture.LoadImage(File.ReadAllBytes(fullPath), false))
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException("Could not read texture " + fullPath);
            }

            return texture;
        }

        static GameObject EnsureBallPrefab(Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
            if (existing != null)
            {
                return existing;
            }

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BallModelPath);
            if (modelAsset == null)
            {
                throw new InvalidOperationException("BallGreenClose FBX was not found at " + BallModelPath);
            }

            GameObject root = new GameObject("BallGreenClose");
            try
            {
                GameObject visual = PrefabUtility.InstantiatePrefab(modelAsset) as GameObject;
                if (visual == null)
                {
                    throw new InvalidOperationException("BallGreenClose FBX could not be instantiated.");
                }

                visual.name = "Visual";
                visual.transform.SetParent(root.transform, false);
                ApplyMaterial(visual, material);
                NormalizeVisual(root.transform, visual.transform);

                SphereCollider collider = root.AddComponent<SphereCollider>();
                collider.radius = 0.25f;
                collider.isTrigger = false;

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = 0.25f;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.12f;
                body.useGravity = false;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, BallPrefabPath);
                if (saved == null)
                {
                    throw new InvalidOperationException("Unity could not save " + BallPrefabPath);
                }

                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void ConfigurePlayerPrefab(GameObject ballPrefab)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerVisualSwitcher switcher = root.GetComponent<PlayerVisualSwitcher>();
                if (switcher == null)
                {
                    throw new InvalidOperationException("Player_GenderSwitch is missing PlayerVisualSwitcher.");
                }

                Transform vexaHand = FindDescendant(root.transform, "Base HumanRPalm");
                Transform boyHand = FindDescendant(root.transform, "RightHand");
                if (vexaHand == null || boyHand == null)
                {
                    throw new InvalidOperationException(
                        $"Hand bones were not found. Vexa={(vexaHand != null)}, Boy={(boyHand != null)}");
                }

                Transform vexaSocket = EnsureSocket(vexaHand, new Vector3(-0.06f, -0.02f, -0.05f));
                Transform boySocket = EnsureSocket(boyHand, new Vector3(-0.03f, 0.08f, 0.04f));

                PetSummonHandSocket handSocket = root.GetComponent<PetSummonHandSocket>();
                if (handSocket == null)
                {
                    handSocket = root.AddComponent<PetSummonHandSocket>();
                }

                handSocket.Configure(
                    switcher,
                    ballPrefab,
                    new[]
                    {
                        new PetSummonHandSocket.VisualBinding(
                            "Vexa", vexaSocket, Vector3.zero,
                            Vector3.zero, 0.28f),
                        new PetSummonHandSocket.VisualBinding(
                            "Boy", boySocket, Vector3.zero,
                            Vector3.zero, 0.28f)
                    });
                EditorUtility.SetDirty(handSocket);

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Transform EnsureSocket(Transform hand, Vector3 initialLocalPosition)
        {
            Transform socket = hand.Find("BallSocket_PetSummon");
            if (socket == null)
            {
                GameObject socketObject = new GameObject("BallSocket_PetSummon");
                socket = socketObject.transform;
                socket.SetParent(hand, false);
                socket.localPosition = initialLocalPosition;
                socket.localRotation = Quaternion.identity;
                socket.localScale = Vector3.one;
            }

            return socket;
        }

        static Transform FindDescendant(Transform root, string exactName)
        {
            Transform[] candidates = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(candidates[i].name, exactName, StringComparison.Ordinal))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        static void ApplyMaterial(GameObject visual, Material material)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                {
                    materials[j] = material;
                }

                renderers[i].sharedMaterials = materials;
            }
        }

        static void NormalizeVisual(Transform root, Transform visual)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float diameter = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (diameter > 0.0001f)
            {
                visual.localScale = Vector3.one / diameter;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 centerOffset = root.InverseTransformPoint(bounds.center);
            visual.localPosition -= centerOffset;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string name = path.Substring(path.LastIndexOf('/') + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        static void SetTextureIfSupported(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
            {
                material.SetTexture(property, texture);
            }
        }

        static void SetFloatIfSupported(Material material, string property, float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }
    }
}
