using System.Collections.Generic;
using UnityEngine;

public class PlanterBrain : MonoBehaviour
{
    [SerializeField] private PlanterSO planterData;
    [Tooltip("Bos birakilirsa her dolu hucrenin merkezinde bir bitki noktasi olusturulur.")]
    [SerializeField] private List<Transform> spawnPoints = new List<Transform>();
    [SerializeField] private float automaticSpawnHeight = 0.25f;

    private List<PlantSpawner> spawners = new List<PlantSpawner>();
    private List<StatModifier> localModifiers = new List<StatModifier>();
    private List<GridObject> occupiedGrids = new List<GridObject>();
    [Header("Resonance")]
    [Tooltip("Optional override. Empty uses Resources/ResonanceRules.")]
    [SerializeField] private ResonanceRulesSO resonanceRules;
    [SerializeField] private List<ActiveResonance> activeResonances = new();
    private readonly List<StatModifier> resonanceModifiers = new();
    private readonly Dictionary<TileModifierType, int> tileCounts = new();
    private readonly HashSet<GridObject> uniqueGrids = new();
    private readonly List<ActiveResonance> previousResonances = new();
    public IReadOnlyList<ActiveResonance> ActiveResonances => activeResonances;
    public ResonanceRulesSO Rules => resonanceRules != null ? resonanceRules : ResonanceManager.DefaultRules;
    public IReadOnlyList<GridObject> OccupiedGrids => occupiedGrids;

    // Cache
    private Dictionary<StatType, float> statCache = new();
    private int cachedVersion = -1;
    private bool localDirty = true;
    private bool removed;

    public List<StatModifier> LocalModifiers => localModifiers; 

