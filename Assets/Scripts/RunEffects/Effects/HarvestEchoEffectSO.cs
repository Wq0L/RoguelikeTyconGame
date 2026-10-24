using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Hasat Yankısı (Bölüm 3.8, orta aşama; 23 Ekim 2026 meteor düzeni). Doğrudan hasattan sonra, kesilen hücrelerin simetriğine
// TEK bir meteor düşer.
// - Tetik: tek bir oyuncu saldırısında en az 'minHarvests' doğrudan hasat (davranış, artçı ve ödül hasatları sayılmaz) ve
//   bekleme dolmuş olmalı.
// - "Simetri merkezi" = açık alanın ortası. Kesilen her hücre, merkeze göre nokta yansımasıyla eşlenir:
//   (x, z) → (minX + maxX − x, minZ + maxZ − z). Açık alan saldırı anındaki sınırlardır.
// - İlk 'maxCells' hasadın yansıması (vuruş sırasıyla) meteorun alanıdır. Yansıma, bu saldırının doğrudan vurduğu bir hücreyse
//   ya da zaten seçildiyse atlanır. Alan boş kalırsa meteor yoktur ve bekleme başlamaz.
// - Meteor 'delay' sn sonra alandaki her hücreye aynı anda vurur: hasat hasarı stat'ı × 'damage'. "Uygun hücre" vuruş anında
//   değerlendirilir: açık olmalı ve canlı bitki taşımalıdır; değilse o hücre boşa gider (bitki referansı saklanmaz).
// - Meteor planlanınca 'cooldown' sn (round saati; duraklatmada durur) yeni meteor düşmez. Her round hazır başlar.
// - Yankı doğrudan vuruş değildir (DamageType.Reward): hasat saymaz, yeni yankı doğurmaz.
// Sunum: planlanınca HarvestEchoPrepared (kaynak ağırlık merkezi → alan merkezi, Value = hücre, Radius = alan yarıçapı);
// vuruş sonrası tek HarvestEcho (alan merkezi, Value = isabet eden hücre, Radius = alan yarıçapı).
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Hasat Yankısı", fileName = "HarvestEchoEffect")]
public sealed class HarvestEchoEffectSO : RunEffectSO
{
    [Tooltip("Tetik: tek saldırıda en az bu kadar doğrudan hasat.")]
    [Range(1, 12)] public int minHarvests = 1;
    [Tooltip("Yankı hasarı: hasat hasarı stat'ına oran (hücre başına).")]
    [Min(0f)] public float damage = 0.5f;
    [Tooltip("Meteorun en çok vurduğu hücre.")]
    [Range(1, 9)] public int maxCells = 6;
    [Tooltip("Meteorun düşüş süresi = vuruşun gecikmesi (sn).")]
    [Min(0f)] public float delay = 0.5f;
    [Tooltip("Meteor planlandıktan sonra yeni meteor için bekleme (sn, round saati).")]
    [Min(0f)] public float cooldown = 6f;

    public override string Summary =>
        $"tek vuruşta {minHarvests}+ hasat: kesilen hücrelerin tarlanın ortasına göre simetriğine meteor (en çok {maxCells} hücre) · hasat hasarının %{Percent(damage)}'i · bekleme {Number(cooldown)} sn";

    public override RunEffect Create() => new HarvestEchoEffect();
}

public sealed class HarvestEchoEffect : RunEffect
{
    private readonly HashSet<long> used = new();
    private readonly List<GridPosition> area = new(9);
    private HarvestEchoEffectSO Config => (HarvestEchoEffectSO)Data;
    private bool pending;
    private double readyAt;
    private Vector3 target;
    private float radius;

    public int Meteors { get; private set; }
    public int Scheduled { get; private set; }
    public int Empty { get; private set; }
    public int CooldownBlocks { get; private set; }

    public override void OnRoundStarted() { readyAt = 0d; Cancel(); }
    public override void OnRoundEnded() => Cancel();
    public override void OnRemoved() => Cancel();

    public override void AttackEnd(DirectAttackReport attack)
    {
        if (attack.DirectHarvests < Config.minHarvests) return;
        if (pending || Host.Clock < readyAt) { CooldownBlocks++; return; }
        GridSystem grid = Host.Grid;
        if (grid == null || !Host.TryBounds(out FieldBounds bounds)) return;
        used.Clear();
        area.Clear();
        Vector3 source = Vector3.zero, center = Vector3.zero;
        IReadOnlyList<DirectHit> hits = attack.Hits;
        for (int i = 0; i < hits.Count && area.Count < Config.maxCells; i++)
        {
            if (!hits[i].Killed) continue;
            GridPosition mirror = bounds.Mirror(hits[i].Cell.GetGridPosition());
            if (!FieldCells.IsOpen(grid, mirror) || attack.Covers(grid.GetGridObject(mirror)) || !used.Add(FieldCells.Key(mirror))) continue;
            area.Add(mirror);
            source += hits[i].Point;
            center += FieldCells.World(grid, mirror);
        }
        if (area.Count == 0) return;
        if (!Host.Schedule(this, Config.delay, 0, Host.StatDamage(Config.damage))) { area.Clear(); return; }
        pending = true;
        readyAt = Host.Clock + Config.cooldown;
        Meteors++;
        Scheduled += area.Count;
        source /= area.Count;
        target = center / area.Count;
        radius = AreaRadius(grid);
        Host.Emit(new RewardCue(RewardCueKind.HarvestEchoPrepared, source, target, area.Count, radius: radius));
    }

    // a = hücre başına hasar. Alan planlama anında saklanır; uygunluk burada, vuruş anında değerlendirilir.
    public override void OnTimer(int kind, int a, int b, int c)
    {
        if (!pending) return;
        pending = false;
        int struck = 0;
        for (int i = 0; i < area.Count; i++)
        {
            if (Host.Strike(area[i], a, RewardSource.HarvestEcho).Hit) struck++;
            else Empty++;
        }
        area.Clear();
        Host.Emit(new RewardCue(RewardCueKind.HarvestEcho, target, struck, radius));
    }

    // Alan merkezinden en uzak hücre merkezine uzaklık + yarım hücre: halka ve meteor boyu bunu okur.
    private float AreaRadius(GridSystem grid)
    {
        float half = .5f * Vector3.Distance(FieldCells.World(grid, new GridPosition(0, 0)), FieldCells.World(grid, new GridPosition(1, 0)));
        float far = 0f;
        for (int i = 0; i < area.Count; i++) far = Mathf.Max(far, Vector3.Distance(FieldCells.World(grid, area[i]), target));
        return far + half;
    }

    private void Cancel()
    {
        pending = false;
        area.Clear();
    }

    public override void Describe(StringBuilder text) =>
        text.Append("meteor ").Append(Meteors).Append(" · hücre ").Append(Scheduled).Append(" (boşa giden ").Append(Empty).Append(") · beklemede reddedilen ")
            .Append(CooldownBlocks).Append(" · hasat ").Append(Host.Kills(RewardSource.HarvestEcho)).Append('\n');
}
