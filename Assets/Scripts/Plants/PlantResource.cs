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

        float seedMultiplier = GetSeedMultiplier();
        float xpMultiplier = GetXPMultiplier();

        int reward = Mathf.RoundToInt(plantData.rewardAmount * seedMultiplier);
        int xpAmount = Mathf.RoundToInt(plantData.xpAmount * xpMultiplier);

        ResourceManager.Instance.AddResource(plantData.resourceType, reward);
        ProgressionManager.Instance.AddXP(xpAmount);
    }

    private float GetSeedMultiplier()
    {
        if (planterBrain != null)
            return planterBrain.GetFinalStat(StatType.SeedGainMultiplier);

        return StatManager.Instance.GetFinalStat(
            StatType.SeedGainMultiplier,
            StatTarget.Planter
        );
    }

    private float GetXPMultiplier()
    {
        if (planterBrain != null)
            return planterBrain.GetFinalStat(StatType.XPGainMultiplier);

        return StatManager.Instance.GetFinalStat(
            StatType.XPGainMultiplier,
            StatTarget.Planter
        );
    }
}