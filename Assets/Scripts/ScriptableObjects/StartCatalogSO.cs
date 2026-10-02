using System.Collections.Generic;
using UnityEngine;

// Başlangıç içeriği kataloğu (Resources/StartCatalog). Seçim ekranı ve StartLoadoutManager buradan okur.
// Listede olmayan içerik seçilemez. Varsayılanlar nötrdür (Bahçıvan + Standart) ve her zaman açıktır.
[CreateAssetMenu(menuName = "ClickerGame/Start/Catalog", fileName = "StartCatalog")]
public sealed class StartCatalogSO : ScriptableObject
{
    public const string ResourcePath = "StartCatalog";

    public List<FarmerSO> farmers = new();
    public List<ScytheSO> scythes = new();
    public List<QuestSO> quests = new();
    public FarmerSO defaultFarmer;
    public ScytheSO defaultScythe;

    private static StartCatalogSO loaded;
    private static bool attempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        loaded = null;
        attempted = false;
    }

    public static StartCatalogSO Active
    {
        get
        {
            if (!attempted)
            {
                loaded = Resources.Load<StartCatalogSO>(ResourcePath);
                attempted = true;
            }
            return loaded;
        }
    }

    public FarmerSO Farmer(string id) => Find(farmers, id);
    public ScytheSO Scythe(string id) => Find(scythes, id);
    public QuestSO Quest(string id) => Find(quests, id);

    private static T Find<T>(List<T> list, string id) where T : ScriptableObject
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (T item in list)
            if (item != null && Id(item) == id) return item;
        return null;
    }

    private static string Id(ScriptableObject item) => item is StartOptionSO o ? o.id : item is QuestSO q ? q.id : null;

    // Bu görevi tamamlayınca açılan içerikler.
    public IEnumerable<StartOptionSO> UnlockedBy(QuestSO quest)
    {
        foreach (FarmerSO f in farmers) if (f != null && f.unlockQuest == quest) yield return f;
        foreach (ScytheSO s in scythes) if (s != null && s.unlockQuest == quest) yield return s;
    }
}
