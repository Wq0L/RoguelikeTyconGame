using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Kritik Ark (Bölüm 3.8, orta aşama). Doğrudan vuruşun KRİTİĞİ, yakındaki başka bir bitkiye düşük hasarlı bir elektrik sıçratır.
// - Kritik zarı eskisi gibi bitki başına atılır (PlayerController); bu etki zarı değiştirmez, yalnız sonucunu okur.
// - Saldırı bitince, kritik vuruşlar vuruş sırasıyla gezilir; her biri için hedef: kritik yiyen bitkinin hücresine en yakın
//   (≤ 'rangeCells' hücre), bu saldırının doğrudan vurmadığı ve bu saldırıda henüz ark almamış canlı bitki. Eşit uzaklıkta tarama
//   sırası belirler (rastgele değil). Hedef yoksa o kritik ark üretmez.
// - Hasar: o kritik vuruşun HESAPLANMIŞ hasarının 'damageRatio' oranı (tek yuvarlama; çarpanlar yeniden uygulanmaz).
// - Saldırı başına en çok 'maxArcsPerAttack' ark. Ark kritik atmaz, yeni ark doğurmaz, doğrudan vuruş değildir (DamageType.Reward).
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Kritik Ark", fileName = "CritArcEffect")]
public sealed class CritArcEffectSO : RunEffectSO
{
    [Tooltip("Arkın hasarı: kritik vuruşun hesaplanmış hasarına oran.")]
    [Range(0f, 1f)] public float damageRatio = 0.3f;
    [Tooltip("Hedef arama uzaklığı (hücre).")]
    [Range(1, 4)] public int rangeCells = 2;
    [Tooltip("Bir saldırıda en çok ark.")]
    [Range(1, 8)] public int maxArcsPerAttack = 3;

    public override string Summary =>
        $"kritik vuruş, {rangeCells} hücre içindeki başka bir bitkiye ark sıçratır · kritik hasarın %{Percent(damageRatio)}'i · vuruş başına en çok {maxArcsPerAttack}";

    public override RunEffect Create() => new CritArcEffect();

    // Kritik şansı 0 ise ark hiç doğmaz: ödül sunulmaz ve alınamaz.
    public override bool CanTake(RunEffects host) =>
        StatManager.Instance != null && StatManager.Instance.GetFinalStat(StatType.CritChance, StatTarget.Player) > 0f;
}

public sealed class CritArcEffect : RunEffect
{
    private readonly HashSet<long> used = new();
    private CritArcEffectSO Config => (CritArcEffectSO)Data;

    public int Arcs { get; private set; }
    public int NoTarget { get; private set; }

    public override void AttackEnd(DirectAttackReport attack)
    {
        IReadOnlyList<DirectHit> hits = attack.Hits;
        GridSystem grid = Host.Grid;
        if (grid == null) return;
        int arcs = 0;
        used.Clear();
        for (int i = 0; i < hits.Count && arcs < Config.maxArcsPerAttack; i++)
        {
            DirectHit hit = hits[i];
            if (!hit.Crit) continue;
            GridPosition from = hit.Cell.GetGridPosition();
            if (FieldCells.Nearest(grid, from, Config.rangeCells, attack, used, out GridPosition target) == null) { NoTarget++; continue; }
            used.Add(FieldCells.Key(target));
            StrikeResult result = Host.Strike(target, RunEffects.Scale(hit.Damage, Config.damageRatio), RewardSource.CritArc);
            if (!result.Hit) continue;
            arcs++;
            Arcs++;
            Host.Emit(new RewardCue(RewardCueKind.CritArc, hit.Point, result.Point));
        }
    }

    public override void Describe(StringBuilder text) =>
        text.Append("ark ").Append(Arcs).Append(" · hedefsiz kritik ").Append(NoTarget).Append(" · hasat ").Append(Host.Kills(RewardSource.CritArc)).Append('\n');
}
