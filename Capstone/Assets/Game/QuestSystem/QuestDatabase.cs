using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    [CreateAssetMenu(fileName = "QuestDatabase", menuName = "Capstone/Quest/Quest Database")]
    public sealed class QuestDatabase : ScriptableObject {
        [SerializeField] List<QuestDefinition> quests = new List<QuestDefinition>();

        public IReadOnlyList<QuestDefinition> Quests => quests;

        public QuestDefinition FindById(string questId) {
            if (string.IsNullOrWhiteSpace(questId)) return null;

            foreach (var quest in quests) {
                if (quest != null && quest.QuestId == questId) {
                    return quest;
                }
            }

            return null;
        }
    }
}
