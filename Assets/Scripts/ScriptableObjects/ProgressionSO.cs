using UnityEngine;

[CreateAssetMenu(menuName = "Game/Progression")]
public class ProgressionSO : ScriptableObject
{
    [Header("XP Eğrisi")]
    public float baseXP = 100f;
    public float xpMultiplier = 1.5f;

    public float GetXPForLevel(int level)
    {
        return baseXP * Mathf.Pow(xpMultiplier, level - 1);
    }
}