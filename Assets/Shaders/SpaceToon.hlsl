// Toon uzay gökyüzü (23 Kasım 2026; kullanıcı: "arkadaki uzay problem gibi hissettiriyor", toonlaştırıldı ve onaylandı).
// Mevcut uzayın katman yapısı korunur (merkez parlaması, nebula, 3 paralaks yıldız katmanı + seyrek parıltı); yumuşak geçişler yok:
// merkez parlaması 3 boyalı bant, nebula 3 cel tonu + ışık tarafı + ince mürekkep kontur, yıldızlar halesiz sert nokta.
// Çok yavaş "nefes": bant ve bulut sınırları birkaç piksel genişleyip daralır; renk / parlaklık sabittir, yanıp sönme yok.
// Renkler sRGB değerleriyle hesaplanır, çıkışta doğrusala çevrilir (proje doğrusal renk uzayında).
// Hem SpaceToonBackground.shader (oyun) hem editör önizlemesi kullanır. Nebula gürültüsü SpaceToonNoiseBaker'ın dokusundan okunur.
#ifndef CLICKER_SPACE_TOON_INCLUDED
#define CLICKER_SPACE_TOON_INCLUDED

#include "Assets/Shaders/SpaceNoise.hlsl"

#define SPACE_TOON_INK float3(0.035, 0.028, 0.075)

struct SpaceToonPalette
{
    float3 deep, mid, glow;     // merkez parlamasının 3 bandı (dış → iç)
    float3 nebulaA, nebulaB;    // nebula ton ailesi (ikisi arası gürültüyle karışır)
    float3 lit;                 // nebulanın ışık tarafı
};

float3 SpaceToonToLinear(float3 c) { return pow(max(c, 0.0), 2.2); }

float SpaceToonStep(float edge, float x)
{
    float w = max(fwidth(x), 1e-5);
    return smoothstep(edge - w, edge + w, x);
}

// x'in edge'i kestiği yerde yaklaşık px piksel kalınlıkta çizgi.
float SpaceToonLine(float edge, float x, float px)
{
    float w = max(fwidth(x), 1e-5);
    return 1.0 - smoothstep(w * px * 0.5, w * (px * 0.5 + 1.2), abs(x - edge));
}

// Sert kenarlı nokta yıldızlar: hale yok; parıldama = nadiren kısa süre küçülme (solma değil).
float4 SpaceToonDots(float2 p, float cells, float chance, float radius, float time, float bright)
{
    float2 grid = p * cells;
    float2 id = floor(grid);
    float2 local = frac(grid) - 0.5;
    float h = SpaceHash12(id);
    float present = step(h, chance);
    float2 jitter = (SpaceHash22(id + 3.7) - 0.5) * 0.5;
    float d = length(local - jitter);
    float aa = max(length(fwidth(grid)) * 0.7, 1e-4);
    float blink = step(0.975, frac(time * (0.12 + h * 0.25) + h * 7.0));
    float r = radius * (0.7 + 0.6 * SpaceHash12(id + 11.3)) * (1.0 - 0.5 * blink);
    float cov = (1.0 - smoothstep(r - aa, r + aa, d)) * present;
    float3 tint = lerp(float3(0.78, 0.88, 1.0), float3(1.0, 0.92, 0.72), step(0.5, SpaceHash12(id + 5.1)));
    return float4(tint * bright, cov);
}

