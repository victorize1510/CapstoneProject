using Capstone.Game.HudSystem;
using Capstone.Game.Elements;
using System.Collections.Generic;
using UnityEngine;

namespace Capstone.Game.CreatureSystem
{
    public enum CreatureDamageKind
    {
        Physical,
        Magical
    }

    [DisallowMultipleComponent, RequireComponent(typeof(PetHudRuntimeStats), typeof(PetCollectionMetadata), typeof(CapsuleCollider))]
    public sealed class CreatureCombatant : MonoBehaviour
    {
        static readonly HashSet<CreatureCombatant> active = new HashSet<CreatureCombatant>();
        public static IReadOnlyCollection<CreatureCombatant> Active => active;
        static readonly HashSet<CharacterController> playerBodies = new();
        static readonly List<Collider> playerCollisionScratch = new(8);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry()
        {
            active.Clear();
            playerBodies.Clear();
            playerCollisionScratch.Clear();
        }

        internal static void RegisterPlayerBody(CharacterController controller)
        {
            if (controller == null) return;
            playerBodies.Add(controller);
            foreach (var actor in active)
                if (actor != null) ApplyPlayerPassThrough(controller, actor.gameObject);
            foreach (var enemy in DummyEnemy.ActiveEnemies)
                if (enemy != null && !enemy.TryGetComponent<CreatureCombatant>(out _))
                    ApplyPlayerPassThrough(controller, enemy.gameObject);
        }

        internal static void UnregisterPlayerBody(CharacterController controller) => playerBodies.Remove(controller);

        internal static void RefreshPlayerPassThrough(GameObject actor)
        {
            if (!Application.isPlaying || !actor.activeInHierarchy) return;
            foreach (var player in playerBodies) ApplyPlayerPassThrough(player, actor);
        }

        static void ApplyPlayerPassThrough(CharacterController player, GameObject actor)
        {
            if (player == null || !player.enabled || !player.gameObject.activeInHierarchy) return;
            actor.GetComponentsInChildren(false, playerCollisionScratch);
            foreach (var bodyCollider in playerCollisionScratch)
            {
                if (bodyCollider != null && bodyCollider != player && bodyCollider.enabled && !bodyCollider.isTrigger &&
                    !Physics.GetIgnoreCollision(player, bodyCollider))
                    Physics.IgnoreCollision(player, bodyCollider);
            }
            playerCollisionScratch.Clear();
        }
        PetHudRuntimeStats stats;
        PetCollectionMetadata metadata;
        PetController pet;
        DummyEnemy enemy;
        Collider body;
        PetStatSnapshot cachedStats;
        float refreshStatsAt;
        Animator collisionVisual;
        Transform hitVisualAnchor;
        internal Transform HitVisualGround { get; private set; }
        internal Renderer HitVisualRenderer { get; private set; }
        float offenseBuffUntil, offenseBuffMultiplier = 1;
        GameObject offenseBuffVisual;
        public CombatBleed Status { get; private set; }
        public float OffenseMultiplier => Mathf.Max(Time.time < offenseBuffUntil ? offenseBuffMultiplier : 1,
            1 + (Status?.Magnitude(CombatAilment.PowerUp) ?? 0));
        public bool MovementLocked => Status != null && Status.Has(CombatAilment.Root | CombatAilment.Stun);
        public bool Stunned => Status != null && Status.Has(CombatAilment.Stun);
        public bool AttackLocked => Status != null && Status.Has(CombatAilment.Stun | CombatAilment.Invulnerable);
        internal float MaximumHealth => IsEnemy ? enemy.maxHealth : stats.MaxHealth;
        internal int MagicAttack => Snapshot.MagicAttack;
        internal bool QualifiesForRain => metadata != null &&
            (RainElement(metadata.Element) || RainElement(metadata.SecondaryElement));
        static bool RainElement(PetElement type) => type == PetElement.Nature || type == PetElement.Water || type == PetElement.Electric;
        public float OffenseBuffRemaining => Mathf.Max(0, offenseBuffUntil - Time.time);
        internal Transform HitVisualAnchor => hitVisualAnchor != null && hitVisualAnchor.gameObject.activeInHierarchy
            ? hitVisualAnchor : null;
        void Awake()
        {
            stats = GetComponent<PetHudRuntimeStats>();
            metadata = GetComponent<PetCollectionMetadata>();
            pet = GetComponent<PetController>();
            enemy = GetComponent<DummyEnemy>();
            body = GetComponent<Collider>();
            pet?.BindCombatant(this);
            BindVisual(stats.VisualAnimator);
            if (Application.isPlaying) Status = CombatBleed.For(gameObject);
        }

