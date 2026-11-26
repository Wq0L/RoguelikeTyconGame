// Toon gökyüzünün nebula gürültüsünü bir kez dokuya çizer (SpaceToonNoiseBaker). Her pikselde her karede 12+ gürültü hesabı yapılmasın.
// R = nebula yoğunluğu (SpaceToonNebulaField), G = ton karışımı (tek oktav). Alan: np ∈ [-2.8, 2.8] x [-1.5, 1.5].
Shader "Hidden/SpaceToonNoiseBake"
{
    SubShader
    {
        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Assets/Shaders/SpaceToon.hlsl"

            // SpaceToonNoiseBaker.DomainX/Y ve SpaceToonBackground.shader ile aynı olmalı.
            #define SPACE_TOON_NOISE_DOMAIN float2(5.6, 3.0)

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = float4(input.positionOS.xy, 0.5, 1.0);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 np = (input.uv - 0.5) * SPACE_TOON_NOISE_DOMAIN;
                return float4(SpaceToonNebulaField(np), SpaceNoise(np * 0.9 + 9.3), 0.0, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
