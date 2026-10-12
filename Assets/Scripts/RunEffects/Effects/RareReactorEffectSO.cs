using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Nadir Reaktör (Bölüm 3.8, orta aşama). 'minRarity' ve üstü bir bitkinin DOĞRUDAN hasadı, saksıların MEVCUT davranış şanslarını
// 'seconds' süreyle artırır.
// - "Gerçek hasat" = oyuncunun kendi vuruşuyla ölen bitki. Davranış, artçı ve ödül etkisi hasatları reaktörü çalıştırmaz
//   (reaktörün artırdığı zarlar yalnız doğrudan hasatta atılır; böylece etki kendi kendini beslemez).
// - Etki: patlama, kasırga, bumerang ve elektrik şansına ayrı bir çarpan (×(1 + 'chanceBonus')). Şansı olmayan saksıya şans
//   vermez; şans yine en çok %100'dür (mevcut sınır).
// - Birikmez: süre içindeki yeni nadir hasat yalnız süreyi yeniler.
// - Geçici stat'lar bu etkinin kendi modifier'larıdır: süre bitince, round sonunda, yeni run'da ve etki kalkınca yalnız onlar
//   kaldırılır; başka bir ödülün (Kıvılcım) katkısına dokunulmaz. StatManager listesi toptan temizlenirse sahiplik unutulur.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Nadir Reaktör", fileName = "RareReactorEffect")]
public sealed class RareReactorEffectSO : RunEffectSO
{
    [Tooltip("Reaktörü çalıştıran en düşük nadirlik.")]
    public PlantRarity minRarity = PlantRarity.Rare;
    [Tooltip("Davranış şansı çarpanı: ×(1 + bu değer).")]
    [Range(0f, 1f)] public float chanceBonus = 0.25f;
    [Tooltip("Etkinin süresi (sn, oyun zamanı).")]
    [Min(0.1f)] public float seconds = 4f;

    public override string Summary =>
        $"{minRarity} ve üstü bitkiyi kendi vuruşunla hasat edince {Number(seconds)} sn mevcut davranış şansları ×{Number(1f + chanceBonus)} · birikmez, süre yenilenir";

    public override RunEffect Create() => new RareReactorEffect();
}

public sealed class RareReactorEffect : RunEffect
{
    private static readonly StatType[] Chances = { StatType.ExplosionChance, StatType.TornadoChance, StatType.BoomerangChance, StatType.ElectricChance };
    private StatManager stats;
    private bool applied;
    private float remaining;
    private RareReactorEffectSO Config => (RareReactorEffectSO)Data;

    public bool IsActive => applied;
    public float Remaining => applied ? remaining : 0f;
    public int Activations { get; private set; }
    public int Refreshes { get; private set; }

    public override void AttackEnd(DirectAttackReport attack)
    {
        IReadOnlyList<DirectHit> hits = attack.Hits;
        for (int i = 0; i < hits.Count; i++)
        {
            if (!hits[i].Killed || hits[i].Rarity < Config.minRarity) continue;
            Activate(hits[i].Cell);
            return;
        }
    }

    private StatModifier Modifier(StatType stat) =>
        new StatModifier { statType = stat, target = StatTarget.Planter, operation = ModifierOperation.MorePercent, value = Config.chanceBonus };

    private void Activate(GridObject cell)
    {
        remaining = Config.seconds;
        if (applied) { Refreshes++; return; }
        stats = StatManager.Instance;
        if (stats == null) return;
        for (int i = 0; i < Chances.Length; i++) stats.AddGlobalModifier(Modifier(Chances[i]));
        stats.OnGlobalModifiersCleared += Forget;
        applied = true;
        Activations++;
        GridSystem grid = Host.Grid;
        Host.Emit(new RewardCue(RewardCueKind.ReactorOn, grid != null && cell != null ? FieldCells.World(grid, cell.GetGridPosition()) : Vector3.zero));
    }

    // Yalnız bu etkinin eklediği dört modifier kalkar (idempotent).
    private void Deactivate()
    {
        if (stats != null)
        {
            stats.OnGlobalModifiersCleared -= Forget;
            if (applied)
                for (int i = 0; i < Chances.Length; i++)
                {
                    StatModifier modifier = Modifier(Chances[i]);
                    if (Contains(stats.GlobalModifiers, modifier)) stats.RemoveGlobalModifier(modifier);
                }
        }
        if (applied && Host != null) Host.Emit(new RewardCue(RewardCueKind.ReactorOff, Vector3.zero));
        applied = false;
        stats = null;
        remaining = 0f;
    }

    private void Forget() => applied = false;

    private static bool Contains(IReadOnlyList<StatModifier> list, StatModifier modifier)
    {
        for (int i = 0; i < list.Count; i++) if (list[i].Equals(modifier)) return true;
        return false;
    }

    public override void Tick(float deltaTime)
    {
        if (!applied) return;
        remaining -= deltaTime;
        if (remaining <= 0f) Deactivate();
    }

    public override void OnRoundEnded() => Deactivate();
    public override void OnRemoved() => Deactivate();

    public override void Describe(StringBuilder text) =>
        text.Append(applied ? $"AKTİF · kalan {remaining:0.0} sn" : "beklemede").Append(" · çalışma ").Append(Activations).Append(" · yenileme ").Append(Refreshes).Append('\n');
}
