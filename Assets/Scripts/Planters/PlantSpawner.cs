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
        timer = Random.Range(0f, planterData.baseSpawnInterval);
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
        float interval = StatManager.Instance.GetStat(StatType.MineSpawnInterval);

        if (planterBrain != null)
        {
            foreach (StatModifier mod in planterBrain.ActiveModifiers)
            {
                if (mod.statType != StatType.MineSpawnInterval) continue;

                switch (mod.operation)
                {
                    case ModifierOperation.Add:
                        interval += mod.value;
                        break;
                    case ModifierOperation.Multiply:
                        interval *= mod.value;
                        break;
                    case ModifierOperation.Set:
                        interval = mod.value;
                        break;
                }
            }
        }

        return Mathf.Max(interval, 0.1f);
    }

    private float GetEffectiveRareBonus()
    {
        float rareBonus = StatManager.Instance.GetStat(StatType.RareMineChance);

        if (planterBrain != null)
        {
            foreach (StatModifier mod in planterBrain.ActiveModifiers)
            {
                if (mod.statType != StatType.RareMineChance) continue;

                switch (mod.operation)
                {
                    case ModifierOperation.Add:
                        rareBonus += mod.value;
                        break;
                    case ModifierOperation.Multiply:
                        rareBonus *= mod.value;
                        break;
                    case ModifierOperation.Set:
                        rareBonus = mod.value;
                        break;
                }
            }
        }

        return rareBonus;
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

        PlantResource plantResource = plantObj.GetComponent<PlantResource>();
        plantResource?.Initialize(selectedPlant, planterBrain);

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
        float rareBonus = GetEffectiveRareBonus();

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