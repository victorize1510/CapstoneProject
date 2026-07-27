#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

public static class PetTestSceneSetup
{
    private const string FallbackPetPath = "Assets/Monsters Ultimate Pack 02 Cute Series/Wolf Pup Cute Series/Prefabs/Wolf Pup.prefab";

    [MenuItem("Tools/Tháº¯ng/Setup Pet Combat Test")]
    public static void SetupActiveScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("No active scene found for pet setup.");
            return;
        }

        BasicPlayerMovement playerMovement = UnityEngine.Object.FindFirstObjectByType<BasicPlayerMovement>();
        GameObject player = playerMovement != null ? playerMovement.gameObject : GameObject.Find("Player");
        if (player == null)
        {
            Debug.LogWarning("Pet setup skipped: Player was not found.");
            return;
        }

        Camera camera = Camera.main;
        GameObject petObject = FindPetCandidate(player) ?? CreateFallbackPet(player.transform);
        if (petObject == null)
        {
            Debug.LogWarning("Pet setup skipped: no pet prefab found and fallback pet could not be loaded.");
            return;
        }

        PetController pet = SetupPet(petObject, player.transform);
        DummyEnemy dummy = SetupDummyEnemy(player.transform);
        SetupCommandInput(player, pet, camera);
        EnsureNavMeshSurface();

        EditorUtility.SetDirty(player);
        EditorUtility.SetDirty(petObject);
        if (dummy != null)
        {
            EditorUtility.SetDirty(dummy.gameObject);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Pet combat test setup completed. Left click the cube/enemy to command the active pet.");
    }

    private static PetController SetupPet(GameObject petObject, Transform owner)
    {
        PetController pet = GetOrAdd<PetController>(petObject);
        NavMeshAgent agent = GetOrAdd<NavMeshAgent>(petObject);
        Animator animator = petObject.GetComponentInChildren<Animator>(true);

        Undo.RecordObjects(new UnityEngine.Object[] { petObject, pet, agent }, "Setup pet controller");

        pet.owner = owner;
        pet.animator = animator;
        pet.agent = agent;
        pet.followOffset = new Vector3(-1.4f, 0f, -1.6f);
        pet.followDistance = 2.2f;
        pet.followResumeDistance = 3.1f;
        pet.attackRange = 1.45f;
        pet.attackApproachDistance = 0.65f;
        pet.attackReachPadding = 0.18f;
        pet.attackRepathInterval = 0.12f;
        pet.snapFaceTargetOnCommand = true;
        pet.attackDamage = 20f;
        pet.attackCooldown = 0.95f;
        pet.commandSpeed = 5.3f;
        pet.followSpeed = 4.2f;
        pet.useNavMeshWhenAvailable = true;
        pet.useDirectMoveFallback = true;
        pet.startUnderground = true;
        pet.useSelfInput = false;
        pet.summonKey = KeyCode.Alpha1;
        pet.summonOffset = new Vector3(0f, 0f, 2.4f);
        pet.spawnDuration = 1.1f;
        pet.keepUndergroundNearOwner = true;
        pet.summonOnCommand = false;
        pet.roamAroundOwner = true;
        pet.roamMinRadius = 1.8f;
        pet.roamMaxRadius = 4.2f;
        pet.roamIdleTimeMin = 0.75f;
        pet.roamIdleTimeMax = 2.2f;
        pet.roamWalkSpeed = 2.1f;
        pet.roamRunSpeed = 4.6f;
        pet.roamRunChance = 0.3f;
        pet.roamPointTolerance = 0.45f;
        pet.undergroundStates = new[] { "Underground", "Idle" };
        pet.spawnStates = new[] { "Spawn", "Idle" };
        pet.idleStates = new[] { "Idle", "Idle 1", "Idle Happy" };
        pet.moveStates = new[] { "Move", "Fly Forward In Place", "Run Forward In Place", "Walk Forward In Place", "Run Forward", "Walk Forward" };
        pet.attackStates = new[] { "Attack", "Bite Attack", "Bite Attack Low", "Projectile Attack", "Projectile Attack Low", "Cast Spell", "Blast Attack", "Wing Attack" };

        if (animator != null)
        {
            Undo.RecordObject(animator, "Setup pet animator");
            RuntimeAnimatorController runtimeController = CreatePetRuntimeController(animator, petObject);
            if (runtimeController != null)
            {
                animator.runtimeAnimatorController = runtimeController;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            EditorUtility.SetDirty(animator);
        }

        ConfigureAgent(agent, petObject);
        return pet;
    }

    private static RuntimeAnimatorController CreatePetRuntimeController(Animator animator, GameObject petObject)
    {
        RuntimeAnimatorController sourceController = animator.runtimeAnimatorController;
        if (sourceController == null)
        {
            Debug.LogWarning("Pet setup could not create a clean animator controller because the pet has no source controller.");
            return null;
        }

        Dictionary<string, Motion> motions = CollectStateMotions(sourceController);
        Motion idle = FindMotion(motions, "Idle", "Idle 1", "Idle Happy");
        Motion underground = FindMotion(motions, "Underground") ?? idle;
        Motion spawn = FindMotion(motions, "Spawn") ?? idle;
        Motion move = FindMotion(motions, "Fly Forward In Place", "Run Forward In Place", "Walk Forward In Place", "Run Forward", "Walk Forward") ?? idle;
        Motion attack = FindMotion(motions, "Bite Attack", "Bite Attack Low", "Projectile Attack", "Projectile Attack Low", "Cast Spell", "Blast Attack", "Wing Attack") ?? idle;
        Motion takeDamage = FindMotion(motions, "Take Damage");
        Motion die = FindMotion(motions, "Die");

        string folder = EnsureFolder("Assets/Animations/Thắng/Pets");
        string controllerPath = folder + "/" + MakeAssetName(petObject.name) + "_PetRuntime.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        }

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);

        AnimatorState undergroundState = AddState(stateMachine, "Underground", underground, new Vector3(250f, 0f, 0f));
        AddState(stateMachine, "Spawn", spawn, new Vector3(250f, 70f, 0f));
        AddState(stateMachine, "Idle", idle, new Vector3(250f, 140f, 0f));
        AddState(stateMachine, "Move", move, new Vector3(250f, 210f, 0f));
        AddState(stateMachine, "Attack", attack, new Vector3(250f, 280f, 0f));

        if (takeDamage != null)
        {
            AddState(stateMachine, "Take Damage", takeDamage, new Vector3(520f, 140f, 0f));
        }

        if (die != null)
        {
            AddState(stateMachine, "Die", die, new Vector3(520f, 210f, 0f));
        }

        stateMachine.defaultState = undergroundState;
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static Dictionary<string, Motion> CollectStateMotions(RuntimeAnimatorController runtimeController)
    {
        Dictionary<string, Motion> motions = new Dictionary<string, Motion>(StringComparer.OrdinalIgnoreCase);
        AnimatorController controller = runtimeController as AnimatorController;
        if (controller != null)
        {
            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                CollectStateMotions(layer.stateMachine, motions);
            }
        }

        foreach (AnimationClip clip in runtimeController.animationClips)
        {
            if (clip != null && !motions.ContainsKey(clip.name))
            {
                motions.Add(clip.name, clip);
            }
        }

        return motions;
    }

    private static void CollectStateMotions(AnimatorStateMachine stateMachine, Dictionary<string, Motion> motions)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            Motion motion = childState.state.motion;
            if (motion != null && !motions.ContainsKey(childState.state.name))
            {
                motions.Add(childState.state.name, motion);
            }
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            CollectStateMotions(childMachine.stateMachine, motions);
        }
    }

    private static Motion FindMotion(Dictionary<string, Motion> motions, params string[] names)
    {
        foreach (string name in names)
        {
            Motion motion;
            if (motions.TryGetValue(name, out motion))
            {
                return motion;
            }
        }

        foreach (string name in names)
        {
            foreach (KeyValuePair<string, Motion> motion in motions)
            {
                if (NormalizeName(motion.Key) == NormalizeName(name))
                {
                    return motion.Value;
                }
            }
        }

        return null;
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (ChildAnimatorStateMachine childMachine in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(childMachine.stateMachine);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        foreach (AnimatorTransition transition in stateMachine.entryTransitions)
        {
            stateMachine.RemoveEntryTransition(transition);
        }
    }

    private static AnimatorState AddState(AnimatorStateMachine stateMachine, string name, Motion motion, Vector3 position)
    {
        AnimatorState state = stateMachine.AddState(name, position);
        state.motion = motion;
        state.writeDefaultValues = true;
        return state;
    }

    private static string EnsureFolder(string folder)
    {
        string[] parts = folder.Split('/');
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

        return current;
    }

    private static string MakeAssetName(string name)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name.Replace(' ', '_');
    }

    private static string NormalizeName(string name)
    {
        return name.Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static void ConfigureAgent(NavMeshAgent agent, GameObject petObject)
    {
        Bounds bounds = CalculateBounds(petObject);
        float xz = Mathf.Max(bounds.size.x, bounds.size.z);

        agent.speed = 5.3f;
        agent.acceleration = 18f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 1.45f;
        agent.radius = Mathf.Clamp(xz * 0.28f, 0.18f, 0.65f);
        agent.height = Mathf.Clamp(bounds.size.y, 0.45f, 3f);
        agent.baseOffset = 0f;
        agent.autoBraking = true;
        agent.updateRotation = false;
    }

    private static DummyEnemy SetupDummyEnemy(Transform player)
    {
        GameObject cube = GameObject.Find("Cube");
        if (cube == null)
        {
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(cube, "Create dummy enemy cube");
            cube.name = "Cube";
            cube.transform.position = player.position + player.forward * 5f + Vector3.up * 0.5f;
        }

        EnsureEnemyTag(cube);

        if (cube.GetComponent<Collider>() == null)
        {
            Undo.AddComponent<BoxCollider>(cube);
        }

        DummyEnemy dummy = GetOrAdd<DummyEnemy>(cube);
        Undo.RecordObject(dummy, "Setup dummy enemy");
        dummy.maxHealth = 100f;
        dummy.autoRevive = true;
        dummy.reviveDelay = 2.5f;
        dummy.writeHealthToName = true;
        return dummy;
    }

    private static void SetupCommandInput(GameObject player, PetController pet, Camera camera)
    {
        PetCommandInput input = GetOrAdd<PetCommandInput>(player);
        Undo.RecordObject(input, "Setup pet command input");
        input.activePet = pet;
        if (input.petSlots == null || input.petSlots.Length != 6)
        {
            input.petSlots = new PetController[6];
        }

        input.petSlots[0] = pet;
        input.commandCamera = camera;
        input.commandMouseButton = 0;
        input.withdrawKey = KeyCode.Backspace;
        input.ignoreWhileRightMouseHeld = false;
        input.allowCommandsWhileRightMouseHeld = true;
        input.aimMouseButton = 1;
        input.moveToGroundWhenNoEnemy = true;
        input.enemySearchRadius = 1.25f;
        input.useScreenCenterWhenCursorLocked = true;
        input.useScreenCenterWhileAiming = false;
    }

    private static GameObject FindPetCandidate(GameObject player)
    {
        GameObject selected = Selection.activeGameObject != null ? GetCandidateRoot(Selection.activeGameObject) : null;
        if (IsPetCandidate(selected, player))
        {
            return selected;
        }

        Dictionary<GameObject, int> candidates = new Dictionary<GameObject, int>();
        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Animator animator in animators)
        {
            GameObject root = GetCandidateRoot(animator.gameObject);
            if (!IsPetCandidate(root, player))
            {
                continue;
            }

            if (!candidates.ContainsKey(root))
            {
                candidates.Add(root, ScorePetCandidate(root));
            }
        }

        GameObject best = null;
        int bestScore = int.MinValue;
        foreach (KeyValuePair<GameObject, int> candidate in candidates)
        {
            if (candidate.Value > bestScore)
            {
                best = candidate.Key;
                bestScore = candidate.Value;
            }
        }

        return best;
    }

    private static GameObject CreateFallbackPet(Transform player)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPetPath);
        if (prefab == null)
        {
            return null;
        }

        GameObject pet = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(pet, "Create fallback pet");
        pet.name = "Pet_WolfPup_Test";
        pet.transform.position = player.position + player.TransformDirection(new Vector3(-1.4f, 0f, -1.6f));
        pet.transform.rotation = player.rotation;
        return pet;
    }

    private static bool IsPetCandidate(GameObject candidate, GameObject player)
    {
        if (candidate == null || candidate == player || !candidate.scene.IsValid())
        {
            return false;
        }

        if (candidate.GetComponentInChildren<BasicPlayerMovement>(true) != null)
        {
            return false;
        }

        if (candidate.GetComponentInChildren<PetController>(true) != null)
        {
            return true;
        }

        Animator animator = candidate.GetComponentInChildren<Animator>(true);
        if (animator == null)
        {
            return false;
        }

        string name = candidate.name.ToLowerInvariant();
        if (name.Contains("camera") || name.Contains("light"))
        {
            return false;
        }

        return true;
    }

    private static int ScorePetCandidate(GameObject candidate)
    {
        int score = 0;
        string assetPath = GetPrefabAssetPath(candidate);
        if (assetPath.Contains("Monsters Ultimate Pack"))
        {
            score += 100;
        }

        if (candidate.GetComponentInChildren<PetController>(true) != null)
        {
            score += 50;
        }

        string name = candidate.name.ToLowerInvariant();
        if (name.Contains("pet") || name.Contains("wolf") || name.Contains("dragon") || name.Contains("cat") || name.Contains("dog"))
        {
            score += 10;
        }

        return score;
    }

    private static GameObject GetCandidateRoot(GameObject gameObject)
    {
        GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(gameObject);
        if (prefabRoot != null)
        {
            return prefabRoot;
        }

        return gameObject.transform.root.gameObject;
    }

    private static string GetPrefabAssetPath(GameObject gameObject)
    {
        GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(gameObject);
        return source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
    }

    private static void EnsureEnemyTag(GameObject cube)
    {
        const string enemyTag = "Enemy";
        try
        {
            if (Array.IndexOf(UnityEditorInternal.InternalEditorUtility.tags, enemyTag) < 0)
            {
                SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                SerializedProperty tags = tagManager.FindProperty("tags");
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = enemyTag;
                tagManager.ApplyModifiedProperties();
            }

            cube.tag = enemyTag;
        }
        catch
        {
            Debug.LogWarning("Could not assign Enemy tag. DummyEnemy component still works for pet targeting.");
        }
    }

    private static void EnsureNavMeshSurface()
    {
        Type surfaceType = FindType("Unity.AI.Navigation.NavMeshSurface");
        if (surfaceType == null)
        {
            Debug.LogWarning("NavMeshSurface type was not found. Pet will use direct-move fallback if no NavMesh is baked.");
            return;
        }

        GameObject surfaceObject = GameObject.Find("Pet Test NavMesh Surface");
        if (surfaceObject == null)
        {
            surfaceObject = new GameObject("Pet Test NavMesh Surface");
            Undo.RegisterCreatedObjectUndo(surfaceObject, "Create pet navmesh surface");
        }

        Component surface = surfaceObject.GetComponent(surfaceType);
        if (surface == null)
        {
            surface = Undo.AddComponent(surfaceObject, surfaceType);
        }

        SetMemberValue(surface, "layerMask", ~0);
        SetEnumMember(surface, "collectObjects", "All");
        SetEnumMember(surface, "useGeometry", "RenderMeshes");

        MethodInfo buildMethod = surfaceType.GetMethod("BuildNavMesh", BindingFlags.Instance | BindingFlags.Public);
        if (buildMethod != null)
        {
            buildMethod.Invoke(surface, null);
        }
    }

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(root.transform.position + Vector3.up * 0.5f, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(gameObject);
    }

    private static Type FindType(string fullName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type type = assembly.GetType(fullName);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static void SetMemberValue(object target, string memberName, object value)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null)
        {
            field.SetValue(target, ConvertValue(value, field.FieldType));
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, ConvertValue(value, property.PropertyType));
        }
    }

    private static object ConvertValue(object value, Type destinationType)
    {
        if (destinationType == typeof(LayerMask) && value is int layerMask)
        {
            return (LayerMask)layerMask;
        }

        return value;
    }

    private static void SetEnumMember(object target, string memberName, string enumName)
    {
        Type type = target.GetType();
        FieldInfo field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum)
        {
            field.SetValue(target, Enum.Parse(field.FieldType, enumName));
            return;
        }

        PropertyInfo property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsEnum)
        {
            property.SetValue(target, Enum.Parse(property.PropertyType, enumName));
        }
    }
}
#endif
