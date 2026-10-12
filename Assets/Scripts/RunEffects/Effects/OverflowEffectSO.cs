using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Taşan Hasar (Bölüm 3.8, orta aşama). Doğrudan vuruşun bitkiyi öldürdükten sonra artan hasarının bir kısmı yakındaki bitkiye geçer.
// - Artan hasar gerçek kalan candan hesaplanır: bitkiye uygulanan hasar − vuruştan önceki canı (PlantHealth.Overkill).
// - Aktarılan = artan hasar × 'ratio' (aktarım başına kayıp: 1 − ratio), tek yuvarlama. 1'in altına düşerse aktarım biter.
// - Hedef: ölen bitkinin hücresine en yakın (≤ 'rangeCells' hücre) canlı bitki; eşit uzaklıkta tarama sırası belirler.
// - Aktarılan hasar hedefi de öldürürse, onun artan hasarı aynı kuralla bir kez daha aktarılır: bir kaynaktan en çok
//   'maxTransfers' aktarım. Bir saldırıda en çok 'maxSourcesPerAttack' doğrudan hasat aktarım başlatır.
// - Aktarım doğrudan vuruş değildir (DamageType.Reward): kritik atmaz, doğrudan hasat saymaz; ölüm ve ödül eski yoldan, bir kez.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Taşan Hasar", fileName = "OverflowEffect")]
public sealed class OverflowEffectSO : RunEffectSO
{
    [Tooltip("Aktarılan pay (0,5: artan hasarın yarısı geçer, yarısı kaybolur).")]
    [Range(0f, 1f)] public float ratio = 0.5f;
    [Tooltip("Bir kaynaktan en çok aktarım (zincirleme).")]
    [Range(1, 6)] public int maxTransfers = 2;
    [Tooltip("Hedef arama uzaklığı (hücre).")]
    [Range(1, 3)] public int rangeCells = 1;
    [Tooltip("Bir saldırıda aktarım başlatan en çok doğrudan hasat.")]
    [Range(1, 12)] public int maxSourcesPerAttack = 4;

    public override string Summary =>
        $"öldüren vuruşun artan hasarının %{Percent(ratio)}'i yandaki bitkiye geçer · en çok {maxTransfers} aktarım · her aktarımda %{Percent(1f - ratio)} kayıp";

    public override RunEffect Create() => new OverflowEffect();
}

public sealed class OverflowEffect : RunEffect
{
    private OverflowEffectSO Config => (OverflowEffectSO)Data;

    public int Sources { get; private set; }
    public int Transfers { get; private set; }
    public long Carried { get; private set; }

    public override void AttackEnd(DirectAttackReport attack)
    {
        GridSystem grid = Host.Grid;
        if (grid == null) return;
        IReadOnlyList<DirectHit> hits = attack.Hits;
        int sources = 0;
        for (int i = 0; i < hits.Count && sources < Config.maxSourcesPerAttack; i++)
        {
            if (!hits[i].Killed || hits[i].Overkill <= 0) continue;
            sources++;
            Sources++;
            Transfer(grid, hits[i].Cell.GetGridPosition(), hits[i].Overkill, hits[i].Point);
        }
    }

    private void Transfer(GridSystem grid, GridPosition from, int overkill, Vector3 fromPoint)
    {
        // Sonlu: en çok maxTransfers adım; her adımda hasar oranla küçülür.
        for (int hop = 0; hop < Config.maxTransfers; hop++)
        {
            int damage = RunEffects.Scale(overkill, Config.ratio, 0);
            if (damage < 1) return;
            if (FieldCells.Nearest(grid, from, Config.rangeCells, null, null, out GridPosition target) == null) return;
            StrikeResult result = Host.Strike(target, damage, RewardSource.Overflow);
            if (!result.Hit) return;
            Transfers++;
            Carried += damage;
            Host.Emit(new RewardCue(RewardCueKind.Overflow, fromPoint, result.Point, damage, hop));
            if (!result.Killed || result.Overkill <= 0) return;
            from = target;
            fromPoint = result.Point;
            overkill = result.Overkill;
        }
    }

    public override void Describe(StringBuilder text) =>
        text.Append("kaynak ").Append(Sources).Append(" · aktarım ").Append(Transfers).Append(" · taşınan hasar ").Append(Carried).Append(" · hasat ")
            .Append(Host.Kills(RewardSource.Overflow)).Append('\n');
}
