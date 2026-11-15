using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using UnityEngine;

namespace Capstone.Game.Skills {
    /// <summary>A cast snapshots its target once; one release, one hit, bounded projectile lifetime.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PetSkillPresentationPlayer), typeof(CreatureCombatant))]
    public sealed class PetSkillCombatExecutor : MonoBehaviour {
        sealed class Shot {
            public int token; public string id; public DummyEnemy target; public PetSkillCombatProfile profile;
            public Vector3 position, direction; public float expires, remaining; public bool released;
            public GameObject visual; public CombatOffenseRoll damage; public float bleed;
        }
        PetController pet;
        CreatureCombatant combat;
        PetSkillPresentationPlayer presentation;
        PetHudRuntimeStats stats;
        DummyEnemy preparedTarget;
        PetSkillCombatProfile preparedProfile;
        string preparedId;
        Vector3 preparedDirection;
        public string LastTargetMode { get; private set; }
        public DummyEnemy LastCastTarget { get; private set; }
        Vector3 retreatDirection;
        float preparedRetreatDistance, retreatRemaining, retreatSpeed, retreatUntil;
        public float PreparedRetreatSeconds => preparedProfile != null && preparedRetreatDistance > 0 ? preparedRetreatDistance / Mathf.Max(.1f, preparedProfile.retreatSpeed) : 0;
        readonly List<Shot> shots = new List<Shot>(8);
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        public int HitCount { get; private set; }
        public int BuffAppliedCount { get; private set; }
        public int ActiveProjectileCount => shots.Count;
        public string LastRejection { get; private set; }
        public bool IsRetreating => retreatRemaining > .001f;
        public Vector3 AimDirection => LastCastTarget != null && LastCastTarget.IsAlive ? LastCastTarget.TargetPosition - transform.position : castFacing;
        Vector3 castFacing;
        void OnEnable() {
            pet = GetComponent<PetController>(); combat = GetComponent<CreatureCombatant>(); stats = GetComponent<PetHudRuntimeStats>(); presentation = GetComponent<PetSkillPresentationPlayer>();
            presentation.SkillCastStarted += CastStarted; presentation.SkillReleased += Release;
        }
        void OnDisable() { if (presentation != null) { presentation.SkillCastStarted -= CastStarted; presentation.SkillReleased -= Release; } Cancel(); }
        public bool Prepare(string id, PetSkillCombatProfile profile) {
            preparedProfile = null; preparedTarget = null; preparedId = null; LastRejection = "";
            preparedRetreatDistance = 0;
            preparedDirection = transform.forward; LastTargetMode = "Forward";
            if (profile == null) return true;
            if (profile.delivery == PetSkillDelivery.SelfBuff) { preparedProfile = profile; preparedId = id; LastTargetMode = "Self"; return true; }
            DummyEnemy target = null;
            var input = pet != null && pet.owner != null ? pet.owner.GetComponent<PetCommandInput>() : null;
            if (input != null && input.cameraLock != null && input.cameraLock.TryGetLockedEnemy(out var pinned) && InReach(pinned, profile, combat.Center)) { target = pinned; LastTargetMode = "Locked"; }
            if (target == null && pet != null && InReach(pet.CombatTarget, profile, combat.Center)) { target = pet.CombatTarget; LastTargetMode = "Combat"; }
            if (target == null && profile.autoAcquireTarget) { target = Nearest(profile, combat.Center, transform.forward, false); if (target != null) LastTargetMode = "Nearest"; }
            if (target != null && profile.delivery != PetSkillDelivery.Melee && profile.retreatDistance > 0) {
                Vector3 separation = combat.Center - target.TargetPosition; separation.y = 0;
                preparedRetreatDistance = Mathf.Min(profile.retreatDistance, Mathf.Max(0, profile.preferredDistance - separation.magnitude));
            }
            preparedTarget = target; preparedProfile = profile; preparedId = id; return true;
        }
        void CastStarted(int token, string id) {
            if (preparedProfile == null || preparedId != id) return;
            Vector3 facing=preparedTarget != null ? preparedTarget.TargetPosition-transform.position : preparedDirection;facing.y=0;
            castFacing = facing.sqrMagnitude > .001f ? facing.normalized : transform.forward;
            LastCastTarget = preparedTarget;
            retreatDirection = -castFacing; retreatDirection.y = 0; retreatDirection.Normalize(); retreatRemaining = preparedRetreatDistance;
            retreatSpeed = Mathf.Max(.1f, preparedProfile.retreatSpeed); retreatUntil = Time.time + PreparedRetreatSeconds;
            for (int i = shots.Count - 1; i >= 0; i--) if (!shots[i].released) Remove(i);
            if (shots.Count >= 8) Remove(0);
            shots.Add(new Shot { token = token, id = id, target = preparedTarget, profile = preparedProfile, direction = preparedDirection,
                damage = preparedProfile.delivery == PetSkillDelivery.SelfBuff ? default : combat.RollOffense(preparedProfile.damageKind, preparedProfile.power),
                bleed = preparedProfile.bleedPower > 0 ? combat.NonCriticalOffense(CreatureDamageKind.Physical, preparedProfile.bleedPower) : 0, expires = Time.time + 10 });
            preparedProfile = null; preparedTarget = null; preparedRetreatDistance = 0;
        }
        void Release(int token, string id) {
            foreach (var shot in shots) if (shot.token == token && !shot.released) {
                shot.released = true;
                if (shot.profile.delivery == PetSkillDelivery.SelfBuff) {
                    if (presentation.NotifyImpact(token, combat.Center, Vector3.up)) {
                        combat.ApplyOffenseBuff(shot.profile.offenseBuffPercent, shot.profile.buffDuration, shot.profile.buffVisual); BuffAppliedCount++;
                    }
                    shot.expires = 0; return;
                }
                Vector3 start = presentation.SocketPosition(shot.profile.releaseSocket);
                shot.direction = transform.forward;
                if (shot.profile.delivery != PetSkillDelivery.Projectile) {
                    bool aimed = InReach(shot.target, shot.profile, combat.Center) && (shot.profile.delivery == PetSkillDelivery.Targeted || InArc(shot.target, combat.Center, shot.direction, shot.profile.meleeHalfAngle));
                    var victim = aimed ? shot.target : Nearest(shot.profile, combat.Center, shot.direction, true);
                    if (victim != null) Hit(shot, victim, victim.ClosestPoint(start), -shot.direction);
                    else LastRejection = "Không có mục tiêu trong vùng đánh: " + LastTargetMode;
                    shot.expires = 0; return;
                }
                if (InReach(shot.target, shot.profile, start)) shot.direction = (shot.target.TargetPosition - start).normalized;
                shot.position = start; shot.remaining = shot.profile.range; shot.expires = Time.time + shot.profile.lifetime;
                // SphereCast does not report colliders already overlapping the muzzle.
                if (ResolveInitialOverlap(shot)) { shot.expires = 0; return; }
                if (shot.profile.projectileVisual != null) {
                    shot.visual = CombatEffectPool.Rent(shot.profile.projectileVisual, start, Quaternion.LookRotation(shot.direction));
                    shot.visual.transform.localScale = Vector3.one * shot.profile.visualScale;
                }
                return;
            }
        }
        void Update() {
            if (pet == null || !pet.CanReceiveCommands || stats.Health <= 0 || InventoryInputController.GameplayInputBlocked) { if(shots.Count>0)presentation.CancelPresentation(); Cancel(); return; }
            if (retreatRemaining > 0) {
                float step = Mathf.Min(retreatRemaining, retreatSpeed * Time.deltaTime);
                if (Time.time <= retreatUntil + .08f && pet.TrySkillRetreatStep(retreatDirection, step)) retreatRemaining -= step;
                else retreatRemaining = 0;
            }
            for (int i = shots.Count - 1; i >= 0; i--) {
                var shot = shots[i];
                if (Time.time >= shot.expires) { Remove(i); continue; }
                if (!shot.released || shot.profile.delivery != PetSkillDelivery.Projectile) continue;
                float step = Mathf.Min(shot.remaining, shot.profile.projectileSpeed * Time.deltaTime);
                int count = Physics.SphereCastNonAlloc(shot.position, shot.profile.projectileRadius, shot.direction, hits, step, ~0, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) { Remove(i); continue; }
                int nearest = -1; float distance = float.PositiveInfinity;
                for (int h = 0; h < count; h++) if (!SourceCollider(hits[h].collider) && hits[h].distance < distance) { nearest = h; distance = hits[h].distance; }
                if (nearest >= 0) {
                    var hit = hits[nearest];
                    var victim = hit.collider.GetComponentInParent<DummyEnemy>();
                    if (Valid(victim)) Hit(shot, victim, hit.point, hit.normal);
                    else presentation.NotifyImpact(shot.token, hit.point, hit.normal); // Surface impact only: no damage/result/recoil.
                    Remove(i); continue;
                }
                shot.position += shot.direction * step; shot.remaining -= step;
                if (shot.visual != null) shot.visual.transform.position = shot.position;
                if (shot.remaining <= .001f) Remove(i);
            }
        }
        bool ClearLine(Vector3 from, Vector3 to, DummyEnemy target) {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f) return true;
            int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) return false;
            for (int i = 0; i < count; i++) if (!SourceCollider(hits[i].collider) && hits[i].collider.GetComponentInParent<DummyEnemy>() != target) return false;
            return true;
        }
        bool SourceCollider(Collider collider) => collider == null || collider.transform.IsChildOf(transform) || (pet != null && pet.owner != null && collider.transform.IsChildOf(pet.owner));
        bool Valid(DummyEnemy enemy) {
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.IsAlive || enemy.CaptureLocked || enemy.transform.IsChildOf(transform)) return false;
            var actor = enemy.GetComponent<CreatureCombatant>();
            return actor == null || actor.IsEnemy;
        }
        bool InReach(DummyEnemy enemy, PetSkillCombatProfile profile, Vector3 from) => Valid(enemy) && (enemy.TargetPosition - from).sqrMagnitude <= profile.range * profile.range && ClearLine(from, enemy.TargetPosition, enemy);
        static bool InArc(DummyEnemy enemy, Vector3 from, Vector3 direction, float halfAngle) {
            Vector3 delta = enemy.TargetPosition - from; delta.y = 0; direction.y = 0;
            return delta.sqrMagnitude < .001f || Vector3.Dot(delta.normalized, direction.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        }
        DummyEnemy Nearest(PetSkillCombatProfile profile, Vector3 from, Vector3 direction, bool arc) {
            DummyEnemy best = null; float nearest = profile.range * profile.range;
            foreach (var candidate in DummyEnemy.ActiveEnemies) {
                if (!Valid(candidate)) continue;
                float distance = (candidate.TargetPosition - from).sqrMagnitude;
                if (distance > nearest || (arc && !InArc(candidate, from, direction, profile.meleeHalfAngle)) || !ClearLine(from, candidate.TargetPosition, candidate)) continue;
                best = candidate; nearest = distance;
            }
            return best;
        }
        bool ResolveInitialOverlap(Shot shot) {
            int count = Physics.OverlapSphereNonAlloc(shot.position, shot.profile.projectileRadius, overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return true;
            DummyEnemy victim = null;
            for (int i = 0; i < count; i++) {
                if (SourceCollider(overlaps[i])) continue;
                var enemy = overlaps[i].GetComponentInParent<DummyEnemy>();
                if (!Valid(enemy)) return true; // Solid world or protected actor blocks the shot.
                victim = enemy;
            }
            if (victim == null) return false;
            Hit(shot, victim, shot.position, -shot.direction); return true;
        }
        void Hit(Shot shot, DummyEnemy victim, Vector3 point, Vector3 normal) {
            if (!Valid(victim) || !presentation.CanImpact(shot.token)) { LastRejection = "Mục tiêu hoặc token va chạm không còn hợp lệ."; return; }
            var hit = new CombatHit(gameObject, shot.id, shot.token, shot.damage.RawDamage, shot.profile.damageKind, shot.damage.Critical, false, point);
            var result = victim.ApplyCombatHit(hit);
            if (result.HpLost > 0) {
                presentation.NotifyImpact(shot.token, point, normal); HitCount++;
                if (!result.Killed && shot.bleed > 0) CombatBleed.Apply(victim, hit, shot.bleed);
            }
        }
        void Remove(int index) { if (shots[index].visual != null) CombatEffectPool.Return(shots[index].visual); presentation?.CompleteMiss(shots[index].token); shots.RemoveAt(index); }
        public void Cancel() { for (int i = shots.Count - 1; i >= 0; i--) Remove(i); preparedProfile = null; preparedTarget = null; preparedId = null; preparedRetreatDistance = 0; retreatRemaining = 0; }
    }
}
