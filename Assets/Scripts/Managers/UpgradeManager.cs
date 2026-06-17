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

        if (purchasedUpgradeIds.Contains(upgradeSO.UpgradeId))
        {
            Debug.Log("Bu upgrade zaten alınmış.");
            return;
        }

        bool success = ResourceManager.Instance.SpendResource(
            upgradeSO.Cost.resourceType,
            upgradeSO.Cost.amount
        );

        if (!success)
        {
            Debug.Log($"Yeterli {upgradeSO.Cost.resourceType} yok.");
            return;
        }

        StatManager.Instance.AddGlobalModifiers(upgradeSO.Modifiers);

        purchasedUpgradeIds.Add(upgradeSO.UpgradeId);
    }
}