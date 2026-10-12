using System.Text;
using System.Collections.Generic;
using UnityEngine;

// Full-game cursor route: charge per actually damaged direct target; choose one of four
// cardinal grid directions independently of reward RNG. Snapshot route to the open edge.
// Legacy profiles retain attack-count charging and the original four-cell centreward route.
// Damage, growth, return timing and bounded scheduling remain on the shared reward path.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Hilal Darbesi", fileName = "CrescentEffect")]
public sealed class CrescentEffectSO : RunEffectSO
{
    [Tooltip("Tam sürüm: kaç doğrudan bitki isabetinde bir hilal çıkar. Eski profil: saldırı sayısı.")]
    [Min(1)] public int everyAttacks = 6;
    [Tooltip("Hilalin ilerlediği hücre sayısı.")]
    [Range(1, 10)] public int steps = 4;
    [Tooltip("İki adım arası (sn, oyun zamanı).")]
    [Min(0.01f)] public float stepSeconds = 0.07f;
    [Tooltip("Adım hasarı: hasat hasarı stat'ına oran.")]
    [Min(0f)] public float damage = 0.8f;

    public override string Summary =>
        RoundManager.Instance!=null&&RoundManager.Instance.Profile!=null&&RoundManager.Instance.Profile.crescentFullRows
        ? $"her {everyAttacks} doğrudan bitki isabetinde fareden hilal · rastgele dört yönden birinde açık alan kenarına gider · hasat hasarının %{Percent(damage)}'i"
        : $"her {everyAttacks} geçerli vuruşta bir hilal · {steps} hücre ilerler · hasat hasarının %{Percent(damage)}'i";

    public override RunEffect Create() => new CrescentEffect();
}

public sealed partial class CrescentEffect : RunEffect
{
    public const int MaxWaves = 4;
    // Dolunay'ın saldırı duraklaması kaynağı ('DOLU').
    public const int PauseSource = 0x444F4C55;
    private const int StepTimer = 0;

    private struct Wave
    {
        public bool active, isReturn, fullMoonDone;
        public int x, z, dx, dz, step, steps, growth, halfWidth, damage;
        public bool fullRow, cursorStart;
        public Vector3 birth;
        public FieldBounds bounds;
        public int serial;
        public double legStart;
        public float legSeconds;
    }

    private readonly Wave[] waves = new Wave[MaxWaves];
    private CrescentEffectSO Config => (CrescentEffectSO)Data;
    private uint routeRandom;
    private int nextSerial;
    private readonly HashSet<PlantHealth>[] struck = {new(128),new(128),new(128),new(128)};
    public bool FullRows {get;private set;}
    public override bool OnAdded(int placement)
    {
        FullRows=RoundManager.Instance!=null&&RoundManager.Instance.Profile!=null&&RoundManager.Instance.Profile.crescentFullRows;
        SeedRoute();return true;
    }
    void SeedRoute() => routeRandom=ChainRandom.Seed(SegmentEventDirector.Mix(SegmentEventDirector.Instance!=null?SegmentEventDirector.Instance.RunSeed:0,
        RoundManager.Instance!=null?RoundManager.Instance.CurrentRound:0,0x4D4F4F4E));
    public override void OnRoundStarted()=>SeedRoute();

    // Yükseltme ödülleri kurar ve kaldırır (GrowingMoonEffect, FullMoonEffect). null: o yükseltme yok.
    public GrowingMoonEffectSO Growth;
    public FullMoonEffectSO FullMoon;

    public int Counter { get; private set; }
    public int Launched { get; private set; }
    public int Skipped { get; private set; }
    public int Steps { get; private set; }
    public int GrowthGains { get; private set; }
    public int GrowthResets { get; private set; }
    public int FullMoons { get; private set; }
    public int ActiveWaves { get { int n = 0; for (int i = 0; i < MaxWaves; i++) if (waves[i].active) n++; return n; } }

    public override void OnRemoved()
    {
        for (int i = 0; i < MaxWaves; i++) {waves[i].active = false;struck[i].Clear();}
        DirectAttackPause.Release(PauseSource);
        Growth = null;
        FullMoon = null;
    }

    public override void OnRoundEnded()
    {
        for (int i = 0; i < MaxWaves; i++) {waves[i].active = false;struck[i].Clear();}
        DirectAttackPause.Release(PauseSource);
    }

    public override void AttackEnd(DirectAttackReport attack)
    {
        if (!FullRows)
        {
            if (!attack.TouchedLivingPlant) return;
            if (++Counter < Config.everyAttacks) return;
            Counter = 0;Launch(attack.Center);return;
        }
        // Report contains direct hits only, after damage. A critical is still one plant hit.
        for(int i=0;i<attack.Hits.Count;i++)
        {
            var hit=attack.Hits[i];if(!hit.Damaged)continue;
            bool duplicate=false;
            for(int j=0;j<i;j++)if(attack.Hits[j].Damaged&&attack.Hits[j].Plant==hit.Plant&&attack.Hits[j].Life==hit.Life){duplicate=true;break;}
            if(!duplicate)Counter++;
        }
        int threshold=Mathf.Max(1,Config.everyAttacks);
        while(Counter>=threshold){Counter-=threshold;Launch(attack.Center);}
    }

