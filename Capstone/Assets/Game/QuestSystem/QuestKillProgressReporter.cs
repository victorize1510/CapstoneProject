using System;
using System.Reflection;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestKillProgressReporter : MonoBehaviour, IQuestProgressSource {
        [SerializeField] QuestManager questManager = null;
        [SerializeField] MonoBehaviour enemyBehaviour = null;
        [SerializeField] bool autoFindQuestManager = true;
        [SerializeField] bool autoFindEnemyBehaviour = true;
        [SerializeField] string questId = string.Empty;
        [SerializeField] string objectiveId = "kill_cube";
        [SerializeField] int progressAmount = 1;
        [SerializeField] string alivePropertyName = "IsAlive";
        [SerializeField] bool logWhenQuestManagerMissing = true;

        bool previousAlive;
        bool hasPreviousAlive;
        PropertyInfo aliveProperty;

        public event Action<QuestProgressReport> ProgressReported;

        void OnEnable() {
            ResolveReferences();
            CacheAliveProperty();

            if (questManager != null) {
                questManager.RegisterProgressSource(this);
            }

            hasPreviousAlive = TryReadAlive(out previousAlive);
        }

        void OnDisable() {
            if (questManager != null) {
                questManager.UnregisterProgressSource(this);
            }
        }

        void Update() {
            if (!TryReadAlive(out bool alive)) return;

            if (!hasPreviousAlive) {
                previousAlive = alive;
                hasPreviousAlive = true;
                return;
            }

            if (previousAlive && !alive) {
                ReportProgress();
            }

            previousAlive = alive;
        }

        public void ReportProgress() {
            if (string.IsNullOrWhiteSpace(objectiveId)) {
                Debug.LogWarning($"{nameof(QuestKillProgressReporter)} on {name} has no objective id.");
                return;
            }

            ResolveQuestManager();
            var report = new QuestProgressReport(
                questId,
                objectiveId,
                Mathf.Max(1, progressAmount),
                QuestProgressMode.Add);

            if (ProgressReported != null) {
                ProgressReported.Invoke(report);
                return;
            }

            if (questManager == null) {
                if (logWhenQuestManagerMissing) {
                    Debug.LogWarning($"{nameof(QuestKillProgressReporter)} could not find a QuestManager for {name}.");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(questId)) {
                questManager.UpdateObjectiveProgress(objectiveId, report.Amount, report.Mode);
            } else {
                questManager.UpdateObjectiveProgress(questId, objectiveId, report.Amount, report.Mode);
            }
        }

        void ResolveReferences() {
            ResolveQuestManager();
            ResolveEnemyBehaviour();
        }

        void ResolveQuestManager() {
            if (questManager != null || !autoFindQuestManager) return;

            questManager = FindFirstObjectByType<QuestManager>();
        }

        void ResolveEnemyBehaviour() {
            if (enemyBehaviour != null || !autoFindEnemyBehaviour) return;

            var behaviours = GetComponents<MonoBehaviour>();
            foreach (var behaviour in behaviours) {
                if (behaviour == null || behaviour == this) continue;

                var property = behaviour.GetType().GetProperty(alivePropertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || property.PropertyType != typeof(bool) || !property.CanRead) continue;

                enemyBehaviour = behaviour;
                break;
            }
        }

        void CacheAliveProperty() {
            aliveProperty = null;
            if (enemyBehaviour == null || string.IsNullOrWhiteSpace(alivePropertyName)) return;

            aliveProperty = enemyBehaviour.GetType().GetProperty(alivePropertyName, BindingFlags.Instance | BindingFlags.Public);
            if (aliveProperty == null || aliveProperty.PropertyType != typeof(bool) || !aliveProperty.CanRead) {
                aliveProperty = null;
            }
        }

        bool TryReadAlive(out bool alive) {
            alive = false;

            if (enemyBehaviour == null) {
                ResolveEnemyBehaviour();
                CacheAliveProperty();
            }

            if (enemyBehaviour == null || aliveProperty == null) return false;

            alive = (bool)aliveProperty.GetValue(enemyBehaviour);
            return true;
        }
    }
}
