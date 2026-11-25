using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GDS.Core {

    [CreateAssetMenu(menuName = "SO/Core/ItemBase")]
    public class ItemBase : ScriptableObject {
        public string Name;
        public Sprite Icon;
        public bool Stackable = false;
        public int MaxStackSize = 100;
        public List<Tag> Tags = new();

        public void OnEnable() => Name ??= name;
        virtual public Item CreateItem() => new() { Base = this, Name = Name, StackSize = Stackable ? MaxStackSize : 1 };
    }

    [System.Serializable]
    public class Item : IEquatable<Item> {
        public string Id = IdExt.ShortId();
        public string Name;
        public int StackSize;
        public ItemBase Base;
        public Sprite Icon => Base.Icon;
        public bool Stackable => Base.Stackable;
        public int MaxStackSize => Base.MaxStackSize;

        public virtual Item Clone() => new() { Id = Id, Base = Base, Name = Name, StackSize = StackSize };

        public virtual bool Equals(Item other) => other != null && other.Id == Id && other.StackSize == StackSize;

        // TODO: Use Describe decorator instead of ToString (to avoid repetition in extended classes)
        public override string ToString() {
            if (Base == null) return "NoItemBase";
            return Stackable
            ? $"{Name}, {StackSize} ({Id})"
            : $"{Name} ({Id})";
        }
    }

    public static class ItemExt {
        public static Item Create<T>(ItemBase itemBase) where T : Item, new() => new T() { Base = itemBase, Name = itemBase.Name, StackSize = itemBase.Stackable ? itemBase.MaxStackSize : 1 };
        public static string ToPrettyString(Item item) => item == null ? "<null>".Gray() : item + $" ({item.GetType()})".Gray();
        public static string ItemNameWithQuant(this Item item) => item.Stackable ? $"{item.Name} ({item.StackSize})" : item.Name;

        public static bool CanStackOn(this Item fromItem, Item toItem) =>
            (fromItem.Stackable && toItem == null) ||
            (fromItem.Stackable && fromItem.Base == toItem.Base && toItem.StackSize < toItem.MaxStackSize);

        public static (Item newFromItem, Item newToItem) TransferAll(this Item fromItem, Item toItem) {
            var total = toItem.StackSize + fromItem.StackSize;
            toItem.StackSize = System.Math.Min(total, toItem.MaxStackSize);
            fromItem.StackSize = total - toItem.StackSize;
            return (fromItem.StackSize > 0 ? fromItem : null, toItem);
        }

        public static (Item newFromItem, Item newToItem) TransferOne(this Item fromItem, Item toItem) {
            if (fromItem == null) { Debug.LogWarning("Warning! fromItem cannot be null, returning noop."); return (fromItem, toItem); }
            if (toItem == null) {
                toItem = fromItem.Clone();
                toItem.StackSize = 0;
            }
            if (toItem.StackSize < toItem.MaxStackSize) {
                fromItem.StackSize--;
                toItem.StackSize++;
            }
            return (fromItem.StackSize > 0 ? fromItem : null, toItem);
        }

        public static (Item newFromItem, Item newToItem) SplitHalf(this Item item) {
            var newItem = item.Clone();
            // Note that half is rounded down
            int half = item.StackSize / 2;
            item.StackSize = half;
            newItem.StackSize -= half;
            return (item.StackSize > 0 ? item : null, newItem);
        }
    }

    public static class IdExt {
        const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        static System.Random random = new System.Random();
        public static string ShortId(int length = 6) {
            return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
        }
    }
}