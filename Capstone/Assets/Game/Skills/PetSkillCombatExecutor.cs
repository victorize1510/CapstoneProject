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
    public sealed partial class PetSkillCombatExecutor : MonoBehaviour
    {
        sealed class Shot
        {
            public int token, emission, emissionCount, pursuitRevision;
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
            public CastEffects effects;
        }

        PetController pet;
        int lastPursuedCast;
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
        readonly RaycastHit[] obstacleHits = new RaycastHit[32];
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
            if (!profile.autoAcquireTarget) return default;
            var nearby = Nearest(profile, combat.Center, transform.forward, false);
            return nearby.Exists || !profile.RequiresTarget ? nearby :
                Nearest(profile, combat.Center, transform.forward, false, 2.5f);
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
            if (!context.CanAct || !combat.MayUseSkill(profile))
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
                if (!InReach(target, profile, combat.Center) && (profile.RequiresTarget || target.Exists))
                {
                    LastRejection = "Mục tiêu đã ra ngoài tầm hoặc bị che khuất.";
                    return false;
                }
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

            if (profile.RequiresTarget && !target.Alive)
            {
                LastRejection = "Cần mục tiêu trong tầm kỹ năng.";
                return false;
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
            trackCastTarget = preparedProfile.TracksUntilRelease;
            Vector3 impactPoint = preparedTarget.Exists ? preparedTarget.Center : combat.Center + castFacing * preparedProfile.CastRange;
            Vector3 origin = presentation.SocketPosition(preparedProfile.releaseSocket);
            Vector3 launch = preparedTarget.Exists ? impactPoint - origin : castFacing;
            if (launch.sqrMagnitude < .001f) launch = castFacing;
            float cost = stats.GetSkillMana(id);
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
            int count = preparedProfile.authoredStatusRules ? AuthoredHitCount(preparedProfile)
                : preparedProfile.weightedWindVolley && preparedProfile.delivery == PetSkillDelivery.Projectile ? PveBalanceRules.WindVolleyCount(countRoll) : 1;
            var rolled = preparedProfile.delivery == PetSkillDelivery.SelfBuff || preparedProfile.statusOnly ? default
                : combat.RollOffense(preparedProfile.damageKind, preparedProfile.power * (1 + (combat.Status?.Magnitude(CombatAilment.Rain) ?? 0)));
            LastVolleyCount = count;
            LastCastRawBudget = rolled.RawDamage;
            // Chup he o commit, khong doc lai nguon khi dan da bay hoac doi form.
            var elements = combat.SnapshotElements(stats.GetSkillElement(id));
            int pursuitRevision = pet != null ? pet.SkillPursuitRevision : 0;
            bool pulses = preparedProfile.pattern != PetSkillPattern.Default && preparedProfile.pattern != PetSkillPattern.ImpactArea;
            bool impactArea = preparedProfile.pattern == PetSkillPattern.ImpactArea;
            int emissions = pulses || impactArea ? 1 : count;
            var effects = preparedProfile.authoredStatusRules ? new CastEffects() : null;
            presentation.ConfigureEmissions(token, emissions);
            for (int emission = 0; emission < emissions; emission++)
            {
                if (shots.Count >= 24)
                {
                    Remove(0);
                }

                shots.Add(new Shot{token = token, emission = emission, emissionCount = count, pursuitRevision = pursuitRevision, id = id, target = preparedTarget, profile = preparedProfile, elements = elements, direction = launch.normalized, impactPoint = impactPoint, aimedAtTarget = preparedTarget.Exists, manaCost = cost, damage = new CombatOffenseRoll(rolled.RawDamage / count, rolled.Critical), bleed = preparedProfile.bleedPower > 0 ? combat.NonCriticalOffense(CreatureDamageKind.Physical, preparedProfile.bleedPower) : 0, expires = Time.time + 10, effects = effects});
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
                    if (shot.profile.SpawnsAtTarget && !InReach(shot.target, shot.profile, combat.Center))
                    {
                        RejectUnreleasedShot(shot, "Mục tiêu đã ra ngoài tầm hoặc bị che khuất.");
                        return;
                    }
                    if (shot.profile.RequiresTarget && !shot.target.Alive)
                    {
                        RejectUnreleasedShot(shot, "Mục tiêu không còn hiệu lực.");
                        return;
                    }
                    if (emission == 0 && !stats.SpendMana(shot.manaCost)) {
                        RejectUnreleasedShot(shot, "Không đủ mana.");
                        return;
                    }
                    if (emission == 0 && GetComponent<PetCombatActionDriver>() is { IsSkillBusy: true } driver && driver.CastToken == token)
                        stats.StartReleasedSkillCooldown(shot.id);
                    if (shot.effects?.PaymentFailed == true) return;
                    if (shot.profile.delivery == PetSkillDelivery.SelfBuff)
                    {
                        if (presentation.NotifyImpact(token, combat.Center, Vector3.up))
                        {
                            if (shot.profile.authoredStatusRules) ApplySelfSkill(shot);
                            else combat.ApplyOffenseBuff(shot.profile.offenseBuffPercent, shot.profile.buffDuration, shot.profile.buffVisual);
                            BuffAppliedCount++;
                        }

                        shot.expires = 0;
                        return;
                    }

                    Vector3 start = presentation.SocketPosition(shot.profile.releaseSocket);
                    if (shot.profile.pattern != PetSkillPattern.Default && shot.profile.pattern != PetSkillPattern.ImpactArea) {
                        StartPulses(shot, CurrentImpactPoint(shot), start, false);
                        shot.expires = 0;
                        return;
                    }
                    if (shot.profile.delivery == PetSkillDelivery.Targeted) {
                        var point = CurrentImpactPoint(shot);
                        float radius = Mathf.Max(.1f, shot.profile.targetedImpactRadius);
                        if (InReach(shot.target, shot.profile, combat.Center)
                            && (shot.target.ClosestPoint(point) - point).sqrMagnitude <= radius * radius)
                            Hit(shot, shot.target, point, Vector3.up);
                        else {
                            presentation.NotifyImpact(token, emission, VisualImpactPoint(shot, point), Vector3.up);
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

                    if (shot.aimedAtTarget)
                    {
                        Vector3 aim = shot.target.Alive ? shot.target.Center : shot.impactPoint;
                        if ((aim - start).sqrMagnitude > .001f) shot.direction = (aim - start).normalized;
                    }

                    shot.position = start;
                    shot.remaining = shot.profile.TravelRange;
                    float flightSeconds = shot.remaining / Mathf.Max(.1f, shot.profile.projectileSpeed);
                    shot.expires = Time.time + Mathf.Max(shot.profile.lifetime, flightSeconds + .1f);
                    // SphereCast khong bao collider da nam trong mieng nong khi phat dan.
                    if (ResolveInitialOverlap(shot))
                    {
                        shot.expires = 0;
                        return;
                    }

                    if (PetSkillPresentationPlayer.IsMouthSocket(shot.profile.releaseSocket) &&
                        AdvanceProjectile(shot, presentation.MouthClearance))
                    {
                        shot.expires = 0;
                        return;
                    }

                    if (shot.profile.projectileVisual != null)
                    {
                        shot.visual = CombatEffectPool.Rent(shot.profile.projectileVisual, shot.position, Quaternion.LookRotation(shot.direction));
                        if (shot.visual != null)
                        {
                            shot.visual.transform.localScale = Vector3.one * shot.profile.visualScale * presentation.VisualScale;
                        }
                    }

                    return;
                }
            }
        }

        void RejectUnreleasedShot(Shot shot, string reason)
        {
            LastRejection = reason;
            GetComponent<PetCombatActionDriver>()?.Reject(reason);
            if (shot.effects != null) shot.effects.PaymentFailed = true;
            foreach (var pending in shots)
                if (pending.token == shot.token) { pending.released = true; pending.expires = 0; }
        }

        static Vector3 CurrentImpactPoint(Shot shot) =>
            shot.profile.TracksUntilRelease && shot.target.Alive ? shot.target.Center : shot.impactPoint;

        Vector3 VisualImpactPoint(Shot shot, Vector3 point) =>
            shot.profile.GroundsImpact && shot.target.Exists
                ? FindImpactGround(shot.target) : point;

        void ShowDodgeIfMoved(Shot shot, float radius) {
            if (!shot.profile.TracksUntilRelease && shot.aimedAtTarget && shot.target.Alive
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

            TickPulses();
            TickRain();
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

                if (AdvanceProjectile(shot, shot.profile.projectileSpeed * Time.deltaTime))
                {
                    Remove(i);
                    continue;
                }

                if (shot.remaining <= .001f)
                {
                    if (shot.emissionCount == 1) ShowDodgeIfMoved(shot, Mathf.Max(.75f, shot.profile.projectileRadius));
                    Remove(i);
                }
            }
        }

        bool AdvanceProjectile(Shot shot, float distanceToTravel)
        {
            float step = Mathf.Min(shot.remaining, distanceToTravel);
            if (step <= 0f) return false;
            int count = Physics.SphereCastNonAlloc(shot.position, shot.profile.projectileRadius, shot.direction, hits, step, ~0, QueryTriggerInteraction.Collide);
            if (count == hits.Length) return true;

            float distance = float.PositiveInfinity;
            RaycastHit contact = default;
            bool travelingStatus = shot.profile.authoredStatusRules && shot.profile.statusOnly;
            bool separateTerrain = shot.profile.ObstacleRadius < shot.profile.projectileRadius;
            if (separateTerrain)
            {
                // The flame cloud overlaps enemies broadly; only its core collides with the floor and walls.
                int blockers = Physics.SphereCastNonAlloc(shot.position, shot.profile.ObstacleRadius, shot.direction,
                    obstacleHits, step, ~0, QueryTriggerInteraction.Ignore);
                if (blockers == obstacleHits.Length) return true;
                for (int i = 0; i < blockers; i++)
                {
                    var block = obstacleHits[i];
                    if (SourceCollider(block.collider) || CreatureCombatTarget.From(block.collider).Exists ||
                        block.distance >= distance) continue;
                    distance = block.distance;
                    contact = block;
                }
            }
            else
            {
                for (int h = 0; h < count; h++)
                {
                    var hit = hits[h];
                    if (SourceCollider(hit.collider) || !IsShotObstacle(hit.collider) || hit.distance >= distance) continue;
                    if (travelingStatus && Valid(CreatureCombatTarget.From(hit.collider))) continue;
                    distance = hit.distance;
                    contact = hit;
                }
            }

            if (travelingStatus)
                for (int h = 0; h < count && shot.effects.Rolled.Count < 4; h++)
                {
                    if (hits[h].distance < distance && !SourceCollider(hits[h].collider))
                    {
                        var victim = CreatureCombatTarget.From(hits[h].collider);
                        if (Valid(victim) && (!separateTerrain || ClearLine(shot.position, victim.Center)))
                            ApplyAuthoredHit(shot, victim, hits[h].point, 1f);
                    }
                }

            if (contact.collider != null)
            {
                var hit = contact;
                var victim = CreatureCombatTarget.From(hit.collider);
                if (Valid(victim))
                {
                    Hit(shot, victim, hit.point, hit.normal);
                }
                else
                {
                    if (shot.profile.pattern == PetSkillPattern.ImpactArea)
                        StartPulses(shot, hit.point + hit.normal * .02f, shot.position, true);
                    else
                        presentation.NotifyImpact(shot.token, shot.emission, hit.point, hit.normal);
                }

                return true;
            }

            shot.position += shot.direction * step;
            shot.remaining -= step;
            if (shot.visual != null)
            {
                shot.visual.transform.position = shot.position;
            }
            return false;
        }

        bool ClearLine(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            if (delta.sqrMagnitude < .0001f)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(from, delta.normalized, obstacleHits, delta.magnitude, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstacleHits.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                var collider = obstacleHits[i].collider;
                if (!SourceCollider(collider) && !CreatureCombatTarget.From(collider).Exists)
                {
                    return false;
                }
            }

            return true;
        }

        bool SourceCollider(Collider collider) => collider == null || collider.transform.IsChildOf(transform) || (pet != null && pet.owner != null && collider.transform.IsChildOf(pet.owner));
        // Hurtbox trigger nhan dan; trigger tuong tac va lanh tho khong chan dan.
        bool IsShotObstacle(Collider collider)
        {
            var target = CreatureCombatTarget.From(collider);
            return target.Exists ? Valid(target) : collider != null && !collider.isTrigger;
        }

        bool Valid(CreatureCombatTarget target) => context.CanHit(target);
        internal bool InReach(CreatureCombatTarget target, PetSkillCombatProfile profile) => InReach(target, profile, combat.Center);
        bool InReach(CreatureCombatTarget target, PetSkillCombatProfile profile, Vector3 from) =>
            Valid(target) && (target.ClosestPoint(from) - from).sqrMagnitude <= profile.CastRange * profile.CastRange &&
            ClearLine(from, target.Center);

        Vector3 FindImpactGround(CreatureCombatTarget target)
        {
            var marker = target.Combatant?.HitVisualGround;
            Vector3 feet = marker != null ? marker.position : target.Object.transform.position;
            Vector3 from = feet + Vector3.up * .3f;
            int count = Physics.RaycastNonAlloc(from, Vector3.down, hits, 12f, ~0, QueryTriggerInteraction.Ignore);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.distance >= closest || hit.normal.y < .35f || SourceCollider(hit.collider) ||
                    CreatureCombatTarget.From(hit.collider).Exists) continue;
                closest = hit.distance;
                feet = hit.point;
            }
            return feet + Vector3.up * .03f;
        }
        static bool InArc(CreatureCombatTarget target, Vector3 from, Vector3 direction, float halfAngle)
        {
            Vector3 delta = target.Center - from;
            delta.y = 0;
            direction.y = 0;
            return delta.sqrMagnitude < .001f || Vector3.Dot(delta.normalized, direction.normalized) >= Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        }

        CreatureCombatTarget Nearest(PetSkillCombatProfile profile, Vector3 from, Vector3 direction, bool arc, float approach = 0f)
        {
            CreatureCombatTarget best = default;
            float searchRange = profile.CastRange + approach;
            float nearest = searchRange * searchRange;
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

                float distance = (victim.ClosestPoint(from) - from).sqrMagnitude;
                if (distance > nearest || (arc && !InArc(victim, from, direction, profile.meleeHalfAngle)) || !ClearLine(from, victim.Center))
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
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (SourceCollider(overlaps[i]) || !IsShotObstacle(overlaps[i]))
                {
                    continue;
                }

                var enemy = CreatureCombatTarget.From(overlaps[i]);
                if (!Valid(enemy))
                {
                    float obstacleRadius = shot.profile.ObstacleRadius;
                    if (obstacleRadius < shot.profile.projectileRadius &&
                        (overlaps[i].ClosestPoint(shot.position) - shot.position).sqrMagnitude > obstacleRadius * obstacleRadius) continue;
                    if (shot.profile.pattern == PetSkillPattern.ImpactArea)
                        StartPulses(shot, shot.position, shot.position, true);
                    return true;
                }

                if (shot.profile.authoredStatusRules && shot.profile.statusOnly) {
                    if (shot.effects.Rolled.Count < 4 && ClearLine(shot.position, enemy.Center))
                        ApplyAuthoredHit(shot, enemy, shot.position, 1f);
                    continue;
                }
                float distance = (enemy.Center - shot.position).sqrMagnitude;
                if (distance >= closest) continue;
                victim = enemy;
                closest = distance;
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

            if (shot.profile.authoredStatusRules) {
                if (shot.profile.pattern == PetSkillPattern.ImpactArea) {
                    shot.target = victim;
                    StartPulses(shot, point, shot.position, true);
                }
                else {
                    presentation.NotifyImpact(shot.token, shot.emission, VisualImpactPoint(shot, point), normal);
                    ApplyAuthoredHit(shot, victim, point, 1f);
                }
                return;
            }
            var hit = new CombatHit(gameObject, shot.id, shot.token, shot.damage.RawDamage, shot.profile.damageKind, shot.damage.Critical, false, point, shot.emission, shot.elements, shot.emissionCount);
            var result = victim.Apply(hit);
            if (result.HpLost > 0)
            {
                presentation.NotifyImpact(shot.token, shot.emission, point, normal);
                HitCount++;
                // The first surviving monster actually hit by a cast owns its follow-up.
                if (!result.Killed && victim.Alive && !context.IsEnemy && shot.token > lastPursuedCast
                    && pet != null && pet.QueueSkillPursuit(victim.Enemy, shot.pursuitRevision))
                    lastPursuedCast = shot.token;
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
            ClearPulses();
            ClearRain();
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
