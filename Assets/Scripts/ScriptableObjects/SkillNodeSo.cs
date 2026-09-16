using System.Collections.Generic;
using UnityEngine;

public enum UnlockType
{
    None,
    Planter_2x2,
    Planter_2x3,
    TileBehavior_Explosive,
    TileBehavior_Duplicate,
    TileBehavior_Tornado,
    Planter_1x3
}

[System.Serializable]
public class SkillPrerequisite
{
    public SkillNodeSO node;
    [Min(1)] public int level = 1;
}

[System.Serializable]
public class SkillNodeTier
{
    public ResourceType costType = ResourceType.Gold; // hangi kaynakla alınıyor
    public int cost;
    public List<StatModifier> effects = new();
}

[CreateAssetMenu(menuName = "Game/SkillNode")]
public class SkillNodeSO : ScriptableObject
{
    [Header("Kimlik")]
    public string nodeName;
    public Sprite icon;
    public bool explicitPrerequisites;
    public List<SkillPrerequisite> prerequisites = new();
    public Vector2Int targetRounds;

    [Header("Konum — 8 yönlü komşuluk buradan hesaplanır")]
    public Vector2Int gridPosition;

    [Header("Seviyeler (sırayla 1, 2, 3...)")]
    public List<SkillNodeTier> tiers = new();

    [Header("Mekanik Açma (opsiyonel, max seviyede tetiklenir)")]
    public UnlockType unlockType = UnlockType.None;
}
