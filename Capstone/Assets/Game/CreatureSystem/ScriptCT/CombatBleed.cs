using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem
{
    [System.Flags]
    public enum CombatAilment {
        None = 0, Bleed = 1, Burn = 2, Shock = 4, Poison = 8, Stun = 16, Root = 32, Slow = 64,
        DefenseDown = 128, MagicDefenseDown = 256, PowerUp = 512, DefenseUp = 1024,
        Shield = 2048, Invulnerable = 4096, Regeneration = 8192, Rain = 16384,
        FruitAttack = 32768, FruitDefense = 65536, FruitMagicAttack = 131072, FruitMagicDefense = 262144, FruitSpeed = 524288,
        Negative = Bleed | Burn | Shock | Poison | Stun | Root | Slow | DefenseDown | MagicDefenseDown,
        All = ~0
    }

    /// <summary>Vet thuong thuoc muc tieu van chay khi thu nguon; khong save, coroutine hay tim scene moi frame.</summary>
    [DisallowMultipleComponent]
    public sealed partial class CombatBleed : MonoBehaviour
    {
        sealed class Wound
        {
            public GameObject source;
            public string sourceId, skill;
            public int cast, remaining;
            public float raw, next;
            public CombatAilment kind;
            public bool resolvedDamage;
        }

        readonly List<Wound> wounds = new List<Wound>(4);
        CreatureCombatTarget target;
        bool startedAsEnemy;
        CreatureCombatant role;
        int sceneHandle;
        public int ActiveWounds => wounds.Count;
        public int TicksApplied { get; private set; }
        public bool Shocked => Magnitude(CombatAilment.Shock) > 0f;

        internal void ClearWounds()
        {
            bool changed = wounds.Count > 0 || effects.Count > 0;
            wounds.Clear();
            effects.Clear();
            stunImmuneUntil = 0;
            fireContactUntil = 0;
            fireContactCooldown?.Clear();
            if (changed) Changed?.Invoke();
        }

        internal void Cleanse(CombatAilment mask) {
            mask &= CombatAilment.Negative;
            int removed = wounds.RemoveAll(wound => (wound.kind & mask) != 0);
            removed += effects.RemoveAll(effect => (effect.Kind & mask) != 0);
            if (removed > 0) Changed?.Invoke();
        }

        void Awake()
        {
            role = GetComponent<CreatureCombatant>();
            target = role != null ? new CreatureCombatTarget(role) : new CreatureCombatTarget(GetComponent<DummyEnemy>());
            startedAsEnemy = target.IsEnemy;
            sceneHandle = gameObject.scene.handle;
        }

        void OnEnable()
        {
            SceneManager.activeSceneChanged += SceneChanged;
        }

        void OnDisable()
        {
            SceneManager.activeSceneChanged -= SceneChanged;
            ClearWounds();
        }

        void SceneChanged(Scene previous, Scene next)
        {
            ClearWounds();
        }

        public static void Apply(DummyEnemy victim, CombatHit hit, float rawPerTick)
        {
            ApplyTarget(new CreatureCombatTarget(victim), hit, rawPerTick);
        }

        public static void ApplyTarget(CreatureCombatTarget victim, CombatHit hit, float rawPerTick)
        {
            if (!victim.Exists || !victim.Alive || rawPerTick <= 0 || float.IsInfinity(rawPerTick) || float.IsNaN(rawPerTick))
            {
                return;
            }

            var effect = victim.Object.GetComponent<CombatBleed>();
            if (effect == null)
            {
                effect = victim.Object.AddComponent<CombatBleed>();
            }

            effect.Refresh(hit, rawPerTick, CombatAilment.Bleed);
        }

        internal void Refresh(CombatHit hit, float raw, CombatAilment kind, int ticks = 3, bool fromSheet = false)
        {
            var metadata = hit.Source != null ? hit.Source.GetComponent<HudSystem.PetCollectionMetadata>() : null;
            string key = metadata != null ? metadata.PetId : hit.Source != null ? hit.Source.GetInstanceID().ToString() : "unknown";
            Wound wound = null;
            foreach (var existing in wounds)
            {
                if (existing.kind == kind && (fromSheet || kind == CombatAilment.Burn || existing.sourceId == key))
                {
                    wound = existing;
                    break;
                }
            }

            if (wound == null)
            {
                if (wounds.Count == 8)
                {
                    wounds.RemoveAt(0);
                }

                wound = new Wound();
                wounds.Add(wound);
            }

            wound.source = hit.Source;
            wound.kind = kind;
            wound.sourceId = key;
            wound.skill = hit.SkillId;
            wound.cast = hit.CastId;
            wound.raw = raw;
            wound.resolvedDamage = fromSheet;
            wound.remaining = Mathf.Max(1, ticks);
            wound.next = Time.time + 1;
            NotifyApplied(kind);
        }

        void Update()
        {
            if (UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            bool changedRole = target.IsEnemy != startedAsEnemy;
            if (changedRole) startedAsEnemy = target.IsEnemy;
            if (!target.Exists || gameObject.scene.handle != sceneHandle || changedRole || (target.IsEnemy && target.Enemy != null && !target.Enemy.IsAlive) || (role != null && !role.IsAlive))
            {
                ClearWounds();
                return;
            }

            TickEffects();
            for (int i = wounds.Count - 1; i >= 0; i--)
            {
                var wound = wounds[i];
                if (Time.time < wound.next)
                {
                    continue;
                }

                // Nhip bi khoa duoc bo qua, khong don damage de phat bu ve sau.
                int elapsed = Mathf.Min(wound.remaining, 1 + Mathf.FloorToInt(Time.time - wound.next));
                wound.next += elapsed;
                wound.remaining -= elapsed;
                for (int due = 0; due < elapsed && target.Alive; due++)
                {
                    var kind = wound.kind == CombatAilment.Bleed ? CreatureDamageKind.Physical : CreatureDamageKind.Magical;
                    var hit = new CombatHit(wound.source, wound.skill, wound.cast, wound.raw, kind, false, true,
                        target.Center, 0, default, 1, wound.resolvedDamage);
                    if (target.Apply(hit).HpLost > 0)
                    {
                        TicksApplied++;
                    }
                }

                if (i >= wounds.Count || !ReferenceEquals(wounds[i], wound))
                {
                    break; // Chet/bat co the xoa vet thuong ngay trong ApplyCombatHit.
                }

                if (wound.remaining == 0 || !target.Alive)
                {
                    wounds.RemoveAt(i);
                    Changed?.Invoke();
                }

                if (target.IsEnemy && !target.Alive)
                {
                    ClearWounds();
                    break;
                }
            }
        }
    }
}
