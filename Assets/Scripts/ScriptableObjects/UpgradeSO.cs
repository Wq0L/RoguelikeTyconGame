using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct ResourceCost
{
    public ResourceType resourceType;
    public int amount;
}

[CreateAssetMenu(menuName = "Game/Upgrade")]
public class UpgradeSO : ScriptableObject
{
    [Header("Upgrade Info")]
    [SerializeField] private string upgradeId;
    [SerializeField] private string upgradeName;
    [SerializeField] private ResourceCost cost;
    [SerializeField] private int newUnlockSize;

    [Header("Stat Modifiers")]
    [SerializeField] private List<StatModifier> modifiers;

    public string UpgradeId => upgradeId;
    public string UpgradeName => upgradeName;
    public ResourceCost Cost => cost;
    public int NewUnlockSize => newUnlockSize;
    public List<StatModifier> Modifiers => modifiers;
}