using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public readonly struct QuestTargetInfo {
        public readonly bool HasTarget;
        public readonly string QuestId;
        public readonly string ObjectiveId;
        public readonly string Label;
        public readonly Vector3 Position;

        public QuestTargetInfo(string questId, string objectiveId, string label, Vector3 position) {
            HasTarget = true;
            QuestId = questId;
            ObjectiveId = objectiveId;
            Label = label;
            Position = position;
        }
    }

    public readonly struct QuestTrackingInfo {
        public readonly QuestRuntimeState Quest;
        public readonly QuestTargetInfo Target;
        public readonly bool HasDistance;
        public readonly float Distance;

        public QuestTrackingInfo(QuestRuntimeState quest, QuestTargetInfo target, Transform origin) {
            Quest = quest;
            Target = target;
            HasDistance = target.HasTarget && origin != null;
            Distance = HasDistance ? Vector3.Distance(origin.position, target.Position) : 0f;
        }
    }
}
