using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject shopPanel2;


    private void Awake()
    {
        shopPanel.SetActive(false);
        shopPanel2.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance.CurrentState == GameStates.Shop && !shopPanel.activeSelf && !shopPanel2.activeSelf)
        {
            ToggleShopPanel();
        }    
    }

    public void ToggleShopPanel()
    {
        shopPanel.SetActive(!shopPanel.activeSelf);
        shopPanel2.SetActive(!shopPanel2.activeSelf);
    }
    
}
