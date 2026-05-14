using TMPro;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private TMP_Text amountText;

    private void Start()
    {
        if (ResourceManager.Instance == null)
        {
            Debug.LogError("ResourceManager bulunamadı!");
            return;
        }

        ResourceManager.Instance.OnResourceAmountChanged += UpdateGoldUI;
        UpdateGoldUI(resourceType, ResourceManager.Instance.GetResourceAmount(resourceType));
    }

    // private void OnEnable()
    // {
        
    //     ResourceManager.Instance.OnResourceAmountChanged += UpdateUI;
    //     UpdateUI(resourceType, ResourceManager.Instance.GetResourceAmount(resourceType));
    // }

    private void OnDisable()
    {
        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceAmountChanged -= UpdateGoldUI;
    }

    public void UpdateGoldUI(ResourceType changedType, int newAmount)
    {
        if (changedType != resourceType) return;

        amountText.text = newAmount.ToString();
    }
}