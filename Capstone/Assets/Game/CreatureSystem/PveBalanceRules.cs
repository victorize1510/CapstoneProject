using System;

namespace Capstone.Game.CreatureSystem {
    /// <summary>Pure rules. Presentation RNG never changes these decisions.</summary>
    public static class PveBalanceRules {
        public const float DownedCaptureSeconds = 15f;
        public static int EncounterLevel(double unit) {
            if (double.IsNaN(unit) || double.IsInfinity(unit) || unit < 0 || unit >= 1) throw new ArgumentOutOfRangeException(nameof(unit));
            return unit < .1 ? 3 : unit < .3 ? 4 : unit < .7 ? 5 : unit < .9 ? 6 : 7;
        }
        public static double PartyExperienceShare(bool alive, bool finisher, bool contributed) => !alive ? 0 : finisher ? 1 : contributed ? .75 : .5;
        public static float CapturedHealth(float health, float maximum) {
            if (float.IsNaN(health) || float.IsInfinity(health) || float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0 || health < 0 || health > maximum)
                throw new ArgumentOutOfRangeException(nameof(health));
            return Math.Max(health, maximum * .25f);
        }
        public static int WindVolleyCount(double unit) {
            if (double.IsNaN(unit) || double.IsInfinity(unit) || unit < 0 || unit >= 1) throw new ArgumentOutOfRangeException(nameof(unit));
            return unit < .1 ? 2 : unit < .5 ? 3 : unit < .9 ? 4 : 5;
        }
        public static double ExperienceFactor(int petLevel, int enemyLevel) {
            if (petLevel < 1 || enemyLevel < 1) throw new ArgumentOutOfRangeException();
            int gap = petLevel - enemyLevel;
            return gap < 0 ? 1 + Math.Min(5, -gap) * .05 : gap <= 2 ? 1 : gap >= 10 ? 0 : 1 - (gap - 2) * .125;
        }
        public static int ExperienceAward(int baseReward, int petLevel, int enemyLevel, double share = 1) {
            if (baseReward < 0 || double.IsNaN(share) || double.IsInfinity(share) || share < 0 || share > 1) throw new ArgumentOutOfRangeException();
            return (int)Math.Min(int.MaxValue, Math.Floor(baseReward * share * ExperienceFactor(petLevel, enemyLevel) + .5));
        }
    }
}
