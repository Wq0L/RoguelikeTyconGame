using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillNodeTestUI : MonoBehaviour
{
    [SerializeField] private SkillNodeSO node;
    [SerializeField] private Button button;
    [SerializeField] private TextMeshProUGUI label;

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        // Test amaçlı — her frame güncelle, sonra event'e bağlarız
        Refresh();
    }

    private void Refresh()
    {
        int level = SkillTreeManager.Instance.GetCurrentLevel(node);
        bool maxed = level >= node.tiers.Count;

        if (maxed)
        {
            label.text = $"{node.nodeName}\nMAX SEVİYE ({level})";
            button.interactable = false;
        }
        else
        {
            var tier = node.tiers[level];
            bool canUpgrade = SkillTreeManager.Instance.CanUpgrade(node);

            label.text = $"{node.nodeName}\nSeviye {level} → {level + 1}\n{tier.cost} {tier.costType}";
            button.interactable = canUpgrade;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() =>
        {
            SkillTreeManager.Instance.TryUpgrade(node);
        });
    }
}