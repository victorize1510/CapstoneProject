using System;
using System.Collections.Generic;

namespace Capstone.Game.QuestSystem {
    [Serializable]
    public sealed class QuestSaveData {
        public int version = 1;
        public string trackedQuestId;
        public List<string> unlockedQuestIds = new List<string>();
        public List<QuestRuntimeSaveData> questStates = new List<QuestRuntimeSaveData>();
        public List<QuestRuntimeSaveData> activeQuests = new List<QuestRuntimeSaveData>();
        public List<string> completedQuestIds = new List<string>();
        public List<string> failedQuestIds = new List<string>();
        public List<string> abandonedQuestIds = new List<string>();

        public IReadOnlyList<QuestRuntimeSaveData> GetSavedQuestStates() {
            return questStates != null && questStates.Count > 0 ? questStates : activeQuests;
        }
    }

    [Serializable]
    public sealed class QuestRuntimeSaveData {
        public string questId;
        public QuestStatus status;
        public bool tracked;
        public bool rewardsClaimed;
        public float acceptedTime;
        public float completedTime;
        public List<QuestObjectiveSaveData> objectives = new List<QuestObjectiveSaveData>();
    }

    [Serializable]
    public sealed class QuestObjectiveSaveData {
        public string objectiveId;
        public int currentAmount;
        public bool completed;
    }
}
