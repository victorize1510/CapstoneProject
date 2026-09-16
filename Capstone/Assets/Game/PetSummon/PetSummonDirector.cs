using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.PetSummon
{
    [DisallowMultipleComponent]
    public sealed class PetSummonDirector : MonoBehaviour
    {
        public enum SequenceState
        {
            Idle,
            Throw,
            Flight,
            Bounce,
            Hover,
            Release,
            Recall,
            Complete
        }

        [Header("References")]
        [SerializeField] BasicPlayerMovement movement;
        [SerializeField] PlayerVisualSwitcher visualSwitcher;
        [SerializeField] PetSummonHandSocket handSocket;
        [SerializeField] PetSummonReleaseEffect releaseEffect;
        [SerializeField] PetCommandInput commandInput;
        [SerializeField] Camera aimCamera;

        [Header("Landing")]
        [SerializeField, Min(0.5f)] float throwDistance = 3.5f;
        [SerializeField, Min(0.25f)] float minimumThrowDistance = 2.5f;
        [SerializeField, Min(0.1f)] float landingSearchRadius = 1.25f;
        [SerializeField, Min(0.25f)] float groundProbeHeight = 2.5f;
        [SerializeField, Min(0.5f)] float groundProbeDistance = 6f;
        [SerializeField, Min(0.01f)] float obstacleProbeRadius = 0.12f;
        [SerializeField] LayerMask groundLayers = ~0;
        [SerializeField] LayerMask obstacleLayers = ~0;
        [SerializeField] int navMeshAreaMask = NavMesh.AllAreas;
        [SerializeField] bool requireNavMesh = false;

        [Header("Timing")]
        [SerializeField, Range(0.05f, 0.95f)] float releaseNormalizedTime = 0.45f;
        [SerializeField, Min(0.25f)] float throwStartTimeout = 3f;
        [SerializeField, Min(0.05f)] float flightDuration = 0.58f;
        [SerializeField, Min(0.05f)] float bounceDuration = 1.1f;
        [SerializeField, Min(0f)] float bounceHeight = 0.24f;
        [SerializeField, Range(0f, 1f)] float bounceHorizontalDamping = 0.35f;
        [SerializeField, Min(0.05f)] float hoverRiseDuration = 0.22f;
        [SerializeField, Min(0f)] float hoverHeight = 0.45f;
        [SerializeField, Min(0f)] float hoverHoldDuration = 0.5f;
        [SerializeField, Min(0f)] float spinDegreesPerSecond = 540f;
        [SerializeField, Range(0.05f, 0.95f)] float recallEffectNormalizedTime = 0.28f;
        [SerializeField, Min(0.25f)] float pickupStartTimeout = 3f;
        [SerializeField, Min(0.25f)] float pickupCompletionTimeout = 3f;

        readonly RaycastHit[] groundHits = new RaycastHit[12];
        readonly RaycastHit[] obstacleHits = new RaycastHit[16];
        readonly Collider[] clearanceHits = new Collider[16];

        Coroutine sequenceRoutine;
        bool sequenceRunning;
        GameObject travellingBall;
        int pendingSlotIndex = -1;
        PetController recallingPet;
        bool playerLockedBySequence;
        float nextHeldBallRetryTime;

        public SequenceState State { get; private set; } = SequenceState.Idle;
        public bool IsSequenceRunning => sequenceRunning;
        public int PendingSlotIndex => pendingSlotIndex;
        public Vector3 LastHoverPosition { get; private set; }
        public string LastFailureReason { get; private set; } = string.Empty;
        public PetSummonReleaseEffect ReleaseEffect => releaseEffect;

        public event Action<int, Vector3> ReleaseReached;
        public event Action<int, bool> SequenceFinished;

        void Reset()
        {
            ResolveReferences();
        }

        void Awake()
        {
            ResolveReferences();
        }

        IEnumerator Start()
        {
            // PlayerSaveController restores the saved summoned state one frame after Start.
            yield return null;
            yield return null;
            if (handSocket != null && (commandInput == null || commandInput.activePet == null || !commandInput.activePet.IsSummoned))
            {
                if (!handSocket.ShowBallInActiveHand())
                {
                    Debug.LogWarning("Pet summon cannot show the held ball: "
                        + handSocket.LastFailureReason, this);
                }
            }
        }

        void LateUpdate()
        {
            if (sequenceRunning || handSocket == null)
            {
                return;
            }

            bool petIsOut = commandInput != null && commandInput.activePet != null
                && commandInput.activePet.IsSummoned;
            if (petIsOut)
            {
                if (handSocket.HasHeldBall)
                {
                    handSocket.DestroyHeldBall();
                }
                return;
            }

            if (!handSocket.HasHeldBall && Time.unscaledTime >= nextHeldBallRetryTime)
            {
                nextHeldBallRetryTime = Time.unscaledTime + 2f;
                if (!handSocket.ShowBallInActiveHand())
                {
                    Debug.LogWarning("Pet summon cannot show the held ball: "
                        + handSocket.LastFailureReason, this);
                }
            }
        }

        void OnDisable()
        {
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }

            sequenceRunning = false;
            if (travellingBall != null)
            {
                Destroy(travellingBall);
                travellingBall = null;
            }

            releaseEffect?.Cancel();
            recallingPet?.SetRecallLocked(false);
            if (recallingPet != null && recallingPet.IsSummoned)
            {
                handSocket?.DestroyHeldBall();
            }

            recallingPet = null;
            UnlockPlayer();
            pendingSlotIndex = -1;
            State = SequenceState.Idle;
        }

        public void Configure(BasicPlayerMovement playerMovement, PlayerVisualSwitcher switcher,
            PetSummonHandSocket socket)
        {
            movement = playerMovement;
            visualSwitcher = switcher;
            handSocket = socket;
        }

        public void ConfigureReleaseEffect(PetSummonReleaseEffect effect)
        {
            releaseEffect = effect;
        }

        public bool RequestSummonSlot(int slotIndex)
        {
            ResolveReferences();
            if (IsSequenceRunning)
            {
                return RejectSummon("Một thao tác gọi/thu pet khác đang chạy.");
            }

            if (!TryGetPetInSlot(slotIndex, out PetController selectedPet))
            {
                return RejectSummon($"Slot {slotIndex + 1} không có pet.");
            }

            if (selectedPet.IsSummoned)
            {
                return RejectSummon($"{selectedPet.name} đã được gọi ra.");
            }

            if (movement == null)
            {
                return RejectSummon("Player thiếu BasicPlayerMovement để chạy animation Throw.");
            }

            if (handSocket == null
                || (!handSocket.HasHeldBall && !handSocket.ShowBallInActiveHand()))
            {
                string socketReason = handSocket != null ? handSocket.LastFailureReason : "Thiếu PetSummonHandSocket.";
                return RejectSummon("Không thể chuẩn bị bóng trong tay. " + socketReason);
            }

            if (!TryResolveLanding(out LandingPlan landing, out string landingFailure))
            {
                return RejectSummon("Không tìm được điểm đáp an toàn cách player tối thiểu "
                    + minimumThrowDistance.ToString("0.0") + " m. " + landingFailure);
            }

            LastFailureReason = string.Empty;
            pendingSlotIndex = slotIndex;
            sequenceRunning = true;
            sequenceRoutine = StartCoroutine(PlayThrowSequence(selectedPet, landing));
            return true;
        }

        public bool RequestRecall()
        {
            ResolveReferences();
            if (IsSequenceRunning)
            {
                return false;
            }

            if (commandInput != null && commandInput.activePet != null && commandInput.activePet.IsSummoned)
            {
                LastFailureReason = string.Empty;
                recallingPet = commandInput.activePet;
                pendingSlotIndex = commandInput.petSlots != null
                    ? Array.IndexOf(commandInput.petSlots, recallingPet)
                    : -1;
                sequenceRunning = true;
                sequenceRoutine = StartCoroutine(PlayRecallSequence(recallingPet));
                return true;
            }

            RestoreBallToHand();
            return false;
        }

        IEnumerator PlayRecallSequence(PetController pet)
        {
            bool succeeded = false;
            bool petWithdrawn = false;
            string failureReason = string.Empty;
            LockPlayer();
            yield return null;

            try
            {
                if (pet == null || !pet.IsSummoned || movement == null || handSocket == null)
                {
                    failureReason = "Thiếu pet, animation controller hoặc socket tay khi thu pet.";
                    yield break;
                }

                pet.SetRecallLocked(true);

                Vector3 direction = pet.transform.position - transform.position;
                direction.y = 0f;
                FaceDirection(direction);

                State = SequenceState.Recall;
                float deadline = Time.unscaledTime + pickupStartTimeout;
                bool pickupStarted = false;
                while (!pickupStarted && Time.unscaledTime < deadline)
                {
                    pickupStarted = movement.TryPlayScriptedPickup();
                    if (!pickupStarted)
                    {
                        yield return null;
                    }
                }

                if (!pickupStarted)
                {
                    failureReason = "Animation Pickup không thể bắt đầu sau khi chờ player chạm đất hoặc kết thúc động tác hiện tại.";
                    yield break;
                }

                deadline = Time.unscaledTime + pickupStartTimeout;
                while (Time.unscaledTime < deadline
                    && movement.ActiveScriptedAction != BasicPlayerMovement.ScriptedAction.PickingUp)
                {
                    yield return null;
                }

                if (movement.ActiveScriptedAction != BasicPlayerMovement.ScriptedAction.PickingUp)
                {
                    failureReason = "Animation Pickup không vào được state trong thời gian chờ.";
                    yield break;
                }

                float pickupDeadline = Time.unscaledTime + pickupCompletionTimeout;
                while (Time.unscaledTime < pickupDeadline
                    && movement.IsScriptedActionPlaying
                    && movement.ActiveScriptedAction == BasicPlayerMovement.ScriptedAction.PickingUp
                    && movement.ScriptedActionNormalizedTime < recallEffectNormalizedTime)
                {
                    yield return null;
                }

                if (!handSocket.HasHeldBall && !handSocket.ShowBallInActiveHand())
                {
                    failureReason = "Không thể tạo bóng trong tay lúc thu pet. "
                        + handSocket.LastFailureReason;
                    yield break;
                }

                Action hidePet = () =>
                {
                    if (pet != null && pet.IsSummoned)
                    {
                        pet.Withdraw();
                    }

                    petWithdrawn = pet == null || !pet.IsSummoned;
                };

                if (releaseEffect != null && handSocket.HeldBall != null)
                {
                    yield return releaseEffect.PlayRecall(pet, handSocket.HeldBall.transform, hidePet);
                }
                else
                {
                    hidePet();
                }

                if (!petWithdrawn)
                {
                    failureReason = "Pet không rút về sau hiệu ứng recall.";
                    yield break;
                }

                while (Time.unscaledTime < pickupDeadline
                    && movement.IsScriptedActionPlaying
                    && movement.ActiveScriptedAction == BasicPlayerMovement.ScriptedAction.PickingUp)
                {
                    yield return null;
                }

                State = SequenceState.Complete;
                succeeded = true;
                yield return null;
            }
            finally
            {
                releaseEffect?.Cancel();
                pet?.SetRecallLocked(false);
                if (!petWithdrawn && pet != null && pet.IsSummoned)
                {
                    handSocket?.DestroyHeldBall();
                }
                else if (petWithdrawn && handSocket != null && !handSocket.HasHeldBall)
                {
                    handSocket.ShowBallInActiveHand();
                }

                int completedSlot = pendingSlotIndex;
                pendingSlotIndex = -1;
                recallingPet = null;
                UnlockPlayer();
                sequenceRunning = false;
                sequenceRoutine = null;
                State = SequenceState.Idle;
                if (!succeeded)
                {
                    LastFailureReason = string.IsNullOrWhiteSpace(failureReason)
                        ? "Sequence thu pet đã dừng trước khi hoàn tất."
                        : failureReason;
                    Debug.LogWarning("Pet recall failed: " + LastFailureReason, this);
                }
                SequenceFinished?.Invoke(completedSlot, succeeded);
            }
        }

        IEnumerator PlayThrowSequence(PetController selectedPet, LandingPlan landing)
        {
            bool succeeded = false;
            bool petReleased = false;
            string failureReason = string.Empty;
            Vector3 actualGroundPoint = default;
            Vector3 actualDirection = landing.Direction;
            LockPlayer();
            yield return null;

            try
            {
                FaceDirection(landing.Direction);
                if (handSocket == null
                    || (!handSocket.HasHeldBall && !handSocket.ShowBallInActiveHand()))
                {
                    failureReason = "Không thể tạo bóng trong tay trước khi ném. "
                        + (handSocket != null ? handSocket.LastFailureReason : string.Empty);
                    yield break;
                }

                if (movement == null)
                {
                    failureReason = "Player thiếu BasicPlayerMovement để chạy animation Throw.";
                    yield break;
                }

                State = SequenceState.Throw;
                float deadline = Time.unscaledTime + throwStartTimeout;
                bool throwStarted = false;
                while (!throwStarted && Time.unscaledTime < deadline)
                {
                    throwStarted = movement.TryPlayScriptedThrow();
                    if (!throwStarted)
                    {
                        yield return null;
                    }
                }

                if (!throwStarted)
                {
                    failureReason = "Animation Throw không thể bắt đầu sau khi chờ player chạm đất hoặc kết thúc động tác hiện tại.";
                    yield break;
                }

                deadline = Time.unscaledTime + throwStartTimeout;
                while (Time.unscaledTime < deadline
                    && movement.ActiveScriptedAction != BasicPlayerMovement.ScriptedAction.Throw)
                {
                    yield return null;
                }

                if (movement.ActiveScriptedAction != BasicPlayerMovement.ScriptedAction.Throw)
                {
                    failureReason = "Animation Throw không vào được state Throw trước khi hết thời gian chờ.";
                    yield break;
                }

                while (movement.IsScriptedActionPlaying
                    && movement.ActiveScriptedAction == BasicPlayerMovement.ScriptedAction.Throw
                    && movement.ScriptedActionNormalizedTime < releaseNormalizedTime)
                {
                    yield return null;
                }

                travellingBall = handSocket.DetachHeldBall();
                if (travellingBall == null)
                {
                    failureReason = "Bóng biến mất trước thời điểm rời tay.";
                    yield break;
                }

                PetSummonBallProjectile projectile = travellingBall.GetComponent<PetSummonBallProjectile>();
                if (projectile == null)
                {
                    projectile = travellingBall.AddComponent<PetSummonBallProjectile>();
                }

                float ballRadius = GetBallRadius(travellingBall);
                if (!projectile.Launch(landing.GroundPoint, ballRadius, flightDuration,
                    bounceHeight, bounceHorizontalDamping, bounceDuration, groundLayers,
                    landing.GroundCollider, transform))
                {
                    failureReason = projectile.FailureReason;
                    yield break;
                }

                State = SequenceState.Flight;
                float motionDeadline = Time.time + flightDuration + bounceDuration + 1f;
                while (!projectile.IsFinished && Time.time < motionDeadline)
                {
                    State = projectile.Phase == PetSummonBallProjectile.MotionPhase.Bounce
                        ? SequenceState.Bounce
                        : SequenceState.Flight;
                    yield return new WaitForFixedUpdate();
                }

                if (!projectile.Succeeded)
                {
                    failureReason = string.IsNullOrWhiteSpace(projectile.FailureReason)
                        ? "Bóng không hoàn tất va chạm và lần nảy vật lý."
                        : projectile.FailureReason;
                    yield break;
                }

                if (!TryResolveActualLanding(projectile.GroundPoint, out actualGroundPoint))
                {
                    failureReason = requireNavMesh
                        ? "Điểm bóng đáp không có mặt đất/NavMesh hợp lệ để spawn pet."
                        : "Điểm bóng đáp không có nền an toàn cách player đủ xa để spawn pet.";
                    yield break;
                }

                Vector3 directionFromPlayer = actualGroundPoint - transform.position;
                directionFromPlayer.y = 0f;
                if (directionFromPlayer.sqrMagnitude > 0.001f)
                {
                    actualDirection = directionFromPlayer.normalized;
                }

                State = SequenceState.Hover;
                projectile.FreezeForHover();
                Vector3 hoverPosition = actualGroundPoint + Vector3.up * (ballRadius + hoverHeight);
                yield return AnimateLinear(travellingBall.transform.position, hoverPosition,
                    hoverRiseDuration);
                LastHoverPosition = hoverPosition;

                float hoverUntil = Time.time + hoverHoldDuration;
                while (Time.time < hoverUntil)
                {
                    SpinBall();
                    yield return null;
                }

                State = SequenceState.Release;
                ReleaseReached?.Invoke(pendingSlotIndex, LastHoverPosition);

                Action releasePet = () =>
                {
                    petReleased = ReleasePet(selectedPet, actualGroundPoint, actualDirection);
                };

                if (releaseEffect != null)
                {
                    yield return releaseEffect.PlaySummon(
                        selectedPet,
                        travellingBall.transform,
                        actualGroundPoint,
                        releasePet);
                }
                else
                {
                    releasePet();
                }

                if (!petReleased)
                {
                    failureReason = "Pet không chuyển sang trạng thái summoned sau hiệu ứng thả.";
                    yield break;
                }

                if (travellingBall != null)
                {
                    Destroy(travellingBall);
                    travellingBall = null;
                }

                State = SequenceState.Complete;
                succeeded = true;
                yield return null;
            }
            finally
            {
                if (!succeeded && !petReleased)
                {
                    RestoreBallToHand();
                }
                else if (!succeeded && travellingBall != null)
                {
                    Destroy(travellingBall);
                    travellingBall = null;
                }

                int completedSlot = pendingSlotIndex;
                pendingSlotIndex = -1;
                UnlockPlayer();
                sequenceRunning = false;
                sequenceRoutine = null;
                State = SequenceState.Idle;
                if (!succeeded)
                {
                    LastFailureReason = string.IsNullOrWhiteSpace(failureReason)
                        ? "Sequence gọi pet đã dừng trước khi hoàn tất."
                        : failureReason;
                    Debug.LogWarning("Pet summon failed: " + LastFailureReason, this);
                }
                SequenceFinished?.Invoke(completedSlot, succeeded);
            }
        }

        IEnumerator AnimateLinear(Vector3 start, Vector3 end, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && travellingBall != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                travellingBall.transform.position = Vector3.Lerp(start, end, Smooth01(t));
                SpinBall();
                yield return null;
            }

            if (travellingBall != null)
            {
                travellingBall.transform.position = end;
            }
        }

        bool TryResolveLanding(out LandingPlan landing, out string failureDetail)
        {
            landing = default;
            failureDetail = string.Empty;
            Vector3 forward = GetAimDirection();
            float farDistance = Mathf.Max(minimumThrowDistance, throwDistance);
            float middleDistance = Mathf.Lerp(farDistance, minimumThrowDistance, 0.5f);
            float[] distances = { farDistance, middleDistance, minimumThrowDistance };
            float[] angles = { 0f, -25f, 25f, -45f, 45f };
            float candidateSearchRadius = Mathf.Clamp(landingSearchRadius, 0.35f, 0.85f);
            RaycastHit ownerGroundHit = default;
            bool hasOwnerGround = requireNavMesh
                && TryFindGround(transform.position, out ownerGroundHit);
            NavMeshHit ownerNavHit = default;
            bool hasOwnerOnNavMesh = hasOwnerGround
                && NavMesh.SamplePosition(ownerGroundHit.point, out ownerNavHit,
                    Mathf.Max(1f, candidateSearchRadius), navMeshAreaMask);
            int missingGround = 0;
            int missingNavMesh = 0;
            int tooNear = 0;
            int blockedNavMesh = 0;
            int heightMismatch = 0;
            int blockedPhysics = 0;

            for (int distanceIndex = 0; distanceIndex < distances.Length; distanceIndex++)
            {
                for (int angleIndex = 0; angleIndex < angles.Length; angleIndex++)
                {
                    Vector3 direction = Quaternion.Euler(0f, angles[angleIndex], 0f) * forward;
                    Vector3 candidate = transform.position + direction * distances[distanceIndex];

                    if (!TryFindGround(candidate, out RaycastHit groundHit))
                    {
                        missingGround++;
                        continue;
                    }

                    NavMeshHit navHit = default;
                    bool hasNavPoint = requireNavMesh
                        && NavMesh.SamplePosition(groundHit.point, out navHit,
                            candidateSearchRadius, navMeshAreaMask);
                    if (requireNavMesh && !hasNavPoint)
                    {
                        missingNavMesh++;
                        continue;
                    }

                    Vector3 horizontalDelta = (hasNavPoint ? navHit.position : groundHit.point)
                        - transform.position;
                    horizontalDelta.y = 0f;
                    if (horizontalDelta.magnitude < minimumThrowDistance - 0.1f)
                    {
                        tooNear++;
                        continue;
                    }

                    if (hasOwnerOnNavMesh && hasNavPoint
                        && NavMesh.Raycast(ownerNavHit.position, navHit.position, out _, navMeshAreaMask))
                    {
                        blockedNavMesh++;
                        continue;
                    }

                    if (hasNavPoint && Mathf.Abs(groundHit.point.y - navHit.position.y) > 0.35f)
                    {
                        heightMismatch++;
                        continue;
                    }

                    if (!HasLandingClearance(groundHit.point, groundHit.collider)
                        || HasHorizontalObstacle(groundHit.point, groundHit.collider))
                    {
                        blockedPhysics++;
                        continue;
                    }

                    landing = new LandingPlan(groundHit.point, direction.normalized,
                        groundHit.collider);
                    return true;
                }
            }

            failureDetail = requireNavMesh
                ? $"Kiểm tra 15 vị trí: thiếu collider nền {missingGround}, "
                    + $"ngoài NavMesh {missingNavMesh}, quá gần {tooNear}, "
                    + $"đường NavMesh bị chặn {blockedNavMesh}, lệch độ cao {heightMismatch}, "
                    + $"vướng vật cản {blockedPhysics}."
                : $"Kiểm tra 15 vị trí: thiếu collider nền {missingGround}, "
                    + $"quá gần {tooNear}, vướng vật cản {blockedPhysics}.";
            return false;
        }

        bool TryResolveActualLanding(Vector3 impactPoint, out Vector3 groundPoint)
        {
            groundPoint = default;
            Vector3 horizontalDelta = impactPoint - transform.position;
            horizontalDelta.y = 0f;
            if (horizontalDelta.magnitude < minimumThrowDistance - 0.15f)
            {
                return false;
            }

            if (!TryFindGround(impactPoint, out RaycastHit groundHit)
                || !HasLandingClearance(groundHit.point, groundHit.collider))
            {
                return false;
            }

            NavMeshHit navHit = default;
            bool hasNavPoint = requireNavMesh
                && NavMesh.SamplePosition(groundHit.point, out navHit,
                    Mathf.Clamp(landingSearchRadius, 0.35f, 0.85f), navMeshAreaMask);
            if (requireNavMesh && !hasNavPoint)
            {
                return false;
            }

            if (hasNavPoint && Mathf.Abs(groundHit.point.y - navHit.position.y) > 0.35f)
            {
                return false;
            }

            groundPoint = hasNavPoint ? navHit.position : groundHit.point;
            return true;
        }

        bool TryFindGround(Vector3 point, out RaycastHit bestHit)
        {
            Vector3 origin = point + Vector3.up * groundProbeHeight;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
                groundProbeHeight + groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore);
            int bestIndex = -1;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (ShouldIgnore(hit.collider) || hit.normal.y < 0.45f || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestIndex = i;
                bestDistance = hit.distance;
            }

            bestHit = bestIndex >= 0 ? groundHits[bestIndex] : default;
            return bestIndex >= 0;
        }

        bool HasLandingClearance(Vector3 groundPoint, Collider groundCollider)
        {
            float radius = Mathf.Max(0.03f, obstacleProbeRadius);
            Vector3 center = groundPoint + Vector3.up * (radius + 0.04f);
            int count = Physics.OverlapSphereNonAlloc(center, radius, clearanceHits,
                obstacleLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider candidate = clearanceHits[i];
                if (candidate != null && candidate != groundCollider && !ShouldIgnore(candidate)
                    && !IsSupportingSurface(candidate, groundPoint)
                    && candidate.bounds.max.y > groundPoint.y + 0.25f)
                {
                    return false;
                }
            }

            return true;
        }

        bool HasHorizontalObstacle(Vector3 groundPoint, Collider groundCollider)
        {
            Vector3 start = transform.position + Vector3.up;
            Vector3 end = groundPoint + Vector3.up;
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
            {
                return false;
            }

            int count = Physics.SphereCastNonAlloc(start, obstacleProbeRadius, delta / distance,
                obstacleHits, distance, obstacleLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                RaycastHit obstacleHit = obstacleHits[i];
                Collider candidate = obstacleHit.collider;
                if (candidate != null && candidate != groundCollider && !ShouldIgnore(candidate)
                    && !IsSupportingSurface(candidate, groundPoint)
                    && obstacleHit.point.y > groundPoint.y + 0.25f
                    && !(IsGroundLayer(candidate.gameObject.layer) && obstacleHit.normal.y >= 0.45f))
                {
                    return true;
                }
            }

            return false;
        }

        bool IsSupportingSurface(Collider candidate, Vector3 groundPoint)
        {
            float probeHeight = Mathf.Max(0.5f, groundProbeHeight);
            Ray ray = new Ray(groundPoint + Vector3.up * probeHeight, Vector3.down);
            return candidate.Raycast(ray, out RaycastHit hit, probeHeight + 0.5f)
                && hit.normal.y >= 0.45f
                && Mathf.Abs(hit.point.y - groundPoint.y) <= 0.2f;
        }

        bool IsGroundLayer(int layer)
        {
            return (groundLayers.value & (1 << layer)) != 0;
        }

        bool TryGetPetInSlot(int slotIndex, out PetController pet)
        {
            pet = null;
            return commandInput != null && commandInput.TryGetPetInSlot(slotIndex, out pet);
        }

        bool RejectSummon(string reason)
        {
            LastFailureReason = reason ?? string.Empty;
            Debug.LogWarning("Pet summon rejected: " + LastFailureReason, this);
            return false;
        }

        bool ReleasePet(PetController pet, Vector3 spawnPoint, Vector3 facingDirection)
        {
            if (pet == null || commandInput == null)
            {
                return false;
            }

            PetController previousPet = commandInput.activePet;
            if (previousPet != null && previousPet != pet && previousPet.IsSummoned)
            {
                previousPet.Withdraw();
            }

            pet.AssignOwner(transform);
            commandInput.SetActivePet(pet);
            pet.SummonAt(spawnPoint, facingDirection);
            return pet.IsSummoned;
        }

        bool ShouldIgnore(Collider candidate)
        {
            if (candidate == null)
            {
                return true;
            }

            Transform candidateTransform = candidate.transform;
            if (candidateTransform == transform || candidateTransform.IsChildOf(transform))
            {
                return true;
            }

            return candidate.GetComponentInParent<PetController>() != null
                || candidate.GetComponentInParent<DummyEnemy>() != null;
        }

        Vector3 GetAimDirection()
        {
            Camera cameraToUse = aimCamera != null ? aimCamera : Camera.main;
            Vector3 direction = cameraToUse != null ? cameraToUse.transform.forward : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                direction = transform.forward;
                direction.y = 0f;
            }

            return direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        void FaceDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        void ResolveReferences()
        {
            movement ??= GetComponent<BasicPlayerMovement>();
            visualSwitcher ??= GetComponent<PlayerVisualSwitcher>();
            handSocket ??= GetComponent<PetSummonHandSocket>();
            releaseEffect ??= GetComponent<PetSummonReleaseEffect>();
            if (releaseEffect == null && Application.isPlaying)
            {
                releaseEffect = gameObject.AddComponent<PetSummonReleaseEffect>();
            }
            commandInput ??= GetComponent<PetCommandInput>();
            aimCamera ??= Camera.main;
            if (movement != null)
            {
                groundLayers = movement.groundLayers;
            }
        }

        void LockPlayer()
        {
            if (playerLockedBySequence)
            {
                return;
            }

            playerLockedBySequence = true;
            movement?.SetGameplayInputLocked(true);
            visualSwitcher?.SetRuntimeSwitchLocked(true);
        }

        void UnlockPlayer()
        {
            if (!playerLockedBySequence)
            {
                return;
            }

            playerLockedBySequence = false;
            movement?.SetGameplayInputLocked(false);
            visualSwitcher?.SetRuntimeSwitchLocked(false);
        }

        void RestoreBallToHand()
        {
            if (handSocket == null)
            {
                if (travellingBall != null)
                {
                    Destroy(travellingBall);
                    travellingBall = null;
                }
                return;
            }

            if (travellingBall != null && handSocket.AttachBallToActiveHand(travellingBall))
            {
                travellingBall = null;
                return;
            }

            if (travellingBall != null)
            {
                Destroy(travellingBall);
                travellingBall = null;
            }

            if (!handSocket.HasHeldBall && !handSocket.ShowBallInActiveHand())
            {
                Debug.LogWarning("Pet summon cannot restore the held ball: "
                    + handSocket.LastFailureReason, this);
            }
        }

        void SpinBall()
        {
            if (travellingBall != null && spinDegreesPerSecond > 0f)
            {
                travellingBall.transform.Rotate(Vector3.right,
                    spinDegreesPerSecond * Time.deltaTime, Space.Self);
            }
        }

        static float GetBallRadius(GameObject ball)
        {
            Renderer[] renderers = ball.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 0.1f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Clamp(bounds.extents.y, 0.04f, 0.3f);
        }

        static float Smooth01(float value)
        {
            return value * value * (3f - 2f * value);
        }

        readonly struct LandingPlan
        {
            public readonly Vector3 GroundPoint;
            public readonly Vector3 Direction;
            public readonly Collider GroundCollider;

            public LandingPlan(Vector3 groundPoint, Vector3 direction, Collider groundCollider)
            {
                GroundPoint = groundPoint;
                Direction = direction;
                GroundCollider = groundCollider;
            }
        }
    }
}
