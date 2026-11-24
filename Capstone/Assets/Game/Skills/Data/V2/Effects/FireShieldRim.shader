Shader "Capstone/Pet Skills/Fire Shield Rim"
{
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" }
        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                float angle = input.uv.x * 6.283185;
                float time = _Time.y;
                float lick = sin(angle * 17.0 - time * 10.0) * sin(angle * 5.0 + time * 3.5);
                float edge = abs(input.uv.y - (0.53 + 0.12 * lick));
                half core = 1.0 - smoothstep(0.08, 0.23, edge);
                half glow = 1.0 - smoothstep(0.14, 0.46, edge);
                half3 color = lerp(half3(1.0, 0.23, 0.025), half3(1.0, 0.89, 0.50), core);
                return half4(color, saturate(core * 0.82 + glow * (0.24 + lick * 0.10)));
            }
            ENDCG
        }
    }
}
