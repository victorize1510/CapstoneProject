using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace GDS.Core.UGUI {

    public class TabsView : MonoBehaviour {
        [SerializeField] ListBagCollection collection = new();
        [SerializeField] Toggle buttonPrefab;
        [SerializeField] ListBagView listBagPrefab;
        [SerializeField] Transform buttonBar;
        [SerializeField] Transform viewport;
        [SerializeField] TextMeshProUGUI selectedTabText;
        [SerializeField] ToggleGroup toggleGroup;

        [NonSerialized] List<Toggle> buttons = new();

        public ListBagCollection Collection => collection;
        bool initialized = false;

        void Awake() {
            if (initialized) return;
            Init(collection);
        }

        public void Init(ListBagCollection listBagCollection) {
            buttonBar.Clear();
            collection = listBagCollection;
            var tabsCount = collection.Bags.Count;

            for (var i = 0; i < tabsCount; i++) {
                var index = i;
                var button = Instantiate(buttonPrefab, buttonBar).GetComponent<Toggle>();
                button.group = toggleGroup;
                button.GetComponentInChildren<TextMeshProUGUI>().text = collection.Bags[i].Name;
                button.onValueChanged.AddListener((_) => {
                    collection.SetCurrentIndex(index);
                });
                buttons.Add(button);
                if (i == 0) button.isOn = true;
            }

            collection.CurrentIndex.OnChange += onTabChange;
            onTabChange(0);
            initialized = true;
        }

        void OnDestroy() {
            collection.CurrentIndex.OnChange -= onTabChange;
        }

        private void onTabChange(int i) {
            if (i >= collection.Bags.Count) return;
            viewport.Clear();
            selectedTabText.text = "Current: " + collection.Bags[i].Name;
            var listBagView = Instantiate(listBagPrefab, viewport);
            listBagView.Init(collection.Bags[i], true);

        }

    }

}