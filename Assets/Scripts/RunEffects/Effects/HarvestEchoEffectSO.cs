using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Hasat Yankısı (Bölüm 3.8, orta aşama). Güçlü bir doğrudan hasattan sonra, hasat edilen hücrelerin simetriğine düşük hasarlı
// vuruş gelir.
// - "Güçlü doğrudan hasat" = tek bir oyuncu saldırısında, o saldırının vuruşlarıyla en az 'minHarvests' bitkinin hasat edilmesi
//   (davranış, artçı ve ödül etkisi hasatları sayılmaz).
// - "Simetri merkezi" = açık alanın ortası. Hasat edilen her hücre, merkeze göre nokta yansımasıyla eşlenir:
//   (x, z) → (minX + maxX − x, minZ + maxZ − z). Açık alan saldırı anındaki sınırlardır.
// - İlk 'maxCells' hasadın yansıması kullanılır (vuruş sırasıyla). Yansıma, bu saldırının doğrudan vurduğu bir hücreyse ya da
//   bu saldırıda zaten seçildiyse atlanır.
// - 'delay' sonra yansıma hücresine vurur: hasat hasarı stat'ı × 'damage'. "Uygun hücre" vuruş anında değerlendirilir: açık
//   olmalı ve canlı bitki taşımalıdır; değilse vuruş boşa gider (bitki referansı saklanmaz).
// - Yankı doğrudan vuruş değildir (DamageType.Reward): güçlü hasat saymaz, yeni yankı doğurmaz.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Hasat Yankısı", fileName = "HarvestEchoEffect")]
public sealed class HarvestEchoEffectSO : RunEffectSO
{
    [Tooltip("Güçlü hasat: tek saldırıda en az bu kadar doğrudan hasat.")]
    [Range(2, 12)] public int minHarvests = 3;
    [Tooltip("Yankı hasarı: hasat hasarı stat'ına oran.")]
    [Min(0f)] public float damage = 0.5f;
    [Tooltip("Bir saldırıda en çok yankı hücresi.")]
    [Range(1, 8)] public int maxCells = 3;
    [Tooltip("Yankının gecikmesi (sn).")]
    [Min(0f)] public float delay = 0.12f;

    public override string Summary =>
        $"tek vuruşta {minHarvests}+ hasat: hasat edilen hücrelerin tarlanın ortasına göre simetriğine vuruş (en çok {maxCells}) · hasat hasarının %{Percent(damage)}'i";

    public override RunEffect Create() => new HarvestEchoEffect();
}

public sealed class HarvestEchoEffect : RunEffect
{
    private readonly HashSet<long> used = new();
    private HarvestEchoEffectSO Config => (HarvestEchoEffectSO)Data;

    public int StrongHarvests { get; private set; }
    public int Scheduled { get; private set; }
    public int Empty { get; private set; }

    public override void AttackEnd(DirectAttackReport attack)
    {
        if (attack.DirectHarvests < Config.minHarvests) return;
        GridSystem grid = Host.Grid;
        if (grid == null || !Host.TryBounds(out FieldBounds bounds)) return;
        StrongHarvests++;
        int damage = Host.StatDamage(Config.damage), count = 0;
        used.Clear();
        IReadOnlyList<DirectHit> hits = attack.Hits;
        for (int i = 0; i < hits.Count && count < Config.maxCells; i++)
        {
            if (!hits[i].Killed) continue;
            GridPosition mirror = bounds.Mirror(hits[i].Cell.GetGridPosition());
            if (!FieldCells.IsOpen(grid, mirror) || attack.Covers(grid.GetGridObject(mirror)) || !used.Add(FieldCells.Key(mirror))) continue;
            if (!Host.Schedule(this, Config.delay, 0, mirror.x, mirror.z, damage)) break;
            count++;
            Scheduled++;
        }
    }

    public override void OnTimer(int kind, int a, int b, int c)
    {
        var cell = new GridPosition(a, b);
        StrikeResult result = Host.Strike(cell, c, RewardSource.HarvestEcho);
        if (!result.Hit) { Empty++; return; }
        Host.Emit(new RewardCue(RewardCueKind.HarvestEcho, result.Point));
    }

    public override void Describe(StringBuilder text) =>
        text.Append("güçlü hasat ").Append(StrongHarvests).Append(" · yankı ").Append(Scheduled).Append(" (boşa giden ").Append(Empty).Append(") · hasat ")
            .Append(Host.Kills(RewardSource.HarvestEcho)).Append('\n');
}
