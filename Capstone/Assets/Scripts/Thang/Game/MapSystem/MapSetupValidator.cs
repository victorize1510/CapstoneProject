using System.Collections.Generic;
using AAMAP;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Capstone.Game.MapSystem {
    [DisallowMultipleComponent]
    public sealed class MapSetupValidator : MonoBehaviour {
        [SerializeField] bool validateOnStart = false;

        void Start() {
            if (validateOnStart) ValidateSetup(true);
        }

        public bool ValidateSetup(bool logResult) {
            List<string> issues = CollectIssues();
            if (issues.Count == 0) {
                if (logResult) Debug.Log("Map setup OK: minimap/world map references look valid.");
                return true;
            }

            if (logResult) {
                for (int i = 0; i < issues.Count; i++) Debug.LogWarning("Map setup issue: " + issues[i], this);
            }

            return false;
        }

        public List<string> CollectIssues() {
            List<string> issues = new List<string>();

            Canvas canvas = FindFirst<Canvas>();
            if (canvas == null) issues.Add("Missing Canvas.");
            else {
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null) issues.Add("Canvas is missing CanvasScaler.");
                else if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) issues.Add("CanvasScaler should use Scale With Screen Size.");
            }

            if (FindFirst<EventSystem>() == null) issues.Add("Missing EventSystem.");

            MinimapManager minimapManager = FindFirst<MinimapManager>();
            MapManager mapManager = FindFirst<MapManager>();
            if (minimapManager == null) issues.Add("Missing AA MinimapManager.");
            if (mapManager == null) issues.Add("Missing AA MapManager.");

            Camera minimapCamera = FindCamera("Minimap Camera");
            Camera mapCamera = FindCamera("Map Camera");
            if (minimapCamera == null) issues.Add("Missing Minimap Camera.");
            if (mapCamera == null) issues.Add("Missing Map Camera.");
            if (minimapCamera != null && !minimapCamera.orthographic) issues.Add("Minimap Camera should be orthographic.");
            if (mapCamera != null && !mapCamera.orthographic) issues.Add("Map Camera should be orthographic.");

            Transform player = AAMapRuntimeBinder.FindPlayerTarget();
            if (player == null) issues.Add("Missing Player target. Add BasicPlayerMovement or tag Player.");

            if (FindFirst<MapSystemController>() == null) issues.Add("Missing MapSystemController.");
            if (FindFirst<MinimapController>() == null) issues.Add("Missing MinimapController.");
            WorldMapController worldMapController = FindFirst<WorldMapController>();
            if (worldMapController == null) issues.Add("Missing WorldMapController.");
            if (FindFirst<MapInputController>() == null) issues.Add("Missing MapInputController.");
            if (FindFirst<MapMarkerManager>() == null) issues.Add("Missing MapMarkerManager.");
            if (FindFirst<MapIconRegistry>() == null) issues.Add("Missing MapIconRegistry.");

            if (mapManager != null) {
                if (mapManager.renderTexture == null) issues.Add("World MapManager renderTexture is missing.");
                else if (Application.isPlaying && !mapManager.renderTexture.IsCreated()) issues.Add("World Map renderTexture exists but is not created at runtime yet.");

                Transform mapMask = mapManager.transform.Find("Map Mask");
                if (mapMask == null) {
                    issues.Add("World Map is missing Map Mask viewport.");
                } else {
                    if (mapMask.GetComponent<Mask>() == null && mapMask.GetComponent<RectMask2D>() == null) {
                        issues.Add("Map Mask should have Mask or RectMask2D.");
                    }

                    RawImage display = FindChildComponent<RawImage>(mapMask, "Map Display");
                    if (display == null) issues.Add("Map Mask is missing Map Display RawImage.");
                    else {
                        if (display.texture == null) issues.Add("Map Display RawImage texture is missing.");
                        if (display.color.a < 0.99f) issues.Add("Map Display RawImage alpha is below 1 and may look transparent.");
                    }
                }
            }

            if (worldMapController != null) {
                if (worldMapController.MinimumVisiblePercent > worldMapController.InitialVisiblePercent) {
                    issues.Add("World Map minimumVisiblePercent is greater than initialVisiblePercent.");
                }
                if (worldMapController.InitialVisiblePercent > worldMapController.MaximumVisiblePercent) {
                    issues.Add("World Map initialVisiblePercent is greater than maximumVisiblePercent.");
                }
                if (worldMapController.MaximumVisiblePercent > 0.30f) {
                    issues.Add("World Map maximumVisiblePercent is above 0.30, so zoom-out may show too much map.");
                }
                if (worldMapController.MapInteractionRect == null) {
                    issues.Add("WorldMapController is missing mapInteractionRect.");
                }
                if (worldMapController.WorldSize.x <= 1f || worldMapController.WorldSize.y <= 1f) {
                    issues.Add("WorldMapController worldSize is too small.");
                }
            }

            return issues;
        }

        static Camera FindCamera(string objectName) {
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.GetComponent<Camera>() : null;
        }

        static T FindChildComponent<T>(Transform root, string childName) where T : Component {
            if (root == null) return null;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
                if (child.name == childName) return child.GetComponent<T>();
            }
            return null;
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
