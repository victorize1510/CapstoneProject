using System;
using System.Collections.Generic;
using Capstone.Game.HudSystem;

namespace Capstone.Game.Elements
{
    public sealed class ElementDescriptor
    {
        public readonly PetElement Element;
        public readonly string Name, IconKey, ColorHex;
        public readonly bool LightText;
        internal ElementDescriptor(PetElement element, string name, string iconKey, string colorHex, bool lightText = false)
        {
            Element = element;
            Name = name;
            IconKey = iconKey;
            ColorHex = colorHex;
            LightText = lightText;
        }
    }

    /// <summary>Nguon duy nhat cho thu tu hien thi, ten, icon va danh muc chin he.</summary>
    public static class ElementCatalog
    {
        public const int Count = 9;
        public const string UnclassifiedName = "Chưa phân loại";
        static readonly ElementDescriptor[] entries = {new ElementDescriptor(PetElement.Fire, "Lửa", "fire", "#EE9B58"), new ElementDescriptor(PetElement.Water, "Nước", "water", "#73B6E2"), new ElementDescriptor(PetElement.Nature, "Cỏ", "grass", "#9DCD75"), new ElementDescriptor(PetElement.Electric, "Điện", "electric", "#F0CD63"), new ElementDescriptor(PetElement.Fighting, "Giác đấu", "fighting", "#DB8174"), new ElementDescriptor(PetElement.Flying, "Bay", "flying", "#A0D5E8"), new ElementDescriptor(PetElement.Normal, "Thường", "normal", "#C5BCA9"), new ElementDescriptor(PetElement.Dark, "Tối", "dark", "#65517D", true), new ElementDescriptor(PetElement.Dragon, "Rồng", "dragon", "#655AB2", true)};
        public static IReadOnlyList<ElementDescriptor> Entries { get; } = Array.AsReadOnly(entries);
        public static int IndexOf(PetElement element)
        {
            switch (element)
            {
                case PetElement.Fire:
                    return 0;
                case PetElement.Water:
                    return 1;
                case PetElement.Nature:
                    return 2;
                case PetElement.Electric:
                    return 3;
                case PetElement.Fighting:
                    return 4;
                case PetElement.Flying:
                    return 5;
                case PetElement.Normal:
                    return 6;
                case PetElement.Dark:
                    return 7;
                case PetElement.Dragon:
                    return 8;
                default:
                    return -1;
            }
        }

        public static bool IsSupported(PetElement element) => IndexOf(element) >= 0;
        public static ElementDescriptor Find(PetElement element)
        {
            int index = IndexOf(element);
            return index >= 0 ? entries[index] : null;
        }

        public static string Name(PetElement element) => Find(element)?.Name ?? UnclassifiedName;
        public static bool IsValidSecondary(PetElement element) => element == PetElement.Unknown || IsSupported(element);
        public static bool IsValidCreatureTypes(PetElement primary, PetElement secondary) => IsSupported(primary) && IsValidSecondary(secondary);
    }
}
