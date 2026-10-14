using TMPro;
using UnityEngine;

namespace Capstone.Game.MapSystem.UI {
    public sealed class RegionDisplayPanel : MonoBehaviour {
        TMP_Text label;
        Vector2Int last = new Vector2Int(int.MinValue, int.MinValue);
        public void Bind(TMP_Text text) => label = text;
        public void SetPosition(Vector3 position) {
            var cell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
            if (cell == last || label == null) return;
            last = cell; label.text = $"Tọa độ: X {cell.x}, Z {cell.y}";
        }
    }
}
