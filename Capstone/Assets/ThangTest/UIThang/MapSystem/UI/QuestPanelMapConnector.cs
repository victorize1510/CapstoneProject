using Capstone.Game.QuestSystem.UI;
using Capstone.Game.UISystem;
using UnityEngine;

namespace Capstone.Game.MapSystem.UI {
    [DisallowMultipleComponent]
    public sealed class QuestPanelMapConnector : MonoBehaviour {
        [SerializeField] QuestPanelController questPanel = null;
        [SerializeField] MapSystemController mapSystem = null;
        [SerializeField] AAMapRuntimeBinder mapBinder = null;
        [SerializeField] MapInputController mapInput = null;
        [SerializeField] GameMenuController gameMenu = null;
        [SerializeField] bool openMapWhenRequested = true;
        QuestPanelController subscribedQuestPanel;

        void OnEnable() {
            ResolveReferences();
            SubscribeQuestPanel();
        }

        void Update() {
            if (subscribedQuestPanel != null) return;
            ResolveReferences();
            SubscribeQuestPanel();
        }

        void OnDisable() {
            if (subscribedQuestPanel != null) {
                subscribedQuestPanel.ShowOnMapRequested -= HandleShowOnMapRequested;
                subscribedQuestPanel = null;
            }
        }

        void HandleShowOnMapRequested(Vector3 worldPosition) {
            ResolveReferences();
            if (openMapWhenRequested) {
                if (gameMenu != null) gameMenu.OpenMapDirect();
                else if (mapInput != null) mapInput.OpenMap();
            }

            if (mapSystem != null) {
                mapSystem.FocusWorldPosition(worldPosition, false);
                return;
            }

            if (mapBinder != null) mapBinder.FocusMap(worldPosition, false);
        }

        void ResolveReferences() {
            if (questPanel == null) questPanel = FindFirstObjectByType<QuestPanelController>();
            if (mapSystem == null) mapSystem = FindFirstObjectByType<MapSystemController>();
            if (mapBinder == null) mapBinder = FindFirstObjectByType<AAMapRuntimeBinder>();
            if (mapInput == null) mapInput = FindFirstObjectByType<MapInputController>();
            if (gameMenu == null) gameMenu = FindFirstObjectByType<GameMenuController>();
        }

        void SubscribeQuestPanel() {
            if (questPanel == null || subscribedQuestPanel == questPanel) return;
            if (subscribedQuestPanel != null) {
                subscribedQuestPanel.ShowOnMapRequested -= HandleShowOnMapRequested;
            }
            subscribedQuestPanel = questPanel;
            subscribedQuestPanel.ShowOnMapRequested += HandleShowOnMapRequested;
        }
    }
}
