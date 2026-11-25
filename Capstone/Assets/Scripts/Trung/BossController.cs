using System;
using System.Collections;
using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.Skills;
using Capstone.Game.UISystem;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

// AI riêng cho boss (vd. Detale). Gắn lên object boss (object có DummyEnemy).
// - Đứng yên (Idle) ở nhà. Pet của Player tới gần -> đuổi theo (Walk) -> đánh thường (Attack).
// - Có danh sách chiêu riêng (Use Skill): nổ quanh người, chém phía trước, bắn đạn, đập xuống chỗ pet.
// - Bị đánh -> Damage (có hồi chiêu, không bị khựng khi đang ra đòn). Hết máu -> Die, không hồi sinh, không bắt được.
// - Gọi state trong Animator bằng TÊN, không cần Parameters hay Animation Profile.
// Máu, thanh máu, số sát thương, phần thưởng... vẫn dùng DummyEnemy của game nên pet đánh boss như quái thường.
[DisallowMultipleComponent]
[RequireComponent(typeof(DummyEnemy))]
public class BossController : MonoBehaviour
{
    public enum SkillShape
    {
        [Tooltip("Nổ quanh boss")] AroundBoss,
        [Tooltip("Chém / phun hình nón phía trước")] FrontCone,
        [Tooltip("Bắn đạn bay tới pet")] Projectile,
        [Tooltip("Hiện vòng cảnh báo dưới chân pet rồi nổ")] TargetGround
    }

    [Serializable]
    public class BossSkill
    {
        [Tooltip("Kéo file chiêu của pet vào (Skill_...). Hiệu ứng, âm thanh, kiểu chiêu, tầm, sức mạnh, hồi chiêu sẽ lấy từ đó")]
        public PetSkillDefinition petSkill;
        [Tooltip("Bật = các ô bên dưới bị thay bằng số liệu của chiêu pet. Tắt = tự chỉnh tay")]
        public bool useValuesFromPetSkill = true;
        public string skillName = "Chiêu";
        [Tooltip("Tên state trong Animator")]
        public string animatorState = "Use Skill";
        public SkillShape shape = SkillShape.AroundBoss;
        [Min(0f)] public float cooldown = 8f;
        [Tooltip("Pet cách boss tối đa bao xa thì mới dùng chiêu này")]
        [Min(0.5f)] public float useRange = 6f;
        [Tooltip("Sát thương = Attack Damage x số này")]
        [Min(0f)] public float damageMultiplier = 2f;
        public CreatureDamageKind damageKind = CreatureDamageKind.Magical;
        [Tooltip("Bán kính vùng nổ / tầm chém / vùng đập")]
        [Min(0.2f)] public float radius = 5f;
        [Tooltip("Chỉ dùng cho FrontCone: độ rộng góc chém")]
        [Range(10f, 180f)] public float coneAngle = 90f;
        [Tooltip("Thời điểm ra đòn trong animation (0 = đầu, 1 = cuối)")]
        [Range(0.05f, 0.95f)] public float hitTime = 0.45f;
        [Tooltip("Chỉ dùng cho TargetGround: vòng cảnh báo hiện bao lâu rồi mới nổ")]
        [Min(0f)] public float warningTime = 1f;
        [Tooltip("Chỉ dùng cho Projectile")]
        [Min(1f)] public float projectileSpeed = 14f;
        [Min(0.1f)] public float projectileRadius = 0.7f;

        [Header("Hiệu ứng (tùy chọn)")]
        [Tooltip("Hiện ở boss lúc bắt đầu tung chiêu")]
        public GameObject castEffect;
        [Tooltip("Hiện ở chỗ trúng / chỗ nổ")]
        public GameObject hitEffect;
        [Tooltip("Hình viên đạn (Projectile). Để trống = quả cầu đơn giản")]
        public GameObject projectilePrefab;
        public AudioClip castSound;
        public AudioClip hitSound;

        [NonSerialized] public float readyAt;
        [NonSerialized] public float projectileScale = 1f;
        // Lấy từ cue Impact của pet (giống cách pet đặt hiệu ứng).
        [NonSerialized] public Vector3 hitOffset;
        [NonSerialized] public Vector3 hitEuler;
        [NonSerialized] public float hitScale = 1f;
        [NonSerialized] public bool breath;
        [NonSerialized] public AudioClip releaseSound;
        [NonSerialized] public float hitLifetime = 3f;    // Hiệu ứng chạy bao lâu rồi tắt (lấy từ chiêu pet).
        [NonSerialized] public float castLifetime = 3f;   // Tiếng lúc tung chiêu ra (bắn đạn, phun...)   // Phun từ miệng (hơi thở rồng, phun lửa...)
    }

    enum Mode { Idle, Chase, Return, Busy, Dead }

    [Header("Miệng (chiêu phun)")]
    [Tooltip("Kéo xương đầu / miệng của boss vào (vd. Bip001 Head). Để trống = tự tính phía trước mặt boss")]
    [SerializeField] Transform mouthPoint;
    [Tooltip("Bật = tự phóng to / thu nhỏ hiệu ứng phun cho dài đúng bằng tầm chiêu (Radius)")]
    [SerializeField] bool fitBreathToRange = true;
    [Tooltip("Bật = lửa phun bắt đầu ở Hit Time của chiêu và TẮT khi animation chiêu chạy tới Breath End Time (không theo thời gian của chiêu pet)")]
    [SerializeField] bool breathEndsWithAnimation = true;
    [Tooltip("Tắt lửa khi animation chiêu chạy tới đâu (0.5 = nửa animation, 1 = hết animation)")]
    [SerializeField, Range(0.1f, 1f)] float breathEndTime = 0.95f;

    [Header("Animator (tên state)")]
    [SerializeField] Animator animator;
    [SerializeField] string idleState = "Idle";
    [SerializeField] string walkState = "Walk";
    [SerializeField] string attackState = "Attack";
    [SerializeField] string damageState = "Damage";
    [SerializeField] string dieState = "Die";
    [Tooltip("State gầm. Để trống hoặc Animator không có state này thì boss chỉ phát tiếng gầm")]
    [SerializeField] string roarState = "Roar";
    [SerializeField, Min(0f)] float crossFade = 0.12f;

    [Header("Gầm (Roar)")]
    [SerializeField] AudioClip roarSound;
    [SerializeField, Range(0f, 3f)] float roarVolume = 1.5f;
    [Tooltip("Gầm 1 lần khi vừa phát hiện pet / bắt đầu trận")]
    [SerializeField] bool roarOnAggro = true;
    [Tooltip("Sau mỗi đòn đánh / chiêu, tỉ lệ boss gầm lên (0.25 = 25%)")]
    [SerializeField, Range(0f, 1f)] float roarChance = 0.2f;
    [Tooltip("2 lần gầm cách nhau ít nhất bấy nhiêu giây")]
    [SerializeField, Min(0f)] float roarCooldown = 15f;

