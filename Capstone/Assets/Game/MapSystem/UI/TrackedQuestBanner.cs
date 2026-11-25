using Capstone.Game.QuestSystem;
using TMPro;
using UnityEngine;

namespace Capstone.Game.MapSystem.UI {
    public sealed class TrackedQuestBanner : MonoBehaviour {
        QuestManager manager;
        TMP_Text title, destination;
        string currentTitle;
        bool mapReady=true;
        public void Bind(QuestManager source, TMP_Text label, TMP_Text destinationLabel = null) {
            if (manager != null) manager.TrackedQuestChanged -= OnQuestChanged;
            manager = source; title = label; destination = destinationLabel;
            if (manager != null) manager.TrackedQuestChanged += OnQuestChanged;
            OnQuestChanged(manager != null ? manager.GetTrackedQuest() : null);
        }
        public void SetTitle(string value) {
            currentTitle=value;
            if (title != null) title.text = value ?? string.Empty;
            gameObject.SetActive(mapReady&&!string.IsNullOrWhiteSpace(value));
        }
        internal void SetMapReady(bool ready) {mapReady=ready;gameObject.SetActive(ready&&!string.IsNullOrWhiteSpace(currentTitle));}
        internal void SetDestination(string label, float distance) {
            if (destination == null) return;
            string value = string.IsNullOrWhiteSpace(label) ? string.Empty :
                distance > 0f ? $"{label} · {Mathf.RoundToInt(distance)} m" : label;
            if (destination.text != value) destination.text = value;
        }
        void OnQuestChanged(QuestRuntimeState quest) => SetTitle(quest?.Definition?.Title);
        void OnDestroy() { if (manager != null) manager.TrackedQuestChanged -= OnQuestChanged; }
    }
}
