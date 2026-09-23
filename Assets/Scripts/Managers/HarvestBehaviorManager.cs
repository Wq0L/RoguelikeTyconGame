using System.Collections.Generic;
using UnityEngine;

// Like TornadoManager: bounded, prewarmed VFXPool instances owned by the scene.
public sealed class HarvestBehaviorManager : MonoBehaviour
{
    public static HarvestBehaviorManager Instance { get; private set; }
    [SerializeField] BoomerangScythe boomerangPrefab;
    [SerializeField] ElectricBurst electricPrefab;
    [SerializeField, Min(1)] int maxBoomerangs = 6;
    [SerializeField, Min(1)] int maxElectricBursts = 8;
    readonly List<BoomerangScythe> boomerangs = new();
    readonly List<ElectricBurst> electricity = new();
    readonly List<GridPosition> footprint = new(), targets = new(), origins = new();
    VFXPool<BoomerangScythe> boomerangPool;
    VFXPool<ElectricBurst> electricPool;
    RoundManager rounds;
    GameManager game;
    public int ActiveBoomerangs => boomerangs.Count;
    public int ActiveElectricBursts => electricity.Count;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (boomerangPrefab != null) boomerangPool = new(boomerangPrefab, maxBoomerangs, transform);
        if (electricPrefab != null) electricPool = new(electricPrefab, maxElectricBursts, transform);
    }
    void Start()
    {
        rounds = RoundManager.Instance; if (rounds != null) rounds.OnRoundEnded += ClearAll;
        game = GameManager.Instance; if (game != null) game.OnGameStateChanged += StateChanged;
    }
    void StateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu || state == GameStates.RunSetup || state == GameStates.RunComplete) ClearAll();
    }
    void OnDisable() => ClearAll();
    void OnDestroy()
    {
        if (rounds != null) rounds.OnRoundEnded -= ClearAll;
        if (game != null) game.OnGameStateChanged -= StateChanged;
        if (Instance == this) Instance = null;
    }
    public void ClearAll()
    {
        while (boomerangs.Count > 0) Release(boomerangs[boomerangs.Count - 1]);
        while (electricity.Count > 0) Release(electricity[electricity.Count - 1]);
    }
    public bool TryBoomerang(PlanterBrain source, GridObject start, int damage)
    {
        if (!isActiveAndEnabled || boomerangPool == null || boomerangs.Count >= maxBoomerangs ||
            RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive || source == null) return false;
        var manager = GridManager.Instance;
        var cell = start?.GetGroundCellCached();
        if (manager == null || cell == null) return false;
        // Pick a cardinal direction and a two/three-cell range independently.
        var grid = manager.GetGridSystem();
        int direction = Random.Range(0, 4), distance = Random.Range(2, 4);
        Vector3 axis = direction < 2
            ? grid.GetWorldPosition(1, 0) - grid.GetWorldPosition(0, 0)
            : grid.GetWorldPosition(0, 1) - grid.GetWorldPosition(0, 0);
        if ((direction & 1) != 0) axis = -axis;
        Vector3 destination = cell.transform.position + axis * distance;
        var effect = boomerangPool.Get(); boomerangs.Add(effect);
        effect.Launch(this, source, cell.transform.position, destination, damage);
        return true;
    }
    public bool TryElectric(PlanterBrain source, int damage)
    {
        if (!isActiveAndEnabled || electricPool == null || electricity.Count >= maxElectricBursts || source == null ||
            RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive || GridManager.Instance == null) return false;
        footprint.Clear();
        foreach (var occupied in source.OccupiedGrids)
        {
            var cell = occupied?.GetGroundCellCached();
            if (cell != null) footprint.Add(cell.GetGridPosition());
        }
        HarvestBehaviorGeometry.ElectricCells(footprint, targets, origins);
        if (targets.Count == 0) return false;
        var effect = electricPool.Get(); electricity.Add(effect);
        effect.Launch(this, source, GridManager.Instance.GetGridSystem(), targets, origins, damage);
        return true;
    }
    public void Release(BoomerangScythe effect) { if (boomerangs.Remove(effect)) boomerangPool.Return(effect); }
    public void Release(ElectricBurst effect) { if (electricity.Remove(effect)) electricPool.Return(effect); }
}
