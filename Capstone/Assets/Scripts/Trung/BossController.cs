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
    }

    enum Mode { Idle, Chase, Return, Busy, Dead }

    [Header("Animator (tên state)")]
    [SerializeField] Animator animator;
    [SerializeField] string idleState = "Idle";
    [SerializeField] string walkState = "Walk";
    [SerializeField] string attackState = "Attack";
    [SerializeField] string damageState = "Damage";
    [SerializeField] string dieState = "Die";
    [SerializeField, Min(0f)] float crossFade = 0.12f;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    internal float DeveloperApproachDistance => Mathf.Max(zoneRadius, aggroRange) + 4f;
    internal int DeveloperLevel { get => level; set => level = Mathf.Max(0, value); }
#endif
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

    [Header("Chiêu")]
    [Tooltip("Chiêu đầu tiên chỉ được dùng sau bấy nhiêu giây kể từ lúc vào trận")]
    [SerializeField, Min(0f)] float firstSkillDelay = 4f;
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
    [Tooltip("Mấy giây sau khi chết thì ẩn boss. 0 = giữ xác")]
    [SerializeField, Min(0f)] float hideAfterDeath = 0f;
    public UnityEvent onDefeated = new UnityEvent();

    DummyEnemy enemy;
    NavMeshAgent agent;
    AudioSource audioSource;
    Vector3 home;
    Quaternion homeRotation;
    Mode mode = Mode.Idle;
    CreatureCombatant target;
    float nextAttackAt;
    float nextReactAt;
    float lastHealth;
    float nextTargetScan;
    float combatStartedAt = -1f;
    int castId;
    string currentLoop;
    Coroutine action;
    readonly List<CreatureCombatant> pets = new List<CreatureCombatant>();
    static Material warningMaterial;

    public bool IsDead => mode == Mode.Dead;
    public CreatureCombatant Target => target;

    void Awake()
    {
        enemy = GetComponent<DummyEnemy>();
        enemy.isBoss = true;          // Không bắt được boss.
        enemy.autoRevive = false;     // Chết là chết hẳn.
        enemy.tintFeedback = false;   // Không tô màu model khi bị đánh / chết.
        // Thanh máu của game lấy tên object làm tên hiển thị -> đặt "Tên • Cấp N", không ghi HP vào tên.
        enemy.writeHealthToName = false;
        if (!string.IsNullOrWhiteSpace(displayName))
            gameObject.name = level > 0 ? $"{displayName.Trim()} • Cấp {level}" : displayName.Trim();

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
            audioSource.maxDistance = 40f;
        }
        // Đi qua kênh âm thanh "Kỹ năng" của game: thanh Hiệu ứng / Kỹ năng trong Cài đặt chỉnh được.
        Capstone.Game.SettingsSystem.GameAudioRouting.Route(audioSource, Capstone.Game.SettingsSystem.GameAudioBus.Skill);

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
            if (skill.projectilePrefab == null) skill.projectilePrefab = combat.projectileVisual;
        }

        if (look != null)
        {
            skill.hitTime = Mathf.Clamp(look.releaseNormalized, 0.05f, 0.95f);
            if (look.cast != null)
            {
                if (look.cast.effectPrefab != null) skill.castEffect = look.cast.effectPrefab;
                if (look.cast.sound != null) skill.castSound = look.cast.sound;
            }
            GameObject release = look.release != null ? look.release.effectPrefab : null;
            GameObject impact = look.impact != null ? look.impact.effectPrefab : null;
            if (skill.projectilePrefab == null && skill.shape == SkillShape.Projectile) skill.projectilePrefab = release;
            PetSkillCue hitCue = null;
            if (impact != null) { skill.hitEffect = impact; hitCue = look.impact; }
            else if (skill.shape != SkillShape.Projectile && release != null) { skill.hitEffect = release; hitCue = look.release; }
            if (hitCue != null)
            {
                skill.hitOffset = hitCue.localOffset;
                skill.hitEuler = hitCue.localEulerAngles;
                skill.hitScale = hitCue.scale > 0f ? hitCue.scale : 1f;
            }
            AudioClip hitSound = look.impact != null && look.impact.sound != null ? look.impact.sound
                : look.release != null ? look.release.sound : null;
            if (hitSound != null) skill.hitSound = hitSound;
        }
    }

    void Start()
    {
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
        BossSkill best = null;
        foreach (BossSkill skill in skills)
        {
            if (skill == null || Time.time < skill.readyAt || distance > skill.useRange) continue;
            if (!HasState(skill.animatorState)) continue;
            if (best == null || skill.damageMultiplier > best.damageMultiplier) best = skill;   // Chiêu mạnh nhất trước.
        }
        return best;
    }

    // ---------- Đánh thường ----------

    IEnumerator BasicAttack(CreatureCombatant victim)
    {
        mode = Mode.Busy;
        StopMoving();
        nextAttackAt = Time.time + attackInterval;
        PlayOnce(attackState);

        yield return WaitForAnimation(attackState, attackHitTime, () => { if (IsValidTarget(victim)) Face(victim.Center); });
        if (mode == Mode.Dead) yield break;
        if (IsValidTarget(victim) && DistanceTo(victim) <= attackRange + 1f)
        {
            DealDamage(victim, 1f, attackKind, "boss-basic");
        }
        yield return WaitForAnimation(attackState, 0.95f, null);
        EndAction();
    }

    // ---------- Chiêu ----------

    IEnumerator CastSkill(BossSkill skill, CreatureCombatant victim)
    {
        mode = Mode.Busy;
        StopMoving();
        skill.readyAt = Time.time + skill.cooldown;
        nextAttackAt = Mathf.Max(nextAttackAt, Time.time + 0.8f);
        castId++;
        if (IsValidTarget(victim)) Face(victim.Center, true);

        PlayOnce(skill.animatorState);
        Spawn(skill.castEffect, transform.position, transform.rotation, null);   // Không gắn làm con: boss scale lớn sẽ phóng to hiệu ứng theo.
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

        switch (skill.shape)
        {
            case SkillShape.AroundBoss:
                Explode(skill, transform.position, skill.radius, false);
                break;
            case SkillShape.FrontCone:
                Explode(skill, transform.position, skill.radius, true);
                break;
            case SkillShape.Projectile:
                StartCoroutine(FlyProjectile(skill, victim));
                break;
            case SkillShape.TargetGround:
                StartCoroutine(GroundSlam(skill, groundPoint, warning));
                break;
        }

        yield return WaitForAnimation(skill.animatorState, 0.95f, null);
        EndAction();
    }

    void Explode(BossSkill skill, Vector3 center, float radius, bool coneOnly)
    {
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

    void EndAction()
    {
        action = null;
        if (mode == Mode.Dead) return;
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
        onDefeated?.Invoke();
        if (hideAfterDeath > 0f) StartCoroutine(HideLater());
    }

    IEnumerator HideLater()
    {
        yield return new WaitForSeconds(hideAfterDeath);
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

    void SpawnHit(BossSkill skill, Vector3 point, Quaternion rotation)
    {
        Quaternion final = rotation * Quaternion.Euler(skill.hitEuler);
        GameObject effect = Spawn(skill.hitEffect, point + rotation * skill.hitOffset, final, null);
        if (effect != null && !Mathf.Approximately(skill.hitScale, 1f)) effect.transform.localScale *= skill.hitScale;
    }

    GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform follow)
    {
        if (prefab == null) return null;
        GameObject effect = Instantiate(prefab, position, rotation, follow);
        IgnoreBossBody(effect, position);
        Destroy(effect, 5f);
        return effect;
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

    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
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
        if (prefab == null) return 0f;
        GameObject probe = Instantiate(prefab, new Vector3(0f, -5000f, 0f), Quaternion.identity);
        probe.transform.localScale *= scale;
        foreach (var ps in probe.GetComponentsInChildren<ParticleSystem>(true))
            ps.Simulate(1.5f, false, true);
        bool any = false;
        Bounds bounds = default;
        foreach (var r in probe.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer psr && psr.GetComponent<ParticleSystem>().particleCount == 0) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        DestroyImmediate(probe);
        return any ? Mathf.Max(bounds.extents.x, bounds.extents.z) : 0f;
    }

    void FitRingsToEffects()
    {
        if (!fitRingToEffect) return;
        foreach (BossSkill skill in skills)
        {
            if (skill.shape != SkillShape.TargetGround || skill.hitEffect == null) continue;
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
