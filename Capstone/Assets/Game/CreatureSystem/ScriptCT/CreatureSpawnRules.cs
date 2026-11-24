using System;
using System.Collections.Generic;

namespace Capstone.Game.CreatureSystem
{
    // Quy tac chon thu va cap, khong phu thuoc scene hay du lieu save.
    internal static class CreatureSpawnRules
    {
        public static int Population(int speciesCount, Random random)
        {
            return speciesCount <= 0 ? 0 : random.Next(3, speciesCount == 1 ? 6 : 7);
        }

        public static int Level(int baseLevel, int variance, int maximum, Random random)
        {
            int center = Math.Max(1, Math.Min(maximum, baseLevel));
            int spread = Math.Max(0, Math.Min(maximum, variance));
            int minimum = Math.Max(1, center - spread);
            return random.Next(minimum, Math.Min(maximum, center + spread) + 1);
        }

        public static int MinimumFormLevel(int stage) => stage <= 0 ? 1 : stage == 1 ? 18 : 36;

        public static bool TryLevelRange(int baseLevel, int variance, int maximum,
            int zoneMin, int zoneMax, int regionMin, int regionMax, int speciesMin, int speciesMax,
            out int minimum, out int highest)
        {
            int center = Math.Max(1, Math.Min(maximum, baseLevel));
            int spread = Math.Max(0, Math.Min(maximum, variance));
            minimum = Math.Max(1, center - spread);
            highest = Math.Min(maximum, center + spread);
            if (zoneMin > 0) minimum = Math.Max(minimum, zoneMin);
            if (zoneMax > 0) highest = Math.Min(highest, zoneMax);
            if (regionMin > 0) minimum = Math.Max(minimum, regionMin);
            if (regionMax > 0) highest = Math.Min(highest, regionMax);
            if (speciesMin > 0) minimum = Math.Max(minimum, speciesMin);
            if (speciesMax > 0) highest = Math.Min(highest, speciesMax);
            return minimum <= highest;
        }

        public static int FormReferenceLevel(bool useIndividualLevel, int baseLevel, int individualLevel)
            => useIndividualLevel ? individualLevel : baseLevel;

        public static bool ValidWeight(float value) => value > 0 && !float.IsNaN(value) && !float.IsInfinity(value);

        public static int Pick(IReadOnlyList<float> weights, IReadOnlyList<bool> represented, Random random)
        {
            bool missing = false;
            if (represented != null)
                for (int i = 0; i < weights.Count; i++)
                    if (ValidWeight(weights[i]) && !represented[i]) missing = true;

            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                if (ValidWeight(weights[i]) && (!missing || !represented[i])) total += weights[i];
            if (total <= 0) return -1;

            double roll = random.NextDouble() * total;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (!ValidWeight(weights[i]) || (missing && represented[i])) continue;
                last = i;
                if ((roll -= weights[i]) < 0) return i;
            }
            return last;
        }
    }
}
