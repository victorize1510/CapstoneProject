using System.Collections.Generic;
using Capstone.Game.HudSystem;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DragonDuskPetSetup
{
    private const string RuntimeControllerPath = "Assets/Animations/Thắng/Pets/Dragon_Dusk_PetRuntime.controller";
    private const string SourceFbxRoot = "Assets/UnityAssset/Trung/Monsters Ultimate Pack 05 Cute Series/Dragon Dusk Cute Series/FBX";

    private static readonly StateSpec[] StateSpecs =
    {
        new StateSpec("Idle", "Dragon Dusk@Idle.FBX", "Idle", 1f),
        new StateSpec("Move", "Dragon Dusk@Fly Forward In Place.FBX", "Fly Forward In Place", 1f),
        new StateSpec("Fly Forward In Place", "Dragon Dusk@Fly Forward In Place.FBX", "Fly Forward In Place", 1f),
        new StateSpec("Fly Forward W Root", "Dragon Dusk@Fly Forward W Root.FBX", "Fly Forward W Root", 1f),
        new StateSpec("Attack", "Dragon Dusk@Bite Attack.FBX", "Bite Attack", 1f),
        new StateSpec("Bite Attack", "Dragon Dusk@Bite Attack.FBX", "Bite Attack", 1f),
        new StateSpec("Bite Attack Low", "Dragon Dusk@Bite Attack Low.FBX", "Bite Attack Low", 1f),
        new StateSpec("Blast Attack", "Dragon Dusk@Blast Attack.FBX", "Blast Attack", 1f),
        new StateSpec("Cast Spell", "Dragon Dusk@Cast Spell.FBX", "Cast Spell", 1f),
        new StateSpec("Projectile Attack", "Dragon Dusk@Projectile Attack.FBX", "Projectile Attack", 1f),
        new StateSpec("Projectile Attack Low", "Dragon Dusk@Projectile Attack Low.FBX", "Projectile Attack Low", 1f),
        new StateSpec("Wing Attack", "Dragon Dusk@Wing Attack.FBX", "Wing Attack", 1f),
        new StateSpec("Spawn", "Dragon Dusk@Spawn.FBX", "Spawn", 1f),
        new StateSpec("Underground", "Dragon Dusk@Underground.FBX", "Underground", 1f),
        new StateSpec("Take Damage", "Dragon Dusk@Take Damage.FBX", "Take Damage", 1f),
        new StateSpec("Die", "Dragon Dusk@Die.FBX", "Die", 1f),
        new StateSpec("Turn Left", "Dragon Dusk@Turn Left.FBX", "Turn Left", 1f),
        new StateSpec("Turn Right", "Dragon Dusk@Turn Right.FBX", "Turn Right", 1f)
    };

    private static readonly SkillSpec[] SkillSpecs =
    {
        new SkillSpec("Bite", "Close-range bite attack.", 1, 1.2f, 0.8f, 0.08f, 0.1f, 0.3f, "Bite Attack", "Bite Attack Low"),
        new SkillSpec("Blast", "Short magic blast.", 1, 3.8f, 1.0f, 0.08f, 0.1f, 0.3f, "Blast Attack", "Cast Spell"),
        new SkillSpec("Projectile", "Ranged projectile attack.", 1, 3.0f, 0.95f, 0.08f, 0.1f, 0.3f, "Projectile Attack", "Projectile Attack Low"),
        new SkillSpec("Wing Strike", "Wing strike attack.", 1, 4.2f, 1.0f, 0.08f, 0.1f, 0.3f, "Wing Attack")
    };

    [MenuItem("Tools/ToolCuaThang/Capstone/Pet/Rebuild Dragon Dusk Runtime Animator")]
    public static void RebuildRuntimeAnimator()
    {
        AnimatorController controller = LoadOrCreateController();
        if (controller == null) return;

        Undo.RecordObject(controller, "Rebuild Dragon Dusk runtime animator");
        EnsureSingleBaseLayer(controller);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        ClearStateMachine(stateMachine);
        controller.parameters = new AnimatorControllerParameter[0];

        AnimatorState defaultState = null;
        int index = 0;
        foreach (StateSpec spec in StateSpecs)
        {
            AnimationClip clip = LoadClip(spec);
            if (clip == null)
            {
                Debug.LogWarning("Dragon Dusk setup skipped missing clip: " + spec.ClipPath);
                continue;
            }

            Vector3 position = new Vector3(220f + (index % 3) * 240f, 80f + (index / 3) * 70f, 0f);
            AnimatorState state = stateMachine.AddState(spec.StateName, position);
            state.motion = clip;
            state.speed = spec.Speed;
            state.writeDefaultValues = true;

            if (spec.StateName == "Idle")
            {
                defaultState = state;
            }

            index++;
        }

        if (defaultState != null)
        {
            stateMachine.defaultState = defaultState;
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Dragon Dusk runtime animator rebuilt: " + RuntimeControllerPath);
    }

    [MenuItem("Tools/ToolCuaThang/Capstone/Pet/Setup Dragon Dusk In Open Scene")]
    public static void SetupDragonDuskInOpenScene()
    {
        RebuildRuntimeAnimator();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RuntimeControllerPath);
        if (controller == null)
        {
            Debug.LogError("Dragon Dusk runtime controller was not found: " + RuntimeControllerPath);
            return;
        }

        List<GameObject> pets = FindDragonDuskSceneObjects();
        if (pets.Count == 0)
        {
            Debug.LogWarning("No Dragon Dusk object found in the open scene.");
            return;
        }

        foreach (GameObject petRoot in pets)
        {
            SetupPetObject(petRoot, controller);
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Dragon Dusk setup finished for " + pets.Count + " scene object(s).");
    }

    [MenuItem("Tools/ToolCuaThang/Capstone/Pet/Setup Selected Dragon Dusk")]
    public static void SetupSelectedDragonDusk()
    {
        RebuildRuntimeAnimator();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RuntimeControllerPath);
        if (controller == null)
        {
            Debug.LogError("Dragon Dusk runtime controller was not found: " + RuntimeControllerPath);
            return;
        }

        int changed = 0;
        foreach (GameObject selected in Selection.gameObjects)
        {
            if (selected == null) continue;
            SetupPetObject(selected, controller);
            changed++;
        }

        if (changed == 0)
        {
            Debug.LogWarning("Select a Dragon Dusk object first.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Dragon Dusk setup finished for selected object(s): " + changed);
    }

    private static void SetupPetObject(GameObject petRoot, AnimatorController controller)
    {
        Undo.RegisterFullObjectHierarchyUndo(petRoot, "Setup Dragon Dusk pet");

        Animator animator = petRoot.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            EditorUtility.SetDirty(animator);
        }

        PetController petController = petRoot.GetComponent<PetController>();
        if (petController == null)
        {
            petController = Undo.AddComponent<PetController>(petRoot);
        }

        petController.animator = animator;
        petController.undergroundStates = new[] { "Underground", "Idle" };
        petController.spawnStates = new[] { "Spawn", "Idle" };
        petController.idleStates = new[] { "Idle" };
        petController.moveStates = new[] { "Move", "Fly Forward In Place" };
        petController.attackStates = new[] { "Attack", "Bite Attack", "Bite Attack Low", "Projectile Attack", "Projectile Attack Low", "Cast Spell", "Blast Attack", "Wing Attack" };
        petController.locomotionFade = 0.14f;
        petController.attackFade = 0.08f;
        EditorUtility.SetDirty(petController);

        PetHudRuntimeStats stats = petRoot.GetComponent<PetHudRuntimeStats>();
        if (stats == null)
        {
            stats = Undo.AddComponent<PetHudRuntimeStats>(petRoot);
        }

        ApplyRuntimeStats(stats, petController, animator);
        PrefabUtility.RecordPrefabInstancePropertyModifications(petRoot);
    }

    private static void ApplyRuntimeStats(PetHudRuntimeStats stats, PetController petController, Animator animator)
    {
        SerializedObject serialized = new SerializedObject(stats);

        SetString(serialized, "displayName", "Dragon Dusk");
        SetBool(serialized, "usePrototypeSkillsWhenEmpty", true);
        SetObject(serialized, "petController", petController);
        SetObject(serialized, "animator", animator);
        SetBool(serialized, "autoFindAnimationReferences", true);
        SetBool(serialized, "stopMovementDuringSkill", true);
        SetBool(serialized, "faceTargetDuringSkill", true);

        SerializedProperty skills = serialized.FindProperty("skills");
        if (skills != null)
        {
            skills.arraySize = SkillSpecs.Length;
            for (int i = 0; i < SkillSpecs.Length; i++)
            {
                ApplySkill(skills.GetArrayElementAtIndex(i), SkillSpecs[i]);
            }
        }

        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(stats);
    }

    private static void ApplySkill(SerializedProperty skillProperty, SkillSpec spec)
    {
        skillProperty.FindPropertyRelative("unlocked").boolValue = true;
        skillProperty.FindPropertyRelative("usable").boolValue = true;
        skillProperty.FindPropertyRelative("displayName").stringValue = spec.DisplayName;
        skillProperty.FindPropertyRelative("skillLevel").intValue = spec.SkillLevel;
        skillProperty.FindPropertyRelative("description").stringValue = spec.Description;
        skillProperty.FindPropertyRelative("animationDuration").floatValue = spec.AnimationDuration;
        skillProperty.FindPropertyRelative("animationFade").floatValue = spec.AnimationFade;
        skillProperty.FindPropertyRelative("windupSeconds").floatValue = spec.WindupSeconds;
        skillProperty.FindPropertyRelative("recoverySeconds").floatValue = spec.RecoverySeconds;
        skillProperty.FindPropertyRelative("cooldownPercent").floatValue = 0f;
        skillProperty.FindPropertyRelative("cooldownSeconds").floatValue = spec.CooldownSeconds;

        SerializedProperty states = skillProperty.FindPropertyRelative("animatorStates");
        states.arraySize = spec.AnimatorStates.Length;
        for (int i = 0; i < spec.AnimatorStates.Length; i++)
        {
            states.GetArrayElementAtIndex(i).stringValue = spec.AnimatorStates[i];
        }
    }

    private static AnimatorController LoadOrCreateController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(RuntimeControllerPath);
        if (controller != null) return controller;

        string folder = System.IO.Path.GetDirectoryName(RuntimeControllerPath);
        if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
        {
            System.IO.Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
        }

        controller = AnimatorController.CreateAnimatorControllerAtPath(RuntimeControllerPath);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void EnsureSingleBaseLayer(AnimatorController controller)
    {
        if (controller.layers != null && controller.layers.Length > 0) return;

        AnimatorControllerLayer layer = new AnimatorControllerLayer
        {
            name = "Base Layer",
            defaultWeight = 1f,
            stateMachine = new AnimatorStateMachine()
        };

        layer.stateMachine.name = "Base Layer";
        AssetDatabase.AddObjectToAsset(layer.stateMachine, controller);
        controller.layers = new[] { layer };
    }

    private static void ClearStateMachine(AnimatorStateMachine stateMachine)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            stateMachine.RemoveState(child.state);
        }

        foreach (ChildAnimatorStateMachine child in stateMachine.stateMachines)
        {
            stateMachine.RemoveStateMachine(child.stateMachine);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }
    }

    private static AnimationClip LoadClip(StateSpec spec)
    {
        string path = spec.ClipPath;
        Object[] assets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
        foreach (Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && clip.name == spec.ClipName)
            {
                return clip;
            }
        }

        foreach (Object asset in assets)
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null)
            {
                return clip;
            }
        }

        return null;
    }

    private static List<GameObject> FindDragonDuskSceneObjects()
    {
        List<GameObject> results = new List<GameObject>();
        Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform transform in transforms)
        {
            if (transform == null || transform.gameObject == null) continue;
            if (!transform.gameObject.scene.IsValid()) continue;
            if (EditorUtility.IsPersistent(transform.gameObject)) continue;
            if (!transform.name.ToLowerInvariant().Contains("dragon dusk")) continue;

            Transform root = transform.root;
            GameObject candidate = root != null ? root.gameObject : transform.gameObject;
            if (!results.Contains(candidate))
            {
                results.Add(candidate);
            }
        }

        return results;
    }

    private static void SetString(SerializedObject serialized, string propertyName, string value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.stringValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.boolValue = value;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null) property.objectReferenceValue = value;
    }

    private readonly struct StateSpec
    {
        public readonly string StateName;
        public readonly string FbxName;
        public readonly string ClipName;
        public readonly float Speed;

        public StateSpec(string stateName, string fbxName, string clipName, float speed)
        {
            StateName = stateName;
            FbxName = fbxName;
            ClipName = clipName;
            Speed = speed;
        }

        public string ClipPath => SourceFbxRoot + "/" + FbxName;
    }

    private readonly struct SkillSpec
    {
        public readonly string DisplayName;
        public readonly string Description;
        public readonly int SkillLevel;
        public readonly float CooldownSeconds;
        public readonly float AnimationDuration;
        public readonly float AnimationFade;
        public readonly float WindupSeconds;
        public readonly float RecoverySeconds;
        public readonly string[] AnimatorStates;

        public SkillSpec(string displayName, string description, int skillLevel, float cooldownSeconds, float animationDuration, float animationFade, float windupSeconds, float recoverySeconds, params string[] animatorStates)
        {
            DisplayName = displayName;
            Description = description;
            SkillLevel = skillLevel;
            CooldownSeconds = cooldownSeconds;
            AnimationDuration = animationDuration;
            AnimationFade = animationFade;
            WindupSeconds = windupSeconds;
            RecoverySeconds = recoverySeconds;
            AnimatorStates = animatorStates;
        }
    }
}
