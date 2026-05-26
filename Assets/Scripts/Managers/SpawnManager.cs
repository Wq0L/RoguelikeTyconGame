using System.Collections;
using UnityEngine;

public class SpawnManager : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private GameObject minePrefab;
    [SerializeField] private int maxTryCount = 20;
    [SerializeField] private int poolSize = 20;


    private GridSystem gridSystem;
    private bool isInitialized;
    private bool hasSpawnedInitialMines;
    private Coroutine spawnCoroutine;
    private ObjectPool minePool;
    private Transform mineParent;

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
        isInitialized = true;


        InitializePool();

        GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;

        HandleGameStateChanged(GameManager.Instance.CurrentState);
    }

    private void InitializePool()
    {
        GameObject poolContainer = new GameObject("MinePool");
        minePool = poolContainer.AddComponent<ObjectPool>();
        minePool.prefab = minePrefab;
        minePool.poolSize = poolSize;
        minePool.Initialize();
        mineParent = poolContainer.transform;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
        }

        if (mineParent != null)
        {
            Destroy(mineParent.gameObject);
        }
    }

    private void HandleGameStateChanged(GameStates state)
    {
        if (!isInitialized) return;

        if (state == GameStates.Playing)
        {
            StartSpawning();
        }
        else
        {
            StopSpawning();
        }
    }

    private void StartSpawning()
    {
        if (spawnCoroutine != null) return;

        if (!hasSpawnedInitialMines)
        {
            SpawnInitialMines(Random.Range(5, 11));
            hasSpawnedInitialMines = true;
        }

        spawnCoroutine = StartCoroutine(SpawnLoop());
        Debug.Log("SpawnManager: Spawn başladı.");
    }

    private void StopSpawning()
    {
        if (spawnCoroutine != null)
        {
            StopCoroutine(spawnCoroutine);
            spawnCoroutine = null;
        }

        Debug.Log("SpawnManager: Spawn durdu.");
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            int spawnRate = Mathf.RoundToInt(StatManager.Instance.GetStat(StatType.MineSpawnInterval));

            yield return new WaitForSeconds(spawnRate);
            TrySpawnRandomMine();
        }
    }

    private void TrySpawnRandomMine()
    {
        for (int i = 0; i < maxTryCount; i++)
        {
            int x = Random.Range(0, gridManager.GetWidth());
            int z = Random.Range(0, gridManager.GetHeight());

            GridPosition gridPosition = new GridPosition(x, z);
            GridObject gridObject = gridSystem.GetGridObject(gridPosition);

            if (gridObject == null) continue;
            if (gridObject.HasMineObject()) continue;

            GameObject groundObject = gridObject.GetGroundObject();
            if (groundObject == null) continue;

            GroundCell groundCell = gridObject.GetGroundCellCached();
            if (groundCell == null) continue;

            Transform spawnPoint = groundCell.GetMineSpawnPoint();
            if (spawnPoint == null) continue;

            GameObject mine = minePool.GetObject(spawnPoint.position, spawnPoint.rotation);
            
            PooledObject pooledObject = mine.GetComponent<PooledObject>();
            if (pooledObject == null)
            {
                pooledObject = mine.AddComponent<PooledObject>();
            }
            pooledObject.Initialize(minePool);

            MineBrain mineBrain = mine.GetComponent<MineBrain>();
            if (mineBrain != null)
            {
                mineBrain.SetGridObject(gridObject);
            }

            gridObject.SetMineObject(mine);

            //Debug.Log("Mine spawnlandı: " + x + ", " + z);
            return;
        }

        //Debug.Log("Boş hücre bulunamadı.");
    }

    private void SpawnInitialMines(int count)
    {
        for (int i = 0; i < count; i++)
        {
            TrySpawnRandomMine();
        }
    }
}