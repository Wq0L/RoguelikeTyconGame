using UnityEngine;

// Oyunun tam sayı (int) hasar, can ve kaynak değerlerine dönüşümün tek yeri (Bölüm 3.7.6.1). Geçici teknik güvenlik: sınırsız
// modun nihai büyük sayı modeli değildir (TODO: endless sayı modeli).
// - Normal aralık: eski yuvarlama aynen (Mathf.RoundToInt ile aynı: Math.Round, çift sayıya yuvarlama), sonra çağıranın en küçük
//   değeri (doğrudan vuruş 1, davranış 0 …). Çarpım çağıranın eski türüyle (float / double) yapılır; burada yalnız dönüşüm var.
// - int sınırını aşan sonlu pozitif değer ve +sonsuz: int.MaxValue'ya doyar (1'e, sıfıra ya da negatife taşmaz). Stat değeri
//   bundan büyük olabilir; uygulanan değer doyar. Sayılır, ilk seferde bir uyarı yazılır.
// - NaN ve -sonsuz: geçersiz. En küçük değer döner (oyun kilitlenmez); sayılır, ilk seferde bir hata yazılır (vuruş başına log yok).
// - Sonlu negatif değer: en küçük değere kırpılır (eski Math.Max(0, …) kuralı); sayılmaz.
public enum NumericSite { DirectDamage, CritDamage, BehaviorBase, BehaviorDamage, ChainDamage, EchoDamage, IncomingDamage, PlantHealth, Experience, Resource, Score }

public static class NumericSafety
{
    private const int SiteCount = (int)NumericSite.Score + 1;
    // float olarak 2^31; bu ve üstü int'e sığmaz.
    private const float IntLimitF = 2147483648f;
    private const double IntLimitD = 2147483648d;

    private static readonly int[] saturated = new int[SiteCount], invalid = new int[SiteCount];
    private static readonly bool[] warned = new bool[SiteCount], failed = new bool[SiteCount];

    // Ölçüm ve test: doyan / geçersiz girdiyle karşılaşılan dönüşüm sayısı (oyun başlarken sıfırlanır).
    public static int Saturated(NumericSite site) => saturated[(int)site];
    public static int Invalid(NumericSite site) => invalid[(int)site];
    public static int TotalSaturated { get { int sum = 0; foreach (int n in saturated) sum += n; return sum; } }
    public static int TotalInvalid { get { int sum = 0; foreach (int n in invalid) sum += n; return sum; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetCounters()
    {
        System.Array.Clear(saturated, 0, SiteCount);
        System.Array.Clear(invalid, 0, SiteCount);
        System.Array.Clear(warned, 0, SiteCount);
        System.Array.Clear(failed, 0, SiteCount);
    }

    public static int ToInt(float value, int min, NumericSite site)
    {
        if (float.IsNaN(value) || float.IsNegativeInfinity(value)) return Reject(min, site, value);
        if (value >= IntLimitF) return Saturate(site, value);
        if (value <= -IntLimitF) return min;
        int rounded = (int)System.Math.Round(value);
        return rounded < min ? min : rounded;
    }

    public static int ToInt(double value, int min, NumericSite site)
    {
        if (double.IsNaN(value) || double.IsNegativeInfinity(value)) return Reject(min, site, value);
        if (value >= IntLimitD - .5d) return value >= IntLimitD ? Saturate(site, value) : int.MaxValue;
        if (value <= int.MinValue) return min;
        int rounded = (int)System.Math.Round(value);
        return rounded < min ? min : rounded;
    }

    // İki negatif olmayan tam sayının toplamı (banka, can): int.MaxValue'da doyar.
    public static int Add(int total, int amount, NumericSite site)
    {
        long sum = (long)total + amount;
        if (sum > int.MaxValue) return Saturate(site, sum);
        return (int)System.Math.Max(int.MinValue, sum);
    }

    // Kullanılamayan sayı girdisini (NaN / sonsuz; ör. XP, can çarpanı) dönüştürmeden bildirmek için: değer çağıranca kullanılmaz
    // (XP eklenmez, çarpan uygulanmaz); sayılır, ilk seferde hata yazılır. Sonsuz bir stat (float sınırı, 3,4e38) da buraya düşer.
    public static void ReportInvalid(NumericSite site, double value) => Note(invalid, failed, site, value, "Değer kullanılmadı (eklenmedi / uygulanmadı)");

    private static int Reject(int min, NumericSite site, double value)
    {
        Note(invalid, failed, site, value, "En küçük değer kullanıldı");
        return min;
    }

    private static int Saturate(NumericSite site, double value)
    {
        Note(saturated, warned, site, value, null);
        return int.MaxValue;
    }

    // invalidOutcome: geçersiz değerle ne yapıldığı (hata logu); null: doyum (uyarı).
    private static void Note(int[] counter, bool[] logged, NumericSite site, double value, string invalidOutcome)
    {
        int i = (int)site;
        if (counter[i] < int.MaxValue) counter[i]++;
        if (logged[i]) return;
        logged[i] = true;
        if (invalidOutcome != null)
            Debug.LogError($"Sayısal güvenlik ({site}): geçersiz değer {value}. {invalidOutcome}; oyun sürüyor. " +
                           "Bu, bozuk veri, hesap hatası ya da sayı sınırını aşmış bir stat demektir (bu oturumda bu tür için bir kez yazılır, sonrakiler sayılır).");
        else
            Debug.LogWarning($"Sayısal güvenlik ({site}): {value} int sınırını aşıyor; {int.MaxValue} değerine doyuruldu " +
                             "(geçici teknik sınır; bu oturumda bu tür için bir kez yazılır, sonrakiler sayılır).");
    }
}
