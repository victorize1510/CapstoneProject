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

        public event Action HudDataChanged;

        void Reset() {
            EnsureDefaults();
        }

        void Awake() {
            EnsureDefaults();
        }

        void OnValidate() {
            selectedSlotIndex = Mathf.Clamp(selectedSlotIndex, 0, 5);
            selectedPetMaxHealth = Mathf.Max(1f, selectedPetMaxHealth);
            selectedPetMaxEnergy = Mathf.Max(1f, selectedPetMaxEnergy);
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
            return skills;
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

            Debug.Log($"HUD skill request placeholder: {skill.displayName}");
        }

        void EnsureDefaults() {
            while (petSlots.Count < 6) {
                int index = petSlots.Count;
                petSlots.Add(new PetSlotHudData {
                    occupied = index == 0,
                    selected = index == selectedSlotIndex,
                    displayName = index == 0 ? "Starter Pet" : string.Empty,
                    level = index == 0 ? selectedPetLevel : 0
                });
            }

            if (petSlots.Count > 6) petSlots.RemoveRange(6, petSlots.Count - 6);

            for (int i = 0; i < petSlots.Count; i++) {
                PetSlotHudData slot = petSlots[i];
                slot.selected = i == selectedSlotIndex && slot.occupied;
                petSlots[i] = slot;
            }

            while (skills.Count < 4) {
                int index = skills.Count + 1;
                skills.Add(new SkillHudData {
                    unlocked = true,
                    usable = true,
                    displayName = "Skill " + index,
                    cooldownPercent = 0f
                });
            }

            if (skills.Count > 4) skills.RemoveRange(4, skills.Count - 4);
        }
    }
}
