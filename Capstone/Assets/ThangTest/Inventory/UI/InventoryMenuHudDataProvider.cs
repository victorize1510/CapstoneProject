using System;
using UnityEngine;

namespace Capstone.Game.Inventory {
    public interface IPlayerStatsHudProvider {
        bool HasPlayerStats { get; }
        int Level { get; }
        int CurrentHp { get; }
        int MaxHp { get; }
        int CurrentExp { get; }
        int RequiredExp { get; }
        event Action HudDataChanged;
    }

    public interface ICurrencyHudProvider {
        bool HasCurrency { get; }
        int Gold { get; }
        int Gems { get; }
        event Action HudDataChanged;
    }

    [DisallowMultipleComponent]
    public sealed class InventoryMenuHudDataProvider : MonoBehaviour, IPlayerStatsHudProvider, ICurrencyHudProvider {
        [Header("Player Runtime Data")]
        [SerializeField] bool hasPlayerStats;
        [SerializeField, Min(0)] int level;
        [SerializeField, Min(0)] int currentHp;
        [SerializeField, Min(0)] int maxHp;
        [SerializeField, Min(0)] int currentExp;
        [SerializeField, Min(0)] int requiredExp;

        [Header("Currency Runtime Data")]
        [SerializeField] bool hasCurrency;
        [SerializeField, Min(0)] int gold;
        [SerializeField, Min(0)] int gems;

        public bool HasPlayerStats => hasPlayerStats;
        public bool HasCurrency => hasCurrency;
        public int Level => Mathf.Max(0, level);
        public int CurrentHp => Mathf.Clamp(currentHp, 0, MaxHp);
        public int MaxHp => Mathf.Max(0, maxHp);
        public int CurrentExp => Mathf.Clamp(currentExp, 0, RequiredExp);
        public int RequiredExp => Mathf.Max(0, requiredExp);
        public int Gold => Mathf.Max(0, gold);
        public int Gems => Mathf.Max(0, gems);

        public event Action HudDataChanged;

        void OnValidate() {
            maxHp = Mathf.Max(0, maxHp);
            currentHp = maxHp > 0 ? Mathf.Clamp(currentHp, 0, maxHp) : 0;
            requiredExp = Mathf.Max(0, requiredExp);
            currentExp = requiredExp > 0 ? Mathf.Clamp(currentExp, 0, requiredExp) : 0;
        }

        public void SetPlayerStats(int nextLevel, int nextCurrentHp, int nextMaxHp, int nextCurrentExp, int nextRequiredExp) {
            hasPlayerStats = true;
            level = Mathf.Max(0, nextLevel);
            maxHp = Mathf.Max(0, nextMaxHp);
            currentHp = maxHp > 0 ? Mathf.Clamp(nextCurrentHp, 0, maxHp) : 0;
            requiredExp = Mathf.Max(0, nextRequiredExp);
            currentExp = requiredExp > 0 ? Mathf.Clamp(nextCurrentExp, 0, requiredExp) : 0;
            HudDataChanged?.Invoke();
        }

        public void SetCurrency(int nextGold, int nextGems) {
            hasCurrency = true;
            gold = Mathf.Max(0, nextGold);
            gems = Mathf.Max(0, nextGems);
            HudDataChanged?.Invoke();
        }

        public void ClearPlayerStats() {
            hasPlayerStats = false;
            level = 0;
            currentHp = 0;
            maxHp = 0;
            currentExp = 0;
            requiredExp = 0;
            HudDataChanged?.Invoke();
        }

        public void ClearCurrency() {
            hasCurrency = false;
            gold = 0;
            gems = 0;
            HudDataChanged?.Invoke();
        }
    }
}
