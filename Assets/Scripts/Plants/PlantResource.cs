using UnityEngine;

public class PlantResource : MonoBehaviour
{
    [SerializeField] private PlantHealth plantHealth;

    private PlantSO plantData;
    private PlanterBrain planterBrain;

    private void OnEnable()
    {
        plantHealth.OnDied += GiveReward;
    }

    private void OnDisable()
    {
        plantHealth.OnDied -= GiveReward;
    }

    public void Initialize(PlantSO data, PlanterBrain brain)
    {
        plantData = data;
        planterBrain = brain;
    }

    private void GiveReward()
    {
        if (plantData == null) return;

        float resourceMultiplier = GetResourceMultiplier(plantData.resourceType);
        float xpMultiplier = GetXPMultiplier();

        int reward = Mathf.RoundToInt(plantData.rewardAmount * resourceMultiplier);
        int xpAmount = Mathf.RoundToInt(plantData.xpAmount * xpMultiplier);
        int scoreMultiplier = 1;

        if (planterBrain != null)
        {
            float dupChance = planterBrain.GetFinalStat(StatType.DuplicateChance);
            if (dupChance > 0f && Random.value <= dupChance)
            {
                reward *= 2;
                xpAmount *= 2;
                scoreMultiplier = 2;
                Debug.Log("Duplicate! Ödül 2x");
            }
        }

        ResourceManager.Instance.AddResource(plantData.resourceType, reward);
        ProgressionManager.Instance.AddXP(xpAmount);

        for (int i = 0; i < scoreMultiplier; i++)
            HarvestScoreManager.Instance.AddScore(plantData.rarity);

        Debug.Log($"Hasat: {plantData.resourceType} x{reward} | XP x{xpAmount} | Multiplier: {resourceMultiplier}");
    }

    private float GetResourceMultiplier(ResourceType type)
    {
        StatType statType = type switch
        {
            ResourceType.Gold  => StatType.GoldGainMultiplier,
            ResourceType.Iron  => StatType.IronGainMultiplier,
            ResourceType.Stone => StatType.StoneGainMultiplier,
            _                  => StatType.GoldGainMultiplier
        };

        if (planterBrain != null)
            return planterBrain.GetFinalStat(statType);

        return StatManager.Instance.GetFinalStat(statType, StatTarget.Planter);
    }

    private float GetXPMultiplier()
    {
        if (planterBrain != null)
            return planterBrain.GetFinalStat(StatType.XPGainMultiplier);

        return StatManager.Instance.GetFinalStat(StatType.XPGainMultiplier, StatTarget.Planter);
    }
}