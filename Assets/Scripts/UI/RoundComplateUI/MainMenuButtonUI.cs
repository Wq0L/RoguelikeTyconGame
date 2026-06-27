using UnityEngine;
using UnityEngine.UI;

public class MainMenuButtonUI : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private RunCompleteUI runCompleteUI;

    private void Awake()
    {
        button.onClick.AddListener(HandleClick);
    }

    private void HandleClick()
    {
        runCompleteUI.OnMainMenuPressed();
    }
}