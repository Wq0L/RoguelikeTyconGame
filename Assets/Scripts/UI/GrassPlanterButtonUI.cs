using System;
using UnityEngine;
using UnityEngine.UI;

public class GrassPlanterButtonUI : MonoBehaviour
{

    [SerializeField] private PlanterSO planterSO;
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        if (planterSO == null || !planterSO.IsUnlocked ||
            GameManager.Instance.CurrentState != GameStates.Shop) return;
        // Para yeterli mi kontrol et ve kes
        bool success = ResourceManager.Instance.SpendResource(
            planterSO.costType,
            planterSO.cost
        );

        if (!success)
        {
            // Debug.Log("Yeterli kaynak yok.");
            return;
        }

        // Para kesildi, placement başlat
        PlacementManager.Instance.StartPlacement(planterSO);
    }

}
