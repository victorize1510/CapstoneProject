using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>Visual-only rotation. Lifetime and damage belong to the owning cast/buff.</summary>
    public sealed class PetSkillEffectMotion : MonoBehaviour {
        public Vector3 degreesPerSecond = new Vector3(0, 65, 0);
        void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
