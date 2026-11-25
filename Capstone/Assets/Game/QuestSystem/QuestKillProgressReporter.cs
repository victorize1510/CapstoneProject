using System;
using System.Reflection;
using Capstone.Game.ProfileSystem;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [DisallowMultipleComponent]
    public sealed class QuestKillProgressReporter : MonoBehaviour, IQuestProgressSource {
        [SerializeField] QuestManager questManager = null;
        [SerializeField] QuestEventBus questEventBus = null;
        [SerializeField] PlayerProfileRuntimeProvider profileProvider = null;
        [SerializeField] MonoBehaviour enemyBehaviour = null;
        [SerializeField] bool autoFindQuestManager = true;
        [SerializeField] bool autoFindProfileProvider = true;
        [SerializeField] bool autoFindEnemyBehaviour = true;
        [SerializeField] bool recordBattleResult = true;
        [SerializeField] bool countsAsBoss = false;
        [SerializeField] string questId = string.Empty;
        [SerializeField] string objectiveId = "kill_cube";
        [SerializeField] string enemyId = string.Empty;
        [SerializeField] int progressAmount = 1;
        [SerializeField] string alivePropertyName = "IsAlive";
        [SerializeField] bool logWhenQuestManagerMissing = true;

        bool previousAlive;
        bool hasPreviousAlive;
        PropertyInfo aliveProperty;
        DummyEnemy eventEnemy;

        public event Action<QuestProgressReport> ProgressReported;

        void OnEnable() {
            ResolveReferences();
            CacheAliveProperty();

            if (questManager != null) {
                questManager.RegisterProgressSource(this);
            }

            if (questEventBus == null && questManager != null) {
                questEventBus = questManager.GetComponent<QuestEventBus>();
            }

            eventEnemy = enemyBehaviour as DummyEnemy;
            if (eventEnemy != null) {
                eventEnemy.Defeated += HandleEnemyDefeated;
                hasPreviousAlive = false;
            } else {
                hasPreviousAlive = TryReadAlive(out previousAlive);
            }
        }

        void OnDisable() {
            if (eventEnemy != null) {
                eventEnemy.Defeated -= HandleEnemyDefeated;
                eventEnemy = null;
            }

            if (questManager != null) {
                questManager.UnregisterProgressSource(this);
            }
        }

        void Update() {
            if (eventEnemy != null) return;
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

        void HandleEnemyDefeated(GameObject _) {
            ReportProgress();
        }

        public void ReportProgress() {
            if (string.IsNullOrWhiteSpace(objectiveId)) {
                Debug.LogWarning($"{nameof(QuestKillProgressReporter)} on {name} has no objective id.");
                return;
            }

            ResolveQuestManager();
            RecordProfileProgress();
            var report = new QuestProgressReport(
                questId,
                objectiveId,
                Mathf.Max(1, progressAmount),
                QuestProgressMode.Add);

            if (ProgressReported != null) {
                ProgressReported.Invoke(report);
                return;
            }

            var typedReport = new QuestProgressEvent(
                QuestObjectiveType.DefeatEnemy,
                string.IsNullOrWhiteSpace(enemyId) ? objectiveId : enemyId,
                report.Amount,
                report.Mode,
                report.QuestId,
                report.ObjectiveId);

            if (questEventBus != null) {
                questEventBus.Report(typedReport);
                return;
            }

            if (questManager == null) {
                if (logWhenQuestManagerMissing) {
                    Debug.LogWarning($"{nameof(QuestKillProgressReporter)} could not find a QuestManager for {name}.");
                }

                return;
            }

            questManager.ReportProgress(typedReport);
        }

        void ResolveReferences() {
            ResolveQuestManager();
            ResolveProfileProvider();
            ResolveEnemyBehaviour();
        }

        void ResolveQuestManager() {
            if (questManager != null || !autoFindQuestManager) return;

            questManager = FindFirstObjectByType<QuestManager>();
        }

        void ResolveProfileProvider() {
            if (profileProvider != null || !autoFindProfileProvider) return;

            profileProvider = FindFirstObjectByType<PlayerProfileRuntimeProvider>(FindObjectsInactive.Include);
        }

        void RecordProfileProgress() {
            if (!recordBattleResult) return;

            ResolveProfileProvider();
            if (profileProvider == null) return;

            profileProvider.RecordBattle();
            if (countsAsBoss) {
                profileProvider.RecordBossDefeated();
            }
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
