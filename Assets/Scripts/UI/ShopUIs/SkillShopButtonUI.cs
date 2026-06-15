using UnityEngine;
using UnityEngine.UI;

public class SkillShopButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private UIManager uiManager;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        uiManager.OpenSkillShop();
    }
}