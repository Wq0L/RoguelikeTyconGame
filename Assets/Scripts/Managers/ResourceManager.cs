using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }
    public event Action<ResourceType, int> OnResourceAmountChanged;
    public event Action<ResourceType, int, Vector3> OnHarvestResourceAdded;
    private Dictionary<ResourceType, int> resources = new();

    private void Awake()
    {
        if (Instance != null)
        {   
            Destroy(gameObject);
            return;
        }
        Instance = this;

        foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
        {
            resources[type] = 0;
        }

        resources[ResourceType.Gold] = 80000;
        resources[ResourceType.Iron] = 80000;
        resources[ResourceType.Stone] = 80000;

    }


    public void AddResource(ResourceType type, int amount, Vector3? harvestPosition = null)
    {
        if (amount <= 0) return;

        resources[type] += amount;

        // Register visual deliveries before notifying counters of the new balance.
        if (harvestPosition.HasValue)
            OnHarvestResourceAdded?.Invoke(type, amount, harvestPosition.Value);
        OnResourceAmountChanged?.Invoke(type, resources[type]);
    }

    public bool SpendResource(ResourceType type, int amount)
    {
        if (amount <= 0) return false;

        if (resources[type] < amount) return false;

        resources[type] -= amount;

        OnResourceAmountChanged?.Invoke(type, resources[type]);
        return true;
    }

    public int GetResourceAmount(ResourceType type)
    {
        return resources[type];
    }
    public bool CanAfford(ResourceType type, int amount)
    {
        return resources[type] >= amount;
    }

}
