using TMPro;
using UnityEngine;

namespace GDS.Core.UGUI {
    public class CountWarning : MonoBehaviour {
        [SerializeField] TextMeshProUGUI textTmp;
        public void SetState(int dataCount, int viewCount) {
            gameObject.SetActive(true);
            if (dataCount == viewCount) { gameObject.SetActive(false); return; }
            textTmp.text = $"Slot count mismatch: Data ({dataCount}), View ({viewCount})";
        }
    }
}
