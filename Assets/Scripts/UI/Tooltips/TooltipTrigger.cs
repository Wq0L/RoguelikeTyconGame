using UnityEngine;
using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private ITooltipProvider provider;

    private void Awake()
    {
        provider = GetComponent<ITooltipProvider>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (provider == null) return;
        TooltipManager.Instance.Show(provider);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        TooltipManager.Instance.Hide();
    }
   
    private void OnDisable()
    {
        TooltipManager.Instance.Hide();
    }
}