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
    // Aynı anda en çok maxBoomerangs. Zincir işi doluysa sırasını kaybetmeden bekler (HarvestChain).
    public bool HasBoomerangCapacity => isActiveAndEnabled && boomerangPool != null && boomerangs.Count < maxBoomerangs;

    public bool TryBoomerang(PlanterBrain source, GridObject start, int damage) => TryBoomerang(source, start, damage, HarvestLink.None, 1f, 0u);

    // link: vuruşların zincir bağlamı. damageFactor: zincir neslinin hasar çarpanı. directionSeed: 0 ise yön ve menzil
    // UnityEngine.Random'dan (normal tetik, eski akış); değilse zincirin ayrı akışından.
    public bool TryBoomerang(PlanterBrain source, GridObject start, int damage, HarvestLink link, float damageFactor, uint directionSeed)
    {
        if (DemoSceneSettings.Blocks(UnlockType.TileBehavior_Boomerang)) return false;
        if (!HasBoomerangCapacity || RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive || source == null) return false;
        var manager = GridManager.Instance;
        var cell = start?.GetGroundCellCached();
        if (manager == null || cell == null) return false;
        // Pick a cardinal direction and a two/three-cell range independently.
        var grid = manager.GetGridSystem();
        int direction, distance;
        if (directionSeed != 0) { direction = ChainRandom.Range(ref directionSeed, 0, 4); distance = ChainRandom.Range(ref directionSeed, 2, 4); }
        else { direction = Random.Range(0, 4); distance = Random.Range(2, 4); }
        Vector3 axis = direction < 2
            ? grid.GetWorldPosition(1, 0) - grid.GetWorldPosition(0, 0)
            : grid.GetWorldPosition(0, 1) - grid.GetWorldPosition(0, 0);
        if ((direction & 1) != 0) axis = -axis;
        Vector3 destination = cell.transform.position + axis * distance;
        var effect = boomerangPool.Get(); boomerangs.Add(effect);
        effect.Launch(this, source, cell.transform.position, destination, damage, link, damageFactor);
        return true;
    }

    public bool TryElectric(PlanterBrain source, int damage) => TryElectric(source, damage, HarvestLink.None, 1f);

    // Elektrik vuruşu anlıktır: hasar görselden bağımsız uygulanır. Efekt havuzu doluysa (maxElectricBursts)
    // yalnız çizim atlanır, hedefler yine vurulur. damageFactor: zincir neslinin hasar çarpanı (normal davranış hasarının üzerine).
    public bool TryElectric(PlanterBrain source, int damage, HarvestLink link, float damageFactor)
    {
        if (!isActiveAndEnabled || source == null ||
            RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive || GridManager.Instance == null) return false;
        footprint.Clear();
        foreach (var occupied in source.OccupiedGrids)
        {
            var cell = occupied?.GetGroundCellCached();
            if (cell != null) footprint.Add(cell.GetGridPosition());
        }
        HarvestBehaviorGeometry.ElectricCells(footprint, targets, origins);
        if (targets.Count == 0) return false;
        ElectricBurst effect = electricPool != null && electricity.Count < maxElectricBursts ? electricPool.Get() : null;
        if (effect != null) electricity.Add(effect);
        else SkippedElectricVisuals++;
        // Normal tetikte hasar Strike içinde bir kez katsayılarla hesaplanır (eski yol). Zincirde önce normal davranış hasarı
        // hesaplanır, çarpan bir kez uygulanır ve hesaplanmış hasar olarak verilir.
        bool chained = link.IsChain;
        int final = chained ? HarvestChain.Scale(source.GetBehaviorDamage(damage, DamageType.Electric), damageFactor) : damage;
        ElectricBurst.Strike(this, effect, source, GridManager.Instance.GetGridSystem(), targets, origins, final, chained, link, out _, out _, out _);
        return true;
    }
    // Çifte Akım'ın ikinci dalgası: aynı saksıdan, aynı çapraz yönlerde, HESAPLANMIŞ hasarla (katsayılar yeniden uygulanmaz).
    // reach: ışın başına hücre (0: ilk dalgayla aynı). Yeni hedefe yönelme, sıçrayış ya da zincir yoktur: yalnız ışınlar uzar.
    // Hedefler dalga anında değerlendirilir. Görsel havuzu doluysa yalnız çizim atlanır, hasar uygulanır. Hedef hücre yoksa false.
    public bool TryElectricEcho(PlanterBrain source, int finalDamage, int reach, out int struck, out int killed, out int cells)
    {
        struck = killed = cells = 0;
        if (!isActiveAndEnabled || source == null ||
            RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive || GridManager.Instance == null) return false;
        footprint.Clear();
        foreach (var occupied in source.OccupiedGrids)
        {
            var cell = occupied?.GetGroundCellCached();
            if (cell != null) footprint.Add(cell.GetGridPosition());
        }
        HarvestBehaviorGeometry.ElectricCells(footprint, targets, origins, reach > 0 ? reach : HarvestBehaviorGeometry.ElectricReachCells);
        if (targets.Count == 0) return false;
        ElectricBurst effect = electricPool != null && electricity.Count < maxElectricBursts ? electricPool.Get() : null;
        if (effect != null) electricity.Add(effect);
        else SkippedElectricVisuals++;
        // İkinci dalganın vuruşları zincire katılmaz (HarvestLink.Aftershock).
        ElectricBurst.Strike(this, effect, source, GridManager.Instance.GetGridSystem(), targets, origins, finalDamage, true, HarvestLink.Aftershock, out struck, out killed, out cells);
        return true;
    }
    public int SkippedElectricVisuals { get; private set; }
    // Bir dalgada çizim kapasitesini (ElectricBurst.MaxBolts) aşan hedef sayısı: hasar uygulanır, yalnız o şimşek çizilmez.
    public int SkippedElectricBolts { get; private set; }
    public void CountSkippedBolts(int count) => SkippedElectricBolts += count;
    public void Release(BoomerangScythe effect) { if (boomerangs.Remove(effect)) boomerangPool.Return(effect); }
    public void Release(ElectricBurst effect) { if (electricity.Remove(effect)) electricPool.Return(effect); }
}
