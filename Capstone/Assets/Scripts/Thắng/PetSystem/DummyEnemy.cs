using System;
using System.Collections.Generic;
using Capstone.Game.MapSystem;
using UnityEngine;

[DisallowMultipleComponent]
public class DummyEnemy : MonoBehaviour
{
    private static readonly HashSet<DummyEnemy> activeEnemies = new HashSet<DummyEnemy>();
    [Header("Mau")]
    public float maxHealth = 100f;
    public bool autoRevive = true;
    public float reviveDelay = 2.5f;
    [Tooltip("Dummy tap luyen khong cap EXP, EV hoac tien do ha dich.")]
    public bool trainingDummy;
    public bool IsTrainingDummy => trainingDummy;
    [Header("Chon muc tieu")]
    [Tooltip("Uu tien khi tu dong khoa muc tieu; marker Boss cung xac dinh vai tro boss.")]
    public bool isBoss;
    [Header("Tien trinh")]
    [SerializeField, Min(0)]
    private int experienceReward = 50;
    [SerializeField]
    private Capstone.Game.HudSystem.PetStatValues effortYield = new Capstone.Game.HudSystem.PetStatValues(0, 1, 0, 0, 0, 0);
    [Header("Phan hoi")]
    public Color idleColor = Color.white;
    public Color hitColor = new Color(1f, 0.35f, 0.2f, 1f);
    public Color deadColor = new Color(0.25f, 0.25f, 0.25f, 1f);
    public float hitFlashTime = 0.12f;
    public bool writeHealthToName = true;
    [Tooltip("Tat tren sinh vat co texture de giu mau goc.")]
    public bool tintFeedback = true;
    private MaterialPropertyBlock colorProperties;
    private Renderer cachedRenderer;
    private Collider[] colliders;
    private string baseName;
    private float health;
    private float reviveAt;
    private float flashUntil;
    private bool alive = true;
    bool downed, defeatCommitted;
    float downedUntil;
    public float DownedDuration => Capstone.Game.CreatureSystem.PveBalanceRules.DownedCaptureSeconds;
    public bool DeathVisualComplete { get; internal set; }
    internal bool DeathManagedByEncounter;
    GameObject downedSource;
    public string EncounterId { get; private set; } = Guid.NewGuid().ToString("N");
    internal string QuestZoneId { get; set; }
    internal DayNightCycle QuestDayNightClock { get; set; }
    internal bool? QuestEncounterStartedAtNight { get; private set; }

    internal void MarkQuestEncounterStarted() {
        if (QuestEncounterStartedAtNight.HasValue) return;
        var clock = QuestDayNightClock != null ? QuestDayNightClock : FindFirstObjectByType<DayNightCycle>();
        if (clock == null) return;
        float hour = Mathf.Repeat(clock.timeOfDay, 1f) * 24f;
        QuestEncounterStartedAtNight = hour >= 18f || hour < 6f;
    }

    internal void ResetQuestEncounterTime() => QuestEncounterStartedAtNight = null;
    public bool IsDowned => downed;
    public bool IsSelectable => alive || downed;
    public bool IsDownedAvailable => downed && (Time.time < downedUntil || CaptureLocked);
    public float DownedSecondsRemaining => downed ? Mathf.Max(0, downedUntil - Time.time) : 0;
    public float CurrentHealth => health;
    public bool CaptureLocked { get; internal set; }

    internal void InitializeCaptureHealth(float current, float maximum)
    {
        maxHealth = maximum;
        health = current;
        alive = health > 0;
        RefreshName();
        HealthChanged?.Invoke(this);
    }

    internal void FinishCaptureWithoutDefeat()
    {
        alive = false;
        downed = false;
        defeatCommitted = true;
        autoRevive = false;
        GetComponent<Capstone.Game.CreatureSystem.CombatBleed>()?.ClearWounds();
        HealthChanged?.Invoke(this);
        enabled = false;
    }

    private MapMarker mapMarker;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeRegistry()
    {
        activeEnemies.Clear();
    }

    public static IReadOnlyCollection<DummyEnemy> ActiveEnemies
    {
        get
        {
            return activeEnemies;
        }
    }

    public event Action<GameObject> Defeated;
    public event Action<DummyEnemy> HealthChanged;
    public bool IsAlive
    {
        get
        {
            return alive;
        }
    }

    public int ExperienceReward
    {
        get
        {
            return trainingDummy ? 0 : Mathf.Max(0, experienceReward);
        }
    }

    public bool IsBoss => isBoss || (mapMarker != null && mapMarker.MarkerType == MapMarkerType.Boss);
    public Capstone.Game.HudSystem.PetStatValues EffortYield => trainingDummy ? default : effortYield.Clamped(0, 3);
    public Vector3 TargetPosition
    {
        get
        {
            Bounds bounds = GetTargetBounds();
            return bounds.center;
        }
    }

