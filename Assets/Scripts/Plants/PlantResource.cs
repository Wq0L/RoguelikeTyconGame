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

        int reward = plantData.rewardAmount;
        float multiplier = StatManager.Instance.GetStat(StatType.GoldGainMultiplier);

        if (planterBrain != null)
        {
            foreach (StatModifier mod in planterBrain.ActiveModifiers)
            {
                if (mod.statType != StatType.GoldGainMultiplier) continue;

                switch (mod.operation)
                {
                    case ModifierOperation.Add:
                        multiplier += mod.value;
                        break;
                    case ModifierOperation.Multiply:
                        multiplier *= mod.value;
                        break;
                    case ModifierOperation.Set:
                        multiplier = mod.value;
                        break;
                }
            }
        }

        reward = Mathf.RoundToInt(reward * multiplier);

        ResourceManager.Instance.AddResource(plantData.resourceType, reward);
        ProgressionManager.Instance.AddXP(plantData.xpAmount);
    }
}