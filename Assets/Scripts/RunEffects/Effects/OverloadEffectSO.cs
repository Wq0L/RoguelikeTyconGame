using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Aşırı Yükleme (Bölüm 3.8, güçlü aşama). Bir saksının saldırı davranışı (patlama, kasırga, bumerang, elektrik) NORMAL yoldan
// tetiklendiğinde, aynı saksı + davranış için daha zayıf bir ek proc hakkı doğar.
// - Tetik: doğrudan hasadın normal davranış tetiği (şansı tutmuş ve gerçekten başlamış; AttackFeed.BehaviorTriggered). Zincirden,
//   artçıdan ve ek proc'un kendisinden doğan davranışlar hak doğurmaz. Tile'ın pasif stat'ları (hasar, üretim, nadirlik …) birer
//   saldırı değildir ve "tetiklenmiş" sayılmaz.
// - Aynı kök saldırıda (tek bir oyuncu saldırısı) en çok 'maxPerAttack' ek proc; aynı saksı + davranış çifti 'cooldown' dolmadan
//   yeniden hak doğurmaz (bekleme, hak doğduğu anda başlar).
// - Ek proc 'delay' sonra, davranışın MEVCUT yolundan çalışır: hasarı o saksının normal davranış hasarı × 'damageFactor'. Normal
//   davranış hasarı tile / rezonans / ödül katsayılarını bir kez içerir; ek proc onları ikinci kez uygulamaz, yeni şans zarı atmaz.
// - Ek proc zincirin dışındadır (HarvestLink.Excluded): öldürdüğü bitki Zincir Hasat'ı başlatmaz, zincirin "aynı saksı + davranış
//   bir kökte bir kez" kaydına dokunmaz ve onu atlatmaz. Artçı Patlama / Çifte Akım da doğurmaz.
// - Kasırga ve bumerang ek proc'u aktif sınıra (12) tabidir: sınır doluysa ek proc düşer ve sayılır; sınır aşılmaz, beklemez.
// - Çalışma anında saksı yerinde değilse (satıldıysa) ek proc düşer (güvenli pasif).
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Aşırı Yükleme", fileName = "OverloadEffect")]
public sealed class OverloadEffectSO : RunEffectSO
{
    [Tooltip("Ek proc'un hasarı: saksının normal davranış hasarına oran.")]
    [Range(0f, 1f)] public float damageFactor = 0.5f;
    [Tooltip("Ek proc'un gecikmesi (sn).")]
    [Min(0f)] public float delay = 0.3f;
    [Tooltip("Aynı saksı + davranış için bekleme süresi (sn, oyun zamanı).")]
    [Min(0f)] public float cooldown = 2.5f;
    [Tooltip("Bir oyuncu saldırısında en çok ek proc.")]
    [Range(1, 8)] public int maxPerAttack = 3;

    public override string Summary =>
        $"tetiklenen saksı davranışı {Number(delay)} sn sonra bir kez daha çalışır · hasarın %{Percent(damageFactor)}'i · saksı + davranış başına {Number(cooldown)} sn bekleme";

    public override RunEffect Create() => new OverloadEffect();
}

public sealed class OverloadEffect : RunEffect
{
    private struct Job
    {
        public PlanterBrain planter;
        public DamageType type;
        public GridObject cell;
        public Vector3 position;
        public bool used;
    }

    private const int JobCapacity = 16;
    private readonly Job[] jobs = new Job[JobCapacity];
    private readonly Dictionary<long, double> readyAt = new();
    private int attackSerial = -1, attackCount;
    private OverloadEffectSO Config => (OverloadEffectSO)Data;

    public int Granted { get; private set; }
    public int Fired { get; private set; }
    public int CooldownBlocks { get; private set; }
    public int BudgetBlocks { get; private set; }
    public int Dropped { get; private set; }

    private static long Key(PlanterBrain planter, DamageType type) => ((long)planter.GetInstanceID() << 8) | (uint)type;

