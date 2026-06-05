using UnityEngine;

public class PlantSpawner : MonoBehaviour
{
    private PlanterSO planterData;
    private GridObject gridObject;
    private float timer;
    private GameObject spawnedPlant;

    public void Initialize(PlanterSO data, GridObject gridObj)
    {
        planterData = data;
        gridObject = gridObj;
        timer = Random.Range(0f, planterData.baseSpawnInterval);
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Playing)
            return;

        if (spawnedPlant != null)
            return;

        timer += Time.deltaTime;

        if (timer >= planterData.baseSpawnInterval)
        {
            timer = 0f;
            TrySpawnPlant();
        }
    }

    private void TrySpawnPlant()
    {
        PlantSO selectedPlant = RollPlant();
        if (selectedPlant == null) return;

        GameObject plantObj = Instantiate(
            selectedPlant.prefab,
            transform.position,
            transform.rotation
        );

        PlantBrain plantBrain = plantObj.GetComponent<PlantBrain>();
        plantBrain?.Initialize(selectedPlant, gridObject); 

        PlantHealth plantHealth = plantObj.GetComponent<PlantHealth>();
        if (plantHealth != null)
            plantHealth.OnDied += OnPlantDied;

        spawnedPlant = plantObj;
        gridObject?.SetPlantObject(plantObj);
    }

    private void OnPlantDied()
    {
        spawnedPlant = null;
        timer = 0f;
    }

    private PlantSO RollPlant()
    {
        float rareBonus = StatManager.Instance.GetStat(StatType.RareMineChance);

        float totalWeight = 0f;
        foreach (PlantSpawnEntry entry in planterData.spawnTable)
            totalWeight += GetAdjustedChance(entry, rareBonus);

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        foreach (PlantSpawnEntry entry in planterData.spawnTable)
        {
            cumulative += GetAdjustedChance(entry, rareBonus);
            if (roll <= cumulative)
                return entry.plant;
        }

        return null;
    }

    private float GetAdjustedChance(PlantSpawnEntry entry, float rareBonus)
    {
        if (entry.baseChance <= 0f) return 0f;

        float chance = entry.baseChance;

        switch (entry.plant.rarity)
        {
            case PlantRarity.Uncommon:  chance += rareBonus * 0.5f; break;
            case PlantRarity.Rare:      chance += rareBonus * 1f;   break;
            case PlantRarity.Epic:      chance += rareBonus * 2f;   break;
            case PlantRarity.Legendary: chance += rareBonus * 3f;   break;
        }

        return chance;
    }
}