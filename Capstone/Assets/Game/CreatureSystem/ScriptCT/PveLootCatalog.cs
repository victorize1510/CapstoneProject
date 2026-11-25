using Capstone.Game.Inventory;
using Capstone.Game.HudSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Capstone.Game.CreatureSystem {
    [CreateAssetMenu(menuName="Capstone/PvE/Loot catalog")]
    public sealed class PveLootCatalog:ScriptableObject {
        public PveLootCatalog sharedCatalog;
        public PveLootCatalog EffectiveCatalog => sharedCatalog != null ? sharedCatalog : this;
        [Serializable] public sealed class Entry {
            public MonsterItemDefinition item;
            [Min(1)] public int weight=1;
            [Min(1)] public int quantity=1;
        }
        public List<Entry> entries=new List<Entry>();
        public MonsterItemDefinition potion,healingApple,manaPotion;
        public Sprite goldIcon;
        public Material glowMaterial;
        [Range(0,1)] public float itemChance=.35f;
        [Min(0)] public int minimumGold=2,maximumGold=5;
        public MonsterItemDefinition smallHpPotion, smallMpPotion;
        [Range(0,1)] public float smallHpChance, smallMpChance;
        [Range(0,1)] public float evolutionStoneChance;

        public MonsterItemDefinition SelectEvolutionStone(PetElement primaryElement, float roll) {
            if (!(roll >= 0f && roll < evolutionStoneChance) || !Elements.ElementCatalog.IsSupported(primaryElement)) return null;
            foreach (var definition in InventoryDefinitionCatalog.Definitions)
                if (definition.EvolutionStoneElement == primaryElement) return definition;
            return null;
        }

        public void SelectSmallPotions(float hpRoll, float mpRoll, out MonsterItemDefinition hp, out MonsterItemDefinition mp) {
            hp = hpRoll >= 0f && hpRoll < smallHpChance ? smallHpPotion : null;
            mp = mpRoll >= 0f && mpRoll < smallMpChance ? smallMpPotion : null;
        }
        public static MonsterItemDefinition SelectFamilyMaterial(string familyId, string formId, float roll) {
            formId = PetPrefabCatalog.CanonicalFormId(formId?.Trim());
            float chance = formId switch { "01" => .45f, "02" => .35f, "03" => .25f, _ => 0f };
            if (!(roll >= 0f && roll < chance) || string.IsNullOrWhiteSpace(familyId)) return null;
            string slug = PetPrefabCatalog.CanonicalDefinitionId(familyId.Trim()).Replace(' ', '-').ToLowerInvariant();
            return InventoryDefinitionCatalog.Find($"loot-{slug}-{formId}");
        }
        public bool TrySelect(float unit,out MonsterItemDefinition item,out int quantity) {
            item=null;quantity=0;
            if(float.IsNaN(unit)||float.IsInfinity(unit)||unit<0||unit>=1)return false;
            if(entries!=null&&entries.Count>0){
                long total=0;foreach(var entry in entries)if(entry!=null&&entry.item!=null&&entry.weight>0&&entry.quantity>0)total+=entry.weight;
                if(total==0)return false;
                double choice=unit*total;
                foreach(var entry in entries){if(entry==null||entry.item==null||entry.weight<=0||entry.quantity<=0)continue;choice-=entry.weight;if(choice<0){item=entry.item;quantity=entry.quantity;return true;}}
                return false;
            }
            // Existing authored catalogs retain their old distribution until migrated.
            item=unit<.4f?potion:unit<.8f?healingApple:manaPotion;quantity=item!=null?1:0;return item!=null;
        }
    }
}
