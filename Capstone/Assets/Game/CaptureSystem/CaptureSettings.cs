using Capstone.Game.Inventory;
using UnityEngine;
namespace Capstone.Game.CaptureSystem {
    [CreateAssetMenu(menuName="Game/Capture/Settings")]
    public sealed class CaptureSettings : ScriptableObject {
        [Tooltip("Off until a trustworthy unique-species source is connected.")] public bool enableCriticalCapture;
        public GameObject ballVisual;
        public AudioClip successSound, escapeSound;
    }
    public static class CaptureAssets {
        public static MonsterItemDefinition LoadBall() => Resources.Load<MonsterItemDefinition>("Capture/BasicCaptureBall");
        public static CaptureSettings Settings => Resources.Load<CaptureSettings>("Capture/CaptureSettings");
    }
}
