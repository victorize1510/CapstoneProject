using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Capstone.Game.PetSummon;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Explicit installation of the supplied Generic girl rig. Never runs on domain reload.</summary>
public static class GirlFinalPlayerSetup
{
    public const string ModelPath = "Assets/ThangTest/Model&/girl test/girl simple/Source/GirlFinal.fbx";
    public const string AnimationFolder = "Assets/ThangTest/Model&/GIRLANIMCUOI";
    public const string RuntimeFolder = "Assets/ThangTest/Model&/girl test/girl simple/Runtime";
    public const string ControllerPath = RuntimeFolder + "/GirlFinal.controller";
    public const string PlayerPrefabPath = "Assets/Prefabs/Thắng/Player.prefab";
    public const string ScenePath = "Assets/Scenes/TestChar.unity";
    public const string BoyControllerPath = "Assets/Animations/Thắng/Boy/BoyBasic.controller";
    public static readonly Vector3 GirlPosition = new Vector3(0f, -0.0102f, 0f);
    public static readonly Vector3 BoyPosition = new Vector3(0f, -0.0165f, 0f);
    // FBX units are centimetres represented by a 100x Armature. Socket offsets are in bone units.
    public static readonly Vector3 GirlSocketPosition = new Vector3(-0.0003f, 0.0008f, 0.0004f);
    public const int GirlThrowReleaseFrame = 46;

    static readonly string[] ClipFiles = {
        "GirlFinal@Idle.fbx", "GirlFinal@Sad Idle.fbx", "GirlFinal@Dwarf Idle.fbx",
        "GirlFinal@@Running.fbx", "GirlFinal@Running.fbx", "GirlFinal@Jump.fbx",
        "GirlFinal@Picking Up Object.fbx", "GirlFinal@Throw.fbx"
    };