    [Header("Tiếng bước chân / đánh thường")]
    [Tooltip("Kéo 1 hoặc nhiều tiếng bước chân, boss phát ngẫu nhiên cho đỡ nhàm")]
    [SerializeField] AudioClip[] footstepSounds;
    [Tooltip("Mấy giây 1 bước khi đi (khớp với nhịp chân của animation Walk)")]
    [SerializeField, Min(0.1f)] float footstepInterval = 0.6f;
    [SerializeField, Range(0f, 3f)] float footstepVolume = 1f;
    [Tooltip("Tiếng lúc vung đòn (đầu animation Attack)")]
    [SerializeField] AudioClip attackSwingSound;
    [Tooltip("Tiếng lúc đòn trúng pet")]
    [SerializeField] AudioClip attackHitSound;
    [SerializeField, Range(0f, 3f)] float attackVolume = 1f;

    [Header("Tên trên thanh máu")]
    [SerializeField] string displayName = "Detale";
    [Tooltip("Cấp hiện cạnh tên. 0 = không hiện cấp")]
    [SerializeField, Min(0)] int level = 30;

    [Header("Chỉ số")]
    [Tooltip("Bật = đặt lại máu boss bằng Max Health bên dưới")]
    [SerializeField] bool overrideHealth = true;
    [SerializeField, Min(1f)] float maxHealth = 3000f;
    [Tooltip("Sát thương một đòn đánh thường")]
    [SerializeField, Min(0f)] float attackDamage = 60f;
    [SerializeField, Range(0f, 1f)] float critChance = 0.1f;
    [SerializeField, Min(1f)] float critMultiplier = 1.5f;

    [Header("Di chuyển")]
    [SerializeField, Min(0.1f)] float moveSpeed = 3.2f;
    [SerializeField, Min(30f)] float turnSpeed = 300f;
    [Header("Vùng của boss (cố định quanh chỗ đặt boss, không đi theo boss)")]
    [Tooltip("Bán kính vùng của boss, tính từ chỗ bạn đặt boss. Boss chỉ đánh và chỉ đuổi pet ở TRONG vùng này. "
        + "Pet ra khỏi vùng -> boss bỏ đuổi, quay về chỗ cũ. Vòng tròn đỏ cam trong Scene")]
    [SerializeField, Min(2f)] float zoneRadius = 15f;
    [Tooltip("Pet trong vùng VÀ cách boss không quá khoảng này thì boss mới bắt đầu đánh. Vòng vàng quanh boss")]
    [SerializeField, Min(1f)] float aggroRange = 10f;
    [Tooltip("Bỏ đuổi về nhà thì hồi đầy máu (người chơi không đánh lén rồi chạy được)")]
    [SerializeField] bool healWhenReturning = true;
    [SerializeField] bool snapToGround = true;

    [Header("Đánh thường")]
    [SerializeField, Min(0.5f)] float attackRange = 3f;
    [SerializeField, Min(0.2f)] float attackInterval = 2f;
    [SerializeField, Range(0.05f, 0.95f)] float attackHitTime = 0.4f;
    [SerializeField] CreatureDamageKind attackKind = CreatureDamageKind.Physical;

    [Header("Bị đánh")]
    [SerializeField] bool playDamageAnimation = true;
    [Tooltip("Mấy giây mới được phát Damage một lần (boss không bị khựng liên tục)")]
    [SerializeField, Min(0f)] float damageReactCooldown = 2.5f;

    [Header("Âm thanh boss")]
    [Tooltip("Độ to tiếng chiêu / đánh của boss (1 = gốc, 2 = gấp đôi)")]
    [SerializeField, Range(0f, 3f)] float soundVolume = 1.8f;
    [Tooltip("0 = nghe như nhau ở mọi chỗ, 1 = càng xa boss càng nhỏ")]
    [SerializeField, Range(0f, 1f)] float sound3D = 0.6f;
    [Tooltip("Đứng trong khoảng này quanh boss thì nghe to hết cỡ")]
    [SerializeField, Min(1f)] float soundFullDistance = 20f;
    [Tooltip("Xa hơn khoảng này thì không nghe nữa")]
    [SerializeField, Min(2f)] float soundMaxDistance = 70f;

    [Header("Chiêu")]
    [Tooltip("Chiêu đầu tiên chỉ được dùng sau bấy nhiêu giây kể từ lúc vào trận")]
    [SerializeField, Min(0f)] float firstSkillDelay = 4f;
    [Tooltip("Dùng xong 1 chiêu (hiệu ứng đã tan hết) thì phải đợi bấy nhiêu giây mới được dùng chiêu khác. Giữa lúc đó boss vẫn đánh thường")]
    [SerializeField, Min(0f)] float skillGap = 4f;
    [Tooltip("Bật = dùng lần lượt từng chiêu theo thứ tự trong danh sách, dùng hết 1 vòng rồi mới lặp lại.\nTắt = luôn ưu tiên chiêu mạnh nhất đã hồi xong")]
    [SerializeField] bool rotateSkills = true;
    [Tooltip("Chiêu tới lượt mà quá bấy nhiêu giây vẫn chưa dùng được (pet ngoài tầm...) thì bỏ qua, sang chiêu kế")]
    [SerializeField, Min(1f)] float skipSkillAfter = 8f;
    [SerializeField] List<BossSkill> skills = new List<BossSkill> { new BossSkill() };

    [Header("Vòng đỏ cảnh báo (chiêu Target Ground)")]
    [Tooltip("Bật = tự đo kích thước Hit Effect lúc vào game rồi chỉnh Radius (vòng đỏ + vùng sát thương) cho khớp hiệu ứng")]
    [SerializeField] bool fitRingToEffect = true;
    [Tooltip("Nhân thêm cho kích thước đo được (1 = khít hiệu ứng)")]
    [SerializeField, Range(0.5f, 1.5f)] float ringFitScale = 0.9f;
    [Tooltip("Tắt = vòng đỏ hiện ngay dưới chân pet.\nBật = pet đứng sát boss thì vòng bị đẩy ra xa (cách thân boss Ring Gap From Boss mét)")]
    [SerializeField] bool pushRingAwayFromBoss = false;
    [Tooltip("Chỉ dùng khi bật Push Ring Away From Boss")]
    [SerializeField, Min(0f)] float ringGapFromBoss = 2f;
    [SerializeField] Color ringColor = new Color(1f, 0.15f, 0.1f, 0.9f);
    [SerializeField, Min(0.02f)] float ringWidth = 0.15f;

    [Header("Chết")]
    [Tooltip("Bật = chạy hết animation Die, nằm yên một lúc rồi chìm dần xuống đất và biến mất. Tắt = giữ xác")]
    [SerializeField] bool vanishAfterDeath = true;
    [Tooltip("Xác nằm yên bao lâu (giây) sau khi animation Die chạy xong")]
    [SerializeField, Min(0f)] float corpseStayTime = 2f;
    [Tooltip("Thời gian chìm dần xuống đất cho tới khi biến mất")]
    [SerializeField, Min(0.1f)] float vanishTime = 2.5f;
    [Tooltip("Hiện khói khi biến mất (khói chết chung của game)")]
    [SerializeField] bool deathSmoke = true;
    [SerializeField] AudioClip dieSound;
    public UnityEvent onDefeated = new UnityEvent();

