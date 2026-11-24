using System.Collections.Generic;
using Capstone.Game.CreatureSystem;
using Capstone.Game.HudSystem;
using UnityEngine;

namespace Capstone.Game.Skills {
    public sealed partial class PetSkillCombatExecutor {
        sealed class CastEffects {
            public readonly HashSet<int> Rolled = new();
            public bool PaymentFailed;
            public bool StunRolled, WingBleedRolled, Pushed;
        }

        sealed class Pulse {
            public Shot Shot;
            public Vector3 Point, Origin, Direction, PreviousPoint;
            public Dictionary<GameObject, (int Count, float NextHit)> TravelHits;
            public float Next, Interval;
            public int Index;
            public bool Impact;
            public GameObject Visual;
        }

        readonly List<Pulse> pulses = new(4);
        readonly CreatureCombatTarget[] pulseTargets = new CreatureCombatTarget[4];
        readonly HashSet<CreatureCombatant> rainRecipients = new();
        readonly List<CreatureCombatant> leavingRain = new();
        Vector3 rainPosition;
        float rainUntil, rainNext, rainRadius, rainMagnitude;
        static int AuthoredHitCount(PetSkillCombatProfile profile) =>
            Random.Range(Mathf.Max(1, profile.minimumHits), Mathf.Clamp(profile.maximumHits, Mathf.Max(1, profile.minimumHits), 10) + 1);

        internal bool ChannelActive(int token) {
            foreach (var pulse in pulses)
                if (pulse.Shot.token == token && (pulse.Shot.profile.pattern == PetSkillPattern.Cone || pulse.Shot.profile.pattern == PetSkillPattern.Beam)) return true;
            return false;
        }

        internal Vector3 ChannelDirection(int token, Vector3 origin)
        {
            foreach (var pulse in pulses)
                if (pulse.Shot.token == token) return pulse.Direction;
            if (LastCombatTarget.Alive)
            {
                Vector3 direction = LastCombatTarget.Center - origin;
                if (direction.sqrMagnitude > .001f) return direction.normalized;
            }
            return transform.forward;
        }

        void StartPulses(Shot shot, Vector3 point, Vector3 origin, bool impact) {
            var visualPoint = VisualImpactPoint(shot, point);
            if (!presentation.NotifyImpact(shot.token, shot.emission, visualPoint, Vector3.up)) return;
            if (pulses.Count >= 8) {
                ReturnPulse(pulses[0]);
                pulses.RemoveAt(0);
            }
            var pulse = new Pulse { Shot = shot, Point = point, Origin = origin, Direction = shot.direction,
                Next = Time.time, Impact = impact, Interval = shot.profile.effectSeconds / Mathf.Max(1, shot.emissionCount - 1) };
            if (shot.profile.pattern == PetSkillPattern.Cone || shot.profile.pattern == PetSkillPattern.Beam)
                pulse.Direction = ChannelDirection(shot.token, origin);
            if (shot.profile.pattern == PetSkillPattern.TravelingArea) {
                // Keep the travel plane level even when the caster looks up/down.
                var direction = Vector3.ProjectOnPlane(shot.direction, Vector3.up);
                pulse.Direction = direction.sqrMagnitude > .001f ? direction.normalized : castFacing;
                pulse.Point = pulse.Origin = pulse.PreviousPoint = combat.Center;
                pulse.TravelHits = new Dictionary<GameObject, (int, float)>();
                pulse.Visual = CombatEffectPool.Rent(shot.profile.projectileVisual, pulse.Point, Quaternion.LookRotation(pulse.Direction));
                if (pulse.Visual != null) pulse.Visual.transform.localScale *= shot.profile.visualScale * presentation.VisualScale;
            }
            pulses.Add(pulse);
        }

