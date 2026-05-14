using UnityEngine;

public class ShopButton : MonoBehaviour
{
    [SerializeField] private UpgradeItemData item;

    public void OnClickBuy()
    {
        ShopManager.Instance.TryBuy(item);
        RoundManager.Instance.StartNextRound();
    }
}
