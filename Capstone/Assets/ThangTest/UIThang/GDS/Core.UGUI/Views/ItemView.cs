using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GDS.Core.UGUI {

    public interface IItemView {
        Item Item { get; set; }
    }

    public class ItemView : MonoBehaviour, IItemView {
        [SerializeField, Required] TextMeshProUGUI stackSize;
        [SerializeField, Required] Image icon;
        [SerializeField, Required] Sprite missingIconSprite;

        Item item;
        public Item Item {
            get => item;
            set => SetItem(value);
        }

        public Image Icon => icon;

        void SetItem(Item value) {
            // TODO: should we guard for value equality here?
            item = value;
            if (item == null) return;

            icon.sprite = item.Icon ?? missingIconSprite;
            stackSize.enabled = item.Stackable;
            stackSize.text = item.StackSize.ToString();
        }

    }
}
