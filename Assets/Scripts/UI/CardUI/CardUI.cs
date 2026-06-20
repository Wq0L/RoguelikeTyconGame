using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI rarityText; // yeni

    [SerializeField] private Button button;

    private TileModifierSO modifier;
    private Action<TileModifierSO> onSelected;

    public void Setup(TileModifierSO mod, Action<TileModifierSO> callback)
    {
        modifier = mod;
        onSelected = callback;
        nameText.text = mod.modifierName;
        rarityText.text = mod.rarity.ToString(); // yeni
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(modifier));
    }
}