// Seyrek dört kollu toon parıltı (astroid); çok hafif nefes alır.
float4 SpaceToonSparkles(float2 p, float cells, float chance, float size, float time)
{
    float2 grid = p * cells;
    float2 id = floor(grid);
    float2 local = frac(grid) - 0.5;
    float h = SpaceHash12(id + 91.7);
    float present = step(h, chance);
    float2 q = local - (SpaceHash22(id + 17.3) - 0.5) * 0.4;
    float pulse = 1.0 + 0.05 * sin(time * (0.8 + h * 1.5) + h * 40.0);
    float s = size * (0.7 + 0.6 * SpaceHash12(id + 4.4)) * pulse;
    float2 a = abs(q) / s;
    float v = pow(a.x, 0.6) + pow(a.y, 0.6);
    float w = max(fwidth(v), 1e-4);
    float cov = (1.0 - smoothstep(1.0 - w, 1.0 + w, v)) * present;
    float3 tint = lerp(float3(0.82, 0.9, 1.0), float3(1.0, 0.93, 0.74), step(0.5, SpaceHash12(id + 2.9)));
    return float4(tint, cov);
}

// p: ekran (yükseklik 1, merkez 0). n / nLit: nebula yoğunluğu (nLit ışığa doğru küçük bir adımdaki değer), hue: ton karışımı (0–1).
// breath: −1…1 nefes evresi × genlik. density: yıldız yoğunluğu. Dönüş doğrusal renk.
float3 SpaceToonSky(float2 p, float n, float nLit, float hue, float2 lS1, float2 lS2, float2 lS3, float2 lSpark,
                    float time, float breath, float density, SpaceToonPalette pal)
{
    float r = length(p * float2(0.75, 1.1)) + (n - 0.5) * 0.14;
    float3 color = lerp(lerp(pal.glow, pal.mid, SpaceToonStep(0.30 + 0.014 * breath, r)), pal.deep, SpaceToonStep(0.60 + 0.018 * breath, r));

    float nb = 0.012 * breath;
    float3 neb = lerp(pal.nebulaA, pal.nebulaB, smoothstep(0.35, 0.65, hue));
    float m1 = SpaceToonStep(0.56 - nb, n), m2 = SpaceToonStep(0.66 - nb, n), m3 = SpaceToonStep(0.76 - nb, n);
    float3 cloud = lerp(lerp(lerp(color, neb, 0.20), lerp(color, neb, 0.34), m2), lerp(color, neb * 1.1, 0.48), m3);
    cloud = lerp(cloud, lerp(cloud, pal.lit, 0.16), SpaceToonStep(0.018, n - nLit));
    color = lerp(color, cloud, m1);
    color = lerp(color, SPACE_TOON_INK, SpaceToonLine(0.56 - nb, n, 1.0) * 0.55);

    float4 s = SpaceToonDots(lS1, 95.0, 0.55 * density, 0.08, time, 0.55);
    color = lerp(color, s.rgb, s.a * (1.0 - m1 * 0.4));
    s = SpaceToonDots(lS2 + 31.7, 48.0, 0.42 * density, 0.08, time, 0.8);
    color = lerp(color, s.rgb, s.a * (1.0 - m1 * 0.3));
    s = SpaceToonDots(lS3 + 71.3, 22.0, 0.22 * density, 0.075, time, 1.0);
    color = lerp(color, s.rgb, s.a);
    s = SpaceToonSparkles(lSpark + 13.1, 5.0, 0.22, 0.13, time);
    color = lerp(color, s.rgb, s.a);
    return SpaceToonToLinear(color);
}

// SpaceToonNoiseBake'in hesapladığı alan (bake ile birebir aynı olmalı): 3 oktav döndürülmüş fbm, alan bükmeli.
float SpaceToonFbm3(float2 p)
{
    const float2x2 rot = float2x2(0.8, -0.6, 0.6, 0.8);
    float v = 0.5 * SpaceNoise(p);
    p = mul(rot, p) * 2.03 + 17.1; v += 0.25 * SpaceNoise(p);
    p = mul(rot, p) * 2.03 + 17.1; v += 0.125 * SpaceNoise(p);
    return v / 0.875;
}

float SpaceToonNebulaField(float2 np)
{
    float warp = SpaceToonFbm3(np * 0.9 + 4.0);
    return SpaceToonFbm3(np + warp * 1.3);
}

#endif