    DummyEnemy enemy;
    NavMeshAgent agent;
    AudioSource audioSource;
    Vector3 home;
    Quaternion homeRotation;
    Mode mode = Mode.Idle;
    CreatureCombatant target;
    float nextAttackAt;
    float nextSkillAt;
    int skillsInFlight;   // Đạn đang bay / vòng đỏ chưa nổ / lửa còn đang phun.
    // Xoay vòng chiêu: mỗi chiêu dùng 1 lần theo thứ tự danh sách, hết vòng thì lặp lại.
    readonly HashSet<BossSkill> usedThisRound = new HashSet<BossSkill>();
    BossSkill waitingSkill;
    float waitingSince;
    float nextReactAt;
    float lastHealth;
    float nextTargetScan;
    float combatStartedAt = -1f;
    int castId;
    string currentLoop;
    float nextRoarAt;
    bool roarPending;
    float footstepTimer;
    Coroutine action;
    readonly List<CreatureCombatant> pets = new List<CreatureCombatant>();
    static Material warningMaterial;

    public bool IsDead => mode == Mode.Dead;
    public CreatureCombatant Target => target;

    // ---------- Dùng cho bảng cheat của dev (DevCheatService) ----------
    /// <summary>Cấp boss. Đổi thì tên trên thanh máu ("Tên • Cấp N") cập nhật theo.</summary>
    public int DeveloperLevel
    {
        get => level;
        set { level = Mathf.Max(0, value); ApplyDisplayName(); }
    }

    /// <summary>Khoảng cách (tính từ tâm boss) để dev dịch chuyển Player tới: ngoài tầm đánh thường + thân boss,
    /// nhưng vẫn trong vùng boss để vào trận ngay.</summary>
    public float DeveloperApproachDistance
    {
        get
        {
            float safe = Mathf.Max(8f, BodyRadius() + attackRange + 3f);
            return zoneRadius > 2f ? Mathf.Min(safe, zoneRadius - 1f) : safe;
        }
    }

    void ApplyDisplayName()
    {
        if (string.IsNullOrWhiteSpace(displayName)) return;
        gameObject.name = level > 0 ? $"{displayName.Trim()} • Cấp {level}" : displayName.Trim();
    }

    void Awake()
    {
        enemy = GetComponent<DummyEnemy>();
        enemy.isBoss = true;          // Không bắt được boss.
        enemy.autoRevive = false;     // Chết là chết hẳn.
        enemy.tintFeedback = false;   // Không tô màu model khi bị đánh / chết.
        // Thanh máu của game lấy tên object làm tên hiển thị -> đặt "Tên • Cấp N", không ghi HP vào tên.
        enemy.writeHealthToName = false;
        ApplyDisplayName();

        // Tắt AI quái thường nếu object có sẵn, để không giành điều khiển với script này.
        var basicAi = GetComponent<BasicCreatureEnemy>();
        if (basicAi != null) basicAi.enabled = false;

        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        agent = GetComponent<NavMeshAgent>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
        }
        // Đi qua kênh âm thanh "Kỹ năng" của game: thanh Hiệu ứng / Kỹ năng trong Cài đặt chỉnh được.
        Capstone.Game.SettingsSystem.GameAudioRouting.Route(audioSource, Capstone.Game.SettingsSystem.GameAudioBus.Skill);
        // Boss to, camera đứng xa -> âm 3D mặc định bị nhỏ. Giữ to trong bán kính Sound Full Distance.
        audioSource.spatialBlend = sound3D;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.minDistance = soundFullDistance;
        audioSource.maxDistance = Mathf.Max(soundFullDistance + 1f, soundMaxDistance);

