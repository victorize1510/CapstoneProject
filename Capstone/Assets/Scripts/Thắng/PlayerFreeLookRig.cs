using Unity.Cinemachine;
using UnityEngine;

/// <summary>Feeds existing gameplay state into cameras authored in the scene.</summary>
[DisallowMultipleComponent]
public sealed class PlayerFreeLookRig : MonoBehaviour
{
    public BasicCameraFollow source;
    public CinemachineBrain brain;
    public CinemachineCamera freeCamera;
    public CinemachineOrbitalFollow orbit;
    public CinemachineCamera lockCamera;
    public CinemachineFollow lockFollow;
    public Transform orbitTarget;
    public Transform lockTarget;
    public Transform lookTarget;

    public bool IsReady => source != null && brain != null && freeCamera != null
        && orbit != null && lockCamera != null && lockFollow != null
        && orbitTarget != null && lockTarget != null && lookTarget != null;

    public Vector3 CollisionOrigin { get; private set; }
    public float ZoomDistance { get; private set; }
    bool initialized;
    bool preferencesCached;
    Vector3 orbitDamping, lockDamping;
    Vector2 freeRotationDamping, lockRotationDamping;
    CinemachineRotationComposer freeComposer, lockComposer;
    public void ApplyUserSmoothing(bool enabled) {
        if (!IsReady) return;
        if (!preferencesCached) {
            orbitDamping = orbit.TrackerSettings.PositionDamping; lockDamping = lockFollow.TrackerSettings.PositionDamping;
            freeComposer = freeCamera.GetComponent<CinemachineRotationComposer>();
            lockComposer = lockCamera.GetComponent<CinemachineRotationComposer>();
            if (freeComposer != null) freeRotationDamping = freeComposer.Damping;
            if (lockComposer != null) lockRotationDamping = lockComposer.Damping;
            preferencesCached = true;
        }
        orbit.TrackerSettings.PositionDamping = enabled ? orbitDamping : Vector3.zero;
        lockFollow.TrackerSettings.PositionDamping = enabled ? lockDamping : Vector3.zero;
        if (freeComposer != null) freeComposer.Damping = enabled ? freeRotationDamping : Vector2.zero;
        if (lockComposer != null) lockComposer.Damping = enabled ? lockRotationDamping : Vector2.zero;
    }

    public void Render(Vector3 position, float yaw, float pitch, float distance,
        bool locked, Quaternion lockYaw, Vector3 lockOffset, Vector3 focus)
    {
        if (!IsReady) return;

        // World-space orbit reference keeps player turning from recentering the camera.
        orbitTarget.SetPositionAndRotation(position, Quaternion.identity);
        lockTarget.SetPositionAndRotation(position, lockYaw);
        lookTarget.position = focus;
        CollisionOrigin = position + Vector3.up * source.lookHeight;
        ZoomDistance = distance;

        orbit.HorizontalAxis.Value = orbit.HorizontalAxis.ClampValue(yaw);
        float height = Mathf.InverseLerp(source.minPitch, source.maxPitch, pitch);
        orbit.VerticalAxis.Value = Mathf.Lerp(orbit.VerticalAxis.Range.x, orbit.VerticalAxis.Range.y, height);
        float baseDistance = new Vector2(orbit.Orbits.Center.Radius, orbit.Orbits.Center.Height).magnitude;
        orbit.RadialAxis.Value = orbit.RadialAxis.ClampValue(distance / Mathf.Max(0.1f, baseDistance));
        lockFollow.FollowOffset = lockOffset.normalized * distance;

        freeCamera.Priority = 10;
        lockCamera.Priority = locked ? 20 : 0;
        if (!initialized)
        {
            freeCamera.PreviousStateIsValid = false;
            lockCamera.PreviousStateIsValid = false;
            initialized = true;
        }

        // ManualUpdate runs once after the player and gameplay camera state, including
        // UI/focus gates. No other component writes the render camera's Transform.
        brain.ManualUpdate();
    }
}
