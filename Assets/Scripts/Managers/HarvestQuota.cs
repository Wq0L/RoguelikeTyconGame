using System;

// Hasat Kotası: run, N round'luk segmentlere bölünür. Segmentte kazanılan Harvest Score segmentin son round'unda
// kotayı geçmeli, yoksa run biter. Kota her segmentte aynı oranda büyür; tarla büyümeyi bırakınca kota onu yakalar.
// RoundManager, arayüz ve run simülatörü aynı hesabı kullanır.
public static class HarvestQuota
{
    public const int DefaultSegmentRounds = 5;
    // Simülatör (2026-09-30, 24 seed): yeni oyuncu profilinin 24 run'ından 19'u kotaya takılır (round P10/P50/P90 70/75/95;
    // Tarla Tükendi'de 98/105/111). 40. round'a kadar 24'te 1. Orta ve deneyimli profiller 130 round boyunca geçer
    // (en dar geçişte kotanın 6–8 katı). İlk segmentler herkes için rahat: 10, 15, 20, 30, 45 …
    public const float DefaultStart = 10f;
    public const float DefaultGrowth = 1.45f;

    public static int SegmentOf(int round, int segmentRounds) => (Math.Max(1, round) - 1) / Math.Max(1, segmentRounds) + 1;

    public static int SegmentEnd(int segment, int segmentRounds) => Math.Max(1, segment) * Math.Max(1, segmentRounds);

    public static long Target(int segment, float start, float growth) =>
        Nice(start * Math.Pow(Math.Max(1f, growth), Math.Max(1, segment) - 1));

    // Okunur hedefler: 100'ün altında 5'in katı, üstünde iki anlamlı basamak (135, 1.500, 12.000).
    public static long Nice(double value)
    {
        if (double.IsNaN(value) || value <= 5) return 5;
        if (value >= 1e17) return (long)1e17;
        double step = value < 100 ? 5 : Math.Pow(10, Math.Floor(Math.Log10(value)) - 1);
        return (long)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step);
    }

    // "12.345": kültüre bağlı değil (round özetiyle aynı biçim).
    public static string Format(long value)
    {
        if (value < 0) value = 0;
        char[] buffer = new char[26];
        int position = buffer.Length, digits = 0;
        do
        {
            if (digits > 0 && digits % 3 == 0) buffer[--position] = '.';
            buffer[--position] = (char)('0' + value % 10);
            value /= 10;
            digits++;
        } while (value > 0);
        return new string(buffer, position, buffer.Length - position);
    }
}
