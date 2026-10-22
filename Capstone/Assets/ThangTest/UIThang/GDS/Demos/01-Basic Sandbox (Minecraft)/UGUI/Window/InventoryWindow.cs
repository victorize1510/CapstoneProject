using GDS.Core;
using GDS.Core.UGUI;
using TMPro;
using UnityEngine;

namespace GDS.Demos.Basic.UGUI {

    public class InventoryWindow : MonoBehaviour {
        [SerializeField] WindowView windowView;
        [SerializeField] SetBagView euqipmentView;
        [SerializeField] ListBagView inventoryView;
        [SerializeField] TextMeshProUGUI goldLabel;

        Observable<int> playerGold;
        bool ready = false;

        public void Init(PlayerInventory player) {
            playerGold = player.PlayerGold;
            ready = true;

            inventoryView.Init(player.Inventory);
            euqipmentView.Init(player.Equipment);
            windowView.Init(player);
            OnEnable();
        }

        void OnEnable() {
            if (!ready) return;
            OnPlayerGoldChange(playerGold.Value);
            playerGold.OnChange += OnPlayerGoldChange;
        }
        void OnDisable() {
            if (!ready) return;
            playerGold.OnChange -= OnPlayerGoldChange;
        }
        void OnPlayerGoldChange(int value) { goldLabel.text = value.ToString(); }
    }

}