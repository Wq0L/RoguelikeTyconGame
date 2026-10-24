#if UNITY_EDITOR
using System;
using UnityEngine;

// Yalnız Editor (Mechanics Test Lab): sayısal iz için dar GÖZLEM noktaları. Bu dosyanın tamamı oyuncu build'ine girmez.
// Oynanış bunlara bağlı değildir: hesap burada yapılmaz, taşınmaz ve değiştirilmez; hesabı yapan kod, kullandığı gerçek ara
// değerleri (taban, sapma, çarpan, yuvarlanmış sonuç) olduğu gibi bildirir. Hiçbiri rastgele sayı tüketmez.
// - Dinleyici yokken çağıran yer hiçbir şey kurmaz (Listening false): kapalı kayıtta maliyet bir bool okumadır.
// - Dinleyicinin hatası hasarı, ödülü ya da skoru yarıda bırakmaz (ObserverEvents: yakalanır, bir kez raporlanır, sayılır).
public static class EditorNumericTrace
{
    // Tek bir doğrudan vuruşun gerçek hesabı (PlayerController.AttackInRadius).
    public readonly struct DirectHit
    {
        public readonly PlantHealth Target;
        // Hasat hasarı stat'ı ve bu vuruşun sapması (0,85–1,15).
        public readonly float BaseDamage, Variance;
        // Bu saldırıya özel çarpanlar: Hasat Ritmi ve ödül etkilerinin saldırı çarpanı (Açgözlü Hasat).
        public readonly float RhythmMultiplier, FeedMultiplier;
        // Tek yuvarlamadan çıkan hasar (kritikten önce), kritik çarpanı, kritikten sonraki hasar, saksı bonusundan sonra uygulanan.
        public readonly int Damage, FinalDamage, Applied;
        public readonly bool Crit;
        public readonly float CritChance, CritMultiplier;

        public DirectHit(PlantHealth target, float baseDamage, float variance, float rhythm, float feed, int damage, bool crit, float critChance,
            float critMultiplier, int finalDamage, int applied)
        {
            Target = target; BaseDamage = baseDamage; Variance = variance; RhythmMultiplier = rhythm; FeedMultiplier = feed; Damage = damage;
            Crit = crit; CritChance = critChance; CritMultiplier = critMultiplier; FinalDamage = finalDamage; Applied = applied;
        }
    }

    // Bir davranışın (patlama, elektrik, kasırga, bumerang) vuruş başına hasarının hesaplandığı an: normal tetik, zincir ve ikinci
    // dalga aynı yerden bildirir. Precomputed: hasar çağırana hesaplanmış geldi (katsayılar burada yeniden uygulanmadı).
    public readonly struct BehaviorDamage
    {
        public readonly PlanterBrain Source;
        public readonly DamageType Type;
        public readonly HarvestLink Link;
        // Davranışın tabanı (hasat hasarı stat'ı, int), davranışın kendi katsayısı (kasırga, bumerang; diğerlerinde 1),
        // zincir neslinin çarpanı (normal tetikte 1) ve vuruş başına hesaplanmış hasar.
        public readonly int BaseDamage, Damage;
        public readonly float TypeCoefficient, ChainFactor;
        public readonly bool Precomputed;

        public BehaviorDamage(PlanterBrain source, DamageType type, HarvestLink link, int baseDamage, float typeCoefficient, float chainFactor,
            bool precomputed, int damage)
        {
            Source = source; Type = type; Link = link; BaseDamage = baseDamage; TypeCoefficient = typeCoefficient; ChainFactor = chainFactor;
            Precomputed = precomputed; Damage = damage;
        }
    }

