using UnityEngine;

// Composition boundary for run effects. Gameplay consumers do not need to know each source.
// Local planter/resonance modifiers remain local; rounding happens once at the existing call site.
public static class RunPower
{
    private static readonly HarvestResourceScale resources = new();
    public static HarvestResourceScale Resources => resources;

    // Hasat Ritmi (Bölüm 3.6): sayaç round'lar arasında korunur, yeni run'da Reset ile sıfırlanır.
    private static readonly HarvestRhythm rhythm = new();
    public static HarvestRhythm Rhythm => rhythm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        resources.Clear();
        rhythm.Clear();
    }

    // attackMultiplier: bu saldırıya özel çarpan (Hasat Ritmi güçlendirmesi). Diğer katsayılarla aynı çarpımda, tek yuvarlamadan önce.
    public static float DirectDamage(float baseDamage, float variance, PlantRarity? rarity = null, float attackMultiplier = 1f) =>
        baseDamage * variance * SpecializationManager.DirectMultiplier * StartLoadoutManager.DirectDamageMultiplier *
        BossRewardManager.DirectDamageMultiplier * (rarity.HasValue ? BossRewardManager.RareDirectMultiplier(rarity.Value) : 1f) * attackMultiplier;

    public static float Behavior(float resonance) => resonance * SpecializationManager.BehaviorMultiplier * BossRewardManager.BehaviorDamageMultiplier;

    public static int ScaleHarvestResource(ResourceType type, int amount) => resources.Apply(type, amount,
        SpecializationManager.HarvestResourceMultiplier * StartLoadoutManager.HarvestResourceMultiplier);

    // Level başına kart seçim hakkı (Bölüm 3.7.4): temel hak (profil) + run'daki aktif değişimler (alınan boss ödülleri).
    // Hak, level kazanıldığı anda bu değerle bekleyen sayaca eklenir; daha önce kazanılmış haklar yeniden hesaplanmaz.
    // Geçerli aralık 1–5. Aralığın dışına çıkaracak ödül kırpılmaz: sunulmaz ve alınamaz (BossRewardManager).
    public const int MinLevelChoices = 1, MaxLevelChoices = 5;
    public static int LevelChoices(int baseChoices) => baseChoices + BossRewardManager.LevelChoiceDelta;
    public static bool ValidLevelChoices(int choices) => choices >= MinLevelChoices && choices <= MaxLevelChoices;

    // Davranışın gecikmiş ikinci darbesi (Artçı Patlama, Çifte Akım) var mı; ayarı ödül verisinden gelir.
    public static bool TryGetEcho(DamageType type, out BehaviorEcho echo) => BossRewardManager.TryGetEcho(type, out echo);
}

// Gecikmiş ikinci darbenin ayarı: gecikme (sn), ilk darbenin hesaplanmış hasarına oran, ilk darbenin yarıçapına oran (yalnız patlama),
// çapraz erişim (yalnız elektrik; hücre, 0: ilk dalgayla aynı).
public readonly struct BehaviorEcho
{
    public readonly float Delay, Damage, Radius;
    public readonly int Reach;
    public BehaviorEcho(float delay, float damage, float radius, int reach = 0) { Delay = delay; Damage = damage; Radius = radius; Reach = reach; }
}

public readonly struct HarvestRhythmConfig
{
    public readonly int Harvests;
    public readonly float Damage, Radius;
    public HarvestRhythmConfig(int harvests, float damage, float radius) { Harvests = harvests; Damage = damage; Radius = radius; }
}

// Tek bir oyuncu saldırısının bağlamı. Genel statlara dokunmaz; davranışlar bu bağlamı görmez.
public readonly struct DirectAttack
{
    public readonly float DamageMultiplier, RadiusMultiplier;
    public readonly bool Empowered;
    public DirectAttack(float damage, float radius, bool empowered) { DamageMultiplier = damage; RadiusMultiplier = radius; Empowered = empowered; }
    public static readonly DirectAttack Normal = new DirectAttack(1f, 1f, false);
}

// Hasat Ritmi durumu. Ayar (eşik, çarpanlar) ödül alınmışsa BossRewardManager'dan gelir; alınmamışsa hiçbir şey saymaz.
// Kurallar: yalnız NORMAL saldırıların doğrudan hasatları sayılır (davranış ve güçlendirilmiş saldırı hasatları sayılmaz);
// eşik dolunca tek bir hak hazırlanır ve sayaç sıfırlanır (aynı vuruştaki fazlası yeni hak biriktirmez); hak, canlı bitkiye
// değen bir sonraki saldırıda harcanır (boş savuruş harcamaz); otomatik ek saldırı üretmez.
public sealed class HarvestRhythm
{
    public int Count { get; private set; }
    public bool Ready { get; private set; }
    // Ölçüm: hazırlanan hak ve harcanan (güçlendirilmiş) saldırı sayısı.
    public int Charges { get; private set; }
    public int EmpoweredAttacks { get; private set; }
    public int Version { get; private set; }

    public bool Enabled => BossRewardManager.TryGetRhythm(out _);
    public int Threshold => BossRewardManager.TryGetRhythm(out HarvestRhythmConfig config) ? config.Harvests : 0;

    // Sıradaki saldırının bağlamı. Hak burada harcanmaz; Complete harcar.
    public DirectAttack Next() =>
        Ready && BossRewardManager.TryGetRhythm(out HarvestRhythmConfig config) ? new DirectAttack(config.Damage, config.Radius, true) : DirectAttack.Normal;

    public void Complete(DirectAttack attack, int directHarvests, bool touchedLivingPlant)
    {
        if (!BossRewardManager.TryGetRhythm(out HarvestRhythmConfig config)) return;
        if (attack.Empowered)
        {
            if (!touchedLivingPlant) return;
            Ready = false;
            EmpoweredAttacks++;
            Version++;
            return;
        }
        if (Ready || directHarvests <= 0) return;
        Count += directHarvests;
        if (Count >= config.Harvests)
        {
            Ready = true;
            Count = 0;
            Charges++;
        }
        Version++;
    }

    public void Clear()
    {
        Count = 0;
        Ready = false;
        Charges = EmpoweredAttacks = 0;
        Version++;
    }
}
