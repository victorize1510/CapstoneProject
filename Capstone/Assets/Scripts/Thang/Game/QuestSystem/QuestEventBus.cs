using System;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestEventBus : MonoBehaviour {
        public event Action<QuestProgressEvent> ProgressReported;

        public void Report(QuestProgressEvent progressEvent) {
            ProgressReported?.Invoke(progressEvent);
        }

        public void ReportEnemyDefeated(string enemyId, int amount = 1) {
            Report(QuestProgressEvent.EnemyDefeated(enemyId, amount));
        }

        public void ReportBossDefeated(string bossId, int amount = 1) {
            Report(QuestProgressEvent.BossDefeated(bossId, amount));
        }

        public void ReportCreatureDefeated(string creatureId, string creatureTypeId = "", int amount = 1) {
            Report(QuestProgressEvent.CreatureDefeated(creatureId, creatureTypeId, amount));
        }

        public void ReportCreatureCaptured(string creatureId, string creatureTypeId = "", int amount = 1) {
            Report(QuestProgressEvent.CreatureCaptured(creatureId, creatureTypeId, amount));
        }

        public void ReportItemCollected(string itemId, int amount = 1) {
            Report(QuestProgressEvent.ItemCollected(itemId, amount));
        }

        public void ReportItemGiven(string itemId, int amount = 1) {
            Report(QuestProgressEvent.ItemGiven(itemId, amount));
        }

        public void ReportItemUsed(string itemId, int amount = 1) {
            Report(QuestProgressEvent.ItemUsed(itemId, amount));
        }

        public void ReportObjectInteracted(string objectId, int amount = 1) {
            Report(QuestProgressEvent.ObjectInteracted(objectId, amount));
        }

        public void ReportNpcInteraction(string npcId, int amount = 1) {
            Report(QuestProgressEvent.NpcInteraction(npcId, amount));
        }

        public void ReportLocationReached(string locationId, Vector3 worldPosition, int amount = 1) {
            Report(QuestProgressEvent.LocationReached(locationId, worldPosition, amount));
        }

        public void ReportAreaExplored(string areaId, Vector3 worldPosition, int amount = 1) {
            Report(QuestProgressEvent.AreaExplored(areaId, worldPosition, amount));
        }

        public void ReportLocationDiscovered(string locationId, Vector3 worldPosition, int amount = 1) {
            Report(QuestProgressEvent.LocationDiscovered(locationId, worldPosition, amount));
        }

        public void ReportQuestCompleted(string questId) {
            Report(QuestProgressEvent.QuestCompleted(questId));
        }

        public void ReportTrainerLevelChanged(int level) {
            Report(QuestProgressEvent.TrainerLevelChanged(level));
        }

        public void ReportCreatureLevelChanged(string creatureId, int level) {
            Report(QuestProgressEvent.CreatureLevelChanged(creatureId, level));
        }

        public void ReportCustom(string customKey, int amount = 1) {
            Report(new QuestProgressEvent(QuestObjectiveType.CustomObjective, customKey: customKey, amount: amount));
        }
    }
}
