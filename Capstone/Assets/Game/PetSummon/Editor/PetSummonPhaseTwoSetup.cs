using System;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.PetSummon.Editor
{
    [InitializeOnLoad]
    public static class PetSummonPhaseTwoSetup
    {
        const string PlayerPrefabPath = "Assets/Prefabs/Thắng/Player_GenderSwitch.prefab";
        const string LegacyVexaPrefabPath = "Assets/Prefabs/Thắng/Vexa/Vefects_Vexa.prefab";
        const string BallPrefabPath = "Assets/Game/PetSummon/Prefabs/BallGreenClose.prefab";
        static bool attemptedThisDomain;

        static PetSummonPhaseTwoSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/Capstone/Pet Summon/Run Phase 2 Setup")]
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
            GameObject legacyVexaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyVexaPrefabPath);
            return HasBallPhysics(ballPrefab)
                && HasPhaseTwoComponents(playerPrefab, true)
                && HasPhaseTwoComponents(legacyVexaPrefab, false);
        }

        static void RunSetup(bool logSuccess)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PetSummon] Phase 2 setup waits until Unity returns to Edit Mode.");
                return;
            }

            try
            {
                ConfigureBallPrefab();
                GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
                if (ballPrefab == null)
                {
                    throw new InvalidOperationException(
                        "BallGreenClose prefab is missing. Run Phase 1 setup first.");
                }

                ConfigurePlayerPrefab(PlayerPrefabPath, ballPrefab, true);
                ConfigurePlayerPrefab(LegacyVexaPrefabPath, ballPrefab, false);
                AssetDatabase.SaveAssets();

                if (logSuccess)
                {
                    Debug.Log("[PetSummon] Phase 2 ready: Throw, flight, one bounce and hover are configured.");
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

        static bool HasPhaseTwoComponents(GameObject prefab, bool requireFullHandBindings)
        {
            if (prefab == null || prefab.GetComponent<PetSummonDirector>() == null)
            {
                return false;
            }

            PetSummonHandSocket handSocket = prefab.GetComponent<PetSummonHandSocket>();
            return handSocket != null && handSocket.BallPrefab != null
                && (!requireFullHandBindings || handSocket.BindingCount >= 2);
        }

        static bool HasBallPhysics(GameObject prefab)
        {
            if (prefab == null || prefab.GetComponent<Rigidbody>() == null
                || prefab.GetComponent<PetSummonBallProjectile>() == null)
            {
                return false;
            }

            Collider[] colliders = prefab.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && !colliders[i].isTrigger)
                {
                    return true;
                }
            }

            return false;
        }

        static void ConfigureBallPrefab()
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(BallPrefabPath);
                if (root == null)
                {
                    throw new InvalidOperationException(
                        "BallGreenClose prefab is missing. Run Phase 1 setup first.");
                }

                Rigidbody body = root.GetComponent<Rigidbody>();
                if (body == null)
                {
                    body = root.AddComponent<Rigidbody>();
                }

                body.mass = 0.25f;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.12f;
                body.useGravity = false;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;

                Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0)
                {
                    SphereCollider sphere = root.AddComponent<SphereCollider>();
                    sphere.radius = 0.25f;
                    colliders = new Collider[] { sphere };
                }

                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].isTrigger = false;
                    }
                }

                if (root.GetComponent<PetSummonBallProjectile>() == null)
                {
                    root.AddComponent<PetSummonBallProjectile>();
                }

                EditorUtility.SetDirty(root);
                PrefabUtility.SaveAsPrefabAsset(root, BallPrefabPath);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        static void ConfigurePlayerPrefab(string prefabPath, GameObject ballPrefab,
            bool requireFullHandBindings)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    throw new InvalidOperationException("Player prefab was not found at " + prefabPath);
                }

                BasicPlayerMovement movement = root.GetComponent<BasicPlayerMovement>();
                PlayerVisualSwitcher switcher = root.GetComponent<PlayerVisualSwitcher>();
                PetSummonHandSocket handSocket = root.GetComponent<PetSummonHandSocket>();
                if (movement == null)
                {
                    throw new InvalidOperationException(prefabPath + " is missing BasicPlayerMovement.");
                }

                if (requireFullHandBindings
                    && (switcher == null || handSocket == null || handSocket.BindingCount < 2))
                {
                    throw new InvalidOperationException(
                        "Phase 1 references are missing on Player_GenderSwitch. Run Phase 1 setup first.");
                }

                if (handSocket == null)
                {
                    handSocket = root.AddComponent<PetSummonHandSocket>();
                }

                if (!requireFullHandBindings && handSocket.BallPrefab == null)
                {
                    handSocket.Configure(switcher, ballPrefab,
                        Array.Empty<PetSummonHandSocket.VisualBinding>());
                    EditorUtility.SetDirty(handSocket);
                }

                PetSummonDirector director = root.GetComponent<PetSummonDirector>();
                if (director == null)
                {
                    director = root.AddComponent<PetSummonDirector>();
                }

                director.Configure(movement, switcher, handSocket);
                EditorUtility.SetDirty(director);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }
    }
}
