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
        bool launched;
        Collider expectedGroundCollider;
        Vector3 plannedGroundPoint;

        public MotionPhase Phase { get; private set; } = MotionPhase.Idle;
        public bool IsFinished => Phase == MotionPhase.Settled || Phase == MotionPhase.Failed;
        public bool Succeeded => Phase == MotionPhase.Settled;
        public Vector3 GroundPoint { get; private set; }
        public string FailureReason { get; private set; } = string.Empty;

        void Awake()
        {
            CacheComponents();
        }

        void FixedUpdate()
        {
            if (!launched || IsFinished || Time.time <= phaseDeadline)
            {
                return;
            }

            Fail(Phase == MotionPhase.Flight
                ? "Bóng không chạm được mặt đất trong thời gian bay cho phép."
                : "Bóng không hoàn tất lần nảy trên mặt đất.");
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!launched || IsFinished || collision == null || collision.collider == null
                || !IsGroundLayer(collision.collider.gameObject.layer))
            {
                return;
            }

            if (!TryGetGroundContact(collision, out ContactPoint contact))
            {
                return;
            }

            if (!IsValidGroundContact(collision.collider, contact.point))
            {
                return;
            }

            GroundPoint = contact.point;
            if (Phase == MotionPhase.Flight)
            {
                Phase = MotionPhase.Bounce;
                phaseDeadline = Time.time + bounceTimeout;

                Vector3 horizontalVelocity = Vector3.ProjectOnPlane(body.linearVelocity, contact.normal)
                    * bounceHorizontalDamping;
                float bounceSpeed = Mathf.Sqrt(2f * Physics.gravity.magnitude * bounceHeight);
                body.position += contact.normal * 0.01f;
                body.linearVelocity = horizontalVelocity + contact.normal * bounceSpeed;
                return;
            }

            if (Phase == MotionPhase.Bounce)
            {
                Settle(contact.point);
            }
        }

        public bool Launch(Vector3 groundTarget, float radius, float flightDuration,
            float bounceArcHeight, float horizontalDamping, float maximumBounceDuration,
            LayerMask validGroundLayers, Collider plannedGroundCollider, Transform ownerRoot)
        {
            CacheComponents();
            FailureReason = string.Empty;
            GroundPoint = default;

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

            float duration = Mathf.Max(0.2f, flightDuration);
            Vector3 targetCenter = groundTarget + Vector3.up * ballRadius;
            Vector3 displacement = targetCenter - body.position;

            body.isKinematic = false;
            body.useGravity = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.None;
            body.linearVelocity = displacement / duration - Physics.gravity * (0.5f * duration);
            body.angularVelocity = transform.right * 12f;

            launched = true;
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

        void Settle(Vector3 groundPoint)
        {
            GroundPoint = groundPoint;
            launched = false;
            Phase = MotionPhase.Settled;
            FreezeForHover();
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

            PetController[] pets = FindObjectsByType<PetController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < pets.Length; i++)
            {
                if (pets[i] != null)
                {
                    IgnoreRootCollisions(pets[i].transform);
                }
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
            if (collider == expectedGroundCollider)
            {
                return true;
            }

            if (collider.GetComponentInParent<PetController>() != null
                || collider.GetComponentInParent<DummyEnemy>() != null
                || Mathf.Abs(point.y - plannedGroundPoint.y) > 0.4f)
            {
                return false;
            }

            Vector3 offset = point - plannedGroundPoint;
            offset.y = 0f;
            return offset.sqrMagnitude <= 2.25f;
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
