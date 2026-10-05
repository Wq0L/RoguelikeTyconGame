// Round süresi tablosu (Bölüm 3.7.8, RunProfileSO.roundDurations) için TEK hesap. RoundManager, arayüz, testler ve ölçüm araçları
// "şu round kaç saniye" sorusunu buraya sorar; round numarasına göre süre seçen kod başka yerde yazılmaz.
// Tablo boşsa (eski profiller) bu sınıf hiçbir şey belirlemez: süre eski yoldan gelir (profilin sabit süresi ya da süre stat'ı).
// Veriyi kopyalamaz, profili canlı okur. Geçersiz tablo düzeltilmez: Validate hatayı söyler, RoundManager run'ı başlatmaz.
public static class RoundDurations
{
    // Tablodaki bir round'un süre sınırları (sn). Alt sınır oyundaki diğer süre yollarının alt sınırıyla aynıdır; üst sınır veri
    // hatasına karşı bir güvenlik sınırıdır (tek bir round'un iki dakikayı aşması istenen bir tasarım değil).
    public const float MinSeconds = 30f, MaxSeconds = 120f;

    public static bool HasTable(RunProfileSO profile) =>
        profile != null && profile.roundDurations != null && profile.roundDurations.Count > 0;

    // Round'un süresi: başlangıcı bu round'u geçmeyen son satır. Tablo yoksa 0.
    public static float SecondsFor(RunProfileSO profile, int round)
    {
        if (!HasTable(profile)) return 0f;
        float seconds = profile.roundDurations[0].seconds;
        for (int i = 1; i < profile.roundDurations.Count && profile.roundDurations[i].fromRound <= round; i++)
            seconds = profile.roundDurations[i].seconds;
        return seconds;
    }

    // Run'ın bütün round'larının süre toplamı (aktif hasat süresi). Tablo yoksa 0.
    public static double TotalSeconds(RunProfileSO profile)
    {
        if (!HasTable(profile)) return 0d;
        double total = 0d;
        for (int round = 1; round <= profile.runLength; round++) total += SecondsFor(profile, round);
        return total;
    }

    // null: geçerli (ya da tablo boş: eski yol). Aksi halde hatayı anlatan metin.
    public static string Validate(RunProfileSO profile)
    {
        if (!HasTable(profile)) return null;
        if (profile.fixedRoundDuration > 0f)
            return $"süre tablosu sabit süreyle (fixedRoundDuration = {profile.fixedRoundDuration:0.##}) birlikte kullanılamaz: süre tek yerden gelmeli";
        int previous = 0;
        for (int i = 0; i < profile.roundDurations.Count; i++)
        {
            RoundDurationBand band = profile.roundDurations[i];
            if (i == 0 && band.fromRound != 1) return $"ilk satır Round 1'den başlamalı (yazılan: Round {band.fromRound})";
            if (band.fromRound == previous) return $"Round {band.fromRound} tabloda iki kez başlangıç yazılmış ({i}. ve {i + 1}. satır)";
            if (band.fromRound < previous) return $"satırlar sıralı değil: {i + 1}. satır (Round {band.fromRound}) bir öncekinden (Round {previous}) küçük";
            if (band.fromRound > profile.runLength) return $"{i + 1}. satır (Round {band.fromRound}) run sınırlarının dışında (1–{profile.runLength})";
            // NaN ve sonsuz da reddedilir (NaN ile her karşılaştırma yanlış döner).
            if (!(band.seconds >= MinSeconds && band.seconds <= MaxSeconds))
                return $"{i + 1}. satırın süresi {band.seconds:0.##} sn: {MinSeconds:0}–{MaxSeconds:0} sn arasında olmalı";
            previous = band.fromRound;
        }
        return null;
    }
}
