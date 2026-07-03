using System.Collections.Generic;
using UnityEngine;

public class VFXPool<T> where T : Component
{
    private readonly Queue<T> pool = new();
    private readonly T prefab;
    private readonly Transform parent;

    public VFXPool(T prefab, int size, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;

        for (int i = 0; i < size; i++)
        {
            T obj = Object.Instantiate(prefab, parent);
            obj.gameObject.SetActive(false);
            pool.Enqueue(obj);
        }
    }

    public T Get()
    {
        T obj = pool.Count > 0 ? pool.Dequeue() : Object.Instantiate(prefab, parent);
        obj.gameObject.SetActive(true);
        return obj;
    }

    public void Return(T obj)
    {
        obj.gameObject.SetActive(false);
        pool.Enqueue(obj);
    }
}