using System;
using UnityEngine;

/// <summary>Collision changes boom length only. Never chooses another shoulder, heading or world-space fallback.</summary>
public sealed class CameraBoomCollision
{
    readonly RaycastHit[] hits = new RaycastHit[64];
    readonly Collider[] overlaps = new Collider[64];
    readonly Predicate<Collider> ignore;
    public bool QuerySaturated { get; private set; }
    public bool HasClearPosition { get; private set; }

    public CameraBoomCollision(Predicate<Collider> ignoreCollider) { ignore = ignoreCollider; }

    public Vector3 Resolve(Vector3 origin, Vector3 desired, float radius, float padding, int mask)
    {
        QuerySaturated = false;
        HasClearPosition = true;
        Vector3 offset = desired - origin;
        float length = offset.magnitude;
        if (length < .0001f) return origin;
        Vector3 direction = offset / length;
        radius = Mathf.Max(.03f, radius);
        float safe = length;
        bool originClear = IsClear(origin, radius, mask);
        int count = Physics.SphereCastNonAlloc(origin, radius, direction, hits, length, mask, QueryTriggerInteraction.Ignore);
        if (count == hits.Length) { QuerySaturated = true; safe = 0; }
        else for (int i = 0; i < count; i++)
        {
            if (Ignored(hits[i].collider)) continue;
            // Some shapes report a zero-distance contact for an overlapping
            // start sphere, even when the boom points away from the wall.
            // The point-ray/inside checks below resolve that start contact;
            // only retain forward sweep hits with actual travel here.
            if (!originClear && hits[i].distance <= .0001f) continue;
            safe = Mathf.Min(safe, Mathf.Max(0, hits[i].distance - padding));
        }

        // SphereCast omits colliders intersecting its start sphere. A point ray still
        // catches the wall when the pivot itself is outside, e.g. beside a doorway.
        if (!originClear)
        {
            count = Physics.OverlapSphereNonAlloc(origin, .001f, overlaps, mask, QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) { QuerySaturated = true; safe = 0; }
            for (int i = 0; i < count; i++) if (!Ignored(overlaps[i]))
            {
                // No radial solution can unbury a pivot inside solid geometry.
                // Expose this condition instead of teleporting around/through a wall.
                HasClearPosition = false;
                return origin;
            }
            count = Physics.RaycastNonAlloc(origin, direction, hits, length, mask, QueryTriggerInteraction.Ignore);
            if (count == hits.Length) { QuerySaturated = true; safe = 0; }
            else for (int i = 0; i < count; i++) if (!Ignored(hits[i].collider))
                safe = Mathf.Min(safe, Mathf.Max(0, hits[i].distance - radius - padding));
        }

        Vector3 candidate = origin + direction * safe;
        if (IsClear(candidate, radius, mask)) return candidate;
        if (!originClear) { HasClearPosition = false; return origin; }
        // Search only the already swept, near side of the obstacle. The pivot is
        // known clear; this cannot cross to the other side of an initially hit wall.
        float low = 0, high = safe;
        for (int i = 0; i < 12; i++)
        {
            float middle = (low + high) * .5f;
            if (IsClear(origin + direction * middle, radius, mask)) low = middle;
            else high = middle;
        }
        return origin + direction * low;
    }

    bool Ignored(Collider collider) => collider == null || ignore != null && ignore(collider);
    bool IsClear(Vector3 point, float radius, int mask)
    {
        int count = Physics.OverlapSphereNonAlloc(point, radius, overlaps, mask, QueryTriggerInteraction.Ignore);
        if (count == overlaps.Length) { QuerySaturated = true; return false; }
        for (int i = 0; i < count; i++) if (!Ignored(overlaps[i])) return false;
        return true;
    }
}

/// <summary>One state per camera. Anticipate motion; smooth distance inside the current physical clearance.</summary>
public struct CameraBoomRecovery
{
    bool initialized, recovering;
    float distance, velocity, hold;
    Vector3 previousOrigin;
    bool motionInitialized;
    Vector3 motionOrigin, motionOffset, linearVelocity, angularVelocity;

    public void PredictMotion(Vector3 origin, Vector3 offset, float deltaTime, float horizon,
        out Vector3 translation, out Vector3 rotation)
    {
        translation = rotation = Vector3.zero;
        if (!motionInitialized || deltaTime <= 0 || deltaTime > .15f || (origin - motionOrigin).sqrMagnitude > 4)
        {
            motionInitialized = true;
            linearVelocity = angularVelocity = Vector3.zero;
        }
        else
        {
            float blend = 1 - Mathf.Exp(-deltaTime / .045f);
            linearVelocity = Vector3.Lerp(linearVelocity, (origin - motionOrigin) / deltaTime, blend);
            var change = Quaternion.FromToRotation(motionOffset, offset);
            change.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180) angle -= 360;
            Vector3 angular = Mathf.Abs(angle) < .001f ? Vector3.zero : axis * (angle / deltaTime);
            angularVelocity = Vector3.Lerp(angularVelocity, angular, blend);
            translation = Vector3.ClampMagnitude(linearVelocity * horizon, .8f);
            rotation = Vector3.ClampMagnitude(angularVelocity * horizon, 45);
        }
        motionOrigin = origin; motionOffset = offset;
    }

    public float Step(float safe, float requested, Vector3 origin, float deltaTime, float returnTime)
        => Approach(safe, safe, requested, origin, deltaTime, 0, returnTime);

    public float Approach(float safe, float anticipated, float requested, Vector3 origin,
        float deltaTime, float enterTime, float returnTime)
    {
        float goal = Mathf.Min(safe, anticipated);
        bool blocked = goal < requested - .001f;
        if (!initialized || deltaTime < 0 || (origin - previousOrigin).sqrMagnitude > 64)
        {
            initialized = true; distance = goal; velocity = hold = 0; recovering = blocked;
        }
        previousOrigin = origin;
        // This clamp is only the emergency boundary. Ordinary motion starts
        // retracting earlier toward the predicted clearance, using enterTime.
        if (safe < distance) { distance = safe; velocity = 0; }
        recovering |= blocked;
        bool inward = goal < distance - .001f;
        if (inward) hold = .06f;
        float smoothTime = inward ? enterTime : returnTime;
        if (!recovering || smoothTime <= 0) { distance = goal; velocity = 0; recovering = blocked; }
        else if (!inward && hold > 0) hold = Mathf.Max(0, hold - Mathf.Max(0, deltaTime));
        else if (deltaTime > 0)
        {
            // Manual cameras can resume after UI/focus pauses with a large pipeline
            // delta. Recovery must not turn that catch-up into a one-frame zoom-out.
            distance = Mathf.SmoothDamp(distance, goal, ref velocity, smoothTime, Mathf.Infinity, Mathf.Min(deltaTime, .05f));
            if (!blocked && Mathf.Abs(distance - goal) < .002f) { distance = goal; velocity = 0; recovering = false; }
        }
        return Mathf.Clamp(distance, 0, safe);
    }
}
