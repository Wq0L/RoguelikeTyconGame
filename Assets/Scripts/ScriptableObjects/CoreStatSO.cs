using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stats/Core Stats")]
public class CoreStatsSO : ScriptableObject
{
    public List<StatEntry> stats = new();

    // Bu setin sahip olduğu statlar. Saksıya ait statların (davranış şansları, Odak çarpanı) tabanı burada değil, saksı verisindedir.
    public bool TryGetBaseStat(StatType statType, out float value)
    {
        for (int i = 0; i < stats.Count; i++)
            if (stats[i].statType == statType) { value = stats[i].value; return true; }
        value = 0f;
        return false;
    }

    public float GetBaseStat(StatType statType)
    {
        if (TryGetBaseStat(statType, out float value)) return value;

        Debug.LogWarning($"CoreStatsSO içinde base stat bulunamadı: {statType}. Default değer döndürüldü.");

        return StatDefaults.GetDefaultBase(statType);
    }
}

[System.Serializable]
public class StatEntry
{
    public StatType statType;
    public float value;
}