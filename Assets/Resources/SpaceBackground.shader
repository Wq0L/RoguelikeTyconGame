// Prosedürel uzay arka planı: radyal gradyan + nebula + 3 paralaks yıldız katmanı + gezegen + kayan yıldız.
// Tek bir quad üzerinde çizilir (SpaceBackground.cs). Derinlik yazmaz; Transparent kuyruğunun başında
// çizildiği için opak geometrinin arkasında kalır ve early-z ile kapalı pikseller hesaplanmaz.
Shader "ClickerGame/SpaceBackground"
{
    Properties
    {
        _DeepColor ("Deep Color", Color) = (0.043, 0.043, 0.118, 1)
        _HorizonColor ("Center Glow", Color) = (0.165, 0.106, 0.302, 1)
        _NebulaA ("Nebula A", Color) = (0.557, 0.231, 0.749, 1)
        _NebulaB ("Nebula B", Color) = (0.173, 0.498, 0.82, 1)
        _NebulaIntensity ("Nebula Intensity", Range(0, 2)) = 0.34
        _StarIntensity ("Star Intensity", Range(0, 3)) = 0.95
        _StarDensity ("Star Density", Range(0, 1)) = 0.38
        _PlanetColor ("Planet Color", Color) = (0.95, 0.55, 0.42, 1)
        _PlanetRim ("Planet Rim", Color) = (0.5, 0.8, 1, 1)
        _PlanetParams ("Planet (viewport x, y, radius, enabled)", Vector) = (0.86, 0.8, 0.13, 1)
        _ShootingRate ("Shooting Star Rate", Range(0, 1)) = 0.35
        _Parallax ("Parallax", Range(0, 1)) = 0.35
        [HideInInspector] _SpaceNoiseTex ("Nebula Noise (runtime)", 2D) = "black" {}
        [HideInInspector] _SpaceView ("View (time, aspect, zoom, margin)", Vector) = (0, 1.7778, 0, 1.1)
        [HideInInspector] _SpaceOffset ("Camera offset in view heights", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-499"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "SpaceBackground"
            Blend Off
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Shaders/SpaceNoise.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float4 _HorizonColor;
                float4 _NebulaA;
                float4 _NebulaB;
                float _NebulaIntensity;
                float _StarIntensity;
                float _StarDensity;
                float4 _PlanetColor;
                float4 _PlanetRim;
                float4 _PlanetParams;
                float _ShootingRate;
                float _Parallax;
                float4 _SpaceView;
                float4 _SpaceOffset;
            CBUFFER_END

            // SpaceNoiseBaker'ın bir kez çizdiği nebula gürültüsü (R = yoğunluk, G = renk karışımı).
            TEXTURE2D(_SpaceNoiseTex);
            SAMPLER(sampler_SpaceNoiseTex);
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
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float3 ShootingStar(float2 p, float aspect, float time)
            {
                float period = lerp(16.0, 3.0, _ShootingRate);
                float slot = floor(time / period);
                float local = time - slot * period;
                const float duration = 0.85;
                // Dallanma yok: fwidth akış kontrolü içinde kullanılmasın.
                float visible = step(local, duration) * step(1e-4, _ShootingRate);

                float h1 = SpaceHash12(float2(slot, 1.7));
                float h2 = SpaceHash12(float2(slot, 9.1));
                float h3 = SpaceHash12(float2(slot, 4.4));
                float2 start = float2((h1 - 0.3) * aspect * 0.8, 0.1 + h2 * 0.35);
                float angle = radians(195.0 + h3 * 35.0);
                float2 dir = float2(cos(angle), sin(angle));
                float progress = saturate(local / duration);
                float2 head = start + dir * progress * 0.6;

                float2 rel = p - head;
                float along = dot(rel, -dir);
                float across = abs(dot(rel, float2(-dir.y, dir.x)));
                const float trail = 0.17;
                float inTrail = smoothstep(-0.004, 0.004, along) * (1.0 - saturate(along / trail));
                float width = 0.0016 + 0.0022 * inTrail;
                float aa = max(fwidth(across), 1e-4);
                float line_ = 1.0 - smoothstep(width - aa, width + aa, across);
                float fade = sin(progress * PI);
                float headGlow = exp(-dot(rel, rel) / 0.00002) * 0.8; // yuvarlak, parlak baş
                return float3(1.0, 0.95, 0.85) * (line_ * inTrail * inTrail + headGlow) * fade * visible * 1.6;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float time = _SpaceView.x;
                float aspect = _SpaceView.y;
                float zoom = _SpaceView.z;
                float margin = _SpaceView.w;
                float2 camOffset = _SpaceOffset.xy;

                // Görünür alanın yüksekliği 1 birim olan ekran koordinatı (merkez 0).
                float2 p = (input.uv - 0.5) * margin * float2(aspect, 1.0);

                // Katman başına paralaks: uzak katmanlar kamerayla neredeyse hiç kaymaz.
                #define LAYER(f) (p * (1.0 + zoom * (f) * _Parallax * 2.0) + camOffset * (f) * _Parallax)

                float r = length(p * float2(0.75, 1.1));
                float3 color = lerp(_HorizonColor.rgb, _DeepColor.rgb, smoothstep(0.0, 0.9, r));

                // Nebula: önceden bake edilmiş gürültüden tek örnek (her karede fbm hesaplanmaz).
                // Küçük bir salınım nebulaya canlılık verir, bake alanının içinde kalır.
                float2 np = LAYER(0.08) * 1.4 + float2(sin(time * 0.013), cos(time * 0.011)) * 0.05;
                float2 field = SAMPLE_TEXTURE2D(_SpaceNoiseTex, sampler_SpaceNoiseTex, np / SPACE_NOISE_DOMAIN + 0.5).rg;
                float n = field.r;
                float n2 = field.g;
                float mask = smoothstep(0.42, 0.88, n);
                float3 nebula = lerp(_NebulaA.rgb, _NebulaB.rgb, saturate(n2 * 1.5 - 0.25));
                nebula *= mask * (0.55 + 0.45 * smoothstep(0.3, 0.7, n2)) * _NebulaIntensity;
                color += nebula;

                float density = _StarDensity;
                float3 stars = SpaceStarLayer(LAYER(0.12), 95.0, 0.55 * density, 0.07, 0.0, time) * 0.55;
                stars += SpaceStarLayer(LAYER(0.25) + 31.7, 48.0, 0.42 * density, 0.07, 0.15, time);
                stars += SpaceStarLayer(LAYER(0.45) + 71.3, 22.0, 0.22 * density, 0.06, 0.45, time) * 1.3;
                color += stars * _StarIntensity * (1.0 - mask * 0.35);

                // Gezegen: ışıklı küre, yavaş dönen bantlar ve atmosfer halkası.
                // _PlanetParams.w ile çarpılır (dallanma yok, fwidth güvenli).
                float planetOn = step(0.5, _PlanetParams.w);
                float2 center = (_PlanetParams.xy - 0.5) * float2(aspect, 1.0);
                float radius = max(_PlanetParams.z, 1e-3);
                float2 q = (LAYER(0.05) - center) / radius;
                float d = length(q);
                float z = sqrt(saturate(1.0 - d * d));
                float3 normal = float3(q, z);
                float3 lightDir = normalize(float3(-0.55, 0.45, 0.7));
                float lambert = saturate(dot(normal, lightDir));
                // Bant gürültüsü sadece gezegenin üstündeki piksellerde hesaplanır (içeride türev kullanılmaz).
                float band = 0.5;
                [branch] if (d < 1.02 && planetOn > 0.5)
                {
                    float bandCoord = q.y * 6.0 + SpaceNoise(q * 2.5 + float2(time * 0.02, 0.0)) * 1.6;
                    band = SpaceNoise(float2(bandCoord, 0.5));
                }
                float3 surface = lerp(_PlanetColor.rgb * 0.55, _PlanetColor.rgb, band);
                float3 planet = surface * (0.05 + lambert);
                float rim = saturate(1.0 - z);
                planet += _PlanetRim.rgb * rim * rim * rim * (0.25 + lambert);
                float planetAA = max(fwidth(d), 1e-4);
                float inside = (1.0 - smoothstep(1.0 - planetAA, 1.0 + planetAA, d)) * planetOn;
                color = lerp(color, planet, inside);
                float lit = saturate(dot(normalize(q + 1e-4), lightDir.xy) * 0.5 + 0.6);
                color += _PlanetRim.rgb * exp(-max(d - 1.0, 0.0) * 14.0) * (1.0 - inside) * 0.35 * lit * planetOn;

                color += ShootingStar(LAYER(0.1), aspect, time);

                // Koyu gradyanlarda bantlaşmayı önleyen hafif dither.
                color += (SpaceHash12(input.uv * 4096.0 + frac(time) * 61.0) - 0.5) / 255.0;
                return float4(max(color, 0.0), 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
