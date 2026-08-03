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
            if (FindFirst<WorldMapController>() == null) issues.Add("Missing WorldMapController.");
            if (FindFirst<MapInputController>() == null) issues.Add("Missing MapInputController.");
            if (FindFirst<MapMarkerManager>() == null) issues.Add("Missing MapMarkerManager.");
            if (FindFirst<MapIconRegistry>() == null) issues.Add("Missing MapIconRegistry.");

            return issues;
        }

        static Camera FindCamera(string objectName) {
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.GetComponent<Camera>() : null;
        }

        static T FindFirst<T>() where T : Object {
            T[] items = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            return items.Length > 0 ? items[0] : null;
        }
    }
}
