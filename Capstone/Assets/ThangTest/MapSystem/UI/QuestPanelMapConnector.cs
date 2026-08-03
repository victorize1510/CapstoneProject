using Capstone.Game.QuestSystem.UI;
using UnityEngine;

namespace Capstone.Game.MapSystem.UI {
    [DisallowMultipleComponent]
    public sealed class QuestPanelMapConnector : MonoBehaviour {
        [SerializeField] QuestPanelController questPanel = null;
        [SerializeField] MapSystemController mapSystem = null;
        [SerializeField] AAMapRuntimeBinder mapBinder = null;
        [SerializeField] MapInputController mapInput = null;
        [SerializeField] bool openMapWhenRequested = true;

        void OnEnable() {
            ResolveReferences();
            if (questPanel != null) questPanel.ShowOnMapRequested += HandleShowOnMapRequested;
        }

        void OnDisable() {
            if (questPanel != null) questPanel.ShowOnMapRequested -= HandleShowOnMapRequested;
        }

        void HandleShowOnMapRequested(Vector3 worldPosition) {
            ResolveReferences();
            if (mapSystem != null) {
                mapSystem.FocusWorldPosition(worldPosition, openMapWhenRequested);
                return;
            }

            if (mapBinder != null) mapBinder.FocusMap(worldPosition, false);
            if (openMapWhenRequested && mapInput != null) mapInput.OpenMap();
        }

        void ResolveReferences() {
            if (questPanel == null) questPanel = FindFirstObjectByType<QuestPanelController>();
            if (mapSystem == null) mapSystem = FindFirstObjectByType<MapSystemController>();
            if (mapBinder == null) mapBinder = FindFirstObjectByType<AAMapRuntimeBinder>();
            if (mapInput == null) mapInput = FindFirstObjectByType<MapInputController>();
        }
    }
}
