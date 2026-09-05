using System;
using System.Collections.Generic;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.HudSystem {
    [DisallowMultipleComponent]
    public sealed class PetHudRuntimeStats : MonoBehaviour, IPetHudDataSource, IPetSkillRequestReceiver, IPetSkillLoadoutDataSource {
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
        [SerializeField, Range(2, 4)] int equippedSkillSlotCount = 4;
        [SerializeField] bool usePrototypeSkillsWhenEmpty = true;
        [SerializeField] List<SkillHudData> skills = new List<SkillHudData>();
        [SerializeField] List<SkillHudData> learnedSkills = new List<SkillHudData>();

        [Header("Skill Animation")]
        [SerializeField] PetController petController = null;
        [SerializeField] Animator animator = null;
        [SerializeField] bool autoFindAnimationReferences = true;
        [SerializeField] bool stopMovementDuringSkill = true;
        [SerializeField] bool faceTargetDuringSkill = true;
        [SerializeField, Min(0.02f)] float cooldownHudRefreshInterval = 0.05f;

        readonly List<SkillHudData> runtimeSkills = new List<SkillHudData>(4);
        readonly List<SkillHudData> runtimeLearnedSkills = new List<SkillHudData>();
        readonly List<int> runtimeLearnedSkillIndices = new List<int>();
        readonly List<float> cooldownRemaining = new List<float>(4);
        bool cooldownHudDirty;
        float nextCooldownHudRefreshAt;
        bool restoredLoadout;
        public event Action PersistentDataChanged;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        public int Level => Mathf.Max(0, level);
        public float Health => maxHealth > 0f ? Mathf.Clamp(health, 0f, maxHealth) : 0f;
        public float MaxHealth => Mathf.Max(0f, maxHealth);
        public float Energy => maxEnergy > 0f ? Mathf.Clamp(energy, 0f, maxEnergy) : 0f;
        public float MaxEnergy => Mathf.Max(0f, maxEnergy);
        public Sprite Icon => icon;
        public int EquippedSkillSlotCount => Mathf.Clamp(equippedSkillSlotCount, 2, 4);

        public event Action HudDataChanged;
        public event Action<int> SkillRequested;

        void Reset() {
            ResolveAnimationReferences();
            EnsurePrototypeSkills();
        }

        void Awake() {
            ResolveAnimationReferences();
            EnsureSkillList();
            EnsureLearnedSkillList();
        }

        void Update() {
            cooldownHudDirty |= TickCooldowns(Time.deltaTime);
            if (!cooldownHudDirty || Time.unscaledTime < nextCooldownHudRefreshAt) return;

            cooldownHudDirty = false;
            nextCooldownHudRefreshAt = Time.unscaledTime + cooldownHudRefreshInterval;
            HudDataChanged?.Invoke();
        }

        void OnValidate() {
            ResolveAnimationReferences();
            maxHealth = Mathf.Max(0f, maxHealth);
            maxEnergy = Mathf.Max(0f, maxEnergy);
            equippedSkillSlotCount = Mathf.Clamp(equippedSkillSlotCount, 2, 4);
            cooldownHudRefreshInterval = Mathf.Max(0.02f, cooldownHudRefreshInterval);
            health = maxHealth > 0f ? Mathf.Clamp(health, 0f, maxHealth) : 0f;
            energy = maxEnergy > 0f ? Mathf.Clamp(energy, 0f, maxEnergy) : 0f;

            EnsureSkillList(false);
            if (skills.Count > EquippedSkillSlotCount) {
                skills.RemoveRange(EquippedSkillSlotCount, skills.Count - EquippedSkillSlotCount);
            }
            for (int i = 0; i < skills.Count; i++) {
                skills[i] = NormalizeSkill(skills[i], i);
            }
            EnsureLearnedSkillList(false);
        }

        public IReadOnlyList<SkillHudData> GetSkills() {
            EnsureSkillList();
            EnsureCooldownStorage();
            runtimeSkills.Clear();

            for (int i = 0; i < skills.Count && i < EquippedSkillSlotCount; i++) {
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

        public IReadOnlyList<SkillHudData> GetLearnedSkills() {
            EnsureSkillList();
            EnsureLearnedSkillList();
            runtimeLearnedSkills.Clear();
            runtimeLearnedSkillIndices.Clear();

            for (int i = 0; i < learnedSkills.Count; i++) {
                SkillHudData skill = NormalizeSkill(learnedSkills[i], i % 4);
                if (!skill.unlocked || skill.RequiredPetLevel > Level) continue;
                runtimeLearnedSkills.Add(skill);
                runtimeLearnedSkillIndices.Add(i);
            }

            return runtimeLearnedSkills;
        }

        public bool TryEquipLearnedSkill(int learnedSkillIndex, int equippedSlotIndex) {
            EnsureSkillList();
            EnsureLearnedSkillList();
            GetLearnedSkills();
            if (learnedSkillIndex < 0 || learnedSkillIndex >= runtimeLearnedSkillIndices.Count) return false;
            if (equippedSlotIndex < 0 || equippedSlotIndex >= EquippedSkillSlotCount) return false;

            while (skills.Count <= equippedSlotIndex) skills.Add(default);

            int sourceIndex = runtimeLearnedSkillIndices[learnedSkillIndex];
            SkillHudData selected = NormalizeSkill(learnedSkills[sourceIndex], sourceIndex % 4);
            int previousSlot = FindEquippedSkill(selected);
            if (previousSlot == equippedSlotIndex) return true;

            if (previousSlot >= 0) {
                SkillHudData replaced = skills[equippedSlotIndex];
                skills[equippedSlotIndex] = selected;
                skills[previousSlot] = replaced;
            }
            else {
                skills[equippedSlotIndex] = selected;
            }

            cooldownRemaining.Clear();
            EnsureCooldownStorage();
            HudDataChanged?.Invoke();
            PersistentDataChanged?.Invoke();
            return true;
        }

        public bool TryGetSkillUnlockingAtLevel(int targetLevel, out SkillHudData skill) {
            EnsureLearnedSkillList(false);
            targetLevel = Mathf.Max(1, targetLevel);
            for (int i = 0; i < learnedSkills.Count; i++) {
                SkillHudData candidate = NormalizeSkill(learnedSkills[i], i % 4);
                if (candidate.RequiredPetLevel != targetLevel) continue;
                skill = candidate;
                return true;
            }

            skill = default;
            return false;
        }

        public int UnlockSkillsUpToLevel(int currentLevel, out SkillHudData firstUnlocked) {
            EnsureLearnedSkillList(false);
            firstUnlocked = default;
            currentLevel = Mathf.Max(1, currentLevel);
            int unlockedCount = 0;

            for (int i = 0; i < learnedSkills.Count; i++) {
                SkillHudData skill = NormalizeSkill(learnedSkills[i], i % 4);
                if (skill.unlocked || skill.RequiredPetLevel > currentLevel) continue;

                skill.unlocked = true;
                skill.usable = true;
                learnedSkills[i] = skill;
                if (unlockedCount == 0) firstUnlocked = skill;
                unlockedCount++;
            }

            if (unlockedCount > 0) { HudDataChanged?.Invoke(); PersistentDataChanged?.Invoke(); }
            return unlockedCount;
        }

        public void SetIdentity(string nextName, int nextLevel, Sprite nextIcon = null) {
            displayName = nextName ?? string.Empty;
            level = Mathf.Max(0, nextLevel);
            if (nextIcon != null) icon = nextIcon;
            HudDataChanged?.Invoke();
            PersistentDataChanged?.Invoke();
        }

        public void SetStatus(float nextHealth, float nextMaxHealth, float nextEnergy, float nextMaxEnergy) {
            maxHealth = Mathf.Max(0f, nextMaxHealth);
            maxEnergy = Mathf.Max(0f, nextMaxEnergy);
            health = maxHealth > 0f ? Mathf.Clamp(nextHealth, 0f, maxHealth) : 0f;
            energy = maxEnergy > 0f ? Mathf.Clamp(nextEnergy, 0f, maxEnergy) : 0f;
            HudDataChanged?.Invoke();
            PersistentDataChanged?.Invoke();
        }

        public void SetSkills(IEnumerable<SkillHudData> nextSkills) {
            restoredLoadout = true;
            skills.Clear();
            if (nextSkills != null) {
                foreach (SkillHudData skill in nextSkills) {
                    if (skills.Count >= EquippedSkillSlotCount) break;
                    skills.Add(NormalizeSkill(skill, skills.Count));
                }
            }

            cooldownRemaining.Clear();
            EnsureCooldownStorage();
            EnsureLearnedSkillList(false);
            HudDataChanged?.Invoke();
            PersistentDataChanged?.Invoke();
        }

        public PetRuntimeStatsSaveData CreateSaveData() {
            EnsureSkillList(false);
            EnsureLearnedSkillList(false);

            var saveData = new PetRuntimeStatsSaveData {
                captured = true,
                hasSkillLoadout = true,
                level = Level,
                health = Health,
                maxHealth = MaxHealth,
                energy = Energy,
                maxEnergy = MaxEnergy,
                equippedSkillSlotCount = EquippedSkillSlotCount
            };

            var capturedProgress = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < skills.Count && i < EquippedSkillSlotCount; i++) {
                string skillId = ResolveSkillId(skills[i]);
                saveData.equippedSkillIds.Add(skillId ?? string.Empty);
                AddSkillProgress(saveData.learnedSkillProgress, capturedProgress, skills[i], skillId);
            }

            for (int i = 0; i < learnedSkills.Count; i++) {
                string skillId = ResolveSkillId(learnedSkills[i]);
                AddSkillProgress(saveData.learnedSkillProgress, capturedProgress, learnedSkills[i], skillId);
            }

            return saveData;
        }

        public bool CanRestoreSaveData(PetRuntimeStatsSaveData data, out string error) {
            error = string.Empty;
            if (data == null || !data.captured) return true;
            EnsureSkillList(false);
            EnsureLearnedSkillList(false);
            if (data.hasSkillLoadout && data.equippedSkillIds != null) {
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string id in data.equippedSkillIds) {
                    if (string.IsNullOrWhiteSpace(id)) continue;
                    if (!TryFindSkill(id, out var skill) || !seen.Add(ResolveSkillId(skill))) {
                        error = "Missing or duplicate equipped skill: " + id; return false;
                    }
                }
            }
            if (data.learnedSkillProgress != null) foreach (var progress in data.learnedSkillProgress) {
                if (progress == null || !TryFindSkill(progress.skillId, out _)) {
                    error = "Missing learned skill: " + progress?.skillId; return false;
                }
            }
            return true;
        }

        public void RestoreFromSaveData(PetRuntimeStatsSaveData saveData) {
            if (saveData == null || !saveData.captured) return;
            if (!CanRestoreSaveData(saveData, out string error)) throw new InvalidOperationException(error);

            level = Mathf.Max(0, saveData.level);
            maxHealth = Mathf.Max(0f, saveData.maxHealth);
            health = maxHealth > 0f ? Mathf.Clamp(saveData.health, 0f, maxHealth) : 0f;
            maxEnergy = Mathf.Max(0f, saveData.maxEnergy);
            energy = maxEnergy > 0f ? Mathf.Clamp(saveData.energy, 0f, maxEnergy) : 0f;
            equippedSkillSlotCount = Mathf.Clamp(saveData.equippedSkillSlotCount, 2, 4);

            EnsureSkillList(false);
            EnsureLearnedSkillList(false);

            var progressById = new Dictionary<string, PetSkillProgressSaveData>(StringComparer.OrdinalIgnoreCase);
            if (saveData.learnedSkillProgress != null) {
                foreach (var progress in saveData.learnedSkillProgress) {
                    if (progress == null || string.IsNullOrWhiteSpace(progress.skillId)) continue;
                    progressById[progress.skillId] = progress;
                }
            }

            ApplySkillProgress(skills, progressById);
            ApplySkillProgress(learnedSkills, progressById);

            if (saveData.hasSkillLoadout && saveData.equippedSkillIds != null) {
                var restoredSkills = new List<SkillHudData>(EquippedSkillSlotCount);
                foreach (string skillId in saveData.equippedSkillIds) {
                    if (restoredSkills.Count >= EquippedSkillSlotCount) break;
                    if (string.IsNullOrWhiteSpace(skillId)) { restoredSkills.Add(default); continue; }
                    if (!TryFindSkill(skillId, out var skill)) throw new InvalidOperationException("Missing skill " + skillId);

                    restoredSkills.Add(ApplySkillProgress(skill, progressById));
                }

                restoredLoadout = true;
                skills.Clear();
                skills.AddRange(restoredSkills);
            }

            EnsureSkillList(false);
            EnsureLearnedSkillList(false);
            cooldownRemaining.Clear();
            EnsureCooldownStorage();
            HudDataChanged?.Invoke();
        }

        public void RequestSkill(int skillIndex) {
            EnsureCooldownStorage();
            if (skillIndex < 0 || skillIndex >= skills.Count) return;
            if (skillIndex < cooldownRemaining.Count && cooldownRemaining[skillIndex] > 0.001f) return;

            SkillHudData skill = skills[skillIndex];
            if (!skill.unlocked || !skill.usable) return;
            SkillRequested?.Invoke(skillIndex);
            PlaySkillAnimation(skill);

            if (skill.cooldownSeconds > 0f && skillIndex < cooldownRemaining.Count) {
                cooldownRemaining[skillIndex] = skill.cooldownSeconds;
                HudDataChanged?.Invoke();
            }
        }

        void EnsureCooldownStorage() {
            while (cooldownRemaining.Count < skills.Count && cooldownRemaining.Count < EquippedSkillSlotCount) {
                cooldownRemaining.Add(0f);
            }

            if (cooldownRemaining.Count > skills.Count) {
                cooldownRemaining.RemoveRange(skills.Count, cooldownRemaining.Count - skills.Count);
            }
        }

        void EnsureSkillList(bool sendChangeEvent = true) {
            bool changed = false;
            int slotCount = EquippedSkillSlotCount;
            for (int i = 0; i < skills.Count && i < slotCount; i++) {
                skills[i] = NormalizeSkill(skills[i], i);
            }

            while (usePrototypeSkillsWhenEmpty && !restoredLoadout && skills.Count < slotCount) {
                skills.Add(DefaultSkill(skills.Count));
                changed = true;
            }

            if (skills.Count > slotCount) {
                skills.RemoveRange(slotCount, skills.Count - slotCount);
                changed = true;
            }

            EnsureCooldownStorage();
            if (changed && sendChangeEvent) HudDataChanged?.Invoke();
        }

        void EnsureLearnedSkillList(bool sendChangeEvent = true) {
            bool changed = false;

            for (int i = 0; i < learnedSkills.Count; i++) {
                learnedSkills[i] = NormalizeSkill(learnedSkills[i], i % 4);
            }

            for (int i = 0; i < skills.Count; i++) {
                SkillHudData equipped = NormalizeSkill(skills[i], i);
                if (!HasUsefulSkillData(equipped)) continue;
                if (FindLearnedSkill(equipped) >= 0) continue;
                learnedSkills.Add(equipped);
                changed = true;
            }

            if (changed && sendChangeEvent) HudDataChanged?.Invoke();
        }

        int FindEquippedSkill(SkillHudData skill) {
            for (int i = 0; i < skills.Count; i++) {
                if (SameSkill(skills[i], skill)) return i;
            }

            return -1;
        }

        int FindLearnedSkill(SkillHudData skill) {
            for (int i = 0; i < learnedSkills.Count; i++) {
                if (SameSkill(learnedSkills[i], skill)) return i;
            }

            return -1;
        }

        static bool SameSkill(SkillHudData left, SkillHudData right) {
            if (!string.IsNullOrWhiteSpace(left.skillId) && !string.IsNullOrWhiteSpace(right.skillId))
                return string.Equals(left.skillId, right.skillId, StringComparison.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(left.displayName) || !string.IsNullOrWhiteSpace(right.displayName)) {
                return string.Equals(left.displayName?.Trim(), right.displayName?.Trim(), StringComparison.OrdinalIgnoreCase);
            }

            string leftState = left.animatorStates != null && left.animatorStates.Length > 0 ? left.animatorStates[0] : string.Empty;
            string rightState = right.animatorStates != null && right.animatorStates.Length > 0 ? right.animatorStates[0] : string.Empty;
            return !string.IsNullOrWhiteSpace(leftState)
                && string.Equals(leftState, rightState, StringComparison.OrdinalIgnoreCase);
        }

        bool TryFindSkill(string skillId, out SkillHudData skill) {
            for (int i = 0; i < learnedSkills.Count; i++) {
                if (!MatchesSkillId(learnedSkills[i], skillId)) continue;
                skill = learnedSkills[i];
                return true;
            }

            for (int i = 0; i < skills.Count; i++) {
                if (!MatchesSkillId(skills[i], skillId)) continue;
                skill = skills[i];
                return true;
            }

            skill = default;
            return false;
        }

        static void AddSkillProgress(
            ICollection<PetSkillProgressSaveData> destination,
            ISet<string> capturedIds,
            SkillHudData skill,
            string skillId) {
            if (string.IsNullOrEmpty(skillId) || !capturedIds.Add(skillId)) return;

            destination.Add(new PetSkillProgressSaveData {
                skillId = skillId,
                level = Mathf.Max(1, skill.skillLevel),
                unlocked = skill.unlocked,
                usable = skill.usable
            });
        }

        static void ApplySkillProgress(
            IList<SkillHudData> destination,
            IReadOnlyDictionary<string, PetSkillProgressSaveData> progressById) {
            for (int i = 0; i < destination.Count; i++) {
                destination[i] = ApplySkillProgress(destination[i], progressById);
            }
        }

        static SkillHudData ApplySkillProgress(
            SkillHudData skill,
            IReadOnlyDictionary<string, PetSkillProgressSaveData> progressById) {
            string skillId = ResolveSkillId(skill);
            if (string.IsNullOrEmpty(skillId)) return skill;
            if (!progressById.TryGetValue(skillId, out var progress)) {
                foreach (var candidate in progressById) if (MatchesSkillId(skill, candidate.Key)) { progress = candidate.Value; break; }
                if (progress == null) return skill;
            }

            skill.skillLevel = Mathf.Max(1, progress.level);
            skill.unlocked = progress.unlocked;
            skill.usable = progress.usable;
            return skill;
        }

        static string ResolveSkillId(SkillHudData skill) {
            if (!string.IsNullOrWhiteSpace(skill.skillId)) return skill.skillId.Trim();
            if (!string.IsNullOrWhiteSpace(skill.displayName)) {
                return skill.displayName.Trim().ToLowerInvariant();
            }

            if (skill.animatorStates == null || skill.animatorStates.Length == 0) return string.Empty;
            return string.IsNullOrWhiteSpace(skill.animatorStates[0])
                ? string.Empty
                : skill.animatorStates[0].Trim().ToLowerInvariant();
        }

        static bool MatchesSkillId(SkillHudData skill, string id) {
            if (string.IsNullOrWhiteSpace(id)) return false;
            if (string.Equals(ResolveSkillId(skill), id, StringComparison.OrdinalIgnoreCase)) return true;
            if (skill.legacySkillIds != null) foreach (string alias in skill.legacySkillIds)
                if (string.Equals(alias, id, StringComparison.OrdinalIgnoreCase)) return true;
            return string.Equals(skill.displayName?.Trim(), id, StringComparison.OrdinalIgnoreCase);
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

        void ResolveAnimationReferences() {
            if (!autoFindAnimationReferences) return;
            if (petController == null) petController = GetComponent<PetController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
        }

        void PlaySkillAnimation(SkillHudData skill) {
            ResolveAnimationReferences();
            if (skill.animatorStates == null || skill.animatorStates.Length == 0) return;

            float duration = skill.animationDuration > 0f ? skill.animationDuration : 0.85f;
            float fade = skill.animationFade > 0f ? skill.animationFade : 0.08f;
            float windup = Mathf.Max(0f, skill.windupSeconds);
            float recovery = Mathf.Max(0f, skill.recoverySeconds);
            if (petController != null && petController.PlaySkillAnimation(skill.animatorStates, duration, fade, stopMovementDuringSkill, faceTargetDuringSkill, windup, recovery)) {
                return;
            }

            if (animator == null) return;
            foreach (string stateName in skill.animatorStates) {
                if (string.IsNullOrWhiteSpace(stateName)) continue;

                int hash;
                if (!TryGetAnimatorStateHash(stateName, out hash)) continue;

                animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
                return;
            }
        }

        bool TryGetAnimatorStateHash(string stateName, out int hash) {
            string fullName = "Base Layer." + stateName;
            hash = Animator.StringToHash(fullName);
            if (animator != null && animator.HasState(0, hash)) return true;

            hash = Animator.StringToHash(stateName);
            return animator != null && animator.HasState(0, hash);
        }

        SkillHudData NormalizeSkill(SkillHudData skill, int index) {
            SkillHudData fallback = DefaultSkill(index);
            if (!HasUsefulSkillData(skill)) return restoredLoadout || !usePrototypeSkillsWhenEmpty ? default : fallback;
            if (string.IsNullOrWhiteSpace(skill.skillId)) {
                skill.skillId = ResolveSkillId(skill);
                skill.legacySkillIds = new[] { skill.skillId };
            }

            if (string.IsNullOrWhiteSpace(skill.displayName)) skill.displayName = fallback.displayName;
            if (string.IsNullOrWhiteSpace(skill.description)) skill.description = fallback.description;
            if (skill.element == PetElement.Unknown
                && string.Equals(skill.displayName?.Trim(), fallback.displayName, StringComparison.OrdinalIgnoreCase)) {
                skill.element = fallback.element;
            }
            if (skill.animatorStates == null || skill.animatorStates.Length == 0) {
                skill.animatorStates = fallback.animatorStates;
            }

            if (skill.animationDuration <= 0f) skill.animationDuration = fallback.animationDuration;
            if (skill.animationFade <= 0f) skill.animationFade = fallback.animationFade;
            if (skill.windupSeconds <= 0f) skill.windupSeconds = fallback.windupSeconds;
            if (skill.recoverySeconds <= 0f) skill.recoverySeconds = fallback.recoverySeconds;

            skill.skillLevel = Mathf.Max(1, skill.skillLevel);
            skill.requiredPetLevel = Mathf.Max(1, skill.requiredPetLevel);
            skill.cooldownPercent = Mathf.Clamp01(skill.cooldownPercent);
            skill.cooldownSeconds = Mathf.Max(0f, skill.cooldownSeconds);
            skill.cooldownRemainingSeconds = 0f;
            skill.animationDuration = Mathf.Max(0f, skill.animationDuration);
            skill.animationFade = Mathf.Max(0f, skill.animationFade);
            skill.windupSeconds = Mathf.Max(0f, skill.windupSeconds);
            skill.recoverySeconds = Mathf.Max(0f, skill.recoverySeconds);

            return skill;
        }

        [ContextMenu("Use Dragon Prototype Skills")]
        void EnsurePrototypeSkills() {
            AddPrototypeSkills();
            cooldownRemaining.Clear();
            EnsureCooldownStorage();
            HudDataChanged?.Invoke();
        }

        void AddPrototypeSkills() {
            skills.Clear();
            for (int i = 0; i < EquippedSkillSlotCount; i++) {
                skills.Add(DefaultSkill(i));
            }
        }

        static SkillHudData DefaultSkill(int index) {
            switch (index) {
                case 0:
                    return CreateSkill("Bite", PetElement.Nature, "Basic close-range bite attack.", 1.2f, 0.8f, 0.12f, 0.3f, "Bite Attack", "Bite Attack Low");
                case 1:
                    return CreateSkill("Blast", PetElement.Light, "A short roar-like magic blast.", 3.8f, 1.0f, 0f, 0f, "Blast Attack", "Cast Spell");
                case 2:
                    return CreateSkill("Projectile", PetElement.Fire, "Fires a ranged projectile.", 3.0f, 0.95f, 0f, 0f, "Projectile Attack", "Projectile Attack Low");
                case 3:
                    return CreateSkill("Wing Strike", PetElement.Wind, "A wing attack that knocks the air forward.", 4.2f, 1.0f, 0f, 0f, "Wing Attack");
                default:
                    return default;
            }
        }

        static SkillHudData CreateSkill(string name, PetElement element, string description, float cooldown, float duration, float windup, float recovery, params string[] animatorStates) {
            return new SkillHudData {
                skillId = name.ToLowerInvariant(),
                legacySkillIds = new[] { name.ToLowerInvariant() },
                unlocked = true,
                usable = true,
                displayName = name,
                element = element,
                description = description,
                skillLevel = 1,
                requiredPetLevel = 1,
                animatorStates = animatorStates,
                animationDuration = duration,
                animationFade = 0.08f,
                windupSeconds = windup,
                recoverySeconds = recovery,
                cooldownSeconds = cooldown
            };
        }

        static bool HasUsefulSkillData(SkillHudData skill) {
            if (!string.IsNullOrWhiteSpace(skill.skillId)) return true;
            if (!string.IsNullOrWhiteSpace(skill.displayName)) return true;
            if (!string.IsNullOrWhiteSpace(skill.description)) return true;
            if (skill.icon != null) return true;
            if (skill.skillLevel > 1) return true;
            if (skill.cooldownSeconds > 0f) return true;
            if (skill.animationDuration > 0f) return true;
            if (skill.windupSeconds > 0f) return true;
            if (skill.recoverySeconds > 0f) return true;
            if (skill.animatorStates != null && skill.animatorStates.Length > 0) return true;
            return skill.unlocked || skill.usable;
        }
    }
}
