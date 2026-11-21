using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using Capstone.Game.Inventory;
using Capstone.Game.Elements;
using UnityEngine;

namespace Capstone.Game.Skills
{
    /// <summary>Cast chup muc tieu mot lan; moi phat chi trung mot lan va co gioi han thoi gian song.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PetSkillPresentationPlayer), typeof(CreatureCombatant))]
    public sealed class PetSkillCombatExecutor : MonoBehaviour
    {
        sealed class Shot
        {
            public int token, emission, emissionCount;
            public string id;
            public CreatureCombatTarget target;
            public PetSkillCombatProfile profile;
            public Vector3 position, direction;
            public Vector3 impactPoint;
            public bool aimedAtTarget;
            public float manaCost;
            public float expires, remaining;
            public bool released;
            public GameObject visual;
            public CombatOffenseRoll damage;
            public CombatElementSnapshot elements;
            public float bleed;
        }

        PetController pet;
        CreatureCombatContext context;
        CreatureCombatant combat;
        PetSkillPresentationPlayer presentation;
        PetHudRuntimeStats stats;
        CreatureCombatTarget preparedTarget;
        PetSkillCombatProfile preparedProfile;
        string preparedId;
        Vector3 preparedDirection;
        public string LastTargetMode { get; private set; }

        public CreatureCombatTarget LastCombatTarget { get; private set; }

        public DummyEnemy LastCastTarget => LastCombatTarget.Enemy;
        Vector3 retreatDirection;
        float preparedRetreatDistance, retreatRemaining, retreatSpeed, retreatUntil;
        public float PreparedRetreatSeconds => preparedProfile != null && preparedRetreatDistance > 0 ? preparedRetreatDistance / Mathf.Max(.1f, preparedProfile.retreatSpeed) : 0;
        readonly List<Shot> shots = new List<Shot>(8);
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        public int HitCount { get; private set; }

        public int BuffAppliedCount { get; private set; }

        public int ActiveProjectileCount => shots.Count;
        public int LastVolleyCount { get; private set; }

        internal System.Func<float> VolleyRandomSource;
        public float LastCastRawBudget { get; private set; }

        public string LastRejection { get; private set; }

        public bool IsRetreating => retreatRemaining > .001f;
        internal bool SkipNextRetreat;
        CreatureCombatTarget coordinatedTarget;
        bool hasCoordinatedTarget;
        internal void StageTarget(CreatureCombatTarget target) { coordinatedTarget=target;hasCoordinatedTarget=true; }
        internal CreatureCombatTarget ResolveIntentTarget(PetSkillCombatProfile profile) {
            if(profile.delivery==PetSkillDelivery.SelfBuff)return default;
            if(context.IsEnemy)return context.Target;
            var input=pet!=null&&pet.owner!=null?pet.owner.GetComponent<PetCommandInput>():null;
            if(input!=null&&input.cameraLock!=null&&input.cameraLock.TryGetLockedEnemy(out var locked)){
                var pinned=new CreatureCombatTarget(locked);if(Valid(pinned))return pinned;
            }
            var selected=new CreatureCombatTarget(input!=null?input.SelectedEnemy:null);
            if(Valid(selected))return selected;
            var current=new CreatureCombatTarget(pet!=null?pet.CombatTarget:null);
            if(Valid(current))return current;
            return profile.autoAcquireTarget?Nearest(profile,combat.Center,transform.forward,false):default;
        }
        public Vector3 AimDirection => trackCastTarget && LastCombatTarget.Alive ? LastCombatTarget.Center - transform.position : castFacing;
        bool trackCastTarget;
        Vector3 castFacing;
        void OnEnable()
        {
            pet = GetComponent<PetController>();
            combat = GetComponent<CreatureCombatant>();
            stats = GetComponent<PetHudRuntimeStats>();
            presentation = GetComponent<PetSkillPresentationPlayer>();
            context = CreatureCombatContext.Get(gameObject);
            presentation.SkillCastStarted += CastStarted;
            presentation.SkillReleased += Release;
            presentation.SkillProjectileReleased += ReleaseEmission;
        }

        void OnDisable()
        {
            if (presentation != null)
            {
                presentation.SkillCastStarted -= CastStarted;
                presentation.SkillReleased -= Release;
                presentation.SkillProjectileReleased -= ReleaseEmission;
            }

            Cancel();
        }

