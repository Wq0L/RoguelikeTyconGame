using TMPro;
using UnityEngine;

public class GoldUI : MonoBehaviour
{
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private TMP_Text amountText;
    private ResourceManager resources;

    private void OnEnable() => Subscribe();
    private void Start() => Subscribe();

    private void Subscribe()
    {
        if (resources == null && ResourceManager.Instance != null)
        {
            resources = ResourceManager.Instance;
            resources.OnResourceAmountChanged += UpdateGoldUI;
        }
        if (resources != null)
            UpdateGoldUI(resourceType, resources.GetResourceAmount(resourceType));
    }

    private void OnDisable()
    {
        if (resources != null) resources.OnResourceAmountChanged -= UpdateGoldUI;
        resources = null;
    }

    public void UpdateGoldUI(ResourceType changedType, int newAmount)
    {
        if (changedType != resourceType || amountText == null) return;
        string tint = resourceType == ResourceType.Gold ? "FFD36A" :
            resourceType == ResourceType.Iron ? "B9DDED" : "C8C3BD";
        amountText.text = $"<size=60%><color=#{tint}>{resourceType}</color></size>\n{newAmount:N0}";
    }
}
