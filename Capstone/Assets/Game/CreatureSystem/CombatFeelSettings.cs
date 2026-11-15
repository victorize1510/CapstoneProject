using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    [CreateAssetMenu(menuName = "Capstone/Creatures/Combat feel", fileName = "CombatFeelSettings")]
    public sealed class CombatFeelSettings : ScriptableObject {
        [Range(0, .03f)] public float cameraAmplitude = .03f;
        [Range(.05f, .2f)] public float cameraDuration = .1f;
        [Range(0, .05f)] public float criticalVisualPause = .035f;
        public GameObject basicImpact, bleedImpact;
        public AudioClip basicSound, bleedSound;
    }
}
