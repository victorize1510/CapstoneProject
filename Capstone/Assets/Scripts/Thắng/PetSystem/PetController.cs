// PetController.cs
// Điều khiển pet (sinh vật đồng hành) của người chơi:
//   - Triệu hồi / thu hồi pet (kèm hiệu ứng summon & recall)
//   - Đi theo chủ, lang thang quanh chủ, di chuyển tới điểm được ra lệnh
//   - Đuổi theo và tấn công quái (DummyEnemy), nhận EXP/EV khi hạ quái
//   - Tự tìm đường bằng NavMesh HOẶC tự di chuyển trực tiếp (né vật cản bằng SphereCast)
//   - Chọn animation phù hợp cho nhiều loại model pet khác nhau

using System.Collections.Generic;
using Capstone.Game.HudSystem;
using Capstone.Game.MapSystem;
using Capstone.Game.SaveSystem;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Component chính điều khiển hành vi của pet.
/// Hoạt động theo mô hình máy trạng thái (State Machine) với enum PetState.
/// [DisallowMultipleComponent]: không cho gắn 2 PetController lên cùng 1 GameObject.
/// </summary>
[DisallowMultipleComponent]
public class PetController : MonoBehaviour
{
    // Các góc (độ) thử lệch hướng khi bị vật cản chặn đường lúc di chuyển trực tiếp (không NavMesh).
    private static readonly float[] DirectAvoidanceAngles = { 35f, 70f, 105f, 140f };
    // Các vị trí dự phòng (tính theo hệ toạ độ local của chủ) để đặt pet
    // khi vị trí followOffset mặc định không an toàn (bị kẹt trong tường, không có đất...).
    private static readonly Vector3[] SafeSpotFallbackOffsets =
    {
        new Vector3(1.7f, 0f, -1.7f),
        new Vector3(-2.2f, 0f, 0f),
        new Vector3(2.2f, 0f, 0f),
        new Vector3(0f, 0f, -2.2f)
    };


    // ======================================================================
    // TRẠNG THÁI CỦA PET
    // ======================================================================
    /// <summary>
    /// Các trạng thái của máy trạng thái. Pet luôn ở đúng 1 trạng thái tại 1 thời điểm.
    /// </summary>
    private enum PetState
    {
        // Chưa được triệu hồi: bị ẩn hoàn toàn (tắt renderer, collider, animator, map marker).
        Dormant,
        // Đang chạy animation xuất hiện, chưa nhận lệnh.
        Summoning,
        // Chạy theo chủ.
        FollowOwner,
        // Đi lang thang ngẫu nhiên quanh chủ khi chủ ở gần.
        RoamAroundOwner,
        // Di chuyển tới điểm người chơi ra lệnh.
        MoveToPoint,
        // Đuổi theo mục tiêu để vào tầm đánh.
        ChaseTarget,
        // Đang tấn công mục tiêu.
        AttackTarget
    }


    // ======================================================================
    // THAM CHIẾU & API HIỂN THỊ (dùng cho hệ thống hình ảnh / tiến hoá)
    // ======================================================================
    [Header("References")]
    // Transform của người chơi (chủ). Nếu để trống sẽ tự tìm BasicPlayerMovement trong Start().
    public Transform owner;
    // Animator của model pet.
    public Animator animator;
    // Thời lượng animation triệu hồi (đã chia theo tốc độ phát).
    public float SummonPresentationAnimationDuration => summonAnimationDuration;
    // True khi animation triệu hồi (Cast Spell hoặc Spawn) đã chạy xong.
    // Các script hiệu ứng summon dùng để biết khi nào kết thúc presentation.
    public bool HasSummonPresentationAnimationFinished
    {
        get
        {
            // Chưa bắt đầu animation thì chắc chắn chưa xong.
            if (!summonPresentationAnimationStarted) return false;
            // Không dùng Cast Spell (dùng Spawn) -> chỉ cần đủ thời gian.
            if (!summonCastActive) return Time.time - summonStartedAt >= summonAnimationDuration;
            // Không có Animator -> coi như xong.
            if (animator == null) return true;
            // Đang ở state Cast Spell thì đợi tới 98%; nếu đã thoát khỏi Cast Spell thì đợi đủ thời gian.
            var info = animator.GetCurrentAnimatorStateInfo(0);
            bool inCast = info.shortNameHash == Animator.StringToHash("Cast Spell");
            return !animator.IsInTransition(0) && (inCast && info.normalizedTime >= .98f
                || !inCast && Time.time - summonStartedAt >= summonAnimationDuration);
        }
    }
    /// <summary>
    /// Gắn Animator mới cho pet (ví dụ khi pet tiến hoá và đổi model).
    /// Khôi phục tốc độ animator cũ, lưu lại trạng thái enabled ban đầu và quét lại parameter.
    /// </summary>
    public void BindVisualAnimator(Animator nextAnimator)
    {
        if (nextAnimator == null) return;
        RestoreSummonAnimatorSpeed();
        animator = nextAnimator;
        animatorInitiallyEnabled = nextAnimator.enabled;
        ConfigureAnimator();
    }

    /// <summary>
    /// Renderer này có nên hiển thị không?
    /// Chỉ hiện khi pet đang được triệu hồi, renderer thuộc model hiện tại (nếu có PetEvolutionVisualController)
    /// và renderer được phép bật theo bảng rendererEnabledStates.
    /// </summary>
    public bool ShouldShowVisualRenderer(Renderer renderer)
    {
        if (renderer == null || !IsSummoned) return false;
        var visual = GetComponentInChildren<Capstone.Game.HudSystem.PetEvolutionVisualController>(true);
        return (visual == null || visual.IsCurrentVisualRenderer(renderer))
            && rendererEnabledStates.TryGetValue(renderer, out bool allowed) && allowed;
    }

    /// <summary>
    /// Bật/tắt 1 renderer. Giá trị được ghi nhớ, nhưng renderer chỉ thực sự bật khi pet đang được triệu hồi.
    /// </summary>
    public void SetVisualRendererEnabled(Renderer renderer, bool enabled)
    {
        if (renderer == null) return;
        rendererEnabledStates[renderer] = enabled;
        renderer.enabled = enabled && IsSummoned;
    }

    // Áp lại trạng thái ẩn/hiện của pet theo IsSummoned (dùng sau khi đổi model).
    public void RefreshVisualPresence() => SetWorldPresence(IsSummoned);
    // NavMeshAgent (tuỳ chọn). Chỉ dùng khi useNavMeshWhenAvailable = true.
    public NavMeshAgent agent;


    // ======================================================================
    // THÔNG SỐ ĐI THEO CHỦ
    // ======================================================================
    [Header("Follow")]
    // Vị trí mong muốn so với chủ (local): lệch trái 1.4m, lùi sau 1.6m.
    public Vector3 followOffset = new Vector3(-1.4f, 0f, -1.6f);
    // Gần hơn mức này -> dừng lại (rồi chuyển sang lang thang).
    public float followDistance = 4f;
    // Xa hơn mức này -> bắt đầu chạy theo chủ lại.
    // Khoảng giữa followDistance và followResumeDistance là vùng 'hysteresis' để pet không giật qua lại.
    public float followResumeDistance = 7f;
    // Khoảng cách 'dây xích' tối đa (hiện luôn lớn hơn followResumeDistance nên ít tác dụng).
    public float leashDistance = 10f;
    // Xa hơn mức này -> dịch chuyển tức thời về gần chủ.
    public float teleportDistance = 24f;


    // ======================================================================
    // THÔNG SỐ DI CHUYỂN
    // ======================================================================
    [Header("Movement")]
    // Tốc độ khi đi theo chủ.
    public float followSpeed = 4.2f;
    // Tốc độ khi thực hiện lệnh (đi tới điểm, đuổi quái).
    public float commandSpeed = 5.3f;
    // Gia tốc của NavMeshAgent.
    public float acceleration = 18f;
    // Tốc độ xoay của NavMeshAgent (độ/giây).
    public float angularSpeed = 720f;
    // Độ 'gắt' khi xoay mặt: càng lớn xoay càng nhanh.
    public float turnSharpness = 14f;
    // Khoảng cách coi như đã tới điểm được ra lệnh.
    public float pointStoppingDistance = 0.35f;
    // Có dùng NavMesh hay không. Mặc định FALSE -> dùng di chuyển trực tiếp.
    public bool useNavMeshWhenAvailable = false;
    // Khi không dùng được NavMesh thì tự di chuyển trực tiếp.
    public bool useDirectMoveFallback = true;
    // Khi di chuyển trực tiếp, bám pet xuống mặt đất.
    public bool snapFallbackToGround = true;
    // Layer được coi là mặt đất (mặc định: tất cả).
    public LayerMask groundMask = ~0;

    // ======================================================================
    // THÔNG SỐ DI CHUYỂN TRỰC TIẾP (KHÔNG NAVMESH)
    // ======================================================================
    [Header("Movement without NavMesh")]
    // Bán kính thân pet dùng cho SphereCast dò vật cản.
    [Min(0.1f)] public float directBodyRadius = 0.32f;
    // Độ cao điểm bắt đầu dò vật cản (tính từ chân pet).
    [Min(0.2f)] public float directProbeHeight = 0.65f;
    // Dò xa thêm bao nhiêu mét phía trước.
    [Min(0.2f)] public float directLookAhead = 0.9f;
    // Bậc thềm cao tối đa pet có thể bước lên.
    [Min(0.05f)] public float directMaxStepHeight = 0.3f;
    // Pháp tuyến mặt đất tối thiểu (y) để coi là đi được (0.65 ~ dốc ~49 độ).
    [Range(0.1f, 1f)] public float directMinGroundNormal = 0.65f;
    // Layer được coi là vật cản.
    public LayerMask directObstacleMask = ~0;
    // Bán kính tối thiểu của NavMeshAgent.
    [Min(0.1f)] public float minimumNavigationRadius = 0.65f;
    // Khoảng thời gian tối thiểu giữa 2 lần tính lại đường đi NavMesh.
    [Min(0.02f)] public float navPathRefreshInterval = 0.12f;
    // Đích thay đổi ít hơn mức này thì không tính lại đường.
    [Min(0.01f)] public float navDestinationThreshold = 0.15f;
    // Khoảng thời gian thử bật lại NavMeshAgent khi thất bại.
    [Min(0.1f)] public float navMeshRetryInterval = 1f;


