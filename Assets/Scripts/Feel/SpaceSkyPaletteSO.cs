using System;
using System.Collections.Generic;
using UnityEngine;

// Oyunun toon gökyüzü renkleri (23 Kasım 2026). Kullanıcı: "güce göre değişsin, müzik nasıl güçleniyorsa aynı şekilde".
// - Güç = müzik gücü (BossRewardManager.MusicPower): her boss ödülü kendi aşamasına göre +1 / +2 / +3 ekler; müzik havuzu da parçayı bu
//   değerle seçer. Gökyüzü aynı değeri okur, ikisi birlikte ilerler.
// - Renk teorisi: renk çemberinde soğuktan sıcağa yürüyüş (sakin → doruk). Zeminin ve bulutların koyuluğu (OKLab L) aşamalar arasında eşit
//   tutuldu: tarla her aşamada aynı netlikte okunur, yalnız renk sıcaklığı değişir. Geçiş OKLCh'de, ton açısı kısa yoldan döner:
//   düz RGB karışımının ortasındaki gri çamur oluşmaz.
// - Ayarlar > Görüntü > Arka Plan: "Sabit" seçilirse fixedPalette (mor toon) kullanılır; "Güce göre" varsayılandır.
// Renkler inspector'da sRGB'dir (shader'a dönüşümsüz verilir). Düzenleme Play'e basmadan inspector'daki güç önizlemesiyle görülür.
[CreateAssetMenu(menuName = "ClickerGame/Space Sky Palette", fileName = "SpaceSkyPalette")]
public sealed class SpaceSkyPaletteSO : ScriptableObject
{
    [Serializable]
    public struct SkyColors
    {
        [Tooltip("Merkez parlamasının en dış bandı (kenarlar).")] public Color deep;
        [Tooltip("Orta bant.")] public Color mid;
        [Tooltip("Tarlanın arkasındaki iç bant.")] public Color glow;
        [Tooltip("Nebula ton ailesi: A ile B arası gürültüyle karışır.")] public Color nebulaA;
        public Color nebulaB;
        [Tooltip("Nebulanın ışık (sol üst) tarafı.")] public Color lit;
    }

    [Serializable]
    public sealed class Stage
    {
        [Tooltip("Yalnız editörde görünür ad.")] public string name;
        [Tooltip("Bu müzik gücünden itibaren (dahil). İlk aşama 0 olmalı.")] [Min(0)] public int minPower;
        public SkyColors colors;
    }

    public const string ResourcePath = "SpaceSkyPalette";

    [Tooltip("Ayarlarda 'Sabit' seçilince kullanılan palet (onaylanan mor toon gökyüzü).")]
    public SkyColors fixedPalette;
    [Tooltip("Güce göre renk merdiveni; minPower'a göre artan sırada.")]
    public List<Stage> stages = new();
    [Tooltip("Güç değişince yeni renge geçiş süresi (sn, ölçeksiz zaman). Geçiş yeni round başlarken başlar, round ortasında renk kaymaz.")]
    [Min(0.1f)] public float transitionSeconds = 3.5f;
    [Tooltip("Nefes genliği: 0 kapalı, 1 onaylanan değer (bant / bulut sınırları birkaç piksel, 10 sn'lik döngü).")]
    [Range(0f, 2f)] public float breath = 1f;

    public int StageIndexFor(int power)
    {
        int index = 0;
        for (int i = 0; i < stages.Count; i++)
            if (stages[i] != null && power >= stages[i].minPower) index = i;
        return index;
    }

    public SkyColors ForPower(int power) => stages.Count == 0 ? fixedPalette : stages[StageIndexFor(power)].colors;