        void TickPulses() {
            for (int i = pulses.Count - 1; i >= 0; i--) {
                if (i >= pulses.Count) continue;
                var pulse = pulses[i];
                var shot = pulse.Shot;
                if (pulse.TravelHits != null) {
                    float distance = TravelDistance(pulse);
                    var next = pulse.Point + pulse.Direction * distance;
                    if (distance <= .0001f || !ClearLine(pulse.Point, next)) {
                        ReturnPulse(pulse);
                        pulses.RemoveAt(i);
                        continue;
                    }
                    pulse.PreviousPoint = pulse.Point;
                    pulse.Point = next;
                    if (pulse.Visual != null) pulse.Visual.transform.position = pulse.Point;
                    TickPulse(pulse);
                    continue;
                }
                if (Time.time < pulse.Next) continue;
                TickPulse(pulse);
                if (i >= pulses.Count || pulses[i] != pulse) continue;
                pulse.Index++;
                if (pulse.Index >= shot.emissionCount) {
                    ReturnPulse(pulse);
                    pulses.RemoveAt(i);
                } else pulse.Next = Time.time + pulse.Interval;
            }
        }

        static float TravelDistance(Pulse pulse) =>
            Mathf.Min(pulse.Shot.profile.projectileSpeed * Time.deltaTime,
                Mathf.Max(0f, pulse.Shot.profile.TravelRange - Vector3.Distance(pulse.Origin, pulse.Point)));

        void TickPulse(Pulse pulse) {
            var shot = pulse.Shot;
            var pattern = shot.profile.pattern;
            bool usesFacing = pattern == PetSkillPattern.Cone || pattern == PetSkillPattern.Beam;
            var origin = usesFacing ? presentation.SocketPosition(shot.profile.releaseSocket) : combat.Center;
            if (usesFacing && shot.target.Alive) {
                Vector3 delta = shot.target.Center - origin;
                if (delta.sqrMagnitude > .001f)
                    pulse.Direction = Vector3.RotateTowards(pulse.Direction.normalized, delta.normalized,
                        100f * Mathf.Deg2Rad * Mathf.Max(.02f, pulse.Interval), 0);
            }
            int count = 0;
            if (PulseCanHit(pulse, shot.target, origin)) pulseTargets[count++] = shot.target;
            if (shot.profile.splashFraction > 0 || count == 0) {
                foreach (var actor in CreatureCombatant.Active) {
                    if (actor == null || actor.gameObject == shot.target.Object) continue;
                    var candidate = new CreatureCombatTarget(actor);
                    if (!PulseCanHit(pulse, candidate, origin)) continue;
                    pulseTargets[count++] = candidate;
                    if (count == pulseTargets.Length || shot.profile.splashFraction <= 0f) break;
                }
                if (!context.IsEnemy && count < pulseTargets.Length && (shot.profile.splashFraction > 0 || count == 0))
                {
                    foreach (var enemy in DummyEnemy.ActiveEnemies)
                    {
                        var candidate = new CreatureCombatTarget(enemy);
                        if (candidate.Combatant != null || candidate.Object == shot.target.Object || !PulseCanHit(pulse, candidate, origin)) continue;
                        pulseTargets[count++] = candidate;
                        if (count == pulseTargets.Length || shot.profile.splashFraction <= 0f) break;
                    }
                }
            }
            for (int i = 0; i < count; i++) {
                var victim = pulseTargets[i];
                if (pulse.TravelHits != null)
                {
                    pulse.TravelHits.TryGetValue(victim.Object, out var contact);
                    pulse.TravelHits[victim.Object] = (contact.Count + 1, Time.time + Mathf.Max(.05f, pulse.Interval));
                }
                if (shot.profile.displacement < 0)
                    victim.Combatant?.Displace(pulse.Point - victim.Center, -shot.profile.displacement / shot.emissionCount);
                ApplyAuthoredHit(shot, victim, victim.ClosestPoint(pulse.Point), i == 0 ? 1f : shot.profile.splashFraction);
            }
        }

