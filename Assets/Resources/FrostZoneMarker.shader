// Don Cephesi şeridi: tile başına buz rengi. _Active 0: yaklaşan (taralı, açık); 1: aktif (dolu, koyu kenar).
// ZTest Always: saksı ve bitkilerin üstünde yarı saydam görünür (etkinin olduğu hücreler saksılı hücrelerdir).
// _Pulse scriptten sürülür (unscaled time): round sonunda timeScale 0 iken de nefes alır.
Shader "ClickerGame/FrostZoneMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.82, 1, 1)
        _Active ("Active", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-21" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "FrostZoneMarker"
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
                float _Active;
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

            float RoundedBox(float2 p, float halfSize, float radius)
            {
                float2 q = abs(p) - (halfSize - radius);
                return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - radius;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = (input.uv - 0.5) * 2.0;
                float d = RoundedBox(p, 0.94, 0.14);
                float aa = max(fwidth(d), 1e-4);
                float inside = 1.0 - smoothstep(-aa, aa, d);
                float border = 1.0 - smoothstep(0.06 - aa, 0.06 + aa, abs(d + 0.06));

                // Yaklaşan: çapraz tarama; aktif: dolu buz ve nefes alan parlaklık.
                float stripes = step(0.5, frac((input.uv.x + input.uv.y) * 5.0));
                float previewFill = 0.08 + stripes * 0.14;
                float activeFill = 0.24 + 0.08 * _Pulse;
                float fill = lerp(previewFill, activeFill, _Active);

                float3 ink = _Color.rgb * 0.35;
                float3 color = lerp(_Color.rgb, ink, border * _Active);
                float alpha = saturate(fill * inside + border * lerp(0.55, 0.9, _Active));
                return float4(color, alpha * _Color.a);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
