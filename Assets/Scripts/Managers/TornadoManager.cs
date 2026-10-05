using System.Collections.Generic;
using UnityEngine;

// Tornado'ların tek sahibi: prefab, aynı anda max sayı, round sonu temizlik.
// PlanterBrain sadece "spawn et" der; yaşam döngüsünü burası bilir.
public class TornadoManager : MonoBehaviour
{
    public static TornadoManager Instance { get; private set; }

    [SerializeField] private Tornado tornadoPrefab;
    [Tooltip("Late game'de ekran tornado kaynamasın diye üst sınır")]
    [SerializeField] private int maxActiveTornadoes = 3;

    private readonly List<Tornado> activeTornadoes = new();
    private VFXPool<Tornado> tornadoPool;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        if (tornadoPrefab != null)
            tornadoPool = new VFXPool<Tornado>(tornadoPrefab, Mathf.Max(1, maxActiveTornadoes), transform);
    }

    private void Start()
    {
        // RoundManager Instance'ını Awake'te kuruyor, Start'ta abone olmak güvenli
        if (RoundManager.Instance != null)
            RoundManager.Instance.OnRoundEnded += ClearAll;
    }

    private void OnDestroy()
    {
        if (RoundManager.Instance != null)
            RoundManager.Instance.OnRoundEnded -= ClearAll;

        if (Instance == this) Instance = null;
    }

    // Aynı anda en çok maxActiveTornadoes. Zincir işi doluysa sırasını kaybetmeden bekler (HarvestChain).
    public bool HasCapacity => tornadoPool != null && activeTornadoes.Count < maxActiveTornadoes;

    public bool TrySpawn(GridObject startGrid, int baseDamage) => TrySpawn(startGrid, baseDamage, HarvestLink.None, 1f, 0u);

    // link: vuruşların zincir bağlamı (kasırga kökü yaşadığı sürece tutar). damageFactor: zincir neslinin hasar çarpanı.
    // pathSeed: 0 ise yol UnityEngine.Random'dan (normal tetik, eski akış); değilse zincirin ayrı akışından.
    public bool TrySpawn(GridObject startGrid, int baseDamage, HarvestLink link, float damageFactor, uint pathSeed)
    {
        if (DemoSceneSettings.Blocks(UnlockType.TileBehavior_Tornado)) return false;
        if (startGrid == null || !HasCapacity) return false;

        GroundCell startCell = startGrid.GetGroundCellCached();
        if (startCell == null) return false;
        var source = startGrid.GetPlanterBrain();
        // Rezonans × uzmanlaşma × boss ödülü davranış katsayısı (PlanterBrain.BehaviorDamageMultiplier ile aynı ortak hesap).
        float resonance = source != null ? source.BehaviorDamageMultiplier(DamageType.Tornado) : RunPower.Behavior(1f);

        Tornado tornado = tornadoPool.Get();
        tornado.transform.SetPositionAndRotation(startCell.transform.position, Quaternion.identity);
        activeTornadoes.Add(tornado);
        tornado.Launch(startCell, baseDamage, this, resonance, link, damageFactor, pathSeed);
        return true;
    }

    // Tornado işi bitince kendi OnDestroy'undan çağırır
    public void Unregister(Tornado tornado)
    {
        activeTornadoes.Remove(tornado);
    }

    public void Release(Tornado tornado)
    {
        if (tornado != null && activeTornadoes.Remove(tornado))
            tornadoPool.Return(tornado);
    }

    private void ClearAll()
    {
        for (int i = activeTornadoes.Count - 1; i >= 0; i--)
            Release(activeTornadoes[i]);
        activeTornadoes.Clear();
    }
}
