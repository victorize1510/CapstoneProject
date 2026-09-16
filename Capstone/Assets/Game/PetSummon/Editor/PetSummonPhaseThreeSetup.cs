using System;
using UnityEditor;
using UnityEngine;

namespace Capstone.Game.PetSummon.Editor
{
    [InitializeOnLoad]
    public static class PetSummonPhaseThreeSetup
    {
        static readonly string[] PlayerPrefabPaths =
        {
            "Assets/Prefabs/Thắng/Player_GenderSwitch.prefab",
            "Assets/Prefabs/Thắng/Vexa/Vefects_Vexa.prefab"
        };

        static bool attemptedThisDomain;

        static PetSummonPhaseThreeSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
        }

        [MenuItem("Tools/Capstone/Pet Summon/Run Phase 3 Setup")]
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
            for (int i = 0; i < PlayerPrefabPaths.Length; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPaths[i]);
                PetSummonDirector director = prefab != null
                    ? prefab.GetComponent<PetSummonDirector>()
                    : null;
                PetSummonReleaseEffect effect = prefab != null
                    ? prefab.GetComponent<PetSummonReleaseEffect>()
                    : null;
                if (director == null || effect == null || director.ReleaseEffect != effect)
                {
                    return false;
                }
            }

            return true;
        }

        static void RunSetup(bool logSuccess)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[PetSummon] Phase 3 setup waits until Unity returns to Edit Mode.");
                return;
            }

            try
            {
                for (int i = 0; i < PlayerPrefabPaths.Length; i++)
                {
                    ConfigurePrefab(PlayerPrefabPaths[i]);
                }

                AssetDatabase.SaveAssets();
                if (logSuccess)
                {
                    Debug.Log(
                        "[PetSummon] Phase 3 ready: slot pet release, light beam, particles and reveal distortion are configured.");
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

        static void ConfigurePrefab(string prefabPath)
        {
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    throw new InvalidOperationException("Player prefab was not found at " + prefabPath);
                }

                PetSummonDirector director = root.GetComponent<PetSummonDirector>();
                if (director == null)
                {
                    throw new InvalidOperationException(
                        prefabPath + " is missing PetSummonDirector. Run Phase 2 setup first.");
                }

                PetSummonReleaseEffect effect = root.GetComponent<PetSummonReleaseEffect>();
                if (effect == null)
                {
                    effect = root.AddComponent<PetSummonReleaseEffect>();
                }

                director.ConfigureReleaseEffect(effect);
                EditorUtility.SetDirty(effect);
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
