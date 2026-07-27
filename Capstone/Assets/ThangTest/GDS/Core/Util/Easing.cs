using System;

namespace GDS.Core {
    public static class Easing {
        public static float InQuad(float t) => t * t;
        public static float OutQuad(float t) => 1 - (1 - t) * (1 - t);
        public static float InCubic(float t) => t * t * t;
        public static float OutCubic(float t) => 1 - MathF.Pow(1 - t, 3);
        public static float InBack(float t) {
            float s = 1.70158f;
            return t * t * ((s + 1) * t - s);
        }
        public static float OutBack(float t) => 1 - InBack(1 - t);
        public static float InOutBack(float t) {
            if (t < 0.5) return InBack(t * 2) / 2;
            return 1 - InBack((1 - t) * 2) / 2;
        }
    }
}