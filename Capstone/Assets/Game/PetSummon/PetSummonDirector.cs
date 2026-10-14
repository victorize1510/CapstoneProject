using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Capstone.Game.PetSummon
{
    [DisallowMultipleComponent]
    public sealed class PetSummonDirector : MonoBehaviour
    {
        static readonly float[] LandingAngles = { 0f, -25f, 25f, -45f, 45f };
        static readonly float[] LandingDistanceFactors = { 1f, 0.5f, 0f };
        static readonly WaitForFixedUpdate WaitForBallPhysics = new WaitForFixedUpdate();

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
        [SerializeField, Min(0.5f)] float throwDistance = 5f;
        [SerializeField, Min(0.25f)] float minimumThrowDistance = 3.5f;
        [SerializeField, Min(0.1f)] float petSpawnAheadDistance = 0.7f;
        [SerializeField, Min(0.1f)] float landingSearchRadius = 1.25f;
        [SerializeField, Min(0.25f)] float groundProbeHeight = 2.5f;
        [SerializeField, Min(0.5f)] float groundProbeDistance = 6f;
        [SerializeField, Min(0.01f)] float obstacleProbeRadius = 0.12f;
        [SerializeField] LayerMask groundLayers = ~0;
        [SerializeField] LayerMask obstacleLayers = ~0;
        [SerializeField] int navMeshAreaMask = NavMesh.AllAreas;
        [SerializeField] bool requireNavMesh = false;

        [Header("Timing")]
        [SerializeField, Min(1)] int releaseFrame = 32;
        [SerializeField, Min(0.25f)] float throwStartTimeout = 3f;
        [SerializeField, Min(0.1f)] float throwApexHeight = 1.2f;
        [SerializeField, Min(0.1f)] float ballMotionSpeed = 1f;
        [SerializeField, Min(0.05f)] float bounceDuration = 1.1f;
        [SerializeField, Min(0f)] float bounceHeight = 1.1f;
        [SerializeField, Range(0f, 1f)] float bounceHorizontalDamping = 0.35f;
        [SerializeField, Range(0.05f, 0.95f)] float recallEffectNormalizedTime = 0.28f;
        [SerializeField, Min(0.25f)] float pickupStartTimeout = 3f;
        [SerializeField, Min(0.25f)] float pickupCompletionTimeout = 3f;

        readonly RaycastHit[] groundHits = new RaycastHit[12];
        readonly RaycastHit[] obstacleHits = new RaycastHit[16];
        readonly Collider[] clearanceHits = new Collider[16];

        Coroutine sequenceRoutine;
        Coroutine queuedSwitchRoutine;
        bool sequenceRunning;
        int queuedSlotIndex = -1;
        GameObject travellingBall;
        int pendingSlotIndex = -1;
        PetController recallingPet;
        bool playerLockedBySequence;

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

        void OnDisable()
        {
            if (queuedSwitchRoutine != null)
            {
                StopCoroutine(queuedSwitchRoutine);
                queuedSwitchRoutine = null;
            }
            queuedSlotIndex = -1;
            if (sequenceRoutine != null)
            {
                StopCoroutine(sequenceRoutine);
                sequenceRoutine = null;
            }

            sequenceRunning = false;
            ClearSequenceBalls();

            releaseEffect?.Cancel();
            recallingPet?.SetRecallLocked(false);

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

        public bool RequestSummonSlot(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= 6 || !Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "summon:" + slotIndex)) return false;
            return RequestSummonSlotCore(slotIndex);
        }

        bool RequestSummonSlotCore(int slotIndex)
        {
            ResolveReferences();
            if (!TryGetPetInSlot(slotIndex, out PetController selectedPet))
            {
                return RejectSummon($"Slot {slotIndex + 1} không có pet.");
            }

            if (IsSequenceRunning)
            {
                if (State != SequenceState.Recall && pendingSlotIndex == slotIndex)
                {
                    queuedSlotIndex = -1;
                    return true;
                }
                return QueuePetSwitch(slotIndex);
            }

            if (selectedPet.IsSummoned)
            {
                return selectedPet == commandInput?.activePet
                    || RejectSummon($"{selectedPet.name} đã được gọi ra.");
            }

            if (commandInput?.activePet != null && commandInput.activePet.IsSummoned)
            {
                return QueuePetSwitch(slotIndex);
            }

            if (movement == null)
            {
                return RejectSummon("Player thiếu BasicPlayerMovement để chạy animation Throw.");
            }

            if (!movement.CanStartPetThrow)
            {
                return RejectSummon("Chỉ có thể ném bóng khi player đứng vững trên mặt đất.");
            }

            // A direct new request supersedes any older queued switch.
            queuedSlotIndex = -1;
            if (!TryResolveLanding(out LandingPlan landing, out string landingFailure))
            {
                return RejectSummon("Không tìm được điểm đáp an toàn cách player tối thiểu "
                    + minimumThrowDistance.ToString("0.0") + " m. " + landingFailure);
            }

            if (handSocket == null || !handSocket.ShowBallInActiveHand())
            {
                string socketReason = handSocket != null ? handSocket.LastFailureReason : "Thiếu PetSummonHandSocket.";
                return RejectSummon("Không thể chuẩn bị bóng trong tay. " + socketReason);
            }

            LastFailureReason = string.Empty;
            pendingSlotIndex = slotIndex;
            sequenceRunning = true;
            sequenceRoutine = StartCoroutine(PlayThrowSequence(selectedPet, landing));
            return true;
        }

        bool QueuePetSwitch(int slotIndex)
        {
            queuedSlotIndex = slotIndex;
            LastFailureReason = string.Empty;
            if (queuedSwitchRoutine == null)
                queuedSwitchRoutine = StartCoroutine(ProcessQueuedPetSwitch());
            return true;
        }

        IEnumerator ProcessQueuedPetSwitch()
        {
            yield return null;
            int recallAttempts = 0;
            int lastTargetSlot = -1;
            float nextLandingCheck = 0;
            while (queuedSlotIndex >= 0)
            {
                if (queuedSlotIndex != lastTargetSlot)
                {
                    lastTargetSlot = queuedSlotIndex;
                    recallAttempts = 0;
                    nextLandingCheck = 0;
                }
                if (!TryGetPetInSlot(queuedSlotIndex, out PetController target))
                {
                    queuedSlotIndex = -1;
                    break;
                }
                if (sequenceRunning) { yield return null; continue; }
                if (target.IsSummoned)
                {
                    queuedSlotIndex = -1;
                    break;
                }

                PetController current = commandInput != null ? commandInput.activePet : null;
                if (current != null && current.IsSummoned)
                {
                    if (recallAttempts >= 2)
                    {
                        RejectSummon("Không thể hoàn tất animation thu pet cũ để đổi slot.");
                        queuedSlotIndex = -1;
                        break;
                    }
                    if (!RequestRecallCore()) { yield return null; continue; }
                    recallAttempts++;
                    yield return null;
                    continue;
                }
                recallAttempts = 0;

                if (movement == null || handSocket == null)
                {
                    RejectSummon("Không thể đổi pet vì thiếu animation player hoặc bóng trong tay.");
                    queuedSlotIndex = -1;
                    break;
                }
                if (!movement.CanStartPetThrow || Time.unscaledTime < nextLandingCheck)
                {
                    yield return null;
                    continue;
                }
                // A blocked destination can remain blocked for seconds; avoid repeated multi-ray searches every frame.
                nextLandingCheck = Time.unscaledTime + .2f;
                if (!TryResolveLanding(out _, out _)) { yield return null; continue; }

                int nextSlot = queuedSlotIndex;
                queuedSlotIndex = -1;
                if (RequestSummonSlotCore(nextSlot)) break;
                queuedSlotIndex = nextSlot;
                yield return new WaitForSecondsRealtime(0.35f);
            }
            queuedSwitchRoutine = null;
        }

        public bool RequestRecall() {
            if (!Capstone.Game.UISystem.PlayerActionGate.TryAccept(this, "recall")) return false;
            return RequestRecallCore();
        }

        bool RequestRecallCore()
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

            var saveController = FindFirstObjectByType<Capstone.Game.SaveSystem.PlayerSaveController>();
            if (saveController != null && commandInput != null
                && saveController.CancelPendingSummonedRestore(commandInput.activePet))
            {
                ClearSequenceBalls();
                return true;
            }

            ClearSequenceBalls();
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

                handSocket.DestroyHeldBall();

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
                ClearSequenceBalls();

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

                float releaseNormalizedTime = 0f;
                float clipDeadline = Time.unscaledTime + 0.25f;
                while (!TryGetThrowReleaseTime(out releaseNormalizedTime)
                    && Time.unscaledTime < clipDeadline)
                {
                    yield return null;
                }

                if (releaseNormalizedTime <= 0f)
                {
                    failureReason = "Không tìm thấy clip Throw của model đang dùng để xác định khung thả bóng.";
                    yield break;
                }

                while (movement.IsScriptedActionPlaying
                    && movement.ActiveScriptedAction == BasicPlayerMovement.ScriptedAction.Throw
                    && (!TryGetThrowPlaybackTime(out float throwProgress)
                        || throwProgress < releaseNormalizedTime))
                {
                    yield return null;
                }

                if (!movement.IsGroundedForPetThrow
                    || movement.ActiveScriptedAction != BasicPlayerMovement.ScriptedAction.Throw
                    || !TryGetThrowPlaybackTime(out float releaseProgress)
                    || releaseProgress < releaseNormalizedTime)
                {
                    failureReason = "Player rời mặt đất hoặc animation Throw kết thúc trước khi thả bóng.";
                    yield break;
                }

                if (!TryFindGround(landing.GroundPoint, out RaycastHit currentGround)
                    || currentGround.collider != landing.GroundCollider
                    || HasTrajectoryObstacle(landing.GroundPoint, landing.GroundCollider))
                {
                    failureReason = "Đường bay hoặc điểm đáp đã bị chắn trước lúc bóng rời tay.";
                    yield break;
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
                if (!projectile.Launch(landing.GroundPoint, ballRadius, throwApexHeight,
                    bounceHeight, bounceHorizontalDamping, bounceDuration, groundLayers,
                    landing.GroundCollider, transform, ballMotionSpeed))
                {
                    failureReason = projectile.FailureReason;
                    yield break;
                }

                State = SequenceState.Flight;
                float motionDeadline = Time.time + projectile.ExpectedFlightDuration
                    + bounceDuration + 1f;
                while (!projectile.IsFinished && Time.time < motionDeadline)
                {
                    State = projectile.Phase == PetSummonBallProjectile.MotionPhase.Bounce
                        ? SequenceState.Bounce
                        : SequenceState.Flight;
                    yield return WaitForBallPhysics;
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

                if (!TryResolvePetSpawnPoint(actualGroundPoint, actualDirection,
                    out Vector3 petSpawnPoint))
                {
                    failureReason = "Phía trước bóng không có nền và khoảng trống an toàn cho pet.";
                    yield break;
                }

                // The projectile freezes itself at the physical bounce apex.
                LastHoverPosition = travellingBall.transform.position;
                State = SequenceState.Release;
                ReleaseReached?.Invoke(pendingSlotIndex, LastHoverPosition);

                Action releasePet = () =>
                {
                    petReleased = ReleasePet(selectedPet, petSpawnPoint, actualDirection);
                };

                if (releaseEffect != null)
                {
                    yield return releaseEffect.PlaySummon(
                        selectedPet,
                        petSpawnPoint,
                        travellingBall.transform,
                        releasePet,
                        actualGroundPoint);
                }
                else
                {
                    releasePet();
                    if (petReleased && selectedPet != null)
                    {
                        petReleased = selectedPet.CompleteSummonPresentation(petSpawnPoint);
                    }
                }

                if (!petReleased || selectedPet == null || !selectedPet.IsSummoned
                    || selectedPet.IsSummonPresentationPending)
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
                if (!succeeded && selectedPet != null && selectedPet.IsSummonPresentationPending)
                {
                    selectedPet.Withdraw();
                }

                ClearSequenceBalls();

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

        bool TryGetThrowReleaseTime(out float normalizedTime)
        {
            normalizedTime = 0f;
            Animator animator = visualSwitcher != null ? visualSwitcher.ActiveVisualAnimator : null;
            animator ??= movement != null ? movement.animator : null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimationClip throwClip = null;
            AnimatorClipInfo[] playingClips = animator.IsInTransition(0)
                ? animator.GetNextAnimatorClipInfo(0)
                : animator.GetCurrentAnimatorClipInfo(0);
            for (int i = 0; i < playingClips.Length; i++)
            {
                AnimationClip clip = playingClips[i].clip;
                if (clip != null && clip.name.IndexOf("Throw", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throwClip = clip;
                    break;
                }
            }

            if (throwClip == null && playingClips.Length > 0)
            {
                AnimatorStateInfo stateInfo = animator.IsInTransition(0)
                    ? animator.GetNextAnimatorStateInfo(0)
                    : animator.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.shortNameHash == Animator.StringToHash("Throw"))
                {
                    throwClip = playingClips[0].clip;
                }
            }

            if (throwClip == null)
            {
                AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
                for (int i = 0; i < clips.Length; i++)
                {
                    AnimationClip clip = clips[i];
                    if (clip != null && clip.name.IndexOf("Throw", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        throwClip = clip;
                        break;
                    }
                }
            }

            if (throwClip == null || throwClip.length <= 0f || throwClip.frameRate <= 0f)
            {
                return false;
            }

            var profile = visualSwitcher != null ? visualSwitcher.ActiveVisualProfile : null;
            int activeReleaseFrame = profile != null && profile.throwReleaseFrame > 0
                ? profile.throwReleaseFrame : releaseFrame;
            normalizedTime = Mathf.Clamp01(activeReleaseFrame / (throwClip.length * throwClip.frameRate));
            return normalizedTime > 0f && normalizedTime < 1f;
        }

        bool TryGetThrowPlaybackTime(out float normalizedTime)
        {
            normalizedTime = 0f;
            Animator animator = visualSwitcher != null ? visualSwitcher.ActiveVisualAnimator : null;
            animator ??= movement != null ? movement.animator : null;
            if (animator == null)
            {
                return false;
            }

            int throwHash = Animator.StringToHash("Throw");
            if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.shortNameHash == throwHash)
                {
                    normalizedTime = next.normalizedTime;
                    return true;
                }
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            if (current.shortNameHash != throwHash)
            {
                return false;
            }

            normalizedTime = current.normalizedTime;
            return true;
        }

        bool TryResolveLanding(out LandingPlan landing, out string failureDetail)
        {
            landing = default;
            failureDetail = string.Empty;
            Vector3 forward = GetAimDirection();
            float farDistance = Mathf.Max(minimumThrowDistance, throwDistance);
            float candidateSearchRadius = Mathf.Clamp(landingSearchRadius, 0.35f, 0.85f);
            RaycastHit ownerGroundHit = default;
            bool hasOwnerGround = TryFindGround(transform.position, out ownerGroundHit);
            if (!hasOwnerGround)
            {
                failureDetail = "Player chưa đứng trên một mặt đất hợp lệ.";
                return false;
            }
            if (Mathf.Abs(ownerGroundHit.point.y - transform.position.y) > 0.35f)
            {
                failureDetail = "Player đang ở trên không hoặc mép địa hình.";
                return false;
            }
            NavMeshHit ownerNavHit = default;
            bool hasOwnerOnNavMesh = requireNavMesh && hasOwnerGround
                && NavMesh.SamplePosition(ownerGroundHit.point, out ownerNavHit,
                    Mathf.Max(1f, candidateSearchRadius), navMeshAreaMask);
            int missingGround = 0;
            int missingNavMesh = 0;
            int tooNear = 0;
            int blockedNavMesh = 0;
            int heightMismatch = 0;
            int blockedPhysics = 0;

            for (int distanceIndex = 0; distanceIndex < LandingDistanceFactors.Length;
                distanceIndex++)
            {
                float candidateDistance = Mathf.Lerp(minimumThrowDistance, farDistance,
                    LandingDistanceFactors[distanceIndex]);
                for (int angleIndex = 0; angleIndex < LandingAngles.Length; angleIndex++)
                {
                    Vector3 direction = Quaternion.Euler(0f, LandingAngles[angleIndex], 0f)
                        * forward;
                    Vector3 candidate = transform.position + direction * candidateDistance;

                    if (!TryFindGround(candidate, out RaycastHit groundHit))
                    {
                        missingGround++;
                        continue;
                    }

                    if (Mathf.Abs(groundHit.point.y - ownerGroundHit.point.y) > 1.2f)
                    {
                        heightMismatch++;
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
                        || HasHorizontalObstacle(groundHit.point, groundHit.collider)
                        || HasTrajectoryObstacle(groundHit.point, groundHit.collider))
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
                    + $"quá gần {tooNear}, lệch độ cao {heightMismatch}, "
                    + $"vướng vật cản {blockedPhysics}.";
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
                || Mathf.Abs(groundHit.point.y - impactPoint.y) > 0.2f
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

        bool TryResolvePetSpawnPoint(Vector3 ballGroundPoint, Vector3 direction,
            out Vector3 spawnPoint)
        {
            spawnPoint = default;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return false;
            }

            Vector3 candidate = ballGroundPoint
                + direction.normalized * Mathf.Max(0.1f, petSpawnAheadDistance);
            if (!TryFindGround(candidate, out RaycastHit groundHit)
                || Mathf.Abs(groundHit.point.y - ballGroundPoint.y) > 0.35f
                || !HasPetSpawnClearance(groundHit.point, groundHit.collider)
                || HasHorizontalObstacle(groundHit.point, groundHit.collider))
            {
                return false;
            }

            spawnPoint = groundHit.point;
            return true;
        }

        bool HasPetSpawnClearance(Vector3 groundPoint, Collider groundCollider)
        {
            Vector3 lower = groundPoint + Vector3.up * 0.5f;
            Vector3 upper = groundPoint + Vector3.up * 1.2f;
            int count = Physics.OverlapCapsuleNonAlloc(lower, upper, 0.42f,
                clearanceHits, obstacleLayers, QueryTriggerInteraction.Ignore);
            if (count == clearanceHits.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                Collider candidate = clearanceHits[i];
                if (candidate == null || candidate == groundCollider
                    || candidate.transform.IsChildOf(transform)
                    || (travellingBall != null
                        && candidate.transform.IsChildOf(travellingBall.transform))
                    || IsSupportingSurface(candidate, groundPoint))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        bool TryFindGround(Vector3 point, out RaycastHit bestHit)
        {
            Vector3 origin = point + Vector3.up * groundProbeHeight;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits,
                groundProbeHeight + groundProbeDistance, groundLayers, QueryTriggerInteraction.Ignore);
            if (hitCount == groundHits.Length)
            {
                bestHit = default;
                return false;
            }

            int bestIndex = -1;
            float bestHeightDelta = float.PositiveInfinity;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = groundHits[i];
                if (ShouldIgnore(hit.collider)
                    || (hit.collider.attachedRigidbody != null
                        && !hit.collider.attachedRigidbody.isKinematic)
                    || hit.normal.y < 0.65f)
                {
                    continue;
                }

                float heightDelta = Mathf.Abs(hit.point.y - point.y);
                if (heightDelta >= bestHeightDelta)
                {
                    continue;
                }

                bestIndex = i;
                bestHeightDelta = heightDelta;
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

        bool HasTrajectoryObstacle(Vector3 groundPoint, Collider groundCollider)
        {
            if (handSocket == null || !handSocket.TryGetLaunchPosition(out Vector3 start))
            {
                return true;
            }

            float radius = handSocket.HeldBall != null ? GetBallRadius(handSocket.HeldBall) : handSocket.BallRadius;
            Vector3 target = groundPoint + Vector3.up * radius;
            if (!PetSummonBallProjectile.TryCalculateArc(start, target, throwApexHeight,
                out Vector3 velocity, out float duration, ballMotionSpeed))
            {
                return true;
            }
            Vector3 previous = start;
            const int segments = 20;
            for (int segment = 1; segment <= segments; segment++)
            {
                float time = duration * segment / segments;
                float speed = Mathf.Max(0.1f, ballMotionSpeed);
                Vector3 next = start + velocity * time
                    + Physics.gravity * (0.5f * speed * speed * time * time);
                Vector3 delta = next - previous;
                float distance = delta.magnitude;
                if (distance > 0.001f)
                {
                    int count = Physics.SphereCastNonAlloc(previous, obstacleProbeRadius,
                        delta / distance, obstacleHits, distance, obstacleLayers,
                        QueryTriggerInteraction.Ignore);
                    if (count == obstacleHits.Length)
                    {
                        return true;
                    }

                    for (int i = 0; i < count; i++)
                    {
                        RaycastHit hit = obstacleHits[i];
                        Collider collider = hit.collider;
                        if (collider == null || ShouldIgnore(collider)
                            || (handSocket.HeldBall != null
                                && collider.transform.IsChildOf(handSocket.HeldBall.transform)))
                        {
                            continue;
                        }

                        if (collider == groundCollider && segment >= segments - 1
                            && hit.normal.y >= 0.65f)
                        {
                            continue;
                        }

                        return true;
                    }
                }

                previous = next;
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
                LastFailureReason = "Pet cũ phải hoàn tất animation thu trước khi gọi pet mới.";
                return false;
            }

            pet.AssignOwner(transform);
            if (!pet.TrySummonAt(spawnPoint, facingDirection, true))
            {
                return false;
            }

            commandInput.SetActivePet(pet);
            return true;
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

        void ClearSequenceBalls()
        {
            if (travellingBall != null)
            {
                travellingBall.SetActive(false);
                Destroy(travellingBall);
                travellingBall = null;
            }
            handSocket?.DestroyHeldBall();
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
