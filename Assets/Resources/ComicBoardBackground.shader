// Yetenek ağacının comic kağıt zemini (10 Kasım 2026, kullanıcı: "derinliği var ama kötü hissettiriyor"): kitin diliyle basılı
// bir plan panosu. Yumuşak nebula / galaksi / parlama yok; her şey düz renk ve keskin kenar.
// Katmanlar (uzaktan yakına): taban rengi → tram noktalı bulutlar (0.18) → küçük nokta yıldızlar (0.1) → dört köşeli toon yıldızlar
// (0.45) → düğümlerle birebir kayan ızgara (hücre = düğüm aralığı, düğüm hücrenin ortasında). Derinlik: katmanlar ağaçla farklı
// hızda kayar (SkillTreeSpaceBackground pan / zoom verir). Tek gürültü örneği; eski uzay shader'ından ucuz.
Shader "UI/ComicBoardBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        _BaseCenter ("Base (centre)", Color) = (0.105, 0.2, 0.31, 1)
        _BaseEdge ("Base (edge)", Color) = (0.06, 0.105, 0.19, 1)
        _GridColor ("Grid", Color) = (0.3, 0.46, 0.62, 1)
        _DotColor ("Halftone A", Color) = (0.16, 0.29, 0.44, 1)
        _DotColorB ("Halftone B", Color) = (0.2, 0.22, 0.42, 1)
        _StarColor ("Stars", Color) = (0.74, 0.83, 0.93, 1)
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.5
        _DotStrength ("Halftone Strength", Range(0, 1)) = 0.8
        _DotScale ("Halftone dots per screen height", Range(10, 120)) = 54
        _StarDensity ("Star Density", Range(0, 1)) = 0.2
        _Vignette ("Vignette", Range(0, 1)) = 0.3
        _Parallax ("Parallax", Range(0, 2)) = 1
        [HideInInspector] _SpaceNoiseTex ("Cloud Noise (runtime)", 2D) = "black" {}
        [HideInInspector] _SpaceView ("View (time, aspect, zoom, cells per height)", Vector) = (0, 1.7778, 1, 7.2)
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
            float4 _BaseCenter, _BaseEdge, _GridColor, _DotColor, _DotColorB, _StarColor;
            float _GridStrength, _DotStrength, _DotScale, _StarDensity, _Vignette, _Parallax;
            float4 _SpaceView;
            float4 _SpacePan;
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

            // Düz, keskin kenarlı yıldız katmanı. pointed: 1 = dört köşeli toon yıldız, 0 = yuvarlak nokta.
            float ToonStars(float2 q, float cellsPerHeight, float density, float size, float pointed, float seed, float time)
            {
                q *= cellsPerHeight;
                float2 id = floor(q);
                float h = SpaceHash12(id + seed);
                float2 offset = (float2(SpaceHash12(id + seed + 17.3), SpaceHash12(id + seed + 41.7)) - 0.5) * 0.6;
                float2 d = frac(q) - 0.5 - offset;
                float s = size * (0.55 + 0.9 * SpaceHash12(id + seed + 7.1)) * (1.0 + 0.1 * sin(time * (0.5 + h * 1.5) + h * 40.0));
                float power = lerp(2.0, 0.62, pointed);
                // Şeklin "yarıçapı" (1. dereceden): kenar yumuşatması ekran pikselinden (q süreklidir). v'nin türevinden alınırsa
                // hücre sınırında sıçrar ve sınır boyunca ince çizikler çıkar.
                float n = pow(pow(abs(d.x) + 1e-5, power) + pow(abs(d.y) + 1e-5, power), 1.0 / power);
                float px = fwidth(q.x) + 1e-6;
                return (1.0 - smoothstep(s - px, s + px, n)) * step(h, density);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float time = _SpaceView.x;
                float aspect = _SpaceView.y;
                float zoom = max(_SpaceView.z, 0.01);
                float cells = max(_SpaceView.w, 0.5);
                float2 pan = _SpacePan.xy;

                // Ekran yüksekliği 1 birim, merkez 0.
                float2 p = (i.uv - 0.5) * float2(aspect, 1.0);
                // f: katmanın ağaca göre hızı (0 = sonsuz uzak, 1 = ağaçla birlikte).
                #define LAYER(f) (p / lerp(1.0, zoom, saturate((f) * _Parallax)) - pan * (f) * _Parallax)

                float r = length(p * float2(0.75, 1.05));
                float3 color = lerp(_BaseCenter.rgb, _BaseEdge.rgb, smoothstep(0.0, 1.0, r));

                // Tram bulutları: 45° döndürülmüş nokta ızgarası; nokta yarıçapı bulut yoğunluğundan (nokta merkezinde örneklenir,
                // noktalar tam yuvarlak kalır).
                float2 hp = LAYER(0.18) + float2(time * 0.003, 0.0);
                float2 hq = float2(hp.x - hp.y, hp.x + hp.y) * 0.7071 * _DotScale;
                float2 centre = (floor(hq) + 0.5) / (_DotScale * 0.7071);
                float2 hc = float2(centre.x + centre.y, centre.y - centre.x) * 0.5;
                float2 field = tex2D(_SpaceNoiseTex, hc * 1.1 / SPACE_NOISE_DOMAIN + 0.5).rg;
                float cloud = smoothstep(0.4, 0.9, field.r);
                float dotRadius = cloud * 0.46;
                float dotAa = fwidth(hq.x) * 1.2 + 1e-5;
                float halftone = (1.0 - smoothstep(dotRadius - dotAa, dotRadius + dotAa, length(frac(hq) - 0.5))) * step(0.04, cloud);
                color = lerp(color, lerp(_DotColor.rgb, _DotColorB.rgb, saturate(field.g * 1.5 - 0.25)), halftone * _DotStrength);

                // Yıldızlar: uzak küçük noktalar + daha yakın dört köşeli toon yıldızlar.
                float stars = ToonStars(LAYER(0.1), 30.0, 0.3 * _StarDensity, 0.06, 0.0, 3.1, time) * 0.3;
                stars = max(stars, ToonStars(LAYER(0.3) + 11.7, 13.0, 0.4 * _StarDensity, 0.1, 1.0, 31.7, time) * 0.45);
                stars = max(stars, ToonStars(LAYER(0.45) + 5.3, 6.0, 0.28 * _StarDensity, 0.12, 1.0, 71.3, time) * 0.7);
                color = lerp(color, _StarColor.rgb, stars);

                // Izgara: ağaçla birebir (düğüm tam sayı konumda, çizgiler yarım konumda: düğüm hücrenin ortasında durur). Kesişimlerde
                // kısa artı işaretleri daha belirgin. Çok uzaklaşınca (hücre birkaç piksel) söner: kıpırtı olmaz.
                float2 g = (p - pan) / zoom * cells;
                float2 gw = fwidth(g) + 1e-6;
                float2 gl = abs(frac(g) - 0.5);
                float2 lines = 1.0 - smoothstep(gw * 0.6, gw * 1.6, gl);
                float2 near = step(gl, 0.09);
                float grid = max(lines.x, lines.y) * 0.3 + max(lines.x * near.y, lines.y * near.x) * 0.7;
                float gridFade = saturate(1.0 - max(gw.x, gw.y) * 7.0);
                color = lerp(color, _GridColor.rgb, grid * _GridStrength * gridFade);

                float v = smoothstep(0.45, 1.05, length(p / float2(aspect * 0.62, 0.62)));
                color *= 1.0 - v * _Vignette;
                color += (SpaceHash12(i.uv * 4096.0) - 0.5) / 255.0;

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
