using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Progression")]
public class ProgressionSO : ScriptableObject
{
    [Header("XP Eğrisi")]
    public float baseXP = 100f;
    public float xpMultiplier = 1.5f;

    [Header("Dengelenmiş seviye maliyetleri")]
    [Tooltip("Açıkken her eleman ilgili seviyeden bir sonrakine gereken XP'dir. Liste bittikten sonra son maliyet kullanılır.")]
    public bool useAuthoredRequirements;
    public List<float> xpRequirements = new();

    [Header("Tablo sonrası (Bölüm 3.7.7)")]
    [Tooltip("0: liste bittikten sonra son maliyet tekrar eder (eski profiller). >0: maliyet son tablo değerinden kesintisiz, doğrusal " +
             "büyür: C(L) = C(N) × [1 + s × (L − N)], L > N. N listenin son maliyetinin ait olduğu seviye, s bu değer. " +
             "Tasarımsal level sınırı yoktur; sayı türlerinin sınırı ayrıdır (ProgressionManager raporlar).")]
    [Min(0f)] public float tailGrowth;

    // Bölüm 3.7.6.1: NaN / sonsuz maliyet ve ≤ 0 formül tabanı geçersizdir (level döngüsü ilerlemez). Geçerliyse null.
    // Elle yazılmış listedeki ≤ 0 değer eskisi gibi 1 sayılır (GetXPForLevel); yalnız sayı olmayan değer reddedilir.
    public string Validate()
    {
        if (float.IsNaN(tailGrowth) || float.IsInfinity(tailGrowth) || tailGrowth < 0f) return $"tailGrowth {tailGrowth}";
        if (useAuthoredRequirements && xpRequirements != null && xpRequirements.Count > 0)
        {
            for (int i = 0; i < xpRequirements.Count; i++)
                if (float.IsNaN(xpRequirements[i]) || float.IsInfinity(xpRequirements[i])) return $"{i + 1}. maliyet {xpRequirements[i]}";
            return null;
        }
        if (!(baseXP > 0f) || float.IsInfinity(baseXP)) return $"baseXP {baseXP}";
        if (!(xpMultiplier > 0f) || float.IsInfinity(xpMultiplier)) return $"xpMultiplier {xpMultiplier}";
        return null;
    }

    private bool Authored => useAuthoredRequirements && xpRequirements != null && xpRequirements.Count > 0;
    // Tablonun son maliyetinin ait olduğu seviye (N) ve o maliyet (C(N)).
    public int TableLevels => Authored ? xpRequirements.Count : 0;
    private float LastCost => Mathf.Max(1f, xpRequirements[xpRequirements.Count - 1]);
    public bool HasGrowingTail => Authored && tailGrowth > 0f;

    // Bu level'dan itibaren maliyet sabit mi: elle yazılmış listenin sonundan sonra son maliyet tekrar eder (büyüyen kuyruk yoksa).
    public bool CostIsConstantFrom(int level) => Authored && !HasGrowingTail && level >= xpRequirements.Count;

    public float GetXPForLevel(int level)
    {
        level = Mathf.Max(1, level);
        if (Authored)
        {
            int n = xpRequirements.Count;
            if (level > n && tailGrowth > 0f) return (float)(LastCost * (1.0 + (double)tailGrowth * (level - n)));
            return Mathf.Max(1f, xpRequirements[Mathf.Min(level - 1, n - 1)]);
        }
        return baseXP * Mathf.Pow(xpMultiplier, level - 1);
    }

    // Tablo bittikten sonra (level ≥ N) saklı XP'nin kaç level'a yettiğinin kapalı hesabı; tablo içinde ya da formül eğrisinde −1
    // (bilinmiyor). Sabit maliyette XP ÷ maliyet; büyüyen kuyrukta maliyetler aritmetik dizi olduğundan ikinci derece denklemin kökü.
    // Level işleme bunu yalnız "iş level sayacına sığar mı" sorusu için kullanır; level'lar yine tek tek işlenir.
    public double LevelsAffordable(int level, double xp)
    {
        if (!Authored || level < xpRequirements.Count || !(xp > 0d)) return -1d;
        double cost = GetXPForLevel(level);
        if (!HasGrowingTail) return xp / cost;
        double step = (double)LastCost * tailGrowth, b = cost - step / 2d;
        return (System.Math.Sqrt(b * b + 2d * step * xp) - b) / step;
    }
}
