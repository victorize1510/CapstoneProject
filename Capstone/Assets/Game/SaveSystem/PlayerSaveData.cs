using System;
using System.Collections.Generic;
using Capstone.Game.Inventory;
using Capstone.Game.QuestSystem;

namespace Capstone.Game.SaveSystem {
    [Serializable]
    public sealed class PlayerSaveData {
        public const int CurrentVersion = 6;

        public int version = CurrentVersion;
        public string savedAtUtc = string.Empty;
        public QuestSaveData quest = new QuestSaveData();
        public InventorySaveData inventory = new InventorySaveData();
        public CurrencySaveData currency = new CurrencySaveData();
        public PetRosterSaveData pets = new PetRosterSaveData();
        public PlayerProfileSaveData profile = new PlayerProfileSaveData();
        public AchievementSaveData achievements = new AchievementSaveData();
    }

    [Serializable]
    public sealed class PlayerProfileSaveData {
        public bool captured;
        public string displayName = string.Empty;
        public string playerId = string.Empty;
        public string dateStarted = string.Empty;
        public int avatarIndex;
        public int level;
        public int currentExperience;
        public int requiredExperience;
        public double playTimeSeconds;
        public int creaturesSeen = -1;
        public int speciesCaptured = -1;
        public int codexEntries = -1;
        public int codexTotal = -1;
        public int bossesDefeated = -1;
        public int totalBattles = -1;
        public List<string> capturedSpeciesIds = new List<string>();
    }

    [Serializable]
    public sealed class AchievementSaveData {
        public bool captured;
        public List<AchievementMetricSaveData> metrics = new List<AchievementMetricSaveData>();
        public List<string> unlockedAchievementIds = new List<string>();
    }

    [Serializable]
    public sealed class AchievementMetricSaveData {
        public string metricId = string.Empty;
        public int progress;
    }

    [Serializable]
    public sealed class CurrencySaveData {
        public bool captured;
        public int gold;
    }

    [Serializable]
    public sealed class InventorySaveData {
        public bool captured;
        public int capacity = 40;
        public List<InventoryItemSaveData> items = new List<InventoryItemSaveData>();
    }

    [Serializable]
    public sealed class InventoryItemSaveData {
        public string itemId = string.Empty;
        public string displayName = string.Empty;
        public GameItemCategory category;
        public InventoryItemRarity rarity;
        public string description = string.Empty;
        public string effect = string.Empty;
        public string source = string.Empty;
        public string flavorText = string.Empty;
        public int healAmount;
        public int quantity;
        public bool stackable;
        public int maxStackSize = 1;
        public bool usableFromInventory;
        public bool consumable;
    }

    [Serializable]
    public sealed class PetRosterSaveData {
        public bool captured;
        public string activePetId = string.Empty;
        public bool activePetSummoned;
        public int boxCapacity = 60;
        public string releaseCountDateUtc = string.Empty;
        public int releasedToday;
        public List<string> partyPetIds = new List<string>();
        public List<string> boxPetIds = new List<string>();
        public List<string> releasedPetIds = new List<string>();
        public List<PetInstanceSaveData> petStates = new List<PetInstanceSaveData>();
    }

    [Serializable]
    public sealed class PetInstanceSaveData {
        public string petId = string.Empty;
        public string definitionId = string.Empty;
        public PetCustomizationSaveData customization = new PetCustomizationSaveData();
        public PetRuntimeStatsSaveData runtimeStats = new PetRuntimeStatsSaveData();
    }

    [Serializable]
    public sealed class PetCustomizationSaveData {
        public string nickname = string.Empty;
        public bool favorite;
        public int experience;
        public int experienceToNextLevel;
        public int attack;
        public int defense;
        public int speed;
        public float criticalRate;
        public float criticalDamagePercent;
        public long obtainedOrder;
        public string captureDate = string.Empty;
        public string captureLocation = string.Empty;
        public string trainerName = string.Empty;
        public string personality = string.Empty;
        public bool codexRegistered;
        public string codexNumber = string.Empty;
        public string codexDescription = string.Empty;
        public bool nextEvolutionDiscovered;
        public string currentFormId = "base";
        public int evolutionStage;
        public List<string> promptedEvolutionIds = new List<string>();
        public List<PetResourceInvestmentSaveData> resourceInvestments = new List<PetResourceInvestmentSaveData>();
    }

    [Serializable]
    public sealed class PetResourceInvestmentSaveData {
        public string itemId = string.Empty;
        public string displayName = string.Empty;
        public int quantity;
        public bool refundable = true;
    }

    [Serializable]
    public sealed class PetRuntimeStatsSaveData {
        public bool captured;
        public bool hasSkillLoadout;
        public int level;
        public float health;
        public float maxHealth;
        public float energy;
        public float maxEnergy;
        public int equippedSkillSlotCount = 4;
        public List<string> equippedSkillIds = new List<string>();
        public List<PetSkillProgressSaveData> learnedSkillProgress = new List<PetSkillProgressSaveData>();
    }

    [Serializable]
    public sealed class PetSkillProgressSaveData {
        public string skillId = string.Empty;
        public int level = 1;
        public bool unlocked;
        public bool usable;
    }
}