    private int FreeWave()
    {
        for (int i = 0; i < MaxWaves; i++) if (!waves[i].active) return i;
        return -1;
    }

    private void Launch(Vector3 center)
    {
        int index = FreeWave();
        GridSystem grid = Host.Grid;
        if (index < 0 || grid == null || !Host.TryBounds(out FieldBounds bounds)) { Skipped++; return; }
        GridPosition origin = bounds.Clamp(grid.GetGridPosition(center));
        // Açık alanın ortasına doğru, farkın büyük olduğu eksen (iki katı alınmış fark: yarım hücre kesri oluşmaz).
        int towardX = bounds.MinX + bounds.MaxX - 2 * origin.x, towardZ = bounds.MinZ + bounds.MaxZ - 2 * origin.z;
        int dx = 0, dz = 0;
        if (Mathf.Abs(towardX) >= Mathf.Abs(towardZ)) dx = towardX < 0 ? -1 : 1;
        else dz = towardZ < 0 ? -1 : 1;
        int length=Config.steps;
        if(FullRows)
        {
            int direction=Mathf.Min(3,(int)(ChainRandom.Value(ref routeRandom)*4));
            dx=direction==0?1:direction==1?-1:0;dz=direction==2?1:direction==3?-1:0;
            length=1+(dx>0?bounds.MaxX-origin.x:dx<0?origin.x-bounds.MinX:dz>0?bounds.MaxZ-origin.z:origin.z-bounds.MinZ);
            // Virtual previous cell makes the first timed hit include the cursor cell.
            origin=new GridPosition(origin.x-dx,origin.z-dz);
        }
        struck[index].Clear();
        waves[index] = new Wave { active = true, x = origin.x, z = origin.z, dx = dx, dz = dz, steps = length, damage = Host.StatDamage(Config.damage),
            fullRow=FullRows,cursorStart=FullRows,birth=center,bounds=bounds,serial=++nextSerial,legStart=Host.Clock,legSeconds=Config.stepSeconds };
        if (!Host.Schedule(this, Config.stepSeconds, StepTimer, index)) { waves[index].active = false; Skipped++; return; }
        Launched++;
        Vector3 from = FullRows?center:FieldCells.World(grid, origin);
        Host.Emit(new RewardCue(RewardCueKind.CrescentLaunch, from, FieldCells.World(grid,new GridPosition(origin.x+dx*length,origin.z+dz*length)), length));
    }

    public override void OnTimer(int kind, int a, int b, int c)
    {
        if (kind == StepTimer && a >= 0 && a < MaxWaves) Step(a);
    }

    private void Step(int index)
    {
        Wave wave = waves[index];
        if (!wave.active || !Host.TryBounds(out FieldBounds bounds)) { waves[index].active = false;struck[index].Clear(); return; }
        if(wave.fullRow)bounds=wave.bounds;
        wave.step++;
        int cx = wave.x + wave.dx * wave.step, cz = wave.z + wave.dz * wave.step;
        // Tarlanın kenarı: dalga burada biter (kilitli ve tarla dışı hücreye vurulmaz).
        if (!bounds.Contains(cx, cz)) { waves[index].active = false;struck[index].Clear(); return; }

        int half = wave.isReturn ? wave.halfWidth : Growth != null ? Mathf.Min(wave.growth, Growth.maxHalfWidth) : 0;
        int damage = wave.isReturn || Growth == null || wave.growth == 0
            ? wave.damage
            : RunEffects.Scale(wave.damage, 1f + Growth.damagePerGrowth * wave.growth);
        RewardSource source = wave.isReturn ? RewardSource.CrescentReturn : RewardSource.Crescent;
        int harvests = 0;
        // Cephe, ilerleme yönüne dik: (dx, dz) için dik yön (dz, dx).
        for (int offset = -half; offset <= half; offset++)
        {
            var cell=new GridPosition(cx+wave.dz*offset,cz+wave.dx*offset);
            if(wave.fullRow&&!bounds.Contains(cell))continue;
            var plant=FieldCells.LivingPlant(Host.Grid,cell);
            if(wave.fullRow&&plant!=null&&!struck[index].Add(plant))continue;
            var result=Host.Strike(cell,damage,source);
            if(result.Killed)harvests++;
            if(result.Hit)Host.Emit(new RewardCue(wave.isReturn?RewardCueKind.MoonReturnContact:RewardCueKind.MoonContact,result.Point));
        }
        Steps++;
        GridSystem grid = Host.Grid;
        Vector3 at = FieldCells.World(grid, new GridPosition(cx, cz));
        Host.Emit(new RewardCue(RewardCueKind.CrescentStep, at, at + new Vector3(wave.dz, 0f, wave.dx) * (FieldCells.CellSize * half), half));

        // Büyüyen Ay: bu adımın GERÇEK hasadı dalganın kalan adımlarını büyütür; hasatsız adım ("başarısız ilerleme") büyümeyi sıfırlar.
        if (!wave.isReturn && Growth != null)
        {
            if (harvests > 0)
            {
                if (wave.growth < Growth.maxGrowth) { wave.growth++; GrowthGains++; Host.Emit(new RewardCue(RewardCueKind.CrescentGrow, at, wave.growth)); }
                // Dolunay: büyüme eşiği bu dalgada ilk kez aşıldı.
                if (FullMoon != null && !wave.fullMoonDone && wave.growth >= FullMoon.growthThreshold)
                {
                    wave.fullMoonDone = true;
                    TriggerFullMoon(wave, bounds, at);
                }
            }
            else if (wave.growth > 0)
            {
                wave.growth = 0;
                GrowthResets++;
                Host.Emit(new RewardCue(RewardCueKind.CrescentReset, at));
            }
        }

        if (wave.step >= wave.steps || !Host.Schedule(this, Config.stepSeconds, StepTimer, index)) wave.active = false;
        wave.legStart=Host.Clock;wave.legSeconds=Config.stepSeconds;
        if(!wave.active)struck[index].Clear();
        waves[index] = wave;
    }

