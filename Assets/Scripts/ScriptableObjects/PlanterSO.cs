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
    public float baseSpawnInterval;

    [Header("Boyut")]
    public int sizeX = 1;
    public int sizeZ = 1;
    
    [Header("Fiyat")]
    public int cost;
}