using System;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetCommandHudProvider : MonoBehaviour, IPetHudProvider {
        [SerializeField] PetCommandInput petCommandInput = null;
        [SerializeField] bool autoFindPetCommandInput = true;
        [SerializeField] bool searchDataInChildren = true;
        [SerializeField, Min(0.05f)] float changeCheckInterval = 0.2f;

        readonly List<PetSlotHudData> slots = new List<PetSlotHudData>(6);
        readonly List<SkillHudData> skills = new List<SkillHudData>(4);
        readonly PetController[] lastSlots = new PetController[6];

        PetController lastActivePet;
        float nextChangeCheckAt;

        public event Action HudDataChanged;

        void Awake() {
            ResolveReferences();
            RememberCurrentParty();
        }

        void OnEnable() {
            ResolveReferences();
            RememberCurrentParty();
        }

        void Update() {
            if (Time.unscaledTime < nextChangeCheckAt) return;
            nextChangeCheckAt = Time.unscaledTime + changeCheckInterval;

            ResolveReferences();
            if (PartyChanged()) {
                RememberCurrentParty();
                HudDataChanged?.Invoke();
            }
        }

        public void Bind(PetCommandInput input) {
            petCommandInput = input;
            RememberCurrentParty();
            HudDataChanged?.Invoke();
        }

        public PetStatusHudData GetSelectedPetStatus() {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) {
                return new PetStatusHudData {
                    hasPet = false,
                    displayName = "No Pet"
                };
            }

            IPetHudDataSource source = FindDataSource(pet);
            return new PetStatusHudData {
                hasPet = true,
                displayName = CleanDisplayName(source?.DisplayName, pet.name),
                level = source != null ? source.Level : 0,
                health = source != null ? source.Health : 0f,
                maxHealth = source != null ? source.MaxHealth : 0f,
                energy = source != null ? source.Energy : 0f,
                maxEnergy = source != null ? source.MaxEnergy : 0f,
                icon = source?.Icon
            };
        }

        public IReadOnlyList<PetSlotHudData> GetPetSlots() {
            ResolveReferences();
            slots.Clear();

            for (int i = 0; i < 6; i++) {
                PetController pet = GetPetAt(i);
                IPetHudDataSource source = pet != null ? FindDataSource(pet) : null;
                slots.Add(new PetSlotHudData {
                    occupied = pet != null,
                    selected = pet != null && pet == GetSelectedPet(),
                    displayName = pet != null ? CleanDisplayName(source?.DisplayName, pet.name) : string.Empty,
                    level = source != null ? source.Level : 0,
                    icon = source?.Icon
                });
            }

            return slots;
        }

        public IReadOnlyList<SkillHudData> GetSkills() {
            ResolveReferences();
            skills.Clear();

            PetController pet = GetSelectedPet();
            IPetHudDataSource source = pet != null ? FindDataSource(pet) : null;
            IReadOnlyList<SkillHudData> sourceSkills = source?.GetSkills();
            if (sourceSkills == null) return skills;

            for (int i = 0; i < sourceSkills.Count && i < 4; i++) {
                skills.Add(sourceSkills[i]);
            }

            return skills;
        }

        public void SelectPetSlot(int slotIndex) {
            ResolveReferences();
            PetController pet = GetPetAt(slotIndex);
            if (pet == null || petCommandInput == null) return;

            petCommandInput.SetActivePet(pet);
            RememberCurrentParty();
            HudDataChanged?.Invoke();
        }

        public void RequestSkill(int skillIndex) {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) return;

            foreach (MonoBehaviour behaviour in GetPetBehaviours(pet)) {
                if (behaviour is IPetSkillRequestReceiver receiver) {
                    receiver.RequestSkill(skillIndex);
                    return;
                }
            }
        }

        void ResolveReferences() {
            if (petCommandInput != null || !autoFindPetCommandInput) return;
            petCommandInput = FindFirstObjectByType<PetCommandInput>();
        }

        PetController GetSelectedPet() {
            if (petCommandInput == null) return null;
            if (petCommandInput.activePet != null) return petCommandInput.activePet;

            for (int i = 0; i < 6; i++) {
                PetController pet = GetPetAt(i);
                if (pet != null) return pet;
            }

            return null;
        }

        PetController GetPetAt(int slotIndex) {
            if (petCommandInput == null || petCommandInput.petSlots == null) return null;
            if (slotIndex < 0 || slotIndex >= petCommandInput.petSlots.Length) return null;
            return petCommandInput.petSlots[slotIndex];
        }

        IPetHudDataSource FindDataSource(PetController pet) {
            foreach (MonoBehaviour behaviour in GetPetBehaviours(pet)) {
                if (behaviour is IPetHudDataSource source) return source;
            }

            return null;
        }

        IEnumerable<MonoBehaviour> GetPetBehaviours(PetController pet) {
            if (pet == null) yield break;

            MonoBehaviour[] behaviours = searchDataInChildren
                ? pet.GetComponentsInChildren<MonoBehaviour>(true)
                : pet.GetComponents<MonoBehaviour>();

            foreach (MonoBehaviour behaviour in behaviours) {
                if (behaviour != null) yield return behaviour;
            }
        }

        bool PartyChanged() {
            if (petCommandInput == null) return lastActivePet != null;
            if (lastActivePet != petCommandInput.activePet) return true;

            for (int i = 0; i < lastSlots.Length; i++) {
                if (lastSlots[i] != GetPetAt(i)) return true;
            }

            return false;
        }

        void RememberCurrentParty() {
            if (petCommandInput == null) {
                lastActivePet = null;
                Array.Clear(lastSlots, 0, lastSlots.Length);
                return;
            }

            lastActivePet = petCommandInput.activePet;
            for (int i = 0; i < lastSlots.Length; i++) {
                lastSlots[i] = GetPetAt(i);
            }
        }

        static string CleanDisplayName(string preferred, string fallback) {
            string value = !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback;
            if (string.IsNullOrWhiteSpace(value)) return "Pet";
            return value.Replace("(Clone)", string.Empty).Trim();
        }
    }
}