    public void Initialize(List<GridObject> gridObjects)
    {
        if (spawners.Count > 0) return;
        if (!TryResolveSpawnGrids(gridObjects, out List<GridObject> spawnGrids, out string error))
        {
            Debug.LogError(error, this);
            return;
        }
        occupiedGrids = new List<GridObject>(gridObjects);
        RefreshTileBuffs();
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            foreach (GridObject grid in gridObjects)
            {
                GroundCell cell = grid.GetGroundCellCached();
                if (cell != null)
                    CreateSpawner(cell.transform.position + Vector3.up * automaticSpawnHeight, grid);
            }
            return;
        }
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            CreateSpawner(spawnPoints[i].position, spawnGrids[i]);
        }
    }

    public bool ValidateSpawnPoints(List<GridObject> gridObjects, out string error)
    {
        return TryResolveSpawnGrids(gridObjects, out _, out error);
    }

    // Resolve every point before creating any spawners. Never redirect a duplicate
    // to a different cell: its visual position would no longer match its grid.
    private bool TryResolveSpawnGrids(List<GridObject> gridObjects,
        out List<GridObject> spawnGrids, out string error)
    {
        spawnGrids = new List<GridObject>();
        error = null;
        if (spawnPoints == null || spawnPoints.Count == 0) return true;

        Dictionary<GridObject, int> assignedPoints = new Dictionary<GridObject, int>();
        for (int i = 0; i < spawnPoints.Count; i++)
        {
            Transform point = spawnPoints[i];
            if (point == null)
            {
                error = $"[{name}] Yerleştirme durduruldu: Spawn Points listesinin {i + 1}. elemanı boş. " +
                    "Noktayı bağla veya otomatik noktalar için listenin tamamını boşalt.";
                return false;
            }

            GridObject closest = FindClosestGridObject(point.position, gridObjects);
            if (closest == null)
            {
                error = $"[{name}] Yerleştirme durduruldu: '{point.name}' (nokta {i + 1}) için zemin hücresi bulunamadı.";
                return false;
            }
            if (assignedPoints.TryGetValue(closest, out int previous))
            {
                GridPosition cell = closest.GetGroundCellCached().GetGridPosition();
                error = $"[{name}] Yerleştirme durduruldu: '{spawnPoints[previous].name}' (nokta {previous + 1}) ve " +
                    $"'{point.name}' (nokta {i + 1}) aynı ({cell.x}, {cell.z}) hücresine denk geliyor. " +
                    "Başka hücreye aktarılmadı. Prefabta noktaları ayrı hücre merkezlerine taşı " +
                    "veya Spawn Points listesini boşaltarak otomatik noktaları kullan.";
                return false;
            }
            assignedPoints.Add(closest, i);
            spawnGrids.Add(closest);
        }
        return true;
    }

    public void Initialize(PlanterSO data, List<GridObject> gridObjects)
    {
        planterData = data;
        Initialize(gridObjects);
    }

    private void CreateSpawner(Vector3 position, GridObject grid)
    {
        GameObject spawnerObj = new GameObject("PlantSpawner_" + spawners.Count);
        spawnerObj.transform.position = position;
        spawnerObj.transform.SetParent(transform);
        PlantSpawner spawner = spawnerObj.AddComponent<PlantSpawner>();
        spawner.Initialize(planterData, grid, this);
        spawners.Add(spawner);
    }

    public void ApplyBuff(TileModifierSO tileModifier, List<StatModifier> rolledModifiers)
    {
        // Compatibility entry point: occupied cells are the source of truth, not an append-only list.
        RefreshTileBuffs();
    }

    [ContextMenu("Refresh Tile Buffs and Resonance")]
    public void RefreshTileBuffs()
    {
        previousResonances.Clear();
        previousResonances.AddRange(activeResonances);
        localModifiers.Clear();
        tileCounts.Clear();
        uniqueGrids.Clear();
        foreach (GridObject grid in occupiedGrids)
        {
            if (grid == null || !uniqueGrids.Add(grid)) continue;
            GroundCell cell = grid.GetGroundCellCached();
            if (cell == null || cell.CurrentModifier == null) continue;
            localModifiers.AddRange(cell.RolledModifiers);
            TileModifierType type = cell.CurrentModifier.modifierType;
            tileCounts.TryGetValue(type, out int count);
            tileCounts[type] = count + 1;
        }
        ResonanceManager.Evaluate(Rules,
            tileCounts, resonanceModifiers, activeResonances);
        localDirty = true;
        PlayNewResonances();
    }

    private void PlayNewResonances()
    {
        if (!Application.isPlaying || VFXManager.Instance == null) return;
        VFXManager.Instance.RequestResonance(this, previousResonances);
    }

    public bool TryGetResonancePresentation(IReadOnlyList<ActiveResonance> previous,
        out Bounds bounds, out Color color, out string message)
    {
        bounds = default;
        message = "";
        color = new Color(.4f, .85f, 1f);
        bool hasNew = false;
        foreach (var resonance in activeResonances)
            hasNew |= ResonanceManager.IsNewTier(resonance, previous);
        if (!hasNew) return false;
        foreach (var resonance in activeResonances)
        {
            if (message.Length > 0) message += "\n";
            message += resonance.resonanceName + "\n<color=#B9FFCB>" + TileBuffText.Resonance(resonance) + "</color>";
            if (resonance.tileType == TileModifierType.Damage) color = new Color(1f, .65f, .2f);
            else if (resonance.tileType == TileModifierType.Fertile) color = new Color(.45f, 1f, .55f);
        }
        if (message.Length == 0) return false;
        message = "<size=125%><color=#FFE36A>REZONANS!</color></size>\n" + message;
        bool found = false;
        foreach (var grid in occupiedGrids)
        {
            var cell = grid?.GetGroundCellCached();
            if (cell == null) continue;
            if (!found) { bounds = new Bounds(cell.transform.position, Vector3.zero); found = true; }
            else bounds.Encapsulate(cell.transform.position);
        }
        return found;
    }

    public float GetFinalStat(StatType statType)
    {
        int currentVersion = StatManager.Instance.GlobalVersion;

        if (currentVersion != cachedVersion || localDirty)
        {
            statCache.Clear();
            cachedVersion = currentVersion;
            localDirty = false;
        }

        if (statCache.TryGetValue(statType, out float cached))
            return cached;

        float result = GetOrdinaryStat(statType);

        // Only strongest unconditional resonance per stat is in this list.
        result = StatCalculator.Calculate(result, statType, StatTarget.Planter, null, resonanceModifiers);

        statCache[statType] = result;
        return result;
    }

    public float GetOrdinaryStat(StatType statType)
    {
        return StatCalculator.Calculate(
            planterData.GetBaseStat(statType),
            statType,
            StatTarget.Planter,
            StatManager.Instance != null ? StatManager.Instance.GlobalModifiers : null,
            localModifiers
        );

    }

    public float GetHarvestXP(float sourceElectricMultiplier = 1f) => GetOrdinaryStat(StatType.XPGainMultiplier) *
        Mathf.Max(sourceElectricMultiplier, ResonanceManager.Multiplier(activeResonances, StatType.XPGainMultiplier));

    public float GetHarvestScore(PlantRarity rarity) => StatCalculator.Calculate(
        planterData.GetBaseStat(StatType.HarvestScoreMultiplier), StatType.HarvestScoreMultiplier, StatTarget.Planter,
        StatManager.Instance != null ? StatManager.Instance.GlobalModifiers : null, localModifiers, false) *
        ResonanceManager.Multiplier(activeResonances, StatType.HarvestScoreMultiplier, rarity);

    public int GetBehaviorDamage(int baseDamage, DamageType type) => (int)System.Math.Min(int.MaxValue,
        System.Math.Max(0, System.Math.Round(baseDamage * (double)ResonanceManager.BehaviorMultiplier(activeResonances, type))));

    public void RemoveSelf()
    {
        if (removed) return;
        CleanupPlacement();
        if (planterData != null && ResourceManager.Instance != null)
            ResourceManager.Instance.AddResource(planterData.costType, planterData.cost / 2);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy() => CleanupPlacement();

    private void CleanupPlacement()
    {
        if (removed) return;
        removed = true;
        foreach (PlantSpawner spawner in spawners)
            if (spawner != null) spawner.RemoveSpawnedPlant();
        spawners.Clear();

        foreach (GridObject grid in occupiedGrids)
        {
            if (grid == null) continue;
            // A delayed destruction must not clear a replacement planter's ownership.
            if (grid.GetPlanterBrain() == this || grid.GetPlanterObject() == gameObject)
                grid.ClearPlanterObject();
        }
        occupiedGrids.Clear();
        localModifiers.Clear();
        resonanceModifiers.Clear();
        activeResonances.Clear();
        previousResonances.Clear();
        tileCounts.Clear();
        uniqueGrids.Clear();
        statCache.Clear();
    }

    private GridObject FindClosestGridObject(Vector3 worldPos, List<GridObject> gridObjects)
    {
        GridObject closest = null;
        float minDist = float.MaxValue;

        foreach (GridObject gridObj in gridObjects)
        {
            GroundCell cell = gridObj.GetGroundCellCached();
            if (cell == null) continue;

            Vector3 delta = worldPos - cell.transform.position;
            float dist = delta.x * delta.x + delta.z * delta.z;
            if (dist < minDist)
            {
                minDist = dist;
                closest = gridObj;
            }
        }

        return closest;
    }

    private bool IsValidHarvestSource(GridObject sourceGrid, PlantHealth sourcePlant)
    {
        return !removed && sourcePlant != null && sourcePlant.IsDead &&
            sourcePlant.KilledBy.CanTriggerBehaviors() && sourcePlant.Owner == this &&
            sourceGrid != null && occupiedGrids.Contains(sourceGrid) &&
            sourceGrid.GetPlanterBrain() == this;
    }

    public void TryExplode(GridObject sourceGrid, PlantHealth sourcePlant)
    {
        if (!IsValidHarvestSource(sourceGrid, sourcePlant)) return;
        float chance = GetFinalStat(StatType.ExplosionChance);
        if (chance <= 0f) return;
        if (Random.value > chance) return; // şans tutmadı

        VFXManager.Instance?.PlayExplosion(sourcePlant.transform.position, true);

        int damage = Mathf.RoundToInt(
            StatManager.Instance.GetFinalStat(StatType.HarvestDamage, StatTarget.Player)
        );

        GridSystem gridSystem = GridManager.Instance.GetGridSystem();
        damage = GetBehaviorDamage(damage, DamageType.Explosion);
        HashSet<GridObject> hitTargets = new HashSet<GridObject>();

        foreach (GridObject occupiedGrid in occupiedGrids)
        {
            GroundCell cell = occupiedGrid.GetGroundCellCached();
            if (cell == null) continue;

            GridPosition pos = cell.GetGridPosition();

            Vector2Int[] directions = {
                new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 0), new Vector2Int(-1, 0)
            };

            foreach (Vector2Int dir in directions)
            {
                GridPosition neighborPos = new GridPosition(pos.x + dir.x, pos.z + dir.y);
                GridObject neighbor = gridSystem.GetGridObject(neighborPos);

                if (neighbor == null) continue;
                if (occupiedGrids.Contains(neighbor)) continue;
                if (!hitTargets.Add(neighbor)) continue;

                GameObject plant = neighbor.GetPlantObject();
                if (plant == null) continue;

                IDamageable damageable = plant.GetComponent<IDamageable>();
                if (damageable == null || (damageable is PlantHealth health && health.IsDead)) continue;
                VFXManager.Instance?.PlayExplosion(plant.transform.position, false);
                damageable.TakeDamage(damage, DamageType.Explosion);
            }
        }
    }

    public void TriggerHarvestBehaviors(GridObject sourceGrid, PlantHealth sourcePlant)
    {
        TryExplode(sourceGrid, sourcePlant);
        TryTornado(sourceGrid, sourcePlant);
        if (IsValidHarvestSource(sourceGrid, sourcePlant) && HarvestBehaviorManager.Instance != null)
        {
            int damage = Mathf.RoundToInt(StatManager.Instance.GetFinalStat(StatType.HarvestDamage, StatTarget.Player));
            float boomerang = GetFinalStat(StatType.BoomerangChance);
            if (boomerang > 0f && Random.value < boomerang)
                HarvestBehaviorManager.Instance.TryBoomerang(this, sourceGrid, damage);
            float electric = GetFinalStat(StatType.ElectricChance);
            if (electric > 0f && Random.value < electric)
                HarvestBehaviorManager.Instance.TryElectric(this, damage);
        }
    }

    public void TryTornado(GridObject sourceGrid, PlantHealth sourcePlant)
    {
        
        if (!IsValidHarvestSource(sourceGrid, sourcePlant)) return;

        float chance = GetFinalStat(StatType.TornadoChance);
        if (chance <= 0f) return;
        if (Random.value > chance) return; // şans tutmadı
        if (TornadoManager.Instance == null) return;

        int damage = Mathf.RoundToInt(
            StatManager.Instance.GetFinalStat(StatType.HarvestDamage, StatTarget.Player)
        );

        TornadoManager.Instance.TrySpawn(sourceGrid, damage);
    }

    public void SetGridObject(GridObject gridObject)
    {
        Initialize(new List<GridObject> { gridObject });
    }
}
