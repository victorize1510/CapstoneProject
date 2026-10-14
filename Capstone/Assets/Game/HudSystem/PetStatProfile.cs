using UnityEngine;

namespace Capstone.Game.HudSystem {
    [CreateAssetMenu(menuName = "Game/Pets/Stat Profile", fileName = "PetStatProfile")]
    public sealed class PetStatProfile : ScriptableObject {
        [SerializeField] string definitionId = string.Empty;
        [SerializeField] string formId = "base";
        [Header("Species/form Base Stats (BST is their sum)")]
        [SerializeField, Range(1, 255)] int hp = 70;
        [SerializeField, Range(1, 255)] int attack = 70;
        [SerializeField, Range(1, 255)] int defense = 70;
        [SerializeField, Range(1, 255)] int magicAttack = 70;
        [SerializeField, Range(1, 255)] int magicDefense = 70;
        [SerializeField, Range(1, 255)] int speed = 70;
        [Header("Base critical bonuses (percent)")]
        [SerializeField, Range(0f, 100f)] float criticalRate = 6.25f;
        [SerializeField, Range(0f, 100f)] float criticalDamage = 50f;

        public string DefinitionId => definitionId?.Trim() ?? string.Empty;
        public string FormId => string.IsNullOrWhiteSpace(formId) ? "base" : formId.Trim();
        public PetStatValues BaseStats => new PetStatValues(hp, attack, defense, magicAttack, magicDefense, speed).Clamped(1, 255);
        public int BaseStatTotal => BaseStats.Total;
        public float CriticalRate => Mathf.Clamp(criticalRate, 0f, 100f);
        public float CriticalDamage => Mathf.Clamp(criticalDamage, 0f, 100f);
    }
}
