// Bitkinin altında rarity'sini gösteren toon halka. Düz renkli bant + içe doğru sönen hafif dolgu.
// Epic/Legendary hafifçe nabız atar; Legendary'de dış halkada dönen kesikli çizgi vardır.
// _Time oyun zamanıdır: shop/menüde dünya donunca halka da durur.
Shader "ClickerGame/RarityAura"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 1
        _Pulse ("Pulse", Range(0, 1)) = 0
        _Dashes ("Dashes", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RarityAura"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            // Toprağın üstünde kalsın, z-fight olmasın.
            Offset -2, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Strength;
                float _Pulse;
                float _Dashes;
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

            float Band(float r, float center, float width)
            {
                float aa = max(fwidth(r), 1e-4);
                return 1.0 - smoothstep(width - aa, width + aa, abs(r - center));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float r = length(p);
                float wave = sin(_Time.y * 2.6) * 0.5 + 0.5;
                float pulse = 1.0 + (wave - 0.5) * 0.12 * _Pulse;
                float rr = r / pulse;

                // Ana toon halka ve koyu iç kontur
                float ring = Band(rr, 0.78, 0.07);
                float ink = Band(rr, 0.68, 0.025) * 0.6;
                // İçe doğru sönen hafif dolgu (nabızla parlaklaşır)
                float fill = smoothstep(0.72, 0.1, rr) * 0.22 * (1.0 + wave * 0.6 * _Pulse);

                // Legendary: dış halkada dönen kesikli çizgi
                float angle = atan2(p.y, p.x) / 6.28318 + 0.5;
                float dash = step(0.5, frac(angle * 10.0 + _Time.y * 0.35));
                float outer = Band(rr, 0.93, 0.035) * dash * _Dashes;

                float3 color = _Color.rgb;
                float alpha = saturate(ring + outer + fill) * _Strength;
                color = lerp(color, color * 0.35, saturate(ink / max(ring + ink + fill, 1e-3)));
                alpha = saturate(alpha + ink * _Strength * 0.5);
                alpha *= 1.0 - smoothstep(0.97, 1.0, r);
                return float4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
