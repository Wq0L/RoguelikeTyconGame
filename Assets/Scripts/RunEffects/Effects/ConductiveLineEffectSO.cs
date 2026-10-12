using System.Collections.Generic;
using System.Text;
using UnityEngine;

// İletken Hat (Bölüm 3.8, orta aşama; ön koşul: Elektrik Anteni). Ödül alınırken ikinci antenin konumu seçilir; iki antenin kenar
// hücreleri arasındaki doğru üzerindeki hücreler "hat"tır (AntennaSlots.Line: iki uç dahil, tam sayı adımlı doğru).
// - Hat kurulamayan konum seçilemez: hat en az 'minCells' hücre olmalıdır (iki anten aynı köşe hücresine bağlanıyorsa hat tek
//   hücredir: geçersiz). İlk antenin konumu da seçilemez.
// - Her 'interval' saniyede bir hat üzerindeki canlı bitkilere vurur: hasat hasarı stat'ı × 'damage'. Doğrudan vuruş değildir
//   (DamageType.Reward); antenlerin kendi vuruşundan ayrı bir sayaçtır.
// - İkinci anten de bir antendir: 'secondAntennaPulses' açıksa kendi üç hücresine vurur.
// - Tarla büyürse hat, antenlerin yeni kenar hücreleri arasında yeniden çözülür. Antenlerden biri yoksa hat pasiftir.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/İletken Hat", fileName = "ConductiveLineEffect")]
public sealed class ConductiveLineEffectSO : RunEffectSO
{
    [Tooltip("İki hat vuruşu arası (sn, oyun zamanı).")]
    [Min(0.2f)] public float interval = 4f;
    [Tooltip("Hat vuruşunun hasarı: hasat hasarı stat'ına oran.")]
    [Min(0f)] public float damage = 0.45f;
    [Tooltip("Hattın kurulabilmesi için en az hücre sayısı (iki uç dahil).")]
    [Range(2, 11)] public int minCells = 3;
    [Tooltip("İkinci anten kendi üç hücrelik vuruşunu da yapar mı.")]
    public bool secondAntennaPulses = true;

    public override bool NeedsPlacement => true;
    public override string PlacementPrompt => "İKİNCİ ANTENİ YERLEŞTİR\n<size=70%>İki anten arasındaki hücreler hat olur · hat en az " + minCells + " hücre olmalı</size>";
    public override string Summary =>
        $"ikinci anten · iki anten arasındaki hücrelere {Number(interval)} sn'de bir hat vuruşu · hasat hasarının %{Percent(damage)}'i";

    public override RunEffect Create() => new ConductiveLineEffect();

    private static readonly List<int> scratchSlots = new();
    private static readonly List<GridPosition> scratchCells = new();

    public override bool CanTake(RunEffects host)
    {
        CollectPlacements(host, scratchSlots);
        return scratchSlots.Count > 0;
    }

    public override void CollectPlacements(RunEffects host, List<int> into)
    {
        into.Clear();
        AntennaEffect antennas = host.Find<AntennaEffect>();
        if (antennas == null || antennas.Count != 1 || !host.TryBounds(out FieldBounds bounds)) return;
        AntennaSlots.All(bounds, into);
        for (int i = into.Count - 1; i >= 0; i--)
            if (!Forms(bounds, antennas.Slot(0), into[i])) into.RemoveAt(i);
    }

    public override bool IsValidPlacement(RunEffects host, int placement)
    {
        AntennaEffect antennas = host.Find<AntennaEffect>();
        return antennas != null && antennas.Count == 1 && host.TryBounds(out FieldBounds bounds) && Forms(bounds, antennas.Slot(0), placement);
    }

    private bool Forms(FieldBounds bounds, int first, int second)
    {
        if (first == second || !AntennaSlots.IsValid(bounds, first) || !AntennaSlots.IsValid(bounds, second)) return false;
        AntennaSlots.Line(AntennaSlots.Anchor(bounds, first), AntennaSlots.Anchor(bounds, second), scratchCells);
        return scratchCells.Count >= minCells;
    }

    public override Vector3 PlacementPosition(RunEffects host, int placement) =>
        host.TryBounds(out FieldBounds bounds) ? AntennaSlots.World(host.Grid, bounds, placement) : Vector3.zero;

    public override void PlacementCells(RunEffects host, int placement, List<GridPosition> into)
    {
        into.Clear();
        AntennaEffect antennas = host.Find<AntennaEffect>();
        if (antennas == null || antennas.Count != 1 || !host.TryBounds(out FieldBounds bounds)) return;
        AntennaSlots.Line(AntennaSlots.Anchor(bounds, antennas.Slot(0)), AntennaSlots.Anchor(bounds, placement), into);
    }
}

