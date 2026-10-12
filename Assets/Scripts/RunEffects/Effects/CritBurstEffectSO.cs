using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Kritik Patlama (Bölüm 3.8, orta aşama). Tek bir doğrudan saldırıda en az 'minCrits' FARKLI bitkiye kritik vurulursa saldırının
// merkezinde ek bir alan vuruşu olur.
// - Kritik zarı eskisi gibi bitki başına atılır; tek saldırılık ortak zara çevrilmez. Bir saldırı bir bitkiye bir kez vurduğu için
//   kritik sayısı farklı bitki sayısıdır.
// - Alan: saldırının merkezi, o saldırının gerçek temas yarıçapı × 'radiusFactor' (hedefleme oyuncu saldırısıyla aynı temas
//   kuralı: daire hücrenin yüzeyine değiyorsa hücre alandadır).
// - Hasar: hasat hasarı stat'ı × kritik çarpanı stat'ı × 'damage'. Doğrudan vuruş çarpanları (Keskin Bıçak, tırpan, uzmanlaşma,
//   Altın Hedef, Hasat Ritmi) ve saksı bonusu uygulanmaz; patlama kritik atmaz.
// - Saldırı başına en çok bir patlama. Doğrudan vuruş değildir (DamageType.Reward): yeni kritik saymaz, kendini tetiklemez.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Kritik Patlama", fileName = "CritBurstEffect")]
public sealed class CritBurstEffectSO : RunEffectSO
{
    [Tooltip("Tek saldırıda kritik yemesi gereken farklı bitki sayısı.")]
    [Range(2, 12)] public int minCrits = 3;
    [Tooltip("Patlama hasarı: hasat hasarı × kritik çarpanı stat'larına oran.")]
    [Min(0f)] public float damage = 0.5f;
    [Tooltip("Alan: saldırının temas yarıçapına oran.")]
    [Range(0.25f, 2f)] public float radiusFactor = 1f;

    public override string Summary =>
        $"tek vuruşta {minCrits}+ bitkiye kritik: merkezde ek alan vuruşu · hasat hasarı × kritik çarpanının %{Percent(damage)}'i";

    public override RunEffect Create() => new CritBurstEffect();

    // Kritik şansı 0 ise patlama hiç doğmaz: ödül sunulmaz ve alınamaz.
    public override bool CanTake(RunEffects host) =>
        StatManager.Instance != null && StatManager.Instance.GetFinalStat(StatType.CritChance, StatTarget.Player) > 0f;
}

public sealed class CritBurstEffect : RunEffect
{
    private readonly List<GridObject> area = new();
    private CritBurstEffectSO Config => (CritBurstEffectSO)Data;

    public int Bursts { get; private set; }

    public override void AttackEnd(DirectAttackReport attack)
    {
        if (attack.Crits < Config.minCrits) return;
        GridSystem grid = Host.Grid;
        StatManager stats = StatManager.Instance;
        if (grid == null || stats == null) return;
        float critMultiplier = stats.GetFinalStat(StatType.CritMultiplier, StatTarget.Player);
        int damage = Host.StatDamage(Config.damage * critMultiplier);
        grid.GetGridObjectsInRadius(attack.Center, attack.Radius * Config.radiusFactor, area);
        for (int i = 0; i < area.Count; i++) Host.Strike(area[i].GetGridPosition(), damage, RewardSource.CritBurst);
        Bursts++;
        Host.Emit(new RewardCue(RewardCueKind.CritBurst, attack.Center, area.Count));
    }

    public override void Describe(StringBuilder text) =>
        text.Append("patlama ").Append(Bursts).Append(" · isabet ").Append(Host.Hits(RewardSource.CritBurst)).Append(" · hasat ").Append(Host.Kills(RewardSource.CritBurst)).Append('\n');
}
