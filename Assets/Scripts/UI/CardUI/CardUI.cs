using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI rarityText; // yeni

    [SerializeField] private Button button;
    [Header("Rarity artwork")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Sprite commonPlate;
    [SerializeField] private Sprite rarePlate;
    [SerializeField] private Sprite epicPlate;
    [SerializeField] private Sprite legendaryPlate;

    private TileModifierSO modifier;
    private Action<TileModifierSO> onSelected;

    public void Setup(TileModifierSO mod, Action<TileModifierSO> callback)
    {
        modifier = mod;
        onSelected = callback;
        nameText.text = mod.modifierName;
        rarityText.text = mod.rarity.ToString(); // yeni
        cardImage.sprite = mod.rarity switch
        {
            TileRarity.Rare => rarePlate,
            TileRarity.Epic => epicPlate,
            TileRarity.Legendary => legendaryPlate,
            _ => commonPlate
        };
        rarityText.color = mod.rarity switch
        {
            TileRarity.Rare => new Color32(112, 225, 240, 255),
            TileRarity.Epic => new Color32(208, 167, 255, 255),
            TileRarity.Legendary => new Color32(255, 215, 125, 255),
            _ => new Color32(209, 225, 236, 255)
        };
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelected?.Invoke(modifier));
    }
}
