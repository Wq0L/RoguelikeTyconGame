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
    [SerializeField] private Transform loopVfxPrefab;
    [SerializeField, Min(0.01f)] private float vfxScale = 0.3f;
    private ParticleSystem[] particles;

    private void Awake()
    {
        if (loopVfxPrefab != null)
        {
            Transform effect = Instantiate(loopVfxPrefab, transform);
            effect.localPosition = Vector3.zero;
            effect.localRotation = Quaternion.identity;
            effect.localScale = Vector3.one * vfxScale;
        }
        particles = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particle in particles)
        {
            var main = particle.main;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

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
    public void Launch(GroundCell startCell, int baseDamage, TornadoManager manager, float resonanceMultiplier = 1f)
    {
        gridSystem = GridManager.Instance.GetGridSystem();
        currentCell = startCell;
        previousCell = null;
        owner = manager;
        damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * damageMultiplier));
        damage = (int)System.Math.Min(int.MaxValue, System.Math.Round(damage * (double)resonanceMultiplier));
        transform.position = startCell.transform.position + Vector3.up * heightOffset;
        foreach (var particle in particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(false);
        }

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

        owner.Release(this);
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

    private void OnDisable()
    {
        StopAllCoroutines();
        transform.DOKill();
        if (visual != null) visual.DOKill();
        if (particles != null)
            foreach (var particle in particles)
                particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void OnDestroy()
    {
        if (owner != null) owner.Unregister(this);
    }
}
