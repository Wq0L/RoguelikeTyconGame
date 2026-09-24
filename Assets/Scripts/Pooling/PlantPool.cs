using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// Scene-owned: inactive plants never survive a scene unload. Death returns are
// deferred until LateUpdate so all reward/behavior callbacks see the same life.
[DefaultExecutionOrder(10000)]
public sealed class PlantPool : MonoBehaviour
{
    const int MaxRetainedPerPrefab = 256;
    static readonly Dictionary<int, PlantPool> scenes = new();
    readonly Dictionary<GameObject, Stack<PooledPlant>> available = new();
    readonly List<PooledPlant> pending = new();
    Transform inactiveRoot;
    int sceneHandle;
    public int CreatedCount { get; private set; }
    public int ActiveCount { get; private set; }
    public int InactiveCount { get; private set; }
    public int PendingCount => pending.Count;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => scenes.Clear();

    public static PlantPool ForScene(Scene scene)
    {
        if (scenes.TryGetValue(scene.handle, out var pool) && pool != null) return pool;
        var root = new GameObject("Plant Pool");
        SceneManager.MoveGameObjectToScene(root, scene);
        pool = root.AddComponent<PlantPool>();
        pool.sceneHandle = scene.handle;
        scenes[scene.handle] = pool;
        var storage = new GameObject("Inactive Plants");
        storage.transform.SetParent(root.transform, false);
        storage.SetActive(false);
        pool.inactiveRoot = storage.transform;
        return pool;
    }

    public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!available.TryGetValue(prefab, out var stack))
            available.Add(prefab, stack = new Stack<PooledPlant>());
        PooledPlant item = null;
        while (stack.Count > 0 && item == null) { item = stack.Pop(); InactiveCount--; }
        if (item == null)
        {
            // Inactive parent prevents OnEnable before the spawn data is assigned.
            var go = Instantiate(prefab, inactiveRoot);
            go.SetActive(false);
            item = go.AddComponent<PooledPlant>();
            item.Configure(this, prefab);
            CreatedCount++;
        }
        item.Leased = true;
        item.Pending = false;
        item.transform.SetParent(transform, false);
        item.transform.SetPositionAndRotation(position, rotation);
        item.transform.localScale = prefab.transform.localScale;
        ActiveCount++;
        return item.gameObject; // Caller initializes, enables, then subscribes the spawner.
    }

    public static void Release(GameObject plant)
    {
        if (plant == null) return;
        if (plant.TryGetComponent<PooledPlant>(out var item) && item.Owner != null)
        {
            if (!item.Leased || item.Pending) return;
            item.Pending = true;
            item.Owner.pending.Add(item);
        }
        else Destroy(plant); // Hand-authored/non-pooled plants keep their old lifecycle.
    }

    void LateUpdate()
    {
        for (int i = 0; i < pending.Count; i++)
        {
            var item = pending[i];
            if (item == null) { ActiveCount--; continue; }
            item.gameObject.SetActive(false);
            item.Leased = false;
            item.Pending = false;
            ActiveCount--;
            item.transform.SetParent(inactiveRoot, false);
            var stack = available[item.Prefab];
            if (stack.Count < MaxRetainedPerPrefab) { stack.Push(item); InactiveCount++; }
            else Destroy(item.gameObject);
        }
        pending.Clear();
    }

    void OnDestroy()
    {
        if (scenes.TryGetValue(sceneHandle, out var pool) && pool == this) scenes.Remove(sceneHandle);
    }
}