    // ======================================================================
    // THÔNG SỐ CHIẾN ĐẤU
    // ======================================================================
    [Header("Combat")]
    // Tầm đánh (đo tới điểm gần nhất trên collider của quái).
    public float attackRange = 1.45f;
    // Pet sẽ đứng cách bề mặt quái bao nhiêu mét khi tiếp cận.
    public float attackApproachDistance = 0.65f;
    // Cộng thêm vào tầm đánh để tránh trường hợp đứng sát mép mà không đánh được.
    public float attackReachPadding = 0.18f;
    // Bao lâu tính lại điểm tiếp cận khi đuổi quái.
    public float attackRepathInterval = 0.12f;
    // Xoay mặt ngay lập tức về phía quái khi nhận lệnh tấn công.
    public bool snapFaceTargetOnCommand = true;
    // Sát thương gốc (chưa cộng chỉ số Attack của pet).
    public float attackDamage = 20f;

    // Sát thương cơ bản hiện tại = sát thương gốc + Attack trong PetCollectionMetadata (không tính chí mạng).
    public float CurrentBaseAttackDamage
    {
        get
        {
            PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
            return Mathf.Max(0f, attackDamage + (metadata != null ? metadata.Attack : 0));
        }
    }
    // Thời gian hồi giữa 2 đòn đánh.
    public float attackCooldown = 0.95f;
    // Độ trễ từ lúc bắt đầu đòn tới lúc gây sát thương (khớp với animation).
    public float attackHitDelay = 0.28f;
    // Thời gian 1 đòn đánh 'chiếm' trạng thái Attack trước khi quyết định đánh tiếp/đuổi/quay về.
    public float attackStateTime = 0.75f;


    // ======================================================================
    // THÔNG SỐ TRIỆU HỒI
    // ======================================================================
    [Header("Summon")]
    // Cho phép pet tự đọc phím (summonKey) để triệu hồi/gọi về. Thường để false, do script khác điều khiển.
    public bool useSelfInput = false;
    // Phím triệu hồi khi useSelfInput = true.
    public KeyCode summonKey = KeyCode.Alpha1;
    // Thời lượng animation Spawn mặc định.
    public float spawnDuration = 1.1f;
    // Nếu nhận lệnh khi chưa triệu hồi thì tự triệu hồi.
    public bool summonOnCommand = false;


    // ======================================================================
    // THÔNG SỐ LANG THANG QUANH CHỦ
    // ======================================================================
    [Header("Roam")]
    // Bật/tắt hành vi lang thang. Tắt thì pet chỉ đứng theo chủ.
    public bool roamAroundOwner = true;
    // Bán kính lang thang nhỏ nhất / lớn nhất quanh chủ.
    public float roamMinRadius = 2.2f;
    public float roamMaxRadius = 5f;
    // Thời gian đứng nghỉ giữa 2 lần lang thang (ngẫu nhiên trong khoảng).
    public float roamIdleTimeMin = 1f;
    public float roamIdleTimeMax = 3f;
    // Tốc độ đi bộ / chạy khi lang thang.
    public float roamWalkSpeed = 2f;
    public float roamRunSpeed = 4.6f;
    // Xác suất (12%) pet chạy thay vì đi bộ.
    public float roamRunChance = 0.12f;
    // Khoảng cách coi như đã tới điểm lang thang.
    public float roamPointTolerance = 0.45f;


    // ======================================================================
    // TÊN STATE TRONG ANIMATOR
    // ======================================================================
    // Mỗi mảng là danh sách tên ứng viên; code sẽ phát state ĐẦU TIÊN tồn tại trong Animator.
    // Nhờ vậy 1 script dùng được cho nhiều model pet có tên animation khác nhau.
    [Header("Animator States")]
    public string[] spawnStates = { "Spawn", "Idle" };
    public string[] idleStates = { "Idle", "Idle 1", "Idle Happy" };
    public string[] moveStates = { "Move", "Fly Forward In Place", "Run Forward In Place", "Walk Forward In Place", "Run Forward", "Walk Forward" };
    public string[] attackStates = { "Attack", "Bite Attack", "Bite Attack Low", "Projectile Attack", "Projectile Attack Low", "Cast Spell", "Blast Attack", "Wing Attack" };
    // Thời gian chuyển (crossfade) giữa các animation di chuyển / tấn công.
    public float locomotionFade = 0.14f;
    public float attackFade = 0.08f;


    // ======================================================================
    // TÊN PARAMETER TRONG ANIMATOR (không bắt buộc phải có)
    // ======================================================================
    [Header("Animator Parameters")]
    public string speedParameter = "Speed";
    public string movingParameter = "IsMoving";
    public string attackTriggerParameter = "Attack";
    public string attackingParameter = "IsAttacking";


    // ======================================================================
    // BIẾN NỘI BỘ
    // ======================================================================
    // Cache hash của các parameter mà Animator THỰC SỰ có, để tránh lỗi khi set parameter không tồn tại.
    private readonly HashSet<int> floatParameters = new HashSet<int>();
    private readonly HashSet<int> boolParameters = new HashSet<int>();
    private readonly HashSet<int> triggerParameters = new HashSet<int>();
    // Mảng đệm cho các hàm Physics ...NonAlloc (tránh cấp phát bộ nhớ mỗi frame).
    private readonly RaycastHit[] groundHits = new RaycastHit[16];
    private readonly RaycastHit[] directObstacleHits = new RaycastHit[24];

    // --- Trạng thái hiện tại & dữ liệu runtime ---
    private PetState state = PetState.Dormant;
    // Quái đang nhắm tới.
    private DummyEnemy target;
    private MapMarker mapMarker;
    private PetLevelUpService levelUpService;
    // Điểm được ra lệnh đi tới.
    private Vector3 commandedPoint;
    private Vector3 attackDestination;
    private Vector3 roamDestination;
    private Vector3 lastRoamDirection;
    // Tên state animator đang phát (để không phát lại liên tục).
    private string currentAnimatorState;
    // Các mốc thời gian (Time.time) dùng cho tấn công, triệu hồi, lang thang, skill...
    private float attackStartedAt;
    private float summonStartedAt;
    private float summonAnimationDuration;
    private float summonAnimatorSpeedBefore;
    private bool summonAnimatorSpeedOverridden;
    private float nextRoamAt;
    private float activeRoamSpeed;
    private float nextAttackAt;
    private float nextAttackRepathAt;
    private float pendingHitAt;
    private float directMoveSpeed;
    private float skillAnimationUntil;
    private float pendingSkillStartAt;
    private float pendingSkillFade;
    // Dữ liệu animation skill đang chờ phát (khi có windup).
    private string[] pendingSkillCandidates;
    private bool skillStopsMovement;
    private bool skillFacesTarget;
    private bool hasRoamDestination;
    private bool hasPendingHit;
    private bool hasPendingSkillAnimation;
    // Đang bị khoá do quá trình thu hồi (recall).
    private bool recallLocked;
    private DummyEnemy recallFacingEnemy;
    private bool recallWasInCombat;
    // Cờ cho quá trình summon presentation (hiệu ứng bay ra rồi mới hiện animation).
    private bool summonRevealPending;
    private bool summonCastActive;
    private bool summonPresentationAnimationStarted;
    private bool warnedMissingCastSpell;
    private bool navDestinationInitialized;
    // Lưu trạng thái bật/tắt BAN ĐẦU của renderer/collider để khi hiện lại chỉ bật đúng những cái vốn bật.
    private readonly Dictionary<Renderer, bool> rendererEnabledStates = new Dictionary<Renderer, bool>();
    private readonly Dictionary<Collider, bool> colliderEnabledStates = new Dictionary<Collider, bool>();
    // Mảng đệm cho OverlapCapsule khi kiểm tra chỗ trống.
    private readonly Collider[] presenceHits = new Collider[24];
    private bool animatorInitiallyEnabled;
    private bool markerInitiallyEnabled;
    private Vector3 lastNavDestination;
    // Dữ liệu né vật cản khi di chuyển trực tiếp.
    private float directAvoidanceUntil;
    private float directAvoidanceAngle;
    private float directStuckSince;
    private float nextNavPathRefreshAt;
    private float nextNavMeshRetryAt;
    // Phía né vật cản: 1 = phải, -1 = trái (ưu tiên phía vừa né thành công).
    private int directAvoidanceSide = 1;
    // Chiều đi vòng quanh chủ khi lang thang và số bước đã đi cùng chiều.
    private int roamOrbitSide = 1;
    private int roamStepsInDirection;


    // ======================================================================
    // THUỘC TÍNH CÔNG KHAI
    // ======================================================================
    // Pet đã được triệu hồi (đang hiện trong thế giới) hay chưa.
    public bool IsSummoned { get; private set; }
    // Đang chờ script hiệu ứng hoàn tất summon presentation.
    public bool IsSummonPresentationPending => summonRevealPending;

    // Chỉ nhận lệnh khi đã triệu hồi xong và không đang bị thu hồi.
    public bool CanReceiveCommands
    {
        get { return IsSummoned && state != PetState.Summoning && !recallLocked; }
    }

    // Đang có mục tiêu chiến đấu còn sống.
    public bool HasCombatTarget
    {
        get { return target != null && target.IsAlive; }
    }


    // ======================================================================
    // VÒNG ĐỜI UNITY
    // ======================================================================
    // Reset: gọi khi gắn component trong Editor -> tự gán animator và agent.
    private void Reset()
    {
        animator = GetComponentInChildren<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

    // Awake: lấy tham chiếu, cấu hình animator/agent, lưu trạng thái hiển thị ban đầu rồi ẩn pet.
    private void Awake()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        ConfigureAnimator();
        ConfigureAgent();
        animatorInitiallyEnabled = animator != null && animator.enabled;
        CacheWorldPresence();
        SetWorldPresence(false);
    }

    // OnEnable: đảm bảo có map marker; nếu pet chưa triệu hồi thì ẩn marker.
    private void OnEnable()
    {
        EnsureMapMarker();
        markerInitiallyEnabled = mapMarker != null && mapMarker.enabled;
        if (!IsSummoned && mapMarker != null)
        {
            mapMarker.enabled = false;
        }
    }

    // Start: tự tìm chủ nếu chưa gán; đưa pet về trạng thái Dormant nếu chưa được khôi phục từ save.
    private void Start()
    {
        if (owner == null)
        {
            BasicPlayerMovement player = FindFirstObjectByType<BasicPlayerMovement>();
            if (player != null)
            {
                owner = player.transform;
            }
        }

        // A pet instantiated while loading a save can be restored before its first
        // Start frame. Do not hide it again after that restore has succeeded.
        if (!IsSummoned)
        {
            EnterDormant();
        }
    }

