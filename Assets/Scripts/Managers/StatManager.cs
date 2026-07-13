using System;
using System.Collections.Generic;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    public static StatManager Instance { get; private set; }

    [SerializeField] private CoreStatsSO coreStatsSO;

    private readonly List<StatModifier> globalModifiers = new();

    public IReadOnlyList<StatModifier> GlobalModifiers => globalModifiers;

    public event Action<StatModifier> OnGlobalModifierAdded;
    public event Action<StatModifier> OnGlobalModifierRemoved;
    public event Action OnGlobalModifiersCleared;
    public event Action<StatType, float> OnStatChanged;

    private int globalVersion;
    public int GlobalVersion => globalVersion;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public float GetBaseStat(StatType statType)
    {
        if (coreStatsSO == null)
        {
            Debug.LogWarning("CoreStatsSO atanmadı.");
            return StatDefaults.GetDefaultBase(statType);
        }

        return coreStatsSO.GetBaseStat(statType);
    }

    public float GetFinalStat(StatType statType, StatTarget target)
    {
        float baseValue = GetBaseStat(statType);

        return StatCalculator.Calculate(
            baseValue,
            statType,
            target,
            globalModifiers,
            null
        );
    }

    public void AddGlobalModifier(StatModifier modifier)
    {
        globalModifiers.Add(modifier);

        globalVersion++;

        float newValue = GetFinalStat(modifier.statType, modifier.target);

        Debug.Log(
            $"Global modifier eklendi: {modifier.statType} | {modifier.target} | {modifier.operation} | {modifier.value} | Final: {newValue}"
        );

        OnGlobalModifierAdded?.Invoke(modifier);
        OnStatChanged?.Invoke(modifier.statType, newValue);
    }

    public void AddGlobalModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            AddGlobalModifier(modifiers[i]);
        }
    }

    public void RemoveGlobalModifier(StatModifier modifier)
    {
        bool removed = globalModifiers.Remove(modifier);

        if (!removed)
        {
            Debug.LogWarning($"RemoveGlobalModifier: modifier listede bulunamadı — {modifier.statType} | {modifier.value}");
            return;
        }

        globalVersion++;

        float newValue = GetFinalStat(modifier.statType, modifier.target);

        Debug.Log(
            $"Global modifier çıkarıldı: {modifier.statType} | {modifier.target} | {modifier.operation} | {modifier.value} | Final: {newValue}"
        );

        OnGlobalModifierRemoved?.Invoke(modifier);
        OnStatChanged?.Invoke(modifier.statType, newValue);
    }

    public void RemoveGlobalModifiers(List<StatModifier> modifiers)
    {
        if (modifiers == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            RemoveGlobalModifier(modifiers[i]);
        }
    }

    public void ClearGlobalModifiers()
    {
        globalModifiers.Clear();

        globalVersion++;

        OnGlobalModifiersCleared?.Invoke();

        if (coreStatsSO == null)
            return;

        foreach (StatEntry statEntry in coreStatsSO.stats)
        {
            float value = GetFinalStat(statEntry.statType, StatTarget.All);
            OnStatChanged?.Invoke(statEntry.statType, value);
        }
    }
}