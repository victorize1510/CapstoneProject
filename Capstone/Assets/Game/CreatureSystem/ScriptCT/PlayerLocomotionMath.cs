using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    /// <summary>Phep tinh di chuyen thuan; khong doc input, Animator hay trang thai scene.</summary>
    public static class PlayerLocomotionMath
    {
        public static float JumpVelocity(float height, float gravity)
        {
            return Mathf.Sqrt(Mathf.Max(0f, height * -2f * gravity));
        }

        public static float JumpAirTime(float height, float gravity, float fallMultiplier)
        {
            float magnitude = Mathf.Abs(gravity);
            if (magnitude <= .001f) return 0f;
            float riseTime = JumpVelocity(height, gravity) / magnitude;
            float fallTime = Mathf.Sqrt((2f * height) / (magnitude * Mathf.Max(1f, fallMultiplier)));
            return riseTime + fallTime;
        }

        public static float LandingCarryDistance(float speed, float airTime, float ratio, float nudge, float maximum)
        {
            float distance = Mathf.Max(speed * airTime * ratio, nudge);
            return Mathf.Clamp(distance, 0f, maximum);
        }

        public static Vector3 MoveDirection(Vector2 input, Vector3 forward, Vector3 right)
        {
            return Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
        }
    }
}
