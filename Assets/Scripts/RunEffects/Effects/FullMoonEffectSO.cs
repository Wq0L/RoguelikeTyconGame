using System.Text;
using UnityEngine;

// Dolunay: tam sürümde her ana hilalle bir büyük ek hilal çıkar; yönü ana hilalden farklıdır.
// Eski profil yolu hasada bağlı growthThreshold koşulunu korur.
// - Tam sürümde ek dalga doğduğu anda hareket eder. Eski profil returnDelay ile karşı uçtan dönüşü korur. Her ek dalganın hasarı ilk
//   dalganın TABAN hasarının 'returnDamage' oranıdır (büyüme hasarı taşınmaz); cephesi eşiğin aşıldığı andaki genişliktedir.
// - İkinci dalga büyümez ve yeni Dolunay başlatamaz; dalga başına en çok bir Dolunay.
// - Bedel: 'pauseSeconds' süren doğrudan saldırı duraklaması. Yalnız oyuncunun saldırı zamanlayıcısı bekler (DirectAttackPause):
//   süre sert üst sınırla kırpılır, oyun zamanıyla azalır, görseli beklemez, round sonunda ve yeni run'da kalkar.
// Kural CrescentEffect'tedir; bu etki yalnız ayarını oraya kurar.
[CreateAssetMenu(menuName = "ClickerGame/Run Effects/Dolunay", fileName = "FullMoonEffect")]
public sealed class FullMoonEffectSO : RunEffectSO
{
    [Tooltip("Yalnız eski profil: ek hilali başlatan hasat büyüme düzeyi.")]
    [Range(1, 6)] public int growthThreshold = 2;
    [Tooltip("Her Dolunay dalgasının hasarı: hilalin taban hasarına oran.")]
    [Range(0f, 2f)] public float returnDamage = 0.7f;
    [Tooltip("Yalnız eski profil: dönüş dalgasının gecikmesi (sn). Tam sürümde ek hilal hemen hareket eder.")]
    [Min(0f)] public float returnDelay = 0.15f;
    [Tooltip("Doğrudan saldırı duraklaması (sn). DirectAttackPause.MaxSeconds ile kırpılır.")]
    [Range(0f, DirectAttackPause.MaxSeconds)] public float pauseSeconds = 0.35f;

    public override string Summary =>
        $"her ana hilalle mouse halkasından bir büyük ek hilal · ana hilalden farklı rastgele yön · taban hasarın %{Percent(returnDamage)}'i · bedel: {Number(pauseSeconds)} sn vuruş duraklaması";

    public override RunEffect Create() => new FullMoonEffect();

    // Büyüyen Ay alınmış olmalı (büyüme olmadan eşik hiç aşılmaz).
    public override bool CanTake(RunEffects host)
    {
        CrescentEffect crescent = host.Find<CrescentEffect>();
        return crescent != null && crescent.Growth != null;
    }
}

public sealed class FullMoonEffect : RunEffect
{
    private CrescentEffect crescent;

    public override bool OnAdded(int placement)
    {
        crescent = Host.Find<CrescentEffect>();
        if (crescent == null || crescent.Growth == null) return false;
        crescent.FullMoon = (FullMoonEffectSO)Data;
        return true;
    }

    public override void OnRemoved()
    {
        if (crescent != null && crescent.FullMoon == Data) crescent.FullMoon = null;
        crescent = null;
        // Yalnız kendi kilidi: başka bir kaynağın duraklaması sürer.
        DirectAttackPause.Release(CrescentEffect.PauseSource);
    }

    public override void Describe(StringBuilder text) => text.Append("ayar Hilal Darbesi'ne kurulu\n");
}
