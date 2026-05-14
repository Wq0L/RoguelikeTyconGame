using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private Button buyButton;


    private void Awake()
    {
        shopPanel.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance.CurrentState == GameStates.Shop && !shopPanel.activeSelf)
        {
            ToggleShopPanel();
        }    
    }

    public void ToggleShopPanel()
    {
        shopPanel.SetActive(!shopPanel.activeSelf);
    }
    
}
