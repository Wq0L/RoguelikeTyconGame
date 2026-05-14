using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : MonoBehaviour
{
    public GameObject prefab;
    public int poolSize = 10;
    private Queue<GameObject> availableObjects = new();
    private HashSet<GameObject> activeObjects = new();

    public void Initialize()
    {
        for (int i = 0; i < poolSize; i++)
        {
            GameObject obj = Instantiate(prefab, transform);
            obj.SetActive(false);
            availableObjects.Enqueue(obj);
        }
    }

    public GameObject GetObject(Vector3 position, Quaternion rotation)
    {
        GameObject obj;

        if (availableObjects.Count > 0)
        {
            obj = availableObjects.Dequeue();
        }
        else
        {
            obj = Instantiate(prefab, transform);
        }

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);
        activeObjects.Add(obj);

        return obj;
    }

    public void ReturnObject(GameObject obj)
    {
        if (!activeObjects.Contains(obj)) return;

        activeObjects.Remove(obj);
        obj.SetActive(false);
        availableObjects.Enqueue(obj);
    }

    public int GetActiveCount() => activeObjects.Count;
    public int GetAvailableCount() => availableObjects.Count;
}
