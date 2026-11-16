#ifndef CAPSTONE_CAMERA_OCCLUSION_CLIP_INCLUDED
#define CAPSTONE_CAMERA_OCCLUSION_CLIP_INCLUDED
// Named globals are scoped and restored by CameraVisibility's render callbacks.
float4 _CapstoneFocusA, _CapstoneFocusB;
float _CapstoneOcclusionView;

float CapstoneOcclusionRegion(float4 focus, float2 uv, float eyeDepth)
{
    if (focus.w <= 0) return 0;
    float3 view = TransformWorldToView(focus.xyz);
    if (-view.z <= .05 || eyeDepth >= -view.z - .12) return 0;
    float4 projected = ComputeScreenPos(TransformWorldToHClip(focus.xyz));
    float2 delta = uv - projected.xy / projected.w;
    delta.x *= _ScaledScreenParams.x / _ScaledScreenParams.y;
    // Soft vertical ellipse around the actor's silhouette, in screen-height units.
    delta.y *= .72;
    // URP flips projection Y when rendering into textures. Radius is a magnitude, never a signed projection value.
    float radius = clamp(focus.w * abs(UNITY_MATRIX_P._m11) / max(.1, -view.z) * .5, .035, .23);
    return 1 - smoothstep(radius * .45, radius, length(delta));
}

void CapstoneCameraClip(float4 positionCS)
{
    if (_CapstoneOcclusionView < .5) return;
    // The complete player fades uniformly before near-plane penetration. It is
    // separate from the scenery ellipse, so it cannot cut holes into the body.
    if (_CapstoneActorFade > .0001)
        clip(1 - saturate(_CapstoneActorFade) - max(.001, InterleavedGradientNoise(positionCS.xy, 0)));
    if (_CapstoneCameraFade <= .0001) return;
    float2 uv = positionCS.xy / _ScaledScreenParams.xy;
    float eyeDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
    float region = max(CapstoneOcclusionRegion(_CapstoneFocusA, uv, eyeDepth),
                       CapstoneOcclusionRegion(_CapstoneFocusB, uv, eyeDepth));
    // Keep 18% coverage even at full occlusion; never erase a whole house.
    float coverage = 1 - saturate(_CapstoneCameraFade) * region * .82;
    clip(coverage - InterleavedGradientNoise(positionCS.xy, 0));
}
#endif
