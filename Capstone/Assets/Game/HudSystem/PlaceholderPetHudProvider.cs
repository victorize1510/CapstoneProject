using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PlaceholderPetHudProvider : MonoBehaviour, IPetHudProvider {
        [Header("Prototype Data")]
        [SerializeField] List<PetSlotHudData> petSlots = new List<PetSlotHudData>();
        [SerializeField] List<SkillHudData> skills = new List<SkillHudData>();
        [SerializeField] int selectedSlotIndex;
        [SerializeField, Min(1)] int selectedPetLevel = 18;
        [SerializeField, Min(0f)] float selectedPetHealth = 2450f;
        [SerializeField, Min(1f)] float selectedPetMaxHealth = 2450f;
        [SerializeField, Min(0f)] float selectedPetEnergy = 350f;
        [SerializeField, Min(1f)] float selectedPetMaxEnergy = 850f;
        [SerializeField, Min(0.02f)] float cooldownHudRefreshInterval = 0.05f;

        readonly List<SkillHudData> runtimeSkills = new List<SkillHudData>(4);
        readonly List<float> cooldownRemaining = new List<float>(4);
        bool cooldownHudDirty;
        float nextCooldownHudRefreshAt;

        public event Action HudDataChanged;

        void Reset() {
            EnsureDefaults();
        }

        void Awake() {
            EnsureDefaults();
        }

        void Update() {
            EnsureDefaults();
            cooldownHudDirty |= TickCooldowns(Time.deltaTime);
            if (!cooldownHudDirty || Time.unscaledTime < nextCooldownHudRefreshAt) return;

            cooldownHudDirty = false;
            nextCooldownHudRefreshAt = Time.unscaledTime + cooldownHudRefreshInterval;
            HudDataChanged?.Invoke();
        }

        void OnValidate() {
            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, 5);
            selectedPetMaxHealth = Mathf.Max(1f, selectedPetMaxHealth);
            selectedPetMaxEnergy = Mathf.Max(1f, selectedPetMaxEnergy);
            cooldownHudRefreshInterval = Mathf.Max(0.02f, cooldownHudRefreshInterval);
            selectedPetHealth = Mathf.Clamp(selectedPetHealth, 0f, selectedPetMaxHealth);
            selectedPetEnergy = Mathf.Clamp(selectedPetEnergy, 0f, selectedPetMaxEnergy);
            EnsureDefaults();
        }

        public PetStatusHudData GetSelectedPetStatus() {
            EnsureDefaults();

            PetSlotHudData selected = petSlots[Mathf.Clamp(selectedSlotIndex, 0, petSlots.Count - 1)];
            return new PetStatusHudData {
                hasPet = selected.occupied,
                displayName = string.IsNullOrWhiteSpace(selected.displayName) ? "Selected Pet" : selected.displayName,
                level = selected.level > 0 ? selected.level : selectedPetLevel,
                health = selectedPetHealth,
                maxHealth = selectedPetMaxHealth,
                energy = selectedPetEnergy,
                maxEnergy = selectedPetMaxEnergy,
                icon = selected.icon
            };
        }

        public IReadOnlyList<PetSlotHudData> GetPetSlots() {
            EnsureDefaults();
            return petSlots;
        }

        public IReadOnlyList<SkillHudData> GetSkills() {
            EnsureDefaults();
            runtimeSkills.Clear();
            for (int i = 0; i < skills.Count && i < 4; i++) {
                SkillHudData skill = skills[i];
                float remaining = i < cooldownRemaining.Count ? cooldownRemaining[i] : 0f;
                if (remaining > 0.001f) {
                    skill.cooldownRemainingSeconds = remaining;
                    skill.cooldownPercent = skill.cooldownSeconds > 0f
                        ? Mathf.Clamp01(remaining / skill.cooldownSeconds)
                        : Mathf.Clamp01(skill.cooldownPercent);
                    skill.usable = false;
                }
                else {
                    skill.cooldownRemainingSeconds = 0f;
                    skill.cooldownPercent = 0f;
                }

                runtimeSkills.Add(skill);
            }

            return runtimeSkills;
        }

        public void SelectPetSlot(int slotIndex) {
            EnsureDefaults();
            if (slotIndex < 0 || slotIndex >= petSlots.Count || !petSlots[slotIndex].occupied) return;

            selectedSlotIndex = slotIndex;
            for (int i = 0; i < petSlots.Count; i++) {
                PetSlotHudData slot = petSlots[i];
                slot.selected = i == selectedSlotIndex;
                petSlots[i] = slot;
            }

            HudDataChanged?.Invoke();
        }

        public void RequestSkill(int skillIndex) {
            if (skillIndex < 0 || skillIndex >= skills.Count) return;

            SkillHudData skill = skills[skillIndex];
            if (!skill.unlocked || !skill.usable) return;
            if (skillIndex < cooldownRemaining.Count && cooldownRemaining[skillIndex] > 0.001f) return;

            Debug.Log($"HUD skill request placeholder: {skill.displayName}");
            if (skill.cooldownSeconds > 0f && skillIndex < cooldownRemaining.Count) {
                cooldownRemaining[skillIndex] = skill.cooldownSeconds;
                HudDataChanged?.Invoke();
            }
        }

        void EnsureDefaults() {
            while (petSlots.Count < 6) {
                int index = petSlots.Count;
                petSlots.Add(new PetSlotHudData {
                    occupied = index == 0,
                    selected = index == selectedSlotIndex,
                    summoned = index == selectedSlotIndex && index == 0,
                    displayName = index == 0 ? "Starter Pet" : string.Empty,
                    level = index == 0 ? selectedPetLevel : 0
                });
            }

            if (petSlots.Count > 6) petSlots.RemoveRange(6, petSlots.Count - 6);

            for (int i = 0; i < petSlots.Count; i++) {
                PetSlotHudData slot = petSlots[i];
                slot.selected = i == selectedSlotIndex && slot.occupied;
                slot.summoned = slot.occupied && i == selectedSlotIndex;
                petSlots[i] = slot;
            }

            while (skills.Count < 4) {
                int index = skills.Count + 1;
                skills.Add(new SkillHudData {
                    unlocked = true,
                    usable = true,
                    displayName = "Skill " + index,
                    skillLevel = 1,
                    description = "Prototype pet skill.",
                    animatorStates = DefaultAnimatorStates(index - 1),
                    animationDuration = 0.85f,
                    animationFade = 0.08f,
                    cooldownSeconds = 2f + index,
                    cooldownPercent = 0f
                });
            }

            if (skills.Count > 4) skills.RemoveRange(4, skills.Count - 4);

            for (int i = 0; i < skills.Count; i++) {
                SkillHudData skill = skills[i];
                skill.skillLevel = Mathf.Max(1, skill.skillLevel);
                skill.cooldownPercent = Mathf.Clamp01(skill.cooldownPercent);
                skill.cooldownSeconds = Mathf.Max(0f, skill.cooldownSeconds);
                skill.animationDuration = Mathf.Max(0f, skill.animationDuration);
                skill.animationFade = Mathf.Max(0f, skill.animationFade);
                skills[i] = skill;
            }

            while (cooldownRemaining.Count < skills.Count && cooldownRemaining.Count < 4) cooldownRemaining.Add(0f);
            if (cooldownRemaining.Count > skills.Count) cooldownRemaining.RemoveRange(skills.Count, cooldownRemaining.Count - skills.Count);
        }

        bool TickCooldowns(float deltaTime) {
            if (deltaTime <= 0f || cooldownRemaining.Count == 0) return false;

            bool changed = false;
            for (int i = 0; i < cooldownRemaining.Count; i++) {
                float remaining = cooldownRemaining[i];
                if (remaining <= 0f) continue;

                cooldownRemaining[i] = Mathf.Max(0f, remaining - deltaTime);
                changed = true;
            }

            return changed;
        }

        static string[] DefaultAnimatorStates(int index) {
            switch (index) {
                case 0: return new[] { "Bite Attack", "Bite Attack Low" };
                case 1: return new[] { "Blast Attack", "Cast Spell" };
                case 2: return new[] { "Projectile Attack", "Projectile Attack Low" };
                case 3: return new[] { "Wing Attack" };
                default: return Array.Empty<string>();
            }
        }
    }
}
