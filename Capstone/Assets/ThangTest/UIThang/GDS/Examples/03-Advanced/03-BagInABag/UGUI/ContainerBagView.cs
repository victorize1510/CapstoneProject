using System.Collections.Generic;
using GDS.Core;
using GDS.Core.UGUI;
using UnityEngine;

namespace GDS.Examples.UGUI {

    public class ContainerBagView : MonoBehaviour {
        [SerializeField] ListBagView bagView;
        [SerializeField] List<ListBagView> containers;
        void Start() {
            bagView.Bag.OnItemChanged += UpdateItem;
            bagView.Bag.Slots.ForEach(slot => UpdateItem(slot));
        }
        void OnDestroy() { bagView.Bag.OnItemChanged -= UpdateItem; }
        private void UpdateItem(ListSlot slot) {
            if (slot.Item is not ContainerItem i) {
                containers[slot.Index].gameObject.SetActive(false);
                return;
            }

            containers[slot.Index].gameObject.SetActive(true);
            containers[slot.Index].Init(i.Capacity);
        }

    }

}