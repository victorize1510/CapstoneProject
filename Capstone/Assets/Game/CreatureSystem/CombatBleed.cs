using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Capstone.Game.CreatureSystem
{
    /// <summary>Vet thuong thuoc muc tieu van chay khi thu nguon; khong save, coroutine hay tim scene moi frame.</summary>
    [DisallowMultipleComponent]
    public sealed class CombatBleed : MonoBehaviour
    {
        sealed class Wound
        {
            public GameObject source;
            public string sourceId, skill;
            public int cast, remaining;
            public float raw, next;
        }

        readonly List<Wound> wounds = new List<Wound>(4);
        CreatureCombatTarget target;
        bool startedAsEnemy;
        CreatureCombatant role;
        int sceneHandle;
        public int ActiveWounds => wounds.Count;
        public int TicksApplied { get; private set; }

        internal void ClearWounds()
        {
            wounds.Clear();
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
            wounds.Clear();
        }

        void SceneChanged(Scene previous, Scene next)
        {
            wounds.Clear();
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

            effect.Refresh(hit, rawPerTick);
        }

        void Refresh(CombatHit hit, float raw)
        {
            var metadata = hit.Source != null ? hit.Source.GetComponent<HudSystem.PetCollectionMetadata>() : null;
            string key = metadata != null ? metadata.PetId : hit.Source != null ? hit.Source.GetInstanceID().ToString() : "unknown";
            Wound wound = null;
            foreach (var existing in wounds)
            {
                if (existing.sourceId == key)
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
            wound.sourceId = key;
            wound.skill = hit.SkillId;
            wound.cast = hit.CastId;
            wound.raw = raw;
            wound.remaining = 3;
            wound.next = Time.time + 1;
        }

        void Update()
        {
            if (UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if (!target.Exists || gameObject.scene.handle != sceneHandle || (startedAsEnemy && !target.IsEnemy) || (target.IsEnemy && target.Enemy != null && !target.Enemy.IsAlive) || (role != null && !role.IsAlive))
            {
                wounds.Clear();
                return;
            }

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
                    var hit = new CombatHit(wound.source, wound.skill, wound.cast, wound.raw, CreatureDamageKind.Physical, false, true, target.Center);
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
                }

                if (target.IsEnemy && !target.Alive)
                {
                    wounds.Clear();
                    break;
                }
            }
        }
    }
}
