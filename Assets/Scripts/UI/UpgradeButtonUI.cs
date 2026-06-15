using System;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeButtonUI : MonoBehaviour
{
    public static event Action<UpgradeSO> OnUpgradeClicked;

    [SerializeField] private UpgradeSO upgradeSO;
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        OnUpgradeClicked?.Invoke(upgradeSO);
    }
}