    public override void BehaviorTriggered(PlanterBrain planter, DamageType type, GridObject cell, Vector3 position)
    {
        if (planter == null || type < DamageType.Explosion || type > DamageType.Electric) return;
        if (AttackFeed.Serial != attackSerial) { attackSerial = AttackFeed.Serial; attackCount = 0; }
        if (attackCount >= Config.maxPerAttack) { BudgetBlocks++; return; }
        long key = Key(planter, type);
        if (readyAt.TryGetValue(key, out double ready) && Host.Clock < ready) { CooldownBlocks++; return; }
        int slot = -1;
        for (int i = 0; i < JobCapacity && slot < 0; i++) if (!jobs[i].used) slot = i;
        if (slot < 0 || !Host.Schedule(this, Config.delay, 0, slot)) { Dropped++; return; }
        jobs[slot] = new Job { planter = planter, type = type, cell = cell, position = position, used = true };
        readyAt[key] = Host.Clock + Config.cooldown;
        attackCount++;
        Granted++;
    }

    public override void OnTimer(int kind, int a, int b, int c)
    {
        if (a < 0 || a >= JobCapacity || !jobs[a].used) return;
        Job job = jobs[a];
        jobs[a] = default;
        if (Execute(job)) { Fired++; Host.Emit(new RewardCue(RewardCueKind.Overload, job.position, (int)job.type)); }
        else Dropped++;
    }

    // Davranışın mevcut yolu, zincir dışı bağlamla ve hasar çarpanıyla. Kaynak saksı hâlâ yerinde ve hücrenin sahibi olmalı.
    private bool Execute(Job job)
    {
        PlanterBrain planter = job.planter;
        if (planter == null || !planter.IsPlaced || job.cell == null || job.cell.GetPlanterBrain() != planter) return false;
        HarvestLink link = HarvestLink.Excluded(RewardSource.Overload);
        float factor = Config.damageFactor;
        switch (job.type)
        {
            case DamageType.Explosion:
                planter.Explode(job.position, link, factor);
                return true;
            case DamageType.Electric:
                // Hesaplanmış hasarla, zincir dışı dalga (ikinci dalganın yolu): katsayılar yeniden uygulanmaz.
                HarvestBehaviorManager electric = HarvestBehaviorManager.Instance;
                return electric != null && electric.TryElectricEcho(planter,
                    HarvestChain.Scale(planter.GetBehaviorDamage(PlanterBrain.HarvestDamage, DamageType.Electric), factor), 0, out _, out _, out _);
            case DamageType.Tornado:
                TornadoManager tornadoes = TornadoManager.Instance;
                return tornadoes != null && tornadoes.HasCapacity && tornadoes.TrySpawn(job.cell, PlanterBrain.HarvestDamage, link, factor, Host.Fork());
            default:
                HarvestBehaviorManager boomerangs = HarvestBehaviorManager.Instance;
                return boomerangs != null && boomerangs.HasBoomerangCapacity &&
                    boomerangs.TryBoomerang(planter, job.cell, PlanterBrain.HarvestDamage, link, factor, Host.Fork());
        }
    }

    public override void OnRoundStarted()
    {
        // Bekleme süreleri round saatine göredir: yeni round'da saat sıfırlandığı için kayıt da sıfırlanır.
        readyAt.Clear();
        ClearJobs();
    }

    public override void OnRoundEnded() => ClearJobs();
    public override void OnRemoved() { readyAt.Clear(); ClearJobs(); }

    private void ClearJobs()
    {
        for (int i = 0; i < JobCapacity; i++) jobs[i] = default;
    }

    public override void Describe(StringBuilder text)
    {
        int waiting = 0;
        foreach (KeyValuePair<long, double> entry in readyAt) if (entry.Value > Host.Clock) waiting++;
        text.Append("hak ").Append(Granted).Append(" · çalışan ").Append(Fired).Append(" · düşen ").Append(Dropped).Append(" · beklemede reddedilen ").Append(CooldownBlocks)
            .Append(" · saldırı bütçesinde reddedilen ").Append(BudgetBlocks).Append(" · beklemedeki çift ").Append(waiting).Append('\n');
    }
}
