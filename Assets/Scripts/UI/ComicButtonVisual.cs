using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// State colors live in shared materials; no per-frame material allocations.
[RequireComponent(typeof(Button), typeof(Image))]
public class ComicButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    public Material normal, hover, pressed, disabled;
    Button button;
    Image image;
    bool over, down, selected;
    void Awake() { button = GetComponent<Button>(); image = GetComponent<Image>(); }
    void OnEnable() { if (!button) Awake(); Apply(); }
    void OnDisable() { over = down = selected = false; }
    void Update() { Apply(); }
    void Apply()
    {
        if (!image || !button) return;
        var target = !button.IsInteractable() ? disabled : down ? pressed : over || selected ? hover : normal;
        if (image.material != target) image.material = target;
    }
    public void OnPointerEnter(PointerEventData e) { over = true; Apply(); }
    public void OnPointerExit(PointerEventData e) { over = down = false; Apply(); }
    public void OnPointerDown(PointerEventData e) { if(e.button == PointerEventData.InputButton.Left) down = true; Apply(); }
    public void OnPointerUp(PointerEventData e) { down = false; Apply(); }
    public void OnSelect(BaseEventData e) { selected = true; Apply(); }
    public void OnDeselect(BaseEventData e) { selected = down = false; Apply(); }
}
