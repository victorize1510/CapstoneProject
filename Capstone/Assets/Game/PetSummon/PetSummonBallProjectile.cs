using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.PetSummon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PetSummonBallProjectile : MonoBehaviour
    {
        public enum MotionPhase
        {
            Idle,
            Flight,
            Bounce,
            Deflected,
            Settled,
            Failed
        }

        readonly List<Collider> ignoredBallColliders = new List<Collider>();
        readonly List<Collider> ignoredOwnerColliders = new List<Collider>();

        Rigidbody body;
        Collider[] ballColliders;
        LayerMask groundLayers;
        float ballRadius;
        float bounceHeight;
        float bounceTimeout;
        float bounceHorizontalDamping;
        float phaseDeadline;
        float bounceStartedAt;
        bool bounceLeftGround;
        bool launched;
        Collider expectedGroundCollider;
        Vector3 plannedGroundPoint;
        float motionSpeed = 1f;

        public MotionPhase Phase { get; private set; } = MotionPhase.Idle;
        public bool IsFinished => Phase == MotionPhase.Settled || Phase == MotionPhase.Failed;
        public bool Succeeded => Phase == MotionPhase.Settled;
        public Vector3 GroundPoint { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;
        public float ExpectedFlightDuration { get; private set; }

        void Awake()
        {
            CacheComponents();
        }

        void FixedUpdate()
        {
            if (!launched || IsFinished)
            {
                return;
            }

            // Scale velocity and gravity together so a faster throw keeps the same arc.
            if (Phase == MotionPhase.Flight)
                body.AddForce(Physics.gravity * (motionSpeed * motionSpeed - 1f), ForceMode.Acceleration);

            if (Phase == MotionPhase.Bounce)
            {
                bounceLeftGround |= body.position.y > GroundPoint.y + ballRadius + 0.12f;
                if (bounceLeftGround && Time.time > bounceStartedAt + Time.fixedDeltaTime
                    && body.linearVelocity.y <= 0.05f)
                {
                    Settle();
                    return;
                }
            }

            if (Time.time <= phaseDeadline)
            {
                return;
            }

            if (Phase == MotionPhase.Deflected)
            {
                Fail("Bóng va vào tường hoặc vật cản; không gọi pet.");
                return;
            }

            Fail(Phase == MotionPhase.Flight
                ? "Bóng không chạm được mặt đất trong thời gian bay cho phép."
                : "Bóng không hoàn tất lần nảy trên mặt đất.");
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!launched || IsFinished || Phase == MotionPhase.Deflected
                || collision == null || collision.collider == null)
            {
                return;
            }

            if (!IsGroundLayer(collision.collider.gameObject.layer)
                || !TryGetGroundContact(collision, out ContactPoint contact)
                || !IsValidGroundContact(collision.collider, contact.point))
            {
                Deflect(collision);
                return;
            }

            if (Phase == MotionPhase.Flight)
            {
                GroundPoint = contact.point;
                Phase = MotionPhase.Bounce;
                bounceStartedAt = Time.time;
                bounceLeftGround = false;
                phaseDeadline = Time.time + bounceTimeout;

                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up)
                    * (bounceHorizontalDamping / motionSpeed);
                float bounceSpeed = Mathf.Sqrt(2f * -Physics.gravity.y * bounceHeight);
                body.position += contact.normal * 0.01f;
                body.linearVelocity = horizontalVelocity + Vector3.up * bounceSpeed;
                return;
            }

            if (Phase == MotionPhase.Bounce && bounceLeftGround)
            {
                Fail("Bóng chạm đất trước khi hoàn tất cú nảy.");
            }
        }

        public static bool TryCalculateArc(Vector3 start, Vector3 target,
            float apexAboveEndpoints, out Vector3 initialVelocity, out float duration, float speed = 1f)
        {
            initialVelocity = default;
            duration = 0f;
            float gravity = -Physics.gravity.y;
            if (gravity <= 0.01f)
            {
                return false;
            }

            float apex = Mathf.Max(start.y, target.y) + Mathf.Max(0.1f, apexAboveEndpoints);
            float rise = apex - start.y;
            float fall = apex - target.y;
            float verticalSpeed = Mathf.Sqrt(2f * gravity * rise);
            duration = verticalSpeed / gravity + Mathf.Sqrt(2f * fall / gravity);
            if (duration <= 0.01f)
            {
                return false;
            }

            Vector3 horizontal = target - start;
            horizontal.y = 0f;
            initialVelocity = horizontal / duration + Vector3.up * verticalSpeed;
            speed = Mathf.Max(0.1f, speed);
            initialVelocity *= speed;
            duration /= speed;
            return true;
        }

        public bool Launch(Vector3 groundTarget, float radius, float arcApexHeight,
            float bounceArcHeight, float horizontalDamping, float maximumBounceDuration,
            LayerMask validGroundLayers, Collider plannedGroundCollider, Transform ownerRoot,
            float speed = 1f)
        {
            CacheComponents();
            FailureReason = string.Empty;
            GroundPoint = default;
            ExpectedFlightDuration = 0f;
            motionSpeed = Mathf.Max(0.1f, speed);

            if (body == null || ballColliders.Length == 0)
            {
                Fail("Prefab bóng phải có Rigidbody và ít nhất một Collider.");
                return false;
            }

            if (plannedGroundCollider == null)
            {
                Fail("Collider mặt đất dự kiến đã biến mất trước lúc ném bóng.");
                return false;
            }

            if (Physics.gravity.y >= -0.01f)
            {
                Fail("Cần trọng lực hướng xuống để ném và nảy bóng.");
                return false;
            }

            ballRadius = Mathf.Max(0.02f, radius);
            bounceHeight = Mathf.Max(0.05f, bounceArcHeight);
            bounceHorizontalDamping = Mathf.Clamp01(horizontalDamping);
            bounceTimeout = Mathf.Max(0.25f, maximumBounceDuration);
            groundLayers = validGroundLayers;
            expectedGroundCollider = plannedGroundCollider;
            plannedGroundPoint = groundTarget;
            IgnoreGameplayCollisions(ownerRoot);

            for (int i = 0; i < ballColliders.Length; i++)
            {
                Collider ballCollider = ballColliders[i];
                if (ballCollider == null)
                {
                    continue;
                }

                ballCollider.enabled = true;
                ballCollider.isTrigger = false;
            }

            // The hand animated after the previous physics step. Keep the exact
            // visible release pose instead of launching from a stale Rigidbody pose.
            body.position = transform.position;
            Physics.SyncTransforms();
            Vector3 targetCenter = groundTarget + Vector3.up * ballRadius;
            if (!TryCalculateArc(body.position, targetCenter, arcApexHeight,
                out Vector3 launchVelocity, out float duration, motionSpeed))
            {
                Fail("Không tính được quỹ đạo ném bóng.");
                return false;
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
            body.linearVelocity = launchVelocity;
            body.angularVelocity = transform.right * 12f;

            launched = true;
            ExpectedFlightDuration = duration;
            Phase = MotionPhase.Flight;
            phaseDeadline = Time.time + duration + 1f;
            return true;
        }

        public void PrepareForHand()
        {
            CacheComponents();
            RestoreOwnerCollisions();
            launched = false;
            Phase = MotionPhase.Idle;
            FailureReason = string.Empty;
            expectedGroundCollider = null;
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.useGravity = false;
                body.isKinematic = true;
                body.interpolation = RigidbodyInterpolation.None;
                body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }

            for (int i = 0; i < ballColliders.Length; i++)
            {
                if (ballColliders[i] != null)
                {
                    ballColliders[i].enabled = false;
                }
            }
        }

        public void FreezeForHover()
        {
            CacheComponents();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.useGravity = false;
                body.isKinematic = true;
            }

            for (int i = 0; i < ballColliders.Length; i++)
            {
                if (ballColliders[i] != null)
                {
                    ballColliders[i].enabled = false;
                }
            }
        }

        void Settle()
        {
            launched = false;
            Phase = MotionPhase.Settled;
            FreezeForHover();
        }

        void Deflect(Collision collision)
        {
            Phase = MotionPhase.Deflected;
            phaseDeadline = Time.time + 0.35f;
            if (body != null && collision.contactCount > 0)
            {
                Vector3 incoming = collision.relativeVelocity.sqrMagnitude > 0.01f
                    ? collision.relativeVelocity
                    : body.linearVelocity;
                Vector3 reflected = Vector3.Reflect(incoming,
                    collision.GetContact(0).normal) * 0.5f;
                body.linearVelocity = reflected + Vector3.up * 0.5f;
            }
        }

        void Fail(string reason)
        {
            FailureReason = reason ?? string.Empty;
            launched = false;
            Phase = MotionPhase.Failed;
            FreezeForHover();
        }

        void CacheComponents()
        {
            body ??= GetComponent<Rigidbody>();
            ballColliders ??= GetComponentsInChildren<Collider>(true);
        }

        void IgnoreGameplayCollisions(Transform ownerRoot)
        {
            RestoreOwnerCollisions();
            IgnoreRootCollisions(ownerRoot);

            PetCommandInput commandInput = ownerRoot != null
                ? ownerRoot.GetComponent<PetCommandInput>()
                : null;
            PetController activePet = commandInput != null ? commandInput.activePet : null;
            if (activePet != null && activePet.IsSummoned)
            {
                IgnoreRootCollisions(activePet.transform);
            }
        }

        void IgnoreRootCollisions(Transform root)
        {
            if (root == null)
            {
                return;
            }

            Collider[] ownerColliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ballColliders.Length; i++)
            {
                Collider ballCollider = ballColliders[i];
                if (ballCollider == null)
                {
                    continue;
                }

                for (int j = 0; j < ownerColliders.Length; j++)
                {
                    Collider ownerCollider = ownerColliders[j];
                    if (ownerCollider == null || ownerCollider == ballCollider)
                    {
                        continue;
                    }

                    Physics.IgnoreCollision(ballCollider, ownerCollider, true);
                    ignoredBallColliders.Add(ballCollider);
                    ignoredOwnerColliders.Add(ownerCollider);
                }
            }
        }

        void RestoreOwnerCollisions()
        {
            int pairCount = Mathf.Min(ignoredBallColliders.Count, ignoredOwnerColliders.Count);
            for (int i = 0; i < pairCount; i++)
            {
                Collider ballCollider = ignoredBallColliders[i];
                Collider ownerCollider = ignoredOwnerColliders[i];
                if (ballCollider != null && ownerCollider != null)
                {
                    Physics.IgnoreCollision(ballCollider, ownerCollider, false);
                }
            }

            ignoredBallColliders.Clear();
            ignoredOwnerColliders.Clear();
        }

        bool IsGroundLayer(int layer)
        {
            return (groundLayers.value & (1 << layer)) != 0;
        }

        bool IsValidGroundContact(Collider collider, Vector3 point)
        {
            if (collider.GetComponentInParent<PetController>() != null
                || collider.GetComponentInParent<DummyEnemy>() != null
                || Mathf.Abs(point.y - plannedGroundPoint.y) > 0.4f)
            {
                return false;
            }

            Vector3 offset = point - plannedGroundPoint;
            offset.y = 0f;
            return offset.sqrMagnitude <= 2.25f
                && (collider == expectedGroundCollider || IsGroundLayer(collider.gameObject.layer));
        }

        static bool TryGetGroundContact(Collision collision, out ContactPoint bestContact)
        {
            bestContact = default;
            float bestUp = 0.45f;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                if (contact.normal.y <= bestUp)
                {
                    continue;
                }

                bestUp = contact.normal.y;
                bestContact = contact;
            }

            return bestUp > 0.45f;
        }

        void OnDestroy()
        {
            RestoreOwnerCollisions();
        }
    }
}
