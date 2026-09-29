// Bu round'un kart seçiminin düştüğü tile: kenarında kalın sarı toon çerçeve, dışa taşan hale ve içe sönen ışık.
// Quad tile'dan büyüktür (_Edge = tile kenarının uv'deki yeri); hale komşu tile'lara hafifçe taşar.
// _Glow scriptten sürülür (unscaled time): round sonunda timeScale 0 iken de nefes alır.
Shader "ClickerGame/FreshTileMarker"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.87, 0.32, 1)
        _Edge ("Tile Edge (uv)", Range(0.3, 1)) = 0.77
        _Glow ("Glow", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "FreshTileMarker"
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
                float _Edge;
                float _Glow;
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

            // Yuvarlak köşeli kare için işaretli mesafe (negatif = içeride).
            float RoundedBox(float2 p, float halfSize, float radius)
            {
                float2 q = abs(p) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float Band(float d, float center, float width)
            {
                float aa = max(fwidth(d), 1e-4);
                return 1.0 - smoothstep(width - aa, width + aa, abs(d - center));
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float d = RoundedBox(p, _Edge, _Edge * 0.16);
                float thickness = _Edge * 0.075;

                // Ana bant tile kenarının hemen içinde; iç kenarında ince koyu kontur (çizgi roman).
                float band = Band(d, -thickness, thickness);
                float ink = Band(d, -thickness * 2.0 - 0.012, 0.012);
                float halo = d > 0.0 ? exp(-d * 10.0) * 0.6 : 0.0;
                float fill = d < -thickness * 2.0 ? smoothstep(-_Edge * 0.7, -thickness * 2.0, d) * 0.3 : 0.0;

                float glow = 0.55 + 0.45 * _Glow;
                float3 color = lerp(_Color.rgb, _Color.rgb * 0.3, saturate(ink - band));
                float alpha = saturate(band + ink * 0.7 + (halo + fill) * glow);
                alpha *= 1.0 - smoothstep(0.94, 1.0, max(abs(p.x), abs(p.y)));
                return float4(color * (0.9 + 0.2 * _Glow), alpha * _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
