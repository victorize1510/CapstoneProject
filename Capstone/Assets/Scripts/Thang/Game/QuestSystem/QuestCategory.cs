namespace Capstone.Game.QuestSystem {
    public enum QuestCategory {
        Main = 0,
        Side = 1,
        Event = 2,
        Other = 3
    }

    public enum QuestSubType {
        None = 0,
        Daily = 1,
        Companion = 2,
        Exploration = 3,
        Bounty = 4,
        Creature = 5,
        Custom = 100
    }

    public enum QuestAvailabilityState {
        Locked = 0,
        Available = 1,
        Active = 2,
        Completed = 3,
        Failed = 4,
        Abandoned = 5
    }

    public static class QuestCategoryUtility {
        public static QuestCategory FromLegacyType(QuestType type) {
            switch (type) {
                case QuestType.Main:
                    return QuestCategory.Main;
                case QuestType.Side:
                    return QuestCategory.Side;
                case QuestType.Event:
                    return QuestCategory.Event;
                default:
                    return QuestCategory.Other;
            }
        }
    }
}
