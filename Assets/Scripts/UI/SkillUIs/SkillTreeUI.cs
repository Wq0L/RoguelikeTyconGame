using System.Collections.Generic;
using UnityEngine;

public class SkillTreeUI : MonoBehaviour
{
    [SerializeField] private List<SkillNodeUI> nodeUIs;

    private void OnEnable()
    {
        if (SkillTreeManager.Instance == null) return; // ilk frame güvenliği

        SkillTreeManager.Instance.OnTreeChanged += RefreshAll;
        ResourceManager.Instance.OnResourceAmountChanged += HandleResourceChanged;

        RefreshAll(); // panel her açıldığında taze veri
    }

    private void OnDisable()
    {
        if (SkillTreeManager.Instance != null)
            SkillTreeManager.Instance.OnTreeChanged -= RefreshAll;

        if (ResourceManager.Instance != null)
            ResourceManager.Instance.OnResourceAmountChanged -= HandleResourceChanged;
    }

    private void HandleResourceChanged(ResourceType type, int amount) => RefreshAll();

    private void RefreshAll()
    {
        foreach (SkillNodeUI nodeUI in nodeUIs)
            nodeUI.Refresh();
    }
}