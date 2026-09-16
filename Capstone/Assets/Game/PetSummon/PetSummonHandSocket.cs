using System;
using UnityEngine;

namespace Capstone.Game.PetSummon
{
    [DisallowMultipleComponent]
    public sealed class PetSummonHandSocket : MonoBehaviour
    {
        [Serializable]
        public sealed class VisualBinding
        {
            [SerializeField] string visualName = string.Empty;
            [SerializeField] Transform socket = null;
            [SerializeField] Vector3 ballLocalPosition = Vector3.zero;
            [SerializeField] Vector3 ballLocalEulerAngles = Vector3.zero;
            [SerializeField, Min(0.001f)] float ballLocalScale = 0.2f;

            public string VisualName => visualName?.Trim() ?? string.Empty;
            public Transform Socket => socket;
            public Vector3 BallLocalPosition => ballLocalPosition;
            public Quaternion BallLocalRotation => Quaternion.Euler(ballLocalEulerAngles);
            public float BallLocalScale => Mathf.Max(0.001f, ballLocalScale);

            public VisualBinding(string name, Transform targetSocket, Vector3 localPosition,
                Vector3 localEulerAngles, float localScale)
            {
                visualName = name;
                socket = targetSocket;
                ballLocalPosition = localPosition;
                ballLocalEulerAngles = localEulerAngles;
                ballLocalScale = Mathf.Max(0.001f, localScale);
            }
        }

        [SerializeField] PlayerVisualSwitcher visualSwitcher = null;
        [SerializeField] GameObject ballPrefab = null;
        [SerializeField] VisualBinding[] bindings = Array.Empty<VisualBinding>();
        [SerializeField] Vector3 runtimeBoySocketLocalPosition = new Vector3(-0.03f, 0.08f, 0.04f);
        [SerializeField] Vector3 runtimeVexaSocketLocalPosition = new Vector3(-0.06f, -0.02f, -0.05f);
        [SerializeField, Min(0.001f)] float runtimeBallLocalScale = 0.26f;
        [SerializeField, Min(0.05f)] float visibleBallDiameter = 0.143f;

        GameObject heldBall;

        public PlayerVisualSwitcher VisualSwitcher => visualSwitcher;
        public GameObject BallPrefab => ballPrefab;
        public GameObject HeldBall => heldBall;
        public bool HasHeldBall => heldBall != null;
        public bool IsConfigured => ballPrefab != null;
        public int BindingCount => bindings?.Length ?? 0;
        public string LastFailureReason { get; private set; } = string.Empty;

        void Reset()
        {
            visualSwitcher = GetComponent<PlayerVisualSwitcher>();
        }

        void Awake()
        {
            if (visualSwitcher == null)
            {
                visualSwitcher = GetComponent<PlayerVisualSwitcher>();
            }
        }

        void OnEnable()
        {
            if (visualSwitcher != null)
            {
                visualSwitcher.ActiveVisualChanged += HandleActiveVisualChanged;
            }
        }

        void OnDisable()
        {
            if (visualSwitcher != null)
            {
                visualSwitcher.ActiveVisualChanged -= HandleActiveVisualChanged;
            }
        }

        public bool TryGetActiveBinding(out VisualBinding binding)
        {
            binding = null;
            string activeName = visualSwitcher != null
                ? visualSwitcher.ActiveVisualName
                : gameObject.name;
            int bindingCount = bindings?.Length ?? 0;
            for (int i = 0; i < bindingCount; i++)
            {
                VisualBinding candidate = bindings[i];
                if (candidate == null || candidate.Socket == null)
                {
                    continue;
                }

                if (string.Equals(candidate.VisualName, activeName, StringComparison.OrdinalIgnoreCase))
                {
                    binding = candidate;
                    return true;
                }
            }

            if (TryCreateRuntimeBinding(activeName, out binding))
            {
                return true;
            }

            LastFailureReason = $"Không tìm thấy xương bàn tay phải cho model '{activeName}'.";
            return false;
        }

        public bool ShowBallInActiveHand()
        {
            LastFailureReason = string.Empty;
            if (ballPrefab == null)
            {
                LastFailureReason = "Chưa gán prefab BallGreenClose.";
                return false;
            }

            if (!TryGetActiveBinding(out VisualBinding binding))
            {
                return false;
            }

            if (heldBall == null)
            {
                heldBall = Instantiate(ballPrefab, binding.Socket, false);
                heldBall.name = "BallGreenClose_Held";
            }
            else
            {
                heldBall.transform.SetParent(binding.Socket, false);
                heldBall.SetActive(true);
            }

            ApplyBinding(heldBall.transform, binding);
            if (PrepareHeldBall(heldBall))
            {
                return true;
            }

            DestroyHeldBall();
            return false;
        }

        public bool AttachBallToActiveHand(GameObject ball)
        {
            if (ball == null || !TryGetActiveBinding(out VisualBinding binding))
            {
                return false;
            }

            if (heldBall != null && heldBall != ball)
            {
                DestroyHeldBall();
            }

            heldBall = ball;
            heldBall.transform.SetParent(binding.Socket, false);
            ApplyBinding(heldBall.transform, binding);
            heldBall.SetActive(true);
            if (PrepareHeldBall(heldBall))
            {
                return true;
            }

            DestroyHeldBall();
            return false;
        }

        public GameObject DetachHeldBall(Transform newParent = null)
        {
            GameObject ball = heldBall;
            heldBall = null;
            if (ball != null)
            {
                ball.transform.SetParent(newParent, true);
            }

            return ball;
        }

