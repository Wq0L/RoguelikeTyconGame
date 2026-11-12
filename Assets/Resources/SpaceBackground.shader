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

            // Elliptical broken ink arcs, with a small sunlit lip like a drawn crater.
            float2 ComicCrater(float2 p, float2 center, float2 scale, float tilt, float aa)
            {
                float2 v = p - center;
                v = float2(v.x + v.y * tilt, v.y) / scale;
                float ring = length(v);
                float width = aa / min(scale.x, scale.y);
                float arc = smoothstep(-0.5, 0.1, v.y + v.x * 0.35);
                float ink = (1.0 - smoothstep(0.045, 0.045 + width, abs(ring - 1.0))) * arc;
                float lip = (1.0 - smoothstep(0.09, 0.09 + width, abs(ring - 1.19))) * saturate(v.y);
                return float2(ink, lip);
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

                // Boyalı gezegen (12 Kasım 2026; kullanıcı: "gezegen sırıtmış, bu işe uygun olsun"). Sahnenin boyalı diliyle aynı:
                // 3 bant (sıcak ışık / orta / mora kayan gölge) titrek sınırla, uzun fırça lekeleri, gölgede çapraz tarama,
                // ışık kenarında kırık parlama, sahne mürekkebiyle kalın kontur; yumuşak parlama halesi yerine iki sert bantlı
                // atmosfer halkası. _PlanetParams.w ile çarpılır (dallanma yok, fwidth güvenli).
                float planetOn = step(0.5, _PlanetParams.w);
                float2 center = (_PlanetParams.xy - 0.5) * float2(aspect, 1.0);
                float radius = max(_PlanetParams.z, 1e-3);
                float2 q = (LAYER(0.05) - center) / radius;
                float d = length(q);
                float z = sqrt(saturate(1.0 - d * d));
                float3 normal = float3(q, z);
                float3 lightDir = normalize(float3(-0.55, 0.45, 0.7));
                float lambert = saturate(dot(normal, lightDir));
                float planetAA = max(fwidth(d), 1e-4);
                float3 ink = float3(.08, .05, .10);
                float3 planet = _PlanetColor.rgb;
                [branch] if (d < 1.02 && planetOn > 0.5)
                {
                    float2 terrain = float2(q.x, q.y + z * 0.22);
                    float wob = (SpaceNoise(terrain * 5.0 + 3.1) - 0.5) * 0.18 + (SpaceNoise(terrain * 21.0) - 0.5) * 0.04;
                    float l = lambert + wob;
                    float band = step(0.30, l) + step(0.72, l);                 // 0 gölge, 1 orta, 2 ışık
                    float patches = SpaceNoise(terrain * float2(5.0, 8.0) + SpaceNoise(terrain * 3.0) * 2.0);
                    float paint = step(.38, patches) * .10 + step(.66, patches) * .10;
                    float3 baseCol = lerp(_PlanetColor.rgb * float3(1.0, .74, .64), float3(1.0, .74, .50), paint);
                    float3 litCol = lerp(baseCol, float3(1.0, .80, .58), .25);
                    float3 midCol = baseCol * .90;
                    float3 shadowCol = baseCol * float3(.46, .36, .64);          // mora kayan gölge
                    planet = band > 1.5 ? litCol : (band > 0.5 ? midCol : shadowCol);
                    float stroke = floor(SpaceNoise(float2(terrain.x * 2.6 + terrain.y * 1.1, terrain.y * 13.0) + 7.7) * 3.0) / 2.0 - 0.5;
                    planet *= 1.0 + stroke * 0.08;                               // uzun, eğik fırça lekeleri (3 ton)
                    // Çizgisel katman (kullanıcı: "gezegen biraz daha çizgisel olabilir"): bant sınırlarına elle çizilmiş mürekkep,
                    // leke kenarlarına ince çizgi, kürenin kıvrımını izleyen kuru fırça çizgileri, gölgede belirgin / en koyuda çapraz tarama.
                    float lw = max(fwidth(l), 1e-4);
                    float terminator = 1.0 - smoothstep(lw * 1.1, lw * 2.4, abs(l - 0.30));          // gölge sınırı: kalın
                    float litEdge = (1.0 - smoothstep(lw * .5, lw * 1.4, abs(l - 0.72)))
                        * step(.4, SpaceNoise(terrain * 9.0 + 5.3));                                   // ışık sınırı: ince, kesik
                    float pw = max(fwidth(patches), 1e-4);
                    float patchLine = (1.0 - smoothstep(pw * .6, pw * 1.6, abs(patches - .38)))
                        + (1.0 - smoothstep(pw * .6, pw * 1.6, abs(patches - .66))) * .7;
                    float flowCoord = terrain.y * 6.0 + (SpaceNoise(terrain * 2.2 + 9.1) - .5) * 1.4;
                    float fw = max(fwidth(flowCoord), 1e-4);
                    float flow = (1.0 - smoothstep(fw * .5, fw * 1.5, abs(frac(flowCoord + .5) - .5)))   // en yakın çizgiye uzaklık
                        * step(.55, SpaceNoise(float2(terrain.x * 4.0, flowCoord * .7) + 2.2)) * step(0.5, band);
                    float hatchA = step(frac((q.x + q.y) * 13.0), 0.30) * (1.0 - step(0.5, band));
                    float hatchB = step(frac((q.x - q.y) * 13.0), 0.30) * (1.0 - step(0.12, l));       // en koyuda çapraz
                    planet = lerp(planet, ink, saturate(hatchA + hatchB) * .62);
                    planet = lerp(planet, ink, saturate(patchLine) * .42 * step(0.5, band));
                    planet = lerp(planet, ink, saturate(flow) * .38);
                    planet = lerp(planet, ink, saturate(terminator) * .92);
                    planet = lerp(planet, ink, saturate(litEdge) * .55);
                    float2 crater = ComicCrater(q, float2(-.61, .35), float2(.12, .055), -.7, planetAA);
                    crater += ComicCrater(q, float2(.30, .51), float2(.23, .10), -.4, planetAA);
                    crater += ComicCrater(q, float2(-.20, .80), float2(.17, .045), -.3, planetAA);
                    crater += ComicCrater(q, float2(.49, -.23), float2(.18, .08), .5, planetAA);
                    crater += ComicCrater(q, float2(-.19, -.68), float2(.25, .065), .5, planetAA);
                    planet = lerp(planet, litCol, saturate(crater.y) * .55 * step(0.5, band));
                    planet = lerp(planet, ink, saturate(crater.x) * .9);
                    // ışık tarafında kenara yakın kırık parlama (karo kenarlarındaki gibi)
                    float rimBand = smoothstep(.845, .86, d) * (1.0 - smoothstep(.915, .93, d));
                    float chip = step(.38, SpaceNoise(float2(atan2(q.y, q.x) * 7.0, 1.7)));
                    planet = lerp(planet, float3(1.0, .86, .66), rimBand * step(.42, lambert) * chip * .8);
                }
                float outline = smoothstep(.945 - planetAA, .945 + planetAA, d);   // kalın mürekkep kontur
                planet = lerp(planet, ink, outline);
                float inside = (1.0 - smoothstep(1.0 - planetAA, 1.0 + planetAA, d)) * planetOn;
                color = lerp(color, planet, inside);
                // kademeli atmosfer: iki sert bant, ışık tarafında biraz daha geniş
                float lit = saturate(dot(normalize(q + 1e-4), lightDir.xy) * 0.5 + 0.6);
                float ringIn = 1.0 - smoothstep(1.028 - planetAA, 1.028 + planetAA, d - (lit - .6) * .02);
                float ringOut = 1.0 - smoothstep(1.062 - planetAA, 1.062 + planetAA, d - (lit - .6) * .04);
                float atmosphere = (ringIn * .36 + (ringOut - ringIn) * .14) * (1.0 - inside) * planetOn;
                color = lerp(color, _PlanetRim.rgb * .85, atmosphere);

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
