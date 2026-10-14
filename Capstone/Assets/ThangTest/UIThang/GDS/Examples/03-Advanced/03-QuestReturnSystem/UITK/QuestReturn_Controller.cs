using System.Collections.Generic;
using GDS.Core;
using GDS.Core.UITK;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples.UITK {
    [RequireComponent(typeof(UIDocument))]
    public class QuestReturnSystem_Controller : MonoBehaviour {
        public Store store;

        public ListBag inventory = new() { Size = 20 };

        public List<QuestData> quests;

        void OnEnable() {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.AddManipulator(new DragDropManipulator(store));
            root.Q<ListBagView>().Init(inventory);
            root.Q<QuestView>().Init(inventory, quests);
        }
    }

}