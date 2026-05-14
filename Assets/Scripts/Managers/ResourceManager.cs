using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManager : MonoBehaviour
{
    public static ResourceManager Instance { get; private set; }
    public event Action<ResourceType, int> OnResourceAmountChanged;
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

    }


    public void AddResource(ResourceType type, int amount)
    {
        if (amount <= 0) return;

        resources[type] += amount;

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

}
