// Boss bölgesinin çevre çizgisi (Don Cephesi, Sert Kabuk). Bölgenin içi boyanmaz: tile kendi rengini korur.
// _Active 0: yaklaşan (kesikli çizgi); 1: aktif (düz çizgi, nefes alan parlaklık).
// UV.x çizgi boyunca dünya birimi (kesik deseni hücreler arasında hizalı), UV.y çizgi genişliği (0 dış kenar, 1 iç kenar).
// İki malzemeyle çizilir (FrostZoneMarkers): asıl çizgi normal derinlik testiyle (zemin çizgisi; saksı ve bitkiler onu örter),
// ikinci kopya yalnız bir nesnenin ARKASINDA kalan kısmı soluk çizer (_ZTest Greater, _Alpha): tarla saksıyla doluyken de
// bölgenin sınırı okunur. Dolgu yoktur; saksı ve bitkinin üstüne renk yayılmaz, yalnız ince soluk çizgi görünür.
// _Pulse scriptten sürülür (unscaled time): round sonunda timeScale 0 iken de nefes alır.
Shader "ClickerGame/FrostZoneMarker"
{
    Properties
    {
        _Color ("Color", Color) = (0.55, 0.82, 1, 1)
        _Active ("Active", Range(0, 1)) = 0
        _Pulse ("Pulse", Range(0, 1)) = 0.5
        _DashLength ("Dash period (world units)", Float) = 0.5
        _DashFill ("Dash fill", Range(0.1, 1)) = 0.58
        _Alpha ("Alpha", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest ("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-21" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "BossZoneOutline"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Active;
                float _Pulse;
                float _DashLength;
                float _DashFill;
                float _Alpha;
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
                float across = input.uv.y;
                float aa = max(fwidth(across), 1e-4);
                // Çizginin şekli: iki kenarda yumuşak bitiş.
                float shape = smoothstep(0.0, aa, across) * (1.0 - smoothstep(1.0 - aa, 1.0, across));
                // Ortada boss rengi, iki yanında koyu mürekkep: her tile renginin üstünde okunur.
                float core = smoothstep(0.24, 0.24 + aa, across) * (1.0 - smoothstep(0.76 - aa, 0.76, across));

                // Yaklaşan: kesikli; aktif: düz.
                float phase = frac(input.uv.x / max(_DashLength, 1e-3));
                float da = max(fwidth(input.uv.x / max(_DashLength, 1e-3)), 1e-4);
                float dash = smoothstep(0.0, da, phase) * (1.0 - smoothstep(_DashFill - da, _DashFill, phase));
                float visible = lerp(dash, 1.0, _Active);

                float3 ink = _Color.rgb * 0.22;
                float3 bright = lerp(_Color.rgb, float3(1, 1, 1), 0.18 * _Pulse * _Active);
                float3 color = lerp(ink, bright, core);
                float alpha = shape * visible * lerp(0.88, 1.0, _Active);
                return float4(color, alpha * _Color.a * _Alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
