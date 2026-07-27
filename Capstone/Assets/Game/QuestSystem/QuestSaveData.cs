using System;
using System.Collections.Generic;

namespace Capstone.Game.QuestSystem {
    [Serializable]
    public sealed class QuestSaveData {
        public string trackedQuestId;
        public List<QuestRuntimeSaveData> activeQuests = new List<QuestRuntimeSaveData>();
        public List<string> completedQuestIds = new List<string>();
        public List<string> failedQuestIds = new List<string>();
        public List<string> abandonedQuestIds = new List<string>();
    }

    [Serializable]
    public sealed class QuestRuntimeSaveData {
        public string questId;
        public QuestStatus status;
        public bool tracked;
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
