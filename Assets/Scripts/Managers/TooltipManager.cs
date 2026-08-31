using System.Collections.Generic;
using UnityEngine;

public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private Transform tooltipParent;

    private Dictionary<GameObject, Queue<GameObject>> pools = new();

    private GameObject activeTooltip;
    private GameObject activePrefabKey;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show(ITooltipProvider provider)
    {
        if (!provider.ShouldShowTooltip()) return;

        Hide();

        GameObject prefab = provider.GetTooltipPrefab();
        if (prefab == null) return;

        GameObject instance = GetFromPool(prefab);
        instance.transform.SetParent(tooltipParent, false);
        instance.transform.position = provider.GetTooltipPosition();
        instance.SetActive(true);

        provider.FillTooltip(instance);

        activeTooltip = instance;
        activePrefabKey = prefab;
    }

    public void Hide()
    {
        if (activeTooltip == null) return;

        activeTooltip.SetActive(false);
        ReturnToPool(activePrefabKey, activeTooltip);

        activeTooltip = null;
        activePrefabKey = null;
    }

    private GameObject GetFromPool(GameObject prefab)
    {
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();

        Queue<GameObject> pool = pools[prefab];

        if (pool.Count > 0)
            return pool.Dequeue();

        return Instantiate(prefab);
    }

    private void ReturnToPool(GameObject prefab, GameObject instance)
    {
        if (!pools.ContainsKey(prefab))
            pools[prefab] = new Queue<GameObject>();

        pools[prefab].Enqueue(instance);
    }
}