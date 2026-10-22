using System;
using System.Collections.Generic;
using System.Reflection;
using Capstone.Game.Inventory;
using Capstone.Game.MapSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Capstone.Game.HudSystem.Editor {
    public static class GameplayHudValidation {
        static Action stopCombatWatch;

        [MenuItem("Game Tools/GameToolThang/HUD/Watch Next M3 Attack")]
        public static void WatchNextPinnedAttack() {
            Require(Application.isPlaying, "Run the M3 combat check in Play Mode.");
            var input = UnityEngine.Object.FindFirstObjectByType<PetCommandInput>();
            Require(input != null && input.CanUsePetSkills && input.cameraLock != null,
                "Spawn a pet and wait until it is ready before checking M3.");
            stopCombatWatch?.Invoke();
            BasicCameraFollow camera = input.cameraLock;
            DummyEnemy enemy = null;
            PetController pet = null;
            float initialHealth = 0;
            var health = typeof(DummyEnemy).GetField("health", BindingFlags.Instance | BindingFlags.NonPublic);
            var target = typeof(PetController).GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
            double deadline = EditorApplication.timeSinceStartup + 45;
            Action<DummyEnemy> onPin = pinned => {
                enemy = pinned; pet = input.activePet;
                initialHealth = (float)health.GetValue(enemy);
                if (pet == null || !pet.HasCombatTarget || (DummyEnemy)target.GetValue(pet) != enemy) {
                    Debug.LogError("M3 pinned an enemy but did not send that exact target to the pet.");
                    stopCombatWatch?.Invoke();
                    return;
                }
                deadline = EditorApplication.timeSinceStartup + 20;
                Debug.Log("M3 target command verified; watching for actual damage to " + enemy.name);
            };
            EditorApplication.CallbackFunction tick = () => {
                if (!Application.isPlaying) { stopCombatWatch?.Invoke(); return; }
                if (enemy != null && (float)health.GetValue(enemy) < initialHealth) {
                    Debug.Log("M3 combat validation passed: pin sent the exact target, pet approached and damaged " + enemy.name);
                    stopCombatWatch?.Invoke();
                } else if (EditorApplication.timeSinceStartup >= deadline) {
                    Debug.LogError("M3 combat validation timed out: " + (enemy == null ? "no new pin received." : "no damage reached the pinned enemy."));
                    stopCombatWatch?.Invoke();
                }
            };
            stopCombatWatch = () => {
                if (camera != null) camera.EnemyPinned -= onPin;
                EditorApplication.update -= tick;
                stopCombatWatch = null;
            };
            camera.EnemyPinned += onPin;
            EditorApplication.update += tick;
            Debug.Log("M3 combat check armed: unpin first if needed, then pin a nearby enemy with M3.");
        }

        [MenuItem("Game Tools/GameToolThang/HUD/Validate Gameplay HUD")]
        public static void Validate() {
            var hud = UnityEngine.Object.FindFirstObjectByType<GameplayHudController>();
            Require(hud != null, "Missing GameplayHudController.");
            var rootObject = GameObject.Find("GameplayHUDRoot");
            Require(rootObject != null, "HUD must be visible; close menus before validating.");
            var root = rootObject.GetComponent<RectTransform>();
            string[] modules = { "MiniMap", "ActivePetInfo", "PartyCarousel", "SkillBar", "QuickItems" };
            Require(root.childCount == modules.Length, "Expected exactly five HUD modules.");
            var bounds = new Rect[5]; var corners = new Vector3[4];
            for (int i = 0; i < modules.Length; i++) {
                var rect = root.Find(modules[i]) as RectTransform;
                Require(rect != null, "Missing module " + modules[i]);
                rect.GetWorldCorners(corners);
                Vector3 min = root.InverseTransformPoint(corners[0]), max = root.InverseTransformPoint(corners[2]);
                bounds[i] = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                Rect canvas = root.rect;
                Require(bounds[i].xMin >= canvas.xMin - 1 && bounds[i].xMax <= canvas.xMax + 1
                    && bounds[i].yMin >= canvas.yMin - 1 && bounds[i].yMax <= canvas.yMax + 1,
                    modules[i] + " exceeds the viewport.");
                for (int j = 0; j < i; j++) Require(!bounds[i].Overlaps(bounds[j]), modules[i] + " overlaps " + modules[j]);
            }
            int skillCount = 0;
            foreach (Transform child in root.Find("SkillBar")) if (child.name.StartsWith("SkillSlot")) skillCount++;
            Require(skillCount == 4, "Must show four skill slots, including empty slots.");
            int itemCount = 0;
            foreach (Transform child in root.Find("QuickItems")) if (child.name.StartsWith("QuickItem")) itemCount++;
            Require(itemCount == 2, "Must show two quick items.");
            foreach (var frame in root.GetComponentsInChildren<HudFrameGraphic>(true))
                Require(frame.GetComponent<CanvasRenderer>() != null, "Missing CanvasRenderer: " + frame.name);
            var input = UnityEngine.Object.FindFirstObjectByType<PetCommandInput>();
            PetController active = input != null ? input.activePet : null;
            var carousel = hud.Carousel;
            Require(carousel != null, "Missing carousel view.");
            ValidateCarouselPresentation(carousel);
            Require(Mathf.Approximately(root.Find("ActivePetInfo").localScale.x, .65f)
                && Mathf.Approximately(root.Find("SkillBar").localScale.x, .5f)
                && Mathf.Approximately(root.Find("QuickItems").localScale.x, .5f), "Skill/quick-item HUD must be half scale; pet information must be at 65% scale.");
            Require(Mathf.Approximately(((RectTransform)root.Find("MiniMap")).rect.width, 350f), "Minimap must remain 350px wide.");
            if (Application.isPlaying) {
                Require(root.Find("SkillBar").gameObject.activeSelf == hud.PetSkillsAvailable,
                    "Skill bar must only be visible when the spawned pet can receive commands.");
                if (!hud.PetSkillsAvailable) {
                    foreach (var button in root.Find("SkillBar").GetComponentsInChildren<Button>(true))
                        Require(!button.interactable, "Hidden skill buttons must also be disabled.");
                    ValidateRejectedSkills(active);
                }
                Debug.Log($"Pet skill availability: ready={hud.PetSkillsAvailable}, bar visible={root.Find("SkillBar").gameObject.activeSelf}.");
            }
            Require(Mathf.Approximately(bounds[1].width, 442f) && Mathf.Approximately(bounds[2].width, bounds[1].width)
                && Mathf.Approximately(bounds[2].xMin, bounds[1].xMin), "Pet row and status must share both horizontal edges.");
            int before = carousel.FocusIndex;
            carousel.Focus(5); carousel.Focus(6); Require(carousel.FocusIndex == 0, "Carousel must loop 6 -> 1.");
            carousel.Focus(-1); Require(carousel.FocusIndex == 5, "Carousel must loop 1 -> 6.");
            Require(input == null || input.activePet == active, "Carousel focus changed gameplay active pet without confirmation.");
            carousel.Focus(before);
            var map = UnityEngine.Object.FindFirstObjectByType<MinimapController>();
            Require(map != null && map.Manager != null && map.Camera != null, "Existing minimap references are missing.");
            Require(map.Manager.renderTexture != null, "Minimap must retain its RenderTexture.");
            if (Application.isPlaying) {
                Require(map.Camera.isActiveAndEnabled, "Minimap camera is disabled.");
                Require(map.Manager.renderTexture.IsCreated(), "Minimap RenderTexture was not created.");
            }
            var compass = GameObject.Find("CompassHUDRoot"); Require(compass == null || !compass.activeInHierarchy, "Compass is still visible.");
            ValidateQuickSlotSave();
            Debug.Log("Gameplay HUD validation passed: full-size bottom modules fit, 5 visible pet cards with 50% overlap on both sides, correct draw order, 4 skills gated by spawn/2 items, carousel wraps without changing active pet, unchanged minimap and quick item save.");
            if (map.Camera != null) Debug.Log($"Minimap runtime: camera enabled={map.Camera.isActiveAndEnabled}, target={map.Target?.name}, culling={map.Camera.cullingMask}, position={map.Camera.transform.position}, near/far={map.Camera.nearClipPlane}/{map.Camera.farClipPlane}.");
        }
        static void ValidateCarouselPresentation(PetPartyCarouselView carousel) {
            Require(!carousel.IsAnimating, "Wait for carousel animation to finish before validation.");
            Require(Mathf.Approximately(carousel.transform.localScale.x, 1f), "Carousel must use full scale.");
            Transform viewport = carousel.transform.Find("LoopViewport");
            var visible = new List<RectTransform>();
            foreach (Transform child in viewport) {
                var group = child.GetComponent<CanvasGroup>();
                if (group != null && group.alpha > .001f) visible.Add((RectTransform)child);
            }
            Require(visible.Count == 5, "Exactly five party cards should be visible at rest.");
            visible.Sort((a, b) => a.anchoredPosition.x.CompareTo(b.anchoredPosition.x));
            foreach (var rect in visible) Require(Mathf.Abs(rect.anchoredPosition.y) < .001f, "All card centers must share a horizontal line.");
            Require(Mathf.Approximately(visible[1].localScale.x, .95f) && Mathf.Approximately(visible[0].localScale.x, .9f), "Surrounding cards must shrink by 5% per step.");
            ValidateOverlap(visible[2], visible[1]); ValidateOverlap(visible[1], visible[0]);
            ValidateOverlap(visible[2], visible[3]); ValidateOverlap(visible[3], visible[4]);
            Require(Mathf.Approximately(visible[0].GetComponent<CanvasGroup>().alpha, visible[4].GetComponent<CanvasGroup>().alpha), "Both rim cards must fade symmetrically.");
        }
        static void ValidateOverlap(RectTransform inner, RectTransform outer) {
            float innerWidth = inner.rect.width * inner.localScale.x, outerWidth = outer.rect.width * outer.localScale.x;
            float overlap = (innerWidth + outerWidth) * .5f - Mathf.Abs(inner.anchoredPosition.x - outer.anchoredPosition.x);
            Require(Mathf.Abs(overlap / outerWidth - .5f) < .001f, "Inner card must cover half of its outer neighbor, including rim cards.");
            Require(inner.GetSiblingIndex() > outer.GetSiblingIndex(), "Nearer cards must draw above farther cards on both sides.");
        }
        static void ValidateRejectedSkills(PetController pet) {
            if (pet == null) return;
            var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            if (stats == null) return;
            var provider = UnityEngine.Object.FindFirstObjectByType<PetCommandHudProvider>();
            var data = stats.GetSkills();
            var cooldowns = new float[data.Count];
            for (int i = 0; i < data.Count; i++) cooldowns[i] = data[i].cooldownRemainingSeconds;
            int requests = 0;
            Action<int> count = _ => requests++;
            stats.SkillRequested += count;
            try {
                for (int i = 0; i < cooldowns.Length; i++) {
                    provider?.RequestSkill(i);
                    stats.RequestSkill(i);
                }
                data = stats.GetSkills();
                Require(requests == 0, "Pet accepted skills while dormant or in a summon/recall sequence.");
                for (int i = 0; i < cooldowns.Length; i++)
                    Require(Mathf.Approximately(cooldowns[i], data[i].cooldownRemainingSeconds),
                        "Rejected skills must not consume cooldown.");
            } finally { stats.SkillRequested -= count; }
        }
        static void ValidateQuickSlotSave() {
            var first = new GameObject("HUD_SaveValidation_Source");
            var second = new GameObject("HUD_SaveValidation_Restored");
            try {
                var source = first.AddComponent<MonsterInventoryAdapter>();
                var restored = second.AddComponent<MonsterInventoryAdapter>();
                source.AssignQuickItem(0, "existing-item-A"); source.AssignQuickItem(1, "existing-item-B");
                var snapshot = source.CreateSaveData();
                Require(restored.RestoreFromSaveData(snapshot, out string error), "Quick item save restore failed: " + error);
                Require(restored.GetQuickItemId(0) == "existing-item-A" && restored.GetQuickItemId(1) == "existing-item-B", "Quick item assignment lost on restore.");
                snapshot.quickItemIds = null; // Older saves have no quick-slot fields.
                Require(restored.RestoreFromSaveData(snapshot, out error), "Legacy inventory save must still restore: " + error);
                Require(string.IsNullOrEmpty(restored.GetQuickItemId(0)) && string.IsNullOrEmpty(restored.GetQuickItemId(1)), "Legacy restore must clear obsolete assignments.");
            } finally {
                UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
            }
        }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("HUD validation: " + message); }
    }
}
