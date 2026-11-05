using UnityEngine;

// Kalıcı içerik açan görev. Sayaç türü QuestTracker'da hangi olayın sayılacağını belirler.
// Tek-run görevleri: sayaç her yeni run'da sıfırlanır; kayıtta yalnız en iyi run ve tamamlanma tutulur.
[CreateAssetMenu(menuName = "ClickerGame/Start/Quest", fileName = "Quest")]
public sealed class QuestSO : ScriptableObject
{
    [Tooltip("Kayıttaki sabit kimlik. Yayınlandıktan sonra değiştirme.")]
    public string id;
    [Tooltip("Seçim ekranında görünen görev metni.")]
    public string description;
    public QuestCounter counter;
    [Min(1)] public int target = 1;

    // Oyuncuya görünen görev metni: çeviri tablosundan (data.quest.<id>); anahtar yoksa asset'teki yazı.
    public string Description => GameText.Data(DescriptionKey, description);
    public string DescriptionKey => string.IsNullOrEmpty(id) ? null : "data.quest." + id;
}

public enum QuestCounter
{
    // Tek run'da hasat edilen Legendary bitki (doğrudan + davranış; bitki başına bir, çifte ödül ayrı hasat değildir).
    LegendaryHarvestInRun = 0,
}
