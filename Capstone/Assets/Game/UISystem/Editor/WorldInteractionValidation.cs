using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Capstone.Game.ProfileSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Capstone.Game.UISystem.Editor {
    public static class WorldInteractionValidation {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Profile Runtime And Interaction")]
        public static void Run() {
            var results = new List<string>();
            try {
                ValidateSight(results);
                if (!Application.isPlaying) throw new InvalidOperationException("Sight checks passed; enter Play Mode to validate runtime Profile creation.");
                ValidateProfile(results);
                Directory.CreateDirectory("Temp/ProfileInteraction");
                File.WriteAllLines("Temp/ProfileInteraction/validation.txt", results);
                Debug.Log($"Profile runtime and world interaction: {results.Count} PASS. Profile is open for visual inspection; no save values changed.");
            } catch (Exception error) { Debug.LogException(error); throw; }
        }

        static void ValidateSight(List<string> results) {
            var player = new GameObject("Interaction test player");
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var model = new GameObject("Interaction test active model");
            foreach (var obj in new[] { player, target, wall, model }) obj.hideFlags = HideFlags.HideAndDontSave;
            player.SetActive(false);
            player.transform.position = new Vector3(10000, 10000, 10000);
            player.AddComponent<CharacterController>();
            var movement = player.AddComponent<BasicPlayerMovement>();
            var switcher = player.AddComponent<PlayerVisualSwitcher>();
            model.transform.SetParent(player.transform, false);
            typeof(PlayerVisualSwitcher).GetField("visuals", Private).SetValue(switcher,
                new[] { new PlayerVisualSwitcher.VisualProfile { root = model } });
            typeof(PlayerVisualSwitcher).GetField("activeVisualIndex", Private).SetValue(switcher, 0);
            wall.SetActive(false);
            try {
                void Place(Vector3 offset) { target.transform.position = player.transform.position + offset; Physics.SyncTransforms(); }
                bool Visible() => WorldInteractionView.CanInteract(player.transform, target.transform, 3f);
                Place(new Vector3(0, .5f, 2));
                Check(Visible(), "front target is reachable", results);
                Place(new Vector3(0, .5f, -2));
                Check(!Visible(), "behind model is rejected", results);
                Place(new Vector3(2, .5f, 0));
                Check(!Visible(), "outside view cone is rejected", results);
                Place(new Vector3(1, .5f, 2));
                Check(Visible(), "inside view cone is accepted", results);
                Place(new Vector3(0, .5f, 4));
                Check(!Visible(), "outside reach is rejected", results);
                Place(new Vector3(0, .1f, 1));
                Check(Visible(), "ground item can be seen from eye height", results);
                movement.SetPickupTarget(target.transform);
                var canPickup = typeof(BasicPlayerMovement).GetProperty("CanPickUp", Private);
                Check((bool)canPickup.GetValue(movement), "E pickup uses the visible bound item", results);
                Place(new Vector3(0, .1f, -1));
                Check(!(bool)canPickup.GetValue(movement), "E pickup rejects bound item behind model", results);
                movement.SetPickupTarget(null);
                Check(!(bool)canPickup.GetValue(movement), "E pickup cannot animate without an item", results);
                Place(new Vector3(0, .5f, 2));
                model.transform.localRotation = Quaternion.Euler(0, 180, 0);
                Check(!Visible(), "active model facing overrides player root/camera direction", results);
                model.transform.localRotation = Quaternion.identity;
                wall.transform.position = player.transform.position + new Vector3(0, 1, 1);
                wall.transform.localScale = new Vector3(3, 3, .2f);
                wall.SetActive(true); Physics.SyncTransforms();
                Check(!Visible(), "wall blocks interaction", results);
                wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
                Check(Visible(), "trigger volume does not hide a target", results);
                target.SetActive(false);
                Check(!Visible(), "inactive pickup is rejected", results);
            } finally {
                Object.DestroyImmediate(wall); Object.DestroyImmediate(target); Object.DestroyImmediate(player);
            }
        }

        static void ValidateProfile(List<string> results) {
            var profile = Object.FindFirstObjectByType<ProfilePanelController>(FindObjectsInactive.Include);
            Check(profile != null, "runtime Profile controller exists", results);
            var initialRoot = (RectTransform)typeof(ProfilePanelController).GetField("root", Private).GetValue(profile);
            Check(initialRoot != null && initialRoot.Find("ProfileScreen/PlayerInfoPanel") != null
                && initialRoot.Find("ProfileScreen/StatsOverviewPanel") != null,
                "Awake built the complete Profile before validation", results);
            // Exercise runtime AddComponent (Editor Reset masked the original crash).
            profile.RebuildPanel();
            Object.FindFirstObjectByType<GameMenuController>().OpenProfileDirect();
            var root = (RectTransform)typeof(ProfilePanelController).GetField("root", Private).GetValue(profile);
            var design = root.Find("ProfileScreen");
            Check(profile.IsOpen, "Profile opens through Trainer Menu", results);
            foreach (string path in new[] { "MainProfilePanel/BasicInfoSection", "MainProfilePanel/CurrentTeamSection", "PlayerInfoPanel", "StatsOverviewPanel" })
                Check(design.Find(path) != null, "runtime section built: " + path, results);
            Check(design.Find("MainProfilePanel/CurrentTeamSection").GetComponentsInChildren<Button>(true).Length == 6,
                "runtime team has all six slots", results);
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
                Check(selectable.targetGraphic != null, "runtime target graphic assigned: " + selectable.name, results);
            foreach (string field in new[] { "displayNameText", "playerIdValue", "playTimeValue", "currentAreaValue", "totalBattlesValue" }) {
                var text = (Text)typeof(ProfilePanelController).GetField(field, Private).GetValue(profile);
                Check(text != null && !string.IsNullOrWhiteSpace(text.text), "runtime data bound: " + field, results);
            }
            Canvas.ForceUpdateCanvases();
        }

        static void Check(bool valid, string message, List<string> results) {
            if (!valid) throw new InvalidOperationException(message);
            results.Add("PASS " + message);
        }
    }
}
