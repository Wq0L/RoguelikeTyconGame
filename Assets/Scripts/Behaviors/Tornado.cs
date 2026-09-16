using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

// Grid üstünde rastgele gezer, girdiği tile'daki bitkiye DamageType.Tornado ile vurur.
// Update yok: hareket coroutine + DOTween. timeScale 0 olunca (shop/menü) kendiliğinden donar.
public class Tornado : MonoBehaviour
{
    [Header("Hareket")]
    [SerializeField] private int maxSteps = 8;             // kaç tile gezecek
    [SerializeField] private float stepDuration = 0.35f;   // iki tile arası geçiş süresi
    [SerializeField] private float stepPause = 0.1f;       // tile üstünde bekleme
    [SerializeField] private float heightOffset = 0f;      // model zemine gömülüyorsa yükselt

    [Header("Hasar")]
    [Tooltip("Vuruş başına hasar = HarvestDamage x bu değer (en az 1)")]
    [SerializeField] private float damageMultiplier = 0.5f;

    [Header("Görsel (opsiyonel)")]
    [SerializeField] private Transform visual;             // dönecek child (model/particle)
    [SerializeField] private float spinDuration = 0.4f;    // bir tam tur süresi

    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 0), new Vector2Int(-1, 0)
    };

    private readonly List<GroundCell> candidates = new();
    private GridSystem gridSystem;
    private GroundCell currentCell;
    private GroundCell previousCell;
    private TornadoManager owner;
    private int damage;
    private WaitForSeconds moveWait;
    private WaitForSeconds pauseWait;

    // TornadoManager spawn eder etmez çağırır
    public void Launch(GroundCell startCell, int baseDamage, TornadoManager manager)
    {
        gridSystem = GridManager.Instance.GetGridSystem();
        currentCell = startCell;
        previousCell = null;
        owner = manager;
        damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));

        // Her adımda new'lemesin diye bir kere oluşturuyoruz (GC dostu)
        moveWait = new WaitForSeconds(stepDuration);
        pauseWait = new WaitForSeconds(stepPause);

        if (visual != null)
        {
            visual.DOLocalRotate(new Vector3(0f, 360f, 0f), spinDuration, RotateMode.FastBeyond360)
                  .SetRelative(true)
                  .SetEase(Ease.Linear)
                  .SetLoops(-1, LoopType.Restart);
        }

        StartCoroutine(WanderRoutine());
    }

    private IEnumerator WanderRoutine()
    {
        for (int step = 0; step < maxSteps; step++)
        {
            GroundCell next = PickNextCell();
            if (next == null) break; // etrafı kilitli, gidecek yer yok

            previousCell = currentCell;
            currentCell = next;

            transform.DOMove(next.transform.position + Vector3.up * heightOffset, stepDuration)
                     .SetEase(Ease.InOutSine);
            yield return moveWait;

            HitCell(next);
            yield return pauseWait;
        }

        Destroy(gameObject);
    }

    private GroundCell PickNextCell()
    {
        candidates.Clear();
        GridPosition pos = currentCell.GetGridPosition();

        foreach (Vector2Int dir in Directions)
        {
            GridObject grid = gridSystem.GetGridObject(new GridPosition(pos.x + dir.x, pos.z + dir.y));
            GroundCell cell = grid?.GetGroundCellCached();

            if (cell == null || cell.IsLocked) continue;
            if (cell == previousCell) continue; // ping-pong yapmasın

            candidates.Add(cell);
        }

        // Çıkmaz sokaktaysa geldiği yere döner (ilk adımda previousCell null → durur)
        if (candidates.Count == 0)
            return previousCell;

        return candidates[Random.Range(0, candidates.Count)];
    }

    private void HitCell(GroundCell cell)
    {
        // Round'un bittiği frame'de son bir vuruş kaçmasın
        if (RoundManager.Instance == null || !RoundManager.Instance.IsRoundActive) return;

        GameObject plant = gridSystem.GetGridObject(cell.GetGridPosition())?.GetPlantObject();
        if (plant == null) return;

        if (plant.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            // Ekrandaki sayı da DamageTypeRules'a göre otomatik doğru çıkar
            int shownDamage = damageable is PlantHealth health
                ? health.GetIncomingDamage(damage, DamageType.Tornado) : damage;

            damageable.TakeDamage(damage, DamageType.Tornado);
            VFXManager.Instance.PlayHit(plant.transform.position, shownDamage, false);
        }
    }

    private void OnDestroy()
    {
        transform.DOKill();
        if (visual != null) visual.DOKill();
        if (owner != null) owner.Unregister(this);
    }
}
