using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerGenderSwitcherSetup
{
    private const string PlayerMenuRoot = "Tools/ToolCuaThang/Capstone/Player/";
    private const string MenuRoot = PlayerMenuRoot + "Advanced/";
    private const string VexaPrefabPath = "Assets/Prefabs/Th\u1EAFng/Vexa/Vefects_Vexa.prefab";
    private const string BoyModelPath = "Assets/Prefabs/Th\u1EAFng/BoyFantasyBeast/Meshy_AI_Kael_Ashborne_biped/Meshy_AI_Kael_Ashborne_biped_Character_output.fbx";
    private const string BoyAnimationsPath = "Assets/Prefabs/Th\u1EAFng/BoyFantasyBeast/Meshy_AI_Kael_Ashborne_biped/Meshy_AI_Kael_Ashborne_biped_Meshy_AI_Meshy_Merged_Animations.fbx";
    private const string BoyAnimationsFolder = "Assets/Animations/Th\u1EAFng/Boy";
    private const string BoyIdlePath = "Assets/Animations/Th\u1EAFng/Boy/IdleBoy.fbx";
    private const string BoyIdleSadPath = "Assets/Animations/Th\u1EAFng/Boy/IdleSad_Boy.fbx";
    private const string BoyIdleHappyPath = "Assets/Animations/Th\u1EAFng/Boy/IdleHappy.fbx";
    private const string BoyJumpPath = "Assets/Animations/Th\u1EAFng/Boy/Jump_Boy.fbx";
    private const string BoyPickUpPath = "Assets/Animations/Th\u1EAFng/Boy/PickupBOY.fbx";
    private const string BoyThrowPath = "Assets/Animations/Th\u1EAFng/Boy/Throw_boy.fbx";
    private const string OutputPrefabPath = "Assets/Prefabs/Th\u1EAFng/Player_GenderSwitch.prefab";
    private const string BoyControllerPath = "Assets/Animations/Th\u1EAFng/Boy/BoyBasic.controller";
    private const string BoyRetargetedFolder = "Assets/Animations/Th\u1EAFng/Boy/RuntimeFixed";
    private const float BoyAnimatorSpeed = 1f;
    private const float BoyRunStateSpeed = 2f;
    private static readonly Vector3 DefaultVisualPosition = Vector3.zero;
    private static readonly Vector3 DefaultVisualEulerAngles = Vector3.zero;
    private static readonly Vector3 DefaultBoyVisualEulerAngles = Vector3.zero;
    private static readonly Vector3 DefaultVisualScale = Vector3.one;

    [MenuItem(PlayerMenuRoot + "Setup All Player", priority = 0)]
    public static void SetupAllPlayer()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        EnsureFolders();
        CapstonePlayerSetup.RepairPlayerControllerSetup();
        AnimatorController boyController = EnsureBoyAnimatorController();

        BasicPlayerMovement movement = GetSelectedOrScenePlayer();
        if (movement == null)
        {
            CapstonePlayerSetup.CreateVexaBasicPlayer();
            movement = GetSelectedOrScenePlayer();
        }

        if (movement == null)
        {
            EditorUtility.DisplayDialog("Setup All Player", "Cannot find or create a Player in the open scene.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(movement.gameObject, "Setup all player visuals");
        SetupPlayer(movement.gameObject, boyController, false);
        EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        Selection.activeGameObject = movement.gameObject;

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Setup All Player",
            "Player setup finished.\n\nIncluded: Vexa setup, BOY Mixamo repair, gender switch with G, jump enabled for Vexa and BOY, roll/crouch disabled.",
            "OK");
    }
    [MenuItem(MenuRoot + "Create/Update Gender Switch Player Prefab")]
    public static void CreateOrUpdateGenderSwitchPrefab()
    {
        CreateOrUpdateGenderSwitchPrefabInternal(true);
    }

    public static void CreateOrUpdateGenderSwitchPrefabSilent()
    {
        CreateOrUpdateGenderSwitchPrefabInternal(false);
    }

    private static bool CreateOrUpdateGenderSwitchPrefabInternal(bool showDialog)
    {
        GameObject vexaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VexaPrefabPath);
        if (vexaPrefab == null)
        {
            string message = "Cannot find:\n" + VexaPrefabPath;
            Debug.LogError(message);
            if (showDialog)
            {
                EditorUtility.DisplayDialog("Missing Vexa prefab", message, "OK");
            }

            return false;
        }

        EnsureFolders();
        AnimatorController boyController = EnsureBoyAnimatorController();
        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(VexaPrefabPath);
        try
        {
            prefabRoot.name = "Player_GenderSwitch";
            SetupPlayer(prefabRoot, boyController, true);
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, OutputPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string successMessage = "Created/updated:\n" + OutputPrefabPath + "\n\nPress G at runtime to switch Vexa/Boy.";
        Debug.Log(successMessage);
        if (showDialog)
        {
            EditorUtility.DisplayDialog("Gender Switch Player", successMessage, "OK");
        }

        return true;
    }

    [InitializeOnLoadMethod]
    private static void AutoRepairBoyControllerAfterReload()
    {
        EditorApplication.delayCall += AutoRepairBoyControllerOnce;
    }

    private static void AutoRepairBoyControllerOnce()
    {
        const string sessionKey = "Capstone.BoyRuntimeFixedClips.Generated";
        if (SessionState.GetBool(sessionKey, false))
        {
            return;
        }

        SessionState.SetBool(sessionKey, true);
        if (!AssetDatabase.IsValidFolder(BoyAnimationsFolder))
        {
            return;
        }

        try
        {
            EnsureFolders();
            EnsureBoyAnimatorController();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("Could not auto-repair BOY animator clips: " + exception.Message);
        }
    }

    public static void RepairBoyMixamoAnimations()
    {
        EnsureFolders();
        EnsureBoyAnimatorController();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("BOY Mixamo animations repaired. RuntimeFixed clips were generated and BoyBasic.controller was updated.");
    }

    public static void SetupSelectedPlayer()
    {
        BasicPlayerMovement movement = GetSelectedOrScenePlayer();
        if (movement == null)
        {
            EditorUtility.DisplayDialog("No Player", "Select the Player object, or keep a BasicPlayerMovement object in the open scene.", "OK");
            return;
        }

        EnsureFolders();
        AnimatorController boyController = EnsureBoyAnimatorController();
        Undo.RegisterFullObjectHierarchyUndo(movement.gameObject, "Setup player gender switcher");
        SetupPlayer(movement.gameObject, boyController, false);
        EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        Selection.activeGameObject = movement.gameObject;
    }

    public static void SetupSelectedBoyAnimator()
    {
        if (Selection.activeTransform == null)
        {
            EditorUtility.DisplayDialog("No Boy Selected", "Select the Boy object, Armature, or any child bone/mesh under Boy.", "OK");
            return;
        }

        EnsureFolders();
        AnimatorController boyController = EnsureBoyAnimatorController();
        Transform boyRoot = FindBoyVisualRoot(Selection.activeTransform);
        Animator animator = boyRoot.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(boyRoot.gameObject);
        }

        Avatar boyAvatar = FindBoyAvatar();
        Undo.RecordObject(animator, "Setup selected Boy animator");
        if (boyAvatar != null)
        {
            animator.avatar = boyAvatar;
        }

        animator.runtimeAnimatorController = boyController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.speed = BoyAnimatorSpeed;

        EditorUtility.SetDirty(animator);
        EditorUtility.SetDirty(boyRoot.gameObject);
        if (boyRoot.gameObject.scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(boyRoot.gameObject.scene);
        }

        Selection.activeGameObject = animator.gameObject;
        Debug.Log("Setup Boy animator: " + GetTransformPath(animator.transform) + " -> " + BoyControllerPath);
    }

    public static void ReimportBoyAsExactGenericRig()
    {
        EnsureFolders();
        ConfigureBoyImportSettings();
        EnsureBoyAnimatorController();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "Boy Rig Reimported",
            "BOY model and BOY animations were reimported as Generic using their exact Armature/Hips skeleton.\n\nUse this when Humanoid retarget makes the body look loose or puppet-like.",
            "OK");
    }

    private static Transform FindBoyVisualRoot(Transform selected)
    {
        Transform current = selected;
        while (current.parent != null)
        {
            if (current.name.Equals("Boy", System.StringComparison.OrdinalIgnoreCase)
                || current.name.IndexOf("Meshy_AI_Kael", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return current;
            }

            current = current.parent;
        }

        return selected.root;
    }

    private static string GetTransformPath(Transform transform)
    {
        string path = transform.name;
        Transform current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static BasicPlayerMovement GetSelectedOrScenePlayer()
    {
        if (Selection.activeGameObject != null)
        {
            BasicPlayerMovement selected = Selection.activeGameObject.GetComponentInParent<BasicPlayerMovement>();
            if (selected != null)
            {
                return selected;
            }
        }

        BasicPlayerMovement[] players = Object.FindObjectsByType<BasicPlayerMovement>(FindObjectsSortMode.None);
        return players != null && players.Length > 0 ? players[0] : null;
    }

    private static void SetupPlayer(GameObject player, AnimatorController boyController, bool prefabAssetMode)
    {
        BasicPlayerMovement movement = player.GetComponent<BasicPlayerMovement>();
        if (movement == null)
        {
            movement = prefabAssetMode ? player.AddComponent<BasicPlayerMovement>() : Undo.AddComponent<BasicPlayerMovement>(player);
        }

        Animator vexaAnimator = movement.animator != null ? movement.animator : FindBestAnimator(player.transform);
        GameObject boy = FindOrCreateBoyVisual(player.transform, prefabAssetMode);
        Animator boyAnimator = boy.GetComponent<Animator>();
        if (boyAnimator == null)
        {
            boyAnimator = boy.GetComponentInChildren<Animator>(true);
        }

        if (boyAnimator == null)
        {
            boyAnimator = prefabAssetMode ? boy.AddComponent<Animator>() : Undo.AddComponent<Animator>(boy);
        }

        Avatar boyAvatar = FindBoyAvatar();
        if (boyAvatar != null)
        {
            boyAnimator.avatar = boyAvatar;
        }

        boyAnimator.runtimeAnimatorController = boyController;
        boyAnimator.applyRootMotion = false;
        boyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        boyAnimator.speed = BoyAnimatorSpeed;

        PlayerVisualSwitcher switcher = player.GetComponent<PlayerVisualSwitcher>();
        if (switcher == null)
        {
            switcher = prefabAssetMode ? player.AddComponent<PlayerVisualSwitcher>() : Undo.AddComponent<PlayerVisualSwitcher>(player);
        }

        SerializedObject serializedSwitcher = new SerializedObject(switcher);
        serializedSwitcher.FindProperty("movement").objectReferenceValue = movement;
        serializedSwitcher.FindProperty("allowRuntimeSwitch").boolValue = true;
        serializedSwitcher.FindProperty("switchKey").intValue = (int)KeyCode.G;
        serializedSwitcher.FindProperty("defaultVisualIndex").intValue = 0;

        SerializedProperty visuals = serializedSwitcher.FindProperty("visuals");
        visuals.arraySize = 2;
        ConfigureVisual(visuals.GetArrayElementAtIndex(0), "Vexa", player, vexaAnimator, vexaAnimator != null ? vexaAnimator.runtimeAnimatorController : null, 1f, DefaultVisualPosition, DefaultVisualEulerAngles, DefaultVisualScale, true, false, false);
        ConfigureVisual(visuals.GetArrayElementAtIndex(1), "Boy", boy, boyAnimator, boyController, BoyAnimatorSpeed, DefaultVisualPosition, DefaultBoyVisualEulerAngles, DefaultVisualScale, true, false, false);
        serializedSwitcher.ApplyModifiedPropertiesWithoutUndo();

        movement.animator = vexaAnimator != null ? vexaAnimator : boyAnimator;
        movement.enableRoll = false;
        movement.enableJump = true;
        movement.enableCrouch = false;
        movement.useRootMotion = false;
        movement.rootMotionDrivesLocomotion = false;
        movement.rootMotionDrivesRoll = false;
        movement.rootMotionDrivesJump = false;
        movement.applyAnimatorRootYaw = false;
        movement.applyAnimatorRootRotationToVisual = false;
        movement.keepAnimatorTransformPinned = false;

        boy.SetActive(false);
        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(switcher);
        EditorUtility.SetDirty(movement);
        EditorUtility.SetDirty(boy);
    }

    private static void ConfigureVisual(SerializedProperty element, string displayName, GameObject root, Animator animator, RuntimeAnimatorController controller, float speed, Vector3 localPosition, Vector3 localEulerAngles, Vector3 localScale, bool jump, bool crouch, bool roll)
    {
        element.FindPropertyRelative("displayName").stringValue = displayName;
        element.FindPropertyRelative("root").objectReferenceValue = root;
        element.FindPropertyRelative("animator").objectReferenceValue = animator;
        element.FindPropertyRelative("controller").objectReferenceValue = controller;
        element.FindPropertyRelative("animatorSpeed").floatValue = speed;
        SetVector3(element, "localPosition", localPosition);
        SetVector3(element, "localEulerAngles", localEulerAngles);
        SetVector3(element, "localScale", localScale);
        element.FindPropertyRelative("enableJump").boolValue = jump;
        element.FindPropertyRelative("enableCrouch").boolValue = crouch;
        element.FindPropertyRelative("enableRoll").boolValue = roll;
    }

    private static void SetVector3(SerializedProperty parent, string propertyName, Vector3 value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
        {
            property.vector3Value = value;
        }
    }

    private static GameObject FindOrCreateBoyVisual(Transform player, bool prefabAssetMode)
    {
        Transform existingChild = player.Find("Boy");
        if (existingChild != null)
        {
            NormalizeBoyTransform(existingChild);
            return existingChild.gameObject;
        }

        if (!prefabAssetMode)
        {
            GameObject sceneBoy = GameObject.Find("Boy");
            if (sceneBoy != null && !sceneBoy.transform.IsChildOf(player))
            {
                Undo.SetTransformParent(sceneBoy.transform, player, "Parent Boy visual to Player");
                NormalizeBoyTransform(sceneBoy.transform);
                return sceneBoy;
            }
        }

        GameObject boyModel = AssetDatabase.LoadAssetAtPath<GameObject>(BoyModelPath);
        if (boyModel == null)
        {
            throw new MissingReferenceException("Cannot find Boy model at " + BoyModelPath);
        }

        GameObject boy = PrefabUtility.InstantiatePrefab(boyModel, player) as GameObject;
        if (boy == null)
        {
            boy = Object.Instantiate(boyModel, player);
        }

        boy.name = "Boy";
        if (!prefabAssetMode)
        {
            Undo.RegisterCreatedObjectUndo(boy, "Create Boy visual");
        }

        NormalizeBoyTransform(boy.transform);
        return boy;
    }

    private static void NormalizeBoyTransform(Transform boy)
    {
        boy.localPosition = Vector3.zero;
        boy.localRotation = Quaternion.Euler(DefaultBoyVisualEulerAngles);
        boy.localScale = Vector3.one;
    }

    private static AnimatorController EnsureBoyAnimatorController()
    {
        ConfigureBoyImportSettings();
        EnsureFolders();

        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(BoyControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(BoyControllerPath);
        }

        AnimatorControllerLayer layer = controller.layers[0];
        AnimatorStateMachine stateMachine = layer.stateMachine;
        ClearStateMachine(stateMachine);

        AnimationClip idleClip = EnsureRetargetedBoyClip(BoyIdlePath, "IdleBoy", true);
        AnimationClip sadClip = EnsureRetargetedBoyClip(BoyIdleSadPath, "IdleSad_Boy", true);
        AnimationClip happyClip = EnsureRetargetedBoyClip(BoyIdleHappyPath, "IdleHappy", true);
        AnimationClip jumpClip = EnsureRetargetedBoyClip(BoyJumpPath, "Jump_Boy", false);
        AnimationClip pickUpClip = EnsureRetargetedBoyClip(BoyPickUpPath, "PickupBOY", false);
        AnimationClip throwClip = EnsureRetargetedBoyClip(BoyThrowPath, "Throw_boy", false);
        AnimationClip alertClip = FindClip(BoyAnimationsPath, "Alert");
        AnimationClip runClip = FindClip(BoyAnimationsPath, "Run_03", "Running", "Walking");
        AnimationClip sprintClip = FindClip(BoyAnimationsPath, "RunFast", "Sprint");

        idleClip = idleClip != null ? idleClip : alertClip;
        sadClip = sadClip != null ? sadClip : idleClip;
        happyClip = happyClip != null ? happyClip : idleClip;
        jumpClip = jumpClip != null ? jumpClip : idleClip;
        pickUpClip = pickUpClip != null ? pickUpClip : idleClip;
        throwClip = throwClip != null ? throwClip : idleClip;
        runClip = runClip != null ? runClip : idleClip;
        sprintClip = sprintClip != null ? sprintClip : runClip;

        AnimatorState idle = AddState(stateMachine, "IdleNeutral", idleClip, new Vector3(220f, 80f, 0f), 1f);
        AddState(stateMachine, "IdleBattle", alertClip != null ? alertClip : idleClip, new Vector3(220f, 190f, 0f), 1f);
        AddState(stateMachine, "IdleSad", sadClip, new Vector3(220f, 300f, 0f), 1f);
        AddState(stateMachine, "IdleHappy", happyClip, new Vector3(220f, 410f, 0f), 1f);
        AddState(stateMachine, "SlowRun", runClip, new Vector3(500f, 80f, 0f), BoyRunStateSpeed);
        AddState(stateMachine, "Sprint", sprintClip, new Vector3(500f, 190f, 0f), BoyRunStateSpeed);
        AddState(stateMachine, "RunningBackward", runClip, new Vector3(500f, 300f, 0f), BoyRunStateSpeed);
        AddState(stateMachine, "Floating", idleClip, new Vector3(500f, 410f, 0f), 1f);
        AddState(stateMachine, "IdleJump", jumpClip, new Vector3(780f, 80f, 0f), 1f);
        AddState(stateMachine, "Jump", jumpClip, new Vector3(780f, 190f, 0f), 1f);
        AddState(stateMachine, "PickingUp", pickUpClip, new Vector3(780f, 300f, 0f), 1f);
        AddState(stateMachine, "Throw", throwClip, new Vector3(780f, 410f, 0f), 1f);

        stateMachine.defaultState = idle;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 position, float speed)
    {
        AnimatorState state = stateMachine.AddState(name, position);
        state.motion = motion;
        state.speed = speed;
        state.writeDefaultValues = true;
        return state;
    }

    private static AnimationClip EnsureRetargetedBoyClip(string sourcePath, string outputName, bool loopTime)
    {
        AnimationClip source = FindClip(sourcePath, outputName);
        if (source == null)
        {
            Debug.LogWarning("Missing BOY source animation: " + sourcePath);
            return null;
        }

        EnsureFolders();
        string outputPath = BoyRetargetedFolder + "/" + outputName + "_BoyRig.anim";
        AnimationClip target = AssetDatabase.LoadAssetAtPath<AnimationClip>(outputPath);
        if (target == null)
        {
            target = new AnimationClip { name = outputName + "_BoyRig" };
            AssetDatabase.CreateAsset(target, outputPath);
        }

        target.ClearCurves();
        target.frameRate = source.frameRate;
        target.wrapMode = loopTime ? WrapMode.Loop : WrapMode.Once;

        AnimationUtility.SetAnimationEvents(target, AnimationUtility.GetAnimationEvents(source));
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.loopTime = loopTime;
        settings.loopBlend = loopTime;
        AnimationUtility.SetAnimationClipSettings(target, settings);

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            string remappedPath = RemapBoyAnimationPath(binding.path);
            if (remappedPath == null)
            {
                continue;
            }

            EditorCurveBinding remappedBinding = binding;
            remappedBinding.path = remappedPath;
            AnimationUtility.SetEditorCurve(target, remappedBinding, AnimationUtility.GetEditorCurve(source, binding));
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(source))
        {
            string remappedPath = RemapBoyAnimationPath(binding.path);
            if (remappedPath == null)
            {
                continue;
            }

            EditorCurveBinding remappedBinding = binding;
            remappedBinding.path = remappedPath;
            AnimationUtility.SetObjectReferenceCurve(target, remappedBinding, AnimationUtility.GetObjectReferenceCurve(source, binding));
        }

        EditorUtility.SetDirty(target);
        return target;
    }

    private static string RemapBoyAnimationPath(string sourcePath)
    {
        if (string.IsNullOrEmpty(sourcePath))
        {
            return null;
        }

        if (sourcePath == "Armature" || sourcePath.StartsWith("Armature/", System.StringComparison.Ordinal))
        {
            return sourcePath;
        }

        int hipsIndex = sourcePath.IndexOf("Hips", System.StringComparison.Ordinal);
        if (hipsIndex >= 0)
        {
            return "Armature/" + sourcePath.Substring(hipsIndex);
        }

        return "Armature/" + sourcePath;
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState state in stateMachine.states)
        {
            stateMachine.RemoveState(state.state);
        }

        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(child.stateMachine);
        }
    }

    private static AnimationClip FindClip(string assetPath, params string[] nameParts)
    {
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
        if (assets == null || assets.Length == 0)
        {
            assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        }

        AnimationClip firstClip = null;
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__"))
            {
                continue;
            }

            if (firstClip == null)
            {
                firstClip = clip;
            }

            if (nameParts == null || nameParts.Length == 0)
            {
                return clip;
            }

            for (int j = 0; j < nameParts.Length; j++)
            {
                if (clip.name.IndexOf(nameParts[j], System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return clip;
                }
            }
        }

        return firstClip;
    }

    private static AnimationClip FindBoyFolderClipByFileName(params string[] fileNameParts)
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { BoyAnimationsFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (ContainsAny(fileName, fileNameParts))
            {
                AnimationClip clip = FindClip(path);
                if (clip != null)
                {
                    return clip;
                }
            }
        }

        return null;
    }

    private static Avatar FindBoyAvatar()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(BoyModelPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Avatar avatar = assets[i] as Avatar;
            if (avatar != null && avatar.isValid)
            {
                return avatar;
            }
        }

        return null;
    }

    private static Animator FindBestAnimator(Transform root)
    {
        Animator rootAnimator = root.GetComponent<Animator>();
        return rootAnimator != null ? rootAnimator : root.GetComponentInChildren<Animator>(true);
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, name);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void ConfigureBoyImportSettings()
    {
        ConfigureBoyModelImporter(BoyModelPath);
        ConfigureAnimationImporter(BoyAnimationsPath, "Alert", "RunFast", "Run_03", "Running", "Walking", "Character_output");
        // Standalone Mixamo FBX files stay untouched; runtime .anim copies are retargeted to BOY's Armature root.
    }

    private static void ConfigureBoyModelImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        ConfigureExactBoyRig(importer, false, ref changed);
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static void ConfigureBoyFolderAnimationImports()
    {
        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { BoyAnimationsFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string clipName = System.IO.Path.GetFileNameWithoutExtension(path);
            ConfigureSingleClipAnimationImporter(path, clipName, ShouldLoopBoyClip(clipName));
        }
    }

    private static bool ShouldLoopBoyClip(string clipName)
    {
        return ContainsAny(clipName, "Idle", "Happy", "Sad", "Run", "Walk", "Sprint");
    }

    private static void ConfigureSingleClipAnimationImporter(string assetPath, string clipName, bool loopTime)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        ConfigureExactBoyRig(importer, true, ref changed);
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                string targetName = i == 0 ? clipName : clipName + "_" + i;
                if (clips[i].name != targetName)
                {
                    clips[i].name = targetName;
                    changed = true;
                }

                if (clips[i].loopTime != loopTime)
                {
                    clips[i].loopTime = loopTime;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            if (clips != null && clips.Length > 0)
            {
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
        }
    }

    private static void ConfigureAnimationImporter(string assetPath, params string[] loopNameParts)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        bool changed = false;
        ConfigureExactBoyRig(importer, true, ref changed);
        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                bool shouldLoop = ContainsAny(clips[i].name, loopNameParts);
                if (clips[i].loopTime != shouldLoop)
                {
                    clips[i].loopTime = shouldLoop;
                    changed = true;
                }
            }

            if (changed)
            {
                importer.clipAnimations = clips;
            }
        }

        if (changed)
        {
            if (clips != null && clips.Length > 0)
            {
                importer.clipAnimations = clips;
            }

            importer.SaveAndReimport();
        }
    }

    private static void ConfigureExactBoyRig(ModelImporter importer, bool importAnimation, ref bool changed)
    {
        // BOY uses the compatible merged Armature/Hips animation file for runtime states.
        // Generic keeps those original bone curves instead of Humanoid retargeting them.
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        if (importer.importAnimation != importAnimation)
        {
            importer.importAnimation = importAnimation;
            changed = true;
        }

        if (importer.optimizeGameObjects)
        {
            importer.optimizeGameObjects = false;
            changed = true;
        }

        if (importer.sourceAvatar != null)
        {
            importer.sourceAvatar = null;
            changed = true;
        }
    }

    private static bool ContainsAny(string value, params string[] parts)
    {
        if (string.IsNullOrEmpty(value) || parts == null)
        {
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureFolders()
    {
        EnsureFolder("Assets/Animations", "Th\u1EAFng");
        EnsureFolder("Assets/Animations/Th\u1EAFng", "Boy");
        EnsureFolder(BoyAnimationsFolder, "RuntimeFixed");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string full = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(full))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}






