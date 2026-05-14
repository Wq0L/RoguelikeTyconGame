using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }
    [SerializeField] private UpgradeItemData shopItemData;


    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public bool TryBuy(UpgradeItemData item)
    {
        if (item == null)
            return false;

        bool paid = ResourceManager.Instance.SpendResource(
            item.CostResource,
            item.CostAmount
        );

        if (!paid)
        {
            Debug.Log($"Yeterli {item.CostResource} yok!");
            return false;
        }

        Debug.Log($"{item.DisplayName} satın alındı!");

        // Şimdilik burada item verilir / upgrade açılır
        return true;
    }
}
