using UnityEngine;

// Sert Kabuk verisi: açık alanın bir kenar şeridinde, olay aktifken doğan bitkiler daha canlı olur. Uygulama: HardShellEvent.
// Kalıcı bitki canı (PlantSO, PlantHealthScaling) değişmez; çarpan yalnız o bitkinin o yaşamına uygulanır.
[CreateAssetMenu(menuName = "ClickerGame/Segment Events/Sert Kabuk", fileName = "SertKabuk")]
public class HardShellSO : SegmentEventSO
{
    [Tooltip("Şeritteki bitkilerin can çarpanı (ilk test değeri; dengelenmedi).")]
    [Range(1f, 3f)] public float healthMultiplier = 1.5f;
    [Tooltip("Şeridin kalınlığı: açık alan kenarının bu oranı kadar satır/sütun, en az 1, açık alanın tamamı asla.")]
    [Range(0.1f, 0.5f)] public float coverage = 0.4f;

    public override SegmentEventRuntime CreateRuntime(SegmentEventTiming timing) => new HardShellEvent(this, timing);
}
