using System.Collections.Generic;
using UnityEngine;

public class VFXPool<T> where T : Component
{
    public const int Capacity = 121;
    private readonly int capacity;
    public int CreatedCount { get; private set; }
    public int LeasedCount { get; private set; }
    private readonly Queue<T> pool = new();
    private readonly T prefab;
    private readonly Transform parent;

    public VFXPool(T prefab, int size, Transform parent, int maximum = int.MaxValue)
    {
        capacity = maximum;
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < Mathf.Min(size, capacity); i++)
        {
            T obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            pool.Enqueue(obj);
            CreatedCount++;
        }
    }

    public T Get()
    {
        bool create = pool.Count == 0;
        if (create && CreatedCount >= capacity) return null;
        T obj = create ? Object.Instantiate(prefab, parent) : pool.Dequeue();
        if (create) CreatedCount++;
        LeasedCount++;
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        LeasedCount--;
        pool.Enqueue(obj);
    }
}