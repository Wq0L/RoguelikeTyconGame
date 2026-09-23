using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Progression")]
public class ProgressionSO : ScriptableObject
{
    [Header("XP Eğrisi")]
    public float baseXP = 100f;
    public float xpMultiplier = 1.5f;

    [Header("Dengelenmiş seviye maliyetleri")]
    [Tooltip("Açıkken her eleman ilgili seviyeden bir sonrakine gereken XP'dir. Liste bittikten sonra son maliyet kullanılır.")]
    public bool useAuthoredRequirements;
    public List<float> xpRequirements = new();

    public float GetXPForLevel(int level)
    {
        level = Mathf.Max(1, level);
        if (useAuthoredRequirements && xpRequirements != null && xpRequirements.Count > 0)
            return Mathf.Max(1f, xpRequirements[Mathf.Min(level - 1, xpRequirements.Count - 1)]);
        return baseXP * Mathf.Pow(xpMultiplier, level - 1);
    }
}
