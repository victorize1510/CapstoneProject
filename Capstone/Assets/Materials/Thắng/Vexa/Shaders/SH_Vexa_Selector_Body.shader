Shader "SH_Vexa_Selector_Body"
{
    Properties
    {
        [MainTexture] _MultTexture("Mult Texture", 2D) = "white" {}
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
        _ID01("ID 01", 2D) = "white" {}
        _ID02("ID 02", 2D) = "white" {}
        _ID03("ID 03", 2D) = "white" {}
        _EyesColor("Eyes Color", Color) = (1,1,1,0)
        _EyesWhiteColor("Eyes White Color", Color) = (1,1,1,0)
        _HairColor("Hair Color", Color) = (0.08490568,0.08490568,0.08490568,0)
        _SkinColor("Skin Color", Color) = (1,1,1,0)
        _ClothColor("Cloth Color", Color) = (0.2735849,0.2735849,0.2735849,0)
        _ClothAccentColor("Cloth Accent Color", Color) = (1,0,0.5400758,0)
        _ClothAccentSecondaryColor("Cloth Accent Secondary Color", Color) = (1,0,0.5400758,0)
        _MetalColor("Metal Color", Color) = (0.6466714,0.7924528,0.7895944,0)
        [HDR] _EmissiveColor("Emissive Color", Color) = (0,1,0.980212,0)
        _EmissiveIntensity("Emissive Intensity", Float) = 1
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
        }            TEXTURE2D(_MultTexture); SAMPLER(sampler_MultTexture);
            TEXTURE2D(_NormalTexture); SAMPLER(sampler_NormalTexture);
            TEXTURE2D(_AmbientOcclusionTexture); SAMPLER(sampler_AmbientOcclusionTexture);
            TEXTURE2D(_ID01); SAMPLER(sampler_ID01);
            TEXTURE2D(_ID02); SAMPLER(sampler_ID02);
            TEXTURE2D(_ID03); SAMPLER(sampler_ID03);

            CBUFFER_START(UnityPerMaterial)
            float4 _MultTexture_ST;
            float4 _NormalTexture_ST;
            float4 _AmbientOcclusionTexture_ST;
            float4 _ID01_ST;
            float4 _ID02_ST;
            float4 _ID03_ST;
            float4 _EyesColor;
            float4 _EyesWhiteColor;
            float4 _HairColor;
            float4 _SkinColor;
            float4 _ClothColor;
            float4 _ClothAccentColor;
            float4 _ClothAccentSecondaryColor;
            float4 _MetalColor;
            float4 _EmissiveColor;
            float _EmissiveIntensity;
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
                float2 uv = input.uv;
                half4 id01 = SAMPLE_TEXTURE2D(_ID01, sampler_ID01, uv * _ID01_ST.xy + _ID01_ST.zw);
                half4 id02 = SAMPLE_TEXTURE2D(_ID02, sampler_ID02, uv * _ID02_ST.xy + _ID02_ST.zw);
                half4 id03 = SAMPLE_TEXTURE2D(_ID03, sampler_ID03, uv * _ID03_ST.xy + _ID03_ST.zw);

                half4 selected = lerp(_HairColor, _SkinColor, id01.r);
                selected = lerp(selected, _ClothAccentColor, id01.g);
                selected = lerp(selected, _ClothColor, id02.g);
                selected = lerp(selected, _MetalColor, id02.b);
                selected = lerp(selected, _EmissiveColor, id02.r);
                selected = lerp(selected, _ClothAccentSecondaryColor, id03.b);
                selected = lerp(selected, _EyesWhiteColor, id03.g);
                selected = lerp(selected, _EyesColor, id01.b);

                half3 albedo = SAMPLE_TEXTURE2D(_MultTexture, sampler_MultTexture, uv * _MultTexture_ST.xy + _MultTexture_ST.zw).rgb * selected.rgb;
                half occlusion = lerp(1.0h, SAMPLE_TEXTURE2D(_AmbientOcclusionTexture, sampler_AmbientOcclusionTexture, uv * _AmbientOcclusionTexture_ST.xy + _AmbientOcclusionTexture_ST.zw).r, saturate(_AOIntensity));
                half3 normalWS = GetVexaNormalWS(input, uv * _NormalTexture_ST.xy + _NormalTexture_ST.zw);
                half3 emission = _EmissiveColor.rgb * id02.r * _EmissiveIntensity;
                half3 color = ApplyVexaLighting(albedo, normalWS, input.positionWS, occlusion, emission);
                color = MixFog(color, input.fogCoord);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}