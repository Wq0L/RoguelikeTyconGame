using UnityEngine;

// Run takvimi: kota dönemleri ve boss round'ları için TEK hesap. RoundManager, olay yürütücüsü, ödül ve uzmanlaşma yöneticileri,
// arayüz ve ölçüm araçları dönem / boss sorularını buradan sorar; kendi round aritmetiklerini tutmazlar.
// İki kaynak:
// - Açık takvim (RunProfileSO.bossCalendar dolu, Bölüm 3.7.2): her tarih bir boss round'udur ve bir kota dönemini kapatır.
//   Dönem k = bir önceki tarihten sonraki round … k. tarih. Kota ve boss hedefleri dönem sırasıyla tablodan okunur.
// - Eşit segment (takvim boş: eski profiller ve profilsiz sahne): segmentRounds'luk dönemler; boss havuzu varsa son segment
//   hariç her segmentin son round'u boss round'udur. Formüller eski hesapla aynıdır (HarvestQuota).
// Veriyi kopyalamaz, profili canlı okur. Geçersiz açık takvim düzeltilmez: Validate hatayı söyler, RoundManager run'ı başlatmaz.
public sealed class RunCalendar
{
    private readonly RunProfileSO profile;   // null: sahnedeki RoundManager alanları
    private readonly int sceneSegmentRounds, sceneRunLength;
    private readonly float sceneQuotaStart, sceneQuotaGrowth;

    public RunCalendar(RunProfileSO profile) => this.profile = profile;

    public RunCalendar(int segmentRounds, int runLength, float quotaStart, float quotaGrowth)
    {
        sceneSegmentRounds = segmentRounds;
        sceneRunLength = runLength;
        sceneQuotaStart = quotaStart;
        sceneQuotaGrowth = quotaGrowth;
    }

    public bool IsExplicit => profile != null && profile.bossCalendar != null && profile.bossCalendar.Count > 0;
    public int RunLength => profile != null ? profile.runLength : sceneRunLength;
    public bool QuotaEnabled => IsExplicit || (profile != null ? profile.segmentRounds : sceneSegmentRounds) > 0;
    // Eşit segment uzunluğu. Açık takvimde dönemler eşit değildir: PeriodLength kullanılır.
    public int UniformRounds => Mathf.Max(1, profile != null ? profile.segmentRounds : sceneSegmentRounds);

    private int Dates => profile.bossCalendar.Count;
    private int Date(int period) => profile.bossCalendar[Mathf.Clamp(period, 1, Dates) - 1].round;

    // Round'un dönemi (1'den başlar). Her round tam bir döneme aittir.
    public int PeriodOf(int round)
    {
        if (!IsExplicit) return HarvestQuota.SegmentOf(round, UniformRounds);
        for (int i = 0; i < Dates; i++)
            if (round <= profile.bossCalendar[i].round) return i + 1;
        return Dates;
    }

    public int PeriodStart(int period) =>
        IsExplicit ? (period <= 1 ? 1 : Date(period - 1) + 1) : HarvestQuota.SegmentStart(period, UniformRounds);

    public int PeriodEnd(int period) => IsExplicit ? Date(period) : HarvestQuota.SegmentEnd(period, UniformRounds);

    public int PeriodLength(int period) => PeriodEnd(period) - PeriodStart(period) + 1;

    public bool IsPeriodStart(int round) =>
        IsExplicit ? round == PeriodStart(PeriodOf(round)) : (round - 1) % UniformRounds == 0;

    // Dönemin son round'u: kota burada değerlendirilir.
    public bool IsPeriodEnd(int round) =>
        QuotaEnabled && (IsExplicit ? round == PeriodEnd(PeriodOf(round)) : round % UniformRounds == 0);

    // fromRound'dan itibaren (dahil) ilk boss round'u; yoksa 0. "Bu aşamanın ödülleri hangi boss'tan itibaren" sorusu buraya sorulur.
    public int NextBossRound(int fromRound)
    {
        for (int round = Mathf.Max(1, fromRound); round <= RunLength; round++)
            if (IsBossRound(round)) return round;
        return 0;
    }

    // Dönemin son round'unda boss var mı. Eşit segmentte run'ın son segmentinde boss yoktur; açık takvimde her tarih boss'tur.
    public bool HasBoss(int period) =>
        profile != null && profile.bossPool != null && period >= 1 &&
        (IsExplicit ? period <= Dates : period * UniformRounds < profile.runLength);

