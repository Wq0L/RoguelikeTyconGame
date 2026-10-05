using System;
using System.Collections.Generic;
using UnityEngine;

// Bir run'ın tek yapılandırması: uzunluk, kota segmentleri ve hedefleri, segment olayları (boss), başlangıç ekonomisi.
// Aktif profil Resources/RunProfileSelection'dan okunur (Tools > Run Profili). Profil seçili değilse
// RoundManager ve ResourceManager sahnedeki kendi alanlarını kullanır.
[CreateAssetMenu(menuName = "ClickerGame/Run Profile", fileName = "RunProfile")]
public class RunProfileSO : ScriptableObject
{
    [Tooltip("Arayüzde ve konsolda görünen ad.")]
    public string displayName = "Run";
    [Min(1)] public int runLength = 130;

    [Header("Hasat Kotası")]
    [Min(1)] public int segmentRounds = HarvestQuota.DefaultSegmentRounds;
    [Tooltip("Tabloda olmayan segmentlerin kotası: başlangıç × büyüme^(segment−1).")]
    [Min(5f)] public float quotaStart = HarvestQuota.DefaultStart;
    [Range(1f, 3f)] public float quotaGrowth = HarvestQuota.DefaultGrowth;
    [Tooltip("Segment kotaları tablosu: 0. eleman 1. segment. Boş ya da 0 olan segmentler eğriden gelir.")]
    public List<long> segmentTargets = new();

    [Header("Segment olayları")]
    [Tooltip("Hangi segmentte hangi olay (boss) olacağı. Olay bir önceki segmentin başında duyurulur.")]
    public List<SegmentEventEntry> events = new();

    [Header("Boss sonrası uzmanlaşma")]
    [Tooltip("Bu segmentin kotası geçilince uzmanlaşma seçilir (0: yok). Segment run'ın son segmentiyse ekran açılmaz. Run başına tek seçim.")]
    [Min(0)] public int specializationAfterSegment;
    [Tooltip("Seçim ekranındaki seçenekler (yalnız biri alınır).")]
    public List<SpecializationSO> specializationOptions = new();

    [Header("Boss ritmi (Bölüm 3.4) — boş bırakılırsa profil eskisi gibi çalışır")]
    [Tooltip("Dolu ise her segmentin boss'u bu havuzdan seçilir: ilk round'larda duyurulur, yalnız segmentin son round'unda aktiftir. " +
             "Run'ın son segmentinde boss yoktur. 'Segment olayları' listesi bu durumda kullanılmaz.")]
    public BossPoolSO bossPool;
    [Tooltip("Boss round'unda (yalnız o round) kazanılması gereken Harvest Score: 0. eleman 1. segment. Segment kotasına ek koşuldur; " +
             "0 ya da eksik: o segmentte boss hedefi yok. Oyuncunun gücüne göre ölçeklenmez.")]
    public List<long> bossTargets = new();
    [Tooltip("Boss geçilince sunulan ödüller (boş: ödül yok).")]
    public BossRewardPoolSO bossRewards;
    [Tooltip("Boss ve ödül seçimi seed'i. 0: her run farklı. Başka bir sayı: aynı kararlarla aynı dizi (test ve karşılaştırma).")]
    public int bossSeed;

    [Header("Boss takvimi (Bölüm 3.7.2) — boş bırakılırsa segmentRounds ritmi (eski davranış)")]
    [Tooltip("Açık boss tarihleri, küçükten büyüğe. Her tarih bir kota dönemini kapatır: dönem, bir önceki tarihten sonraki round'dan bu " +
             "round'a kadardır. Boss dönemin ilk round'unda duyurulur, yalnız bu round'da aktiftir. Son tarih run'ın son round'u olmalıdır. " +
             "Doluysa segmentRounds kullanılmaz; kota ve boss hedefi tabloları dönem sırasıyla (0. eleman 1. dönem) eksiksiz yazılır. " +
             "Geçersiz takvim düzeltilmez: run başlamaz ve hata yazılır.")]
    public List<BossDate> bossCalendar = new();

    [Header("Denge verisi (Bölüm 3.5) — boş bırakılırsa ortak asset'ler kullanılır")]
    [Tooltip("Bu profilin kendi temel statları, can eğrisi, XP tablosu, skill tree'si ve fiyatları. Boş: eski profillerdeki ortak veri.")]
    public RunBalanceSO balance;

    [Header("Level kartları (Bölüm 3.6)")]
    [Tooltip("Kazanılan her level kaç ayrı kart seçimi hakkı verir. Her seçimde üç aday gösterilir, biri alınır; sonraki seçimin " +
             "adayları önceki seçim uygulandıktan sonra oluşur. 1: eski davranış (level başına bir seçim).")]
    [Range(1, 5)] public int choicesPerLevel = 1;

    [Header("Başlangıç ekonomisi")]
    [Min(0)] public int startingGold = 80;
    [Min(0)] public int startingIron;
    [Min(0)] public int startingStone;
    [Tooltip("Yüksek bütçeli test profili. Yalnız editörde uygulanır; build'de ResourceManager'ın normal başlangıcı kullanılır.")]
    public bool debugBudget;

