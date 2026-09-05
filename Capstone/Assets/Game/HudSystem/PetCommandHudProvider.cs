using System;
using System.Collections.Generic;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetCommandHudProvider : MonoBehaviour, IPetHudProvider {
        [SerializeField] PetCommandInput petCommandInput = null;
        [SerializeField] PlayerSaveController saveController = null;
        [SerializeField] bool autoFindPetCommandInput = true;
        [SerializeField] bool searchDataInChildren = true;
        [SerializeField, Min(0.05f)] float changeCheckInterval = 0.2f;

        readonly List<PetSlotHudData> slots = new List<PetSlotHudData>(6);
        readonly List<SkillHudData> skills = new List<SkillHudData>(4);
        readonly List<SkillHudData> learnedSkills = new List<SkillHudData>();
        readonly List<SkillHudData> fallbackSkills = new List<SkillHudData>(4);
        readonly List<float> fallbackCooldownRemaining = new List<float>(4);
        readonly PetController[] lastSlots = new PetController[6];

        PetController lastActivePet;
        float nextChangeCheckAt;
        bool cooldownHudDirty;

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
            cooldownHudDirty |= TickFallbackCooldowns(Time.deltaTime);

            if (Time.unscaledTime < nextChangeCheckAt) return;
            nextChangeCheckAt = Time.unscaledTime + changeCheckInterval;

            ResolveReferences();
            if (PartyChanged()) {
                RememberCurrentParty();
                cooldownHudDirty = false;
                HudDataChanged?.Invoke();
                return;
            }

            if (!cooldownHudDirty) return;

            cooldownHudDirty = false;
            HudDataChanged?.Invoke();
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
            PetCollectionMetadata metadata = FindMetadata(pet, true);
            string baseName = CleanDisplayName(source?.DisplayName, pet.name);
            return new PetStatusHudData {
                hasPet = true,
                displayName = metadata != null ? metadata.ResolveDisplayName(baseName) : baseName,
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
                PetCollectionMetadata metadata = pet != null ? FindMetadata(pet, true) : null;
                string baseName = pet != null ? CleanDisplayName(source?.DisplayName, pet.name) : string.Empty;
                slots.Add(new PetSlotHudData {
                    occupied = pet != null,
                    selected = pet != null && pet == GetSelectedPet(),
                    summoned = pet != null && pet.IsSummoned,
                    favorite = metadata != null && metadata.IsFavorite,
                    displayName = metadata != null ? metadata.ResolveDisplayName(baseName) : baseName,
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
            if (pet == null) return skills;

            IPetHudDataSource source = pet != null ? FindDataSource(pet) : null;
            IReadOnlyList<SkillHudData> sourceSkills = source?.GetSkills();
            if (sourceSkills == null || !HasVisibleSkill(sourceSkills)) {
                return GetFallbackSkills();
            }

            for (int i = 0; i < sourceSkills.Count && i < 4; i++) {
                skills.Add(sourceSkills[i]);
            }

            return skills;
        }

        public PetController GetSelectedPetController() {
            ResolveReferences();
            return GetSelectedPet();
        }

        public PetController GetPetControllerAt(int slotIndex) {
            ResolveReferences();
            return slotIndex >= 0 && slotIndex < 6 ? GetPetAt(slotIndex) : null;
        }

        public void NotifyHudDataChanged() {
            HudDataChanged?.Invoke();
        }

        public IReadOnlyList<SkillHudData> GetLearnedSkills() {
            ResolveReferences();
            learnedSkills.Clear();

            PetController pet = GetSelectedPet();
            if (pet == null) return learnedSkills;

            IPetSkillLoadoutDataSource loadout = FindSkillLoadoutSource(pet);
            IReadOnlyList<SkillHudData> sourceSkills = loadout?.GetLearnedSkills();
            if (sourceSkills == null || sourceSkills.Count == 0) sourceSkills = GetSkills();

            for (int i = 0; i < sourceSkills.Count; i++) {
                learnedSkills.Add(sourceSkills[i]);
            }

            return learnedSkills;
        }

        public bool TryEquipLearnedSkill(int learnedSkillIndex, int equippedSlotIndex) {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) return false;

            PetHudRuntimeStats runtimeStats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            if (saveController == null || runtimeStats == null) return false;
            PetRuntimeStatsSaveData snapshot = runtimeStats.CreateSaveData();

            IPetSkillLoadoutDataSource loadout = FindSkillLoadoutSource(pet);
            if (loadout == null || !loadout.TryEquipLearnedSkill(learnedSkillIndex, equippedSlotIndex)) return false;
            if (!saveController.SaveNow()) {
                runtimeStats.RestoreFromSaveData(snapshot);
                return false;
            }

            fallbackCooldownRemaining.Clear();
            HudDataChanged?.Invoke();
            return true;
        }

        public PetElement GetSelectedPetElement() {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) return PetElement.Unknown;

            PetCollectionMetadata metadata = FindMetadata(pet, true);
            return metadata != null ? metadata.Element : PetElement.Unknown;
        }

        public int GetEquippedSkillSlotCount() {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) return 0;

            IPetSkillLoadoutDataSource loadout = FindSkillLoadoutSource(pet);
            if (loadout != null) return Mathf.Clamp(loadout.EquippedSkillSlotCount, 2, 4);

            IPetHudDataSource source = FindDataSource(pet);
            int skillCount = source?.GetSkills()?.Count ?? 0;
            return Mathf.Clamp(skillCount > 0 ? skillCount : 4, 2, 4);
        }

        public bool TryRenameSelectedPet(string nickname, out string error) {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) {
                error = "Chưa chọn pet để đổi tên.";
                return false;
            }

            PetCollectionMetadata metadata = FindMetadata(pet, true);
            if (metadata == null) {
                error = "Không thể tạo dữ liệu tùy chỉnh cho pet.";
                return false;
            }
            if (saveController == null) {
                error = "Không tìm thấy PlayerSaveController để lưu tên pet.";
                return false;
            }

            PetCustomizationSaveData snapshot = metadata.CreateSaveData();
            if (!metadata.TrySetNickname(nickname, out error)) return false;
            if (!saveController.SaveNow()) {
                metadata.RestoreFromSaveData(snapshot);
                error = "Không thể lưu tên mới. Thay đổi đã được hoàn tác.";
                return false;
            }
            HudDataChanged?.Invoke();
            return true;
        }

        public bool TryToggleSelectedPetFavorite(out bool favorite) {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) {
                favorite = false;
                return false;
            }

            PetCollectionMetadata metadata = FindMetadata(pet, true);
            if (metadata == null) {
                favorite = false;
                return false;
            }

            if (saveController == null) {
                favorite = metadata.IsFavorite;
                return false;
            }

            PetCustomizationSaveData snapshot = metadata.CreateSaveData();
            favorite = metadata.ToggleFavorite();
            if (!saveController.SaveNow()) {
                metadata.RestoreFromSaveData(snapshot);
                favorite = metadata.IsFavorite;
                return false;
            }
            HudDataChanged?.Invoke();
            return true;
        }

        public bool IsSelectedPetFavorite() {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            PetCollectionMetadata metadata = pet != null ? FindMetadata(pet, true) : null;
            return metadata != null && metadata.IsFavorite;
        }

        public void SelectPetSlot(int slotIndex) {
            ResolveReferences();
            PetController pet = GetPetAt(slotIndex);
            if (pet == null || petCommandInput == null) return;

            petCommandInput.SetActivePet(pet);
            RememberCurrentParty();
            saveController?.RequestSave();
            HudDataChanged?.Invoke();
        }

        public bool TrySwapPetSlots(int sourceIndex, int targetIndex) {
            ResolveReferences();
            if (petCommandInput == null || petCommandInput.petSlots == null) return false;
            if (sourceIndex < 0 || sourceIndex >= petCommandInput.petSlots.Length) return false;
            if (targetIndex < 0 || targetIndex >= petCommandInput.petSlots.Length) return false;
            if (sourceIndex == targetIndex) return false;

            PetController source = petCommandInput.petSlots[sourceIndex];
            PetController target = petCommandInput.petSlots[targetIndex];
            if (source == null && target == null) return false;
            if (saveController == null) return false;

            petCommandInput.petSlots[sourceIndex] = target;
            petCommandInput.petSlots[targetIndex] = source;
            if (!saveController.SaveNow()) {
                petCommandInput.petSlots[sourceIndex] = source;
                petCommandInput.petSlots[targetIndex] = target;
                RememberCurrentParty();
                HudDataChanged?.Invoke();
                return false;
            }
            RememberCurrentParty();
            HudDataChanged?.Invoke();
            return true;
        }

        public void RequestSkill(int skillIndex) {
            ResolveReferences();
            PetController pet = GetSelectedPet();
            if (pet == null) return;

            bool sentToReceiver = false;
            foreach (MonoBehaviour behaviour in GetPetBehaviours(pet)) {
                if (behaviour is IPetSkillRequestReceiver receiver) {
                    receiver.RequestSkill(skillIndex);
                    sentToReceiver = true;
                }
            }

            if (sentToReceiver) return;

            SkillHudData fallback = GetFallbackSkill(skillIndex);
            if (!fallback.unlocked || !fallback.usable) return;
            EnsureFallbackCooldownStorage();
            if (skillIndex < fallbackCooldownRemaining.Count && fallbackCooldownRemaining[skillIndex] > 0.001f) return;

            float duration = fallback.animationDuration > 0f ? fallback.animationDuration : 0.85f;
            float fade = fallback.animationFade > 0f ? fallback.animationFade : 0.08f;
            float windup = Mathf.Max(0f, fallback.windupSeconds);
            float recovery = Mathf.Max(0f, fallback.recoverySeconds);
            pet.PlaySkillAnimation(fallback.animatorStates, duration, fade, true, true, windup, recovery);

            if (fallback.cooldownSeconds > 0f && skillIndex < fallbackCooldownRemaining.Count) {
                fallbackCooldownRemaining[skillIndex] = fallback.cooldownSeconds;
                HudDataChanged?.Invoke();
            }
        }

        void ResolveReferences() {
            if (petCommandInput == null && autoFindPetCommandInput) {
                petCommandInput = FindFirstObjectByType<PetCommandInput>();
            }
            if (saveController == null) {
                saveController = FindFirstObjectByType<PlayerSaveController>(FindObjectsInactive.Include);
            }
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

        IPetSkillLoadoutDataSource FindSkillLoadoutSource(PetController pet) {
            foreach (MonoBehaviour behaviour in GetPetBehaviours(pet)) {
                if (behaviour is IPetSkillLoadoutDataSource source) return source;
            }

            return null;
        }

        static PetCollectionMetadata FindMetadata(PetController pet, bool createIfMissing = false) {
            if (pet == null) return null;
            PetCollectionMetadata metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null && createIfMissing) metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            return metadata;
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

        IReadOnlyList<SkillHudData> GetFallbackSkills() {
            EnsureFallbackCooldownStorage();
            fallbackSkills.Clear();

            for (int i = 0; i < 4; i++) {
                SkillHudData skill = GetFallbackSkill(i);
                float remaining = i < fallbackCooldownRemaining.Count ? fallbackCooldownRemaining[i] : 0f;
                if (remaining > 0.001f) {
                    skill.cooldownRemainingSeconds = remaining;
                    skill.cooldownPercent = skill.cooldownSeconds > 0f
                        ? Mathf.Clamp01(remaining / skill.cooldownSeconds)
                        : skill.cooldownPercent;
                    skill.usable = false;
                }
                else {
                    skill.cooldownRemainingSeconds = 0f;
                    skill.cooldownPercent = 0f;
                }

                fallbackSkills.Add(skill);
            }

            return fallbackSkills;
        }

        static SkillHudData GetFallbackSkill(int index) {
            switch (index) {
                case 0:
                    return CreateFallbackSkill("Bite", PetElement.Nature, "Test close-range bite attack.", 1.2f, 0.8f, 0.12f, 0.3f, "Bite Attack", "Bite Attack Low");
                case 1:
                    return CreateFallbackSkill("Blast", PetElement.Light, "Test roar-like magic blast.", 3.8f, 1.0f, 0f, 0f, "Blast Attack", "Cast Spell");
                case 2:
                    return CreateFallbackSkill("Projectile", PetElement.Fire, "Test ranged projectile attack.", 3.0f, 0.95f, 0f, 0f, "Projectile Attack", "Projectile Attack Low");
                case 3:
                    return CreateFallbackSkill("Wing Strike", PetElement.Wind, "Test wing attack.", 4.2f, 1.0f, 0f, 0f, "Wing Attack");
                default:
                    return default;
            }
        }

        static SkillHudData CreateFallbackSkill(string name, PetElement element, string description, float cooldown, float duration, float windup, float recovery, params string[] animatorStates) {
            return new SkillHudData {
                unlocked = true,
                usable = true,
                displayName = name,
                element = element,
                skillLevel = 1,
                description = description,
                animatorStates = animatorStates,
                animationDuration = duration,
                animationFade = 0.08f,
                windupSeconds = windup,
                recoverySeconds = recovery,
                cooldownSeconds = cooldown
            };
        }

        static bool HasVisibleSkill(IReadOnlyList<SkillHudData> sourceSkills) {
            if (sourceSkills == null) return false;
            for (int i = 0; i < sourceSkills.Count && i < 4; i++) {
                if (sourceSkills[i].unlocked) return true;
            }

            return false;
        }

        void EnsureFallbackCooldownStorage() {
            while (fallbackCooldownRemaining.Count < 4) {
                fallbackCooldownRemaining.Add(0f);
            }

            if (fallbackCooldownRemaining.Count > 4) {
                fallbackCooldownRemaining.RemoveRange(4, fallbackCooldownRemaining.Count - 4);
            }
        }

        bool TickFallbackCooldowns(float deltaTime) {
            if (deltaTime <= 0f || fallbackCooldownRemaining.Count == 0) return false;

            bool changed = false;
            for (int i = 0; i < fallbackCooldownRemaining.Count; i++) {
                float remaining = fallbackCooldownRemaining[i];
                if (remaining <= 0f) continue;

                fallbackCooldownRemaining[i] = Mathf.Max(0f, remaining - deltaTime);
                changed = true;
            }

            return changed;
        }
    }
}
