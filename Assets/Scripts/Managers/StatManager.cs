using System;
using System.Collections.Generic;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    public static StatManager Instance { get; private set; }

    [SerializeField] private CoreStatsSO coreStatsSO;

    public event Action<StatType, float> OnStatChanged;

    private Dictionary<StatType, float> statDictionary = new();

    private void Awake()
    {
        Instance = this;
        LoadBaseStats();
    }

    private void LoadBaseStats()
    {
        statDictionary.Clear();

        foreach (StatEntry statEntry in coreStatsSO.stats)
        {
            statDictionary[statEntry.statType] = statEntry.value;
        }
    }

    public float GetStat(StatType statType)
    {
        if (!statDictionary.ContainsKey(statType))
        {
            Debug.LogWarning("Stat bulunamadı: " + statType);
            return 0f;
        }

        return statDictionary[statType];
    }

    public void ApplyModifier(StatModifier modifier)
    {
        if (!statDictionary.ContainsKey(modifier.statType))
        {
            Debug.LogWarning("Stat yok, ekleniyor: " + modifier.statType);
            statDictionary[modifier.statType] = 0f;
        }

        switch (modifier.operation)
        {
            case ModifierOperation.Add:
                statDictionary[modifier.statType] += modifier.value;
                break;

            case ModifierOperation.Multiply:
                statDictionary[modifier.statType] *= modifier.value;
                break;
                
            case ModifierOperation.Set:
                statDictionary[modifier.statType] = modifier.value;
                break;
        }

        OnStatChanged?.Invoke(modifier.statType, statDictionary[modifier.statType]);
    }

    public void ApplyModifiers(List<StatModifier> modifiers)
    {
        foreach (StatModifier modifier in modifiers)
        {
            ApplyModifier(modifier);
        }
    }
}