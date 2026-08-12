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
        public string displayName;
        public int level;
        public Sprite icon;
    }

    [Serializable]
    public struct SkillHudData {
        public bool unlocked;
        public bool usable;
        public string displayName;
        public Sprite icon;
        [Range(0f, 1f)] public float cooldownPercent;
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
}
