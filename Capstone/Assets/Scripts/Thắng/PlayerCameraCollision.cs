using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCameraCollision : CinemachineExtension
{
    public PlayerFreeLookRig rig;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Body || rig == null || !rig.IsReady || rig.ZoomDistance <= 0f) return;
        Vector3 position = state.GetCorrectedPosition();
        if (vcam == rig.freeCamera)
        {
            // Spline length varies between rings; keep the requested 3–7m zoom.
            Vector3 offset = position - rig.orbitTarget.position;
            if (offset.sqrMagnitude > 0.0001f)
                position = rig.orbitTarget.position + offset.normalized * rig.ZoomDistance;
        }
        state.PositionCorrection += rig.source.ConstrainCameraPosition(rig.CollisionOrigin, position) - state.GetCorrectedPosition();
    }
}