        bool PulseCanHit(Pulse pulse, CreatureCombatTarget victim, Vector3 origin) {
            var profile = pulse.Shot.profile;
            if (!Valid(victim)) return false;
            if (profile.pattern == PetSkillPattern.Cone || profile.pattern == PetSkillPattern.Beam) {
                Vector3 delta = victim.Center - origin;
                if ((victim.ClosestPoint(origin) - origin).sqrMagnitude > profile.CastRange * profile.CastRange ||
                    !ClearLine(origin, victim.Center)) return false;
                if (profile.pattern == PetSkillPattern.Cone) return InArc(victim, origin, pulse.Direction, profile.meleeHalfAngle);
                float depth = Mathf.Clamp(Vector3.Dot(delta, pulse.Direction), 0f, profile.CastRange);
                Vector3 beamPoint = origin + pulse.Direction * depth;
                return (victim.ClosestPoint(beamPoint) - beamPoint).sqrMagnitude <= profile.targetedImpactRadius * profile.targetedImpactRadius;
            }
            float radius = profile.targetedImpactRadius;
            if (pulse.TravelHits != null)
            {
                if (pulse.TravelHits.TryGetValue(victim.Object, out var contact) &&
                    (contact.Count >= pulse.Shot.emissionCount || Time.time < contact.NextHit)) return false;
                float step = Vector3.Distance(pulse.PreviousPoint, pulse.Point);
                float depth = Mathf.Clamp(Vector3.Dot(victim.Center - pulse.PreviousPoint, pulse.Direction), 0f, step);
                Vector3 contactPoint = pulse.PreviousPoint + pulse.Direction * depth;
                return (victim.ClosestPoint(contactPoint) - contactPoint).sqrMagnitude <= radius * radius &&
                    ClearLine(contactPoint, victim.Center);
            }
            return (victim.ClosestPoint(pulse.Point) - pulse.Point).sqrMagnitude <= radius * radius &&
                (pulse.Impact || InReach(victim, profile, origin)) && ClearLine(pulse.Point, victim.Center);
        }

        void ApplySelfSkill(Shot shot) {
            var self = new CreatureCombatTarget(combat);
            if (shot.profile.fireContact) combat.Status?.EnableFireContact();
            foreach (var cue in shot.profile.statusCues) {
                if (cue.kind == CombatAilment.Rain) {
                    ClearRain();
                    rainPosition = combat.Center;
                    rainUntil = Time.time + cue.duration;
                    rainRadius = shot.profile.range;
                    rainMagnitude = cue.magnitude;
                    rainNext = 0;
                    TickRain();
                } else if (Random.value <= cue.chance) {
                    ApplyCue(self, shot, cue, 0);
                }
            }
        }

        bool WithinRain(CreatureCombatant ally) => ally != null && ally.CanBeAttacked &&
            ally.IsEnemy == combat.IsEnemy && ally.QualifiesForRain &&
            (ally.Center - rainPosition).sqrMagnitude <= rainRadius * rainRadius;

        void TickRain() {
            if (rainUntil <= 0 || Time.time < rainNext) return;
            if (Time.time >= rainUntil) { ClearRain(); return; }
            rainNext = Time.time + .25f;
            leavingRain.Clear();
            foreach (var ally in rainRecipients) if (!WithinRain(ally)) leavingRain.Add(ally);
            foreach (var ally in leavingRain) {
                if (ally != null) ally.Status?.RemoveSourceEffect(CombatAilment.Rain, gameObject);
                rainRecipients.Remove(ally);
            }
            foreach (var ally in CreatureCombatant.Active) {
                if (!WithinRain(ally) || !rainRecipients.Add(ally)) continue;
                ally.Status?.ApplyTimed(CombatAilment.Rain, rainMagnitude, rainUntil - Time.time, gameObject, "rain.water");
            }
        }

        void ClearRain() {
            foreach (var ally in rainRecipients)
                if (ally != null) ally.Status?.RemoveSourceEffect(CombatAilment.Rain, gameObject);
            rainRecipients.Clear();
            leavingRain.Clear();
            rainUntil = 0;
        }

