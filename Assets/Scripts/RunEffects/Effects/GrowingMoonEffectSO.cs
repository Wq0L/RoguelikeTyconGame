using System.Text;
using UnityEngine;

// Büyüyen Ay (Bölüm 3.8, orta aşama; ön koşul: Hilal Darbesi). Hilalin GERÇEK hasatları aynı dalganın kalan adımlarını büyütür.
// - Bir adım en az bir bitki hasat ettiyse (o adımın vuruşuyla ölen bitki) büyüme düzeyi 1 artar (en çok 'maxGrowth').
// - Büyüme yalnız SONRAKİ adımlara uygulanır: hasar × (1 + 'damagePerGrowth' × düzey), cephe yarı genişliği = min(düzey,
//   'maxHalfWidth') hücre (düzey 1'de üç hücrelik cephe).
// - "Başarısız ilerleme" = hiçbir bitki hasat etmeyen adım (hedef olmaması dahil): büyüme 0'a döner.
// - Büyüme dalgalar arasında taşınmaz; her dalga 0'dan başlar. Kural CrescentEffect'tedir; bu etki yalnız ayarını oraya kurar.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Büyüyen Ay", fileName = "GrowingMoonEffect")]
public sealed class GrowingMoonEffectSO : RunEffectSO
{
    [Tooltip("En yüksek büyüme düzeyi.")]
    [Range(1, 6)] public int maxGrowth = 3;
    [Tooltip("Düzey başına hasar artışı (0,25: düzey 2'de ×1,50).")]
    [Min(0f)] public float damagePerGrowth = 0.25f;
    [Tooltip("Cephenin en büyük yarı genişliği, hücre (1: üç hücrelik cephe).")]
    [Range(0, 3)] public int maxHalfWidth = 1;

    public override string Summary =>
        $"hilalin hasat ettiği her adım kalan adımları büyütür: hasar +%{Percent(damagePerGrowth)} (en çok {maxGrowth} kez), cephe {maxHalfWidth * 2 + 1} hücre · hasatsız adım sıfırlar";

    public override RunEffect Create() => new GrowingMoonEffect();

    public override bool CanTake(RunEffects host) => host.Find<CrescentEffect>() != null;
}

public sealed class GrowingMoonEffect : RunEffect
{
    private CrescentEffect crescent;

    public override bool OnAdded(int placement)
    {
        crescent = Host.Find<CrescentEffect>();
        if (crescent == null) return false;
        crescent.Growth = (GrowingMoonEffectSO)Data;
        return true;
    }

    public override void OnRemoved()
    {
        if (crescent != null && crescent.Growth == Data) crescent.Growth = null;
        crescent = null;
    }

    public override void Describe(StringBuilder text) => text.Append("ayar Hilal Darbesi'ne kurulu\n");
}
