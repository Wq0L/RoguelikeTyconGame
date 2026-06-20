using UnityEngine;

public class PlantSpawner : MonoBehaviour
{
    private PlanterSO planterData;
    private GridObject gridObject;
    private PlanterBrain planterBrain;
    private float timer;
    private GameObject spawnedPlant;

    public void Initialize(PlanterSO data, GridObject gridObj, PlanterBrain brain)
    {
        planterData = data;
        gridObject = gridObj;
        planterBrain = brain;

        timer = Random.Range(0f, GetEffectiveSpawnInterval());
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Round)
            return;

        if (spawnedPlant != null)
            return;

        timer += Time.deltaTime;

        if (timer >= GetEffectiveSpawnInterval())
        {
            timer = 0f;
            TrySpawnPlant();
        }
    }

    private float GetEffectiveSpawnInterval()
    {
        if (planterBrain != null)
            return planterBrain.GetFinalStat(StatType.PlantSpawnRate);

        return planterData.GetBaseStat(StatType.PlantSpawnRate);
    }

    private float GetEffectiveRareBonus()
    {
        if (planterBrain != null)
            return planterBrain.GetFinalStat(StatType.RareSpawnChance);

        return planterData.GetBaseStat(StatType.RareSpawnChance);
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
        {
            plantHealth.Initialize(selectedPlant.maxHealth);  
            plantHealth.OnDied += OnPlantDied;
        }

        PlantResource plantResource = plantObj.GetComponent<PlantResource>();
        plantResource?.Initialize(selectedPlant, planterBrain);

        spawnedPlant = plantObj;
        gridObject?.SetPlantObject(plantObj);
    }

    private void OnPlantDied()
    {
        PlantHealth health = spawnedPlant?.GetComponent<PlantHealth>();
        bool wasExplosion = health != null && health.KilledByExplosion;

        if (planterBrain != null && !wasExplosion)
            planterBrain.TryExplode(gridObject);

        spawnedPlant = null;
        timer = 0f;
    }

    private PlantSO RollPlant()
    {
        float rareBonus = GetEffectiveRareBonus();

        float totalWeight = 0f;
        foreach (PlantSpawnEntry entry in planterData.spawnTable)
            totalWeight += GetAdjustedChance(entry, rareBonus);

        if (totalWeight <= 0f)
            return null;

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
            case PlantRarity.Common:    break;
            case PlantRarity.Uncommon:  chance *= 1f + rareBonus * 0.01f; break;
            case PlantRarity.Rare:      chance *= 1f + rareBonus * 0.01f * 1.5f; break;
            case PlantRarity.Epic:      chance *= 1f + rareBonus * 0.01f * 2f; break;
            case PlantRarity.Legendary: chance *= 1f + rareBonus * 0.01f * 3f; break;
        }

        return chance;
    }
}