    public Vector3 ClosestPoint(Vector3 worldPosition)
    {
        Vector3 bestPoint = TargetPosition;
        float bestSqrDistance = float.PositiveInfinity;
        if (colliders != null)
        {
            foreach (Collider targetCollider in colliders)
            {
                if (targetCollider == null || !targetCollider.enabled)
                {
                    continue;
                }

                Vector3 point = targetCollider.ClosestPoint(worldPosition);
                float sqrDistance = (point - worldPosition).sqrMagnitude;
                if (sqrDistance < bestSqrDistance)
                {
                    bestSqrDistance = sqrDistance;
                    bestPoint = point;
                }
            }
        }

        if (!float.IsInfinity(bestSqrDistance))
        {
            return bestPoint;
        }

        return GetTargetBounds().ClosestPoint(worldPosition);
    }

    private void Awake()
    {
        Capstone.Game.CreatureSystem.LocomotionSound.Attach(gameObject);
        cachedRenderer = GetComponentInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        baseName = gameObject.name;
        health = Mathf.Max(1f, maxHealth);
        ApplyColor(idleColor);
        RefreshName();
    }

    private void OnValidate()
    {
        maxHealth = Mathf.Max(1f, maxHealth);
        reviveDelay = Mathf.Max(0f, reviveDelay);
        experienceReward = Mathf.Max(0, experienceReward);
        effortYield = effortYield.Clamped(0, 3);
    }

    private void OnEnable()
    {
        activeEnemies.Add(this);
        Capstone.Game.CreatureSystem.CreatureCombatant.RefreshPlayerPassThrough(gameObject);
        EnsureMapMarker();
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
    }

    private void OnDestroy()
    {
        activeEnemies.Remove(this);
    }

    private void Update()
    {
        if (downed)
        {
            if (!CaptureLocked && Time.time >= downedUntil)
            {
                Die(downedSource);
            }

            return;
        }

        if (!alive)
        {
            if (autoRevive && Time.time >= reviveAt)
            {
                Revive();
            }

            return;
        }

        if (flashUntil > 0f && Time.time >= flashUntil)
        {
            flashUntil = 0f;
            ApplyColor(idleColor);
        }
    }

    public void TakeDamage(float damage)
    {
        TakeDamage(damage, null);
    }

    public void TakeDamage(float damage, GameObject source)
    {
        TakeCombatDamage(damage, source, Capstone.Game.CreatureSystem.CreatureDamageKind.Physical);
    }

    public void TakeCombatDamage(float damage, GameObject source, Capstone.Game.CreatureSystem.CreatureDamageKind kind)
    {
        ApplyCombatHit(new Capstone.Game.CreatureSystem.CombatHit(source, "legacy", 0, damage, kind, false, false, TargetPosition));
    }

    public Capstone.Game.CreatureSystem.CombatDamageResult ApplyCombatHit(Capstone.Game.CreatureSystem.CombatHit hit)
    {
        float damage = hit.RawDamage;
        if (Capstone.Game.UISystem.GameplayPause.IsPaused || !alive || CaptureLocked || damage <= 0 || float.IsNaN(damage) || float.IsInfinity(damage))
        {
            return new Capstone.Game.CreatureSystem.CombatDamageResult(hit, gameObject, 0, false);
        }

        var combat = GetComponent<Capstone.Game.CreatureSystem.CreatureCombatant>();
        float effectiveness;
        if (combat != null)
        {
            damage = combat.ResolveDamage(hit, out effectiveness);
        }
        else
        {
            damage = Capstone.Game.CreatureSystem.CreatureCombatant.ResolveElementalDamage(damage, hit, GetComponent<Capstone.Game.HudSystem.PetCollectionMetadata>(), out effectiveness);
        }

        float damageBeforeStatus = damage;
        var status = combat != null ? combat.Status : GetComponent<Capstone.Game.CreatureSystem.CombatBleed>();
        if (status != null) damage = status.Absorb(damage);
        if (damage <= 0)
            return new Capstone.Game.CreatureSystem.CombatDamageResult(hit, gameObject, 0, false, effectiveness, damageBeforeStatus);
        float previousHealth = health;
        health = Mathf.Max(0f, health - Mathf.Max(0f, damage));
        combat?.SyncEnemyHealth(health, maxHealth);
        GetComponent<Capstone.Game.CaptureSystem.WildPetCaptureTarget>()?.SyncHealthFromEnemy();
        RefreshName();
        bool shouldDown = health <= 0 && !trainingDummy && !IsBoss && GetComponent<Capstone.Game.CaptureSystem.WildPetCaptureTarget>()is var wild && wild != null && wild.CanEnterDowned;
        if (shouldDown)
        {
            alive = false;
            downed = true;
            downedSource = hit.Source;
            downedUntil = Time.time + Capstone.Game.CreatureSystem.PveBalanceRules.DownedCaptureSeconds;
            GetComponent<Capstone.Game.CreatureSystem.CombatBleed>()?.ClearWounds();
        }

        var result = new Capstone.Game.CreatureSystem.CombatDamageResult(hit, gameObject, previousHealth - health, health <= 0f && !shouldDown, effectiveness,
            Mathf.Max(0, damageBeforeStatus - damage));
        if (health <= 0f)
        {
            // Phat HP mat va dong gop truoc giao dich thuong ket lieu duy nhat.
            if (!shouldDown)
            {
                alive = false;
            }
        }
        else
        {
            flashUntil = Time.time + hitFlashTime;
            ApplyColor(hitColor);
        }

        HealthChanged?.Invoke(this);
        Capstone.Game.CreatureSystem.CombatDamageEvents.Publish(result);
        if (health <= 0 && !shouldDown)
        {
            Die(hit.Source);
        }

        return result;
    }

