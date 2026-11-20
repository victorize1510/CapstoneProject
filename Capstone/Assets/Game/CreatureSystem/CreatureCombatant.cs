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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => active.Clear();
        PetHudRuntimeStats stats;
        PetCollectionMetadata metadata;
        PetController pet;
        DummyEnemy enemy;
        Collider body;
        PetStatSnapshot cachedStats;
        float refreshStatsAt;
        Animator collisionVisual;
        float offenseBuffUntil, offenseBuffMultiplier = 1;
        GameObject offenseBuffVisual;
        public float OffenseMultiplier => Time.time < offenseBuffUntil ? offenseBuffMultiplier : 1;
        public float OffenseBuffRemaining => Mathf.Max(0, offenseBuffUntil - Time.time);
        void Awake()
        {
            stats = GetComponent<PetHudRuntimeStats>();
            metadata = GetComponent<PetCollectionMetadata>();
            pet = GetComponent<PetController>();
            enemy = GetComponent<DummyEnemy>();
            body = GetComponent<Collider>();
            pet?.BindCombatant(this);
            BindVisual(stats.VisualAnimator);
        }

        void OnEnable()
        {
            active.Add(this);
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

            ClearOffenseBuff();
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
            offenseBuffUntil = 0;
            offenseBuffMultiplier = 1;
            if (offenseBuffVisual != null)
            {
                Skills.CombatEffectPool.Return(offenseBuffVisual);
                offenseBuffVisual = null;
            }
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
                ClearOffenseBuff();
            }

            collisionVisual = next;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule == null)
            {
                return; // Giu tuong thich voi BoxCollider va CharacterController cu.
            }

            if (next.TryGetComponent<CreatureVisualBinding>(out var binding) && binding.TryApplyActorGeometry(capsule))
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

                return cachedStats;
            }
        }

        public bool IsEnemy => enemy != null && enemy.enabled && (pet == null || pet.IsWildEncounter);
        public bool IsAlive => isActiveAndEnabled && stats != null && (IsEnemy ? enemy.IsAlive : stats.Health > 0);
        public bool CanBeAttacked => IsAlive && (IsEnemy ? !enemy.CaptureLocked : pet != null && pet.CanReceiveCommands);
        public Vector3 Center => body != null && body.enabled ? body.bounds.center : transform.position + Vector3.up * .5f;
        public Vector3 ClosestPoint(Vector3 point) => body != null && body.enabled ? body.ClosestPoint(point) : Center;
        public float MovementScale => CreatureCombatRules.MovementScale(Snapshot.Speed);
        public float AttackIntervalScale => CreatureCombatRules.AttackIntervalScale(Snapshot.Speed);
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
            return CreatureCombatRules.Mitigate(damage, kind == CreatureDamageKind.Magical ? value.MagicDefense : value.Defense);
        }

        public CombatElementSnapshot SnapshotElements(PetElement attack)
        {
            return new CombatElementSnapshot(attack, metadata != null ? metadata.Element : PetElement.Unknown, metadata != null ? metadata.SecondaryElement : PetElement.Unknown);
        }

        // Cung duong cho Pet va Enemy: phong thu truoc, khac he/STAB sau, chi mot lan.
        public float ResolveDamage(CombatHit hit)
        {
            return ResolveElementalDamage(Mitigate(hit.RawDamage, hit.Kind), hit, metadata);
        }

        internal static float ResolveElementalDamage(float mitigated, CombatHit hit, PetCollectionMetadata metadata)
        {
            if (hit.DamageOverTime || !hit.Elements.Enabled)
            {
                return mitigated;
            }

            return mitigated * ElementDamageMath.Multiplier(ElementCombatSettings.Lookup, hit.Elements, metadata != null ? metadata.Element : PetElement.Unknown, metadata != null ? metadata.SecondaryElement : PetElement.Unknown);
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
            stats.SetStatus(Mathf.Max(0, stats.Health - ResolveDamage(hit)), stats.MaxHealth, stats.Energy, stats.MaxEnergy);
            var result = new CombatDamageResult(hit, gameObject, before - stats.Health, stats.Health <= 0);
            CombatDamageEvents.Publish(result);
            if (stats.Health <= 0)
            {
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