        void ApplyAuthoredHit(Shot shot, CreatureCombatTarget victim, Vector3 point, float fraction) {
            if (!Valid(victim)) return;
            var hit = NewHit(shot, point, shot.damage.RawDamage * fraction);
            var result = shot.profile.statusOnly ? default : victim.Apply(hit);
            if (result.HpLost > 0) {
                HitCount++;
                if (!result.Killed && victim.Alive && !context.IsEnemy && shot.token > lastPursuedCast
                    && pet != null && pet.QueueSkillPursuit(victim.Enemy, shot.pursuitRevision)) lastPursuedCast = shot.token;
            }
            if (!victim.Alive || (!shot.profile.statusOnly && result.HpLost <= 0 &&
                (result.PreventedDamage <= 0 || victim.Combatant?.Status?.Has(CombatAilment.Invulnerable) == true))) return;
            if (!shot.effects.Rolled.Add(victim.Object.GetInstanceID())) return;
            foreach (var cue in shot.profile.statusCues) {
                if (cue.kind == CombatAilment.Stun) {
                    if (shot.effects.StunRolled) continue;
                    shot.effects.StunRolled = true;
                }
                if (cue.kind == CombatAilment.Bleed && shot.id == "wing strike") {
                    if (shot.effects.WingBleedRolled) continue;
                    shot.effects.WingBleedRolled = true;
                }
                if (Random.value > cue.chance) continue;
                ApplyCue(cue.self ? new CreatureCombatTarget(combat) : victim, shot, cue,
                    Mathf.Max(result.HpLost, result.PreventedDamage) * shot.emissionCount);
            }
            if (shot.profile.interruptsCast && Random.value < shot.profile.interruptChance)
                victim.Object.GetComponent<PetCombatActionDriver>()?.InterruptWindup();
            if (shot.profile.displacement > 0 && !shot.effects.Pushed) {
                shot.effects.Pushed = true;
                victim.Combatant?.Displace(victim.Center - combat.Center, shot.profile.displacement);
            }
        }

        CombatHit NewHit(Shot shot, Vector3 point, float raw) =>
            new CombatHit(gameObject, shot.id, shot.token, raw, shot.profile.damageKind, shot.damage.Critical, false,
                point, shot.emission, shot.elements, shot.emissionCount);

        void ApplyCue(CreatureCombatTarget victim, Shot shot, PetSkillStatusCue cue, float damage) {
            if (!victim.Alive) return;
            var recipient = victim.Combatant;
            if (cue.kind == CombatAilment.Rain && (recipient == null || !recipient.QualifiesForRain)) return;
            var state = CombatBleed.For(victim.Object);
            float hp = recipient != null ? recipient.MaximumHealth : victim.Enemy != null ? victim.Enemy.maxHealth : 0;
            float value = cue.valueMode switch {
                PetSkillStatusValue.DamageFraction => damage * cue.magnitude,
                PetSkillStatusValue.MagicAttackFraction => combat.MagicAttack * cue.magnitude,
                PetSkillStatusValue.PoisonCap => Mathf.Min(combat.MagicAttack * cue.magnitude, hp * (victim.Enemy?.IsBoss == true ? .004f : .008f)),
                PetSkillStatusValue.MaxHealthFraction => hp * cue.magnitude,
                _ => cue.magnitude
            };
            if (cue.kind == CombatAilment.Bleed || cue.kind == CombatAilment.Poison || cue.kind == CombatAilment.Burn) {
                if (value > 0) state.Refresh(NewHit(shot, victim.Center, value), value, cue.kind, Mathf.RoundToInt(cue.duration), true);
            } else state.ApplyTimed(cue.kind, value, cue.duration, gameObject, shot.id);
        }

        static void ReturnPulse(Pulse pulse) {
            if (pulse.Visual != null) CombatEffectPool.Return(pulse.Visual);
        }

        void ClearPulses() {
            foreach (var pulse in pulses) ReturnPulse(pulse);
            pulses.Clear();
        }
    }
}
