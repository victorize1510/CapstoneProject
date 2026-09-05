using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [Serializable]
    public struct PetStatusHudData {
        public bool hasPet;
        public string displayName;
        public int level;
        public float health;
        public float maxHealth;
        public float energy;
        public float maxEnergy;
        public Sprite icon;

        public float HealthPercent => maxHealth > 0f ? Mathf.Clamp01(health / maxHealth) : 0f;
        public float EnergyPercent => maxEnergy > 0f ? Mathf.Clamp01(energy / maxEnergy) : 0f;
    }

    [Serializable]
    public struct PetSlotHudData {
        public bool occupied;
        public bool selected;
        public bool summoned;
        public bool favorite;
        public string displayName;
        public int level;
        public Sprite icon;
    }

    [Serializable]
    public struct SkillHudData {
        public string skillId;
        public string[] legacySkillIds;
        public bool unlocked;
        public bool usable;
        public string displayName;
        [Min(1)] public int skillLevel;
        [Min(1)] public int requiredPetLevel;
        public PetElement element;
        [TextArea] public string description;
        public Sprite icon;
        public string[] animatorStates;
        [Min(0f)] public float animationDuration;
        [Min(0f)] public float animationFade;
        [Min(0f)] public float windupSeconds;
        [Min(0f)] public float recoverySeconds;
        [Range(0f, 1f)] public float cooldownPercent;
        [Min(0f)] public float cooldownSeconds;
        [NonSerialized] public float cooldownRemainingSeconds;

        public float CooldownPercent {
            get {
                if (cooldownSeconds > 0f && cooldownRemainingSeconds > 0f) {
                    return Mathf.Clamp01(cooldownRemainingSeconds / cooldownSeconds);
                }

                return Mathf.Clamp01(cooldownPercent);
            }
        }

        public bool IsCoolingDown => CooldownPercent > 0.001f || cooldownRemainingSeconds > 0.001f;
        public int RequiredPetLevel => Mathf.Max(1, requiredPetLevel);
    }

    public interface IPetHudProvider {
        event Action HudDataChanged;

        PetStatusHudData GetSelectedPetStatus();
        IReadOnlyList<PetSlotHudData> GetPetSlots();
        IReadOnlyList<SkillHudData> GetSkills();
        void SelectPetSlot(int slotIndex);
        void RequestSkill(int skillIndex);
    }

    public interface IPetHudDataSource {
        event Action HudDataChanged;

        string DisplayName { get; }
        int Level { get; }
        float Health { get; }
        float MaxHealth { get; }
        float Energy { get; }
        float MaxEnergy { get; }
        Sprite Icon { get; }
        IReadOnlyList<SkillHudData> GetSkills();
    }

    public interface IPetSkillRequestReceiver {
        void RequestSkill(int skillIndex);
    }

    public interface IPetSkillLoadoutDataSource {
        int EquippedSkillSlotCount { get; }
        IReadOnlyList<SkillHudData> GetLearnedSkills();
        bool TryEquipLearnedSkill(int learnedSkillIndex, int equippedSlotIndex);
    }
}
