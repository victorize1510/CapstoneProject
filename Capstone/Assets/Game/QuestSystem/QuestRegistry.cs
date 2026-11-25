using System.Collections.Generic;

namespace Capstone.Game.QuestSystem {
    public sealed class QuestRegistry {
        readonly Dictionary<string, QuestDefinition> questsById = new Dictionary<string, QuestDefinition>();
        readonly List<QuestDefinition> quests = new List<QuestDefinition>();
        readonly List<string> duplicateIds = new List<string>();

        public IReadOnlyList<QuestDefinition> Quests => quests;
        public IReadOnlyList<string> DuplicateIds => duplicateIds;

        public void Rebuild(IEnumerable<QuestDefinition> definitions) {
            questsById.Clear();
            quests.Clear();
            duplicateIds.Clear();

            if (definitions == null) return;

            foreach (var definition in definitions) {
                if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId)) continue;

                if (questsById.ContainsKey(definition.QuestId)) {
                    if (!duplicateIds.Contains(definition.QuestId)) {
                        duplicateIds.Add(definition.QuestId);
                    }

                    continue;
                }

                questsById.Add(definition.QuestId, definition);
                quests.Add(definition);
            }
        }

        public QuestDefinition Find(string questId) {
            return !string.IsNullOrWhiteSpace(questId) && questsById.TryGetValue(questId, out var definition)
                ? definition
                : null;
        }

        public void Register(QuestDefinition definition) {
            if (definition == null || string.IsNullOrWhiteSpace(definition.QuestId)) return;
            if (questsById.ContainsKey(definition.QuestId)) return;

            questsById.Add(definition.QuestId, definition);
            quests.Add(definition);
        }
    }
}