        void OnEnable()
        {
            active.Add(this);
            RefreshPlayerPassThrough(gameObject);
            Capstone.Game.WorldFeedback.CameraActorOccluder.Ensure(gameObject);
            if (Application.isPlaying && GetComponent<PveCombatState>() == null)
            {
                gameObject.AddComponent<PveCombatState>();
            }

            if (metadata != null)
            {
                metadata.Changed += Invalidate;
            }

            if (stats != null)
            {
                stats.PersistentDataChanged += Invalidate;
            }

            Invalidate();
        }

        void OnDisable()
        {
            active.Remove(this);
            if (metadata != null)
            {
                metadata.Changed -= Invalidate;
            }

            if (stats != null)
            {
                stats.PersistentDataChanged -= Invalidate;
            }

            ClearTransientEffects();
        }

        void Update()
        {
            if (UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if (offenseBuffUntil > 0 && (Time.time >= offenseBuffUntil || !IsAlive || (!IsEnemy && pet != null && !pet.CanReceiveCommands)))
            {
                ClearOffenseBuff();
            }
        }

        public void ApplyOffenseBuff(float percent, float duration, GameObject visual)
        {
            if (!IsAlive || float.IsNaN(percent) || float.IsNaN(duration) || float.IsInfinity(percent) || float.IsInfinity(duration))
            {
                return;
            }

            ClearOffenseBuff(); // Dung lai chi lam moi mot buff, khong cong don he so.
            offenseBuffMultiplier = 1 + Mathf.Clamp(percent, 0, 100) / 100;
            offenseBuffUntil = Time.time + Mathf.Clamp(duration, .1f, 60);
            Status?.NotifyApplied(CombatAilment.PowerUp);
            if (visual != null)
            {
                offenseBuffVisual = Skills.CombatEffectPool.Rent(visual, Center, transform.rotation, transform);
                if (offenseBuffVisual != null)
                {
                    offenseBuffVisual.transform.localPosition = transform.InverseTransformPoint(Center);
                    offenseBuffVisual.transform.localScale = Vector3.one * Mathf.Clamp(body != null ? body.bounds.extents.x * 1.6f : 1, .75f, 2);
                }
            }
        }

        public void ClearOffenseBuff()
        {
            bool changed = offenseBuffUntil > 0;
            offenseBuffUntil = 0;
            offenseBuffMultiplier = 1;
            if (offenseBuffVisual != null)
            {
                Skills.CombatEffectPool.Return(offenseBuffVisual);
                offenseBuffVisual = null;
            }
            if (changed) Status?.NotifyChanged();
        }

        void Invalidate()
        {
            refreshStatsAt = -1;
        }

        // Chi canh collider khi doi model, khong do lai theo tung pose animation.
        // Dung cung luong khi load form va tien hoa tai cho.
        internal void BindVisual(Animator next)
        {
            if (next == null || collisionVisual == next)
            {
                return;
            }

            if (collisionVisual != null)
            {
                ClearTransientEffects();
            }

            collisionVisual = next;
            RefreshPlayerPassThrough(gameObject);
            var binding = next.GetComponent<CreatureVisualBinding>();
            hitVisualAnchor = binding != null ? binding.FindSocket("Body") : null;
            HitVisualGround = binding != null ? binding.FindSocket("Ground") : null;
            if (HitVisualGround == null) HitVisualGround = next.transform;
            HitVisualRenderer = next.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (HitVisualRenderer == null) HitVisualRenderer = next.GetComponentInChildren<MeshRenderer>(true);
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return; // Giu tuong thich voi BoxCollider va CharacterController cu.
            }

            if (binding != null && binding.TryApplyActorGeometry(capsule))
            {
                body = capsule;
                return;
            }

            bool found = false;
            Bounds bounds = default;
            foreach (var renderer in next.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is SkinnedMeshRenderer) && !(renderer is MeshRenderer))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (!found || bounds.size.sqrMagnitude < .001f)
            {
                return;
            }

