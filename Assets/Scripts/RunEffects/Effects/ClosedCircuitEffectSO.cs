using System.Text;
using UnityEngine;

// Kapalı Devre (Bölüm 3.8, güçlü aşama; ön koşul: İletken Hat). "Hattın hasatlarıyla ilişkili davranış tetiği" şu somut kurala
// çevrildi (ilk prototip yorumu; belgede açık nokta olarak durur):
// - NORMAL bir hat vuruşu, davranışı olan bir saksının bitkisini hasat ederse (hat sırasındaki ilk böyle hasat), o saksının en
//   yüksek davranış şansı × 'chanceFactor' ile bir zar atılır (etkilerin ayrı zar akışı; UnityEngine.Random'a dokunmaz).
// - Zar tutarsa ve hattın bekleme süresi dolmuşsa 'extraDelay' sonra TEK bir ek hat vuruşu gelir: normal hat hasarının
//   'extraDamage' oranı. Vuruş başına en çok bir ek vuruş.
// - Ek vuruş kendisini yeniden başlatamaz: ek vuruşun hasatları zar attırmaz. Bekleme süresi ('cooldown') ek vuruş planlandığı
//   anda başlar; dolmadan yeni ek vuruş planlanmaz.
// - Davranışı olan saksı kalmadıysa ya da hat pasifse hiçbir şey yapmaz (güvenli pasif).
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Kapalı Devre", fileName = "ClosedCircuitEffect")]
public sealed class ClosedCircuitEffectSO : RunEffectSO
{
    [Tooltip("Ek hat vuruşunun gecikmesi (sn).")]
    [Min(0f)] public float extraDelay = 0.35f;
    [Tooltip("Ek vuruşun hasarı: normal hat hasarına oran.")]
    [Range(0f, 1f)] public float extraDamage = 0.6f;
    [Tooltip("İki ek vuruş arasındaki en kısa süre (sn, oyun zamanı).")]
    [Min(0f)] public float cooldown = 3f;
    [Tooltip("Zar: saksının en yüksek davranış şansı × bu değer.")]
    [Range(0f, 1f)] public float chanceFactor = 1f;

    public override string Summary =>
        $"hat, davranışlı saksının bitkisini hasat ederse o saksının davranış şansıyla ek hat vuruşu · hasarın %{Percent(extraDamage)}'i · bekleme {Number(cooldown)} sn";

    public override RunEffect Create() => new ClosedCircuitEffect();

    public override bool CanTake(RunEffects host) => host.Find<ConductiveLineEffect>() != null;
}

public sealed class ClosedCircuitEffect : RunEffect
{
    private ConductiveLineEffect line;
    private double readyAt;
    private bool pending;
    private ClosedCircuitEffectSO Config => (ClosedCircuitEffectSO)Data;

    public int Rolls { get; private set; }
    public int Extras { get; private set; }
    public int CooldownBlocks { get; private set; }

    public override bool OnAdded(int placement)
    {
        line = Host.Find<ConductiveLineEffect>();
        if (line == null) return false;
        line.Circuit = this;
        return true;
    }

    public override void OnRemoved()
    {
        if (line != null && line.Circuit == this) line.Circuit = null;
        line = null;
        pending = false;
    }

    public override void OnRoundStarted() { readyAt = 0d; pending = false; }
    public override void OnRoundEnded() => pending = false;

    // Normal hat vuruşu, davranışı olan bu saksının bitkisini hasat etti.
    public void OnLineHarvest(PlanterBrain planter)
    {
        if (pending || planter == null || !planter.IsPlaced) return;
        if (Host.Clock < readyAt) { CooldownBlocks++; return; }
        float chance = RunEffects.BestBehaviorChance(planter) * Config.chanceFactor;
        if (chance <= 0f) return;
        Rolls++;
        if (Host.Roll() >= chance) return;
        if (!Host.Schedule(this, Config.extraDelay, 0)) return;
        pending = true;
        line.ShowCircuitReturn();
        readyAt = Host.Clock + Config.cooldown;
    }

    public override void OnTimer(int kind, int a, int b, int c)
    {
        pending = false;
        if (line == null) return;
        Extras++;
        line.Pulse(true, Config.extraDamage);
    }

    public override void Describe(StringBuilder text) =>
        text.Append("zar ").Append(Rolls).Append(" · ek vuruş ").Append(Extras).Append(" · bekleme ").Append(System.Math.Max(0d, readyAt - Host.Clock).ToString("0.0"))
            .Append(" sn · beklemede reddedilen ").Append(CooldownBlocks).Append('\n');
}
