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
        plantData = null;
        planterBrain = null;
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

        int reward = NumericSafety.ToInt(plantData.rewardAmount * resourceMultiplier, 0, NumericSite.Resource);
        // Denge seti nadirliğe göre XP çarpanı verebilir (Bölüm 3.5); yoksa ×1. XP int'e çevrilmez (eskiden büyük XP negatife
        // taşıyordu): eski yuvarlamayla aynı tam sayı, double olarak. float çarpım taşarsa çarpım double'da yapılır.
        float xpProduct = plantData.xpAmount * xpMultiplier * RunBalanceSO.ActiveXpMultiplier(plantData.rarity);
        double xpAmount = System.Math.Round(float.IsPositiveInfinity(xpProduct)
            ? plantData.xpAmount * (double)xpMultiplier * RunBalanceSO.ActiveXpMultiplier(plantData.rarity)
            : xpProduct);
#if UNITY_EDITOR
        // Sayısal iz (yalnız gözlem): ödül hesabının gerçek ara değerleri.
        int editorRounded = reward;
        double editorXpRounded = xpAmount;
        float editorDuplicateChance = 0f;
#endif

        if (planterBrain != null)
        {
            float dupChance = planterBrain.GetFinalStat(StatType.DuplicateChance);
#if UNITY_EDITOR
            editorDuplicateChance = dupChance;
#endif
            if (dupChance > 0f && Random.value <= dupChance)
            {
                reward = NumericSafety.Add(reward, reward, NumericSite.Resource);
                // Debug.Log("Duplicate! Ödül 2x");
            }
        }

#if UNITY_EDITOR
        int editorAfterDuplicate = reward;
#endif
        // Uzmanlaşma hasat kaynağı çarpanı: duplicate dahil ödüle bir kez, kesirli kalan kaynak başına taşınır. XP ve skor etkilenmez.
        reward = SpecializationManager.ScaleHarvestResource(plantData.resourceType, reward);
#if UNITY_EDITOR
        int editorAfterSpecialization = reward;
#endif

        // Resolve at harvest time, not birth: moving/expired regions never leave a permanent pooled buff.
        GridPosition bountyCell=FieldCells.Grid!=null?FieldCells.Grid.GetGridPosition(transform.position):default;
        var bounty=FieldCells.Grid!=null?FieldEventDirector.BountyAt(bountyCell):null;
        if(bounty!=null)
        {
            reward=NumericSafety.Add(reward,reward,NumericSite.Resource);
            xpAmount*=2d;
        }
#if UNITY_EDITOR
        if (EditorNumericTrace.Listening)
            EditorNumericTrace.Report(new EditorNumericTrace.HarvestReward(plantData, planterBrain, bountyCell, resourceMultiplier, editorRounded,
                editorDuplicateChance, editorAfterDuplicate != editorRounded, editorAfterDuplicate, editorAfterSpecialization, bounty != null, reward,
                xpMultiplier, RunBalanceSO.ActiveXpMultiplier(plantData.rarity), editorXpRounded, xpAmount));
#endif
        ResourceManager.Instance.AddResource(plantData.resourceType, reward, transform.position);
        ProgressionManager.Instance.AddXP(xpAmount);
        bounty?.RecordHarvest(bountyCell);

        HarvestScoreManager.Instance.AddScore(plantData.rarity, planterBrain);
        VFXManager.Instance?.PlayRarityHarvest(plantData.rarity, transform.position);

        // Debug.Log($"Hasat: {plantData.resourceType} x{reward} | XP x{xpAmount} | Multiplier: {resourceMultiplier}");
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
            return planterBrain.GetHarvestXP(plantHealth.KillingElectricXPMultiplier);

        return StatManager.Instance.GetFinalStat(StatType.XPGainMultiplier, StatTarget.Planter) * plantHealth.KillingElectricXPMultiplier;
    }
}