            Vector3 size = transform.InverseTransformVector(bounds.size);
            capsule.radius = Mathf.Clamp(Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.z)) * .23f, .2f, .65f);
            capsule.height = Mathf.Max(capsule.radius * 2, Mathf.Abs(size.y));
            Vector3 center = transform.InverseTransformPoint(bounds.center);
            center.y = Mathf.Max(center.y, capsule.height * .5f + .04f);
            capsule.center = center;
            body = capsule;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
            {
                agent.radius = capsule.radius;
                agent.height = capsule.height;
            }
        }

        PetStatSnapshot Snapshot
        {
            get
            {
                if (Time.time >= refreshStatsAt)
                {
                    cachedStats = metadata.CurrentStats;
                    refreshStatsAt = Time.time + .5f;
                }

                return new PetStatSnapshot(cachedStats.Hp, WithItemBuff(cachedStats.Attack, PetCoreStat.Attack),
                    WithItemBuff(cachedStats.Defense, PetCoreStat.Defense), WithItemBuff(cachedStats.MagicAttack, PetCoreStat.MagicAttack),
                    WithItemBuff(cachedStats.MagicDefense, PetCoreStat.MagicDefense), WithItemBuff(cachedStats.Speed, PetCoreStat.Speed),
                    cachedStats.MaxHealth, cachedStats.CriticalRate, cachedStats.CriticalDamage);
            }
        }

        public bool IsEnemy => enemy != null && enemy.enabled && (pet == null || pet.IsWildEncounter);
        public bool IsAlive => isActiveAndEnabled && stats != null && (IsEnemy ? enemy.IsAlive : stats.Health > 0);
        public bool CanBeAttacked => IsAlive && (IsEnemy ? !enemy.CaptureLocked : pet != null && pet.CanReceiveCommands);
        public Vector3 Center => body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up * .5f;
        public Vector3 ClosestPoint(Vector3 point) => body != null && body.enabled ? body.ClosestPoint(point) : Center;
        float SlowScale => 1f - Mathf.Min(.4f, Mathf.Max(Status?.Magnitude(CombatAilment.Slow) ?? 0, Status?.Magnitude(CombatAilment.Shock) ?? 0));
        public float MovementScale => CreatureCombatRules.MovementScale(Snapshot.Speed) * SlowScale;
        public float AttackIntervalScale => CreatureCombatRules.AttackIntervalScale(Snapshot.Speed) / SlowScale;

        static CombatAilment FruitBuff(PetCoreStat stat) => stat switch {
            PetCoreStat.Attack => CombatAilment.FruitAttack,
            PetCoreStat.Defense => CombatAilment.FruitDefense,
            PetCoreStat.MagicAttack => CombatAilment.FruitMagicAttack,
            PetCoreStat.MagicDefense => CombatAilment.FruitMagicDefense,
            PetCoreStat.Speed => CombatAilment.FruitSpeed,
            _ => CombatAilment.None
        };
        int WithItemBuff(int value, PetCoreStat stat) => Mathf.RoundToInt(value * (1 + (Status?.Magnitude(FruitBuff(stat)) ?? 0)));
        internal void ApplyItemBuff(PetCoreStat stat) {
            var buff = FruitBuff(stat);
            if (buff != CombatAilment.None) Status?.ApplyTimed(buff, .15f, 15f, gameObject, string.Empty);
        }
        internal void ClearTransientEffects() {
            ClearOffenseBuff();
            Status?.ClearWounds();
            Skills.CombatStatusVisuals.Clear(this);
        }
        internal void HealFromStatus(float hp) {
            if (!IsAlive || hp <= 0) return;
            if (IsEnemy) enemy.RecoverHealth(hp);
            else stats.ApplyPassiveRecovery(hp, 0, true);
        }
        internal bool MayUseSkill(Skills.PetSkillCombatProfile profile) => !Stunned
            && (profile == null || profile.delivery == Skills.PetSkillDelivery.SelfBuff || !AttackLocked);

        internal void Displace(Vector3 direction, float distance) {
            if (!CanBeAttacked || (enemy != null && enemy.IsBoss) || distance <= 0) return;
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) return;
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent == null || !agent.enabled || !agent.isOnNavMesh) return;
            var destination = transform.position + direction.normalized * distance;
            if (!agent.Raycast(destination, out _)) agent.Move(destination - transform.position);
        }
        public float Offense(CreatureDamageKind kind, float power = 1, float bonus = 0) => RollOffense(kind, power, bonus).RawDamage;
        public CombatOffenseRoll RollOffense(CreatureDamageKind kind, float power = 1, float bonus = 0)
        {
            var value = Snapshot;
            float roll = Random.value;
            return new CombatOffenseRoll(CreatureCombatRules.Offense(value, kind, power * OffenseMultiplier, bonus, roll), roll < value.CriticalRate / 100f);
        }

        public float NonCriticalOffense(CreatureDamageKind kind, float power) => CreatureCombatRules.Offense(Snapshot, kind, power * OffenseMultiplier, 0, 1);
        public float Mitigate(float damage, CreatureDamageKind kind)
        {
            var value = Snapshot;
            bool magical = kind == CreatureDamageKind.Magical;
            var down = magical ? CombatAilment.MagicDefenseDown : CombatAilment.DefenseDown;
            float up = Mathf.Max(Status?.Magnitude(CombatAilment.DefenseUp) ?? 0, Status?.Magnitude(CombatAilment.Rain) ?? 0);
            int defense = magical ? value.MagicDefense : value.Defense;
            return CreatureCombatRules.Mitigate(damage, Mathf.RoundToInt(defense * (1 + up) * (1 - (Status?.Magnitude(down) ?? 0))));
        }

        public CombatElementSnapshot SnapshotElements(PetElement attack)
        {
            return new CombatElementSnapshot(attack, metadata != null ? metadata.Element : PetElement.Unknown, metadata != null ? metadata.SecondaryElement : PetElement.Unknown);
        }

        // Cung duong cho Pet va Enemy: phong thu truoc, khac he/STAB sau, chi mot lan.
        public float ResolveDamage(CombatHit hit)
        {
            return ResolveDamage(hit, out _);
        }

        internal float ResolveDamage(CombatHit hit, out float effectiveness)
        {
            float damage = hit.ResolvedDamage ? hit.RawDamage : Mitigate(hit.RawDamage, hit.Kind);
            return ResolveElementalDamage(damage, hit, metadata, out effectiveness);
        }

        internal static float ResolveElementalDamage(float mitigated, CombatHit hit, PetCollectionMetadata metadata, out float effectiveness)
        {
            effectiveness = 1f;
            if (hit.DamageOverTime || !hit.Elements.Enabled)
            {
                return mitigated;
            }

            effectiveness = ElementDamageMath.Effectiveness(ElementCombatSettings.Lookup, hit.Elements.Attack,
                metadata != null ? metadata.Element : PetElement.Unknown, metadata != null ? metadata.SecondaryElement : PetElement.Unknown);
            return mitigated * (effectiveness * ElementDamageMath.Stab(hit.Elements));
        }

        public bool ReceiveDamage(float rawDamage, CreatureDamageKind kind, GameObject source)
        {
            return ReceiveHit(new CombatHit(source, "basic", 0, rawDamage, kind, false, false, Center));
        }

        public bool ReceiveHit(CombatHit hit) => ApplyCombatHit(hit).HpLost > 0;
        public CombatDamageResult ApplyCombatHit(CombatHit hit)
        {
            if (UISystem.GameplayPause.IsPaused || !CanBeAttacked || hit.RawDamage <= 0 || float.IsNaN(hit.RawDamage) || float.IsInfinity(hit.RawDamage))
            {
                return new CombatDamageResult(hit, gameObject, 0, false);
            }

            if (IsEnemy)
            {
                return enemy.ApplyCombatHit(hit);
            }

            float before = stats.Health;
            float damage = ResolveDamage(hit, out float effectiveness);
            float remaining = Status != null ? Status.Absorb(damage) : damage;
            if (remaining > 0)
                stats.SetStatus(Mathf.Max(0, stats.Health - remaining), stats.MaxHealth, stats.Energy, stats.MaxEnergy);
            var result = new CombatDamageResult(hit, gameObject, before - stats.Health, stats.Health <= 0, effectiveness,
                Mathf.Max(0, damage - remaining));
            CombatDamageEvents.Publish(result);
            if (stats.Health <= 0)
            {
                ClearTransientEffects();
                pet.HandleCombatDefeat();
            }

            return result;
        }

        internal void SyncEnemyHealth(float current, float maximum)
        {
            if (stats != null)
            {
                stats.SetStatus(current, maximum, stats.Energy, stats.MaxEnergy);
            }
        }
    }
}
