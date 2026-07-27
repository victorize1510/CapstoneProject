using UnityEngine;
using GDS.Core.Events;
using GDS.Core.UGUI;
using GDS.Core;
using TMPro;

namespace GDS.Demos.Basic.UGUI {

    public class CharacterSheetWindow : MonoBehaviour {
        [SerializeField] WindowView windowView;
        [SerializeField] TextMeshProUGUI statsText;

        Observable<CharacterStats> stats;

        public CharacterSheetWindow Init(CharacterSheet charSheet) {
            stats = charSheet.Stats;
            windowView.Init(charSheet);
            Render(stats.Value);
            return this;
        }

        void OnEnable() { stats.OnChange += Render; }
        void OnDisable() { stats.OnChange -= Render; }

        void Render(CharacterStats stats) {
            var str = @$"Defense: {stats.Defense}
Attack Damage: {stats.AttackDamage}
Attack Speed: {stats.AttackSpeed}
DPS: {stats.Dps}
";
            statsText.text = str;
        }

    }

}