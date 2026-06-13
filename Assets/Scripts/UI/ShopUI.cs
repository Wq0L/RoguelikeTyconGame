using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject shopPanel2;
    [SerializeField] private GameObject shopPanel3;
    [SerializeField] private GameObject shopPanel4;
    [SerializeField] private GameObject shopPanel5;
    [SerializeField] private GameObject shopPanel6;
    [SerializeField] private GameObject shopPanel7;


    private void Awake()
    {
        shopPanel.SetActive(false);
        shopPanel2.SetActive(false);
        shopPanel3.SetActive(false);
        shopPanel4.SetActive(false);
        shopPanel5.SetActive(false);
        shopPanel6.SetActive(false);
        shopPanel7.SetActive(false);
    }

    void Update()
    {
        if (GameManager.Instance.CurrentState == GameStates.Shop && !shopPanel.activeSelf && !shopPanel2.activeSelf && !shopPanel3.activeSelf && !shopPanel4.activeSelf && !shopPanel5.activeSelf && !shopPanel6.activeSelf && !shopPanel7.activeSelf)
        {
            ToggleShopPanel();
        }    
    }

    public void ToggleShopPanel()
    {
        shopPanel.SetActive(!shopPanel.activeSelf);
        shopPanel2.SetActive(!shopPanel2.activeSelf);
        shopPanel3.SetActive(!shopPanel3.activeSelf);
        shopPanel4.SetActive(!shopPanel4.activeSelf);
        shopPanel5.SetActive(!shopPanel5.activeSelf);
        shopPanel6.SetActive(!shopPanel6.activeSelf);
        shopPanel7.SetActive(!shopPanel7.activeSelf);
    }
    
}
