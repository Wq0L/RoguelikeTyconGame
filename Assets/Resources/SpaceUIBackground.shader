// Skill tree arka planı: UI Image üzerinde çizilen derinlikli uzay.
// Ağaç sürüklendikçe/zoom yapıldıkça katmanlar farklı hızlarda kayar (_SpacePan, _SpaceView.z):
// uzak nebula ve galaksi çok az, yakın yıldızlar ve toz ağaca yakın hızda hareket eder.
// Standart UI shader iskeleti (stencil, RectMask2D, alpha clip) korunur.
Shader "ClickerGame/SpaceUIBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _DeepColor ("Deep Color", Color) = (0.035, 0.035, 0.1, 1)
        _CenterColor ("Center Glow", Color) = (0.13, 0.09, 0.26, 1)
        _NebulaA ("Nebula A", Color) = (0.557, 0.231, 0.749, 1)
        _NebulaB ("Nebula B", Color) = (0.173, 0.498, 0.82, 1)
        _GalaxyColor ("Galaxy Color", Color) = (0.95, 0.78, 1, 1)
        _NebulaIntensity ("Nebula Intensity", Range(0, 2)) = 0.3
        _GalaxyIntensity ("Galaxy Intensity", Range(0, 2)) = 0.35
        _StarIntensity ("Star Intensity", Range(0, 3)) = 1
        _StarDensity ("Star Density", Range(0, 1)) = 0.45
        _Vignette ("Vignette", Range(0, 1)) = 0.5
        _Parallax ("Parallax", Range(0, 2)) = 1
        [HideInInspector] _SpaceNoiseTex ("Nebula Noise (runtime)", 2D) = "black" {}
        [HideInInspector] _SpaceView ("View (time, aspect, zoom, 0)", Vector) = (0, 1.7778, 1, 0)
        [HideInInspector] _SpacePan ("Pan in view heights", Vector) = (0, 0, 0, 0)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Assets/Shaders/SpaceNoise.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            fixed4 _Color;
            float4 _ClipRect;
            float4 _DeepColor, _CenterColor, _NebulaA, _NebulaB, _GalaxyColor;
            float _NebulaIntensity, _GalaxyIntensity, _StarIntensity, _StarDensity, _Vignette, _Parallax;
            float4 _SpaceView;
            float4 _SpacePan;
            // SpaceNoiseBaker'ın bir kez çizdiği nebula gürültüsü (R = yoğunluk, G = renk karışımı).
            sampler2D _SpaceNoiseTex;
            #define SPACE_NOISE_DOMAIN float2(5.6, 3.0)

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _SpaceView.x;
                float aspect = _SpaceView.y;
                float zoom = max(_SpaceView.z, 0.01);
                float2 pan = _SpacePan.xy;

                // Ekran yüksekliği 1 birim, merkez 0.
                float2 p = (i.uv - 0.5) * float2(aspect, 1.0);

                // f: katmanın ağaca göre hızı (0 = sonsuz uzak, 1 = ağaçla birlikte).
                #define LAYER(f) (p / lerp(1.0, zoom, saturate((f) * _Parallax)) - pan * (f) * _Parallax)

                float r = length(p * float2(0.8, 1.1));
                float3 color = lerp(_CenterColor.rgb, _DeepColor.rgb, smoothstep(0.0, 0.95, r));

                // Nebula (en uzak katman): bake edilmiş gürültüden tek örnek.
                float2 np = LAYER(0.05) * 1.3 + float2(sin(time * 0.013), cos(time * 0.011)) * 0.05;
                float2 field = tex2D(_SpaceNoiseTex, np / SPACE_NOISE_DOMAIN + 0.5).rg;
                float n = field.r;
                float n2 = field.g;
                float mask = smoothstep(0.42, 0.88, n);
                float3 nebula = lerp(_NebulaA.rgb, _NebulaB.rgb, saturate(n2 * 1.5 - 0.25));
                color += nebula * mask * (0.55 + 0.45 * smoothstep(0.3, 0.7, n2)) * _NebulaIntensity;

                // Galaksi: ağacın merkezinin arkasında yavaş dönen spiral
                float2 g = LAYER(0.1);
                float gr = length(g);
                float ga = atan2(g.y, g.x);
                float armWave = 0.5 + 0.5 * sin(ga * 2.0 - log(gr + 0.02) * 4.5 + time * 0.04);
                float arms = armWave * armWave * armWave;
                // Tanecik sadece galaksinin görünür olduğu yakın alanda hesaplanır.
                float grain = 0.6;
                UNITY_BRANCH if (gr < 1.3)
                    grain += 0.4 * (0.65 * SpaceNoise(g * 7.0 + 3.0) + 0.35 * SpaceNoise(g * 14.2 + 20.1));
                float disk = exp(-gr * 3.2) * arms * grain;
                float core = exp(-gr * gr * 40.0);
                color += _GalaxyColor.rgb * (disk * 0.7 + core * 1.1) * _GalaxyIntensity;

                // Yıldız katmanları: uzaktan yakına
                float density = _StarDensity;
                float3 stars = SpaceStarLayer(LAYER(0.12), 90.0, 0.55 * density, 0.07, 0.0, time) * 0.5;
                stars += SpaceStarLayer(LAYER(0.3) + 31.7, 44.0, 0.4 * density, 0.07, 0.15, time);
                stars += SpaceStarLayer(LAYER(0.55) + 71.3, 20.0, 0.22 * density, 0.06, 0.45, time) * 1.25;
                stars += SpaceSparkleLayer(LAYER(0.7) + 13.1, 5.0, 0.3 * density, time);
                color += stars * _StarIntensity * (1.0 - mask * 0.3);

                // Yakın toz: ağaçla neredeyse aynı hızda kayar, yavaşça süzülür
                float3 dust = SpaceStarLayer(LAYER(0.85) + float2(time * 0.006, -time * 0.004) + 5.3, 14.0, 0.35 * density, 0.035, 0.6, time * 0.5);
                color += dust * 0.35 * _StarIntensity;

                // Kenarları karart: odak ağacın ortasında kalsın
                float v = smoothstep(0.35, 0.95, length(p / float2(aspect * 0.62, 0.62)));
                color *= 1.0 - v * _Vignette;

                color += (SpaceHash12(i.uv * 4096.0 + frac(time) * 61.0) - 0.5) / 255.0;

                fixed4 c = fixed4(max(color, 0.0), 1.0) * i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a - 0.001);
                #endif
                return c;
            }
            ENDCG
        }
    }

    Fallback Off
}
