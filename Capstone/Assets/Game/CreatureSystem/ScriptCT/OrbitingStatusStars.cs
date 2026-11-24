using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Reusable fallback art for downed status; no status application or timers.</summary>
    public sealed class OrbitingStatusStars : MonoBehaviour {
        public Transform[] stars = System.Array.Empty<Transform>();
        float phase;
        void OnEnable() { phase = 0; }
        void Update() {
            phase += Time.deltaTime * 2.6f;
            for (int i = 0; i < stars.Length; i++) {
                if (stars[i] == null) continue;
                float angle = phase + i * Mathf.PI * 2 / stars.Length;
                stars[i].localPosition = new Vector3(Mathf.Cos(angle) * .65f, Mathf.Sin(angle * 2) * .08f, Mathf.Sin(angle) * .45f);
                stars[i].localRotation = Quaternion.Euler(15, -phase * Mathf.Rad2Deg, 0);
            }
        }
    }
}
