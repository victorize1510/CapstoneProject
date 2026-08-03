using System;
using UnityEngine;

namespace Capstone.Game.Inventory {
    public interface IPlayerStatsHudProvider {
        int Level { get; }
        int CurrentHp { get; }
        int MaxHp { get; }
        int CurrentExp { get; }
        int RequiredExp { get; }
        event Action HudDataChanged;
    }

    public interface ICurrencyHudProvider {
        int Gold { get; }
        int Gems { get; }
        event Action HudDataChanged;
    }

    [DisallowMultipleComponent]
    public sealed class InventoryMenuHudDataProvider : MonoBehaviour, IPlayerStatsHudProvider, ICurrencyHudProvider {
        [Header("Player Placeholder")]
        [SerializeField, Min(1)] int level = 18;
        [SerializeField, Min(0)] int currentHp = 860;
        [SerializeField, Min(1)] int maxHp = 860;
        [SerializeField, Min(0)] int currentExp = 1850;
        [SerializeField, Min(1)] int requiredExp = 2780;

        [Header("Currency Placeholder")]
        [SerializeField, Min(0)] int gold = 12450;
        [SerializeField, Min(0)] int gems = 320;

        public int Level => Mathf.Max(1, level);
        public int CurrentHp => Mathf.Clamp(currentHp, 0, MaxHp);
        public int MaxHp => Mathf.Max(1, maxHp);
        public int CurrentExp => Mathf.Clamp(currentExp, 0, RequiredExp);
        public int RequiredExp => Mathf.Max(1, requiredExp);
        public int Gold => Mathf.Max(0, gold);
        public int Gems => Mathf.Max(0, gems);

        public event Action HudDataChanged;

        void OnValidate() {
            maxHp = Mathf.Max(1, maxHp);
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
            requiredExp = Mathf.Max(1, requiredExp);
            currentExp = Mathf.Clamp(currentExp, 0, requiredExp);
        }

        public void SetPlayerStats(int nextLevel, int nextCurrentHp, int nextMaxHp, int nextCurrentExp, int nextRequiredExp) {
            level = Mathf.Max(1, nextLevel);
            maxHp = Mathf.Max(1, nextMaxHp);
            currentHp = Mathf.Clamp(nextCurrentHp, 0, maxHp);
            requiredExp = Mathf.Max(1, nextRequiredExp);
            currentExp = Mathf.Clamp(nextCurrentExp, 0, requiredExp);
            HudDataChanged?.Invoke();
        }

        public void SetCurrency(int nextGold, int nextGems) {
            gold = Mathf.Max(0, nextGold);
            gems = Mathf.Max(0, nextGems);
            HudDataChanged?.Invoke();
        }
    }
}
