using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Upgrade")]
public class UpgradeSO : ScriptableObject
{
    [Header("Upgrade Info")]
    [SerializeField] private string upgradeId;
    [SerializeField] private string upgradeName;
    [SerializeField] private int cost;
    [SerializeField] private int newUnlockSize;

    [Header("Stat Modifiers")]
    [SerializeField] private List<StatModifier> modifiers;

    public string UpgradeId => upgradeId;
    public string UpgradeName => upgradeName;
    public int Cost => cost;
    public int NewUnlockSize => newUnlockSize;
    public List<StatModifier> Modifiers => modifiers;
}