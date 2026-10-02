using UnityEngine;

// Sis verisi: olay aktifken oyuncunun doğrudan vuruş yarıçapı geçici olarak küçülür. Uygulama: FogEvent.
// Davranışların (patlama, kasırga, bumerang, elektrik) alanına ve hasarına dokunmaz.
[CreateAssetMenu(menuName = "ClickerGame/Segment Events/Sis", fileName = "Sis")]
public class FogSO : SegmentEventSO
{
    [Tooltip("Vuruş yarıçapı çarpanı (ilk test değeri; dengelenmedi). 'Basamak düşür' açıkken kullanılmaz.")]
    [Range(0.5f, 1f)] public float radiusMultiplier = 0.85f;
    [Tooltip("Oyuncunun yarıçapı bunun altındaysa Sis seçilmez. Eşik, daire-hücre yüzeyi temas geometrisine göre ayarlanır.")]
    [Min(0f)] public float minPlayerRadius = 1.2f;
    [Tooltip("Bölüm 3.5: sabit çarpan yerine, yarıçap en iyi nişanın hedef sayısını tam bir basamak düşürecek kadar küçülür " +
             "(ör. 9 → 6 hücre). Sabit çarpan bazı yarıçaplarda hiçbir hedefi değiştirmiyordu. Kapalı: eski çarpan kuralı.")]
    public bool stepDown;

    public override bool IsEligible(SegmentEventContext context) => context == null || context.PlayerRadius >= minPlayerRadius;

    public override SegmentEventRuntime CreateRuntime(SegmentEventTiming timing) => new FogEvent(this, timing);
}
