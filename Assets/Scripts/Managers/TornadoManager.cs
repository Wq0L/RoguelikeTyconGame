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

    public bool TrySpawn(GridObject startGrid, int baseDamage)
    {
        if (tornadoPool == null || startGrid == null) return false;
        if (activeTornadoes.Count >= maxActiveTornadoes) return false;

        GroundCell startCell = startGrid.GetGroundCellCached();
        if (startCell == null) return false;

        Tornado tornado = tornadoPool.Get();
        tornado.transform.SetPositionAndRotation(startCell.transform.position, Quaternion.identity);
        activeTornadoes.Add(tornado);
        tornado.Launch(startCell, baseDamage, this);
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
