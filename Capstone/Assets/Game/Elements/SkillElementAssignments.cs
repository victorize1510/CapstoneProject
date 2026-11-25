using System;
using Capstone.Game.HudSystem;

namespace Capstone.Game.Elements
{
    /// <summary>Chi chuan hoa cac ID da duoc duyet; noi dung legacy khac giu nguyen raw.</summary>
    public static class SkillElementAssignments
    {
        public static PetElement Resolve(string skillId, PetElement stored)
        {
            if (string.Equals(skillId, "bite", StringComparison.OrdinalIgnoreCase) || string.Equals(skillId, "blast", StringComparison.OrdinalIgnoreCase))
            {
                return PetElement.Normal;
            }

            if (string.Equals(skillId, "projectile", StringComparison.OrdinalIgnoreCase))
            {
                return PetElement.Fire;
            }

            if (string.Equals(skillId, "wing strike", StringComparison.OrdinalIgnoreCase))
            {
                return PetElement.Flying;
            }

            return stored == PetElement.Fighting ? PetElement.Normal : stored;
        }
    }
}
