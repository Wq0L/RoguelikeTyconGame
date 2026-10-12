using System;
using System.Collections.Generic;
using UnityEngine;

// Geç oyun tarla olaylarının TEK veri kaynağı (Bölüm 3.8): takvim, süreler ve alanlar. Run profili bu asset'i gösterirse
// (RunProfileSO.fieldEvents) olaylar o profilde çalışır; göstermezse (eski profiller, demo) hiçbir şey olmaz.
// Olaylar boss ödülü değildir: FieldEventDirector yürütür, BossRewardManager'la ilişkisi yoktur.
// - Takvim: 'firstRound'dan önce olay yok. Boss round'unda 'bossRounds' listesinden, diğer round'larda 'normalRoundChance'
//   olasılıkla 'normalRounds' listesinden bir olay seçilir. 'pinned' bir round'un olayını sabitler (test ve elle takvim).
//   Seçim run seed'i + round'dan türeyen ayrı bir akıştan gelir: boss seçimini, ödül teklifini ve UnityEngine.Random'ı değiştirmez.
// - Aynı anda en çok bir tarla olayı aktiftir; boss'un kendi kuralı (Don, Sert Kabuk, Sis) bundan bağımsız sürer.
// Bütün sayılar ilk prototip değeridir (Tools/Balance/GecOyunV1); nihai denge kararı değildir.
[CreateAssetMenu(menuName = "ClickerGame/Field Event Plan", fileName = "FieldEventPlan")]
public sealed class FieldEventPlanSO : ScriptableObject
{
    [Tooltip("Bu round'dan önce tarla olayı çıkmaz.")]
    [Min(1)] public int firstRound = 26;
    [Tooltip("Round başından olayın hazırlık aşamasına kadar geçen süre (sn, oyun zamanı).")]
    [Min(0f)] public float startDelay = 4f;
    [Tooltip("Boss round'larında seçilebilen olaylar ve ağırlıkları.")]
    public List<FieldEventWeight> bossRounds = new();
    [Tooltip("Boss olmayan round'da olay çıkma olasılığı (0–1).")]
    [Range(0f, 1f)] public float normalRoundChance = 0.5f;
    [Tooltip("Boss olmayan round'larda seçilebilen olaylar ve ağırlıkları (ilk sürüm: yalnız Gezgin Bereket).")]
    public List<FieldEventWeight> normalRounds = new();
    [Tooltip("Sabitlenmiş round'lar: o round'da yalnız bu olay çıkar (zar atılmaz).")]
    public List<FieldEventPin> pinned = new();

    public ThornSiegeConfig thornSiege = new();
    public InvasiveSproutConfig invasiveSprout = new();
    public GraftedShellConfig graftedShell = new();
    public WanderingBountyConfig wanderingBounty = new();

    // Round'un olayı (yoksa false). previous: bir önceki olay; başka seçenek varken aynı olay üst üste gelmez.
    public bool TryPick(int round, bool bossRound, System.Random rng, FieldEventKind? previous, out FieldEventKind kind)
    {
        kind = default;
        if (round < firstRound) return false;
        foreach (FieldEventPin pin in pinned)
            if (pin.round == round) { kind = pin.kind; return true; }
        List<FieldEventWeight> list = bossRound ? bossRounds : normalRounds;
        if (list == null || list.Count == 0) return false;
        // Zar sırası sabittir: önce "çıkar mı", sonra "hangisi" (olay çıkmasa da aynı sayıda zar tüketilir).
        double appear = rng.NextDouble(), which = rng.NextDouble();
        if (!bossRound && appear >= normalRoundChance) return false;
        double total = 0d, totalWithoutPrevious = 0d;
        foreach (FieldEventWeight entry in list)
        {
            if (!(entry.weight > 0f)) continue;
            total += entry.weight;
            if (entry.kind != previous) totalWithoutPrevious += entry.weight;
        }
        if (total <= 0d) return false;
        bool skipPrevious = totalWithoutPrevious > 0d;
        double roll = which * (skipPrevious ? totalWithoutPrevious : total);
        bool found = false;
        foreach (FieldEventWeight entry in list)
        {
            if (!(entry.weight > 0f) || (skipPrevious && entry.kind == previous)) continue;
            kind = entry.kind;
            found = true;
            roll -= entry.weight;
            if (roll < 0d) break;
        }
        return found;
    }

