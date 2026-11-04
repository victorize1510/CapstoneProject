using Capstone.Game.QuestSystem;
using TMPro;
using UnityEngine;

namespace Capstone.Game.MapSystem.UI {
    public sealed class TrackedQuestBanner : MonoBehaviour {
        QuestManager manager;
        TMP_Text title;
        string currentTitle;
        bool mapReady=true;
        public void Bind(QuestManager source, TMP_Text label) {
            if (manager != null) manager.TrackedQuestChanged -= OnQuestChanged;
            manager = source; title = label;
            if (manager != null) manager.TrackedQuestChanged += OnQuestChanged;
            OnQuestChanged(manager != null ? manager.GetTrackedQuest() : null);
        }
        public void SetTitle(string value) {
            currentTitle=value;
            if (title != null) title.text = value ?? string.Empty;
            gameObject.SetActive(mapReady&&!string.IsNullOrWhiteSpace(value));
        }
        internal void SetMapReady(bool ready) {mapReady=ready;gameObject.SetActive(ready&&!string.IsNullOrWhiteSpace(currentTitle));}
        void OnQuestChanged(QuestRuntimeState quest) => SetTitle(quest?.Definition?.Title);
        void OnDestroy() { if (manager != null) manager.TrackedQuestChanged -= OnQuestChanged; }
    }
}
