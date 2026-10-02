using System;
using System.Collections.Generic;
using UnityEngine;

// Profil bazlı denge verisi (Bölüm 3.5). Run profili bir denge seti gösterirse oyun aynı mantıkla, bu setteki veriyle çalışır;
// göstermezse (eski profiller) ortak asset'ler aynen kullanılır. Oyun kodu kopyalanmaz: yalnız "hangi veri" sorusu buradan cevaplanır.
// Boş bırakılan her alan ortak veriyi kullanır, yani set yalnız değiştirdiği şeyi taşır.
[CreateAssetMenu(menuName = "ClickerGame/Run Balance", fileName = "RunBalance")]
public sealed class RunBalanceSO : ScriptableObject
{
    [Tooltip("Temel statlar (hasar, saldırı aralığı, yarıçap, kritik…). Boş: sahnedeki StatManager'ın CoreStat'ı.")]
    public CoreStatsSO coreStats;
    [Tooltip("Bitki can eğrisi ve nadirlik can çarpanları. Boş: Resources/PlantHealthScaling.")]
    public PlantHealthScalingSO plantHealth;
    [Tooltip("Level başına XP gereksinimi. Boş: sahnedeki ProgressionManager'ın verisi.")]
    public ProgressionSO progression;
    [Tooltip("Skill tree düğümleri. Boş: sahnedeki SkillTreeManager listesi.")]
    public SkillTreeSetSO skillTree;

    [Header("Bitki ödülleri")]
    [Tooltip("Bitkinin XP'si × bu çarpan (Common, Uncommon, Rare, Epic, Legendary). Eksik ya da boş: ×1.")]
    public float[] rarityXpMultipliers = { 1f, 1f, 1f, 1f, 1f };

    [Header("Saksı taban statları")]
    [Tooltip("Bütün saksıların taban stat'ı (ör. PlantSpawnRate = üretim aralığı, sn). Listede olmayan stat saksının kendi asset değerini kullanır.")]
    public List<StatEntry> planterBaseStats = new();

    [Header("Erken davranış erişimi (Bölüm 3.6)")]
    [Tooltip("Run başında açık gelen kilitler (ör. patlama ve elektrik kartları). Yalnız bu kilidi açan ağaç düğümü bu run'da satılmaz " +
             "ve ön koşul olarak karşılanmış sayılır. Kart yine normal level seçiminden alınır; tile bedava verilmez.")]
    public List<UnlockType> startingUnlocks = new();
    [Tooltip("Dolu ise: bu türlerden bir kart alınana kadar, tarlada saksı varken, her kart seçiminde üç adaydan biri (rastgele bir slot) " +
             "bu türlerden gelir. Alınmazsa zorlanmaz; sonraki seçimde yine sunulur. Biri alınınca normal dağılıma dönülür.")]
    public List<TileModifierType> firstBehaviorOffer = new();

    [Header("Saksı fiyatları")]
    [Tooltip("Listede olmayan saksı kendi asset fiyatını kullanır.")]
    public List<PlanterPrice> planterPrices = new();

    [Serializable]
    public struct PlanterPrice
    {
        public PlanterSO planter;
        public ResourceType costType;
        [Min(0)] public int cost;
    }

    // Aktif run profilinin denge seti (yoksa null). Profil sahne ömrü boyunca değişmez; RoundManager da aynı seçimi okur.
    public static RunBalanceSO Active
    {
        get
        {
            RunProfileSO profile = RoundManager.Instance != null ? RoundManager.Instance.Profile : RunProfileSelectionSO.Active;
            return profile != null ? profile.balance : null;
        }
    }

    public float XpMultiplier(PlantRarity rarity)
    {
        int index = (int)rarity;
        return rarityXpMultipliers != null && index >= 0 && index < rarityXpMultipliers.Length && rarityXpMultipliers[index] > 0f
            ? rarityXpMultipliers[index] : 1f;
    }

    public static float ActiveXpMultiplier(PlantRarity rarity)
    {
        RunBalanceSO balance = Active;
        return balance != null ? balance.XpMultiplier(rarity) : 1f;
    }

    public static bool IsStartingUnlock(UnlockType type)
    {
        RunBalanceSO balance = Active;
        return type != UnlockType.None && balance != null && balance.startingUnlocks != null && balance.startingUnlocks.Contains(type);
    }

    public static bool IsFirstBehaviorType(TileModifierType type)
    {
        RunBalanceSO balance = Active;
        return balance != null && balance.firstBehaviorOffer != null && balance.firstBehaviorOffer.Contains(type);
    }

    // Saksı taban stat'ı: denge setinde varsa o değer (bütün saksılar için), yoksa false.
    public static bool TryPlanterBaseStat(StatType statType, out float value)
    {
        value = 0f;
        RunBalanceSO balance = Active;
        if (balance == null || balance.planterBaseStats == null) return false;
        foreach (StatEntry entry in balance.planterBaseStats)
            if (entry != null && entry.statType == statType) { value = entry.value; return true; }
        return false;
    }

    // Saksının bu run'daki fiyatı: denge setinde varsa oradan, yoksa asset'ten.
    public static void PriceOf(PlanterSO planter, out ResourceType costType, out int cost)
    {
        costType = planter != null ? planter.costType : ResourceType.Gold;
        cost = planter != null ? planter.cost : 0;
        RunBalanceSO balance = Active;
        if (balance == null || planter == null || balance.planterPrices == null) return;
        foreach (PlanterPrice price in balance.planterPrices)
            if (price.planter == planter) { costType = price.costType; cost = price.cost; return; }
    }
}