    // Veri denetimi. null: geçerli. Geçersiz veri düzeltilmez: olaylar o run'da çalışmaz ve hata yazılır.
    public static string Validate(FieldEventPlanSO plan)
    {
        if (plan == null) return null;
        if (plan.firstRound < 1) return "ilk round 1'den küçük";
        if (!(plan.startDelay >= 0f) || float.IsInfinity(plan.startDelay)) return "başlangıç gecikmesi geçersiz";
        if (!(plan.normalRoundChance >= 0f && plan.normalRoundChance <= 1f)) return "normal round olasılığı 0–1 dışında";
        foreach (List<FieldEventWeight> list in new[] { plan.bossRounds, plan.normalRounds })
            foreach (FieldEventWeight entry in list)
                if (!(entry.weight >= 0f) || float.IsInfinity(entry.weight)) return $"{entry.kind} ağırlığı geçersiz";
        foreach (FieldEventPin pin in plan.pinned)
            if (pin.round < plan.firstRound) return $"sabitlenmiş Round {pin.round}, ilk olay round'undan (Round {plan.firstRound}) önce";
        return plan.thornSiege.Validate() ?? plan.invasiveSprout.Validate() ?? plan.graftedShell.Validate() ?? plan.wanderingBounty.Validate();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        string error = Validate(this);
        if (error != null) Debug.LogError($"Tarla olayı planı '{name}': geçersiz — {error}. Bu hâliyle olaylar çalışmaz.", this);
    }
#endif
}

public enum FieldEventKind
{
    ThornSiege = 0,
    InvasiveSprout = 1,
    GraftedShell = 2,
    WanderingBounty = 3,
}

[Serializable]
public struct FieldEventWeight
{
    public FieldEventKind kind;
    [Min(0f)] public float weight;
}

[Serializable]
public struct FieldEventPin
{
    [Min(1)] public int round;
    public FieldEventKind kind;
}

// Ortak yaşam döngüsünün süreleri (sn, oyun zamanı): hazırlık → ortaya çıkış → aktif → çözülme. Süreler mantık zamanlayıcısıdır;
// görsel ya da animasyon beklenmez.
[Serializable]
public sealed class FieldEventTiming
{
    [Tooltip("Hazırlık: bölge işaretlidir, etki yoktur.")]
    [Min(0f)] public float prepareSeconds = 5f;
    [Tooltip("Ortaya çıkış: hedefler belirir, etki henüz yoktur.")]
    [Min(0f)] public float emergeSeconds = 1.2f;
    [Tooltip("Aktif: etki sürer; bu süre dolunca olay kendiliğinden çözülür.")]
    [Min(0.1f)] public float activeSeconds = 30f;
    [Tooltip("Çözülme: etki kalkmıştır; yalnız sunum.")]
    [Min(0f)] public float resolveSeconds = 1f;

    public string Validate(string owner)
    {
        foreach (float value in new[] { prepareSeconds, emergeSeconds, activeSeconds, resolveSeconds })
            if (!(value >= 0f) || float.IsInfinity(value)) return owner + " süresi geçersiz";
        return activeSeconds > 0f ? null : owner + " aktif süresi 0";
    }
}

// Diken Kuşatması: işaretli dikdörtgen bölgede üretim durur; iki kök doğrudan vuruşla kırılınca bölge açılır.
[Serializable]
public sealed class ThornSiegeConfig
{
    public FieldEventTiming timing = new() { prepareSeconds = 5f, emergeSeconds = 1.2f, activeSeconds = 30f, resolveSeconds = 1f };
    [Tooltip("Bölgenin boyutu (hücre). Sığmazsa bir küçüğü denenir.")]
    [Range(1, 6)] public int zoneWidth = 3;
    [Range(1, 6)] public int zoneHeight = 3;
    [Tooltip("Bölge, üretim noktalarının en çok bu kadarını kaplayabilir (ve hiçbir zaman tamamını değil).")]
    [Range(0.05f, 0.9f)] public float maxProductionShare = 0.4f;
    [Tooltip("Bir kökü kırmak için gereken DOĞRUDAN saldırı sayısı (saldırı başına bir).")]
    [Range(1, 60)] public int rootDurability = 6;

    public string Validate() => timing.Validate("Diken Kuşatması") ??
        (!(maxProductionShare > 0f && maxProductionShare < 1f) ? "Diken Kuşatması üretim payı 0–1 dışında" : null);
}

