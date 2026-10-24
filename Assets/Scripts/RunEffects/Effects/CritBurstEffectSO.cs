using System.Text;
using UnityEngine;

// 19 Ekim 2026: oyundan kaldırıldı. Eski asset referansları için veri alanları korunur; etki alınamaz veya hasar veremez.
public sealed class CritBurstEffectSO : RunEffectSO
{
    [Tooltip("Tek saldırıda kritik yemesi gereken farklı bitki sayısı.")]
    [Range(2, 12)] public int minCrits = 3;
    [Tooltip("Patlama hasarı: hasat hasarı × kritik çarpanı stat'larına oran.")]
    [Min(0f)] public float damage = 0.5f;
    [Tooltip("Alan: saldırının temas yarıçapına oran.")]
    [Range(0.25f, 2f)] public float radiusFactor = 1f;

    public override string Summary =>
        $"tek vuruşta {minCrits}+ bitkiye kritik: merkezde ek alan vuruşu · hasat hasarı × kritik çarpanının %{Percent(damage)}'i";

    public override RunEffect Create() => new CritBurstEffect();

    public override bool CanTake(RunEffects host) => false;
}

public sealed class CritBurstEffect : RunEffect
{
    // Compatibility shell: old serialized references remain valid, but cannot deal damage.
    public int Bursts => 0;
    public override bool OnAdded(int placement) => false;
    public override void Describe(StringBuilder text) => text.Append("Kritik Patlama kaldırıldı.\n");
}