    // Update: bộ điều phối chính, chạy mỗi frame. Kiểm tra theo thứ tự ưu tiên từ trên xuống.
    private void Update()
    {
        // 1) Tự đọc phím triệu hồi (nếu bật và gameplay không bị khoá bởi UI inventory).
        if (!Capstone.Game.Inventory.InventoryInputController.GameplayInputBlocked
            && useSelfInput
            && summonKey != KeyCode.None
            && Input.GetKeyDown(summonKey))
        {
            ToggleSummonOrRecall();
            return;
        }

        // 2) Chưa triệu hồi -> không làm gì.
        if (!IsSummoned)
        {
            return;
        }

        // 3) Đang bị thu hồi -> đứng yên và chỉ xoay mặt.
        if (recallLocked)
        {
            StopMovement(false);
            UpdateRecallFacing();
            return;
        }

        // 4) Đang triệu hồi -> chờ animation xuất hiện chạy xong.
        if (state == PetState.Summoning)
        {
            UpdateSummoning();
            return;
        }

        // 5) Mất chủ khi đang follow -> đứng yên.
        if (owner == null && state == PetState.FollowOwner)
        {
            StopMovement();
            UpdateAnimator(0f);
            return;
        }

        // 6) Đang diễn animation skill -> giữ nguyên, có thể dừng di chuyển và quay mặt về mục tiêu.
        if (IsSkillAnimationActive())
        {
            TryStartPendingSkillAnimation();

            if (skillStopsMovement)
            {
                StopMovement(false);
            }

            if (skillFacesTarget && target != null && target.IsAlive)
            {
                FacePoint(GetTargetPosition());
            }

            return;
        }

        // 7) Chạy logic theo trạng thái hiện tại.
        switch (state)
        {
            case PetState.FollowOwner:
                UpdateFollow();
                break;
            case PetState.RoamAroundOwner:
                UpdateRoamAroundOwner();
                break;
            case PetState.MoveToPoint:
                UpdateMoveToPoint();
                break;
            case PetState.ChaseTarget:
                UpdateChaseTarget();
                break;
            case PetState.AttackTarget:
                UpdateAttackTarget();
                break;
        }
    }


    // ======================================================================
    // API RA LỆNH (script khác gọi vào)
    // ======================================================================
    /// <summary>
    /// Đổi chủ của pet. Nếu đang follow thì chạy ngay tới vị trí follow mới.
    /// </summary>
    public void AssignOwner(Transform newOwner)
    {
        owner = newOwner;
        if (state == PetState.FollowOwner)
        {
            MoveToFollowSpot();
        }
    }

    /// <summary>
    /// Ra lệnh pet đi tới 1 điểm trong thế giới. Tới nơi pet sẽ tự quay về với chủ.
    /// </summary>
    public void CommandMove(Vector3 worldPoint)
    {
        // Chưa nhận lệnh được -> tuỳ chọn tự triệu hồi rồi bỏ qua lệnh.
        if (!CanReceiveCommands)
        {
            if (summonOnCommand)
            {
                Summon();
            }

            return;
        }

        // Huỷ mục tiêu, huỷ lang thang và chuyển sang MoveToPoint.
        target = null;
        hasRoamDestination = false;
        commandedPoint = worldPoint;
        state = PetState.MoveToPoint;
        MoveTo(commandedPoint, commandSpeed, pointStoppingDistance);
    }

    /// <summary>
    /// Ra lệnh pet tấn công 1 quái.
    /// </summary>
    public void CommandAttack(DummyEnemy enemy)
    {
        if (!CanReceiveCommands)
        {
            if (summonOnCommand)
            {
                Summon();
            }

            return;
        }

        // Mục tiêu không hợp lệ hoặc đã chết -> quay về với chủ.
        if (enemy == null || !enemy.IsAlive)
        {
            ReturnToOwner();
            return;
        }

        // Đặt mục tiêu và chuyển sang trạng thái đuổi theo.
        target = enemy;
        hasRoamDestination = false;
        directStuckSince = 0f;
        state = PetState.ChaseTarget;
        nextAttackRepathAt = 0f;
        // Quay mặt ngay về phía quái cho cảm giác phản hồi nhanh.
        if (snapFaceTargetOnCommand)
        {
            SnapFacingToPoint(GetTargetPosition());
        }

        MoveToAttackRange();
    }

    /// <summary>
    /// Huỷ mọi lệnh và quay về đi theo chủ.
    /// </summary>
    public void ReturnToOwner()
    {
        if (!CanReceiveCommands)
        {
            return;
        }

        target = null;
        hasRoamDestination = false;
        directStuckSince = 0f;
        state = PetState.FollowOwner;
        MoveToFollowSpot();
    }


    // ======================================================================
    // TRIỆU HỒI / THU HỒI
    // ======================================================================
    /// <summary>
    /// Triệu hồi pet ở vị trí an toàn gần chủ.
    /// Nếu pet đã được triệu hồi rồi thì chỉ reset về trạng thái lang thang/đi theo.
    /// </summary>
    public void Summon()
    {
        // Đã triệu hồi -> reset hành vi.
        if (IsSummoned)
        {
            target = null;
            hasRoamDestination = false;
            if (state != PetState.Summoning)
            {
                state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
                ScheduleNextRoam(0.1f);
            }

            return;
        }

        // Tìm chỗ an toàn gần chủ rồi triệu hồi; thất bại thì cảnh báo (không cho pet kẹt trong tường).
        if (owner == null || !TryFindSafeSpotNearOwner(out Vector3 spot)
            || !TrySummonAt(spot, owner.forward))
        {
            Debug.LogWarning("Pet summon failed: không có mặt đất an toàn gần player.", this);
        }
    }

    /// <summary>
    /// Triệu hồi tại vị trí chỉ định, có cảnh báo nếu thất bại.
    /// </summary>
    public void SummonAt(Vector3 worldPosition, Vector3 facingDirection)
    {
        if (!TrySummonAt(worldPosition, facingDirection))
        {
            Debug.LogWarning("Pet summon failed: vị trí spawn không có nền an toàn.", this);
        }
    }

    /// <summary>
    /// Thử triệu hồi tại vị trí chỉ định, phát animation spawn ngay.
    /// </summary>
    public bool TrySummonAt(Vector3 worldPosition, Vector3 facingDirection)
    {
        return TrySummonAt(worldPosition, facingDirection, false);
    }

    /// <summary>
    /// Thử triệu hồi tại vị trí chỉ định.
    /// deferSpawnAnimation = true: chưa phát animation, đợi script hiệu ứng gọi CompleteSummonPresentation().
    /// </summary>
    public bool TrySummonAt(Vector3 worldPosition, Vector3 facingDirection, bool deferSpawnAnimation)
    {
        // Không có mặt đất / không đủ chỗ đứng -> thất bại.
        if (!TryPlaceForSummon(worldPosition))
        {
            return false;
        }

        // Hiện pet và bắt đầu trạng thái Summoning.
        IsSummoned = true;
        SetWorldPresence(true);
        BeginSummon(deferSpawnAnimation);
        // Quay mặt theo hướng được truyền vào (nếu hợp lệ).
        if (facingDirection.sqrMagnitude > 0.001f)
        {
            FaceDirection(facingDirection);
        }

        return true;
    }

    /// <summary>
    /// Trong lúc hiệu ứng summon (ví dụ bay ra từ quả cầu), di chuyển pet theo hiệu ứng.
    /// Tắt NavMeshAgent và collider để không va chạm lung tung.
    /// </summary>
    public void SetSummonPresentationPosition(Vector3 position)
    {
        if (!IsSummoned || !summonRevealPending)
        {
            return;
        }

        DisableNavigationAgent();
        SetPresentationColliders(false);
        transform.position = position;
    }

    /// <summary>
    /// Kết thúc hiệu ứng summon: đặt pet xuống đất, phát animation, bật lại collider.
    /// </summary>
    public bool CompleteSummonPresentation(Vector3 groundPosition)
    {
        if (!IsSummoned || !summonRevealPending || !TryPlaceForSummon(groundPosition))
        {
            return false;
        }

        StartSummonPresentationAnimation();
        SetPresentationColliders(true);
        summonRevealPending = false;
        return true;
    }

    /// <summary>
    /// Phát animation xuất hiện: ưu tiên 'Cast Spell', nếu không có thì dùng spawnStates.
    /// presentationSpeed: tốc độ phát animation (tạm thời ghi đè animator.speed).
    /// </summary>
    public void StartSummonPresentationAnimation(float presentationSpeed = 1f)
    {
        if (!IsSummoned || !summonRevealPending || summonPresentationAnimationStarted)
        {
            return;
        }

        // Đánh dấu đã bắt đầu và ghi lại thời điểm.
        summonPresentationAnimationStarted = true;
        summonStartedAt = Time.time;
        float speed = Mathf.Max(0.1f, presentationSpeed);
        // Lưu tốc độ animator cũ để khôi phục sau khi triệu hồi xong.
        if (animator != null)
        {
            summonAnimatorSpeedBefore = animator.speed;
            summonAnimatorSpeedOverridden = true;
            animator.speed = speed;
        }
        // Thử phát Cast Spell và lấy độ dài clip để biết thời lượng.
        string[] castState = { "Cast Spell" };
        if (HasAnimatorState(castState) && PlayAnimatorState(castState, attackFade, true))
        {
            summonAnimationDuration = FindAnimationDuration("Cast Spell", spawnDuration) / speed;
            summonCastActive = true;
        }
        // Không có Cast Spell -> cảnh báo 1 lần rồi dùng animation spawn.
        else
        {
            if (!warnedMissingCastSpell)
            {
                Debug.LogWarning("Pet has no Cast Spell animation; using its spawn animation.", this);
                warnedMissingCastSpell = true;
            }
            PlayAnimatorState(spawnStates, attackFade, true);
            summonAnimationDuration = spawnDuration / speed;
            summonCastActive = false;
        }
    }

    /// <summary>
    /// Bật/tắt chế độ trình diễn thu hồi: tắt agent và collider trong lúc hiệu ứng recall chạy.
    /// </summary>
    public void SetRecallPresentation(bool active)
    {
        if (active)
        {
            DisableNavigationAgent();
        }

        SetPresentationColliders(!active && IsSummoned);
    }

