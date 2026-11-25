using Capstone.Game.HudSystem;

namespace Capstone.Game.Elements
{
    /// <summary>Snapshot he tai luc commit; mac dinh la API cu khong ap dung he so moi.</summary>
    public readonly struct CombatElementSnapshot
    {
        public readonly bool Enabled;
        public readonly PetElement Attack, SourcePrimary, SourceSecondary;
        public CombatElementSnapshot(PetElement attack, PetElement primary, PetElement secondary)
        {
            Enabled = true;
            Attack = attack;
            SourcePrimary = primary;
            SourceSecondary = secondary;
        }
    }

    public static class ElementDamageMath
    {
        public static float Effectiveness(ElementMatchupLookup table, PetElement attack, PetElement primary, PetElement secondary)
        {
            if (!ElementCatalog.IsSupported(attack))
            {
                return 1f;
            }

            float first = table.Against(attack, primary);
            return secondary == primary ? first : first * table.Against(attack, secondary);
        }

        public static float Stab(CombatElementSnapshot source)
        {
            return source.Enabled && ElementCatalog.IsSupported(source.Attack) && (source.Attack == source.SourcePrimary || source.Attack == source.SourceSecondary) ? 1.5f : 1f;
        }

        public static float Multiplier(ElementMatchupLookup table, CombatElementSnapshot source, PetElement primary, PetElement secondary)
        {
            if (!source.Enabled)
            {
                return 1f;
            }

            return Effectiveness(table, source.Attack, primary, secondary) * Stab(source);
        }
    }
}