    [Header("Deney (Bölüm 2.2) — varsayılanlar mevcut oyunu değiştirmez")]
    // Emekli elektrik deneyi: yalnız eski asset/test verisi uyumluluğu için tutulur; oynanışı etkilemez.
    [HideInInspector]
    public ElectricTriggerMode electricMode = ElectricTriggerMode.KillChance;
    [HideInInspector]
    public ElectricChargeTuning electricCharge = new();
    [Tooltip(">0: round süresini profil belirler (30–60 sn). Süre yükseltmeleri ve süreden gelen tempo bu run'da etkisizdir; " +
             "yalnız süre veren node'lar satın alınamaz ve önkoşul olarak karşılanmış sayılır. 0: mevcut kural.")]
    [Min(0f)] public float fixedRoundDuration;

    [Header("Round süresi tablosu (Bölüm 3.7.8) — boş bırakılırsa yukarıdaki sabit süre / süre yükseltmeleri (eski davranış)")]
    [Tooltip("Round numarasına göre süre: her satır 'bu round'dan itibaren şu kadar saniye' der (ilk satır Round 1). Doluysa süre " +
             "yükseltmeleri ve süreden gelen tempo bu run'da etkisizdir (sabit süredeki kuralla aynı); 60 sn üstü de yazılabilir " +
             "(sınırlar: RoundDurations). Sabit süreyle birlikte kullanılamaz. Geçersiz tablo düzeltilmez: run başlamaz ve hata yazılır.")]
    public List<RoundDurationBand> roundDurations = new();

    [Header("Bitiş")]
    [Tooltip("Son round'da kota geçilince run sonu ekranının başlığı.")]
    public string victoryTitle = "RUN TAMAMLANDI";

    [NonSerialized] private RunCalendar calendar;
    // Kota dönemleri ve boss round'ları için tek hesap (veriyi kopyalamaz, bu profili canlı okur).
    public RunCalendar Calendar => calendar ??= new RunCalendar(this);

    public long TargetFor(int segment) =>
        segment >= 1 && segment <= segmentTargets.Count && segmentTargets[segment - 1] > 0
            ? segmentTargets[segment - 1]
            : HarvestQuota.Target(segment, quotaStart, quotaGrowth);

    // Boss ritmi: eşit segmentte son segment hariç her segmentin son round'u, açık takvimde her tarih boss round'udur.
    public bool HasBoss(int segment) => Calendar.HasBoss(segment);

    public long BossTargetFor(int segment) => Calendar.BossTarget(segment);

#if UNITY_EDITOR
    private void OnValidate()
    {
        string error = RunCalendar.Validate(this);
        if (error != null) Debug.LogError($"Run profili '{name}': boss takvimi geçersiz — {error}. Bu hâliyle run başlamaz.", this);
        error = RoundDurations.Validate(this);
        if (error != null) Debug.LogError($"Run profili '{name}': round süresi tablosu geçersiz — {error}. Bu hâliyle run başlamaz.", this);
    }
#endif
}

// Round süresi tablosunun bir satırı: fromRound'dan (dahil) bir sonraki satıra kadar her round bu kadar saniye sürer.
[Serializable]
public struct RoundDurationBand
{
    [Min(1)] public int fromRound;
    [Min(1f)] public float seconds;
}

// Boss takvimindeki bir tarih: boss round'u ve o round'da kapanan kota dönemi.
[Serializable]
public struct BossDate
{
    [Min(1)] public int round;
    [Tooltip("Run finali (yalnız takvimin son tarihi olabilir). Şimdilik kayıttır: kuralı havuzdaki normal boss'tur; " +
             "final içeriği (canavar bitki) ayrı pakette tanımlanacak.")]
    public bool final;
    [Tooltip("Bu tarihte havuz yerine kullanılacak boss (boş: havuzdan seçilir). Tarih başına tek boss kurulur.")]
    public SegmentEventSO boss;
}

public enum ElectricTriggerMode { KillChance = 0, Charge = 1 }

// Yük modunda saksının elektrik şansı (tile + rezonans + global, 0–1) dolum süresine çevrilir:
//   süre = max(alt sınır, referans süre × referans şans / şans × Hızlı Şarj)
// Şans arttıkça süre kısalır (monoton), şans 0 ise yük oluşmaz, alt sınır Hızlı Şarj dahil her şeyden sonra uygulanır.
[Serializable]
public class ElectricChargeTuning
{
    [Tooltip("Referans şanstaki dolum süresi (sn). İlk değer, mevcut kuralın sahnede ölçülen elektrik sıklığından seçildi.")]
    [Min(0.1f)] public float referenceSeconds = 10f;
    [Tooltip("Referans elektrik şansı (0–1).")]
    [Range(0.05f, 1f)] public float referenceChance = 0.5f;
    [Tooltip("Dolum süresinin alt sınırı (sn).")]
    [Min(0.1f)] public float minimumSeconds = 3f;
    [Tooltip("Deney: Hızlı Şarj. Yalnız dolum süresini çarpar (1 = yok); oyuncunun saldırı hızını değiştirmez.")]
    [Range(0.2f, 1f)] public float quickChargeMultiplier = 1f;

    public float Seconds(float chance) => chance <= 0f ? float.PositiveInfinity :
        Mathf.Max(minimumSeconds, referenceSeconds * referenceChance / Mathf.Min(1f, chance) * quickChargeMultiplier);
}

[Serializable]
public struct SegmentEventEntry
{
    [Min(1)] public int segment;
    public SegmentEventSO segmentEvent;
}
