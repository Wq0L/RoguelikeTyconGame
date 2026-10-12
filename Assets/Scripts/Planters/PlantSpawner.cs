using UnityEngine;

public class PlantSpawner : MonoBehaviour
{
    private static readonly System.Collections.Generic.HashSet<PlantSpawner> active = new();
    public static System.Collections.Generic.IReadOnlyCollection<PlantSpawner> Active => active;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => active.Clear();
    private void OnEnable() => active.Add(this);
    private void OnDisable() => active.Remove(this);
    private PlanterSO planterData;
    private GridObject gridObject;
    private PlanterBrain planterBrain;
    private float timer;
    private GameObject spawnedPlant;

    public GridObject GridObject => gridObject;
    public GameObject SpawnedPlant => spawnedPlant;

    // Tarla olayı (Bölüm 3.8, Gezgin Bereket): BOŞ üretim noktası beklemeden, normal üretim yoluyla (aynı bitki zarı, aynı havuz,
    // aynı can ve ödül kurulumu) bir bitki çıkarır. Dolu noktaya ikinci bitki eklenmez; round dışında çalışmaz. Kaynak, XP ya da
    // hasat üretmez: yalnız bitkinin doğuşunu öne alır.
    public bool TryReviveNow()
    {
        if (!enabled || spawnedPlant != null || planterData == null || GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameStates.Round) return false;
        timer = 0f;
        TrySpawnPlant();
        return spawnedPlant != null;
    }

    public void RemoveSpawnedPlant()
    {
        enabled = false;
        GameObject plant = spawnedPlant;
        spawnedPlant = null;
        if (plant == null) return;
        PlantHealth health = plant.GetComponent<PlantHealth>();
        if (health != null) health.OnDied -= OnPlantDied;
        if (gridObject != null && gridObject.GetPlantObject() == plant)
            gridObject.ClearPlantObject();
        // Removal is not harvesting: no XP, resources or explosion events.
        plant.SetActive(false);
        PlantPool.Release(plant);
    }

    private void OnDestroy() => RemoveSpawnedPlant();

    public void Initialize(PlanterSO data, GridObject gridObj, PlanterBrain brain)
    {
        planterData = data;
        gridObject = gridObj;
        planterBrain = brain;
        enabled = true;

        timer = Random.Range(0f, GetEffectiveSpawnInterval());
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Round)
            return;

        if (spawnedPlant != null)
            return;

        // Tarla olayı (Bölüm 3.8: Diken Kuşatması, Aşılı Kabuk): kapalı bölgedeki üretim noktası bekler. Süre birikmez; bölge
        // açılınca sayaç kaldığı yerden sürer (üretim borcu ya da toplu geri ödeme yok).
        if (gridObject != null && FieldEventDirector.BlocksSpawn(gridObject.GetGridPosition()))
            return;

        timer += Time.deltaTime;

        if (timer >= GetEffectiveSpawnInterval())
        {
            timer = 0f;
            TrySpawnPlant();
            if(spawnedPlant!=null && gridObject!=null)FieldEventDirector.BountyAt(gridObject.GetGridPosition())?.RecordSpawn(gridObject.GetGridPosition());
        }
    }

    private float GetEffectiveSpawnInterval()
    {
        float interval = planterBrain != null
            ? planterBrain.GetFinalStat(StatType.PlantSpawnRate)
            : planterData.GetBaseStat(StatType.PlantSpawnRate);
        // 60 sn'yi aşan round süresi üretim hızına dönüşür (RoundManager.TempoMultiplier). Taban önce uygulanır:
        // round başına üretim, round'un 90 sn sürdüğü haliyle aynı kalır.
        float tempo = RoundManager.Instance != null ? RoundManager.Instance.TempoMultiplier : 1f;
        // Segment olayı (Don Cephesi) bu üretim noktasının nihai süresini uzatır: taban ve tempodan sonra uygulanır,
        // böylece tabandaki saksı da gerçekten yavaşlar ve taban taşması → nadirlik hesabına dokunulmaz.
        float eventMultiplier = gridObject != null ? SegmentEventDirector.SpawnIntervalMultiplier(gridObject.GetGridPosition()) : 1f;
        // Tarla olayı (Bölüm 3.8, İstilacı Filiz): boss kuralından ayrı bir çarpan. İkisi ayrı sorulur ve çarpılır; biri kalkınca
        // diğeri aynen sürer. Olay yokken 1'dir (sonuç değişmez).
        float fieldMultiplier = gridObject != null ? FieldEventDirector.SpawnIntervalMultiplier(gridObject.GetGridPosition()) : 1f;
        float bountySpeed=gridObject!=null&&FieldEventDirector.BountyAt(gridObject.GetGridPosition())!=null?WanderingBountyEvent.SpawnSpeed:1f;
        return Mathf.Max(StatCalculator.MinimumSpawnInterval, interval) / tempo * eventMultiplier * fieldMultiplier / bountySpeed;
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

        GameObject plantObj = PlantPool.ForScene(gameObject.scene).Rent(
            selectedPlant.prefab,
            transform.position,
            transform.rotation
        );

        PlantBrain plantBrain = plantObj.GetComponent<PlantBrain>();
        plantBrain?.Initialize(selectedPlant, gridObject);

        PlantHealth plantHealth = plantObj.GetComponent<PlantHealth>();
        if (plantHealth != null)
        {
            // Segment olayı (Sert Kabuk): aktifken bu hücrede doğan bitki daha canlı doğar.
            float healthMultiplier = gridObject != null ? SegmentEventDirector.SpawnHealthMultiplier(gridObject.GetGridPosition()) : 1f;
            plantHealth.Initialize(selectedPlant, planterBrain, healthMultiplier);
        }

        PlantResource plantResource = plantObj.GetComponent<PlantResource>();
        plantResource?.Initialize(selectedPlant, planterBrain);

        plantObj.SetActive(true);
        // Aktivasyondan sonra: PlantHealth model renderer'ını bulmuş olur, aura onunla karışmaz.
        // Aura bitki dinlenme ölçeğindeyken kurulur; ardından topraktan esneyerek fırlama.
        PlantRarityAura.Apply(plantObj.transform, selectedPlant.rarity);
        PlantJuice.Attach(plantObj).PlaySpawn();
        VFXManager.Instance?.PlaySprout(plantObj.transform.position, selectedPlant.rarity);
        // Reward and grid callbacks subscribe in OnEnable, as before pooling.
        if (plantHealth != null) plantHealth.OnDied += OnPlantDied;

        spawnedPlant = plantObj;
        gridObject?.SetPlantObject(plantObj);
    }

    private void OnPlantDied()
    {
        PlantHealth health = spawnedPlant?.GetComponent<PlantHealth>();
        spawnedPlant = null;
        timer = 0f;
        if (health != null) health.OnDied -= OnPlantDied;
        if (planterBrain != null)
            planterBrain.TriggerHarvestBehaviors(gridObject, health);
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
