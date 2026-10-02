using UnityEngine;

// Boss sonrası bedelli uzmanlaşma seçeneği. Katsayılar hasar akışında tek yerde uygulanır:
// doğrudan → PlayerController (sapma sonrası, kritik ve saksı bonusundan önce); davranış → PlanterBrain.BehaviorDamageMultiplier
// (her davranışın kendi hesaplanan hasarına, rezonansla aynı noktada). Ortak skill hasarı iki tarafı da beslemeye devam eder.
// Hasat kaynağı → PlantResource (duplicate dahil ödül hesabından sonra, SpecializationManager.ScaleHarvestResource ile).
[CreateAssetMenu(menuName = "ClickerGame/Specialization", fileName = "Specialization")]
public class SpecializationSO : ScriptableObject
{
    public string displayName = "UZMANLAŞMA";
    [Tooltip("İsteğe bağlı kısa not. Kazanç/bedel satırları katsayılardan otomatik yazılır.")]
    public string note = "";
    [Tooltip("Doğrudan saldırı hasarı çarpanı.")]
    [Range(0.5f, 2f)] public float directDamageMultiplier = 1f;
    [Tooltip("Patlama, kasırga, bumerang ve elektrik hasarı çarpanı.")]
    [Range(0.5f, 2f)] public float behaviorDamageMultiplier = 1f;
    [Tooltip("Hasattan gelen Gold, Iron ve Stone çarpanı (doğrudan ve davranış öldürmeleri). Başlangıç parası, satış iadesi ve kart atlama ödülü etkilenmez; XP ve skor etkilenmez.")]
    [Range(0.5f, 1.5f)] public float harvestResourceMultiplier = 1f;
    public Color color = Color.white;

    public bool ChangesNothing => Mathf.Approximately(directDamageMultiplier, 1f) && Mathf.Approximately(behaviorDamageMultiplier, 1f) &&
                                  Mathf.Approximately(harvestResourceMultiplier, 1f);
}
