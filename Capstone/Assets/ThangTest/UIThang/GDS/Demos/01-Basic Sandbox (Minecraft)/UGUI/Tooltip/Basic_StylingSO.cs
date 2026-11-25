using UnityEngine;

namespace GDS.Demos.Basic.UGUI {

    [CreateAssetMenu(menuName = "SO/Demos/Basic/Basic_StylingSO")]
    public class Basic_StylingSO : ScriptableObject {
        public Color CommonBg;
        public Color CommonBorder;
        public Color MagicBg;
        public Color MagicBorder;
        public Color RareBg;
        public Color RareBorder;
        public Color UniqueBg;
        public Color UniqueBorder;
        public Color NameColor;
        public Color AffixColor;

        public Color BackgroundColor(Basic_Item i) => i.Rarity switch {
            Rarity.Unique => UniqueBg,
            Rarity.Rare => RareBg,
            Rarity.Magic => MagicBg,
            _ => CommonBg
        };

        public Color BorderColor(Basic_Item i) => i.Rarity switch {
            Rarity.Unique => UniqueBorder,
            Rarity.Rare => RareBorder,
            Rarity.Magic => MagicBorder,
            _ => CommonBorder,
        };
    }

}