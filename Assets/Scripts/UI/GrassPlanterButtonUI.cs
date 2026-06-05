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
        PlacementManager.Instance.StartPlacement(planterSO);
    }
}