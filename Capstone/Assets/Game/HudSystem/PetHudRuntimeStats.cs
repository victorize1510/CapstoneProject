using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetHudRuntimeStats : MonoBehaviour, IPetHudDataSource, IPetSkillRequestReceiver {
        [Header("Identity")]
        [SerializeField] string displayName = string.Empty;
        [SerializeField] Sprite icon = null;
        [SerializeField, Min(0)] int level;

        [Header("Status")]
        [SerializeField, Min(0f)] float health;
        [SerializeField, Min(0f)] float maxHealth;
        [SerializeField, Min(0f)] float energy;
        [SerializeField, Min(0f)] float maxEnergy;

        [Header("Skills")]
        [SerializeField] List<SkillHudData> skills = new List<SkillHudData>();

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public int Level => Mathf.Max(0, level);
        public float Health => maxHealth > 0f ? Mathf.Clamp(health, 0f, maxHealth) : 0f;
        public float MaxHealth => Mathf.Max(0f, maxHealth);
        public float Energy => maxEnergy > 0f ? Mathf.Clamp(energy, 0f, maxEnergy) : 0f;
        public float MaxEnergy => Mathf.Max(0f, maxEnergy);
        public Sprite Icon => icon;

        public event Action HudDataChanged;
        public event Action<int> SkillRequested;

        void OnValidate() {
            maxHealth = Mathf.Max(0f, maxHealth);
            maxEnergy = Mathf.Max(0f, maxEnergy);
            health = maxHealth > 0f ? Mathf.Clamp(health, 0f, maxHealth) : 0f;
            energy = maxEnergy > 0f ? Mathf.Clamp(energy, 0f, maxEnergy) : 0f;

            if (skills.Count > 4) skills.RemoveRange(4, skills.Count - 4);
        }

        public IReadOnlyList<SkillHudData> GetSkills() {
            return skills;
        }

        public void SetIdentity(string nextName, int nextLevel, Sprite nextIcon = null) {
            displayName = nextName ?? string.Empty;
            level = Mathf.Max(0, nextLevel);
            if (nextIcon != null) icon = nextIcon;
            HudDataChanged?.Invoke();
        }

        public void SetStatus(float nextHealth, float nextMaxHealth, float nextEnergy, float nextMaxEnergy) {
            maxHealth = Mathf.Max(0f, nextMaxHealth);
            maxEnergy = Mathf.Max(0f, nextMaxEnergy);
            health = maxHealth > 0f ? Mathf.Clamp(nextHealth, 0f, maxHealth) : 0f;
            energy = maxEnergy > 0f ? Mathf.Clamp(nextEnergy, 0f, maxEnergy) : 0f;
            HudDataChanged?.Invoke();
        }

        public void SetSkills(IEnumerable<SkillHudData> nextSkills) {
            skills.Clear();
            if (nextSkills != null) {
                foreach (SkillHudData skill in nextSkills) {
                    if (skills.Count >= 4) break;
                    skills.Add(skill);
                }
            }

            HudDataChanged?.Invoke();
        }

        public void RequestSkill(int skillIndex) {
            if (skillIndex < 0 || skillIndex >= skills.Count) return;
            SkillHudData skill = skills[skillIndex];
            if (!skill.unlocked || !skill.usable) return;
            SkillRequested?.Invoke(skillIndex);
        }
    }
}
