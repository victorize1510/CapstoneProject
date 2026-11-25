using Capstone.Game.CreatureSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    [DisallowMultipleComponent]
    public sealed class CombatStatusVisuals : MonoBehaviour {
        readonly GameObject[] instances = new GameObject[6];
        readonly float[] expires = new float[6];
        CreatureCombatTarget target;
        CombatBleed status;
        float nextRetry;

        static CombatAilment TrackedStatus(int index) => index switch
        {
            1 => CombatAilment.Negative,
            4 => CombatAilment.Shock,
            5 => CombatAilment.Burn,
            _ => CombatAilment.None
        };
        static PetSkillCatalog catalog;

        internal static void PlayStatus(CombatBleed status, CreatureCombatTarget target, CombatAilment kind) {
            if (!target.Alive || !CanShow(target)) return;
            var visual = For(target);
            visual.status = status;
            visual.enabled = true;
            visual.Play((kind & CombatAilment.Negative) != 0 ? 1 : 0);
            switch (kind) {
                case CombatAilment.Burn: visual.Play(5); break;
                case CombatAilment.Shock: visual.Play(4); break;
            }
        }

        internal static void PlayPotion(CreatureCombatant actor, bool mana) {
            if (actor != null && actor.gameObject.activeInHierarchy && (actor.IsEnemy || actor.GetComponent<PetController>()?.IsSummoned == true))
                For(new CreatureCombatTarget(actor)).Play(mana ? 3 : 2);
        }

        static bool CanShow(CreatureCombatTarget target) =>
            target.IsEnemy || target.Object.GetComponent<PetController>()?.IsSummoned == true;

        static CombatStatusVisuals For(CreatureCombatTarget target) {
            var view = target.Object.GetComponent<CombatStatusVisuals>() ?? target.Object.AddComponent<CombatStatusVisuals>();
            view.target = target;
            return view;
        }

        internal static void Clear(CreatureCombatant actor) {
            if (actor.TryGetComponent<CombatStatusVisuals>(out var view)) view.ClearAll();
        }

        void Play(int index) {
            if (catalog == null) catalog = Resources.Load<PetSkillCatalog>("PetSkills/PetSkillCatalog");
            if (catalog == null || !target.Exists) return;
            if (instances[index] != null && (TrackedStatus(index) != CombatAilment.None || Time.time + .15f < expires[index])) return;
            Release(index);
            var prefab = index switch {
                0 => catalog.buffAura, 1 => catalog.debuffAura, 2 => catalog.hpPotionAura,
                3 => catalog.mpPotionAura, 4 => catalog.shockAura, _ => catalog.burnAura
            };
            var body = target.Combatant?.HitVisualRenderer;
            var collider = GetComponent<Collider>();
            var bounds = body != null && body.enabled && body.bounds.size.sqrMagnitude > .001f
                ? body.bounds : target.Enemy != null ? target.Enemy.GetTargetBounds()
                : collider != null ? collider.bounds : new Bounds(target.Center, Vector3.one);
            Vector3 center = bounds.center;
            float height = index == 1 ? Mathf.Max(.12f, bounds.size.y * .25f) : 0f;
            var anchor = index >= 4 ? center : new Vector3(center.x, bounds.min.y + height, center.z);
            var effect = CombatEffectPool.Rent(prefab, anchor, Quaternion.identity, transform);
            if (effect == null) return;
            float quality = SettingsSystem.GameSettingsService.EffectsQuality == PetSummon.PetVFXQuality.Low ? .8f : 1f;
            Vector3 scale = new Vector3(Mathf.Max(.3f, bounds.size.x), Mathf.Max(.4f, bounds.size.y),
                Mathf.Max(.3f, bounds.size.z)) * (.65f * quality);
            Vector3 parentScale = transform.lossyScale;
            effect.transform.localScale = new Vector3(scale.x / Mathf.Max(.01f, Mathf.Abs(parentScale.x)),
                scale.y / Mathf.Max(.01f, Mathf.Abs(parentScale.y)), scale.z / Mathf.Max(.01f, Mathf.Abs(parentScale.z)));
            if (index == 3) {
                foreach (var particle in effect.GetComponentsInChildren<ParticleSystem>(true)) {
                    var main = particle.main;
                    var gradient = main.startColor;
                    main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .76f, .22f, gradient.colorMax.a));
                    particle.Clear(false);
                    particle.Play(false);
                }
            }
            instances[index] = effect;
            expires[index] = Time.time + 1.1f;
            enabled = true;
        }

        void Update() {
            if (!target.Alive || !CanShow(target)) { enabled = false; return; }
            var ailments = status != null ? status.ActiveAilments : CombatAilment.None;
            bool active = false;
            bool retry = Time.time >= nextRetry;
            if (retry) nextRetry = Time.time + .25f;
            for (int i = 0; i < instances.Length; i++) {
                var tracked = TrackedStatus(i);
                bool activeStatus = (tracked & ailments) != 0;
                if (tracked != CombatAilment.None ? !activeStatus : Time.time >= expires[i]) Release(i);
                else if (activeStatus && retry && instances[i] == null) Play(i);
                active |= activeStatus || instances[i] != null;
            }
            if (!active) enabled = false;
        }

        void Release(int index) {
            if (instances[index] != null) CombatEffectPool.Return(instances[index]);
            instances[index] = null;
        }

        void ClearAll() {
            for (int i = 0; i < instances.Length; i++) Release(i);
        }
        void OnDisable() => ClearAll();
    }
}
