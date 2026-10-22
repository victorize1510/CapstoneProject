Shader "Capstone/PetSummonEnergy"
{
    Properties
    {
        [HDR] _BaseColor ("Colour", Color) = (0.8,1,1,1)
        _Shape ("0 Shell, 1 Core, 2 Particle, 3 Ribbon, 4 Wave", Float) = 0
        _GroundY ("Spawn Floor", Float) = 0
        _ClipGround ("Clip Below Spawn Floor", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            // Render the outside once: the rear face must not double the flash's opacity.
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; half4 color:COLOR; };
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; float _Shape; float _GroundY; float _ClipGround;
            CBUFFER_END
            Varyings Vert(Attributes v) {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv; o.color = v.color; return o;
            }
            half4 Frag(Varyings i):SV_Target {
                if (_ClipGround > .5) clip(i.positionWS.y - _GroundY);
                half alpha = _BaseColor.a;
                half3 colour = _BaseColor.rgb;
                if (_Shape < .5) {
                    half rim = pow(1-saturate(abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS)))), 3);
                    alpha *= .08h + .92h*rim;
                } else if (_Shape < 1.5) {
                    // A bright centre with a soft silhouette, rather than a flat opaque disk.
                    half facing = saturate(abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))));
                    alpha *= pow(facing,1.5h);
                    colour *= 1.15h;
                } else if (_Shape < 2.5) {
                    float r = length(i.uv*2-1);
                    alpha *= 1-smoothstep(.2,1,r);
                    colour *= i.color.rgb; alpha *= i.color.a;
                } else if (_Shape < 3.5) {
                    alpha *= smoothstep(0,.18,i.uv.y)*smoothstep(0,.18,1-i.uv.y)*i.color.a;
                    colour *= i.color.rgb;
                } else {
                    half rim = 1-saturate(abs(dot(normalize(i.normalWS), GetWorldSpaceNormalizeViewDir(i.positionWS))));
                    alpha *= pow(rim, 5);
                    colour *= 1.25h;
                }
                return half4(colour,alpha);
            }
            ENDHLSL
        }
    }
}
