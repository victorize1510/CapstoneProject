using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCameraCollision : CinemachineExtension
{
    public PlayerFreeLookRig rig;
    Vector3 bodyPosition;
    bool bodySolved;
    int bodyFrame = -1;
    CameraBoomRecovery recovery;

    protected override void OnEnable() { base.OnEnable(); recovery = default; bodySolved = false; }
    public override void OnTargetObjectWarped(CinemachineVirtualCameraBase vcam, Transform target, Vector3 delta)
    { recovery = default; bodySolved = false; }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if ((stage != CinemachineCore.Stage.Body && stage != CinemachineCore.Stage.Finalize)
            || rig == null || !rig.IsReady || rig.ZoomDistance <= 0f) return;
        if (bodyFrame != Time.frameCount || deltaTime < 0) { bodySolved = false; bodyFrame = Time.frameCount; }
        if (deltaTime < 0) recovery = default;
        Vector3 position = state.GetCorrectedPosition();
        // Finalize rechecks only a displacement added after Body (e.g. camera impulse).
        // Idle frames must not pay for two identical physics solves per virtual camera.
        if (stage == CinemachineCore.Stage.Finalize && bodySolved && (position - bodyPosition).sqrMagnitude < .000001f) return;
        if (stage == CinemachineCore.Stage.Body && vcam == rig.freeCamera)
        {
            // Spline length varies between rings; keep the requested 3–7m zoom.
            Vector3 offset = position - rig.orbitTarget.position;
            if (offset.sqrMagnitude > 0.0001f)
                position = rig.orbitTarget.position + offset.normalized * rig.ZoomDistance;
        }
        Vector3 constrained = stage == CinemachineCore.Stage.Body
            ? rig.source.ConstrainRenderedCamera(rig.CollisionOrigin, position, ref recovery, deltaTime)
            : rig.source.ConstrainCameraPosition(rig.CollisionOrigin, position);
        state.PositionCorrection += constrained - state.GetCorrectedPosition();
        if (stage == CinemachineCore.Stage.Body) { bodyPosition = constrained; bodySolved = true; }
    }
}
