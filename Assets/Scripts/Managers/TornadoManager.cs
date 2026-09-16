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

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
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
        if (tornadoPrefab == null || startGrid == null) return false;
        if (activeTornadoes.Count >= maxActiveTornadoes) return false;

        GroundCell startCell = startGrid.GetGroundCellCached();
        if (startCell == null) return false;

        Tornado tornado = Instantiate(tornadoPrefab, startCell.transform.position, Quaternion.identity, transform);
        activeTornadoes.Add(tornado);
        tornado.Launch(startCell, baseDamage, this);
        return true;
    }

    // Tornado işi bitince kendi OnDestroy'undan çağırır
    public void Unregister(Tornado tornado)
    {
        activeTornadoes.Remove(tornado);
    }

    private void ClearAll()
    {
        // Destroy frame sonunda çalışır → döngü sırasında liste değişmez
        foreach (Tornado tornado in activeTornadoes)
        {
            if (tornado != null) Destroy(tornado.gameObject);
        }
        activeTornadoes.Clear();
    }
}