    // Bật/tắt collider của pet (chỉ bật những collider vốn bật ban đầu).
    private void SetPresentationColliders(bool visible)
    {
        foreach (KeyValuePair<Collider, bool> entry in colliderEnabledStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = visible && entry.Value;
            }
        }
    }

    // Tìm độ dài clip animation theo tên (không phân biệt hoa thường). Không thấy thì trả về fallback.
    private float FindAnimationDuration(string clipName, float fallback)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return fallback;
        }

        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return Mathf.Max(0.05f, clip.length);
            }
        }

        return fallback;
    }

    /// <summary>
    /// Dùng khi LOAD SAVE: đặt pet đã triệu hồi xuống gần chủ ngay lập tức (không chạy animation summon).
    /// Tìm kiếm rộng hơn (expandedSearch) để tránh pet bị ẩn vĩnh viễn khi offset mặc định rơi vào vật cản.
    /// </summary>
    public bool TryRestoreSummonedNearOwner()
    {
        if (!TryFindSafeSpotNearOwner(out Vector3 spot, 2.5f, true) || !TryPlaceForSummon(spot))
        {
            EnterDormant();
            return false;
        }

        IsSummoned = true;
        SetWorldPresence(true);
        recallLocked = false;
        target = null;
        hasRoamDestination = false;
        directStuckSince = 0f;
        state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
        ScheduleNextRoam(0.5f);
        PlayAnimatorState(idleStates, locomotionFade, true);
        return true;
    }

    /// <summary>
    /// Thu hồi pet về trạng thái Dormant.
    /// </summary>
    public void Withdraw()
    {
        if (!IsSummoned && state == PetState.Dormant)
        {
            return;
        }

        EnterDormant();
    }

    /// <summary>
    /// Khoá / mở khoá pet trong quá trình thu hồi.
    /// Khi khoá: huỷ mục tiêu, huỷ đòn đánh và skill đang chờ, đứng yên.
    /// Khi mở khoá: quay về lang thang / đi theo chủ.
    /// </summary>
    public void SetRecallLocked(bool locked)
    {
        // Mở khoá -> xoá dữ liệu xoay mặt khi recall.
        if (!locked)
        {
            recallFacingEnemy = null;
            recallWasInCombat = false;
            hasRecallVisualCenter = false;
            recallFacingTargetLost = false;
        }
        if (recallLocked == locked)
        {
            return;
        }

        // Khoá: ghi nhớ có đang đánh nhau không để quay mặt cho đúng.
        if (locked)
        {
            hasRecallVisualCenter = false;
            recallFacingTargetLost = false;
            recallWasInCombat = HasCombatTarget;
            recallFacingEnemy = recallWasInCombat ? target : null;
            recallLocked = true;
            target = null;
            hasRoamDestination = false;
            hasPendingHit = false;
            hasPendingSkillAnimation = false;
            pendingSkillCandidates = null;
            skillAnimationUntil = 0f;
            StopMovement(false);
            SetAnimatorBool(attackingParameter, false);
            UpdateRecallFacing();
            return;
        }

        // Mở khoá: trở lại hành vi bình thường.
        recallLocked = false;
        recallFacingEnemy = null;
        recallWasInCombat = false;

        if (IsSummoned)
        {
            state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
            ScheduleNextRoam(0.1f);
        }
    }

    // Khi đang recall: quay mặt về phía quái (nếu đang đánh) hoặc về phía chủ.
    private void UpdateRecallFacing()
    {
        Vector3 origin = hasRecallVisualCenter ? recallVisualCenter : transform.position;
        if (recallWasInCombat)
        {
            if (!recallFacingTargetLost && recallFacingEnemy != null && recallFacingEnemy.IsAlive)
                FaceDirection(recallFacingEnemy.TargetPosition - origin);
            else recallFacingTargetLost = true;
            // A lost combat target must not turn the pet back toward its owner.
        }
        else if (owner != null) FaceDirection(owner.position - origin);
    }

    // Tâm hình ảnh khi recall (do script hiệu ứng cung cấp) - dùng làm gốc khi tính hướng quay mặt.
    private Vector3 recallVisualCenter;
    private bool hasRecallVisualCenter;
    private bool recallFacingTargetLost;

    // Script hiệu ứng recall gọi để cập nhật tâm hình ảnh.
    public void SetRecallVisualCenter(Vector3 center)
    {
        if (!recallLocked) return;
        recallVisualCenter = center;
        hasRecallVisualCenter = true;
    }


    // ======================================================================
    // SKILL ANIMATION
    // ======================================================================
    /// <summary>
    /// Phát animation skill (không windup/recovery).
    /// </summary>
    public bool PlaySkillAnimation(string[] candidates, float duration, float fade, bool stopMovement, bool faceTarget)
    {
        return PlaySkillAnimation(candidates, duration, fade, stopMovement, faceTarget, 0f, 0f);
    }

    /// <summary>
    /// Phát animation skill cho hệ thống skill.
    /// candidates: danh sách tên state ứng viên; duration: thời lượng skill;
    /// stopMovement: có dừng di chuyển không; faceTarget: có quay mặt về mục tiêu không;
    /// windupSeconds: chờ bao lâu mới phát; recoverySeconds: thời gian hồi sau skill.
    /// Trả về false nếu Animator không có state nào phù hợp.
    /// </summary>
    public bool PlaySkillAnimation(string[] candidates, float duration, float fade, bool stopMovement, bool faceTarget, float windupSeconds, float recoverySeconds)
    {
        if (candidates == null || candidates.Length == 0)
        {
            return false;
        }

        if (!HasAnimatorState(candidates))
        {
            return false;
        }

        // Kẹp các giá trị về mức hợp lệ.
        duration = Mathf.Max(0.05f, duration);
        fade = Mathf.Max(0f, fade);
        windupSeconds = Mathf.Max(0f, windupSeconds);
        recoverySeconds = Mathf.Max(0f, recoverySeconds);

        // Lưu cấu hình skill để Update() giữ đúng hành vi trong suốt thời gian skill.
        skillStopsMovement = stopMovement;
        skillFacesTarget = faceTarget;
        if (skillStopsMovement)
        {
            StopMovement(false);
        }

        if (skillFacesTarget && target != null && target.IsAlive)
        {
            FacePoint(GetTargetPosition());
        }

        // Có windup -> hẹn giờ phát animation (TryStartPendingSkillAnimation sẽ phát).
        if (windupSeconds > 0f)
        {
            pendingSkillCandidates = candidates;
            pendingSkillFade = fade;
            pendingSkillStartAt = Time.time + windupSeconds;
            hasPendingSkillAnimation = true;
            skillAnimationUntil = pendingSkillStartAt + duration + recoverySeconds;
            return true;
        }

        // Không windup -> phát ngay.
        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
        if (!PlayAnimatorState(candidates, fade, true))
        {
            return false;
        }

        skillAnimationUntil = Time.time + duration + recoverySeconds;
        return true;
    }

    // Phím triệu hồi: chưa có thì triệu hồi, đang có thì gọi về bên chủ.
    private void ToggleSummonOrRecall()
    {
        if (!IsSummoned)
        {
            Summon();
            return;
        }

        if (state == PetState.Summoning)
        {
            return;
        }

        ReturnToOwner();
    }

    /// <summary>
    /// Đưa pet về trạng thái Dormant: reset toàn bộ cờ, dừng di chuyển và ẩn pet.
    /// </summary>
    private void EnterDormant()
    {
        RestoreSummonAnimatorSpeed();
        summonRevealPending = false;
        summonCastActive = false;
        summonPresentationAnimationStarted = false;
        recallLocked = false;
        IsSummoned = false;
        target = null;
        hasPendingHit = false;
        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
        skillAnimationUntil = 0f;
        state = PetState.Dormant;
        hasRoamDestination = false;
        lastRoamDirection = Vector3.zero;
        directStuckSince = 0f;
        StopMovement(false);
        SetWorldPresence(false);
    }


    // ======================================================================
    // TÌM CHỖ ĐỨNG AN TOÀN
    // ======================================================================
    /// <summary>
    /// Đặt pet tại vị trí yêu cầu nếu có mặt đất đi được và đủ chỗ trống.
    /// Ưu tiên đặt lên NavMesh nếu bật; nếu không thì đặt trực tiếp transform.
    /// </summary>
    private bool TryPlaceForSummon(Vector3 requestedPosition)
    {
        if (!TryFindWalkableGround(requestedPosition, 0.65f, out Vector3 groundPoint)
            || !HasRoomAt(groundPoint))
        {
            return false;
        }

        // Thử đặt lên NavMesh.
        if (useNavMeshWhenAvailable && TryPlaceAgentOnNavMesh(groundPoint, 0.75f))
        {
            return true;
        }

        // Không dùng NavMesh -> tắt agent và đặt vị trí trực tiếp.
        DisableNavigationAgent();

        transform.position = groundPoint;
        return true;
    }

    /// <summary>
    /// Tìm vị trí an toàn gần chủ. Thứ tự thử:
    /// 1) followOffset  2) các SafeSpotFallbackOffsets  3) (nếu expandedSearch) 2 vòng tròn bán kính 2.6m và 4m, mỗi vòng 12 điểm.
    /// </summary>
    private bool TryFindSafeSpotNearOwner(out Vector3 spot, float ownerGroundTolerance = 1f,
        bool expandedSearch = false)
    {
        spot = default;
        if (owner == null || !TryFindWalkableGround(owner.position, ownerGroundTolerance,
                out Vector3 ownerGround))
        {
            return false;
        }

        // 1) Thử vị trí follow mặc định.
        if (TryResolveSafeSpot(followOffset, ownerGround.y, out spot))
        {
            return true;
        }

        // 2) Thử các vị trí dự phòng.
        for (int i = 0; i < SafeSpotFallbackOffsets.Length; i++)
        {
            if (TryResolveSafeSpot(SafeSpotFallbackOffsets[i], ownerGround.y, out spot))
            {
                return true;
            }
        }

        // 3) Tìm kiếm mở rộng.
        if (expandedSearch)
        {
            // A fixed offset can land inside scenery when a save first loads.
            // Search nearby walkable ground rather than hiding the pet forever.
            for (int ring = 0; ring < 2; ring++)
            {
                float radius = ring == 0 ? 2.6f : 4f;
                for (int step = 0; step < 12; step++)
                {
                    float angle = step * Mathf.PI / 6f;
                    Vector3 offset = new Vector3(Mathf.Sin(angle) * radius, 0f,
                        Mathf.Cos(angle) * radius);
                    if (TryResolveSafeSpot(offset, ownerGround.y, out spot)) return true;
                }
            }
        }

        return false;
    }

    // Đổi offset local của chủ sang toạ độ thế giới, rồi kiểm tra có đất và đủ chỗ trống.
    private bool TryResolveSafeSpot(Vector3 localOffset, float groundHeight, out Vector3 spot)
    {
        Vector3 candidate = owner.TransformPoint(localOffset);
        candidate.y = groundHeight;
        if (TryFindWalkableGround(candidate, 0.6f, out spot) && HasRoomAt(spot))
        {
            return true;
        }

        spot = default;
        return false;
    }

    /// <summary>
    /// Kiểm tra vị trí có đủ chỗ cho thân pet không (OverlapCapsule).
    /// Bỏ qua: chính pet, chủ, pet khác, mặt đất đang đỡ pet, và vật thấp hơn bậc thềm tối đa.
    /// </summary>
    private bool HasRoomAt(Vector3 point)
    {
        int count = Physics.OverlapCapsuleNonAlloc(point + Vector3.up * 0.45f,
            point + Vector3.up * 1.2f, Mathf.Max(0.25f, directBodyRadius),
            presenceHits, directObstacleMask, QueryTriggerInteraction.Ignore);
        // Mảng đệm bị đầy -> có thể bỏ sót va chạm, coi như không an toàn.
        if (count == presenceHits.Length)
        {
            return false;
        }

        // Có collider nào thật sự chắn chỗ đứng thì thất bại.
        for (int i = 0; i < count; i++)
        {
            Collider candidate = presenceHits[i];
            if (candidate != null && !IsIgnoredDirectObstacle(candidate)
                && !IsSupportingGround(candidate, point)
                && candidate.bounds.max.y > point.y + directMaxStepHeight)
            {
                return false;
            }
        }

        return true;
    }

    // Collider này có phải là mặt đất đang đỡ điểm point không (raycast từ trên xuống).
    private bool IsSupportingGround(Collider collider, Vector3 point)
    {
        Ray ray = new Ray(point + Vector3.up, Vector3.down);
        return collider.Raycast(ray, out RaycastHit hit, 1.5f)
            && hit.normal.y >= directMinGroundNormal
            && Mathf.Abs(hit.point.y - point.y) <= 0.2f;
    }


    // ======================================================================
    // ẨN / HIỆN PET TRONG THẾ GIỚI
    // ======================================================================
    // Lưu trạng thái bật/tắt ban đầu của mọi renderer (trừ icon map) và collider con.
    private void CacheWorldPresence()
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.GetComponentInParent<AAMAP.MapIcon>(true) == null
                && !rendererEnabledStates.ContainsKey(renderer))
            {
                rendererEnabledStates.Add(renderer, renderer.enabled);
            }
        }

        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && !colliderEnabledStates.ContainsKey(collider))
            {
                colliderEnabledStates.Add(collider, collider.enabled);
            }
        }

    }

    // Ẩn/hiện toàn bộ pet: renderer, collider, animator, map marker. Khi ẩn thì tắt luôn NavMeshAgent.
    private void SetWorldPresence(bool visible)
    {
        CacheWorldPresence();
        foreach (KeyValuePair<Renderer, bool> entry in rendererEnabledStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = visible && entry.Value;
            }
        }

        foreach (KeyValuePair<Collider, bool> entry in colliderEnabledStates)
        {
            if (entry.Key != null)
            {
                entry.Key.enabled = visible && entry.Value;
            }
        }

        if (animator != null)
        {
            animator.enabled = visible && animatorInitiallyEnabled;
        }

        if (mapMarker != null)
        {
            mapMarker.enabled = visible && markerInitiallyEnabled;
        }

        if (!visible)
        {
            DisableNavigationAgent();
        }
    }


    // ======================================================================
    // QUÁ TRÌNH TRIỆU HỒI
    // ======================================================================
    /// <summary>
    /// Bắt đầu trạng thái Summoning: reset dữ liệu, dừng di chuyển,
    /// và phát animation spawn (trừ khi được hoãn cho hiệu ứng presentation).
    /// </summary>
    private void BeginSummon(bool deferSpawnAnimation)
    {
        if (state == PetState.Summoning)
        {
            return;
        }

        RestoreSummonAnimatorSpeed();
        recallLocked = false;
        nextNavMeshRetryAt = 0f;
        target = null;
        hasRoamDestination = false;
        directStuckSince = 0f;
        IsSummoned = true;
        state = PetState.Summoning;
        summonStartedAt = Time.time;
        summonRevealPending = deferSpawnAnimation;
        summonCastActive = false;
        summonPresentationAnimationStarted = false;
        summonAnimationDuration = spawnDuration;
        StopMovement(false);
        SetAnimatorFloat(speedParameter, 0f);
        SetAnimatorBool(movingParameter, false);
        if (!deferSpawnAnimation)
        {
            PlayAnimatorState(spawnStates, attackFade, true);
        }
    }

    /// <summary>
    /// Chạy mỗi frame khi đang Summoning: chờ animation xuất hiện xong rồi chuyển sang lang thang / đi theo.
    /// </summary>
    private void UpdateSummoning()
    {
        StopMovement(false);
        // Đang chờ script hiệu ứng -> chưa làm gì.
        if (summonRevealPending)
        {
            return;
        }

        // Dùng Cast Spell: kiểm tra animation đã chạy hết hoặc đã tự thoát ra sau đủ thời gian.
        if (summonCastActive)
        {
            AnimatorStateInfo castState = animator != null
                ? animator.GetCurrentAnimatorStateInfo(0) : default;
            bool castFinished = animator != null && !animator.IsInTransition(0)
                && castState.shortNameHash == Animator.StringToHash("Cast Spell")
                && castState.normalizedTime >= 0.98f;
            bool castExitedAfterDuration = animator != null && !animator.IsInTransition(0)
                && castState.shortNameHash != Animator.StringToHash("Cast Spell")
                && Time.time - summonStartedAt >= summonAnimationDuration;
            // Chưa xong và chưa quá timeout (tối thiểu 3s) -> tiếp tục chờ.
            if (!castFinished && !castExitedAfterDuration && Time.time - summonStartedAt
                < Mathf.Max(3f, summonAnimationDuration + 0.5f))
            {
                return;
            }

            // Quá timeout -> cảnh báo nhưng vẫn kết thúc để pet không kẹt mãi ở Summoning.
            if (!castFinished && !castExitedAfterDuration)
            {
                Debug.LogWarning("Cast Spell did not finish before the summon animation timeout.", this);
            }
        }
        // Dùng Spawn: chỉ cần đủ thời gian.
        else if (Time.time - summonStartedAt < summonAnimationDuration)
        {
            return;
        }

        // Kết thúc triệu hồi: khôi phục tốc độ animator, chuyển trạng thái, phát idle.
        summonCastActive = false;
        RestoreSummonAnimatorSpeed();
        state = roamAroundOwner ? PetState.RoamAroundOwner : PetState.FollowOwner;
        ScheduleNextRoam(0.25f);
        PlayAnimatorState(idleStates, locomotionFade, true);
        if (!roamAroundOwner)
        {
            MoveToFollowSpot();
        }
    }

    // Khôi phục animator.speed về giá trị trước khi bị ghi đè lúc triệu hồi.
    private void RestoreSummonAnimatorSpeed()
    {
        if (!summonAnimatorSpeedOverridden) return;
        if (animator != null) animator.speed = summonAnimatorSpeedBefore;
        summonAnimatorSpeedOverridden = false;
    }

    // Khi component bị tắt cũng phải khôi phục tốc độ animator.
    private void OnDisable()
    {
        RestoreSummonAnimatorSpeed();
    }


    // ======================================================================
    // LOGIC THEO TỪNG TRẠNG THÁI
    // ======================================================================
    // FollowOwner: chạy theo chủ, đến đủ gần thì chuyển sang lang thang.
    private void UpdateFollow()
    {
        float ownerDistance = FlatDistance(transform.position, owner.position);
        // Quá xa hoặc lệch độ cao quá 3m -> dịch chuyển tức thời.
        if (ownerDistance > teleportDistance
            || Mathf.Abs(transform.position.y - owner.position.y) > 3f)
        {
            if (TeleportNearOwner())
            {
                return;
            }
        }

        // Xa hơn followResumeDistance -> chạy tới vị trí follow.
        if (ownerDistance > leashDistance || ownerDistance > followResumeDistance)
        {
            MoveToFollowSpot();
        }
        // Đủ gần -> dừng lại, chuyển sang lang thang (nếu bật).
        else if (ownerDistance <= followDistance)
        {
            StopMovement(false);
            if (roamAroundOwner)
            {
                state = PetState.RoamAroundOwner;
                ScheduleNextRoam(0.2f);
            }
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    // RoamAroundOwner: lang thang quanh chủ, chủ đi xa thì chuyển sang follow.
    private void UpdateRoamAroundOwner()
    {
        if (owner == null)
        {
            StopMovement();
            return;
        }

        float ownerDistance = FlatDistance(transform.position, owner.position);
        // Quá xa -> dịch chuyển tức thời.
        if (ownerDistance > teleportDistance
            || Mathf.Abs(transform.position.y - owner.position.y) > 3f)
        {
            if (TeleportNearOwner())
            {
                return;
            }
        }

        // Chủ đi xa -> bỏ lang thang, chuyển sang follow.
        if (ownerDistance > leashDistance || ownerDistance > followResumeDistance)
        {
            hasRoamDestination = false;
            state = PetState.FollowOwner;
            MoveToFollowSpot();
            UpdateAnimator(GetCurrentSpeed());
            return;
        }

        // Đang có điểm lang thang -> đi tới đó, tới nơi thì nghỉ một lúc.
        if (hasRoamDestination)
        {
            MoveTo(roamDestination, activeRoamSpeed, roamPointTolerance);
            if (ReachedPoint(roamDestination, roamPointTolerance + 0.1f))
            {
                hasRoamDestination = false;
                StopMovement(false);
                ScheduleNextRoam();
            }

            UpdateAnimator(GetCurrentSpeed());
            return;
        }

        // Đang nghỉ -> đến giờ thì chọn điểm lang thang mới.
        StopMovement(false);
        if (Time.time >= nextRoamAt)
        {
            PickNextRoamPoint();
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    /// <summary>
    /// Chọn điểm lang thang tiếp theo (thử tối đa 8 lần).
    /// Hướng mới = hướng cũ xoay thêm 35-85 độ -> pet đi vòng quanh chủ thay vì nhảy loạn.
    /// </summary>
    private void PickNextRoamPoint()
    {
        if (owner == null || !TryFindWalkableGround(owner.position, 1f,
                out Vector3 ownerGround))
        {
            ScheduleNextRoam();
            return;
        }

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 direction;
            // Có hướng cũ -> xoay tiếp theo chiều orbit hiện tại.
            if (lastRoamDirection.sqrMagnitude > 0.001f)
            {
                float turn = Random.Range(35f, 85f) * roamOrbitSide;
                direction = Quaternion.Euler(0f, turn, 0f) * lastRoamDirection;
            }
            // Chưa có hướng -> chọn hướng ngẫu nhiên.
            else
            {
                Vector2 random = Random.insideUnitCircle.normalized;
                direction = new Vector3(random.x, 0f, random.y);
            }

            if (direction.sqrMagnitude < 0.001f)
            {
                continue;
            }

            // Chọn bán kính ngẫu nhiên và tính điểm đích, kiểm tra có đất và đủ chỗ.
            float radius = Random.Range(roamMinRadius, Mathf.Max(roamMinRadius, roamMaxRadius));
            Vector3 desired = owner.position + direction.normalized * radius;
            desired.y = ownerGround.y;
            if (!TryFindWalkableGround(desired, 0.65f, out desired)
                || !HasRoomAt(desired))
            {
                continue;
            }

            // Nếu dùng NavMesh thì kéo điểm về NavMesh gần nhất.
            if (useNavMeshWhenAvailable && agent != null && agent.enabled
                && NavMesh.SamplePosition(desired, out NavMeshHit hit, 2.5f,
                    NavMesh.AllAreas))
            {
                desired = hit.position;
            }

            roamDestination = desired;
            // Ngẫu nhiên chạy hay đi bộ.
            activeRoamSpeed = Random.value < roamRunChance ? roamRunSpeed : roamWalkSpeed;
            hasRoamDestination = FlatDistance(transform.position, roamDestination) > roamPointTolerance;
            if (hasRoamDestination)
            {
                lastRoamDirection = direction.normalized;
                // Sau 3-6 bước cùng chiều thì đổi chiều đi vòng.
                if (++roamStepsInDirection >= 3
                    && (roamStepsInDirection >= 6 || Random.value < 0.35f))
                {
                    roamOrbitSide = -roamOrbitSide;
                    roamStepsInDirection = 0;
                }
                return;
            }
        }

        ScheduleNextRoam();
    }

    // Hẹn thời điểm lang thang tiếp theo. Không truyền delay thì chọn ngẫu nhiên trong [roamIdleTimeMin, roamIdleTimeMax].
    private void ScheduleNextRoam(float initialDelay = -1f)
    {
        float delay = initialDelay >= 0f ? initialDelay : Random.Range(roamIdleTimeMin, Mathf.Max(roamIdleTimeMin, roamIdleTimeMax));
        nextRoamAt = Time.time + delay;
    }

    // MoveToPoint: đi tới điểm được ra lệnh, tới nơi thì quay về với chủ.
    private void UpdateMoveToPoint()
    {
        MoveTo(commandedPoint, commandSpeed, pointStoppingDistance);
        if (ReachedDestination(pointStoppingDistance + 0.1f))
        {
            ReturnToOwner();
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    // ChaseTarget: đuổi theo quái, vào tầm thì bắt đầu đánh.
    private void UpdateChaseTarget()
    {
        if (target == null || !target.IsAlive)
        {
            ReturnToOwner();
            return;
        }

        if (IsTargetInAttackRange())
        {
            BeginAttack();
            return;
        }

        if (Time.time >= nextAttackRepathAt || !HasMovePath())
        {
            MoveToAttackRange();
        }

        UpdateAnimator(GetCurrentSpeed());
    }

    // AttackTarget: đứng yên đánh quái, gây sát thương đúng thời điểm, rồi quyết định bước tiếp theo.
    private void UpdateAttackTarget()
    {
        StopMovement();

        if (target == null || !target.IsAlive)
        {
            SetAnimatorBool(attackingParameter, false);
            ReturnToOwner();
            return;
        }

        FacePoint(GetTargetPosition());

        // Đến thời điểm gây sát thương (và quái vẫn trong tầm) -> trừ máu; nếu quái chết thì nhận thưởng.
        if (hasPendingHit && Time.time >= pendingHitAt)
        {
            hasPendingHit = false;
            if (IsTargetInAttackRange())
            {
                DummyEnemy attackedEnemy = target;
                bool wasAlive = attackedEnemy != null && attackedEnemy.IsAlive;
                attackedEnemy?.TakeDamage(CalculateAttackDamage(), gameObject);
                if (wasAlive && attackedEnemy != null && !attackedEnemy.IsAlive)
                {
                    AwardDefeatExperience(attackedEnemy);
                }
            }
        }

        // Hết thời gian 1 đòn -> quyết định: quái chết thì về, quái chạy xa thì đuổi, hết cooldown thì đánh tiếp, còn lại thì đứng idle.
        if (Time.time - attackStartedAt >= attackStateTime)
        {
            SetAnimatorBool(attackingParameter, false);

            if (!target.IsAlive)
            {
                ReturnToOwner();
            }
            else if (!IsTargetInAttackRange())
            {
                state = PetState.ChaseTarget;
                nextAttackRepathAt = 0f;
            }
            else if (Time.time >= nextAttackAt)
            {
                BeginAttack();
            }
            else
            {
                PlayAnimatorState(idleStates, locomotionFade, false);
            }
        }
    }


    // ======================================================================
    // CHIẾN ĐẤU
    // ======================================================================
    /// <summary>
    /// Bắt đầu 1 đòn đánh: đặt các mốc thời gian, quay mặt về quái và phát animation tấn công.
    /// </summary>
    private void BeginAttack()
    {
        if (target == null || !target.IsAlive)
        {
            ReturnToOwner();
            return;
        }

        state = PetState.AttackTarget;
        attackStartedAt = Time.time;
        nextAttackAt = Time.time + attackCooldown;
        pendingHitAt = Time.time + attackHitDelay;
        hasPendingHit = true;

        StopMovement(false);
        FacePoint(GetTargetPosition());
        SetAnimatorBool(attackingParameter, true);

        // Ưu tiên dùng trigger 'Attack'; Animator không có trigger đó thì CrossFade thẳng vào state tấn công.
        if (!SetAnimatorTrigger(attackTriggerParameter))
        {
            PlayAnimatorState(attackStates, attackFade, true);
        }
    }

    // Tính điểm tiếp cận quái và di chuyển tới đó; hẹn lần tính lại tiếp theo.
    private void MoveToAttackRange()
    {
        attackDestination = GetAttackDestination();
        nextAttackRepathAt = Time.time + Mathf.Max(0.03f, attackRepathInterval);
        MoveTo(attackDestination, commandSpeed, Mathf.Min(pointStoppingDistance, attackApproachDistance));
    }


    // ======================================================================
    // DI CHUYỂN
    // ======================================================================
    // Di chuyển tới vị trí follow (theo followOffset của chủ).
    // Lưu ý: stopDistance = followDistance nên pet có thể dừng cách điểm follow tới 4m.
    private void MoveToFollowSpot()
    {
        if (owner == null)
        {
            return;
        }

        Vector3 desired = owner.TransformPoint(followOffset);
        MoveTo(desired, followSpeed, followDistance);
    }

    /// <summary>
    /// Hàm di chuyển chung: thử NavMesh trước, không được thì di chuyển trực tiếp, cả 2 đều không được thì đứng yên.
    /// </summary>
    private void MoveTo(Vector3 destination, float speed, float stopDistance)
    {
        if (TryMoveWithAgent(destination, speed, stopDistance))
        {
            return;
        }

        if (useDirectMoveFallback)
        {
            MoveDirect(destination, speed, stopDistance);
            return;
        }

        StopMovement();
    }

    /// <summary>
    /// Di chuyển bằng NavMeshAgent. Trả về false nếu không dùng được (để chuyển sang di chuyển trực tiếp).
    /// </summary>
    private bool TryMoveWithAgent(Vector3 destination, float speed, float stopDistance)
    {
        if (!useNavMeshWhenAvailable || agent == null || !agent.gameObject.activeInHierarchy)
        {
            return false;
        }

        // Agent đang tắt -> thử bật lại trên NavMesh.
        if (!agent.enabled && !TryEnableAgentOnNavMesh())
        {
            return false;
        }

        // Agent không nằm trên NavMesh -> thử warp về NavMesh gần nhất.
        if (!agent.isOnNavMesh)
        {
            if (!TryWarpToNavMesh(transform.position, 2.5f))
            {
                DisableNavigationAgent();
                return false;
            }
        }

        // Kéo điểm đích về NavMesh gần nhất.
        Vector3 navDestination = destination;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(destination, out hit, 2.5f, NavMesh.AllAreas))
        {
            navDestination = hit.position;
        }

        // Cập nhật thông số agent.
        agent.speed = speed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = Mathf.Max(stopDistance, agent.radius * 0.35f);
        // Chỉ tính lại đường khi cần thiết (đích thay đổi đủ nhiều VÀ đã qua thời gian refresh) để tiết kiệm hiệu năng.
        float destinationThreshold = Mathf.Max(0.01f, navDestinationThreshold);
        bool destinationChanged = !navDestinationInitialized
            || (navDestination - lastNavDestination).sqrMagnitude
                > destinationThreshold * destinationThreshold;
        bool needsPath = agent.isStopped || (!agent.hasPath && !agent.pathPending);
        bool canRefreshDestination = Time.time >= nextNavPathRefreshAt;

        agent.isStopped = false;
        // Gửi đích mới cho agent; thất bại thì tắt agent.
        if (needsPath || (destinationChanged && canRefreshDestination))
        {
            if (!agent.SetDestination(navDestination))
            {
                DisableNavigationAgent();
                return false;
            }
            lastNavDestination = navDestination;
            navDestinationInitialized = true;
            nextNavPathRefreshAt = Time.time + Mathf.Max(0.02f, navPathRefreshInterval);
        }

        // Agent không tự xoay (updateRotation = false) nên tự xoay theo hướng vận tốc.
        FaceVelocity(agent.desiredVelocity.sqrMagnitude > 0.01f ? agent.desiredVelocity : agent.velocity);
        return true;
    }


    // ======================================================================
    // THƯỞNG KHI HẠ QUÁI
    // ======================================================================
    /// <summary>
    /// Cộng EV (effort values) và EXP cho pet khi hạ quái.
    /// Nếu nhận EV thì yêu cầu lưu game và cập nhật HUD.
    /// </summary>
    private void AwardDefeatExperience(DummyEnemy defeatedEnemy)
    {
        if (defeatedEnemy == null)
        {
            return;
        }

        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        if (metadata == null) metadata = gameObject.AddComponent<PetCollectionMetadata>();
        // Cộng EV.
        bool gainedEffort = metadata.GrantEffortValues(defeatedEnemy.EffortYield);
        if (gainedEffort)
        {
            FindFirstObjectByType<PlayerSaveController>()?.RequestSave();
            FindFirstObjectByType<PetCommandHudProvider>()?.NotifyHudDataChanged();
        }
        // Quái không cho EXP thì dừng.
        if (defeatedEnemy.ExperienceReward <= 0) return;

        // Tìm hoặc tạo PetLevelUpService để cộng EXP.
        if (levelUpService == null)
        {
            levelUpService = FindFirstObjectByType<PetLevelUpService>();
        }

        if (levelUpService == null)
        {
            levelUpService = gameObject.AddComponent<PetLevelUpService>();
        }

        levelUpService.GrantBattleExperience(this, defeatedEnemy.ExperienceReward, out _);
    }

    /// <summary>
    /// Tính sát thương 1 đòn: (attackDamage + Attack) và có thể chí mạng.
    /// Chí mạng: nhân thêm (1 + CriticalDamage%).
    /// </summary>
    private float CalculateAttackDamage()
    {
        PetCollectionMetadata metadata = GetComponentInChildren<PetCollectionMetadata>(true);
        if (metadata == null) return Mathf.Max(0f, attackDamage);

        PetStatSnapshot stats = metadata.CurrentStats;
        float damage = Mathf.Max(0f, attackDamage + stats.Attack);
        // Random >= tỉ lệ chí mạng -> không chí mạng.
        if (Random.value >= stats.CriticalRate / 100f)
        {
            return damage;
        }

        return damage * (1f + stats.CriticalDamage / 100f);
    }


    // ======================================================================
    // NAVMESH HELPERS
    // ======================================================================
    // Thử bật agent trên NavMesh. Có giới hạn tần suất thử lại (navMeshRetryInterval).
    private bool TryEnableAgentOnNavMesh()
    {
        if (agent == null)
        {
            return false;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            return true;
        }

        DisableNavigationAgent();

        // Chưa tới lúc thử lại -> bỏ qua.
        if (Time.time < nextNavMeshRetryAt)
        {
            return false;
        }

        nextNavMeshRetryAt = Time.time + Mathf.Max(0.1f, navMeshRetryInterval);
        return TryPlaceAgentOnNavMesh(transform.position, 6f);
    }

    // Warp agent về NavMesh gần vị trí cho trước.
    private bool TryWarpToNavMesh(Vector3 position, float searchRadius)
    {
        return TryPlaceAgentOnNavMesh(position, searchRadius);
    }

    /// <summary>
    /// Đặt agent lên NavMesh gần vị trí cho trước.
    /// Thất bại nếu không tìm thấy NavMesh hoặc điểm NavMesh lệch độ cao quá 0.35m.
    /// </summary>
    private bool TryPlaceAgentOnNavMesh(Vector3 position, float searchRadius)
    {
        if (agent == null || !agent.gameObject.activeInHierarchy
            || !NavMesh.SamplePosition(position, out NavMeshHit hit,
                Mathf.Max(0.25f, searchRadius), agent.areaMask)
            || Mathf.Abs(hit.position.y - position.y) > 0.35f)
        {
            return false;
        }

        // Agent đang hoạt động -> dùng Warp.
        if (agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.Warp(hit.position))
            {
                return false;
            }
        }
        // Agent đang tắt -> đặt transform rồi bật agent.
        else
        {
            DisableNavigationAgent();

            transform.position = hit.position;
            agent.enabled = true;
            if (!agent.isOnNavMesh)
            {
                DisableNavigationAgent();
                return false;
            }
        }

        // Đứng yên, xoá đường đi cũ.
        agent.isStopped = true;
        agent.ResetPath();
        navDestinationInitialized = false;
        return true;
    }


    // ======================================================================
    // DI CHUYỂN TRỰC TIẾP (KHÔNG NAVMESH)
    // ======================================================================
    /// <summary>
    /// Tự di chuyển pet về phía đích từng frame, có né vật cản.
    /// Nếu bị kẹt quá 2.5s khi đang follow/lang thang thì dịch chuyển về gần chủ.
    /// </summary>
    private void MoveDirect(Vector3 destination, float speed, float stopDistance)
    {
        Vector3 current = transform.position;
        Vector3 flatDestination = new Vector3(destination.x, current.y, destination.z);
        Vector3 toDestination = flatDestination - current;

        // Đã đủ gần -> dừng.
        if (toDestination.sqrMagnitude <= stopDistance * stopDistance)
        {
            StopMovement();
            return;
        }

        // Quãng đường đi trong frame này (không vượt quá điểm dừng).
        float step = Mathf.Min(Mathf.Max(0f, speed) * Time.deltaTime,
            toDestination.magnitude - stopDistance);
        if (step <= 0f)
        {
            directStuckSince = 0f;
            StopMovement();
            return;
        }

        // Không tìm được bước đi nào hợp lệ -> tính thời gian kẹt.
        if (!TryChooseDirectStep(current, toDestination.normalized, step,
                out Vector3 next, out Vector3 direction))
        {
            if (directStuckSince <= 0f)
            {
                directStuckSince = Time.time;
            }
            else if (Time.time - directStuckSince > 2.5f
                && (state == PetState.FollowOwner || state == PetState.RoamAroundOwner))
            {
                TeleportNearOwner();
                directStuckSince = 0f;
            }

            StopMovement();
            return;
        }

        // Di chuyển thành công: cập nhật vị trí, xoay mặt, lưu tốc độ (cho animator).
        directStuckSince = 0f;
        transform.position = next;
        FaceDirection(direction);
        directMoveSpeed = step / Mathf.Max(Time.deltaTime, 0.0001f);
    }

    /// <summary>
    /// Chọn hướng bước đi: ưu tiên giữ góc né cũ (trong 0.55s), sau đó thử đi thẳng,
    /// rồi thử lệch 35/70/105/140 độ ở cả 2 bên (ưu tiên phía vừa né thành công).
    /// </summary>
    private bool TryChooseDirectStep(Vector3 current, Vector3 desiredDirection, float step,
        out Vector3 next, out Vector3 chosenDirection)
    {
        chosenDirection = desiredDirection;
        // Đang trong thời gian giữ góc né -> thử lại góc đó trước (tránh lắc qua lại trước vật cản).
        if (Time.time < directAvoidanceUntil && Mathf.Abs(directAvoidanceAngle) > 0.01f)
        {
            Vector3 heldDirection = Quaternion.Euler(0f, directAvoidanceAngle, 0f)
                * desiredDirection;
            if (TryDirectStep(current, heldDirection, step, out next))
            {
                chosenDirection = heldDirection;
                return true;
            }
        }

        // Thử đi thẳng tới đích.
        if (TryDirectStep(current, desiredDirection, step, out next))
        {
            directAvoidanceAngle = 0f;
            return true;
        }

        // Thử các góc né ở 2 bên.
        for (int sideIndex = 0; sideIndex < 2; sideIndex++)
        {
            int side = sideIndex == 0 ? directAvoidanceSide : -directAvoidanceSide;
            for (int angleIndex = 0; angleIndex < DirectAvoidanceAngles.Length; angleIndex++)
            {
                float avoidanceAngle = side * DirectAvoidanceAngles[angleIndex];
                Vector3 candidateDirection = Quaternion.Euler(0f, avoidanceAngle, 0f)
                    * desiredDirection;
                if (!TryDirectStep(current, candidateDirection, step, out next))
                {
                    continue;
                }

                directAvoidanceSide = side;
                directAvoidanceAngle = avoidanceAngle;
                directAvoidanceUntil = Time.time + 0.55f;
                chosenDirection = candidateDirection;
                return true;
            }
        }

        // Mọi hướng đều bị chặn.
        next = current;
        return false;
    }

    /// <summary>
    /// Kiểm tra 1 bước đi theo hướng direction có hợp lệ không:
    /// SphereCast dò vật cản phía trước, sau đó raycast xuống để bám mặt đất.
    /// </summary>
    private bool TryDirectStep(Vector3 current, Vector3 direction, float step, out Vector3 next)
    {
        next = current;
        float radius = Mathf.Max(0.1f, directBodyRadius);
        Vector3 probeOrigin = current + Vector3.up
            * Mathf.Max(directProbeHeight, radius + 0.1f);
        float lookAhead = step + Mathf.Max(radius, directLookAhead);
        // Dò vật cản bằng SphereCast.
        int count = Physics.SphereCastNonAlloc(probeOrigin, radius, direction,
            directObstacleHits, lookAhead, directObstacleMask, QueryTriggerInteraction.Ignore);
        // Mảng đệm đầy -> không chắc chắn, coi như bị chặn.
        if (count == directObstacleHits.Length)
        {
            return false;
        }

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = directObstacleHits[i];
            Collider obstacle = hit.collider;
            if (obstacle == null || IsIgnoredDirectObstacle(obstacle))
            {
                continue;
            }

            // Va chạm có pháp tuyến hướng lên và thấp hơn bậc thềm -> là đất đi được, không phải vật cản.
            bool walkableGround = hit.normal.y >= directMinGroundNormal
                && hit.point.y <= current.y + directMaxStepHeight;
            if (!walkableGround)
            {
                return false;
            }
        }

        // Tính vị trí mới và bám xuống mặt đất.
        Vector3 candidate = current + direction * step;
        if (snapFallbackToGround)
        {
            if (!TryFindWalkableGround(candidate, directMaxStepHeight, out Vector3 groundPoint))
            {
                return false;
            }

            candidate.y = groundPoint.y;
        }

        next = candidate;
        return true;
    }

    // Các collider KHÔNG coi là vật cản: chính pet, chủ, pet khác, và quái đang là mục tiêu.
    private bool IsIgnoredDirectObstacle(Collider obstacle)
    {
        if (obstacle.transform.IsChildOf(transform))
        {
            return true;
        }

        if (owner != null && obstacle.transform.IsChildOf(owner))
        {
            return true;
        }

        PetController pet = obstacle.GetComponentInParent<PetController>();
        if (pet != null)
        {
            return true;
        }

        DummyEnemy enemy = obstacle.GetComponentInParent<DummyEnemy>();
        return enemy != null && enemy == target;
    }


    // ======================================================================
    // HÀM TIỆN ÍCH
    // ======================================================================
    // Dừng di chuyển (cả agent lẫn di chuyển trực tiếp); tuỳ chọn cập nhật animator về idle.
    private void StopMovement(bool updateAnimation = true)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            if (!agent.isStopped)
            {
                agent.isStopped = true;
            }

            if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        navDestinationInitialized = false;
        directMoveSpeed = 0f;
        if (state != PetState.AttackTarget)
        {
            if (updateAnimation && state != PetState.Dormant && state != PetState.Summoning)
            {
                UpdateAnimator(0f);
            }
        }
    }

    // Đã tới điểm được ra lệnh chưa.
    private bool ReachedDestination(float tolerance)
    {
        return ReachedPoint(commandedPoint, tolerance);
    }

    // Đã tới 1 điểm chưa: dùng remainingDistance của agent nếu có đường đi, không thì đo khoảng cách phẳng.
    private bool ReachedPoint(Vector3 point, float tolerance)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath)
        {
            return !agent.pathPending && agent.remainingDistance <= tolerance;
        }

        return FlatDistance(transform.position, point) <= tolerance;
    }

    // Dịch chuyển tức thời pet tới chỗ an toàn gần chủ.
    private bool TeleportNearOwner()
    {
        if (!TryFindSafeSpotNearOwner(out Vector3 spot))
        {
            return false;
        }

        if (useNavMeshWhenAvailable && TryPlaceAgentOnNavMesh(spot, 0.75f))
        {
            StopMovement();
            return true;
        }

        DisableNavigationAgent();

        transform.position = spot;
        StopMovement();
        return true;
    }

    // Tắt NavMeshAgent an toàn (dừng và xoá đường đi trước khi tắt).
    private void DisableNavigationAgent()
    {
        if (agent == null)
        {
            return;
        }

        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            if (agent.hasPath)
            {
                agent.ResetPath();
            }
        }

        if (agent.enabled)
        {
            agent.enabled = false;
        }

        navDestinationInitialized = false;
    }

    // Vị trí ngắm của mục tiêu (nếu không có mục tiêu thì trả về vị trí pet).
    private Vector3 GetTargetPosition()
    {
        return target != null ? target.TargetPosition : transform.position;
    }

    // Điểm gần nhất trên collider của mục tiêu so với pet.
    private Vector3 GetTargetClosestPoint()
    {
        return target != null ? target.ClosestPoint(transform.position) : transform.position;
    }

    // Điểm pet cần đứng để đánh: từ bề mặt quái lùi ra 1 đoạn attackApproachDistance về phía pet.
    private Vector3 GetAttackDestination()
    {
        if (target == null)
        {
            return transform.position;
        }

        Vector3 targetPosition = GetTargetPosition();
        Vector3 closestPoint = GetTargetClosestPoint();
        Vector3 awayFromTarget = transform.position - targetPosition;
        awayFromTarget.y = 0f;

        if (awayFromTarget.sqrMagnitude <= 0.001f)
        {
            awayFromTarget = -transform.forward;
            awayFromTarget.y = 0f;
        }

        return closestPoint + awayFromTarget.normalized * Mathf.Max(0.05f, attackApproachDistance);
    }

    // Quái có trong tầm đánh không (đo tới bề mặt gần nhất, bỏ qua trục Y).
    private bool IsTargetInAttackRange()
    {
        if (target == null || !target.IsAlive)
        {
            return false;
        }

        return FlatDistance(transform.position, GetTargetClosestPoint()) <= attackRange + attackReachPadding;
    }

    // Agent đang có đường đi và đang chạy không.
    private bool HasMovePath()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return false;
        }

        return agent.hasPath && !agent.isStopped;
    }

    // Tốc độ hiện tại: lấy từ agent nếu đang dùng, không thì từ di chuyển trực tiếp.
    private float GetCurrentSpeed()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            return agent.velocity.magnitude;
        }

        return directMoveSpeed;
    }

    /// <summary>
    /// Raycast từ trên (1.5m) xuống (dài 4m) để tìm mặt đất đi được gần độ cao mong muốn nhất.
    /// Bỏ qua: collider bị ignore, rigidbody đang chuyển động, mặt quá dốc, lệch độ cao quá maxHeightChange.
    /// </summary>
    private bool TryFindWalkableGround(Vector3 position, float maxHeightChange,
        out Vector3 groundPoint)
    {
        groundPoint = position;
        Vector3 origin = position + Vector3.up * 1.5f;
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, groundHits, 4f, groundMask, QueryTriggerInteraction.Ignore);
        if (count <= 0 || count == groundHits.Length)
        {
            return false;
        }

        // Chọn điểm chạm có độ cao gần nhất với position.
        int best = -1;
        float bestHeightDelta = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = groundHits[i];
            if (hit.collider == null || IsIgnoredDirectObstacle(hit.collider)
                || (hit.collider.attachedRigidbody != null
                    && !hit.collider.attachedRigidbody.isKinematic)
                || hit.normal.y < directMinGroundNormal
                || Mathf.Abs(hit.point.y - position.y) > maxHeightChange)
            {
                continue;
            }

            float heightDelta = Mathf.Abs(hit.point.y - position.y);
            if (heightDelta >= bestHeightDelta)
            {
                continue;
            }

            best = i;
            bestHeightDelta = heightDelta;
        }

        if (best < 0)
        {
            return false;
        }

        groundPoint.y = groundHits[best].point.y;
        return true;
    }


    // ======================================================================
    // XOAY MẶT
    // ======================================================================
    // Xoay theo hướng vận tốc (bỏ trục Y).
    private void FaceVelocity(Vector3 velocity)
    {
        velocity.y = 0f;
        if (velocity.sqrMagnitude > 0.001f)
        {
            FaceDirection(velocity.normalized);
        }
    }

    // Xoay mượt về phía 1 điểm.
    private void FacePoint(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
        {
            FaceDirection(direction.normalized);
        }
    }

    // Xoay NGAY LẬP TỨC về phía 1 điểm (không mượt).
    private void SnapFacingToPoint(Vector3 point)
    {
        Vector3 direction = point - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    // Xoay mượt về hướng cho trước. Dùng 1 - e^(-k*dt) để tốc độ xoay không phụ thuộc FPS.
    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
    }


    // ======================================================================
    // CẤU HÌNH
    // ======================================================================
    // Cấu hình NavMeshAgent. Agent không tự xoay (code tự xoay) và không tự đi qua OffMeshLink.
    private void ConfigureAgent()
    {
        if (agent == null)
        {
            return;
        }

        if (!useNavMeshWhenAvailable)
        {
            DisableNavigationAgent();
        }

        agent.updateRotation = false;
        agent.speed = followSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = followDistance;
        agent.radius = Mathf.Max(agent.radius, minimumNavigationRadius);
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.autoTraverseOffMeshLink = false;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
    }

    // Cấu hình animator: tắt root motion (code tự di chuyển), quét và cache các parameter có sẵn.
    private void ConfigureAnimator()
    {
        if (animator == null)
        {
            return;
        }

        animator.applyRootMotion = false;
        floatParameters.Clear();
        boolParameters.Clear();
        triggerParameters.Clear();

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            int hash = Animator.StringToHash(parameter.name);
            if (parameter.type == AnimatorControllerParameterType.Float)
            {
                floatParameters.Add(hash);
            }
            else if (parameter.type == AnimatorControllerParameterType.Bool)
            {
                boolParameters.Add(hash);
            }
            else if (parameter.type == AnimatorControllerParameterType.Trigger)
            {
                triggerParameters.Add(hash);
            }
        }
    }


    // ======================================================================
    // ANIMATOR HELPERS
    // ======================================================================
    // Cập nhật animation di chuyển/đứng yên theo tốc độ.
    // Bỏ qua khi đang skill, đang đánh, đang Dormant hoặc Summoning.
    private void UpdateAnimator(float speed)
    {
        if (animator == null || IsSkillAnimationActive() || state == PetState.AttackTarget || state == PetState.Dormant || state == PetState.Summoning)
        {
            return;
        }

        bool moving = speed > 0.08f || (agent != null && agent.enabled && agent.isOnNavMesh && agent.hasPath && !agent.isStopped);
        SetAnimatorFloat(speedParameter, speed);
        SetAnimatorBool(movingParameter, moving);
        PlayAnimatorState(moving ? moveStates : idleStates, locomotionFade, false);
    }

    /// <summary>
    /// Phát state đầu tiên tồn tại trong danh sách candidates.
    /// restart = false: nếu state đó đang phát thì không phát lại.
    /// </summary>
    private bool PlayAnimatorState(string[] candidates, float fade, bool restart)
    {
        if (animator == null || candidates == null)
        {
            return false;
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            // Đang phát đúng state này rồi -> không làm gì.
            if (!restart && currentAnimatorState == candidate)
            {
                return true;
            }

            int hash;
            if (!TryGetStateHash(candidate, out hash))
            {
                continue;
            }

            currentAnimatorState = candidate;
            // Crossfade sang state mới; restart thì phát từ đầu.
            animator.CrossFadeInFixedTime(hash, fade, 0, restart ? 0f : float.NegativeInfinity);
            return true;
        }

        return false;
    }

    // Animator có ít nhất 1 state trong danh sách không.
    private bool HasAnimatorState(string[] candidates)
    {
        if (animator == null || candidates == null)
        {
            return false;
        }

        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            int hash;
            if (TryGetStateHash(candidate, out hash))
            {
                return true;
            }
        }

        return false;
    }

    // Hết thời gian windup thì phát animation skill đang chờ.
    private void TryStartPendingSkillAnimation()
    {
        if (!hasPendingSkillAnimation || Time.time < pendingSkillStartAt)
        {
            return;
        }

        PlayAnimatorState(pendingSkillCandidates, pendingSkillFade, true);
        hasPendingSkillAnimation = false;
        pendingSkillCandidates = null;
    }

    // Lấy hash của state: thử 'Base Layer.<tên>' trước, rồi tới '<tên>'.
    private bool TryGetStateHash(string stateName, out int hash)
    {
        string fullName = "Base Layer." + stateName;
        hash = Animator.StringToHash(fullName);
        if (animator.HasState(0, hash))
        {
            return true;
        }

        hash = Animator.StringToHash(stateName);
        return animator.HasState(0, hash);
    }

    // Đang trong thời gian diễn skill không.
    private bool IsSkillAnimationActive()
    {
        return Time.time < skillAnimationUntil;
    }

    // Set parameter float/bool/trigger chỉ khi Animator có parameter đó (tránh warning).
    private void SetAnimatorFloat(string parameterName, float value)
    {
        int hash = Animator.StringToHash(parameterName);
        if (floatParameters.Contains(hash))
        {
            animator.SetFloat(hash, value, 0.08f, Time.deltaTime);
        }
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        int hash = Animator.StringToHash(parameterName);
        if (boolParameters.Contains(hash))
        {
            animator.SetBool(hash, value);
        }
    }

    private bool SetAnimatorTrigger(string parameterName)
    {
        int hash = Animator.StringToHash(parameterName);
        if (!triggerParameters.Contains(hash))
        {
            return false;
        }

        animator.ResetTrigger(hash);
        animator.SetTrigger(hash);
        return true;
    }

    // Khoảng cách trên mặt phẳng XZ (bỏ qua độ cao).
    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // Đảm bảo pet có MapMarker (tạo MapIcon nếu chưa có) và cấu hình nó là marker loại Pet.
    private void EnsureMapMarker()
    {
        if (mapMarker == null)
        {
            mapMarker = GetComponent<MapMarker>();
        }

        if (mapMarker == null)
        {
            mapMarker = gameObject.AddComponent<MapIcon>();
        }

        mapMarker.ConfigureRuntime(MapMarkerType.Pet, gameObject.name, gameObject.name, null, default, true, true);
    }
}