    [MenuItem("Tools/ToolCuaThang/Capstone/Player/Install GirlFinal in TestChar %&F8", priority = 1)]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Stop Play Mode before installing GirlFinal.");
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var players = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BasicPlayerMovement>(true)).ToArray();
            if (players.Length != 1) throw new InvalidOperationException("TestChar must have exactly one gameplay player.");
            var player = players[0].gameObject;
            Undo.RegisterFullObjectHierarchyUndo(player, "Replace Vexa with GirlFinal");
            ConfigurePlayer(player, AssetDatabase.LoadAssetAtPath<AnimatorController>(BoyControllerPath));
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save TestChar.");
            UpdatePlayerPrefab();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[GirlFinal] Installed Player > Boy / GirlFinal. Generic source animations; G switches models. SampleScene untouched.");
        }
        finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
    }

    public static bool IsConfiguredPlayer(GameObject player)
    {
        return player != null && player.transform.Find("GirlFinal") != null;
    }

    public static bool NeedsSetup(BasicPlayerMovement movement)
    {
        var player = movement.gameObject;
        var switcher = player.GetComponent<PlayerVisualSwitcher>();
        return player.name != "Player" || player.GetComponent<Animator>() != null
            || switcher == null || !switcher.TryGetVisualProfile(0, out var girl)
            || girl.root != player.transform.Find("GirlFinal").gameObject
            || girl.animator == null || girl.animator.transform != girl.root.transform.Find("Armature")
            || girl.controller != AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath)
            || !switcher.TryGetVisualProfile(1, out var boy) || boy.root == null || boy.animator == null;
    }

    public static void UpdatePlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            ConfigurePlayer(root, AssetDatabase.LoadAssetAtPath<AnimatorController>(BoyControllerPath));
            if (PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath) == null)
                throw new IOException("Could not save the shared player prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ConfigurePlayer(GameObject player, AnimatorController boyController)
    {
        if (player.scene.path == "Assets/Scenes/SampleScene.unity") return;
        var girlController = EnsureController();
        if (boyController == null) throw new InvalidOperationException("Missing existing Boy controller.");
        if (PrefabUtility.IsPartOfPrefabInstance(player) && PrefabUtility.GetOutermostPrefabInstanceRoot(player) == player)
            PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        var movement = player.GetComponent<BasicPlayerMovement>();
        if (movement == null) movement = player.AddComponent<BasicPlayerMovement>();
        var boy = PlayerGenderSwitcherSetup.FindOrCreateBoyVisual(player.transform, true);
        var girl = player.transform.Find("GirlFinal");
        if (girl == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (source == null) throw new InvalidOperationException("Missing " + ModelPath);
            girl = ((GameObject)PrefabUtility.InstantiatePrefab(source, player.transform)).transform;
            girl.name = "GirlFinal";
        }
        girl.localPosition = GirlPosition;
        girl.localRotation = Quaternion.identity;
        girl.localScale = Vector3.one;
        boy.transform.localPosition = BoyPosition;
        var armature = girl.Find("Armature");
        if (armature == null || armature.Find("Hips") == null)
            throw new InvalidOperationException("GirlFinal needs its original Armature/Hips hierarchy.");
        foreach (var oldAnimator in girl.GetComponentsInChildren<Animator>(true))
            if (oldAnimator.transform != armature) Object.DestroyImmediate(oldAnimator);
        var girlAnimator = armature.GetComponent<Animator>();
        if (girlAnimator == null) girlAnimator = armature.gameObject.AddComponent<Animator>();
        girlAnimator.avatar = null; // Exact Generic transform bindings, evaluated relative to Armature.
        girlAnimator.runtimeAnimatorController = girlController;
        girlAnimator.applyRootMotion = false;
        girlAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var boyAnimator = boy.GetComponentInChildren<Animator>(true);
        if (boyAnimator == null) throw new InvalidOperationException("Boy is missing Animator.");
        boyAnimator.runtimeAnimatorController = boyController;
        boyAnimator.applyRootMotion = false;
        ApplyGirlMaterial(girl);
        ValidateBindings(armature, girlController);

        // Remove the two legacy visual branches only; gameplay objects and map markers stay on Player.
        foreach (var name in new[] { "Armature", "Vefects_Vexa" })
        {
            var legacy = player.transform.Find(name);
            if (legacy != null) Object.DestroyImmediate(legacy.gameObject);
        }
        var rootAnimator = player.GetComponent<Animator>();
        if (rootAnimator != null) Object.DestroyImmediate(rootAnimator);
        player.name = "Player";
        player.tag = "Player";
        var switcher = player.GetComponent<PlayerVisualSwitcher>();
        if (switcher == null) switcher = player.AddComponent<PlayerVisualSwitcher>();
        var serialized = new SerializedObject(switcher);
        serialized.FindProperty("movement").objectReferenceValue = movement;
        serialized.FindProperty("allowRuntimeSwitch").boolValue = true;
        serialized.FindProperty("switchKey").intValue = (int)KeyCode.G;
        serialized.FindProperty("defaultVisualIndex").intValue = 0;
        var visuals = serialized.FindProperty("visuals");
        visuals.arraySize = 2;
        ConfigureVisual(visuals.GetArrayElementAtIndex(0), girl.gameObject, girlAnimator, GirlPosition, GirlThrowReleaseFrame);
        ConfigureVisual(visuals.GetArrayElementAtIndex(1), boy, boyAnimator, BoyPosition, 0);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        movement.animator = girlAnimator;
        movement.enableJump = true;
        movement.enableCrouch = movement.enableRoll = false;
        movement.useRootMotion = movement.rootMotionDrivesLocomotion = movement.rootMotionDrivesRoll = movement.rootMotionDrivesJump = false;
        movement.applyAnimatorRootYaw = movement.applyAnimatorRootRotationToVisual = movement.keepAnimatorTransformPinned = false;
        movement.useAnimatorStateLengthForActions = true;
        girl.gameObject.SetActive(true);
        girlAnimator.enabled = true;
        boy.SetActive(false);
        ConfigureHandSockets(player);
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(movement);
    }

    static void ConfigureVisual(SerializedProperty profile, GameObject model, Animator animator, Vector3 position, int throwReleaseFrame)
    {
        profile.FindPropertyRelative("displayName").stringValue = model.name;
        profile.FindPropertyRelative("root").objectReferenceValue = model;
        profile.FindPropertyRelative("animator").objectReferenceValue = animator;
        profile.FindPropertyRelative("controller").objectReferenceValue = animator.runtimeAnimatorController;
        profile.FindPropertyRelative("animatorSpeed").floatValue = 1f;
        profile.FindPropertyRelative("throwReleaseFrame").intValue = throwReleaseFrame;
        profile.FindPropertyRelative("localPosition").vector3Value = position;
        profile.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
        profile.FindPropertyRelative("localScale").vector3Value = Vector3.one;
        profile.FindPropertyRelative("enableJump").boolValue = true;
        profile.FindPropertyRelative("enableCrouch").boolValue = false;
        profile.FindPropertyRelative("enableRoll").boolValue = false;
    }

    public static void ConfigureHandSockets(GameObject player)
    {
        var switcher = player.GetComponent<PlayerVisualSwitcher>();
        var socket = player.GetComponent<PetSummonHandSocket>();
        if (switcher == null || socket == null || socket.BallPrefab == null)
            throw new InvalidOperationException("Shared Player needs its existing summon hand component and ball prefab.");
        var bindings = new List<PetSummonHandSocket.VisualBinding>();
        for (int i = 0; i < 2; i++)
        {
            if (!switcher.TryGetVisualProfile(i, out var profile)) throw new InvalidOperationException("Missing player visual " + i);
            var hand = profile.root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "RightHand");
            var anchor = hand.Find("BallSocket_PetSummon");
            if (anchor == null) { anchor = new GameObject("BallSocket_PetSummon").transform; anchor.SetParent(hand, false); }
            anchor.localPosition = i == 0 ? GirlSocketPosition : new Vector3(-0.03f, 0.08f, 0.04f);
            anchor.localRotation = Quaternion.identity;
            anchor.localScale = Vector3.one;
            bindings.Add(new PetSummonHandSocket.VisualBinding(profile.displayName, anchor, Vector3.zero, Vector3.zero, 0.28f));
        }
        socket.Configure(switcher, socket.BallPrefab, bindings.ToArray());
        EditorUtility.SetDirty(socket);
    }

    static void ApplyGirlMaterial(Transform girl)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/ThangTest/Model&/girl test/girl simple/Girl Humanoid.mat");
        if (material == null) throw new InvalidOperationException("Missing GirlFinal material.");
        foreach (var renderer in girl.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMesh.subMeshCount).ToArray();
    }

    public static void ValidateBindings(Transform animationRoot, RuntimeAnimatorController controller)
    {
        foreach (var clip in controller.animationClips.Distinct())
        {
            if (clip.humanMotion) throw new InvalidOperationException("GirlFinal clip unexpectedly uses Humanoid: " + clip.name);
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                if (binding.path.Length > 0 && animationRoot.Find(binding.path) == null)
                    throw new InvalidOperationException(clip.name + " has no matching bone at " + binding.path);
        }
    }

    public static AnimatorController EnsureController()
    {
        EnsureFolder(RuntimeFolder);
        var clips = new List<AnimationClip>();
        foreach (var filename in ClipFiles)
        {
            string path = AnimationFolder + "/" + filename;
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing supplied animation: " + path);
            bool loop = !filename.Contains("Jump") && !filename.Contains("Throw") && !filename.Contains("Picking");
            var settings = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            bool changed = importer.animationType != ModelImporterAnimationType.Generic || !importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            foreach (var setting in settings)
            {
                changed |= setting.loopTime != loop || setting.loopPose || !setting.lockRootRotation
                    || !setting.lockRootPositionXZ || !setting.keepOriginalOrientation || !setting.keepOriginalPositionXZ;
                setting.loopTime = loop;
                setting.loopPose = false;
                setting.lockRootRotation = true;
                setting.lockRootPositionXZ = true;
                setting.keepOriginalOrientation = true;
                setting.keepOriginalPositionXZ = true;
            }
            if (changed) { importer.clipAnimations = settings; importer.SaveAndReimport(); }
            clips.Add(AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__")));
        }
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
            ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var machine = controller.layers[0].stateMachine;
        var states = new Dictionary<string, AnimationClip> {
            {"IdleNeutral",clips[0]}, {"IdleSad",clips[1]}, {"IdleHappy",clips[0]}, {"IdleBattle",clips[2]},
            {"SlowRun",clips[3]}, {"Sprint",clips[4]}, {"RunningBackward",clips[3]},
            {"Floating",clips[5]}, {"IdleJump",clips[5]}, {"Jump",clips[5]}, {"PickingUp",clips[6]}, {"Throw",clips[7]}
        };
        var parameters = new Dictionary<string, AnimatorControllerParameterType> {
            {"Speed",AnimatorControllerParameterType.Float}, {"MoveX",AnimatorControllerParameterType.Float}, {"MoveY",AnimatorControllerParameterType.Float},
            {"Grounded",AnimatorControllerParameterType.Bool}, {"VerticalSpeed",AnimatorControllerParameterType.Float},
            {"IsMoving",AnimatorControllerParameterType.Bool}, {"IsSprinting",AnimatorControllerParameterType.Bool},
            {"IsCrouching",AnimatorControllerParameterType.Bool}, {"IsEnemyNearby",AnimatorControllerParameterType.Bool},
            {"IsRolling",AnimatorControllerParameterType.Bool}, {"IsJumping",AnimatorControllerParameterType.Bool},
            {"IsPickingUp",AnimatorControllerParameterType.Bool}, {"IsThrowing",AnimatorControllerParameterType.Bool}, {"IdleTimer",AnimatorControllerParameterType.Float}
        };
        foreach (var parameter in parameters)
            if (!controller.parameters.Any(p => p.name == parameter.Key)) controller.AddParameter(parameter.Key, parameter.Value);
        int index = 0;
        foreach (var pair in states)
        {
            var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == pair.Key)
                ?? machine.AddState(pair.Key, new Vector3(220 + index / 4 * 260, 80 + index % 4 * 100));
            if (state.motion != pair.Value) { state.motion = pair.Value; EditorUtility.SetDirty(state); }
            state.speed = 1f;
            index++;
        }
        machine.defaultState = machine.states.First(s => s.state.name == "IdleNeutral").state;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