public sealed class ConductiveLineEffect : RunEffect
{
    private readonly List<GridPosition> cells = new();
    private AntennaEffect antennas;
    private int first = -1, second = -1;
    private float timer;
    private ConductiveLineEffectSO Config => (ConductiveLineEffectSO)Data;

    // Kapalı Devre alındıysa: normal hat vuruşunun hasadını ona bildirir (ek vuruş kararı orada).
    public ClosedCircuitEffect Circuit;
    public int Pulses { get; private set; }
    public int ExtraPulses { get; private set; }
    public int LastCellCount { get; private set; }
    public float SecondsUntilPulse => Mathf.Max(0f,Config.interval-timer);
    public void ShowCircuitReturn()
    {
        LineCells(cells);
        if(cells.Count>0)Host.Emit(new RewardCue(RewardCueKind.CircuitReturn,
            FieldCells.World(Host.Grid,cells[cells.Count-1]),FieldCells.World(Host.Grid,cells[0]),cells.Count));
    }

    public override bool OnAdded(int placement)
    {
        antennas = Host.Find<AntennaEffect>();
        if (antennas == null || antennas.Count != 1 || !Config.IsValidPlacement(Host, placement)) return false;
        first = antennas.Slot(0);
        if (!antennas.AddAntenna(placement, Config.secondAntennaPulses)) return false;
        second = placement;
        return true;
    }

    public override void OnRemoved()
    {
        if (antennas != null && second >= 0) antennas.RemoveAntenna(second);
        second = -1;
        Circuit = null;
    }

    public override void OnRoundStarted() => timer = 0f;

    public override void Tick(float deltaTime)
    {
        timer += deltaTime;
        if (timer < Config.interval) return;
        timer = Mathf.Min(timer - Config.interval, Config.interval);
        Pulse(false, 1f);
    }

    // Hattın o anki hücreleri (antenlerden biri yoksa ya da şeridi açık alanda değilse boş).
    public void LineCells(List<GridPosition> into)
    {
        into.Clear();
        if (antennas == null || second < 0 || !antennas.Occupies(first) || !antennas.Occupies(second)) return;
        if (!Host.TryBounds(out FieldBounds bounds) || !AntennaSlots.IsValid(bounds, first) || !AntennaSlots.IsValid(bounds, second)) return;
        AntennaSlots.Line(AntennaSlots.Anchor(bounds, first), AntennaSlots.Anchor(bounds, second), into);
    }

    // extra: Kapalı Devre'nin ek vuruşu (hesaplanmış normal hasarın 'ratio' kadarı). Ek vuruş hiçbir şeyi yeniden başlatmaz.
    public void Pulse(bool extra, float ratio)
    {
        LineCells(cells);
        LastCellCount = cells.Count;
        if (cells.Count == 0) return;
        int damage = Host.StatDamage(Config.damage);
        if (extra) damage = RunEffects.Scale(damage, ratio);
        RewardSource source = extra ? RewardSource.LineExtra : RewardSource.Line;
        PlanterBrain behaviorPlanter = null;
        for (int i = 0; i < cells.Count; i++)
        {
            StrikeResult result = Host.Strike(cells[i], damage, source);
            if(result.Hit)Host.Emit(new RewardCue(extra?RewardCueKind.LineExtraContact:RewardCueKind.LineContact,result.Point));
            // Hat sırasındaki ilk hasadın saksısı, davranışı varsa (Kapalı Devre'nin sorduğu).
            if (!extra && behaviorPlanter == null && result.Killed && RunEffects.BestBehaviorChance(result.Owner) > 0f) behaviorPlanter = result.Owner;
        }
        if (extra) ExtraPulses++; else Pulses++;
        GridSystem grid = Host.Grid;
        Host.Emit(new RewardCue(extra ? RewardCueKind.LineExtraPulse : RewardCueKind.LinePulse,
            FieldCells.World(grid, cells[0]), FieldCells.World(grid, cells[cells.Count - 1]), cells.Count));
        if (!extra && behaviorPlanter != null && Circuit != null) Circuit.OnLineHarvest(behaviorPlanter);
    }

    public override void Describe(StringBuilder text)
    {
        LineCells(cells);
        text.Append("hat ").Append(cells.Count).Append(" hücre · sıradaki vuruş ").Append(Mathf.Max(0f, Config.interval - timer).ToString("0.0")).Append(" sn\n");
        text.Append("vuruş ").Append(Pulses).Append(" (+").Append(ExtraPulses).Append(" ek) · isabet ").Append(Host.Hits(RewardSource.Line))
            .Append(" · hasat ").Append(Host.Kills(RewardSource.Line)).Append('\n');
    }
}
