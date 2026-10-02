using System.Collections.Generic;
using UnityEngine;

// Run başında seçilen başlangıç içeriği (çiftçi ya da tırpan). Etkiler run boyunca geçerlidir; StartLoadoutManager yeni run'da uygular,
// run bitince / ana menüde kaldırır. Sabit id kayıtta tutulur: id'yi değiştirmek eski kayıttaki seçimi ve kilidi geçersiz kılar.
// Etki yolları:
//   modifiers                 → StatManager global modifier (mevcut ve sonradan alınan saksılar dahil; yalnız bu sistemin eklediği kaldırılır)
//   directDamageMultiplier    → yalnız oyuncunun doğrudan vuruşu (PlayerController), davranış hasarına geçmez
//   harvestResourceMultiplier → yalnız hasat kaynağı (PlantResource), kesirli kalan taşınır
//   harvestScoreMultiplier    → yalnız hasat skoru (HarvestScoreManager), kesirli kalan taşınır
public abstract class StartOptionSO : ScriptableObject
{
    [Tooltip("Kayıttaki sabit kimlik. Yayınlandıktan sonra değiştirme.")]
    public string id;
    public string displayName;
    [Tooltip("Kısa oyun tarzı cümlesi (seçim kartında).")]
    [TextArea] public string playstyle;
    [Tooltip("Boşsa başlangıçta açık. Doluysa bu görev tamamlanınca kalıcı olarak açılır.")]
    public QuestSO unlockQuest;
    public int sortOrder;

    [Header("Etkiler")]
    public List<StatModifier> modifiers = new();
    [Min(0.01f)] public float directDamageMultiplier = 1f;
    [Min(0.01f)] public float harvestResourceMultiplier = 1f;
    [Min(0.01f)] public float harvestScoreMultiplier = 1f;

    public bool OpenAtStart => unlockQuest == null;
    public bool IsNeutral => modifiers.Count == 0 && Mathf.Approximately(directDamageMultiplier, 1f) &&
                             Mathf.Approximately(harvestResourceMultiplier, 1f) && Mathf.Approximately(harvestScoreMultiplier, 1f);
}
