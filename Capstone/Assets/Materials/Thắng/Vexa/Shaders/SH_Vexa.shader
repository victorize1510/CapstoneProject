Shader "SH_Vexa"
{
    Properties
    {
        [MainTexture] _BaseColorTexture("Base Color Texture", 2D) = "white" {}
        [Normal] _NormalTexture("Normal Texture", 2D) = "bump" {}
        _NormalIntensity("Normal Intensity", Float) = 1
        _AmbientOcclusionTexture("Ambient Occlusion Texture", 2D) = "white" {}
        _AOIntensity("AO Intensity", Float) = 1
        _MetallicTexture("Metallic Texture", 2D) = "white" {}
        _MetallicMin("Metallic Min", Float) = 0
        _MetallicMax("Metallic Max", Float) = 1
        _RoughnessTexture("Roughness Texture", 2D) = "white" {}
        _RoughnessMin("Roughness Min", Float) = 0
        _RoughnessMax("Roughness Max", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
        HLSLPROGRAM
        #pragma target 3.0
        #pragma vertex vert
        #pragma fragment frag
        #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
        #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
        #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
        #pragma multi_compile_fragment _ _SHADOWS_SOFT
        #pragma multi_compile_fog

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            half3 normalWS : TEXCOORD2;
            half3 tangentWS : TEXCOORD3;
            half3 bitangentWS : TEXCOORD4;
            float fogCoord : TEXCOORD5;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            VertexPositionInputs positionInput = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

            output.positionHCS = positionInput.positionCS;
            output.positionWS = positionInput.positionWS;
            output.normalWS = normalInput.normalWS;
            output.tangentWS = normalInput.tangentWS;
            output.bitangentWS = normalInput.bitangentWS;
            output.uv = input.uv;
            output.fogCoord = ComputeFogFactor(output.positionHCS.z);
            return output;
        }            TEXTURE2D(_BaseColorTexture); SAMPLER(sampler_BaseColorTexture);
            TEXTURE2D(_NormalTexture); SAMPLER(sampler_NormalTexture);
            TEXTURE2D(_AmbientOcclusionTexture); SAMPLER(sampler_AmbientOcclusionTexture);

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColorTexture_ST;
            float4 _NormalTexture_ST;
            float4 _AmbientOcclusionTexture_ST;
            float _NormalIntensity;
            float _AOIntensity;
            float _MetallicMin;
            float _MetallicMax;
            float _RoughnessMin;
            float _RoughnessMax;
            CBUFFER_END        half3 GetVexaNormalWS(Varyings input, float2 normalUV)
        {
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, normalUV), _NormalIntensity);
            half3x3 tangentToWorld = half3x3(normalize(input.tangentWS), normalize(input.bitangentWS), normalize(input.normalWS));
            return normalize(TransformTangentToWorld(normalTS, tangentToWorld));
        }

        half3 ApplyVexaLighting(half3 albedo, half3 normalWS, float3 positionWS, half occlusion, half3 emission)
        {
            half3 color = albedo * SampleSH(normalWS) * occlusion;

            float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
            Light mainLight = GetMainLight(shadowCoord);
            half mainNdotL = saturate(dot(normalWS, mainLight.direction));
            color += albedo * mainLight.color * mainNdotL * mainLight.distanceAttenuation * mainLight.shadowAttenuation * occlusion;

            #if defined(_ADDITIONAL_LIGHTS)
            uint pixelLightCount = GetAdditionalLightsCount();
            for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
            {
                Light light = GetAdditionalLight(lightIndex, positionWS);
                half ndotl = saturate(dot(normalWS, light.direction));
                color += albedo * light.color * ndotl * light.distanceAttenuation * light.shadowAttenuation * occlusion;
            }
            #endif

            return color + emission;
        }
            half4 frag(Varyings input) : SV_Target
            {
                float2 baseUV = input.uv * _BaseColorTexture_ST.xy + _BaseColorTexture_ST.zw;
                float2 normalUV = input.uv * _NormalTexture_ST.xy + _NormalTexture_ST.zw;
                float2 aoUV = input.uv * _AmbientOcclusionTexture_ST.xy + _AmbientOcclusionTexture_ST.zw;

                half3 albedo = SAMPLE_TEXTURE2D(_BaseColorTexture, sampler_BaseColorTexture, baseUV).rgb;
                half occlusion = lerp(1.0h, SAMPLE_TEXTURE2D(_AmbientOcclusionTexture, sampler_AmbientOcclusionTexture, aoUV).r, saturate(_AOIntensity));
                half3 normalWS = GetVexaNormalWS(input, normalUV);
                half3 color = ApplyVexaLighting(albedo, normalWS, input.positionWS, occlusion, 0);
                color = MixFog(color, input.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}