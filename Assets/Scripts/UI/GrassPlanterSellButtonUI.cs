using System;
using UnityEngine;
using UnityEngine.UI;

public class GrassPlanterSellButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        GameManager.Instance.EnterSellMode();
        PlacementManager.Instance.EnterSellMode();
    }
}