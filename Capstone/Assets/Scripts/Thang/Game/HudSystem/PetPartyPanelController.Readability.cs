using UnityEngine.UIElements;

namespace Capstone.Game.HudSystem {
    public sealed partial class PetPartyPanelController {
        VisualElement geneticsTable;
        readonly Label[] ivCells = new Label[6], evCells = new Label[6];
        static readonly PetCoreStat[] GeneticsOrder = { PetCoreStat.Hp, PetCoreStat.Attack, PetCoreStat.MagicAttack, PetCoreStat.Defense, PetCoreStat.MagicDefense, PetCoreStat.Speed };
        void RefreshGeneticsTable(VisualElement documentRoot, PetCollectionMetadata metadata) {
            var next = documentRoot.Q("pet-details-genetics-table"); if (next == null) return;
            if (geneticsTable != next) {
                geneticsTable = next; next.Clear();
                string[] headings = { "Chỉ số", "HP", "ATK", "SP.ATK", "DEF", "SP.DEF", "SPD" };
                var header = new VisualElement(); header.AddToClassList("genetics-row"); header.AddToClassList("genetics-heading");
                for (int i = 0; i < headings.Length; i++) { var label = new Label(headings[i]) { pickingMode = PickingMode.Ignore }; if (i == 0) label.AddToClassList("genetics-key"); header.Add(label); }
                next.Add(header);
                for (int row = 0; row < 2; row++) {
                    var element = new VisualElement { pickingMode = PickingMode.Ignore }; element.AddToClassList("genetics-row");
                    var key = new Label(row == 0 ? "IV (0–"+PetStatCalculator.MaximumIndividualValue+")" : "EV") { pickingMode = PickingMode.Ignore }; key.AddToClassList("genetics-key"); element.Add(key);
                    for (int i = 0; i < 6; i++) { var value = new Label { name = "pet-details-" + (row == 0 ? "iv-" : "ev-") + i, pickingMode = PickingMode.Ignore }; element.Add(value); (row == 0 ? ivCells : evCells)[i] = value; }
                    next.Add(element);
                }
            }
            for (int i = 0; i < 6; i++) { ivCells[i].text = metadata.IndividualValues.Get(GeneticsOrder[i]).ToString(); evCells[i].text = metadata.EffortValues.Get(GeneticsOrder[i]).ToString(); }
            SetNamedText(documentRoot, "pet-details-ev-total", "EV đã phân bổ: " + metadata.EffortValues.Total + " / "+PetStatCalculator.MaximumEffortTotal);
        }
    }
}
