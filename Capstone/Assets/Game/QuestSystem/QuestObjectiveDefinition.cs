using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.QuestSystem {
    public enum QuestObjectiveType {
        CustomObjective = 0,
        Custom = CustomObjective,
        ReachLocation = 1,
        DefeatEnemy = 2,
        Defeat = DefeatEnemy,
        CollectItem = 3,
        Collect = CollectItem,
        InteractObject = 4,
        Interact = InteractObject,
        TalkToNPC = 5,
        Talk = TalkToNPC,
        UseItem = 6,
        CaptureCreature = 7,
        CaptureCreatureType = 8,
        DefeatCreature = 9,
        DefeatBoss = 10,
        GiveItem = 11,
        ExploreArea = 12,
        DiscoverLocation = 13,
        TrainerLevel = 14,
        CreatureLevel = 15,
        CompleteQuest = 16,
        DeliverPet = 17,
        ReachScene = 18
    }

    [Serializable]
    public sealed class QuestObjectiveDefinition {
        [SerializeField] string objectiveId = string.Empty;
        [SerializeField] string title = string.Empty;
        [SerializeField, TextArea] string description = string.Empty;
        [SerializeField] QuestObjectiveType objectiveType = QuestObjectiveType.CustomObjective;
        [SerializeField] int requiredAmount = 1;
        [SerializeField] string targetId = string.Empty;
        [SerializeField] string targetTypeId = string.Empty;
        [SerializeField] string regionId = string.Empty;
        [SerializeField] string customKey = string.Empty;
        [SerializeField] bool hasTargetPosition;
        [SerializeField] Vector3 targetPosition = Vector3.zero;
        [SerializeField, Min(0f)] float targetRadius = 2f;
        [SerializeField] string[] tags = Array.Empty<string>();
        [SerializeField] bool optional = false;
        [SerializeField] string[] allowedZones, allowedForms, alternateItemIds;
        [SerializeField] string sceneId, markerId;
        [SerializeField] bool nightOnly;

        public string ObjectiveId => objectiveId;
        public string Title => title;
        public string Description => description;
        public QuestObjectiveType ObjectiveType => objectiveType;
        public int RequiredAmount => Mathf.Max(1, requiredAmount);
        public string TargetId => targetId;
        public string TargetTypeId => targetTypeId;
        public string RegionId => regionId;
        public string CustomKey => customKey;
        public bool HasTargetPosition => hasTargetPosition;
        public Vector3 TargetPosition => targetPosition;
        public float TargetRadius => Mathf.Max(0f, targetRadius);
        public IReadOnlyList<string> Tags => tags;
        public bool Optional => optional;
        public IReadOnlyList<string> AllowedZones => allowedZones ?? Array.Empty<string>();
        public IReadOnlyList<string> AlternateItemIds => alternateItemIds ?? Array.Empty<string>();
        public string SceneId => sceneId;
        public string MarkerId => markerId;
        public bool IsHandover => objectiveType == QuestObjectiveType.GiveItem || objectiveType == QuestObjectiveType.DeliverPet;

        public bool MatchesCreature(string family, string form) =>
            (string.IsNullOrEmpty(targetTypeId) || string.Equals(targetTypeId, family, StringComparison.OrdinalIgnoreCase)) &&
            (allowedForms == null || allowedForms.Length == 0 || Array.Exists(allowedForms, x => x == form));

        public bool TryGetTargetPosition(out Vector3 position) {
            position = targetPosition;
            return hasTargetPosition;
        }

        public bool Matches(QuestProgressEvent progressEvent) {
            if (!string.IsNullOrWhiteSpace(progressEvent.ObjectiveId)) {
                return progressEvent.ObjectiveId == objectiveId;
            }

            if (!MatchesObjectiveType(progressEvent.ObjectiveType)) return false;
            if (!MatchesValue(targetId, progressEvent.TargetId)) return false;
            if (!MatchesCreature(progressEvent.TargetTypeId, progressEvent.FormId)) return false;
            if (!MatchesValue(regionId, progressEvent.RegionId)) return false;
            if (!MatchesValue(customKey, progressEvent.CustomKey)) return false;
            if (!MatchesValue(sceneId, progressEvent.SceneId)) return false;
            if (nightOnly && !progressEvent.StartedAtNight) return false;
            if (AllowedZones.Count > 0 && !Array.Exists(allowedZones, zone => zone == progressEvent.RegionId)) return false;
            return MatchesTags(progressEvent.Tags);
        }

        bool MatchesObjectiveType(QuestObjectiveType reportedType) {
            if (reportedType == objectiveType) return true;
            if (objectiveType == QuestObjectiveType.CaptureCreatureType && reportedType == QuestObjectiveType.CaptureCreature) return true;
            if (objectiveType == QuestObjectiveType.DefeatCreature && reportedType == QuestObjectiveType.DefeatEnemy) return true;
            return false;
        }

        static bool MatchesValue(string expected, string actual) {
            return string.IsNullOrWhiteSpace(expected) || expected == actual;
        }

        bool MatchesTags(IReadOnlyList<string> eventTags) {
            if (tags == null || tags.Length == 0) return true;
            if (eventTags == null || eventTags.Count == 0) return false;

            foreach (var requiredTag in tags) {
                if (string.IsNullOrWhiteSpace(requiredTag)) continue;

                var matched = false;
                for (var i = 0; i < eventTags.Count; i++) {
                    if (eventTags[i] == requiredTag) {
                        matched = true;
                        break;
                    }
                }

                if (!matched) return false;
            }

            return true;
        }
    }
}
