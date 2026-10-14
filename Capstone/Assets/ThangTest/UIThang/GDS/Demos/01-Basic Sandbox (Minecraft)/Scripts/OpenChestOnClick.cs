using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Demos.Basic {

    public class OpenChestOnClick : MonoBehaviour, IPointerClickHandler {

        [SerializeField, Required] Store Store;
        [SerializeField, Space(12)] Chest Bag;

        public void OnPointerClick(PointerEventData eventData) {
            Store.Bus.Publish(new OpenWindow(Bag));
        }
    }

}