    // Bir hasadın kaynak ve XP ödülünün gerçek hesabı (PlantResource.GiveReward).
    public readonly struct HarvestReward
    {
        public readonly PlantSO Plant;
        public readonly PlanterBrain Planter;
        public readonly GridPosition Cell;
        public readonly float ResourceMultiplier, XpMultiplier, RarityXpMultiplier;
        // Kaynak: çarpandan sonra yuvarlanan, çift ödülden sonra, uzmanlaşma ölçeğinden sonra ve (bereket dahil) bankaya eklenen.
        public readonly int Rounded, AfterDuplicate, AfterSpecialization, Final;
        public readonly bool Duplicated, Bounty;
        public readonly float DuplicateChance;
        // XP: yuvarlanan ve (bereket dahil) eklenen.
        public readonly double XpRounded, XpFinal;

        public HarvestReward(PlantSO plant, PlanterBrain planter, GridPosition cell, float resourceMultiplier, int rounded, float duplicateChance,
            bool duplicated, int afterDuplicate, int afterSpecialization, bool bounty, int final, float xpMultiplier, float rarityXpMultiplier,
            double xpRounded, double xpFinal)
        {
            Plant = plant; Planter = planter; Cell = cell; ResourceMultiplier = resourceMultiplier; Rounded = rounded;
            DuplicateChance = duplicateChance; Duplicated = duplicated; AfterDuplicate = afterDuplicate; AfterSpecialization = afterSpecialization;
            Bounty = bounty; Final = final; XpMultiplier = xpMultiplier; RarityXpMultiplier = rarityXpMultiplier; XpRounded = xpRounded; XpFinal = xpFinal;
        }
    }

    // Bir hasadın Harvest Score'unun gerçek hesabı (HarvestScoreManager.AddScore).
    public readonly struct HarvestScore
    {
        public readonly PlantRarity Rarity;
        public readonly float PlayerMultiplier, PlanterMultiplier;
        // Çarpanlardan sonra yuvarlanan ödül ve başlangıç seçiminin ölçeğinden sonra toplam skora eklenen.
        public readonly int Award, Final;
        public readonly long TotalAfter;

        public HarvestScore(PlantRarity rarity, float playerMultiplier, float planterMultiplier, int award, int final, long totalAfter)
        { Rarity = rarity; PlayerMultiplier = playerMultiplier; PlanterMultiplier = planterMultiplier; Award = award; Final = final; TotalAfter = totalAfter; }
    }

    private static Action<DirectHit>[] directObservers = Array.Empty<Action<DirectHit>>();
    private static Action<BehaviorDamage>[] behaviorObservers = Array.Empty<Action<BehaviorDamage>>();
    private static Action<HarvestReward>[] rewardObservers = Array.Empty<Action<HarvestReward>>();
    private static Action<HarvestScore>[] scoreObservers = Array.Empty<Action<HarvestScore>>();

    public static bool Listening { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetObservers()
    {
        directObservers = Array.Empty<Action<DirectHit>>();
        behaviorObservers = Array.Empty<Action<BehaviorDamage>>();
        rewardObservers = Array.Empty<Action<HarvestReward>>();
        scoreObservers = Array.Empty<Action<HarvestScore>>();
        Listening = false;
    }

    private static void Refresh() =>
        Listening = directObservers.Length + behaviorObservers.Length + rewardObservers.Length + scoreObservers.Length > 0;

    public static event Action<DirectHit> DirectHitObserved
    {
        add { ObserverEvents.Add(ref directObservers, value); Refresh(); }
        remove { ObserverEvents.Remove(ref directObservers, value); Refresh(); }
    }

    public static event Action<BehaviorDamage> BehaviorDamageObserved
    {
        add { ObserverEvents.Add(ref behaviorObservers, value); Refresh(); }
        remove { ObserverEvents.Remove(ref behaviorObservers, value); Refresh(); }
    }

    public static event Action<HarvestReward> HarvestRewardObserved
    {
        add { ObserverEvents.Add(ref rewardObservers, value); Refresh(); }
        remove { ObserverEvents.Remove(ref rewardObservers, value); Refresh(); }
    }

    public static event Action<HarvestScore> HarvestScoreObserved
    {
        add { ObserverEvents.Add(ref scoreObservers, value); Refresh(); }
        remove { ObserverEvents.Remove(ref scoreObservers, value); Refresh(); }
    }

    public static void Report(in DirectHit hit) { if (directObservers.Length > 0) ObserverEvents.Raise(directObservers, hit); }
    public static void Report(in BehaviorDamage damage) { if (behaviorObservers.Length > 0) ObserverEvents.Raise(behaviorObservers, damage); }
    public static void Report(in HarvestReward reward) { if (rewardObservers.Length > 0) ObserverEvents.Raise(rewardObservers, reward); }
    public static void Report(in HarvestScore score) { if (scoreObservers.Length > 0) ObserverEvents.Raise(scoreObservers, score); }
}
#endif