    public bool IsBossRound(int round) => profile != null && IsPeriodEnd(round) && HasBoss(PeriodOf(round));

    public long QuotaTarget(int period) =>
        profile != null ? profile.TargetFor(period) : HarvestQuota.Target(period, sceneQuotaStart, sceneQuotaGrowth);

    // 0: o dönemde boss hasadı hedefi yok (boss yine aktiftir).
    public long BossTarget(int period) =>
        HasBoss(period) && period <= profile.bossTargets.Count && profile.bossTargets[period - 1] > 0 ? profile.bossTargets[period - 1] : 0;

    // Takvimde "final" diye işaretlenmiş tarih (R50). Şimdilik yalnız kayıttır; kuralı havuzdaki normal boss'tur.
    public bool IsFinal(int period) => IsExplicit && period >= 1 && period <= Dates && profile.bossCalendar[period - 1].final;

    // Tarihe elle atanmış boss (boş: havuzdan seçilir). Bir tarih için tek boss kurulur.
    public SegmentEventSO FixedBoss(int period) =>
        IsExplicit && period >= 1 && period <= Dates ? profile.bossCalendar[period - 1].boss : null;

    // Run'ın son round'u boss round'uysa (açık takvim) başarıdan sonra kartlar ve boss ödülü açılır, sonra zafer ekranı gelir.
    // Eşit segmentli profillerde son round'da seçim açılmaz (eski davranış).
    public bool FinalRoundHasChoices => IsExplicit && IsBossRound(RunLength);

    // Açık takvimin veri denetimi. null: geçerli (ya da takvim boş: eski ritim). Aksi halde hatayı anlatan metin.
    public static string Validate(RunProfileSO profile)
    {
        if (profile == null || profile.bossCalendar == null || profile.bossCalendar.Count == 0) return null;
        var dates = profile.bossCalendar;
        if (profile.bossPool == null) return "boss takvimi dolu ama boss havuzu (bossPool) boş";
        if (profile.events != null && profile.events.Count > 0)
            return "boss takvimi eski 'Segment olayları' listesiyle birlikte kullanılamaz (liste bütün segmenti kapsayan eski ritim içindir)";
        int previous = 0;
        for (int i = 0; i < dates.Count; i++)
        {
            int round = dates[i].round;
            if (round < 1 || round > profile.runLength)
                return $"{i + 1}. tarih (Round {round}) run sınırlarının dışında (1–{profile.runLength})";
            if (round == previous) return $"Round {round} takvimde iki kez yazılmış ({i}. ve {i + 1}. tarih)";
            if (round < previous) return $"tarihler sıralı değil: {i + 1}. tarih (Round {round}) bir öncekinden (Round {previous}) küçük";
            if (dates[i].final && i != dates.Count - 1) return $"final kaydı (Round {round}) takvimin son tarihi olmalı";
            previous = round;
        }
        if (previous != profile.runLength)
            return $"son tarih Round {previous}, run {profile.runLength} round: son tarih run'ın son round'u olmalı " +
                   (previous + 1 == profile.runLength
                       ? $"(yoksa Round {profile.runLength} hiçbir kota dönemine ait olmaz)"
                       : $"(yoksa Round {previous + 1}–{profile.runLength} hiçbir kota dönemine ait olmaz)");
        if (profile.segmentTargets.Count != dates.Count)
            return $"kota tablosunda {profile.segmentTargets.Count} değer var, takvimde {dates.Count} dönem: her dönemin kotası açık yazılmalı";
        for (int i = 0; i < dates.Count; i++)
            if (profile.segmentTargets[i] <= 0)
                return $"{i + 1}. dönemin (Round {(i == 0 ? 1 : dates[i - 1].round + 1)}–{dates[i].round}) kotası {profile.segmentTargets[i]}: 0'dan büyük olmalı";
        if (profile.bossTargets.Count != dates.Count)
            return $"boss hasadı tablosunda {profile.bossTargets.Count} değer var, takvimde {dates.Count} boss: her boss'un hedefi açık yazılmalı (0: hedef yok)";
        for (int i = 0; i < dates.Count; i++)
            if (profile.bossTargets[i] < 0) return $"{i + 1}. boss'un (Round {dates[i].round}) hasat hedefi negatif";
        return null;
    }
}
