using System.Text;
using UnityEngine;

// Açgözlü Hasat (Bölüm 3.8, orta aşama). Tek bir doğrudan saldırıda en az 'minHarvests' GERÇEK doğrudan hasat, bir sonraki
// doğrudan saldırıyı güçlendirir.
// - Hak, güçlendirilmemiş bir saldırının kendi vuruşlarıyla yaptığı hasatlardan doğar (davranış, artçı, ödül etkisi hasatları
//   sayılmaz). Güçlendirilmiş saldırı yeni hak doğurmaz: en iyi durumda her iki saldırıdan biri güçlenir.
// - Hak hazırken sıradaki saldırı:
//     · en az iki canlı bitkiye değiyorsa doğrudan hasarı × 'damageMultiplier' olur ve hak harcanır (sonuç ne olursa olsun);
//     · tam bir canlı bitkiye değiyorsa ("tek hedefli vuruş") güçlenmez ve hak KAYBOLUR;
//     · hiçbir canlı bitkiye değmiyorsa (boş savuruş) hak durur.
// - Çarpan yalnız o saldırının doğrudan hasarına, diğer doğrudan katsayılarla aynı çarpımda bir kez girer; davranışlara geçmez,
//   genel stat'ı değiştirmez. Hasat Ritmi'nden bağımsızdır (ikisi aynı saldırıda çarpılabilir).
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Açgözlü Hasat", fileName = "GreedyHarvestEffect")]
public sealed class GreedyHarvestEffectSO : RunEffectSO
{
    [Tooltip("Hak için tek saldırıda gereken doğrudan hasat.")]
    [Range(2, 12)] public int minHarvests = 3;
    [Tooltip("Güçlendirilmiş saldırının doğrudan hasar çarpanı.")]
    [Min(1f)] public float damageMultiplier = 1.5f;

    public override string Summary =>
        $"tek vuruşta {minHarvests}+ hasat: sıradaki vuruşun hasarı ×{Number(damageMultiplier)} · tek hedefe vurursan hak kaybolur";

    public override RunEffect Create() => new GreedyHarvestEffect();
}

public sealed class GreedyHarvestEffect : RunEffect
{
    private bool spending;
    private GreedyHarvestEffectSO Config => (GreedyHarvestEffectSO)Data;

    public bool Ready { get; private set; }
    public int Charges { get; private set; }
    public int Spent { get; private set; }
    public int Lost { get; private set; }

    public override void AttackBegin(DirectAttackReport attack, ref float multiplier)
    {
        spending = false;
        if (!Ready) return;
        int living = attack.LivingTargets;
        if (living >= 2)
        {
            multiplier *= Config.damageMultiplier;
            spending = true;
        }
        else if (living == 1)
        {
            Ready = false;
            Lost++;
            Host.Emit(new RewardCue(RewardCueKind.GreedLost, attack.Center));
        }
    }

    public override void AttackEnd(DirectAttackReport attack)
    {
        if (spending)
        {
            spending = false;
            Ready = false;
            Spent++;
            Host.Emit(new RewardCue(RewardCueKind.GreedSpent, attack.Center));
            return;   // güçlendirilmiş saldırı yeni hak doğurmaz
        }
        if (Ready || attack.DirectHarvests < Config.minHarvests) return;
        Ready = true;
        Charges++;
        Host.Emit(new RewardCue(RewardCueKind.GreedReady, attack.Center));
    }

    public override void OnRemoved() { Ready = false; spending = false; }

    public override void Describe(StringBuilder text) =>
        text.Append(Ready ? "HAK HAZIR" : "hak yok").Append(" · kazanılan ").Append(Charges).Append(" · harcanan ").Append(Spent).Append(" · kaybolan ").Append(Lost).Append('\n');
}