// İstilacı Filiz: ana filiz zamanla komşu hücrelere yayılır; yayıldığı hücrelerde üretim yavaşlar. Ana hedef kırılınca hepsi kalkar.
[Serializable]
public sealed class InvasiveSproutConfig
{
    public FieldEventTiming timing = new() { prepareSeconds = 4f, emergeSeconds = 1f, activeSeconds = 35f, resolveSeconds = 1f };
    [Tooltip("Ana filizi kırmak için gereken doğrudan saldırı sayısı.")]
    [Range(1, 60)] public int mainDurability = 8;
    [Tooltip("İki yayılma arası (sn).")]
    [Min(0.1f)] public float spreadInterval = 2.5f;
    [Tooltip("Yayılmanın sürdüğü en uzun süre (sn); sonra yeni parça çıkmaz.")]
    [Min(0f)] public float spreadSeconds = 20f;
    [Tooltip("Ana filiz dahil en çok parça (hücre).")]
    [Range(1, 24)] public int maxPieces = 8;
    [Tooltip("Parçaların ana filize en çok uzaklığı (hücre).")]
    [Range(1, 5)] public int maxRadius = 2;
    [Tooltip("Etkilenen hücrede üretim aralığı çarpanı (1,6: %60 daha yavaş).")]
    [Range(1f, 4f)] public float slowMultiplier = 1.6f;

    public string Validate() => timing.Validate("İstilacı Filiz") ??
        (!(slowMultiplier >= 1f) || float.IsInfinity(slowMultiplier) ? "İstilacı Filiz yavaşlatma çarpanı geçersiz" :
         !(spreadInterval > 0f) ? "İstilacı Filiz yayılma aralığı 0" : null);
}

// Aşılı Kabuk: iki bölge, iki hedef. Kırılan hedefin bölgesi kurtulur, diğeri kapanır; seçim yapılmazsa varsayılan bölge kapanır.
[Serializable]
public sealed class GraftedShellConfig
{
    [Tooltip("activeSeconds burada okunmaz: aktif süre = seçim süresi + kapalı kalma süresi.")]
    public FieldEventTiming timing = new() { prepareSeconds = 6f, emergeSeconds = 1.2f, activeSeconds = 28f, resolveSeconds = 1f };
    [Range(1, 6)] public int zoneWidth = 3;
    [Range(1, 6)] public int zoneHeight = 2;
    [Tooltip("İki bölge birlikte üretim noktalarının en çok bu kadarını kaplayabilir.")]
    [Range(0.05f, 0.9f)] public float maxProductionShare = 0.6f;
    [Tooltip("Bir hedefi kırmak için gereken doğrudan saldırı sayısı.")]
    [Range(1, 60)] public int targetDurability = 4;
    [Tooltip("Seçim süresi (sn): dolunca hazırlıkta gösterilen varsayılan bölge (A) kapanır.")]
    [Min(0.5f)] public float choiceSeconds = 8f;
    [Tooltip("Kapanan bölgenin kapalı kaldığı süre (sn).")]
    [Min(0.5f)] public float closedSeconds = 20f;

    public string Validate() => timing.Validate("Aşılı Kabuk") ??
        (!(choiceSeconds > 0f) || !(closedSeconds > 0f) || float.IsInfinity(choiceSeconds + closedSeconds) ? "Aşılı Kabuk seçim / kapalı süresi geçersiz" :
         !(maxProductionShare > 0f && maxProductionShare < 1f) ? "Aşılı Kabuk üretim payı 0–1 dışında" : null);
}

// Gezgin Bereket: gezen fırsat bölgesi. Bölgedeki gerçek doğrudan hasatlar ilerleme sağlar; eşikte boş üretim noktaları canlanır.
[Serializable]
public sealed class WanderingBountyConfig
{
    public FieldEventTiming timing = new() { prepareSeconds = 3f, emergeSeconds = 0.8f, activeSeconds = 30f, resolveSeconds = 1f };
    [Tooltip("Kare bölgenin kenarı (hücre). Sığmazsa bir küçüğü denenir.")]
    [Range(1, 6)] public int zoneSize = 3;
    [Tooltip("Bölgenin bir hücre kaydığı aralık (sn).")]
    [Min(0.5f)] public float moveInterval = 5f;
    // Legacy serialized values retained for asset compatibility; passive bounty no longer reads these.
    [HideInInspector] [Range(1, 60)] public int threshold = 6;
    [HideInInspector] [Range(1, 12)] public int maxBlooms = 3;

    public string Validate() => timing.Validate("Gezgin Bereket") ?? (!(moveInterval > 0f) ? "Gezgin Bereket hareket aralığı 0" : null);
}
