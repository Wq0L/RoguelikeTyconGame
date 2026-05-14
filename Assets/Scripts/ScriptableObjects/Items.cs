using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "UpgradeItemData", menuName = "Shop/Upgrade Item", order = 1)]
public class UpgradeItemData : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;

    [SerializeField] private ResourceType costResource;
    [SerializeField] private int costAmount;

    [SerializeField] private int upgradeLevel;
    [SerializeField] private Sprite icon;
    [SerializeField] private string description;

    public string Id => id;
    public string DisplayName => displayName;
    public ResourceType CostResource => costResource;
    public int CostAmount => costAmount;
    public int UpgradeLevel => upgradeLevel;
    public Sprite Icon => icon;
    public string Description => description;
}