    public void Revive()
    {
        downed = false;
        defeatCommitted = false;
        DeathVisualComplete = false;
        GetComponent<Capstone.Game.CreatureSystem.CreatureAnimationDriver>()?.Cry?.ResetPresence();
        downedSource = null;
        EncounterId = Guid.NewGuid().ToString("N");
        ResetQuestEncounterTime();
        GetComponent<Capstone.Game.CreatureSystem.PveCombatState>()?.ResetEncounter();
        alive = true;
        health = Mathf.Max(1f, maxHealth);
        reviveAt = 0f;
        GetComponent<Capstone.Game.CreatureSystem.CreatureCombatant>()?.SyncEnemyHealth(health, maxHealth);
        flashUntil = 0f;
        SetCollidersEnabled(true);
        Capstone.Game.CreatureSystem.CreatureCombatant.RefreshPlayerPassThrough(gameObject);
        ApplyColor(idleColor);
        RefreshName();
        HealthChanged?.Invoke(this);
    }

    private void Die(GameObject source)
    {
        if (defeatCommitted)
        {
            return;
        }

        defeatCommitted = true;
        downed = false;
        alive = false;
        GetComponent<Capstone.Game.CreatureSystem.CombatBleed>()?.ClearWounds();
        reviveAt = Time.time + reviveDelay;
        SetCollidersEnabled(autoRevive);
        ApplyColor(deadColor);
        RefreshName();
        Capstone.Game.QuestSystem.ChapterQuestFlow.ReportDefeat(this, source);
        bool rewardHandled = !trainingDummy && Capstone.Game.CreatureSystem.PveRewardService.HandleDefeat(this, source);
        if (!trainingDummy && GetComponent<Capstone.Game.CreatureSystem.CreatureCombatant>() != null)
        {
            var driver = GetComponent<Capstone.Game.CreatureSystem.CreatureAnimationDriver>();
            if (driver != null)
            {
                driver.TryGetActionHash(Capstone.Game.CreatureSystem.CreatureAction.Death, out int deathHash);
                driver.Cry?.BeginDeath(deathHash);
            }
            var visual = GetComponent<Capstone.Game.CreatureSystem.EnemyDeathVisual>()
                ?? gameObject.AddComponent<Capstone.Game.CreatureSystem.EnemyDeathVisual>();
            visual.Begin();
        }
        if (!trainingDummy)
        {
            Defeated?.Invoke(source);
        }

        if (!rewardHandled && !trainingDummy && source != null)
        {
            source.GetComponent<PetController>()?.AwardCombatDefeat(this);
        }
    }

    internal void RecoverHealth(float amount)
    {
        if (!alive || CaptureLocked || amount <= 0 || health >= maxHealth)
        {
            return;
        }

        health = Mathf.Min(maxHealth, health + amount);
        GetComponent<Capstone.Game.CreatureSystem.CreatureCombatant>()?.SyncEnemyHealth(health, maxHealth);
        HealthChanged?.Invoke(this);
        RefreshName();
    }

    internal Bounds GetTargetBounds()
    {
        if (cachedRenderer != null)
        {
            return cachedRenderer.bounds;
        }

        if (colliders != null && colliders.Length > 0 && colliders[0] != null)
        {
            return colliders[0].bounds;
        }

        return new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
    }

    private void SetCollidersEnabled(bool enabledValue)
    {
        if (colliders == null)
        {
            return;
        }

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = enabledValue;
            }
        }
    }

    private void ApplyColor(Color color)
    {
        if (cachedRenderer == null || !tintFeedback)
        {
            return;
        }

        if (colorProperties == null)
        {
            colorProperties = new MaterialPropertyBlock();
        }

        cachedRenderer.GetPropertyBlock(colorProperties);
        colorProperties.SetColor("_BaseColor", color);
        colorProperties.SetColor("_Color", color);
        cachedRenderer.SetPropertyBlock(colorProperties);
    }

    private void RefreshName()
    {
        if (!writeHealthToName)
        {
            return;
        }

        gameObject.name = alive ? baseName + " (" + Mathf.CeilToInt(health) + " HP)" : baseName + " (KO)";
    }

    private void EnsureMapMarker()
    {
        if (mapMarker == null)
        {
            mapMarker = GetComponent<MapMarker>();
        }

        if (mapMarker == null && !IsBoss) return;
        if (mapMarker == null)
        {
            mapMarker = gameObject.AddComponent<MapIcon>();
        }

        string id = string.IsNullOrWhiteSpace(baseName) ? gameObject.name : baseName;
        mapMarker.ConfigureRuntime(IsBoss ? MapMarkerType.Boss : MapMarkerType.Enemy, id, id, null, default, true, true);
    }
}
