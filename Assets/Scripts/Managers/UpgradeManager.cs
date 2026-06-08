using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    private HashSet<string> purchasedUpgradeIds = new();

    private void OnEnable()
    {
        UpgradeButtonUI.OnUpgradeClicked += TryBuyUpgrade;
    }

    private void OnDisable()
    {
        UpgradeButtonUI.OnUpgradeClicked -= TryBuyUpgrade;
    }

    private void TryBuyUpgrade(UpgradeSO upgradeSO)
    {
        if (upgradeSO == null)
            return;

        //Debug.Log($"Upgrade: {upgradeSO.UpgradeName} | Cost: {upgradeSO.Cost} | Mevcut Gold: {ResourceManager.Instance.GetResourceAmount(ResourceType.Gold)}");

        // Daha önce alınmış mı?
        if (purchasedUpgradeIds.Contains(upgradeSO.UpgradeId))
        {
            Debug.Log("Bu upgrade zaten alınmış.");
            return;
        }

        // Gold yeterli mi?
        bool success = ResourceManager.Instance.SpendResource(
            ResourceType.Gold,
            upgradeSO.Cost
        );

        if (!success)
        {
            Debug.Log("Yeterli gold yok.");
            return;
        }

        // Stat modifierları uygula
        StatManager.Instance.ApplyModifiers(upgradeSO.Modifiers);

        // Satın alınanlara ekle
        purchasedUpgradeIds.Add(upgradeSO.UpgradeId);

        //Debug.Log("Upgrade satın alındı: " + upgradeSO.UpgradeName);
    }
}