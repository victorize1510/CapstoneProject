using System;
using Capstone.Game.HudSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    [DisallowMultipleComponent, RequireComponent(typeof(DummyEnemy))]
    public sealed class CreatureQaLevelTarget : MonoBehaviour
    {
        [SerializeField, Min(2)] int targetLevel = 18;
        public int TargetLevel => targetLevel;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool used;

        void OnEnable()
        {
            if (Application.isPlaying) CombatDamageEvents.Applied += OnDamageApplied;
        }

        void OnDisable() => CombatDamageEvents.Applied -= OnDamageApplied;

        void OnDamageApplied(CombatDamageResult result)
        {
            if (used || result.Target != gameObject) return;

            var pet = ResolveOwnedPet(result.Hit.Source);
            if (pet == null)
            {
                Debug.LogWarning($"{name}: chưa xác định được thú đang triệu hồi của người chơi. Cube vẫn dùng được.", this);
                return;
            }

            var stats = pet.GetComponentInChildren<PetHudRuntimeStats>(true);
            var metadata = pet.GetComponentInChildren<PetCollectionMetadata>(true);
            if (stats == null || metadata == null || metadata.MaxLevel < targetLevel)
            {
                Debug.LogWarning($"{name}: dữ liệu thú hoặc giới hạn cấp không hỗ trợ Lv{targetLevel}.", this);
                return;
            }
            if (stats.Level >= targetLevel)
            {
                Debug.Log($"{name}: {stats.DisplayName} đã ở Lv{stats.Level}. Cube vẫn sẵn sàng cho thú cấp thấp hơn.", this);
                return;
            }

            var service = FindFirstObjectByType<PetLevelUpService>();
            if (service == null || service.gameObject.scene != gameObject.scene || !HasSafeSaveContext())
            {
                Debug.LogWarning($"{name}: cần Pet Level Up Service trong cùng scene và slot QA đã nạp nếu có lưu game.", this);
                return;
            }

            long needed = -metadata.Experience;
            for (int level = Mathf.Max(1, stats.Level); level < targetLevel; level++)
                needed += service.CalculateExperienceRequirement(level);
            if (needed <= 0 || needed > int.MaxValue)
            {
                Debug.LogWarning($"{name}: EXP hiện tại và đường cong cấp không cho phép cấp chính xác Lv{targetLevel}.", this);
                return;
            }

            used = true;
            if (!service.GrantBattleExperience(pet, (int)needed, out var gain))
            {
                used = false;
                Debug.LogWarning($"{name}: Pet Level Up Service từ chối cấp EXP.", this);
                return;
            }
            Debug.Log($"{name}: {stats.DisplayName} Lv{gain.PreviousLevel} → Lv{gain.CurrentLevel} (+{gain.ExperienceGranted} EXP).", this);
            gameObject.SetActive(false);
        }

        bool HasSafeSaveContext()
        {
            var saves = FindObjectsByType<PlayerSaveController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            PlayerSaveController activeSave = null;
            foreach (var save in saves)
            {
                if (!save.isActiveAndEnabled) continue;
                if (activeSave != null || save.gameObject.scene != gameObject.scene || save.SlotId != DemoSceneOneSaveIsolation.CreatureQaSlot
                    || !save.HasCompletedStartup || save.IsApplyingSave || save.IsSaveBlocked
                    || (save.HasSave && !save.IsCurrentSlotLoaded)) return false;
                activeSave = save;
            }
            return true;
        }

        static PetController ResolveOwnedPet(GameObject source)
        {
            if (source == null) return null;
            var pet = source.GetComponentInParent<PetController>();
            var commands = pet == null
                ? source.GetComponentInParent<PetCommandInput>()
                : pet.owner != null ? pet.owner.GetComponent<PetCommandInput>() : null;
            if (commands == null) return null;
            if (pet == null) pet = commands.activePet;
            return pet != null && pet.owner == commands.transform && !pet.IsWildEncounter && pet.CanReceiveCommands
                && commands.petSlots != null && Array.IndexOf(commands.petSlots, pet) >= 0 ? pet : null;
        }
#endif
    }
}