        public void DestroyHeldBall()
        {
            if (heldBall == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(heldBall);
            }
            else
            {
                DestroyImmediate(heldBall);
            }

            heldBall = null;
        }

        public void Configure(PlayerVisualSwitcher switcher, GameObject prefab, VisualBinding[] visualBindings)
        {
            visualSwitcher = switcher;
            ballPrefab = prefab;
            bindings = visualBindings ?? Array.Empty<VisualBinding>();
        }

        void HandleActiveVisualChanged(int _, PlayerVisualSwitcher.VisualProfile __)
        {
            if (heldBall != null)
            {
                ShowBallInActiveHand();
            }
        }

        bool TryCreateRuntimeBinding(string visualName, out VisualBinding binding)
        {
            binding = null;
            if (!Application.isPlaying)
            {
                return false;
            }

            Transform visualRoot = transform;
            Animator animator = GetComponentInChildren<Animator>(true);
            if (visualSwitcher != null)
            {
                visualRoot = visualSwitcher.ActiveVisualRoot != null
                    ? visualSwitcher.ActiveVisualRoot.transform
                    : transform;
                animator = visualSwitcher.ActiveVisualAnimator;
            }

            Transform hand = ResolveRightHand(visualRoot, animator, visualName);
            if (hand == null)
            {
                return false;
            }

            Transform socket = hand.Find("BallSocket_PetSummon");
            if (socket == null)
            {
                GameObject socketObject = new GameObject("BallSocket_PetSummon");
                socket = socketObject.transform;
                socket.SetParent(hand, false);
                socket.localPosition = string.Equals(visualName, "Boy", StringComparison.OrdinalIgnoreCase)
                    ? runtimeBoySocketLocalPosition
                    : runtimeVexaSocketLocalPosition;
            }

            string bindingName = string.IsNullOrWhiteSpace(visualName) ? visualRoot.name : visualName;
            binding = new VisualBinding(bindingName, socket, Vector3.zero, Vector3.zero,
                runtimeBallLocalScale);

            int oldLength = bindings?.Length ?? 0;
            VisualBinding[] updatedBindings = new VisualBinding[oldLength + 1];
            if (oldLength > 0)
            {
                Array.Copy(bindings, updatedBindings, oldLength);
            }

            updatedBindings[oldLength] = binding;
            bindings = updatedBindings;
            return true;
        }

        static Transform ResolveRightHand(Transform visualRoot, Animator animator, string visualName)
        {
            if (animator != null && animator.isHuman)
            {
                Transform humanoidHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (humanoidHand != null)
                {
                    return humanoidHand;
                }
            }

            string primaryName = string.Equals(visualName, "Boy", StringComparison.OrdinalIgnoreCase)
                ? "RightHand"
                : "Base HumanRPalm";
            Transform hand = FindDescendant(visualRoot, primaryName);
            if (hand != null)
            {
                return hand;
            }

            hand = FindDescendant(visualRoot, "RightHand");
            return hand != null ? hand : FindDescendant(visualRoot, "Base HumanRPalm");
        }

        static Transform FindDescendant(Transform root, string exactName)
        {
            Transform[] candidates = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (string.Equals(candidates[i].name, exactName, StringComparison.Ordinal))
                {
                    return candidates[i];
                }
            }

            return null;
        }

        bool PrepareHeldBall(GameObject ball)
        {
            if (ball == null)
            {
                LastFailureReason = "Không có instance BallGreenClose để gắn vào tay.";
                return false;
            }

            PetSummonBallProjectile projectile = ball.GetComponent<PetSummonBallProjectile>();
            if (projectile != null)
            {
                projectile.PrepareForHand();
            }
            else
            {
                Rigidbody body = ball.GetComponent<Rigidbody>();
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

                Collider[] colliders = ball.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled = false;
                    }
                }
            }

            Renderer[] renderers = ball.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                LastFailureReason = "Prefab BallGreenClose không có Renderer.";
                return false;
            }

            Bounds visualBounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = true;
                    if (!hasBounds)
                    {
                        visualBounds = renderers[i].bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        visualBounds.Encapsulate(renderers[i].bounds);
                    }
                }
            }

            float diameter = Mathf.Max(visualBounds.size.x,
                visualBounds.size.y, visualBounds.size.z);
            if (!hasBounds || diameter < 0.0001f)
            {
                LastFailureReason = "Mesh BallGreenClose có kích thước hiển thị bằng 0.";
                return false;
            }

            // Model import scale differs between source FBX files. Keep the visible ball
            // hand-sized regardless of that scale, and match the physics sphere to it.
            float targetDiameter = Mathf.Max(0.05f, visibleBallDiameter);
            float scaleFactor = targetDiameter / diameter;
            ball.transform.localScale *= scaleFactor;
            SphereCollider sphere = ball.GetComponent<SphereCollider>();
            if (sphere != null)
            {
                float largestWorldScale = Mathf.Max(
                    Mathf.Abs(ball.transform.lossyScale.x),
                    Mathf.Abs(ball.transform.lossyScale.y),
                    Mathf.Abs(ball.transform.lossyScale.z));
                sphere.radius = targetDiameter * 0.5f / Mathf.Max(0.0001f, largestWorldScale);
            }

            return true;
        }

        void ApplyBinding(Transform ballTransform, VisualBinding binding)
        {
            // The hand socket owns alignment. Parenting alone keeps the ball in the
            // same palm position through locomotion, Throw, Pickup and recall.
            ballTransform.localScale = Vector3.one * binding.BallLocalScale;
            ballTransform.localPosition = binding.BallLocalPosition;
            ballTransform.localRotation = binding.BallLocalRotation;
        }
    }
}
