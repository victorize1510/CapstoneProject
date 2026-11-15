using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Full authoring library. Intentionally outside Resources to avoid loading all model dependencies for UI.</summary>
    [CreateAssetMenu(menuName = "Capstone/Creatures/Library", fileName = "CreatureLibrary")]
    public sealed class CreatureLibrary : ScriptableObject {
        public List<CreatureFamilyDefinition> families = new List<CreatureFamilyDefinition>();
    }
}
