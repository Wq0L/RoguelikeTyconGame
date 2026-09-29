using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

// Ayar satırlarının ortak tabanı: başlık ve üzerine gelince alttaki açıklama alanında görünen metin.
// Görseller Tools > Comic UI > Build Options Menu ile sahnede kurulur.
public abstract class OptionRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] protected TMP_Text label;
    [SerializeField, TextArea] private string description;

    public Action<string> HoverChanged;

    public void OnPointerEnter(PointerEventData eventData) => HoverChanged?.Invoke(description);
    public void OnPointerExit(PointerEventData eventData) => HoverChanged?.Invoke(null);
}
