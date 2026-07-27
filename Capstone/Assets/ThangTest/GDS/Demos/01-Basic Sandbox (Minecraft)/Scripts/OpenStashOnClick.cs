using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.EventSystems;

namespace GDS.Demos.Basic {

    public class OpenStashOnClick : MonoBehaviour, IPointerClickHandler {

        [SerializeField, Required] Store store;
        [SerializeField, Space(12)] Stash Bag;

        public void OnPointerClick(PointerEventData eventData) {
            store.Bus.Publish(new OpenWindow(Bag));
        }
    }

}