        public bool Prepare(string id, PetSkillCombatProfile profile)
        {
            bool staged=hasCoordinatedTarget, skipRetreat=SkipNextRetreat;
            var intended=coordinatedTarget;hasCoordinatedTarget=false;SkipNextRetreat=false;
            preparedProfile = null;
            preparedTarget = default;
            preparedId = null;
            LastRejection = "";
            if (!context.CanAct)
            {
                return false;
            }

            preparedRetreatDistance = 0;
            preparedDirection = transform.forward;
            LastTargetMode = "Forward";
            if (profile == null)
            {
                return true;
            }

            if (profile.delivery == PetSkillDelivery.SelfBuff)
            {
                preparedProfile = profile;
                preparedId = id;
                LastTargetMode = "Self";
                return true;
            }

            CreatureCombatTarget target = default;
            var input = pet != null && pet.owner != null ? pet.owner.GetComponent<PetCommandInput>() : null;
            if(staged) {
                target=intended;LastTargetMode="Coordinated";
                if(!InReach(target,profile,combat.Center))return false;
            }
            else if (context.IsEnemy)
            {
                target = context.Target;
                LastTargetMode = "Retaliation";
                if (!InReach(target, profile, combat.Center))
                {
                    return false;
                }
            }
            else
            {
                if (input != null && InReach(new CreatureCombatTarget(input.SelectedEnemy), profile, combat.Center))
                {
                    target = new CreatureCombatTarget(input.SelectedEnemy);
                    LastTargetMode = "Selected";
                }

                if (!target.Exists && input != null && input.cameraLock != null && input.cameraLock.TryGetLockedEnemy(out var pinned) && InReach(new CreatureCombatTarget(pinned), profile, combat.Center))
                {
                    target = new CreatureCombatTarget(pinned);
                    LastTargetMode = "Locked";
                }

                if (!target.Exists && pet != null && InReach(new CreatureCombatTarget(pet.CombatTarget), profile, combat.Center))
                {
                    target = new CreatureCombatTarget(pet.CombatTarget);
                    LastTargetMode = "Combat";
                }

                if (!target.Exists && profile.autoAcquireTarget)
                {
                    target = Nearest(profile, combat.Center, transform.forward, false);
                    if (target.Exists)
                    {
                        LastTargetMode = "Nearest";
                    }
                }
            }

            if (!skipRetreat && target.Exists && profile.delivery != PetSkillDelivery.Melee && profile.retreatDistance > 0)
            {
                Vector3 separation = combat.Center - target.Center;
                separation.y = 0;
                preparedRetreatDistance = Mathf.Min(profile.retreatDistance, Mathf.Max(0, profile.preferredDistance - separation.magnitude));
            }

            SkipNextRetreat=false;
            preparedTarget = target;
            preparedProfile = profile;
            preparedId = id;
            return true;
        }

        void CastStarted(int token, string id)
        {
            if (preparedProfile == null || preparedId != id)
            {
                return;
            }

            Vector3 facing = preparedTarget.Exists ? preparedTarget.Center - transform.position : preparedDirection;
            facing.y = 0;
            castFacing = facing.sqrMagnitude > .001f ? facing.normalized : transform.forward;
            LastCombatTarget = preparedTarget;
            trackCastTarget = preparedProfile.trackTargetUntilRelease;
            Vector3 impactPoint = preparedTarget.Exists ? preparedTarget.Center : combat.Center + castFacing * preparedProfile.range;
            Vector3 origin = presentation.SocketPosition(preparedProfile.releaseSocket);
            Vector3 launch = preparedTarget.Exists ? impactPoint - origin : castFacing;
            if (launch.sqrMagnitude < .001f) launch = castFacing;
            float cost = stats.SpeciesSkillSet != null && stats.SpeciesSkillSet.TryGet(id, out var entry) ? entry.skill.ManaCost : 0f;
            retreatDirection = -castFacing;
            retreatDirection.y = 0;
            retreatDirection.Normalize();
            retreatRemaining = preparedRetreatDistance;
            retreatSpeed = Mathf.Max(.1f, preparedProfile.retreatSpeed);
            retreatUntil = Time.time + PreparedRetreatSeconds;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                if (!shots[i].released)
                {
                    Remove(i);
                }
            }

