using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CapstonePlayerSetup
{
    private const string ControllerGuid = "7055637715af6304e8460cbc24b40236";
    private const string VexaPrefabGuid = "af66125fc77de28428e08afd4fd94286";
    private const string IdleClipGuid = "341c576f8aa097d46b777b4b1e950b42";
    private const string IdleChillClipGuid = "b4954a14d34f06446b69305ceeb4a95c";
    private const string JumpClipGuid = "adda36e5788cbd54d8a92cb11a9c8c56";
    private const string SlowRunClipGuid = "48917c912e9f1444babc48600de9f89d";
    private const string FastRunClipGuid = "60d1bc6d8510ed249872168e2a4b8ffd";
    private const string IdleToRollClipGuid = "bbb126017a6250e46b21d4d1d84297f3";
    private const string RunToRollingClipGuid = "bb84197e6ab9a584db837cec0b5b3db3";
    private const string RunLookBackClipGuid = "204dd593ee97f9446af04cea427b13f0";
    private const string RunningBackwardClipGuid = "0bb15fcde6a37f548ae84d57dfff6597";
    private const string ThrowClipGuid = "439b968c5093b564eace9d4a6b606c50";
    private static readonly string ControllerFallbackPath = "Assets/Animations/Th\u1EAFng/PlayerBasic.controller";

    private struct StateSpec
    {
        public readonly string StateName;
        public readonly string ClipGuid;
        public readonly string PreferredClipName;
        public readonly bool Loop;
        public readonly bool UseRootMotionXZ;
        public readonly Vector3 Position;
        public readonly float Speed;

        public StateSpec(string stateName, string clipGuid, string preferredClipName, bool loop, bool useRootMotionXZ, Vector3 position, float speed)
        {
            StateName = stateName;
            ClipGuid = clipGuid;
            PreferredClipName = preferredClipName;
            Loop = loop;
            UseRootMotionXZ = useRootMotionXZ;
            Position = position;
            Speed = speed;
        }
    }

    [InitializeOnLoadMethod]
    private static void AutoRepairPlayerControllerAfterCompile()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            ConfigureImportedClips(GetStateSpecs());

            AnimatorController controller = LoadController();
            if (controller == null || !ControllerHasRequiredStates(controller))
            {
                EnsureAnimatorController(true);
                int fixedPlayers = FixPlayersInOpenScenes(false);
                Debug.Log("[Capstone] Rebuilt PlayerBasic controller and refreshed player setup. Players fixed: " + fixedPlayers);
            }
        };
    }

    [MenuItem("Tools/Capstone/Create Vexa Basic Player In Scene")]
    public static void CreateVexaBasicPlayer()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetAssetPath(VexaPrefabGuid));
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Vexa prefab not found", "Cannot find Vexa prefab by GUID:\n" + VexaPrefabGuid, "OK");
            return;
        }

        GameObject player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (player == null)
        {
            EditorUtility.DisplayDialog("Create player failed", "Could not instantiate the Vexa prefab.", "OK");
            return;
        }

        Undo.RegisterCreatedObjectUndo(player, "Create Vexa Basic Player");
        player.name = "Player";
        player.transform.position = Vector3.zero;
        player.transform.rotation = Quaternion.identity;

        SetupPlayer(player);
        EnsureTestGround();
        SetupCamera(player.transform);

        Selection.activeGameObject = player;
        EditorSceneManager.MarkSceneDirty(player.scene);
    }

    [MenuItem("Tools/Capstone/Rebuild Player Animator Controller")]
    public static void RebuildPlayerAnimatorController()
    {
        EnsureAnimatorController(true);
        int fixedPlayers = FixPlayersInOpenScenes(false);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Player controller rebuilt", "Rebuilt PlayerBasic and refreshed players: " + fixedPlayers, "OK");
    }

    [MenuItem("Tools/Capstone/Fix Player Animator Missing")]
    public static void FixPlayerAnimatorMissing()
    {
        int fixedCount = FixPlayersInOpenScenes(true);
        EditorUtility.DisplayDialog("Player animator fixed", "Fixed player animator setup count: " + fixedCount, "OK");
    }

    [MenuItem("Tools/Capstone/Setup Selected Basic Player")]
    public static void SetupSelectedBasicPlayer()
    {
        GameObject player = Selection.activeGameObject;
        if (player == null)
        {
            EditorUtility.DisplayDialog("No player selected", "Select the player object in the Hierarchy first.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(player, "Setup Basic Player");
        SetupPlayer(player);
        SetupCamera(player.transform);
        EditorSceneManager.MarkSceneDirty(player.scene);
    }

    [MenuItem("Tools/Capstone/Setup Selected Basic Player", true)]
    public static bool ValidateSetupSelectedBasicPlayer()
    {
        return Selection.activeGameObject != null;
    }

    private static int FixPlayersInOpenScenes(bool includeSelectionFallback)
    {
        int fixedCount = 0;
        BasicPlayerMovement[] players = Object.FindObjectsByType<BasicPlayerMovement>(FindObjectsSortMode.None);
        foreach (BasicPlayerMovement movement in players)
        {
            if (movement == null)
            {
                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(movement.gameObject, "Fix player animator missing");
            SetupPlayer(movement.gameObject);
            fixedCount++;
            EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        }

        if (includeSelectionFallback && Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<BasicPlayerMovement>() == null)
        {
            Undo.RegisterFullObjectHierarchyUndo(Selection.activeGameObject, "Fix selected player animator missing");
            SetupPlayer(Selection.activeGameObject);
            fixedCount++;
            EditorSceneManager.MarkSceneDirty(Selection.activeGameObject.scene);
        }

        return fixedCount;
    }

    private static void SetupPlayer(GameObject player)
    {
        RuntimeAnimatorController controller = EnsureAnimatorController(false);
        Transform animatorTarget = FindChildRecursive(player.transform, "Armature");
        if (animatorTarget == null)
        {
            animatorTarget = player.transform;
        }

        Animator rootAnimator = player.GetComponent<Animator>();
        if (rootAnimator != null && animatorTarget != player.transform)
        {
            Undo.DestroyObjectImmediate(rootAnimator);
        }

        Animator animator = animatorTarget.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(animatorTarget.gameObject);
        }

        Undo.RecordObject(animator, "Assign player animator controller");
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterController characterController = GetOrAddComponent<CharacterController>(player);
        Undo.RecordObject(characterController, "Setup character controller");
        characterController.height = 1.8f;
        characterController.radius = 0.35f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.35f;
        characterController.slopeLimit = 45f;

        BasicPlayerMovement movement = GetOrAddComponent<BasicPlayerMovement>(player);
        Undo.RecordObject(movement, "Setup player movement");
        movement.animator = animator;
        movement.cameraTransform = Camera.main != null ? Camera.main.transform : null;
        movement.useRootMotion = true;
        movement.fallbackToCodeMotionWhenNoRootMotion = true;
        movement.slowRunSpeed = 3.5f;
        movement.fastRunSpeed = 6f;
        movement.backwardSpeed = 2.8f;
        movement.slowRunRootMotionScale = 1f;
        movement.fastRunRootMotionScale = 1f;
        movement.backwardRootMotionScale = 1f;
        movement.rollRootMotionScale = 1f;
        movement.jumpHeight = 1.4f;
        movement.idleChillDelay = 5f;
        movement.rollKey = KeyCode.Q;
        movement.aimMouseButton = 1;
        movement.throwDuration = 0.75f;
        movement.idleRollDistance = 2.2f;
        movement.runRollDistance = 3.6f;
        movement.keepAnimatorTransformPinned = true;

        AnimatorRootMotionRelay relay = GetOrAddComponent<AnimatorRootMotionRelay>(animator.gameObject);
        Undo.RecordObject(relay, "Setup root motion relay");
        relay.movement = movement;
    }

    private static void SetupCamera(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(cameraObject, "Create main camera");
            cameraObject.tag = "MainCamera";
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        Undo.RecordObject(camera.transform, "Setup camera position");
        camera.transform.position = target.position + new Vector3(0f, 2.2f, -4.5f);
        camera.transform.LookAt(target.position + Vector3.up * 1.2f);

        BasicCameraFollow follow = GetOrAddComponent<BasicCameraFollow>(camera.gameObject);
        Undo.RecordObject(follow, "Setup camera follow");
        follow.target = target;
        follow.offset = new Vector3(0f, 2.2f, -4.5f);
        follow.aimOffset = new Vector3(0.75f, 1.8f, -2.75f);
        follow.followSpeed = 10f;
        follow.lookHeight = 1.2f;
        follow.aimLookHeight = 1.45f;
        follow.aimLookAhead = 10f;
        follow.aimMouseButton = 1;
        follow.aimBlendSpeed = 10f;
        follow.mouseSensitivity = 2.2f;
        follow.lockCursorOnPlay = true;
        follow.showAimReticle = true;
    }

    private static void EnsureTestGround()
    {
        GameObject ground = GameObject.Find("Basic Test Ground");
        if (ground != null)
        {
            return;
        }

        ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Undo.RegisterCreatedObjectUndo(ground, "Create test ground");
        ground.name = "Basic Test Ground";
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
    }

    private static RuntimeAnimatorController EnsureAnimatorController(bool forceRebuild)
    {
        AnimatorController controller = LoadController();
        if (controller == null)
        {
            EnsureFolder("Assets/Animations/Th\u1EAFng");
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerFallbackPath);
        }

        if (forceRebuild || !ControllerHasRequiredStates(controller))
        {
            RebuildAnimatorController(controller);
        }

        return controller;
    }

    private static AnimatorController LoadController()
    {
        string controllerPath = GetAssetPath(ControllerGuid);
        if (string.IsNullOrEmpty(controllerPath))
        {
            controllerPath = ControllerFallbackPath;
        }

        return AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
    }

    private static void RebuildAnimatorController(AnimatorController controller)
    {
        StateSpec[] stateSpecs = GetStateSpecs();
        ConfigureImportedClips(stateSpecs);

        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("FastRun", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Backward", AnimatorControllerParameterType.Bool);
        controller.AddParameter("LookBack", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Rolling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Jumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Throwing", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        stateMachine.name = "Base Layer";

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        AnimatorState defaultState = null;
        foreach (StateSpec spec in stateSpecs)
        {
            AnimatorState state = stateMachine.AddState(spec.StateName, spec.Position);
            state.motion = LoadClipByGuid(spec.ClipGuid, spec.PreferredClipName);
            state.speed = spec.Speed;
            state.writeDefaultValues = true;

            if (spec.StateName == "Idle")
            {
                defaultState = state;
            }

            if (state.motion == null)
            {
                Debug.LogWarning("[Capstone] Missing animation clip for state: " + spec.StateName);
            }
        }

        if (defaultState != null)
        {
            stateMachine.defaultState = defaultState;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static StateSpec[] GetStateSpecs()
    {
        return new StateSpec[]
        {
            new StateSpec("Idle", IdleClipGuid, "Idle", true, false, new Vector3(260f, 80f, 0f), 1f),
            new StateSpec("IdleChill", IdleChillClipGuid, "IdleChill", true, false, new Vector3(260f, 190f, 0f), 1f),
            new StateSpec("SlowRun", SlowRunClipGuid, "Slow Run", true, true, new Vector3(520f, 80f, 0f), 1f),
            new StateSpec("FastRun", FastRunClipGuid, "Fast Run", true, true, new Vector3(520f, 190f, 0f), 1f),
            new StateSpec("RunningBackward", RunningBackwardClipGuid, "Running Backward", true, true, new Vector3(520f, 300f, 0f), 1f),
            new StateSpec("RunLookBack", RunLookBackClipGuid, "Run Look Back", true, true, new Vector3(780f, 80f, 0f), 1f),
            new StateSpec("IdleToRoll", IdleToRollClipGuid, "Idle to Roll", false, true, new Vector3(260f, 410f, 0f), 1f),
            new StateSpec("RunToRolling", RunToRollingClipGuid, "Run To Rolling", false, true, new Vector3(520f, 410f, 0f), 1f),
            new StateSpec("Jump", JumpClipGuid, "Jump01", false, false, new Vector3(780f, 300f, 0f), 1f),
            new StateSpec("Throw", ThrowClipGuid, "GunShot01", false, false, new Vector3(780f, 410f, 0f), 1f),
        };
    }

    private static bool ControllerHasRequiredStates(AnimatorController controller)
    {
        if (controller == null || controller.layers == null || controller.layers.Length == 0)
        {
            return false;
        }

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        StateSpec[] specs = GetStateSpecs();
        foreach (StateSpec spec in specs)
        {
            bool found = false;
            foreach (ChildAnimatorState state in states)
            {
                if (state.state != null && state.state.name == spec.StateName)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }

    private static void ConfigureImportedClips(StateSpec[] stateSpecs)
    {
        foreach (StateSpec spec in stateSpecs)
        {
            ConfigureClipImport(spec);
        }
    }

    private static void ConfigureClipImport(StateSpec spec)
    {
        string path = GetAssetPath(spec.ClipGuid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[Capstone] Cannot find animation asset by GUID: " + spec.ClipGuid);
            return;
        }

        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null)
        {
            return;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        if (clips == null || clips.Length == 0)
        {
            return;
        }

        bool changed = false;
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i].loopTime != spec.Loop)
            {
                clips[i].loopTime = spec.Loop;
                changed = true;
            }

            if (clips[i].loopPose != spec.Loop)
            {
                clips[i].loopPose = spec.Loop;
                changed = true;
            }

            if (!clips[i].lockRootRotation)
            {
                clips[i].lockRootRotation = true;
                changed = true;
            }

            if (!clips[i].lockRootHeightY)
            {
                clips[i].lockRootHeightY = true;
                changed = true;
            }

            bool shouldLockRootPositionXZ = !spec.UseRootMotionXZ;
            if (clips[i].lockRootPositionXZ != shouldLockRootPositionXZ)
            {
                clips[i].lockRootPositionXZ = shouldLockRootPositionXZ;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip LoadClipByGuid(string guid, string preferredClipName)
    {
        return LoadClip(GetAssetPath(guid), preferredClipName);
    }

    private static AnimationClip LoadClip(string path, string preferredClipName)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        AnimationClip fallback = null;

        foreach (Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__"))
            {
                continue;
            }

            if (clip.name == preferredClipName)
            {
                return clip;
            }

            if (fallback == null)
            {
                fallback = clip;
            }
        }

        return fallback;
    }

    private static string GetAssetPath(string guid)
    {
        return AssetDatabase.GUIDToAssetPath(guid);
    }

    private static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform match = FindChildRecursive(child, childName);
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        if (component == null)
        {
            component = Undo.AddComponent<T>(gameObject);
        }

        return component;
    }

    private static void EnsureFolder(string unityPath)
    {
        if (AssetDatabase.IsValidFolder(unityPath))
        {
            return;
        }

        string[] parts = unityPath.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
