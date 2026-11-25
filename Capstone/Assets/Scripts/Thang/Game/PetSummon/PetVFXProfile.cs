using UnityEngine;

namespace Capstone.Game.PetSummon
{
    public enum PetVFXQuality { Low, Medium, High }

    [CreateAssetMenu(menuName = "Capstone/Pets/VFX Profile", fileName = "PetVFX_Default")]
    public sealed class PetVFXProfile : ScriptableObject
    {
        [Header("Resources")]
        public PetSummonVfxInstance spawnPrefab;
        public PetSummonVfxInstance recallPrefab;
        public Shader bodyShader;
        public Shader energyShader;
        public Texture2D dissolveNoise;
        [Header("Colour")]
        [ColorUsage(true, true)] public Color primaryColor = new Color(.72f, 1f, 1f, 1f);
        public Color secondaryColor = new Color(.45f, 1f, .78f, 1f);
        [Range(0, 1)] public float whiteBlend = .75f;
        [Range(0, 1)] public float bodyEmission = .12f;
        [Header("Timing at 1x")]
        [Min(.1f)] public float spawnDuration = .8f;
        [Min(.1f)] public float recallDuration = .65f;
        [Header("Shape")]
        [Min(.1f)] public float effectScale = 1f;
        [Tooltip("Extra room around the current model for the flash; independent of halo padding.")]
        [Range(0, .2f)] public float burstPadding = .08f;
        [Range(0, .1f)] public float haloPadding = .05f;
        [Header("Recall deformation")]
        public bool deformRecall = true;
        [Range(0, 60)] public float recallTwistDegrees = 60f;
        [Range(0, .08f)] public float recallPullDelay = .08f;
        [Header("Energy")]
        [Range(0, .2f)] public float shellOpacity = .08f;
        [Range(.005f, .05f)] public float orbitThickness = .015f;
        [Range(.005f, .15f)] public float edgeWidth = .035f;
        [Min(0)] public float edgeEmission = 1.5f;
        [Range(0, 1)] public float particleAmount = 1f;

        public static int ParticleBudget(PetVFXQuality quality) => quality == PetVFXQuality.Low ? 16
            : quality == PetVFXQuality.High ? 48 : 32;
        public static int OrbitCount(PetVFXQuality quality) => quality == PetVFXQuality.Low ? 2 : 3;
        public static int SegmentCount(PetVFXQuality quality) => quality == PetVFXQuality.Low ? 40
            : quality == PetVFXQuality.High ? 128 : 80;
    }
}