            float countRoll = preparedProfile.weightedWindVolley ? Mathf.Min(VolleyRandomSource?.Invoke() ?? Random.value, .999999f) : 0;
            int count = preparedProfile.weightedWindVolley && preparedProfile.delivery == PetSkillDelivery.Projectile ? PveBalanceRules.WindVolleyCount(countRoll) : 1;
            var rolled = preparedProfile.delivery == PetSkillDelivery.SelfBuff ? default : combat.RollOffense(preparedProfile.damageKind, preparedProfile.power);
            LastVolleyCount = count;
            LastCastRawBudget = rolled.RawDamage;
            // Chup he o commit, khong doc lai nguon khi dan da bay hoac doi form.
            var elements = combat.SnapshotElements(stats.GetSkillElement(id));
            presentation.ConfigureEmissions(token, count);
            for (int emission = 0; emission < count; emission++)
            {
                if (shots.Count >= 24)
                {
                    Remove(0);
                }

                shots.Add(new Shot{token = token, emission = emission, emissionCount = count, id = id, target = preparedTarget, profile = preparedProfile, elements = elements, direction = launch.normalized, impactPoint = impactPoint, aimedAtTarget = preparedTarget.Exists, manaCost = cost, damage = new CombatOffenseRoll(rolled.RawDamage / count, rolled.Critical), bleed = preparedProfile.bleedPower > 0 ? combat.NonCriticalOffense(CreatureDamageKind.Physical, preparedProfile.bleedPower) : 0, expires = Time.time + 10});
            }

            preparedProfile = null;
            preparedTarget = default;
            preparedRetreatDistance = 0;
        }

        void Release(int token, string id) => ReleaseEmission(token, id, 0);
        void ReleaseEmission(int token, string id, int emission)
        {
            foreach (var shot in shots)
            {
                if (shot.token == token && shot.emission == emission && !shot.released)
                {
                    shot.released = true;
                    if (emission == 0 && !stats.SpendMana(shot.manaCost)) {
                        foreach (var pending in shots) if (pending.token == token) { pending.released = true; pending.expires = 0; }
                        return;
                    }
                    if (shot.profile.delivery == PetSkillDelivery.SelfBuff)
                    {
                        if (presentation.NotifyImpact(token, combat.Center, Vector3.up))
                        {
                            combat.ApplyOffenseBuff(shot.profile.offenseBuffPercent, shot.profile.buffDuration, shot.profile.buffVisual);
                            BuffAppliedCount++;
                        }

                        shot.expires = 0;
                        return;
                    }

                    Vector3 start = presentation.SocketPosition(shot.profile.releaseSocket);
                    if (shot.profile.delivery == PetSkillDelivery.Targeted) {
                        var point = shot.profile.trackTargetUntilRelease && shot.target.Alive ? shot.target.Center : shot.impactPoint;
                        float radius = Mathf.Max(.1f, shot.profile.targetedImpactRadius);
                        if (InReach(shot.target, shot.profile, combat.Center)
                            && (shot.target.ClosestPoint(point) - point).sqrMagnitude <= radius * radius)
                            Hit(shot, shot.target, point, Vector3.up);
                        else {
                            presentation.NotifyImpact(token, emission, point, Vector3.up);
                            ShowDodgeIfMoved(shot, radius);
                        }
                        shot.expires = 0;
                        return;
                    }
                    if (shot.profile.delivery != PetSkillDelivery.Projectile)
                    {
                        Vector3 facing = AimDirection;
                        bool aimed = InReach(shot.target, shot.profile, combat.Center) && InArc(shot.target, combat.Center, facing, shot.profile.meleeHalfAngle);
                        var victim = aimed ? shot.target : Nearest(shot.profile, combat.Center, facing, true);
                        if (victim.Exists)
                        {
                            Hit(shot, victim, victim.ClosestPoint(start), -shot.direction);
                        }
                        else
                        {
                            LastRejection = "Không có mục tiêu trong vùng đánh: " + LastTargetMode;
                            ShowDodgeIfMoved(shot, .75f);
                        }

                        shot.expires = 0;
                        return;
                    }

                    if (shot.profile.trackTargetUntilRelease && InReach(shot.target, shot.profile, start))
                    {
                        shot.direction = (shot.target.Center - start).normalized;
                    }
                    else if (shot.aimedAtTarget && !shot.profile.trackTargetUntilRelease && (shot.impactPoint - start).sqrMagnitude > .001f)
                    {
                        shot.direction = (shot.impactPoint - start).normalized;
                    }

                    shot.position = start;
                    shot.remaining = shot.profile.range;
                    shot.expires = Time.time + shot.profile.lifetime;
                    // SphereCast khong bao collider da nam trong mieng nong khi phat dan.
                    if (ResolveInitialOverlap(shot))
                    {
                        shot.expires = 0;
                        return;
                    }

                    if (shot.profile.projectileVisual != null)
                    {
                        shot.visual = CombatEffectPool.Rent(shot.profile.projectileVisual, start, Quaternion.LookRotation(shot.direction));
                        if (shot.visual != null)
                        {
                            shot.visual.transform.localScale = Vector3.one * shot.profile.visualScale * presentation.VisualScale;
                        }
                    }

                    return;
                }
            }
        }

