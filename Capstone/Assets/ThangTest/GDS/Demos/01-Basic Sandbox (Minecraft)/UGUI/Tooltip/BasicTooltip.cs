using GDS.Core;
using GDS.Core.UGUI;
using GDS.Demos.Basic.UGUI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Demos.Basic {

    public class BasicTooltip : BaseTooltipView {
        [SerializeField] TextMeshProUGUI nameText;
        [SerializeField] TextMeshProUGUI affixText;
        [SerializeField] RectTransform affixTransform;

        [SerializeField] Image background;
        [SerializeField] Image border;
        [SerializeField] Image separator;

        [SerializeField, Required] Basic_StylingSO style;

        Item lastContextItem;

        override public void Render(IItemContext context) {
            if (context == null) return;
            if (context.Item.Equals(lastContextItem)) return;
            lastContextItem = context.Item.Clone();

            if (context.Bag is CraftingBench || context.Item is not Basic_Item item) {
                nameText.text = context.Item.Name;
                affixText.text = "";
                Resize();
                return;
            }

            nameText.text = NameText(item);
            affixText.text = AffixText(item);
            Resize();

            if (style == null) return;
            background.color = style.BackgroundColor(item);
            border.color = style.BorderColor(item);
            separator.color = style.BorderColor(item);
        }

        // Manual resize because Unty layout is a mess
        void Resize() {
            LayoutRebuilder.ForceRebuildLayoutImmediate(affixTransform);
            float width = LayoutUtility.GetPreferredWidth(affixTransform);
            float height = LayoutUtility.GetPreferredHeight(affixTransform);
            var preferredSize = new Vector2(width, height);
            affixTransform.sizeDelta = preferredSize;
            var padding = height == 0 ? 0 : 40;
            rectTransform.sizeDelta = new Vector2(rectTransform.rect.width, 50 + padding + height);
            separator.gameObject.SetActive(padding != 0);
        }

        string AffixText(Item item) => item switch {
            Basic_Weapon i => WeaponText(i),
            Basic_Armor i => ArmorText(i),
            _ => ""
        };

        string NameText(Basic_Item i) => i.Rarity() == Rarity.NoRarity ? i.ItemNameWithQuant() : $"{i.Rarity} {i.ItemNameWithQuant()}";
        string WeaponText(Basic_Weapon i) => $"Attack Damage: {i.AttackDamage}\nAttackSpeed: {i.AttackSpeed}/s\nDPS: {i.Dps}/s";
        string ArmorText(Basic_Armor i) => $"Defense: {i.Defense}";

    }
}
