using System;
using System.Collections.Generic;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [SerializeField] private GameObject groundPrefab;
    [SerializeField] private int width = 10;
    [SerializeField] private int height = 10;
    private GridSystem gridSystem;
    private ObjectPool groundPool;
    private Transform groundParent;

    private void Awake()
    {
        gridSystem = new GridSystem(width, height, 2f);
        InitializeGroundPool();
        SpawnGrounds();
    }

    private void InitializeGroundPool()
    {
        GameObject poolContainer = new GameObject("GroundPool");
        groundPool = poolContainer.AddComponent<ObjectPool>();
        groundPool.prefab = groundPrefab;
        groundPool.poolSize = width * height;
        groundPool.Initialize();
        groundParent = poolContainer.transform;
    }

    void Update()
    {
       
    }

    private void SpawnGrounds()
    {
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                GridPosition gridPosition = new GridPosition(x, z);

                Vector3 worldPosition = gridSystem.GetWorldPosition(x, z);

                GameObject ground = groundPool.GetObject(
                    worldPosition,
                    Quaternion.identity
                );

                GridObject gridObject = gridSystem.GetGridObject(gridPosition);

                gridObject.SetGroundObject(ground);
            }
        }
    }

    public GridSystem GetGridSystem()
    {
        return gridSystem;
    }

    public int GetWidth()
    {
        return width;
    }

    public int GetHeight()
    {
        return height;
    }

    private void OnDestroy()
    {
        if (groundParent != null)
        {
            Destroy(groundParent.gameObject);
        }
    }
}