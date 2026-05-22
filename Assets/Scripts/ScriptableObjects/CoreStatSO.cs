using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Stats/Core Stats")]
public class CoreStatsSO : ScriptableObject
{
    public List<StatEntry> stats;
}

[System.Serializable]
public class StatEntry
{
    public StatType statType;
    public float value;
}