        void ShowDodgeIfMoved(Shot shot, float radius) {
            if (!shot.profile.trackTargetUntilRelease && shot.aimedAtTarget && shot.target.Alive
                && (shot.target.Center - shot.impactPoint).sqrMagnitude > radius * radius)
                FloatingCombatText.ShowDodge(shot.target.Center);
        }

        void Update()
        {
            if (Capstone.Game.UISystem.GameplayPause.IsPaused)
            {
                return;
            }

            if (!context.CanAct)
            {
                if (shots.Count > 0)
                {
                    presentation.CancelPresentation();
                }

                Cancel();
                return;
            }

            if (retreatRemaining > 0)
            {
                float step = Mathf.Min(retreatRemaining, retreatSpeed * Time.deltaTime);
                if (Time.time <= retreatUntil + .08f && context.Retreat(retreatDirection, step))
                {
                    retreatRemaining -= step;
                }
                else
                {
                    retreatRemaining = 0;
                }
            }

            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                if (Time.time >= shot.expires)
                {
                    Remove(i);
                    continue;
                }

                if (!shot.released || shot.profile.delivery != PetSkillDelivery.Projectile)
                {
                    continue;
                }

                float step = Mathf.Min(shot.remaining, shot.profile.projectileSpeed * Time.deltaTime);
                int count = Physics.SphereCastNonAlloc(shot.position, shot.profile.projectileRadius, shot.direction, hits, step, ~0, QueryTriggerInteraction.Collide);
                if (count == hits.Length)
                {
                    Remove(i);
                    continue;
                }

                int nearest = -1;
                float distance = float.PositiveInfinity;
                for (int h = 0; h < count; h++)
                {
                    if (!SourceCollider(hits[h].collider) && IsShotObstacle(hits[h].collider) && hits[h].distance < distance)
                    {
                        nearest = h;
                        distance = hits[h].distance;
                    }
                }

                if (nearest >= 0)
                {
                    var hit = hits[nearest];
                    var victim = CreatureCombatTarget.From(hit.collider);
                    if (Valid(victim))
                    {
                        Hit(shot, victim, hit.point, hit.normal);
                    }
                    else
                    {
                        presentation.NotifyImpact(shot.token, shot.emission, hit.point, hit.normal); // Chi hien va cham be mat; khong phat damage hay recoil.
                    }

                    Remove(i);
                    continue;
                }

                shot.position += shot.direction * step;
                shot.remaining -= step;
                if (shot.visual != null)
                {
                    shot.visual.transform.position = shot.position;
                }

                if (shot.remaining <= .001f)
                {
                    if (shot.emissionCount == 1) ShowDodgeIfMoved(shot, Mathf.Max(.75f, shot.profile.projectileRadius));
                    Remove(i);
                }
            }
        }

        bool ClearLine(Vector3 from, Vector3 to, CreatureCombatTarget target)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(from, delta.normalized, hits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                if (!SourceCollider(hits[i].collider) && CreatureCombatTarget.From(hits[i].collider).Object != target.Object)
                {
                    return false;
                }
            }

            return true;
        }

        bool SourceCollider(Collider collider) => collider == null || collider.transform.IsChildOf(transform) || (pet != null && pet.owner != null && collider.transform.IsChildOf(pet.owner));
        // Hurtbox trigger nhan dan; trigger tuong tac va lanh tho khong chan dan.
        static bool IsShotObstacle(Collider collider) => collider != null && (!collider.isTrigger || CreatureCombatTarget.From(collider).Exists);
        bool Valid(CreatureCombatTarget target) => context.CanHit(target);
        bool InReach(CreatureCombatTarget target, PetSkillCombatProfile profile, Vector3 from) => Valid(target) && (target.Center - from).sqrMagnitude <= profile.range * profile.range && ClearLine(from, target.Center, target);
        static bool InArc(CreatureCombatTarget target, Vector3 from, Vector3 direction, float halfAngle)
        {
            Vector3 delta = target.Center - from;
            delta.y = 0;
            direction.y = 0;
            return delta.sqrMagnitude < .001f || Vector3.Dot(delta.normalized, direction.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        }

        CreatureCombatTarget Nearest(PetSkillCombatProfile profile, Vector3 from, Vector3 direction, bool arc)
        {
            CreatureCombatTarget best = default;
            float nearest = profile.range * profile.range;
            if (context.IsEnemy)
            {
                var candidate = context.Target;
                return InReach(candidate, profile, from) && (!arc || InArc(candidate, from, direction, profile.meleeHalfAngle)) ? candidate : default;
            }

            foreach (var candidate in DummyEnemy.ActiveEnemies)
            {
                var victim = new CreatureCombatTarget(candidate);
                if (!Valid(victim))
                {
                    continue;
                }

                float distance = (victim.Center - from).sqrMagnitude;
                if (distance > nearest || (arc && !InArc(victim, from, direction, profile.meleeHalfAngle)) || !ClearLine(from, victim.Center, victim))
                {
                    continue;
                }

                best = victim;
                nearest = distance;
            }

            return best;
        }

        bool ResolveInitialOverlap(Shot shot)
        {
            int count = Physics.OverlapSphereNonAlloc(shot.position, shot.profile.projectileRadius, overlaps, ~0, QueryTriggerInteraction.Collide);
            if (count == overlaps.Length)
            {
                return true;
            }

            CreatureCombatTarget victim = default;
            for (int i = 0; i < count; i++)
            {
                if (SourceCollider(overlaps[i]) || !IsShotObstacle(overlaps[i]))
                {
                    continue;
                }

                var enemy = CreatureCombatTarget.From(overlaps[i]);
                if (!Valid(enemy))
                {
                    return true; // Vat can ran hoac actor duoc bao ve chan vien dan.
                }

                victim = enemy;
            }

            if (!victim.Exists)
            {
                return false;
            }

            Hit(shot, victim, shot.position, -shot.direction);
            return true;
        }

        void Hit(Shot shot, CreatureCombatTarget victim, Vector3 point, Vector3 normal)
        {
            if (!Valid(victim) || !presentation.CanImpact(shot.token, shot.emission))
            {
                LastRejection = "Mục tiêu hoặc token va chạm không còn hợp lệ.";
                return;
            }

            var hit = new CombatHit(gameObject, shot.id, shot.token, shot.damage.RawDamage, shot.profile.damageKind, shot.damage.Critical, false, point, shot.emission, shot.elements, shot.emissionCount);
            var result = victim.Apply(hit);
            if (result.HpLost > 0)
            {
                presentation.NotifyImpact(shot.token, shot.emission, point, normal);
                HitCount++;
                if (!result.Killed && shot.bleed > 0)
                {
                    CombatBleed.ApplyTarget(victim, hit, shot.bleed);
                }
            }
        }

        void Remove(int index)
        {
            if (shots[index].visual != null)
            {
                CombatEffectPool.Return(shots[index].visual);
            }

            presentation?.CompleteMiss(shots[index].token, shots[index].emission);
            shots.RemoveAt(index);
        }

        public void Cancel()
        {
            hasCoordinatedTarget=false;SkipNextRetreat=false;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Remove(i);
            }

            preparedProfile = null;
            preparedTarget = default;
            preparedId = null;
            preparedRetreatDistance = 0;
            retreatRemaining = 0;
        }

        void OnApplicationFocus(bool value)
        {
            if (!value && !Capstone.Game.UISystem.GameplayPause.IsPaused && (context == null || !context.IsEnemy))
            {
                presentation?.CancelPresentation();
                Cancel();
            }
        }

        public void CancelWindup()
        {
            hasCoordinatedTarget=false;SkipNextRetreat=false;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                if (!shots[i].released)
                {
                    Remove(i);
                }
            }

            preparedProfile = null;
            preparedTarget = default;
            preparedId = null;
            preparedRetreatDistance = retreatRemaining = 0;
        }
    }
}
