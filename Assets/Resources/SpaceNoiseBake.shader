// Nebula gürültüsünü bir kez texture'a çizer (SpaceNoiseBaker). R = yoğunluk, G = renk karışımı.
// Vertex doğrudan clip-space quad alır; kamera/matris bağımsız, herhangi bir anda çalıştırılabilir.
Shader "Hidden/ClickerGame/SpaceNoiseBake"
{
    SubShader
    {
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Assets/Shaders/SpaceNoise.hlsl"

            // SpaceNoiseBaker.DomainX/Y ile aynı olmalı.
            #define SPACE_NOISE_DOMAIN float2(5.6, 3.0)

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

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy, 0.5, 1.0);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 np = (input.uv - 0.5) * SPACE_NOISE_DOMAIN;
                float warp = SpaceFbm(np * 0.9 + 4.0);
                float n = SpaceFbm(np + warp * 1.3);
                float n2 = SpaceFbm(np * 1.7 + 9.3);
                return float4(n, n2, 0.0, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