    // Karşı yönden ikinci dalga: ilk dalganın varacağı son hücreden başlayıp başlangıç hücresine doğru geri gelir. Büyümez, yeni
    // Dolunay başlatamaz. Bedel: kısa, üst sınırı olan doğrudan saldırı duraklaması (DirectAttackPause).
    private void TriggerFullMoon(Wave forward, FieldBounds bounds, Vector3 at)
    {
        int index = FreeWave();
        if (index < 0) { Skipped++; return; }
        int last = 0;
        for (int step = 1; step <= forward.steps; step++)
        {
            if (!bounds.Contains(forward.x + forward.dx * step, forward.z + forward.dz * step)) break;
            last = step;
        }
        if (last == 0) return;
        waves[index] = new Wave
        {
            active = true, isReturn = true,
            // Bir hücre ötesinden başlar: ilk adımı ilk dalganın son hücresine düşer; son adımı başlangıç hücresidir.
            x = forward.x + forward.dx * (last + 1), z = forward.z + forward.dz * (last + 1), dx = -forward.dx, dz = -forward.dz,
            steps = forward.fullRow?last:last + 1, halfWidth = Mathf.Min(forward.growth, Growth != null ? Growth.maxHalfWidth : 0),
            damage = RunEffects.Scale(forward.damage, FullMoon.returnDamage),
            fullRow=forward.fullRow,bounds=forward.bounds,serial=++nextSerial,
            legStart=Host.Clock,legSeconds=FullMoon.returnDelay,
        };
        struck[index].Clear();
        if (!Host.Schedule(this, FullMoon.returnDelay, StepTimer, index)) { waves[index].active = false; Skipped++; return; }
        FullMoons++;
        DirectAttackPause.Request(PauseSource, FullMoon.pauseSeconds);
        Host.Emit(new RewardCue(RewardCueKind.FullMoon, at, forward.fullRow?last:last + 1));
    }

    public override void Describe(StringBuilder text)
    {
        text.Append("sayaç ").Append(Counter).Append('/').Append(Config.everyAttacks).Append(" · dalga ").Append(Launched).Append(" (aktif ").Append(ActiveWaves)
            .Append(", atlanan ").Append(Skipped).Append(") · adım ").Append(Steps).Append('\n');
        if (FullRows && nextSerial > 0)
        {
            int latest=0;for(int i=1;i<MaxWaves;i++)if(waves[i].serial>waves[latest].serial)latest=i;
            var route=waves[latest];
            text.Append("son rota (grid): (").Append(route.x+route.dx).Append(",").Append(route.z+route.dz)
                .Append(") → (").Append(route.x+route.dx*route.steps).Append(",").Append(route.z+route.dz*route.steps).Append(")").Append(route.isReturn?" · dönüş":" · ileri").Append('\n');
        }
        if (Growth != null) text.Append("büyüme +").Append(GrowthGains).Append(" · sıfırlanma ").Append(GrowthResets).Append('\n');
        if (FullMoon != null) text.Append("dolunay ").Append(FullMoons).Append(" · saldırı duraklaması ").Append(DirectAttackPause.RemainingFor(PauseSource).ToString("0.00")).Append(" sn\n");
        text.Append("isabet ").Append(Host.Hits(RewardSource.Crescent) + Host.Hits(RewardSource.CrescentReturn)).Append(" · hasat ")
            .Append(Host.Kills(RewardSource.Crescent) + Host.Kills(RewardSource.CrescentReturn)).Append('\n');
    }
}
