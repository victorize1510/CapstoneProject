using System;
using System.Collections.Generic;

namespace Capstone.Game.Skills {
    public enum SkillDamageLabel { Unspecified, None, Physical, Magical }
    public enum SkillEffectLabel { Damage, Buff, Debuff, Heal, Shield, Control, Stun, Slow, Root, Silence, Dot, Regen, Mobility }
    [Serializable] public sealed class SkillEffectDescription {
        public SkillEffectLabel role;
        public string description;
    }
    [Serializable] public sealed class SkillUiFact { public string label; public string value; }
    // Authored display metadata only. Does not apply damage/status or infer mechanics from names/artwork.
    [Serializable] public sealed class SkillUiMetadata {
        public SkillDamageLabel damageKind;
        public List<SkillEffectDescription> effects = new();
        public List<SkillUiFact> facts = new();
    }
}
