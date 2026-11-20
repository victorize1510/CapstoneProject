using Capstone.Game.CodexSystem;
using Capstone.Game.HudSystem;

namespace Capstone.Game.Elements
{
    // Hai enum co so serialize khac nhau; khong cast so hoac parse ten chuoi.
    public static class CodexElementAdapter
    {
        public static CodexType ToCodex(PetElement value)
        {
            switch (value)
            {
                case PetElement.Fire:
                    return CodexType.Fire;
                case PetElement.Water:
                    return CodexType.Water;
                case PetElement.Nature:
                    return CodexType.Nature;
                case PetElement.Electric:
                    return CodexType.Electric;
                case PetElement.Fighting:
                    return CodexType.Fighting;
                case PetElement.Flying:
                    return CodexType.Flying;
                case PetElement.Normal:
                    return CodexType.Normal;
                case PetElement.Dark:
                    return CodexType.Dark;
                case PetElement.Dragon:
                    return CodexType.Dragon;
                case PetElement.Wind:
                    return CodexType.Wind;
                case PetElement.Earth:
                    return CodexType.Earth;
                case PetElement.Ice:
                    return CodexType.Ice;
                case PetElement.Light:
                    return CodexType.Light;
                default:
                    return CodexType.Unknown;
            }
        }

        public static PetElement ToPet(CodexType value)
        {
            switch (value)
            {
                case CodexType.Fire:
                    return PetElement.Fire;
                case CodexType.Water:
                    return PetElement.Water;
                case CodexType.Nature:
                    return PetElement.Nature;
                case CodexType.Electric:
                    return PetElement.Electric;
                case CodexType.Fighting:
                    return PetElement.Fighting;
                case CodexType.Flying:
                    return PetElement.Flying;
                case CodexType.Normal:
                    return PetElement.Normal;
                case CodexType.Dark:
                    return PetElement.Dark;
                case CodexType.Dragon:
                    return PetElement.Dragon;
                case CodexType.Wind:
                    return PetElement.Wind;
                case CodexType.Earth:
                    return PetElement.Earth;
                case CodexType.Ice:
                    return PetElement.Ice;
                case CodexType.Light:
                    return PetElement.Light;
                default:
                    return PetElement.Unknown;
            }
        }

        public static int DisplayOrder(CodexType value)
        {
            int index = ElementCatalog.IndexOf(ToPet(value));
            return index >= 0 ? index : ElementCatalog.Count;
        }

        public static CodexType? [] CreateFilterTypes()
        {
            var values = new CodexType? [ElementCatalog.Count + 1];
            for (int i = 0; i < ElementCatalog.Count; i++)
            {
                values[i + 1] = ToCodex(ElementCatalog.Entries[i].Element);
            }

            return values;
        }
    }
}
