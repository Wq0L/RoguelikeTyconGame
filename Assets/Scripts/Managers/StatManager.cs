using System;
using System.Collections.Generic;
using UnityEngine;

public class StatManager : MonoBehaviour
{
    public static StatManager Instance { get; private set; }

    [SerializeField] private CoreStatsSO coreStatsSO;

    private readonly List<StatModifier> globalModifiers = new();
    private readonly Dictionary<(StatType, StatTarget), (int version, float baseValue, float result)> cache = new();

    public IReadOnlyList<StatModifier> GlobalModifiers => globalModifiers;

    public event Action<StatModifier> OnGlobalModifierAdded;
    public event Action<StatModifier> OnGlobalModifierRemoved;
    public event Action OnGlobalModifiersCleared;
    // Değer: stat temel settedeyse (CoreStatsSO) global son değer. Saksıya ait statlarda (davranış şansı gibi; tabanı saksı
    // verisinde, son değeri saksıya göre değişir) tek bir global değer yoktur: NaN gelir, gerçek değer PlanterBrain.GetFinalStat'tadır.
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
        // Run profilinin denge seti kendi temel statlarını getirebilir (Bölüm 3.5); yoksa sahnedeki CoreStat.
        RunBalanceSO balance = RunBalanceSO.Active;
        if (balance != null && balance.coreStats != null) coreStatsSO = balance.coreStats;
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

    // Temel stat setinin bu stat için tabanı var mı. Yoksa stat saksıya aittir; oyuncu / global taban olarak sorgulanmaz.
    public bool HasBaseStat(StatType statType) => coreStatsSO == null || coreStatsSO.TryGetBaseStat(statType, out _);

    // Değişiklik bildiriminin değeri. Saksıya ait bir stat için (Kıvılcım: Tornado / Bumerang / Elektrik şansı) global taban
    // sorgulanmaz: eskiden sorgulanıyor, CoreStatsSO "taban bulunamadı" uyarısı veriyor ve anlamsız bir 0 hesaplanıyordu.
    private float NotificationValue(StatModifier modifier) =>
        HasBaseStat(modifier.statType) ? GetFinalStat(modifier.statType, modifier.target) : float.NaN;

    public float GetFinalStat(StatType statType, StatTarget target)
    {
        float baseValue = GetBaseStat(statType);
        var key = (statType, target);
        if (cache.TryGetValue(key, out var cached) && cached.version == globalVersion && cached.baseValue == baseValue)
            return cached.result;
        float result = StatCalculator.Calculate(
            baseValue,
            statType,
            target,
            globalModifiers,
            null
        );
        cache[key] = (globalVersion, baseValue, result);
        return result;
    }

    public void AddGlobalModifier(StatModifier modifier)
    {
        globalModifiers.Add(modifier);

        globalVersion++;

        float newValue = NotificationValue(modifier);

        // Debug.Log(
            // $"Global modifier eklendi: {modifier.statType} | {modifier.target} | {modifier.operation} | {modifier.value} | Final: {newValue}"
        // );

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

        float newValue = NotificationValue(modifier);

        // Debug.Log(
            // $"Global modifier çıkarıldı: {modifier.statType} | {modifier.target} | {modifier.operation} | {modifier.value} | Final: {newValue}"
        // );

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
