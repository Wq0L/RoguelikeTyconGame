// Bölüm 2.2 deneyi: saksının elektrik yükü. Halka saat yönünde dolar (_Fill 0–1); hazırken tamamı parlak ve nabız atar.
// ZTest Always: saksı ve bitkinin üstünde görünür. _Pulse scriptten sürülür (unscaled time), oyun RNG'sine dokunmaz.
Shader "ClickerGame/ElectricChargeRing"
{
    Properties
    {
        _Color ("Color", Color) = (0.35, 0.9, 1, 1)
        _Fill ("Fill", Range(0, 1)) = 0
        _Ready ("Ready", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ElectricChargeRing"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Fill;
                float _Ready;
                float _Pulse;
            CBUFFER_END

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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv - 0.5;
                float r = length(p);
                float aa = max(fwidth(r), 1e-4);
                float ring = smoothstep(0.35 - aa, 0.35 + aa, r) * (1.0 - smoothstep(0.45 - aa, 0.45 + aa, r));

                // 0 = üst, saat yönünde artar.
                float angle = frac(atan2(p.x, p.y) / (2.0 * PI));
                float filled = max(step(angle + 1e-4, _Fill), _Ready);

                float3 ink = _Color.rgb * 0.3;
                float3 lit = lerp(_Color.rgb, float3(1.0, 1.0, 1.0), _Ready * 0.55 * _Pulse);
                float3 color = lerp(ink, lit, filled);
                float alpha = ring * lerp(0.35, 0.95, filled);
                float halo = _Ready * _Pulse * 0.45 * smoothstep(0.43, 0.46, r) * (1.0 - smoothstep(0.46, 0.5, r));
                return float4(color, saturate(alpha + halo) * _Color.a);
            }
            ENDHLSL
        }
    }
}
