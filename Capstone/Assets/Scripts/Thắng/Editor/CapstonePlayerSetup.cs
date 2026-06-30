using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CapstonePlayerSetup
{
    private const string ControllerGuid = "7055637715af6304e8460cbc24b40236";
    private const string VexaPrefabGuid = "af66125fc77de28428e08afd4fd94286";
    private const string NeutralIdleGuid = "e504240169a5ef74aae29a1e7e38854b";
    private const string SadIdleGuid = "a2e1a10679dea714083014a61923af83";
    private const string IdleBattleGuid = "5be2ca04da0951c4d945c78004c6f75d";
    private const string SlowRunGuid = "48917c912e9f1444babc48600de9f89d";
    private const string SprintGuid = "67c8151bfac8543428614cbaa518af51";
    private const string RunToStopGuid = "f88d3e95bed763a4e91a7171012aefd9";
    private const string StandingToCrouchedGuid = "34f3156609079794480dc5e9c3291ddb";
    private const string CrouchIdleGuid = "305dd9f6f04de5140ae1b62c3738f97d";
    private const string CrouchedWalkingGuid = "ae600b20d521dae42b80d101aa4a92a6";
    private const string CrouchedToStandingGuid = "34f3156609079794480dc5e9c3291ddb";
    private const string CrouchedToSprintingGuid = "6c1b77285c076d3499d90fec919798d5";
    private const string IdleToRollGuid = "bbb126017a6250e46b21d4d1d84297f3";
    private const string RunToRollingGuid = "bb84197e6ab9a584db837cec0b5b3db3";
    private const string RunningBackwardGuid = "0bb15fcde6a37f548ae84d57dfff6597";
    private const string RunningJumpGuid = "01692d7a53071914abd2e41fe8d6f5c2";
    private const string PickingUpGuid = "e13fe1e094bbec348acc00a515191bcd";
    private const string ThrowGuid = "8d5141b03c6d1fc41869a230679ba972";
    private const float CrouchTransitionDuration = 0.5f;
    private const float RollControllerHeight = 0.9f;
    private const float RollControllerRadius = 0.3f;
    private static readonly Vector3 RollControllerCenter = new Vector3(0f, 0.45f, 0f);

    private static readonly string AnimationsRoot = "Assets/Animations/Th\u1EAFng";
    private static readonly string ControllerFallbackPath = AnimationsRoot + "/PlayerBasic.controller";

    private struct StateSpec
    {
        public readonly string StateName;
        public readonly string ClipGuid;
        public readonly string[] PreferredClipNames;
        public readonly bool Loop;
        public readonly bool AllowRootMotionXZ;
        public readonly Vector3 Position;
        public readonly float Speed;

        public StateSpec(string stateName, string clipGuid, bool loop, bool allowRootMotionXZ, Vector3 position, float speed, params string[] preferredClipNames)
        {
            StateName = stateName;
            ClipGuid = clipGuid;
            Loop = loop;
            AllowRootMotionXZ = allowRootMotionXZ;
            Position = position;
            Speed = speed;
            PreferredClipNames = preferredClipNames;
        }
    }

    [MenuItem("Tools/Capstone/Repair Player Setup In Open Scene")]
    public static void RepairPlayerControllerSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        AnimatorController controller = LoadController();
        bool needsRebuild = controller == null || !ControllerHasRequiredStates(controller);
        if (needsRebuild)
        {
            EnsureAnimatorController(true);
        }

        int fixedPlayers = FixPlayersInOpenScenes(false, needsRebuild);
        bool fixedCamera = RefreshCameraForFirstOpenPlayer(needsRebuild);
        Debug.Log("[Capstone] Player setup repair finished. Controller rebuilt: " + needsRebuild + ". Players fixed: " + fixedPlayers + ". Camera fixed: " + fixedCamera);
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
        int fixedPlayers = FixPlayersInOpenScenes(false, true);
        bool fixedCamera = RefreshCameraForFirstOpenPlayer(true);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Player controller rebuilt", "Rebuilt PlayerBasic. Players refreshed: " + fixedPlayers + ". Camera refreshed: " + fixedCamera, "OK");
    }

    [MenuItem("Tools/Capstone/Fix Player Animator Missing")]
    public static void FixPlayerAnimatorMissing()
    {
        int fixedCount = FixPlayersInOpenScenes(true, true);
        bool fixedCamera = RefreshCameraForFirstOpenPlayer(true);
        EditorUtility.DisplayDialog("Player animator fixed", "Fixed player animator setup count: " + fixedCount + ". Camera refreshed: " + fixedCamera, "OK");
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

    private static int FixPlayersInOpenScenes(bool includeSelectionFallback, bool force)
    {
        int fixedCount = 0;
        BasicPlayerMovement[] players = Object.FindObjectsByType<BasicPlayerMovement>(FindObjectsSortMode.None);
        foreach (BasicPlayerMovement movement in players)
        {
            if (movement == null)
            {
                continue;
            }

            if (!force && !PlayerNeedsSetup(movement))
            {
                continue;
            }

            Undo.RegisterFullObjectHierarchyUndo(movement.gameObject, "Fix player setup");
            SetupPlayer(movement.gameObject);
            fixedCount++;
            EditorSceneManager.MarkSceneDirty(movement.gameObject.scene);
        }

        if (includeSelectionFallback && Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<BasicPlayerMovement>() == null)
        {
            Undo.RegisterFullObjectHierarchyUndo(Selection.activeGameObject, "Fix selected player setup");
            SetupPlayer(Selection.activeGameObject);
            fixedCount++;
            EditorSceneManager.MarkSceneDirty(Selection.activeGameObject.scene);
        }

        return fixedCount;
    }

    private static bool RefreshCameraForFirstOpenPlayer(bool force)
    {
        BasicPlayerMovement[] players = Object.FindObjectsByType<BasicPlayerMovement>(FindObjectsSortMode.None);
        if (players == null || players.Length == 0 || players[0] == null)
        {
            return false;
        }

        if (!force && !CameraNeedsSetup(players[0].transform))
        {
            return false;
        }

        SetupCamera(players[0].transform);
        return true;
    }

    private static bool CameraNeedsSetup(Transform target)
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return true;
        }

        System.Type brainType = FindType("Unity.Cinemachine.CinemachineBrain");
        System.Type cameraType = FindType("Unity.Cinemachine.CinemachineCamera");
        if (brainType == null || cameraType == null)
        {
            BasicCameraFollow fallbackFollow = camera.GetComponent<BasicCameraFollow>();
            return fallbackFollow == null || fallbackFollow.target != target;
        }

        if (camera.GetComponent(brainType) == null)
        {
            return true;
        }

        GameObject virtualCameraObject = GameObject.Find("CM Player Follow Camera");
        if (virtualCameraObject == null)
        {
            return true;
        }

        Component cinemachineCamera = virtualCameraObject.GetComponent(cameraType);
        return cinemachineCamera == null || GetMember(cinemachineCamera, "Follow") as Transform != target;
    }

    private static bool PlayerNeedsSetup(BasicPlayerMovement movement)
    {
        if (movement == null)
        {
            return false;
        }

        Transform animatorTarget = FindChildRecursive(movement.transform, "Armature");
        if (animatorTarget == null)
        {
            animatorTarget = movement.transform;
        }

        Animator expectedAnimator = animatorTarget.GetComponent<Animator>();
        Animator rootAnimator = movement.GetComponent<Animator>();
        RuntimeAnimatorController controller = LoadController();

        return movement.animator == null
            || expectedAnimator == null
            || movement.animator != expectedAnimator
            || (animatorTarget != movement.transform && rootAnimator != null)
            || expectedAnimator.runtimeAnimatorController != controller
            || expectedAnimator.applyRootMotion
            || movement.useRootMotion
            || movement.rootMotionDrivesLocomotion
            || movement.rootMotionDrivesRoll
            || movement.rootMotionDrivesJump
            || !movement.codeDrivesInPlaceLocomotion
            || movement.applyAnimatorRootYaw
            || movement.applyAnimatorRootRotationToVisual
            || movement.keepAnimatorTransformPinned
            || !movement.snapFeetToGround
            || Mathf.Abs(movement.groundContactOffset) > 0.001f
            || Mathf.Abs(movement.standingToCrouchDuration - CrouchTransitionDuration) > 0.001f
            || Mathf.Abs(movement.crouchToStandingDuration - CrouchTransitionDuration) > 0.001f
            || !movement.resizeControllerDuringRoll
            || Mathf.Abs(movement.rollControllerHeight - RollControllerHeight) > 0.001f
            || Mathf.Abs(movement.rollControllerRadius - RollControllerRadius) > 0.001f
            || (movement.rollControllerCenter - RollControllerCenter).sqrMagnitude > 0.0001f
            || expectedAnimator.GetComponent<AnimatorRootMotionRelay>() != null
            || PlayerRigidbodyNeedsTiltLock(movement.gameObject)
            || movement.idleNeutralState != "IdleNeutral"
            || movement.sprintState != "Sprint"
            || movement.crouchWalkingState != "CrouchWalking"
            || movement.sprintingToRollState != "SprintingToRoll";
    }

    private static bool PlayerRigidbodyNeedsTiltLock(GameObject player)
    {
        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body == null)
        {
            return false;
        }

        RigidbodyConstraints required = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        return body.constraints != required || !body.isKinematic || body.useGravity;
    }

    private static void SetupPlayer(GameObject player)
    {
        RuntimeAnimatorController controller = EnsureAnimatorController(false);
        Transform animatorTarget = FindChildRecursive(player.transform, "Armature");
        if (animatorTarget == null)
        {
            animatorTarget = player.transform;
        }

        RemoveUnexpectedAnimatorSetup(player, animatorTarget);

        Animator animator = animatorTarget.GetComponent<Animator>();
        if (animator == null)
        {
            animator = Undo.AddComponent<Animator>(animatorTarget.gameObject);
        }

        Undo.RecordObject(animator, "Assign player animator controller");
        animator.avatar = null;
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        CharacterController characterController = GetOrAddComponent<CharacterController>(player);
        Undo.RecordObject(characterController, "Setup character controller");
        characterController.height = 1.8f;
        characterController.radius = 0.35f;
        characterController.center = new Vector3(0f, 0.9f, 0f);
        characterController.stepOffset = 0.35f;
        characterController.slopeLimit = 45f;
        characterController.skinWidth = 0.02f;
        characterController.minMoveDistance = 0f;
        characterController.detectCollisions = true;
        characterController.enableOverlapRecovery = true;

        Rigidbody body = player.GetComponent<Rigidbody>();
        if (body != null)
        {
            Undo.RecordObject(body, "Setup player rigidbody");
            body.useGravity = false;
            body.isKinematic = true;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        BasicPlayerMovement movement = GetOrAddComponent<BasicPlayerMovement>(player);
        Undo.RecordObject(movement, "Setup player movement");
        movement.animator = animator;
        movement.cameraTransform = Camera.main != null ? Camera.main.transform : null;
        movement.useRootMotion = false;
        movement.rootMotionDrivesLocomotion = false;
        movement.rootMotionDrivesRoll = false;
        movement.rootMotionDrivesJump = false;
        movement.codeDrivesInPlaceLocomotion = true;
        movement.fallbackToCodeMotionWhenRootMotionIsSmall = false;
        movement.rootMotionScale = 1f;
        movement.rootMotionFallbackDelay = 0.08f;
        movement.rootMotionFallbackThreshold = 0.005f;
        movement.applyAnimatorRootYaw = false;
        movement.applyAnimatorRootRotationToVisual = false;
        movement.keepAnimatorTransformPinned = false;
        movement.snapFeetToGround = true;
        movement.groundLayers = ~0;
        movement.groundSnapDistance = 0.45f;
        movement.groundSnapProbeHeight = 0.35f;
        movement.groundContactOffset = 0f;
        movement.slowRunSpeed = 3.5f;
        movement.sprintSpeed = 6f;
        movement.backwardSpeed = 2.8f;
        movement.crouchWalkSpeed = 1.55f;
        movement.acceleration = 18f;
        movement.deceleration = 24f;
        movement.turnSpeed = 720f;
        movement.jumpHeight = 1.35f;
        movement.rollKey = KeyCode.Q;
        movement.idleRollDuration = 2.15f;
        movement.sprintRollDuration = 1.35f;
        movement.idleRollDistance = 2.6f;
        movement.sprintRollDistance = 3.7f;
        movement.resizeControllerDuringRoll = true;
        movement.rollControllerHeight = RollControllerHeight;
        movement.rollControllerRadius = RollControllerRadius;
        movement.rollControllerCenter = RollControllerCenter;
        movement.standingToCrouchDuration = CrouchTransitionDuration;
        movement.crouchToStandingDuration = CrouchTransitionDuration;
        movement.crouchKey = KeyCode.LeftControl;
        movement.alternateCrouchKey = KeyCode.RightControl;
        movement.pickUpKey = KeyCode.E;
        movement.aimMouseButton = 1;
        movement.rightMouseButtonThrows = false;
        movement.requirePickupTarget = false;
        movement.idleSadDelay = 5f;
        movement.battleRelaxDelay = 5f;
        movement.runToStopDuration = 0.55f;
        movement.jumpDuration = 0.95f;
        movement.pickUpDuration = 1.15f;
        movement.throwDuration = 0.95f;
        movement.useAnimatorStateLengthForActions = true;
        movement.actionExitNormalizedTime = 0.98f;
        movement.minimumActionDuration = 0.08f;

        movement.idleNeutralState = "IdleNeutral";
        movement.idleSadState = "IdleSad";
        movement.idleBattleState = "IdleBattle";
        movement.slowRunState = "SlowRun";
        movement.sprintState = "Sprint";
        movement.runToStopState = "RunToStop";
        movement.standingToCrouchState = "StandingToCrouch";
        movement.crouchIdleState = "CrouchIdle";
        movement.crouchWalkingState = "CrouchWalking";
        movement.crouchToStandingState = "CrouchToStanding";
        movement.crouchToSprintState = "CrouchToSprint";
        movement.idleToRollState = "IdleToRoll";
        movement.sprintingToRollState = "SprintingToRoll";
        movement.runningBackwardState = "RunningBackward";
        movement.jumpState = "Jump";
        movement.pickingUpState = "PickingUp";
        movement.throwState = "Throw";

        AnimatorRootMotionRelay relay = animator.GetComponent<AnimatorRootMotionRelay>();
        if (relay != null)
        {
            Undo.DestroyObjectImmediate(relay);
        }
    }

    private static void RemoveUnexpectedAnimatorSetup(GameObject player, Transform animatorTarget)
    {
        AnimatorRootMotionRelay[] relays = player.GetComponentsInChildren<AnimatorRootMotionRelay>(true);
        foreach (AnimatorRootMotionRelay relay in relays)
        {
            if (relay != null && relay.transform != animatorTarget)
            {
                Undo.DestroyObjectImmediate(relay);
            }
        }

        Animator[] animators = player.GetComponentsInChildren<Animator>(true);
        foreach (Animator nestedAnimator in animators)
        {
            if (nestedAnimator != null && nestedAnimator.transform != animatorTarget)
            {
                Undo.DestroyObjectImmediate(nestedAnimator);
            }
        }
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

        if (SetupCinemachineCamera(camera, target))
        {
            BasicCameraFollow legacyFollow = camera.GetComponent<BasicCameraFollow>();
            if (legacyFollow != null)
            {
                Undo.RecordObject(legacyFollow, "Disable legacy camera follow");
                legacyFollow.enabled = false;
            }

            return;
        }

        BasicCameraFollow follow = GetOrAddComponent<BasicCameraFollow>(camera.gameObject);
        Undo.RecordObject(follow, "Setup fallback camera follow");
        follow.enabled = true;
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

    private static bool SetupCinemachineCamera(Camera camera, Transform target)
    {
        System.Type brainType = FindType("Unity.Cinemachine.CinemachineBrain");
        System.Type cameraType = FindType("Unity.Cinemachine.CinemachineCamera");
        System.Type thirdPersonFollowType = FindType("Unity.Cinemachine.CinemachineThirdPersonFollow");

        if (brainType == null || cameraType == null || thirdPersonFollowType == null)
        {
            return false;
        }

        GetOrAddComponent(camera.gameObject, brainType);

        GameObject virtualCameraObject = GameObject.Find("CM Player Follow Camera");
        if (virtualCameraObject == null)
        {
            virtualCameraObject = new GameObject("CM Player Follow Camera");
            Undo.RegisterCreatedObjectUndo(virtualCameraObject, "Create Cinemachine player camera");
        }

        virtualCameraObject.transform.position = target.position + new Vector3(0f, 2.2f, -4.5f);
        virtualCameraObject.transform.LookAt(target.position + Vector3.up * 1.2f);

        Component cinemachineCamera = GetOrAddComponent(virtualCameraObject, cameraType);
        SetMember(cinemachineCamera, "Follow", target);
        SetMember(cinemachineCamera, "LookAt", target);
        SetMember(cinemachineCamera, "Priority", 20);

        Component thirdPersonFollow = GetOrAddComponent(virtualCameraObject, thirdPersonFollowType);
        SetMember(thirdPersonFollow, "ShoulderOffset", new Vector3(0.75f, -0.35f, 0f));
        SetMember(thirdPersonFollow, "CameraSide", 1f);
        SetMember(thirdPersonFollow, "VerticalArmLength", 1.2f);
        SetMember(thirdPersonFollow, "CameraDistance", 4f);
        SetMember(thirdPersonFollow, "Damping", new Vector3(0.08f, 0.18f, 0.18f));

        EditorSceneManager.MarkSceneDirty(target.gameObject.scene);
        return true;
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
        ConfigureImportedClips(GetStateSpecs());

        AnimatorController controller = LoadController();
        if (controller == null)
        {
            EnsureFolder(AnimationsRoot);
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

        controller.parameters = new AnimatorControllerParameter[0];
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveX", AnimatorControllerParameterType.Float);
        controller.AddParameter("MoveY", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsEnemyNearby", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsRolling", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsJumping", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsPickingUp", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IsThrowing", AnimatorControllerParameterType.Bool);
        controller.AddParameter("IdleTimer", AnimatorControllerParameterType.Float);
        controller.AddParameter("FastRun", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Backward", AnimatorControllerParameterType.Bool);
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
            state.motion = LoadClipForState(spec);
            state.speed = spec.Speed;
            state.cycleOffset = 0f;
            state.writeDefaultValues = true;

            if (spec.StateName == "IdleNeutral")
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
            new StateSpec("IdleNeutral", NeutralIdleGuid, true, false, new Vector3(220f, 80f, 0f), 1f, "Neutral Idle", "Idle"),
            new StateSpec("IdleSad", SadIdleGuid, true, false, new Vector3(220f, 190f, 0f), 1f, "Sad Idle", "IdleChill"),
            new StateSpec("IdleBattle", IdleBattleGuid, true, false, new Vector3(220f, 300f, 0f), 1f, "IdleBattle", "Battle Idle"),
            new StateSpec("SlowRun", SlowRunGuid, true, true, new Vector3(500f, 80f, 0f), 1f, "Slow Run", "Run"),
            new StateSpec("Sprint", SprintGuid, true, true, new Vector3(500f, 190f, 0f), 1f, "Sprint", "Fast Run"),
            new StateSpec("RunningBackward", RunningBackwardGuid, true, true, new Vector3(500f, 300f, 0f), 1f, "Running Backward"),
            new StateSpec("RunToStop", RunToStopGuid, false, true, new Vector3(500f, 410f, 0f), 1f, "Run To Stop"),
            new StateSpec("StandingToCrouch", StandingToCrouchedGuid, false, false, new Vector3(780f, 80f, 0f), -1f, "Crouch To Standing Idle(Thaythechuan)", "Crouch To Standing Idle", "Crouch To Standing", "Crouched To Standing"),
            new StateSpec("CrouchIdle", CrouchIdleGuid, true, false, new Vector3(780f, 190f, 0f), 1f, "Crouch Idle", "Crouched Idle"),
            new StateSpec("CrouchWalking", CrouchedWalkingGuid, true, true, new Vector3(780f, 300f, 0f), 1f, "Crouched Walking"),
            new StateSpec("CrouchToStanding", CrouchedToStandingGuid, false, false, new Vector3(780f, 410f, 0f), 1f, "Crouch To Standing Idle(Thaythechuan)", "Crouch To Standing Idle", "Crouch To Standing", "Crouched To Standing"),
            new StateSpec("CrouchToSprint", CrouchedToSprintingGuid, false, true, new Vector3(780f, 520f, 0f), 1f, "Crouched To Sprinting", "Crouch To Sprint"),
            new StateSpec("IdleToRoll", IdleToRollGuid, false, true, new Vector3(1060f, 80f, 0f), 1f, "Idle to Roll", "Idle To Roll"),
            new StateSpec("SprintingToRoll", RunToRollingGuid, false, true, new Vector3(1060f, 190f, 0f), 1f, "Run To Rolling", "Sprinting To Roll", "Run To Roll"),
            new StateSpec("Jump", RunningJumpGuid, false, true, new Vector3(1060f, 300f, 0f), 1f, "Running Jump", "Jump"),
            new StateSpec("PickingUp", PickingUpGuid, false, false, new Vector3(1060f, 410f, 0f), 1f, "Picking Up Object", "Picking Up"),
            new StateSpec("Throw", ThrowGuid, false, false, new Vector3(1060f, 520f, 0f), 1f, "Throw (chuan)", "Throw", "GunShot01"),
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
            AnimationClip expectedClip = LoadClipForState(spec);
            if (expectedClip == null)
            {
                return false;
            }

            bool found = false;
            foreach (ChildAnimatorState state in states)
            {
                if (state.state == null || state.state.name != spec.StateName)
                {
                    continue;
                }

                float expectedCycleOffset = 0f;
                if (state.state.motion == expectedClip && Mathf.Approximately(state.state.speed, spec.Speed) && Mathf.Approximately(state.state.cycleOffset, expectedCycleOffset))
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
        string path = FindAssetPathForSpec(spec);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[Capstone] Cannot find animation asset for state: " + spec.StateName);
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
        if (importer.animationType != ModelImporterAnimationType.Generic)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            changed = true;
        }

        if (importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.sourceAvatar = null;
            changed = true;
        }

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

            bool freeRootRotation = UsesFreeRootRotation(spec.StateName);
            bool freeRootHeight = UsesFreeRootHeight(spec.StateName);
            bool freeRootPositionXZ = UsesFreeRootPositionXZ(spec.StateName);

            if (clips[i].lockRootRotation == freeRootRotation)
            {
                clips[i].lockRootRotation = !freeRootRotation;
                changed = true;
            }

            if (!clips[i].keepOriginalOrientation)
            {
                clips[i].keepOriginalOrientation = true;
                changed = true;
            }

            if (clips[i].lockRootHeightY == freeRootHeight)
            {
                clips[i].lockRootHeightY = !freeRootHeight;
                changed = true;
            }

            bool keepOriginalPositionY = freeRootHeight;
            if (clips[i].keepOriginalPositionY != keepOriginalPositionY)
            {
                clips[i].keepOriginalPositionY = keepOriginalPositionY;
                changed = true;
            }

            bool heightFromFeet = !freeRootHeight;
            if (clips[i].heightFromFeet != heightFromFeet)
            {
                clips[i].heightFromFeet = heightFromFeet;
                changed = true;
            }

            if (clips[i].lockRootPositionXZ == freeRootPositionXZ)
            {
                clips[i].lockRootPositionXZ = !freeRootPositionXZ;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    private static bool UsesFreeRootRotation(string stateName)
    {
        return false;
    }

    private static bool UsesFreeRootHeight(string stateName)
    {
        return false;
    }

    private static bool UsesFreeRootPositionXZ(string stateName)
    {
        return false;
    }

    private static AnimationClip LoadClipForState(StateSpec spec)
    {
        AnimationClip clip = LoadClipFromPath(GetAssetPath(spec.ClipGuid), spec.PreferredClipNames);
        if (clip != null)
        {
            return clip;
        }

        string path = FindAssetPathForSpec(spec);
        return LoadClipFromPath(path, spec.PreferredClipNames);
    }

    private static AnimationClip LoadClipFromPath(string path, string[] preferredClipNames)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
        AnimationClip fallback = null;

        foreach (UnityEngine.Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__"))
            {
                continue;
            }

            if (NameMatches(clip.name, preferredClipNames))
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

    private static string FindAssetPathForSpec(StateSpec spec)
    {
        string path = GetAssetPath(spec.ClipGuid);
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { AnimationsRoot });
        for (int i = 0; i < guids.Length; i++)
        {
            path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
            if (NameMatches(fileName, spec.PreferredClipNames))
            {
                return path;
            }
        }

        return null;
    }

    private static bool NameMatches(string candidate, string[] preferredNames)
    {
        if (preferredNames == null || preferredNames.Length == 0)
        {
            return false;
        }

        string normalizedCandidate = NormalizeName(candidate);
        for (int i = 0; i < preferredNames.Length; i++)
        {
            string normalizedPreferred = NormalizeName(preferredNames[i]);
            if (normalizedCandidate == normalizedPreferred || normalizedCandidate.Contains(normalizedPreferred) || normalizedPreferred.Contains(normalizedCandidate))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
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

    private static Component GetOrAddComponent(GameObject gameObject, System.Type componentType)
    {
        Component component = gameObject.GetComponent(componentType);
        if (component == null)
        {
            component = Undo.AddComponent(gameObject, componentType);
        }

        return component;
    }

    private static System.Type FindType(string typeName)
    {
        System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            System.Type type = assemblies[i].GetType(typeName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetMember(object target, string memberName, object value)
    {
        if (target == null)
        {
            return;
        }

        System.Type type = target.GetType();
        System.Reflection.PropertyInfo property = type.GetProperty(memberName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value, null);
            EditorUtility.SetDirty(target as Object);
            return;
        }

        System.Reflection.FieldInfo field = type.GetField(memberName);
        if (field != null)
        {
            field.SetValue(target, value);
            EditorUtility.SetDirty(target as Object);
        }
    }

    private static object GetMember(object target, string memberName)
    {
        if (target == null)
        {
            return null;
        }

        System.Type type = target.GetType();
        System.Reflection.PropertyInfo property = type.GetProperty(memberName);
        if (property != null && property.CanRead)
        {
            return property.GetValue(target, null);
        }

        System.Reflection.FieldInfo field = type.GetField(memberName);
        return field != null ? field.GetValue(target) : null;
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

