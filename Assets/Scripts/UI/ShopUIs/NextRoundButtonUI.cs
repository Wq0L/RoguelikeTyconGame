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
        // Yazı çeviri tablosundan; sahnedeki LocalizedText'in anahtarı burada seçilir (ikisi aynı yazıyı yarıştırmaz).
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            LocalizedText.Bind(label, RoundManager.Instance != null && RoundManager.Instance.IsPreparingFirstRound
                ? "ui.game.start_round" : "ui.game.next_round");
    }
}
