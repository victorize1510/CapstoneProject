Shader "Custom/SkyboxPanoramicBlend"
{
    Properties
    {
        _Texture1 ("Texture 1", 2D) = "white" {}
        _Texture2 ("Texture 2", 2D) = "white" {}
        _Blend ("Blend", Range(0,1)) = 0
        _Exposure ("Exposure", Range(0, 8)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Texture1;
            sampler2D _Texture2;
            float _Blend;
            float _Exposure;
            float _Rotation;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 texcoord : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.vertex.xyz;
                return o;
            }

            float2 ToRadialCoords(float3 dir)
            {
                float3 normalizedCoords = normalize(dir);
                float latitude = acos(normalizedCoords.y);
                float longitude = atan2(normalizedCoords.z, normalizedCoords.x);
                float2 sphereCoords = float2(longitude, latitude) * float2(0.5/UNITY_PI, 1.0/UNITY_PI);
                return float2(0.5,1.0) - sphereCoords;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = ToRadialCoords(i.texcoord);
                uv.x += _Rotation / 360.0;

                fixed4 col1 = tex2D(_Texture1, uv);
                fixed4 col2 = tex2D(_Texture2, uv);
                fixed4 result = lerp(col1, col2, _Blend);

                return result * _Exposure;
            }
            ENDHLSL
        }
    }
}