    // Güç önizlemesi için sürekli değer: iki aşamanın minPower'ı arasında yumuşak geçiş (editör önizlemesi; oyunda aşamalar arası geçiş
    // zamana bağlıdır).
    public SkyColors Preview(float power)
    {
        if (stages.Count == 0) return fixedPalette;
        int i = StageIndexFor(Mathf.FloorToInt(power));
        if (i >= stages.Count - 1) return stages[i].colors;
        float from = stages[i].minPower, to = Mathf.Max(from + 1f, stages[i + 1].minPower);
        return Blend(stages[i].colors, stages[i + 1].colors, Mathf.Clamp01((power - from) / (to - from)));
    }

    public static SkyColors Blend(in SkyColors a, in SkyColors b, float t) => new SkyColors
    {
        deep = OkLch.Lerp(a.deep, b.deep, t),
        mid = OkLch.Lerp(a.mid, b.mid, t),
        glow = OkLch.Lerp(a.glow, b.glow, t),
        nebulaA = OkLch.Lerp(a.nebulaA, b.nebulaA, t),
        nebulaB = OkLch.Lerp(a.nebulaB, b.nebulaB, t),
        lit = OkLch.Lerp(a.lit, b.lit, t),
    };

    public static bool Same(in SkyColors a, in SkyColors b) =>
        a.deep == b.deep && a.mid == b.mid && a.glow == b.glow && a.nebulaA == b.nebulaA && a.nebulaB == b.nebulaB && a.lit == b.lit;
}

// sRGB renk ↔ OKLab / OKLCh (Björn Ottosson, 2020; kamu malı formüller). Algıya göre eşit adımlı karışım için.
public static class OkLch
{
    public static Color Lerp(Color a, Color b, float t)
    {
        if (t <= 0f) return a;
        if (t >= 1f) return b;
        Vector3 x = FromColor(a), y = FromColor(b);
        float hx = x.z, hy = y.z;
        // Neredeyse grinin tonu anlamsız: diğerinin tonu kullanılır (geçiş gri üstünden dönmesin).
        if (x.y < 1e-4f) hx = hy;
        if (y.y < 1e-4f) hy = hx;
        float dh = Mathf.DeltaAngle(hx * Mathf.Rad2Deg, hy * Mathf.Rad2Deg) * Mathf.Deg2Rad;
        Color c = ToColor(new Vector3(Mathf.Lerp(x.x, y.x, t), Mathf.Lerp(x.y, y.y, t), hx + dh * t));
        c.a = Mathf.Lerp(a.a, b.a, t);
        return c;
    }

    // (L, C, h radyan)
    public static Vector3 FromColor(Color c)
    {
        float r = ToLinear(c.r), g = ToLinear(c.g), b = ToLinear(c.b);
        float l = Cbrt(0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b);
        float m = Cbrt(0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b);
        float s = Cbrt(0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b);
        float L = 0.2104542553f * l + 0.7936177850f * m - 0.0040720468f * s;
        float A = 1.9779984951f * l - 2.4285922050f * m + 0.4505937099f * s;
        float B = 0.0259040371f * l + 0.7827717662f * m - 0.8086757660f * s;
        return new Vector3(L, Mathf.Sqrt(A * A + B * B), Mathf.Atan2(B, A));
    }

    public static Color ToColor(Vector3 lch)
    {
        float A = lch.y * Mathf.Cos(lch.z), B = lch.y * Mathf.Sin(lch.z), L = lch.x;
        float l = L + 0.3963377774f * A + 0.2158037573f * B;
        float m = L - 0.1055613458f * A - 0.0638541728f * B;
        float s = L - 0.0894841775f * A - 1.2914855480f * B;
        l = l * l * l; m = m * m * m; s = s * s * s;
        float r = 4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s;
        float g = -1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s;
        float b = -0.0041960863f * l - 0.7034186147f * m + 1.7076147010f * s;
        return new Color(ToSrgb(r), ToSrgb(g), ToSrgb(b), 1f);
    }

    private static float Cbrt(float x) => x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
    private static float ToLinear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
    private static float ToSrgb(float c)
    {
        c = Mathf.Clamp01(c);
        return c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;
    }
}
