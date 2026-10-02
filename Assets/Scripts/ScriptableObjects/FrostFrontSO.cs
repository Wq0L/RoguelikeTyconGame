using UnityEngine;

// Don Cephesi verisi: açık alanın bir kenarından giren şeritte üretim yavaşlar. Uygulama: FrostFrontEvent.
[CreateAssetMenu(menuName = "ClickerGame/Segment Events/Don Cephesi", fileName = "DonCephesi")]
public class FrostFrontSO : SegmentEventSO
{
    [Tooltip("Şeritteki üretim noktalarının üretim aralığı çarpanı. Nihai süreye (taban ve tempodan sonra) uygulanır.")]
    [Range(1f, 3f)] public float spawnIntervalMultiplier = 1.5f;
    [Tooltip("Şeridin kalınlığı: açık alan kenarının bu oranı kadar satır/sütun, en az 1, açık alanın tamamı asla.")]
    [Range(0.1f, 0.5f)] public float coverage = 0.25f;
    [Tooltip("0: her run farklı kenar. Başka bir sayı: aynı tarlada hep aynı seçim (test ve karşılaştırma için).")]
    public int seed;

    public override SegmentEventRuntime CreateRuntime(SegmentEventTiming timing) => new FrostFrontEvent(this, timing);
}
