using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Per-instance line opacity. Shared materials stay immutable.</summary>
    public sealed class CombatFxEnvelope : MonoBehaviour {
        public float duration = .3f;
        public bool loop;
        LineRenderer[] lines;
        Color[] colors;
        float born;
        void Awake() {
            lines = GetComponentsInChildren<LineRenderer>(true); colors = new Color[lines.Length];
            for (int i = 0; i < lines.Length; i++) colors[i] = lines[i].startColor;
        }
        void OnEnable() { born = Time.time; }
        void LateUpdate() {
            float t = (Time.time - born) / Mathf.Max(.05f, duration);
            float alpha = loop ? .78f + .12f * Mathf.Sin(t * Mathf.PI * 2) : Mathf.Clamp01(t / .08f) * (1 - Mathf.Clamp01((t - .4f) / .6f));
            for (int i = 0; i < lines.Length; i++) { var color = colors[i]; color.a *= alpha; lines[i].startColor = lines[i].endColor = color; }
        }
    }
}
