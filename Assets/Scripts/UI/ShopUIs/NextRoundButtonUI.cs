using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NextRoundButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private UIManager uiManager;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        uiManager.NextRound();
    }

    private void OnEnable()
    {
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = RoundManager.Instance != null && RoundManager.Instance.IsPreparingFirstRound
                ? "START ROUND" : "NEXT ROUND";
    }
}
