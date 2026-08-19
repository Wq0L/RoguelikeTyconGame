using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillNodeUI : MonoBehaviour
{
    [SerializeField] private SkillNodeSO node;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private float spacing = 150f;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick); // BİR kez bağlanır

        RectTransform rect = GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(
            node.gridPosition.x * spacing,
            node.gridPosition.y * spacing
        );
    }

    private void HandleClick()
    {
        SkillTreeManager.Instance.TryUpgrade(node);
    }

    public void Refresh()
    {
        bool visible = SkillTreeManager.Instance.IsNodeVisible(node);
        gameObject.SetActive(visible);

        if (!visible) return;

        int level = SkillTreeManager.Instance.GetCurrentLevel(node);

        if (SkillTreeManager.Instance.IsMaxLevel(node))
        {
            label.text = $"{node.nodeName}\nMAX ({level})";
            button.interactable = false;
            return;
        }

        SkillNodeTier tier = node.tiers[level];
        label.text = $"{node.nodeName}\nSeviye {level} → {level + 1}\n{tier.cost} {tier.costType}";
        button.interactable = SkillTreeManager.Instance.CanUpgrade(node);
    }
}