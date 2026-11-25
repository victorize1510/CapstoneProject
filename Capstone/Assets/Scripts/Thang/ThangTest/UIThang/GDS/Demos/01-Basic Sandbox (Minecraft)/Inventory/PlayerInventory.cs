using GDS.Core;

namespace GDS.Demos.Basic {
    [System.Serializable]
    public class PlayerInventory {
        public Equipment Equipment = new();
        public Inventory Inventory = new();
        public Observable<int> PlayerGold = new(1000);
    }
}