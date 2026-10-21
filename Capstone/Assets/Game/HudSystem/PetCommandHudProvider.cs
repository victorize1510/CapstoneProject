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
        readonly List<IPetHudDataSource> subscribedSources = new List<IPetHudDataSource>(6);
        readonly List<PetCollectionMetadata> subscribedMetadata = new List<PetCollectionMetadata>(6);

        sealed class PetSources {
            public int Frame=-1;public bool InChildren;
            public readonly List<MonoBehaviour> Components=new List<MonoBehaviour>(16);
            public IPetHudDataSource Hud;public IPetSkillLoadoutDataSource Loadout;
        }
        readonly Dictionary<PetController,PetSources> sourceCache=new Dictionary<PetController,PetSources>();
        readonly List<MonoBehaviour> emptyBehaviours=new List<MonoBehaviour>(0);
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

        void OnDisable() {
            UnsubscribePetData();sourceCache.Clear();
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

        public PetStatusHudData GetSelectedPetStatus() => StatusFor(GetSelectedPetController());

        internal PetStatusHudData StatusFor(PetController pet) {
            ResolveReferences();
            if (pet == null) {
                return new PetStatusHudData {
                    hasPet = false,
                    displayName = "No Pet"
                };
            }

            IPetHudDataSource source = FindDataSource(pet);
            PetCollectionMetadata metadata = FindMetadata(pet, true);
            string baseName = CleanDisplayName(source?.DisplayName, pet.name);
            int maxLevel = metadata != null ? metadata.MaxLevel : int.MaxValue;
            int experienceRequirement = source != null && source.Level < maxLevel
                ? PetLevelUpService.CalculateDefaultExperienceRequirement(source.Level)
                : 0;
            return new PetStatusHudData {
                hasPet = true,
                displayName = metadata != null ? metadata.ResolveDisplayName(baseName) : baseName,
                level = source != null ? source.Level : 0,
                health = source != null ? source.Health : 0f,
                maxHealth = source != null ? source.MaxHealth : 0f,
                energy = source != null ? source.Energy : 0f,
                maxEnergy = source != null ? source.MaxEnergy : 0f,
                experience = metadata != null ? metadata.Experience : 0,
                experienceToNextLevel = experienceRequirement,
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
                    health = source != null ? source.Health : 0f,
                    maxHealth = source != null ? source.MaxHealth : 0f,
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


        internal bool ContainsPet(PetController pet) { if(pet==null)return false;for(int i=0;i<6;i++)if(GetPetControllerAt(i)==pet)return true;return false; }
        internal IReadOnlyList<SkillHudData> SkillsFor(PetController pet) => pet!=null ? FindDataSource(pet)?.GetSkills() ?? Array.Empty<SkillHudData>() : Array.Empty<SkillHudData>();
        internal IReadOnlyList<SkillHudData> LearnedFor(PetController pet) => pet!=null ? FindSkillLoadoutSource(pet)?.GetLearnedSkills() ?? SkillsFor(pet) : Array.Empty<SkillHudData>();
        internal bool FavoriteFor(PetController pet,out bool favorite) { ResolveReferences();if(!ContainsPet(pet)){favorite=false;return false;}return TryTogglePetFavorite(pet,out favorite); }

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

        public bool TryEquipLearnedSkill(int learnedSkillIndex, int equippedSlotIndex) => EquipFor(GetSelectedPetController(), learnedSkillIndex, equippedSlotIndex, false);

        internal bool EquipFor(PetController pet, int learnedSkillIndex, int equippedSlotIndex, bool explicitPreview=true) {
            if (explicitPreview && (!ContainsPet(pet) || equippedSlotIndex < 0 || equippedSlotIndex >= SlotCountFor(pet))) return false;
            var freshLearned=LearnedFor(pet); var freshEquipped=SkillsFor(pet);
            if(learnedSkillIndex<0 || learnedSkillIndex>=freshLearned.Count || !freshLearned[learnedSkillIndex].unlocked) return false;
            if(explicitPreview && equippedSlotIndex>=0 && equippedSlotIndex<freshEquipped.Count && freshEquipped[equippedSlotIndex].skillId==freshLearned[learnedSkillIndex].skillId) return false;
            ResolveReferences();
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

        public PetElement GetSelectedPetElement() => ElementFor(GetSelectedPetController());

        internal PetElement ElementFor(PetController pet) {
            ResolveReferences();
            if (pet == null) return PetElement.Unknown;

            PetCollectionMetadata metadata = FindMetadata(pet, true);
            return metadata != null ? metadata.Element : PetElement.Unknown;
        }

        public int GetEquippedSkillSlotCount() => SlotCountFor(GetSelectedPetController());

        internal int SlotCountFor(PetController pet) {
            ResolveReferences();
            if (pet == null) return 0;

            IPetSkillLoadoutDataSource loadout = FindSkillLoadoutSource(pet);
            if (loadout != null) return Mathf.Clamp(loadout.EquippedSkillSlotCount, 2, 4);

            IPetHudDataSource source = FindDataSource(pet);
            int skillCount = source?.GetSkills()?.Count ?? 0;
            return Mathf.Clamp(skillCount > 0 ? skillCount : 4, 2, 4);
        }

        public bool TryRenameSelectedPet(string nickname, out string error) => RenameFor(GetSelectedPetController(), nickname, out error);

        internal bool RenameFor(PetController pet, string nickname, out string error) {
            ResolveReferences();
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
            return TryTogglePetFavorite(GetSelectedPet(), out favorite);
        }

        public bool TryTogglePetFavoriteAt(int slotIndex, out bool favorite) {
            ResolveReferences();
            return TryTogglePetFavorite(GetPetAt(slotIndex), out favorite);
        }

        bool TryTogglePetFavorite(PetController pet, out bool favorite) {
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

            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(pet, "favorite")) { favorite = metadata.IsFavorite; return false; }
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
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "swap-party")) return false;
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
            if (Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked
                || petCommandInput == null || !petCommandInput.CanUsePetSkills) return;
            PetController pet = GetSelectedPet();
            if (pet == null || pet != petCommandInput.activePet) return;

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

        PetSources SourcesFor(PetController pet){
            if(pet==null)return null;
            if(!sourceCache.TryGetValue(pet,out var entry)){entry=new PetSources();sourceCache.Add(pet,entry);}
            if(entry.Frame==Time.frameCount&&entry.InChildren==searchDataInChildren)return entry;
            entry.Frame=Time.frameCount;entry.InChildren=searchDataInChildren;entry.Components.Clear();
            if(searchDataInChildren)pet.GetComponentsInChildren<MonoBehaviour>(true,entry.Components);else pet.GetComponents<MonoBehaviour>(entry.Components);
            entry.Hud=null;entry.Loadout=null;
            foreach(var behaviour in entry.Components){if(behaviour==null)continue;if(entry.Hud==null&&behaviour is IPetHudDataSource hud)entry.Hud=hud;if(entry.Loadout==null&&behaviour is IPetSkillLoadoutDataSource loadout)entry.Loadout=loadout;}
            return entry;
        }
        IPetHudDataSource FindDataSource(PetController pet){
            var entry=SourcesFor(pet);if(entry!=null&&(entry.Hud==null||(entry.Hud is MonoBehaviour owner&&owner==null))){entry.Frame=-1;entry=SourcesFor(pet);}return entry?.Hud;
        }
        IPetSkillLoadoutDataSource FindSkillLoadoutSource(PetController pet){
            var entry=SourcesFor(pet);if(entry!=null&&(entry.Loadout==null||(entry.Loadout is MonoBehaviour owner&&owner==null))){entry.Frame=-1;entry=SourcesFor(pet);}return entry?.Loadout;
        }

        static PetCollectionMetadata FindMetadata(PetController pet, bool createIfMissing = false) {
            if (pet == null) return null;
            PetCollectionMetadata metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (metadata == null && createIfMissing) metadata = pet.gameObject.AddComponent<PetCollectionMetadata>();
            return metadata;
        }

        List<MonoBehaviour> GetPetBehaviours(PetController pet)=>SourcesFor(pet)?.Components??emptyBehaviours;

        bool PartyChanged() {
            if (petCommandInput == null) return lastActivePet != null;
            if (lastActivePet != petCommandInput.activePet) return true;

            for (int i = 0; i < lastSlots.Length; i++) {
                if (lastSlots[i] != GetPetAt(i)) return true;
            }

            return false;
        }

        void RememberCurrentParty() {
            UnsubscribePetData();sourceCache.Clear();
            if (petCommandInput == null) {
                lastActivePet = null;
                Array.Clear(lastSlots, 0, lastSlots.Length);
                return;
            }

            lastActivePet = petCommandInput.activePet;
            SubscribePetData(lastActivePet);
            for (int i = 0; i < lastSlots.Length; i++) {
                lastSlots[i] = GetPetAt(i);
                SubscribePetData(lastSlots[i]);
            }
        }

        void SubscribePetData(PetController pet) {
            if (pet == null || !isActiveAndEnabled) return;
            IPetHudDataSource source = FindDataSource(pet);
            if (source != null && !subscribedSources.Contains(source)) {
                subscribedSources.Add(source);
                source.HudDataChanged += NotifyHudDataChanged;
            }
            PetCollectionMetadata metadata = FindMetadata(pet, true);
            if (metadata != null && !subscribedMetadata.Contains(metadata)) {
                subscribedMetadata.Add(metadata);
                metadata.Changed += NotifyHudDataChanged;
            }
        }

        void UnsubscribePetData() {
            foreach (IPetHudDataSource source in subscribedSources) source.HudDataChanged -= NotifyHudDataChanged;
            foreach (PetCollectionMetadata metadata in subscribedMetadata) {
                if (metadata != null) metadata.Changed -= NotifyHudDataChanged;
            }
            subscribedSources.Clear();
            subscribedMetadata.Clear();
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
