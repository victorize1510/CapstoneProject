using System.Collections.Generic;

namespace Capstone.Game.QuestSystem {
    public sealed class QuestRequirementEvaluator {
        readonly List<IQuestRequirementProvider> providers = new List<IQuestRequirementProvider>();
        readonly IQuestCompletionLookup completionLookup;

        public QuestRequirementEvaluator(IQuestCompletionLookup completionLookup) {
            this.completionLookup = completionLookup;
        }

        public void SetProviders(IEnumerable<IQuestRequirementProvider> newProviders) {
            providers.Clear();
            if (newProviders == null) return;

            foreach (var provider in newProviders) {
                if (provider != null && !providers.Contains(provider)) {
                    providers.Add(provider);
                }
            }
        }

        public bool AreMet(QuestDefinition definition, out QuestRequirementDefinition failedRequirement) {
            failedRequirement = null;
            if (definition == null) return false;

            foreach (var prerequisiteId in definition.PrerequisiteQuestIds) {
                if (string.IsNullOrWhiteSpace(prerequisiteId)) continue;
                if (completionLookup != null && completionLookup.IsQuestCompleted(prerequisiteId)) continue;

                failedRequirement = QuestRequirementFailure.Prerequisite(prerequisiteId);
                return false;
            }

            foreach (var requirement in definition.Requirements) {
                if (requirement == null || requirement.IsEmpty) continue;
                if (IsMet(requirement)) continue;

                failedRequirement = requirement;
                return false;
            }

            return true;
        }

        bool IsMet(QuestRequirementDefinition requirement) {
            if (requirement.RequirementType == QuestRequirementType.PrerequisiteQuest) {
                return completionLookup != null && completionLookup.IsQuestCompleted(requirement.TargetId);
            }

            foreach (var provider in providers) {
                if (provider != null && provider.TryEvaluateRequirement(requirement, out bool isMet)) {
                    return isMet;
                }
            }

            return false;
        }
    }

    public interface IQuestCompletionLookup {
        bool IsQuestCompleted(string questId);
    }

    static class QuestRequirementFailure {
        public static QuestRequirementDefinition Prerequisite(string questId) {
            return new QuestRequirementDefinition(QuestRequirementType.PrerequisiteQuest, questId);
        }
    }
}
