using Capstone.Game.Inventory;
using UnityEngine;
namespace Capstone.Game.CreatureSystem {
    [CreateAssetMenu(menuName="Capstone/PvE/Loot catalog")]
    public sealed class PveLootCatalog:ScriptableObject {
        public MonsterItemDefinition potion,healingApple,manaPotion;
        public Sprite goldIcon;
        public Material glowMaterial;
        [Range(0,1)] public float itemChance=.35f;
        [Min(0)] public int minimumGold=2,maximumGold=5;
    }
}
