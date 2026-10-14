Shader "Capstone/PetSummonBodyGlow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1, 1, 1, 1)
        _BaseMap ("Pet Texture", 2D) = "white" {}
        _DissolveNoise ("Dissolve Noise", 2D) = "gray" {}
        _BaseColor ("Pet Tint", Color) = (1, 1, 1, 1)
        _WhiteBlend ("White Light Blend", Range(0, 1)) = 0.75
        _GlowEmission ("Soft Emission", Range(0, 1)) = 0.12
        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0
        _DissolveDirection ("0 Bottom Up, 1 Outside In", Float) = 0
        _EdgeWidth ("Dissolve Edge Width", Range(0.005, 0.15)) = 0.035
        [HDR] _EdgeColor ("Dissolve Edge Colour", Color) = (0.7,1,1,1)
        _EdgeEmission ("Edge Emission", Float) = 1.5
        _ReferenceCenter ("Reference Local Center", Vector) = (0,0,0,0)
        _ReferenceRadius ("Reference Local Radius", Float) = 1
        _BodyCenter ("Original Body Center", Vector) = (0, 0, 0, 0)
        _PresentationCenter ("Visible Body Center", Vector) = (0, 0, 0, 0)
        _PresentationScale ("Visible Body Scale", Range(0, 1)) = 1
        _RecallBall ("Recall Ball", Vector) = (0,0,0,0)
        _RecallAxis ("Recall Axis and Extent", Vector) = (0,0,1,1)
        _RecallWarp ("Recall Enabled, Time, Delay, Twist", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+50" }
        Pass
        {
            Name "BodyGlow"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            ZTest LEqual
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float3 referencePosition : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DissolveNoise);
            SAMPLER(sampler_DissolveNoise);

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half4 _BaseColor;
                float4 _BaseMap_ST;
                float4 _BodyCenter;
                float4 _PresentationCenter;
                float _PresentationScale;
                float4 _RecallBall;
                float4 _RecallAxis;
                float4 _RecallWarp;
                half _WhiteBlend;
                half _GlowEmission;
                float _DissolveAmount;
                float _DissolveDirection;
                float _EdgeWidth;
                half4 _EdgeColor;
                float _EdgeEmission;
                float4 _ReferenceCenter;
                float _ReferenceRadius;
                float4x4 _ReferenceWorldToLocal;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.referencePosition = (mul(_ReferenceWorldToLocal, float4(positionWS,1)).xyz
                    - _ReferenceCenter.xyz) / max(.0001, _ReferenceRadius);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                if (_RecallWarp.x > .5) {
                    float3 axis = _RecallAxis.xyz;
                    float3 offset = positionWS - _BodyCenter.xyz;
                    float axial = dot(offset, axis);
                    float near = saturate(.5 + .5 * axial / max(.001, _RecallAxis.w));
                    float start = .18 + _RecallWarp.z * (1-near);
                    float pull = smoothstep(start, .56, _RecallWarp.y);
                    float3 radial = offset - axis*axial;
                    float angle = _RecallWarp.w * pull * (.35 + .65*near);
                    float sine, cosine; sincos(angle, sine, cosine);
                    radial = radial*cosine + cross(axis, radial)*sine;
                    positionWS = lerp(_BodyCenter.xyz, _RecallBall.xyz, pull)
                        + axis*axial*(1-pull) + radial*(1-pull)*(1-pull);
                    normalWS = normalWS*cosine + cross(axis,normalWS)*sine
                        + axis*dot(axis,normalWS)*(1-cosine);
                } else {
                    positionWS = _PresentationCenter.xyz
                        + (positionWS - _BodyCenter.xyz) * saturate(_PresentationScale);
                }
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalWS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Continuous object-relative noise: unaffected by the presentation's travel/scale.
                float3 p = input.referencePosition;
                float noise = SAMPLE_TEXTURE2D(_DissolveNoise,sampler_DissolveNoise,p.xz*1.7+p.y*.3).r;
                float bottomUp = saturate(.5-p.y*.5);
                float outsideIn = 1-saturate(length(p));
                float field = saturate(lerp(bottomUp,outsideIn,saturate(_DissolveDirection))*.88 + noise*.12);
                clip(field - _DissolveAmount);
                half4 pet = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half shade = 0.72h + 0.28h * saturate(dot(normalize(input.normalWS),
                    normalize(half3(0.3h, 0.75h, 0.4h))));
                half3 light = lerp(pet.rgb, _GlowColor.rgb, _WhiteBlend) * shade;
                float edge = (1-smoothstep(0,_EdgeWidth,field-_DissolveAmount))
                    * step(.001,_DissolveAmount) * (1-step(.999,_DissolveAmount));
                return half4(light + _GlowColor.rgb * _GlowEmission + _EdgeColor.rgb*edge*_EdgeEmission,
                    _GlowColor.a * pet.a);
            }
            ENDHLSL
        }
    }
}
