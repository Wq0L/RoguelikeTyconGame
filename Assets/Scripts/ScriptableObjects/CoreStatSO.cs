using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stats/Core Stats")]
public class CoreStatsSO : ScriptableObject
{
    public List<StatEntry> stats = new();

    public float GetBaseStat(StatType statType)
    {
        for (int i = 0; i < stats.Count; i++)
        {
            if (stats[i].statType == statType)
            {
                return stats[i].value;
            }
        }

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