using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestProgressMode {
        Add,
        Set,
        Complete
    }

    public readonly struct QuestProgressEvent {
        static readonly string[] EmptyTags = Array.Empty<string>();

        public readonly string QuestId;
        public readonly string ObjectiveId;
        public readonly QuestObjectiveType ObjectiveType;
        public readonly string TargetId;
        public readonly string TargetTypeId;
        public readonly string RegionId;
        public readonly string CustomKey;
        public readonly int Amount;
        public readonly QuestProgressMode Mode;
        public readonly Vector3? WorldPosition;
        public readonly IReadOnlyList<string> Tags;

        public QuestProgressEvent(
            QuestObjectiveType objectiveType,
            string targetId = "",
            int amount = 1,
            QuestProgressMode mode = QuestProgressMode.Add,
            string questId = "",
            string objectiveId = "",
            string targetTypeId = "",
            string regionId = "",
            string customKey = "",
            Vector3? worldPosition = null,
            IReadOnlyList<string> tags = null) {
            ObjectiveType = objectiveType;
            TargetId = targetId ?? string.Empty;
            Amount = Mathf.Max(1, amount);
            Mode = mode;
            QuestId = questId ?? string.Empty;
            ObjectiveId = objectiveId ?? string.Empty;
            TargetTypeId = targetTypeId ?? string.Empty;
            RegionId = regionId ?? string.Empty;
            CustomKey = customKey ?? string.Empty;
            WorldPosition = worldPosition;
            Tags = tags ?? EmptyTags;
        }

        public static QuestProgressEvent FromLegacyReport(QuestProgressReport report) {
            return new QuestProgressEvent(
                QuestObjectiveType.CustomObjective,
                amount: report.Amount,
                mode: report.Mode,
                questId: report.QuestId,
                objectiveId: report.ObjectiveId);
        }

        public static QuestProgressEvent NpcInteraction(string npcId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.TalkToNPC, npcId, amount);
        }

        public static QuestProgressEvent LocationReached(string locationId, Vector3 worldPosition, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.ReachLocation, locationId, amount, worldPosition: worldPosition);
        }

        public static QuestProgressEvent CreatureCaptured(string creatureId, string creatureTypeId = "", int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.CaptureCreature, creatureId, amount, targetTypeId: creatureTypeId);
        }

        public static QuestProgressEvent EnemyDefeated(string enemyId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.DefeatEnemy, enemyId, amount);
        }

        public static QuestProgressEvent CreatureDefeated(string creatureId, string creatureTypeId = "", int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.DefeatCreature, creatureId, amount, targetTypeId: creatureTypeId);
        }

        public static QuestProgressEvent BossDefeated(string bossId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.DefeatBoss, bossId, amount);
        }

        public static QuestProgressEvent ItemCollected(string itemId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.CollectItem, itemId, amount);
        }

        public static QuestProgressEvent ItemGiven(string itemId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.GiveItem, itemId, amount);
        }

        public static QuestProgressEvent ItemUsed(string itemId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.UseItem, itemId, amount);
        }

        public static QuestProgressEvent ObjectInteracted(string objectId, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.InteractObject, objectId, amount);
        }

        public static QuestProgressEvent AreaExplored(string areaId, Vector3 worldPosition, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.ExploreArea, areaId, amount, worldPosition: worldPosition);
        }

        public static QuestProgressEvent LocationDiscovered(string locationId, Vector3 worldPosition, int amount = 1) {
            return new QuestProgressEvent(QuestObjectiveType.DiscoverLocation, locationId, amount, worldPosition: worldPosition);
        }

        public static QuestProgressEvent TrainerLevelChanged(int level) {
            return new QuestProgressEvent(QuestObjectiveType.TrainerLevel, amount: level, mode: QuestProgressMode.Set);
        }

        public static QuestProgressEvent CreatureLevelChanged(string creatureId, int level) {
            return new QuestProgressEvent(QuestObjectiveType.CreatureLevel, creatureId, level, QuestProgressMode.Set);
        }

        public static QuestProgressEvent QuestCompleted(string completedQuestId) {
            return new QuestProgressEvent(QuestObjectiveType.CompleteQuest, completedQuestId, 1, QuestProgressMode.Complete);
        }
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

    public interface IQuestProgressEventSource {
        event Action<QuestProgressEvent> ProgressReported;
    }
}
