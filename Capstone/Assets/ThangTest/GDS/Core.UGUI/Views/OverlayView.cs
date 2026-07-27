using UnityEngine;
using UnityEngine.UI;

namespace GDS.Core {

    public class OverlayViewUGUI : MonoBehaviour {
        [Required]
        public Image Image;
        public Color ValidColor = Color.green;
        public Color InvalidColor = Color.red;

        public void SetValid(bool valid) {
            Image.color = valid ? ValidColor : InvalidColor;
        }
    }

}