        home = transform.position;
        homeRotation = transform.rotation;
        foreach (BossSkill skill in skills) ApplyPetSkill(skill);
        FitRingsToEffects();
    }

    // Lấy dữ liệu từ chiêu của pet: kiểu chiêu, tầm, sức mạnh, hồi chiêu, hiệu ứng, âm thanh.
    static void ApplyPetSkill(BossSkill skill)
    {
        if (skill == null || skill.petSkill == null || !skill.useValuesFromPetSkill) return;
        PetSkillDefinition pet = skill.petSkill;
        PetSkillCombatProfile combat = pet.combat;
        PetSkillPresentationProfile look = pet.presentation;

        if (!string.IsNullOrWhiteSpace(pet.displayName)) skill.skillName = pet.displayName;
        if (pet.cooldownSeconds > 0f) skill.cooldown = pet.cooldownSeconds;

        if (combat != null)
        {
            skill.damageKind = combat.damageKind;
            skill.damageMultiplier = Mathf.Max(0.1f, combat.power);
            skill.projectileSpeed = combat.projectileSpeed;
            skill.projectileRadius = Mathf.Max(0.3f, combat.projectileRadius * 3f);
            skill.projectileScale = combat.visualScale > 0f ? combat.visualScale : 1f;
            switch (combat.delivery)
            {
                case PetSkillDelivery.Melee:
                    skill.shape = SkillShape.FrontCone;
                    skill.coneAngle = combat.meleeHalfAngle * 2f;
                    skill.radius = Mathf.Max(2f, combat.range);
                    skill.useRange = skill.radius;
                    break;
                case PetSkillDelivery.Projectile:
                    skill.shape = SkillShape.Projectile;
                    skill.useRange = Mathf.Max(4f, combat.range);
                    break;
                case PetSkillDelivery.Targeted:
                    skill.shape = SkillShape.TargetGround;
                    skill.radius = Mathf.Max(1.5f, combat.targetedImpactRadius * 2f);
                    skill.useRange = Mathf.Max(4f, combat.range);
                    break;
                default:   // SelfBuff của pet -> boss dùng thành chiêu nổ quanh người.
                    skill.shape = SkillShape.AroundBoss;
                    skill.radius = Mathf.Max(3f, combat.range);
                    skill.useRange = skill.radius;
                    break;
            }
            // Chiêu phun hình nón / tia (hơi thở rồng, phun lửa, vòi nước) -> phun hình nón trước mặt boss.
            if (combat.pattern == PetSkillPattern.Cone || combat.pattern == PetSkillPattern.Beam)
            {
                skill.breath = true;
                skill.shape = SkillShape.FrontCone;
                skill.coneAngle = combat.pattern == PetSkillPattern.Beam ? 30f : 60f;
                skill.radius = Mathf.Max(5f, combat.range);
                skill.useRange = skill.radius;
            }
            if (skill.projectilePrefab == null) skill.projectilePrefab = combat.projectileVisual;
        }

        if (look != null)
        {
            skill.hitTime = Mathf.Clamp(look.releaseNormalized, 0.05f, 0.95f);
            if (look.cast != null)
            {
                if (look.cast.effectPrefab != null) skill.castEffect = look.cast.effectPrefab;
                skill.castLifetime = Mathf.Max(0.2f, look.cast.lifetimeSeconds);
                if (look.cast.sound != null) skill.castSound = look.cast.sound;
            }
            GameObject release = look.release != null ? look.release.effectPrefab : null;
            GameObject impact = look.impact != null ? look.impact.effectPrefab : null;
            if (skill.projectilePrefab == null && skill.shape == SkillShape.Projectile) skill.projectilePrefab = release;
            PetSkillCue hitCue = null;
            if (skill.breath && release != null) { skill.hitEffect = release; hitCue = look.release; }   // Lửa phun ra từ miệng.
            else if (impact != null) { skill.hitEffect = impact; hitCue = look.impact; }
            else if (skill.shape != SkillShape.Projectile && release != null) { skill.hitEffect = release; hitCue = look.release; }
            if (hitCue != null)
            {
                skill.hitOffset = hitCue.localOffset;
                skill.hitEuler = hitCue.localEulerAngles;
                skill.hitScale = hitCue.scale > 0f ? hitCue.scale : 1f;
                skill.hitLifetime = Mathf.Max(0.2f, hitCue.lifetimeSeconds);
            }
            AudioClip releaseSound = look.release != null ? look.release.sound : null;
            AudioClip impactSound = look.impact != null ? look.impact.sound : null;
            if (impactSound != null)
            {
                skill.hitSound = impactSound;          // Tiếng va chạm / nổ.
                skill.releaseSound = releaseSound;     // Tiếng lúc bắn / phun ra.
            }
            else if (releaseSound != null) skill.hitSound = releaseSound;
        }
    }

    void Start()
    {
        // DummyEnemy tự gắn tiếng bước chân nhỏ của thú thường (LocomotionSound).
        // Boss có tiếng bước chân riêng thì tắt cái đó đi để không kêu 2 lớp.
        if (footstepSounds != null && footstepSounds.Length > 0)
        {
            var genericSteps = GetComponent<LocomotionSound>();
            if (genericSteps != null) genericSteps.enabled = false;
        }

        if (overrideHealth)
        {
            enemy.maxHealth = maxHealth;
            enemy.Revive();
        }
        // Đặt chân boss xuống mặt đất ngay từ đầu, và lấy chỗ đó làm "nhà".
        if (snapToGround)
        {
            transform.position = GroundPoint(transform.position);
            home = transform.position;
        }
        lastHealth = enemy.CurrentHealth;
        enemy.HealthChanged += OnHealthChanged;
        enemy.Defeated += OnDefeated;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            agent.updateRotation = false;
            agent.stoppingDistance = Mathf.Max(0.1f, attackRange * 0.8f);
        }
        PlayLoop(idleState);
    }

    void OnDestroy()
    {
        if (enemy == null) return;
        enemy.HealthChanged -= OnHealthChanged;
        enemy.Defeated -= OnDefeated;
    }

    // ---------- Bộ não ----------

    void Update()
    {
        if (mode == Mode.Dead || GameplayPause.IsPaused) { StopMoving(); return; }
        if (!enemy.IsAlive) return;
        if (mode == Mode.Busy) return;

        if (Time.time >= nextTargetScan)
        {
            nextTargetScan = Time.time + 0.4f;
            target = FindTarget();
        }

        float fromHome = Flat(transform.position - home).magnitude;
        if (target == null || !IsValidTarget(target) || !InZone(target.transform.position))
        {
            target = null;
            if (combatStartedAt >= 0f)
            {
                // Vừa bỏ đuổi: hồi máu, đi về nhà.
                combatStartedAt = -1f;
                if (healWhenReturning && enemy.IsAlive && enemy.CurrentHealth < enemy.maxHealth)
                {
                    enemy.Revive();
                    lastHealth = enemy.CurrentHealth;
                }
            }
            if (fromHome > 0.6f) { mode = Mode.Return; MoveTo(home); }
            else
            {
                mode = Mode.Idle;
                StopMoving();
                transform.rotation = Quaternion.RotateTowards(transform.rotation, homeRotation, turnSpeed * Time.deltaTime);
                PlayLoop(idleState);
            }
            return;
        }

        if (combatStartedAt < 0f)
        {
            combatStartedAt = Time.time;
            foreach (BossSkill skill in skills) if (skill != null) skill.readyAt = Time.time + firstSkillDelay;
            nextSkillAt = 0f;
            skillsInFlight = 0;
            usedThisRound.Clear();
            waitingSkill = null;
            if (roarOnAggro) { roarPending = true; nextRoarAt = 0f; }
        }

        // Gầm: lúc vào trận, hoặc ngẫu nhiên sau một đòn / chiêu.
        if (roarPending && Time.time >= nextRoarAt)
        {
            roarPending = false;
            action = StartCoroutine(Roar(target));
            return;
        }

        float distance = DistanceTo(target);

        // Ưu tiên chiêu đã hồi xong và pet đang trong tầm.
        BossSkill ready = PickSkill(distance);
        if (ready != null) { action = StartCoroutine(CastSkill(ready, target)); return; }

        if (distance <= attackRange)
        {
            StopMoving();
            Face(target.Center);
            if (Time.time >= nextAttackAt) { action = StartCoroutine(BasicAttack(target)); return; }
            PlayLoop(idleState);
            return;
        }

        mode = Mode.Chase;
        MoveTo(ClampToZone(target.transform.position));
    }

    CreatureCombatant FindTarget()
    {
        // Giữ mục tiêu cũ nếu vẫn hợp lệ, tránh đổi mục tiêu liên tục.
        // Đang đánh: giữ mục tiêu cũ miễn là pet còn trong vùng.
        if (target != null && IsValidTarget(target) && InZone(target.transform.position)) return target;

        pets.Clear();
        foreach (PetController pet in FindObjectsByType<PetController>(FindObjectsSortMode.None))
        {
            if (pet == null || !pet.IsSummoned || pet.owner == null || pet.IsWildEncounter) continue;
            var combatant = pet.GetComponent<CreatureCombatant>();
            if (combatant != null && IsValidTarget(combatant)) pets.Add(combatant);
        }

        CreatureCombatant best = null;
        float bestDistance = aggroRange;
        foreach (CreatureCombatant pet in pets)
        {
            float distance = DistanceTo(pet);
            if (distance <= bestDistance && InZone(pet.transform.position))
            {
                best = pet;
                bestDistance = distance;
            }
        }
        return best;
    }

    bool InZone(Vector3 position) => Flat(position - home).magnitude <= zoneRadius;

    // Điểm gần nhất trong vùng: boss không bước ra ngoài vùng của nó.
    Vector3 ClampToZone(Vector3 position)
    {
        Vector3 offset = Flat(position - home);
        float limit = Mathf.Max(0f, zoneRadius - 0.5f);
        if (offset.magnitude <= limit) return position;
        Vector3 clamped = home + offset.normalized * limit;
        clamped.y = position.y;
        return clamped;
    }

    static bool IsValidTarget(CreatureCombatant combatant)
        => combatant != null && combatant.isActiveAndEnabled && !combatant.IsEnemy && combatant.CanBeAttacked;

    BossSkill PickSkill(float distance)
    {
        if (skillsInFlight > 0 || Time.time < nextSkillAt) return null;   // Mỗi lần chỉ 1 chiêu.
        if (rotateSkills) return PickNextInRound(distance);

        BossSkill best = null;
        foreach (BossSkill skill in skills)
        {
            if (!Usable(skill) || Time.time < skill.readyAt || distance > skill.useRange) continue;
            if (best == null || skill.damageMultiplier > best.damageMultiplier) best = skill;   // Chiêu mạnh nhất trước.
        }
        return best;
    }

    bool Usable(BossSkill skill) => skill != null && HasState(skill.animatorState);

    // Lần lượt: chiêu 0 -> 1 -> 2 ... dùng hết thì bắt đầu vòng mới.
    BossSkill PickNextInRound(float distance)
    {
        BossSkill next = null;
        for (int pass = 0; pass < 2 && next == null; pass++)
        {
            foreach (BossSkill skill in skills)
                if (Usable(skill) && !usedThisRound.Contains(skill)) { next = skill; break; }
            if (next == null)
            {
                if (usedThisRound.Count == 0) return null;   // Không có chiêu nào dùng được.
                usedThisRound.Clear();                        // Hết vòng -> lặp lại từ đầu.
            }
        }
        if (next == null) return null;

        if (waitingSkill != next) { waitingSkill = next; waitingSince = Time.time; }
        if (Time.time < next.readyAt) { waitingSince = Time.time; return null; }   // Chờ hồi chiêu thì không tính là bỏ lượt.
        if (distance <= next.useRange)
        {
            usedThisRound.Add(next);
            waitingSkill = null;
            return next;
        }
        // Pet đứng ngoài tầm chiêu này quá lâu -> bỏ qua lượt này.
        if (Time.time - waitingSince > skipSkillAfter)
        {
            usedThisRound.Add(next);
            waitingSkill = null;
        }
        return null;
    }

    // ---------- Đánh thường ----------

    IEnumerator BasicAttack(CreatureCombatant victim)
    {
        mode = Mode.Busy;
        StopMoving();
        nextAttackAt = Time.time + attackInterval;
        PlayOnce(attackState);
        PlaySound(attackSwingSound, attackVolume);

        yield return WaitForAnimation(attackState, attackHitTime, () => { if (IsValidTarget(victim)) Face(victim.Center); });
        if (mode == Mode.Dead) yield break;
        if (IsValidTarget(victim) && DistanceTo(victim) <= attackRange + 1f)
        {
            DealDamage(victim, 1f, attackKind, "boss-basic");
            PlaySound(attackHitSound, attackVolume);
        }
        yield return WaitForAnimation(attackState, 0.95f, null);
        EndAction(true);
    }

    // ---------- Gầm ----------

    IEnumerator Roar(CreatureCombatant victim)
    {
        nextRoarAt = Time.time + roarCooldown;
        bool hasAnim = !string.IsNullOrEmpty(roarState) && HasState(roarState);
        if (!hasAnim)
        {
            PlaySound(roarSound, roarVolume);   // Chưa có animation -> chỉ gầm tiếng, không đứng lại.
            action = null;
            yield break;
        }
        mode = Mode.Busy;
        StopMoving();
        if (IsValidTarget(victim)) Face(victim.Center, true);
        PlayOnce(roarState);
        PlaySound(roarSound, roarVolume);
        yield return WaitForAnimation(roarState, 0.95f, () => { if (IsValidTarget(victim)) Face(victim.Center); });
        nextAttackAt = Mathf.Max(nextAttackAt, Time.time + 0.3f);
        EndAction();
    }

    // ---------- Chiêu ----------

    IEnumerator CastSkill(BossSkill skill, CreatureCombatant victim)
    {
        mode = Mode.Busy;
        StopMoving();
        skill.readyAt = Time.time + skill.cooldown;
        nextSkillAt = Time.time + 12f;   // Tạm khóa chiêu khác (nếu bị ngắt giữa chừng thì 12 giây sau tự mở lại).
        nextAttackAt = Mathf.Max(nextAttackAt, Time.time + 0.8f);
        castId++;
        if (IsValidTarget(victim)) Face(victim.Center, true);

        PlayOnce(skill.animatorState);
        Spawn(skill.castEffect, transform.position, transform.rotation, null, skill.castLifetime);   // Không gắn làm con: boss scale lớn sẽ phóng to hiệu ứng theo.
        PlaySound(skill.castSound);

        // Chiêu đập đất: hiện vòng cảnh báo ngay khi bắt đầu, khóa vị trí pet lúc đó.
        Vector3 groundPoint = IsValidTarget(victim) ? GroundPoint(victim.Center) : transform.position;
        GameObject warning = null;
        if (skill.shape == SkillShape.TargetGround)
        {
            if (pushRingAwayFromBoss) groundPoint = PushAwayFromBoss(groundPoint, skill.radius);
            warning = CreateWarningRing(groundPoint, skill.radius, ringColor, ringWidth);
        }

        yield return WaitForAnimation(skill.animatorState, skill.hitTime, () =>
        {
            if (skill.shape != SkillShape.TargetGround && IsValidTarget(victim)) Face(victim.Center);
        });
        if (mode == Mode.Dead) { if (warning != null) Destroy(warning); yield break; }

        PlaySound(skill.releaseSound);   // Lúc chiêu bay / phun ra.
        GameObject breathFx = null;
        switch (skill.shape)
        {
            case SkillShape.AroundBoss:
                Explode(skill, transform.position, skill.radius, false);
                break;
            case SkillShape.FrontCone:
                breathFx = Explode(skill, transform.position, skill.radius, true);
                break;
            case SkillShape.Projectile:
                StartCoroutine(TrackSkill(FlyProjectile(skill, victim)));
                break;
            case SkillShape.TargetGround:
                StartCoroutine(TrackSkill(GroundSlam(skill, groundPoint, warning)));
                break;
        }

        // Lửa phun / vụ nổ còn hiện thêm một lúc -> tính từ lúc nó tan hết.
        float effectEnd = Time.time + (skill.shape == SkillShape.FrontCone || skill.shape == SkillShape.AroundBoss ? skill.hitLifetime : 0f);
        if (breathFx != null && breathEndsWithAnimation)
        {
            // Lửa bám theo miệng tới lúc animation chạy tới Breath End Time thì tắt.
            yield return WaitForAnimation(skill.animatorState, Mathf.Max(breathEndTime, skill.hitTime + 0.02f), () =>
            {
                if (breathFx != null) breathFx.transform.SetPositionAndRotation(
                    MouthPosition() + BreathRotation() * skill.hitOffset, BreathRotation() * Quaternion.Euler(skill.hitEuler));
            });
            StopEffectNow(breathFx);
            effectEnd = Time.time + 1f;
        }
        yield return WaitForAnimation(skill.animatorState, 0.95f, null);
        nextSkillAt = Mathf.Max(Time.time, effectEnd) + skillGap;
        EndAction(true);
    }

    // Đạn đang bay / vòng đỏ chưa nổ thì chưa cho dùng chiêu khác.
    IEnumerator TrackSkill(IEnumerator routine)
    {
        skillsInFlight++;
        try { yield return StartCoroutine(routine); }
        finally
        {
            skillsInFlight = Mathf.Max(0, skillsInFlight - 1);
            nextSkillAt = Mathf.Max(nextSkillAt, Time.time + skillGap);
        }
    }

    bool IsBreath(BossSkill skill) =>
        skill.shape == SkillShape.FrontCone && (skill.breath || skill.hitEffect != null && mouthPoint != null);

    Quaternion BreathRotation() =>
        Quaternion.LookRotation(Flat(transform.forward).sqrMagnitude > 0.01f ? Flat(transform.forward) : Vector3.forward);

    GameObject Explode(BossSkill skill, Vector3 center, float radius, bool coneOnly)
    {
        GameObject breathFx = null;
        if (coneOnly && IsBreath(skill))
            // Lửa phun: để chạy lâu, tới lúc animation xong thì CastSkill tự tắt.
            breathFx = SpawnHit(skill, MouthPosition(), BreathRotation(), breathEndsWithAnimation ? 8f : -1f);
        else
            SpawnHit(skill, coneOnly ? center + transform.forward * radius * 0.5f : center, transform.rotation);
        PlaySound(skill.hitSound);
        foreach (CreatureCombatant pet in PetsInRange(center, radius))
        {
            if (coneOnly)
            {
                Vector3 direction = Flat(pet.Center - center);
                if (direction.sqrMagnitude > 0.01f && Vector3.Angle(Flat(transform.forward), direction) > skill.coneAngle * 0.5f) continue;
            }
            DealDamage(pet, skill.damageMultiplier, skill.damageKind, skill.skillName);
        }
        return breathFx;
    }

    // Tắt phun ngay: ngừng ra hạt mới, hạt cũ bay nốt rồi tan.
    static void StopEffectNow(GameObject effect)
    {
        if (effect == null) return;
        float longest = 0f;
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            longest = Mathf.Max(longest, ps.main.startLifetime.constantMax);
        }
        Destroy(effect, Mathf.Clamp(longest, 0.2f, 3f));
    }

    IEnumerator GroundSlam(BossSkill skill, Vector3 point, GameObject warning)
    {
        float end = Time.time + skill.warningTime;
        while (Time.time < end && mode != Mode.Dead)
        {
            if (warning != null) warning.transform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 18f));
            yield return null;
        }
        if (warning != null) Destroy(warning);
        if (mode == Mode.Dead) yield break;

        // Hướng hiệu ứng theo hướng boss -> chỗ nổ (giống pet tung chiêu về phía mục tiêu).
        Vector3 aim = Flat(point - transform.position);
        SpawnHit(skill, point, aim.sqrMagnitude > 0.01f ? Quaternion.LookRotation(aim) : transform.rotation);
        PlaySound(skill.hitSound);
        foreach (CreatureCombatant pet in PetsInRange(point, skill.radius))
            DealDamage(pet, skill.damageMultiplier, skill.damageKind, skill.skillName);
    }

    IEnumerator FlyProjectile(BossSkill skill, CreatureCombatant victim)
    {
        Vector3 start = transform.position + Vector3.up * 1.5f + transform.forward * 1f;
        Vector3 aim = IsValidTarget(victim) ? victim.Center : start + transform.forward * 10f;
        Vector3 direction = (aim - start).normalized;

        GameObject bullet;
        if (skill.projectilePrefab != null)
        {
            bullet = Instantiate(skill.projectilePrefab, start, Quaternion.LookRotation(direction));
            bullet.transform.localScale *= Mathf.Max(0.2f, skill.projectileScale * 2.5f);   // Boss to hơn pet.
        }
        else
        {
            bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(bullet.GetComponent<Collider>());
            bullet.transform.position = start;
            bullet.transform.localScale = Vector3.one * skill.projectileRadius;
        }

        float maxDistance = Mathf.Max(skill.useRange * 1.6f, 8f);
        float travelled = 0f;
        var alreadyHit = new HashSet<CreatureCombatant>();
        bool exploded = false;
        while (travelled < maxDistance && bullet != null && mode != Mode.Dead)
        {
            if (!GameplayPause.IsPaused)
            {
                // Bám nhẹ theo mục tiêu.
                if (IsValidTarget(victim))
                    direction = Vector3.RotateTowards(direction, (victim.Center - bullet.transform.position).normalized, 2f * Time.deltaTime, 0f);
                float step = skill.projectileSpeed * Time.deltaTime;
                bullet.transform.position += direction * step;
                bullet.transform.rotation = Quaternion.LookRotation(direction);
                travelled += step;

                foreach (CreatureCombatant pet in PetsInRange(bullet.transform.position, skill.projectileRadius + 0.6f))
                {
                    if (!alreadyHit.Add(pet)) continue;
                    DealDamage(pet, skill.damageMultiplier, skill.damageKind, skill.skillName);
                    exploded = true;
                }
                if (exploded) break;
            }
            yield return null;
        }

        if (bullet != null)
        {
            SpawnHit(skill, bullet.transform.position, Quaternion.identity);
            if (exploded) PlaySound(skill.hitSound);
            Destroy(bullet);
        }
    }

    List<CreatureCombatant> PetsInRange(Vector3 center, float radius)
    {
        var result = new List<CreatureCombatant>();
        foreach (PetController pet in FindObjectsByType<PetController>(FindObjectsSortMode.None))
        {
            if (pet == null || !pet.IsSummoned || pet.owner == null || pet.IsWildEncounter) continue;
            var combatant = pet.GetComponent<CreatureCombatant>();
            if (!IsValidTarget(combatant)) continue;
            Vector3 closest = combatant.ClosestPoint(center);
            if (Flat(closest - center).magnitude <= radius && Mathf.Abs(closest.y - center.y) < radius + 2f) result.Add(combatant);
        }
        return result;
    }

    void DealDamage(CreatureCombatant victim, float multiplier, CreatureDamageKind kind, string id)
    {
        if (!IsValidTarget(victim)) return;
        bool critical = UnityEngine.Random.value < critChance;
        float raw = attackDamage * multiplier * (critical ? critMultiplier : 1f);
        victim.ReceiveHit(new CombatHit(gameObject, id, castId, raw, kind, critical, false, victim.Center));
    }

    void EndAction(bool mayRoar = false)
    {
        action = null;
        if (mode == Mode.Dead) return;
        if (mayRoar && !roarPending && Time.time >= nextRoarAt && UnityEngine.Random.value < roarChance) roarPending = true;
        mode = Mode.Idle;
        currentLoop = null;
        PlayLoop(idleState);
    }

    // ---------- Bị đánh / chết ----------

    void OnHealthChanged(DummyEnemy changed)
    {
        float health = changed.CurrentHealth;
        bool tookDamage = health < lastHealth;
        lastHealth = health;
        if (!tookDamage || mode == Mode.Dead || !changed.IsAlive) return;

        // Bị đánh khi đang đứng / đi: boss quay lại đánh pet đó luôn.
        if (target == null) nextTargetScan = 0f;
        if (!playDamageAnimation || mode == Mode.Busy || Time.time < nextReactAt) return;
        if (!HasState(damageState)) return;
        nextReactAt = Time.time + damageReactCooldown;
        action = StartCoroutine(React());
    }

    IEnumerator React()
    {
        mode = Mode.Busy;
        StopMoving();
        PlayOnce(damageState);
        yield return WaitForAnimation(damageState, 0.9f, null);
        EndAction();
    }

    void OnDefeated(GameObject source)
    {
        if (mode == Mode.Dead) return;
        mode = Mode.Dead;
        if (action != null) StopCoroutine(action);
        StopMoving();
        if (agent != null) agent.enabled = false;
        PlayOnce(dieState);
        PlaySound(dieSound);
        // Game tự thêm EnemyDeathVisual -> nó ẩn boss ngay khi không thấy animation chết của nó. Tắt đi, boss tự lo.
        var autoHide = GetComponent<EnemyDeathVisual>();
        if (autoHide != null) autoHide.enabled = false;
        onDefeated?.Invoke();
        if (vanishAfterDeath) StartCoroutine(VanishRoutine());
    }

    IEnumerator VanishRoutine()
    {
        // 1. Đợi animation Die chạy hết (tối đa 8 giây phòng khi Animator không có state Die).
        yield return null;
        var autoHide = GetComponent<EnemyDeathVisual>();   // Có thể được thêm sau OnDefeated.
        if (autoHide != null) autoHide.enabled = false;
        if (animator != null && HasState(dieState))
        {
            int hash = Animator.StringToHash(dieState);
            float until = Time.time + 8f;
            while (Time.time < until)
            {
                if (autoHide != null) autoHide.enabled = false;
                var info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == hash && info.normalizedTime >= 0.98f) break;
                yield return null;
            }
        }

        // 2. Xác nằm yên một lúc.
        yield return new WaitForSeconds(corpseStayTime);

        // 3. Chìm dần xuống đất (sâu bằng chiều cao thân boss) rồi biến mất.
        if (animator != null) animator.applyRootMotion = false;
        float height = 2f;
        foreach (var r in GetComponentsInChildren<Renderer>())
            if (r.enabled && !(r is ParticleSystemRenderer)) height = Mathf.Max(height, r.bounds.size.y);
        Vector3 start = transform.position;
        Vector3 end = start + Vector3.down * height * 1.1f;
        if (deathSmoke)
        {
            var settings = Resources.Load<CombatFeelSettings>("CombatFeelSettings");
            if (settings != null && settings.deathSmoke != null) Spawn(settings.deathSmoke, enemy.TargetPosition, Quaternion.identity, null, vanishTime);
        }
        for (float t = 0f; t < vanishTime; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / vanishTime));
            yield return null;
        }
        enemy.DeathVisualComplete = true;
        gameObject.SetActive(false);
    }

    // ---------- Di chuyển ----------

    void MoveTo(Vector3 destination)
    {
        Vector3 before = transform.position;
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.SetDestination(destination);
            Vector3 velocity = agent.desiredVelocity;
            if (Flat(velocity).sqrMagnitude > 0.01f) Face(transform.position + velocity);
        }
        else
        {
            Vector3 direction = Flat(destination - transform.position);
            if (direction.magnitude > 0.1f)
            {
                Vector3 step = Vector3.MoveTowards(transform.position, new Vector3(destination.x, transform.position.y, destination.z),
                    moveSpeed * Time.deltaTime);
                if (snapToGround) step.y = GroundPoint(step).y;
                transform.position = step;
                Face(destination);
            }
        }
        bool moving = Flat(transform.position - before).magnitude > 0.001f
            || (agent != null && agent.isActiveAndEnabled && agent.velocity.sqrMagnitude > 0.04f);
        PlayLoop(moving ? walkState : idleState);

        // Tiếng bước chân khi đang đi.
        if (moving && footstepSounds != null && footstepSounds.Length > 0)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                footstepTimer = footstepInterval;
                PlaySound(footstepSounds[UnityEngine.Random.Range(0, footstepSounds.Length)], footstepVolume);
            }
        }
        else footstepTimer = 0f;   // Vừa bước đi là kêu ngay bước đầu.
    }

    void StopMoving()
    {
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    void Face(Vector3 point, bool instant = false)
    {
        Vector3 direction = Flat(point - transform.position);
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
        transform.rotation = instant ? wanted : Quaternion.RotateTowards(transform.rotation, wanted, turnSpeed * Time.deltaTime);
    }

    float DistanceTo(CreatureCombatant other)
    {
        Vector3 closest = other.ClosestPoint(transform.position);
        return Flat(closest - transform.position).magnitude;
    }

    Vector3 GroundPoint(Vector3 position)
    {
        var hits = Physics.RaycastAll(position + Vector3.up * 3f, Vector3.down, 30f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        float best = float.NegativeInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<PetController>() != null) continue;
            if (hit.point.y > best && hit.point.y < position.y + 2f) best = hit.point.y;
        }
        return float.IsNegativeInfinity(best) ? position : new Vector3(position.x, best, position.z);
    }

    static Vector3 Flat(Vector3 value) => new Vector3(value.x, 0f, value.z);

    // ---------- Animation ----------

    bool HasState(string state)
        => animator != null && !string.IsNullOrEmpty(state) && animator.HasState(0, Animator.StringToHash(state));

    void PlayLoop(string state)
    {
        if (currentLoop == state || !HasState(state)) return;
        currentLoop = state;
        animator.CrossFadeInFixedTime(state, crossFade, 0);
    }

    void PlayOnce(string state)
    {
        currentLoop = null;
        if (!HasState(state))
        {
            Debug.LogWarning($"{name}: Animator không có state '{state}'.", this);
            return;
        }
        animator.CrossFadeInFixedTime(state, crossFade, 0, 0f);
    }

    // Đợi tới khi state đang phát đạt mốc normalized (0..1). Có giới hạn thời gian phòng khi state không chạy.
    IEnumerator WaitForAnimation(string state, float normalized, Action eachFrame)
    {
        int hash = Animator.StringToHash(state);
        float timeout = Time.time + 6f;
        float enterDeadline = Time.time + 0.6f;
        while (Time.time < timeout && mode != Mode.Dead)
        {
            eachFrame?.Invoke();
            if (animator == null || !HasState(state))
            {
                yield return new WaitForSeconds(0.6f * normalized);
                yield break;
            }
            AnimatorStateInfo info = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            if (info.shortNameHash == hash)
            {
                if (info.normalizedTime >= normalized) yield break;
            }
            else if (Time.time > enterDeadline) yield break;   // State đã chuyển đi (vd. hết animation).
            yield return null;
        }
    }

    // ---------- Hiệu ứng ----------

    GameObject SpawnHit(BossSkill skill, Vector3 point, Quaternion rotation, float lifetime = -1f)
    {
        Quaternion final = rotation * Quaternion.Euler(skill.hitEuler);
        GameObject effect = Spawn(skill.hitEffect, point + rotation * skill.hitOffset, final, null,
                                  lifetime > 0f ? lifetime : skill.hitLifetime);
        if (effect != null && !Mathf.Approximately(skill.hitScale, 1f)) ScaleEffect(effect, skill.hitScale);
        return effect;
    }

    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform follow, float lifetime = 3f)
    {
        if (prefab == null) return null;
        GameObject effect = Instantiate(prefab, position, rotation, follow);
        IgnoreBossBody(effect, position);
        // Hiệu ứng lặp (Looping) sẽ phun lại liên tục -> hết thời gian thì ngừng phát, đợi hạt cũ tan rồi xóa.
        StartCoroutine(StopEffectAfter(effect, lifetime));
        return effect;
    }

    static IEnumerator StopEffectAfter(GameObject effect, float lifetime)
    {
        yield return new WaitForSeconds(lifetime);
        if (effect == null) yield break;
        float longest = 0f;
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            longest = Mathf.Max(longest, ps.main.startLifetime.constantMax);
        }
        Destroy(effect, Mathf.Clamp(longest, 0.2f, 3f));
    }

    // Hiệu ứng của pet có thể có Collider hoặc hạt va chạm (Particle Collision) -> rơi trúng thân boss là nổ luôn.
    // Pet cũng tắt Collider của hiệu ứng như vậy. Hạt va chạm thì bỏ layer của boss ra khỏi danh sách va chạm.
    int bossLayerMask = -1;
    bool layerWarned;
    void IgnoreBossBody(GameObject effect, Vector3 point)
    {
        foreach (var c in effect.GetComponentsInChildren<Collider>(true)) c.enabled = false;

        if (bossLayerMask < 0)
        {
            bossLayerMask = 0;
            foreach (var c in GetComponentsInChildren<Collider>(true)) bossLayerMask |= 1 << c.gameObject.layer;
        }
        if (bossLayerMask == 0) return;

        // Layer của mặt đất chỗ nổ: không được bỏ, không thì hạt rơi xuyên đất.
        int groundMask = 0;
        foreach (var hit in Physics.RaycastAll(point + Vector3.up * 3f, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(transform)) { groundMask = 1 << hit.collider.gameObject.layer; break; }
        int remove = bossLayerMask & ~groundMask;
        if (remove != bossLayerMask && !layerWarned)
        {
            layerWarned = true;
            Debug.LogWarning($"[Boss] {name} đang cùng Layer với mặt đất nên hạt của chiêu vẫn có thể nổ trên người boss. " +
                             "Hãy đổi Layer của boss sang layer riêng (vd. Enemy).", this);
        }
        if (remove == 0) return;

        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var collision = ps.collision;
            if (!collision.enabled || collision.type != ParticleSystemCollisionType.World) continue;
            collision.collidesWith = collision.collidesWith & ~remove;
        }
    }

    void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, soundVolume * volume);
    }

    Vector3 MouthPosition()
    {
        if (mouthPoint != null) return mouthPoint.position;
        float height = 2f;
        foreach (var c in GetComponentsInChildren<Collider>())
            if (!c.isTrigger && c.enabled) height = Mathf.Max(height, c.bounds.size.y);
        return transform.position + Vector3.up * height * 0.6f + Flat(transform.forward).normalized * BodyRadius();
    }

    // ---------- Vòng đỏ ----------

    float bodyRadius = -1f;
    float BodyRadius()
    {
        if (bodyRadius >= 0f) return bodyRadius;
        bodyRadius = 0f;
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger || !c.enabled) continue;
            Vector3 e = c.bounds.extents;
            bodyRadius = Mathf.Max(bodyRadius, Mathf.Max(e.x, e.z));
        }
        return bodyRadius;
    }

    // Pet đứng sát boss thì đẩy tâm vòng ra xa (theo hướng boss -> pet) để vòng không đè lên boss.
    Vector3 PushAwayFromBoss(Vector3 point, float radius)
    {
        float minDistance = BodyRadius() + ringGapFromBoss + radius;
        Vector3 offset = Flat(point - transform.position);
        if (offset.magnitude >= minDistance) return point;
        Vector3 dir = offset.sqrMagnitude > 0.01f ? offset.normalized : Flat(transform.forward).normalized;
        return GroundPoint(transform.position + dir * minDistance);
    }

    // Đo hiệu ứng: chạy thử hạt 1.5 giây rồi lấy độ rộng trên mặt đất.
    static float MeasureEffectRadius(GameObject prefab, float scale)
    {
        Bounds? b = MeasureEffectBounds(prefab, scale);
        return b.HasValue ? Mathf.Max(b.Value.extents.x, b.Value.extents.z) : 0f;
    }

    // Độ dài hiệu ứng phun theo hướng phun (trục Z xanh dương của prefab), tính từ gốc prefab.
    static float MeasureEffectLength(GameObject prefab, float scale)
    {
        Bounds? b = MeasureEffectBounds(prefab, scale);
        if (!b.HasValue) return 0f;
        return Mathf.Max(b.Value.max.z, b.Value.size.z);   // Prefab đặt tại z = 0.
    }

    // Phóng to cả hạt con: Scaling Mode "Local" của Unity bỏ qua scale của object cha.
    static void ScaleEffect(GameObject effect, float scale)
    {
        effect.transform.localScale *= scale;
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            if (main.scalingMode == ParticleSystemScalingMode.Local) main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    static Bounds? MeasureEffectBounds(GameObject prefab, float scale)
    {
        if (prefab == null) return null;
        GameObject probe = Instantiate(prefab, new Vector3(0f, -5000f, 0f), Quaternion.identity);
        if (!Mathf.Approximately(scale, 1f)) ScaleEffect(probe, scale);
        var systems = probe.GetComponentsInChildren<ParticleSystem>(true);
        var renderers = probe.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        Bounds bounds = default;
        // Chạy thử từng nửa giây tới 3 giây, lấy kích thước lớn nhất (lửa phun cần thời gian mới dài hết cỡ).
        for (float t = 0.5f; t <= 3.01f; t += 0.5f)
        {
            foreach (var ps in systems) ps.Simulate(t, false, true);
            foreach (var r in renderers)
            {
                if (r is ParticleSystemRenderer psr && psr.GetComponent<ParticleSystem>().particleCount == 0) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (systems.Length == 0) break;
        }
        DestroyImmediate(probe);
        return any ? bounds : (Bounds?)null;
    }

    void FitRingsToEffects()
    {

        foreach (BossSkill skill in skills)
        {
            if (skill.hitEffect == null) continue;
            if (skill.breath && fitBreathToRange)
            {
                float length = MeasureEffectLength(skill.hitEffect, skill.hitScale);
                if (length < 0.3f) continue;
                skill.hitScale = Mathf.Clamp(skill.hitScale * skill.radius / length, 0.2f, 20f);
                Debug.Log($"[Boss] {skill.skillName}: hiệu ứng phun dài {length:0.0} m -> phóng x{skill.hitScale:0.00} cho dài {skill.radius:0.0} m.", this);
                continue;
            }
            if (skill.shape != SkillShape.TargetGround || !fitRingToEffect) continue;
            float measured = MeasureEffectRadius(skill.hitEffect, skill.hitScale) * ringFitScale;
            if (measured < 0.3f) continue;   // Đo không ra -> giữ Radius cũ.
            skill.radius = measured;
            Debug.Log($"[Boss] {skill.skillName}: vòng đỏ khớp hiệu ứng, bán kính {measured:0.0} m.", this);
        }
    }

    static GameObject CreateWarningRing(Vector3 center, float radius, Color color, float width)
    {
        var ring = new GameObject("BossWarningRing");
        ring.transform.position = center + Vector3.up * 0.05f;
        var line = ring.AddComponent<LineRenderer>();
        const int segments = 48;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.widthMultiplier = width;
        if (warningMaterial == null) warningMaterial = new Material(Shader.Find("Sprites/Default"));
        line.material = warningMaterial;
        line.startColor = line.endColor = color;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
        return ring;
    }

    static void DrawCircle(Vector3 center, float radius)
    {
        const int segments = 64;
        Vector3 previous = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Gizmos.DrawLine(previous, next);
            Gizmos.DrawLine(previous + Vector3.up * 0.5f, next + Vector3.up * 0.5f);
            previous = next;
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 origin = Application.isPlaying ? home : transform.position;
        Gizmos.color = new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, aggroRange);
        // Vùng của boss: vẽ ở chỗ cố định (nhà), không đi theo boss.
        Gizmos.color = new Color(1f, 0.3f, 0.2f);
        DrawCircle(origin, zoneRadius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}