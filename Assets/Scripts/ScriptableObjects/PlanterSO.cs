using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlantSpawnEntry
{
    public PlantSO plant;
    [Range(0f, 100f)] public float baseChance;
}

[CreateAssetMenu(menuName = "Game/Planter")]
public class PlanterSO : ScriptableObject
{
    [Header("Bilgi")]
    public string planterName;

    [Header("Görsel")]
    public GameObject prefab;

    [Header("Spawn")]
    public List<PlantSpawnEntry> spawnTable;

    [Header("Base Stats")]
    public List<StatEntry> baseStats = new List<StatEntry>();

    [Header("Boyut")]
    public int sizeX = 1;
    public int sizeZ = 1;

    [Header("Fiyat")]
    public ResourceType costType;
    public int cost;
    public UnlockType requiredUnlock;
    // Bu run'daki fiyat: run profilinin denge seti değiştirebilir (RunBalanceSO.planterPrices); yoksa yukarıdaki asset değeri.
    public ResourceType PriceType { get { RunBalanceSO.PriceOf(this, out ResourceType type, out _); return type; } }
    public int Price { get { RunBalanceSO.PriceOf(this, out _, out int price); return price; } }
    public bool IsUnlocked => requiredUnlock == UnlockType.None ||
        (UnlockManager.Instance != null && UnlockManager.Instance.IsUnlocked(requiredUnlock));

    public float GetBaseStat(StatType statType)
    {
        // Run profilinin denge seti saksı tabanını değiştirebilir (Bölüm 3.5: üretim aralığı); yoksa asset değeri.
        if (RunBalanceSO.TryPlanterBaseStat(statType, out float balanced)) return balanced;
        foreach (StatEntry statEntry in baseStats)
        {
            if (statEntry.statType == statType)
                return statEntry.value;
        }

        //Debug.LogWarning($"{planterName} içinde stat bulunamadı: {statType}");
        return StatDefaults.GetDefaultBase(statType);
    }
}
