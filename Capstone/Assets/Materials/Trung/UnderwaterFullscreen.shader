Shader "Fantamon/UnderwaterFullscreen"
{
    Properties
    {
        _WaterColor   ("Water Color", Color) = (0.1, 0.4, 0.5, 1)
        _NormalMap    ("Distortion Normal (gán 723-normal)", 2D) = "bump" {}
        _FogDensity   ("Fog Density", Float) = 0.05
        _Refraction   ("Refraction Strength", Float) = 0.02
        _Speed        ("Scroll Speed", Float) = 0.1
        _NormalScale  ("Normal Tiling", Float) = 4
        _Tint         ("Overall Tint Amount", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "UnderwaterFullscreen"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Vert + Varyings (có sẵn input.texcoord) và _BlitTexture đến từ Blit.hlsl
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
             #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);

            float4 _WaterColor;
            float  _FogDensity;
            float  _Refraction;
            float  _Speed;
            float  _NormalScale;
            float  _Tint;

            float4 Frag (Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;

                // 1) Cuộn normal map theo thời gian để tạo gợn nước rung rinh (khúc xạ)
                float2 nuv = uv * _NormalScale + _Time.y * _Speed;
                float3 nrm = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, nuv));
                float2 offset = nrm.xy * _Refraction;

                // 2) Lấy màu màn hình đã bị làm méo
                float4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset);

                // 3) Fog theo độ sâu: càng xa càng chìm vào màu nước
                float rawDepth = SampleSceneDepth(uv + offset);
                float depth    = LinearEyeDepth(rawDepth, _ZBufferParams);
                float fog      = saturate(1.0 - exp(-_FogDensity * depth));
                col.rgb = lerp(col.rgb, _WaterColor.rgb, fog);

                // 4) Ám màu nước tổng thể cho ấm/lạnh tùy ý
                col.rgb *= lerp(float3(1,1,1), _WaterColor.rgb, _Tint);

                return col;
            }
            ENDHLSL
        }
    }
}
