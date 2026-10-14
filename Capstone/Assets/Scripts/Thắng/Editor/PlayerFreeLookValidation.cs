using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlayerFreeLookValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/ToolCuaThang/Camera/Validate TestChar FreeLook")]
    public static void Validate()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run this check outside Play Mode.");
        var results = new List<string>();
        int failures = 0;
        void Check(bool valid, string label)
        {
            results.Add((valid ? "PASS " : "FAIL ") + label);
            if (!valid) failures++;
        }

        var camera = Camera.main;
        var source = camera != null ? camera.GetComponent<BasicCameraFollow>() : null;
        var rig = source != null ? source.freeLookRig : null;
        Check(camera != null && camera.gameObject.scene.path == TestCharFreeLookSetup.ScenePath, "TestChar render camera retained");
        Check(rig != null && rig.IsReady && rig.source == source && rig.brain.gameObject == camera.gameObject, "scene rig references resolve");
        if (rig == null || !rig.IsReady) throw new InvalidOperationException(string.Join("\n", results));
        Check(rig.brain.enabled && rig.brain.UpdateMethod == CinemachineBrain.UpdateMethods.ManualUpdate, "one manually updated Cinemachine brain");
        Check(rig.orbit.OrbitStyle == CinemachineOrbitalFollow.OrbitStyles.ThreeRing, "three authored FreeLook orbits");
        Check(!rig.orbit.HorizontalAxis.Recentering.Enabled && !rig.orbit.VerticalAxis.Recentering.Enabled, "no automatic recenter");
        Check(rig.freeCamera.LookAt == rig.lookTarget && rig.lockCamera.LookAt == rig.lookTarget
            && rig.freeCamera.Follow == rig.orbitTarget && rig.lockCamera.Follow == rig.lockTarget, "camera targets are authored scene objects");
        Check(rig.freeCamera.GetComponent<CinemachineInputAxisController>() == null
            && rig.lockCamera.GetComponent<CinemachineInputAxisController>() == null, "no second input reader");
        Check(rig.freeCamera.GetComponent<PlayerCameraCollision>().rig == rig
            && rig.lockCamera.GetComponent<PlayerCameraCollision>().rig == rig, "both camera pipelines share collision filter");
        Check(source.minZoomDistance == 3f && source.maxZoomDistance == 7f && source.mouseSensitivity == 2.2f, "TestChar zoom and sensitivity retained");
        Check(source.lockMouseButton == 2 && source.lockBreakDistance == 35f && !source.rightMouseZoomsCamera, "M3/range/right mouse behavior retained");

        // Isolated fixture: no live player, inventory or save data is read/changed.
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var cursorLock = Cursor.lockState;
        var cursorVisible = Cursor.visible;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var root = new GameObject("Camera validation fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        var enemyRegistry = (HashSet<DummyEnemy>)typeof(DummyEnemy).GetField("activeEnemies", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        try
        {
            var go = Child(root.transform, "Render camera");
            var testCamera = go.AddComponent<Camera>();
            testCamera.enabled = false;
            var test = go.AddComponent<BasicCameraFollow>();
            test.enabled = false;
            var player = Child(root.transform, "Player").transform;
            test.target = player;
            Vector3 origin = new Vector3(10000f, 10000f, 10000f);
            player.position = origin;
            go.transform.position = origin;
            go.transform.rotation = Quaternion.Euler(0f, 70f, 0f);
            test.freeLookRig = go.AddComponent<PlayerFreeLookRig>();
            Check(Vector3.Angle(test.PlanarForward, go.transform.forward) < 0.01f, "movement reads the displayed camera heading");
            var obstacle = Child(root.transform, "Wall");
            var collider = obstacle.AddComponent<BoxCollider>();
            obstacle.transform.position = origin + Vector3.back * 2f;
            collider.size = new Vector3(3f, 3f, 0.2f);
            // An additive scene shares default physics without changing TestChar.
            Physics.SyncTransforms();
            var desired = origin + Vector3.back * 5f;
            Check(Vector3.Distance(origin, test.ConstrainCameraPosition(origin, desired)) < 2f, "solid wall retracts camera");
            collider.isTrigger = true;
            Physics.SyncTransforms();
            Check(Vector3.Distance(desired, test.ConstrainCameraPosition(origin, desired)) < 0.001f, "triggers do not obstruct camera");
            collider.isTrigger = false;
            obstacle.transform.SetParent(player, true);
            Physics.SyncTransforms();
            Check(Vector3.Distance(desired, test.ConstrainCameraPosition(origin, desired)) < 0.001f, "player collider ignored");
            obstacle.transform.SetParent(root.transform, true);
            var pet = obstacle.AddComponent<PetController>();
            pet.enabled = false;
            Physics.SyncTransforms();
            Check(Vector3.Distance(desired, test.ConstrainCameraPosition(origin, desired)) < 0.001f, "pet collider ignored");
            UnityEngine.Object.DestroyImmediate(pet);
            test.enableCameraCollision = false;
            Check(test.ConstrainCameraPosition(origin, desired) == desired, "collision setting respected");

            // Freeze uses the same input-lock method used by Menu/Inventory.
            test.SetGameplayInputLocked(true);
            float yaw = (float)typeof(BasicCameraFollow).GetField("yaw", Private).GetValue(test);
            typeof(BasicCameraFollow).GetMethod("UpdateFreeLookRig", Private).Invoke(test, null);
            Check((float)typeof(BasicCameraFollow).GetField("yaw", Private).GetValue(test) == yaw,
                "UI lock does not tick camera or lock tracking");
            test.SetGameplayInputLocked(false);
            typeof(BasicCameraFollow).GetMethod("OnApplicationFocus", Private).Invoke(test, new object[] { false });
            Check((bool)typeof(BasicCameraFollow).GetMethod("IsGameplayInputBlocked", Private).Invoke(test, null), "lost application focus blocks camera input");
            typeof(BasicCameraFollow).GetMethod("OnApplicationFocus", Private).Invoke(test, new object[] { true });
            Check((float)typeof(BasicCameraFollow).GetField("ignoreMouseUntil", Private).GetValue(test) > Time.unscaledTime,
                "returning focus suppresses cursor warp input");

            var enemy = Child(root.transform, "Enemy").AddComponent<DummyEnemy>();
            enemy.transform.position = origin + Vector3.forward * 10f;
            var lockField = typeof(BasicCameraFollow).GetField("lockedEnemy", Private);
            lockField.SetValue(test, enemy);
            Check(test.TryGetLockedEnemy(out var selected) && selected == enemy, "pin API exposes the exact live target");
            // Inactive fixture avoids awakening UI, player discovery or save systems.
            var menuObject = Child(root.transform, "Trainer menu fixture");
            menuObject.SetActive(false);
            var menu = menuObject.AddComponent<Capstone.Game.UISystem.GameMenuController>();
            var menuRoot = new GameObject("Menu root", typeof(RectTransform));
            menuRoot.transform.SetParent(menuObject.transform, false);
            typeof(Capstone.Game.UISystem.GameMenuController).GetField("root", Private).SetValue(menu, menuRoot.GetComponent<RectTransform>());
            object otherPanel = new object();
            try {
                Capstone.Game.Inventory.InventoryInputController.SetExternalGameplayInputBlocked(menu, true);
                test.SetGameplayInputLocked(true); // LocalPlayerControlLock keeps camera input locked.
                Check(Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked
                    && Capstone.Game.Inventory.InventoryInputController.CanTrackCameraWhileGameplayBlocked,
                    "Trainer Menu blocks gameplay while allowing passive camera tracking");
                var yawField = typeof(BasicCameraFollow).GetField("yaw", Private);
                yawField.SetValue(test, 90f);
                typeof(BasicCameraFollow).GetMethod("UpdateFreeLookRig", Private).Invoke(test, null);
                Check((float)yawField.GetValue(test) < 90f && test.TryGetLockedEnemy(out selected) && selected == enemy,
                    "pinned camera keeps tracking while Trainer Menu is open");
                Capstone.Game.Inventory.InventoryInputController.SetExternalGameplayInputBlocked(otherPanel, true);
                float heldYaw = (float)yawField.GetValue(test);
                typeof(BasicCameraFollow).GetMethod("UpdateFreeLookRig", Private).Invoke(test, null);
                Check(!Capstone.Game.Inventory.InventoryInputController.CanTrackCameraWhileGameplayBlocked
                    && (float)yawField.GetValue(test) == heldYaw, "another UI panel still freezes camera tracking");
                Capstone.Game.Inventory.InventoryInputController.SetExternalGameplayInputBlocked(otherPanel, false);
                typeof(BasicCameraFollow).GetMethod("OnApplicationFocus", Private).Invoke(test, new object[] { false });
                typeof(BasicCameraFollow).GetMethod("UpdateFreeLookRig", Private).Invoke(test, null);
                Check((float)yawField.GetValue(test) == heldYaw, "losing focus freezes camera even with Trainer Menu open");
                var movingObject = Child(menuObject.transform, "Coasting player");
                var moving = movingObject.AddComponent<BasicPlayerMovement>();
                moving.useRootMotion = false;
                var velocity = typeof(BasicPlayerMovement).GetField("horizontalVelocity", Private);
                velocity.SetValue(moving, Vector3.forward * 3f);
                moving.SetGameplayInputLocked(true);
                var step = typeof(BasicPlayerMovement).GetMethod("BuildHorizontalDelta", Private);
                step.Invoke(moving, new object[] { .05f });
                Check(((Vector3)velocity.GetValue(moving)).magnitude > 0f
                    && ((Vector3)velocity.GetValue(moving)).magnitude < 3f, "opening Menu decelerates existing movement instead of freezing it");
                for (int i = 0; i < 20; i++) step.Invoke(moving, new object[] { .05f });
                Check(((Vector3)velocity.GetValue(moving)).sqrMagnitude == 0f, "player comes to a complete stop with Menu open");
            } finally {
                Capstone.Game.Inventory.InventoryInputController.SetExternalGameplayInputBlocked(otherPanel, false);
                Capstone.Game.Inventory.InventoryInputController.SetExternalGameplayInputBlocked(menu, false);
                test.SetGameplayInputLocked(false);
                typeof(BasicCameraFollow).GetMethod("OnApplicationFocus", Private).Invoke(test, new object[] { true });
            }
            var alternate = Child(root.transform, "Alternate enemy").AddComponent<DummyEnemy>();
            alternate.transform.position = enemy.transform.position + Vector3.right * 2f;
            enemyRegistry.Add(enemy);
            enemyRegistry.Add(alternate);
            DummyEnemy notified = null;
            test.EnemyPinned += pinned => notified = pinned;
            bool switched = (bool)typeof(BasicCameraFollow).GetMethod("TrySwitchLockedEnemy", Private).Invoke(test, new object[] { 1f });
            Check(switched && test.TryGetLockedEnemy(out selected) && selected == alternate && notified == alternate,
                "wheel target change broadcasts selection without issuing a combat command");
            lockField.SetValue(test, enemy);
            typeof(DummyEnemy).GetField("alive", Private).SetValue(enemy, false);
            typeof(BasicCameraFollow).GetMethod("UpdateEnemyLockInput", Private).Invoke(test, null);
            Check(lockField.GetValue(test) == null, "dead target clears instead of silently repinning");
            typeof(DummyEnemy).GetField("alive", Private).SetValue(enemy, true);
            enemy.transform.position = origin + Vector3.forward * 36f;
            lockField.SetValue(test, enemy);
            typeof(BasicCameraFollow).GetMethod("UpdateLockedEnemyTracking", Private).Invoke(test, new object[] { origin, 0.016f });
            Check(lockField.GetValue(test) == null, "target beyond 35m breaks pin");
        }
        finally
        {
            foreach (var enemy in root.GetComponentsInChildren<DummyEnemy>()) enemyRegistry.Remove(enemy);
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(scene, true);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(originalScene);
            Cursor.lockState = cursorLock;
            Cursor.visible = cursorVisible;
        }
        Directory.CreateDirectory("Temp/TestCharFreeLook20261007");
        File.WriteAllLines("Temp/TestCharFreeLook20261007/validation.txt", results);
        if (failures > 0) throw new InvalidOperationException($"FreeLook validation: {failures} failures.\n" + string.Join("\n", results));
        Debug.Log($"FreeLook validation: {results.Count} PASS, 0 FAIL. No save data changed.");
    }

    static GameObject Child(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }
}
