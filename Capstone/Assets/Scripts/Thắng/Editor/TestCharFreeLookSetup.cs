using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TestCharFreeLookSetup
{
    public const string ScenePath = "Assets/Scenes/TestChar.unity";

    [MenuItem("Tools/ToolCuaThang/Camera/Setup TestChar FreeLook")]
    public static void SetupActiveScene()
    {
        if (Application.isPlaying || EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            Debug.LogError("Open TestChar outside Play Mode before setting up FreeLook.");
            return;
        }
        var camera = Camera.main;
        var player = Object.FindFirstObjectByType<BasicPlayerMovement>();
        if (camera == null || player == null) throw new System.InvalidOperationException("TestChar camera/player missing.");
        Ensure(camera, player.transform);
        EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
        EditorSceneManager.SaveScene(camera.gameObject.scene);
        Debug.Log("TestChar FreeLook saved. Edit Orbital Follow > Orbits and Rotation Composer > Screen Position.");
    }

    public static void Ensure(Camera camera, Transform player)
    {
        if (camera.gameObject.scene.path != ScenePath) return;
        var source = GetOrAdd<BasicCameraFollow>(camera.gameObject);
        Undo.RecordObject(source, "Connect FreeLook gameplay");
        source.target = player;
        source.enabled = true;

        var rig = GetOrAdd<PlayerFreeLookRig>(camera.gameObject);
        var brain = GetOrAdd<CinemachineBrain>(camera.gameObject);
        Undo.RecordObjects(new Object[] { rig, brain }, "Connect FreeLook brain");
        rig.source = source;
        rig.brain = brain;
        brain.enabled = true;
        brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
        brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.18f);
        source.freeLookRig = rig;
        source.dynamicLockSideFromMovement = false;
        source.lockCameraOffset.x = Mathf.Max(.05f, Mathf.Abs(source.lockCameraOffset.x));

        // Subsequent setup calls preserve authored orbits, composition and damping.
        if (!rig.IsReady)
        {
            var root = Create("TestChar Camera Rig", null).transform;
            rig.orbitTarget = Create("Orbit Target", root).transform;
            rig.lockTarget = Create("Lock Target", root).transform;
            rig.lookTarget = Create("Look Target", root).transform;
            rig.orbitTarget.position = player.position;
            rig.lockTarget.position = player.position;
            rig.lookTarget.position = player.position + Vector3.up * source.lookHeight;

            rig.freeCamera = CreateCamera("FreeLook Camera", root, camera, rig.orbitTarget, rig.lookTarget, rig);
            rig.freeCamera.Priority = 10;
            rig.orbit = Undo.AddComponent<CinemachineOrbitalFollow>(rig.freeCamera.gameObject);
            rig.orbit.OrbitStyle = CinemachineOrbitalFollow.OrbitStyles.ThreeRing;
            rig.orbit.Orbits = new Cinemachine3OrbitRig.Settings
            {
                Top = new Cinemachine3OrbitRig.Orbit { Radius = 2.2f, Height = 4.5f },
                Center = new Cinemachine3OrbitRig.Orbit { Radius = 4f, Height = 3f },
                Bottom = new Cinemachine3OrbitRig.Orbit { Radius = 4.99f, Height = 0.35f },
                SplineCurvature = 0.5f
            };
            rig.orbit.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            rig.orbit.TrackerSettings.PositionDamping = new Vector3(0.12f, 0.08f, 0.12f);
            rig.orbit.HorizontalAxis.Range = new Vector2(-180f, 180f);
            rig.orbit.HorizontalAxis.Wrap = true;
            rig.orbit.HorizontalAxis.Recentering.Enabled = false;
            rig.orbit.VerticalAxis.Range = new Vector2(-30f, 38f);
            rig.orbit.VerticalAxis.Value = 12.5f;
            rig.orbit.VerticalAxis.Recentering.Enabled = false;
            rig.orbit.RadialAxis.Range = new Vector2(0.05f, 10f);
            rig.orbit.RadialAxis.Recentering.Enabled = false;

            rig.lockCamera = CreateCamera("Pet Combat Camera", root, camera, rig.lockTarget, rig.lookTarget, rig);
            rig.lockCamera.Priority = 0;
            rig.lockFollow = Undo.AddComponent<CinemachineFollow>(rig.lockCamera.gameObject);
            rig.lockFollow.TrackerSettings.BindingMode = BindingMode.LockToTargetWithWorldUp;
            rig.lockFollow.TrackerSettings.PositionDamping = new Vector3(0.12f, 0.08f, 0.12f);
            rig.lockFollow.FollowOffset = source.lockCameraOffset + Vector3.up * source.lockCameraHeightBonus;
        }
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(rig);
        EditorUtility.SetDirty(brain);
    }

    static CinemachineCamera CreateCamera(string name, Transform root, Camera renderCamera,
        Transform follow, Transform look, PlayerFreeLookRig rig)
    {
        var go = Create(name, root);
        var camera = Undo.AddComponent<CinemachineCamera>(go);
        camera.Follow = follow;
        camera.LookAt = look;
        camera.Lens = LensSettings.FromCamera(renderCamera);
        camera.StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        var composer = Undo.AddComponent<CinemachineRotationComposer>(go);
        composer.Damping = new Vector2(0.18f, 0.18f);
        composer.Composition.ScreenPosition = Vector2.zero;
        composer.Composition.DeadZone.Enabled = false;
        composer.Composition.HardLimits.Enabled = false;
        composer.CenterOnActivate = true;
        var collision = Undo.AddComponent<PlayerCameraCollision>(go);
        collision.rig = rig;
        return camera;
    }

    static GameObject Create(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create scene FreeLook");
        go.transform.SetParent(parent, false);
        return go;
    }

    static T GetOrAdd<T>(GameObject go) where T : Component
        => go.GetComponent<T>() ?? Undo.AddComponent<T>(go);
}
