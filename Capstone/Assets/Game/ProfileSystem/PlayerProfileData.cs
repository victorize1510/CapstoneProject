using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.ProfileSystem {
    public readonly struct PlayerProfileSnapshot {
        public readonly string DisplayName;
        public readonly string PlayerId;
        public readonly int Level;
        public readonly int CurrentExperience;
        public readonly int RequiredExperience;
        public readonly Sprite Avatar;
        public readonly double PlayTimeSeconds;
        public readonly string DateStarted;
        public readonly string CurrentArea;
        public readonly int CreaturesSeen;
        public readonly int SpeciesCaptured;
        public readonly int CodexEntries;
        public readonly int CodexTotal;
        public readonly int StoryCompleted;
        public readonly int StoryTotal;
        public readonly int BossesDefeated;
        public readonly int TotalBattles;
        public readonly AchievementSnapshot[] UnlockedBadges;
        public readonly float AchievementCompletion01;

        public PlayerProfileSnapshot(
            string displayName,
            string playerId,
            int level,
            int currentExperience,
            int requiredExperience,
            Sprite avatar,
            double playTimeSeconds,
            string dateStarted,
            string currentArea,
            int creaturesSeen,
            int speciesCaptured,
            int codexEntries,
            int codexTotal,
            int storyCompleted,
            int storyTotal,
            int bossesDefeated,
            int totalBattles,
            AchievementSnapshot[] unlockedBadges,
            float achievementCompletion01) {
            DisplayName = displayName;
            PlayerId = playerId;
            Level = level;
            CurrentExperience = currentExperience;
            RequiredExperience = requiredExperience;
            Avatar = avatar;
            PlayTimeSeconds = playTimeSeconds;
            DateStarted = dateStarted;
            CurrentArea = currentArea;
            CreaturesSeen = creaturesSeen;
            SpeciesCaptured = speciesCaptured;
            CodexEntries = codexEntries;
            CodexTotal = codexTotal;
            StoryCompleted = storyCompleted;
            StoryTotal = storyTotal;
            BossesDefeated = bossesDefeated;
            TotalBattles = totalBattles;
            UnlockedBadges = unlockedBadges ?? Array.Empty<AchievementSnapshot>();
            AchievementCompletion01 = Mathf.Clamp01(achievementCompletion01);
        }
    }

    public interface IPlayerProfileProvider {
        event Action ProfileChanged;

        bool CanChangeAvatar { get; }
        IReadOnlyList<Sprite> AvatarOptions { get; }
        int SelectedAvatarIndex { get; }
        PlayerProfileSnapshot GetSnapshot();
        bool TrySetDisplayName(string displayName, out string error);
        bool TrySelectAvatar(int avatarIndex);
        bool TrySelectNextAvatar();
    }
}
