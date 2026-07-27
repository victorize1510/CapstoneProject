using System;

namespace Capstone.Game.QuestSystem {
    public enum QuestProgressMode {
        Add,
        Set,
        Complete
    }

    public readonly struct QuestProgressReport {
        public readonly string QuestId;
        public readonly string ObjectiveId;
        public readonly int Amount;
        public readonly QuestProgressMode Mode;

        public QuestProgressReport(string questId, string objectiveId, int amount, QuestProgressMode mode = QuestProgressMode.Add) {
            QuestId = questId;
            ObjectiveId = objectiveId;
            Amount = amount;
            Mode = mode;
        }
    }

    public interface IQuestProgressSource {
        event Action<QuestProgressReport> ProgressReported;
    }
}
