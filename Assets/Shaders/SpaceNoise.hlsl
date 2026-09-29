// Uzay arka planlarının ortak fonksiyonları (oyun sahnesi + skill tree).
// Hem HLSLPROGRAM (URP) hem CGPROGRAM (UI) içinden include edilebilir; isimler çakışmasın diye "Space" önekli.
#ifndef CLICKER_SPACE_NOISE_INCLUDED
#define CLICKER_SPACE_NOISE_INCLUDED

float SpaceHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 SpaceHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

float SpaceNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = SpaceHash12(i);
    float b = SpaceHash12(i + float2(1, 0));
    float c = SpaceHash12(i + float2(0, 1));
    float d = SpaceHash12(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

float SpaceFbm(float2 p)
{
    float value = 0.0;
    float amplitude = 0.5;
    for (int i = 0; i < 4; i++)
    {
        value += amplitude * SpaceNoise(p);
        p = p * 2.03 + 17.1;
        amplitude *= 0.5;
    }
    return value;
}

// Hücre başına en fazla bir yıldız. Yıldız hücre kenarından en az 0.25 uzak kalır; halo kenara varmadan söner.
// AA sürekli grid koordinatından hesaplanır: hücre sınırında çizgi artefaktı olmaz.
float3 SpaceStarLayer(float2 p, float cells, float chance, float radius, float glow, float time)
{
    float2 grid = p * cells;
    float2 id = floor(grid);
    float2 local = frac(grid) - 0.5;
    float h = SpaceHash12(id);
    float present = step(h, chance);
    float2 jitter = (SpaceHash22(id + 3.7) - 0.5) * 0.5;
    float d = length(local - jitter);
    float aa = max(length(fwidth(grid)) * 0.7, 1e-4);
    float core = 1.0 - smoothstep(radius - aa, radius + aa, d);
    float halo = exp(-d * d / (radius * radius * 3.0)) * glow * (1.0 - smoothstep(0.12, 0.24, d));
    float twinkle = 0.6 + 0.4 * sin(time * (1.2 + h * 5.0) + h * 60.0);
    float brightness = (0.45 + 0.55 * SpaceHash12(id + 11.3)) * twinkle * present;
    float3 tint = lerp(float3(0.72, 0.84, 1.0), float3(1.0, 0.88, 0.72), SpaceHash12(id + 5.1));
    return tint * (core + halo) * brightness;
}

// Az sayıda büyük, dört kollu parıltılı yıldız (yakın katman). Kollar yavaşça nefes alır.
float3 SpaceSparkleLayer(float2 p, float cells, float chance, float time)
{
    float2 grid = p * cells;
    float2 id = floor(grid);
    float2 local = frac(grid) - 0.5;
    float h = SpaceHash12(id + 91.7);
    float present = step(h, chance);
    float2 jitter = (SpaceHash22(id + 17.3) - 0.5) * 0.4;
    float2 q = local - jitter;
    float pulse = 0.65 + 0.35 * sin(time * (0.8 + h * 2.0) + h * 40.0);
    float arms = exp(-abs(q.x) * 60.0) * exp(-q.y * q.y * 90.0) + exp(-abs(q.y) * 60.0) * exp(-q.x * q.x * 90.0);
    float core = exp(-dot(q, q) * 900.0);
    float edge = 1.0 - smoothstep(0.2, 0.3, max(abs(q.x), abs(q.y)));
    float3 tint = lerp(float3(0.75, 0.88, 1.0), float3(1.0, 0.9, 0.75), SpaceHash12(id + 2.9));
    return tint * (arms * 0.55 * pulse + core * 1.3) * present * edge;
}

#endif
