using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI rarityText; // yeni
    [SerializeField] private TextMeshProUGUI buffText;

    [SerializeField] private Button button;
    [Header("Rarity artwork")]
    [SerializeField] private Image cardImage;
    [SerializeField] private Sprite commonPlate;
    [SerializeField] private Sprite rarePlate;
    [SerializeField] private Sprite epicPlate;
    [SerializeField] private Sprite legendaryPlate;
    [SerializeField] private Material[] rarityMaterials;
    [SerializeField] private TextMeshProUGUI effectNameText;
    [SerializeField] private ComicCardSparkles sparkles;
    [SerializeField] private ComicTilePreview tilePreview;

    private TileCardOffer currentOffer;
    private Action<TileCardOffer> onSelected;

    public void Setup(TileCardOffer offer, Action<TileCardOffer> callback)
    {
        currentOffer = offer;
        TileModifierSO mod = offer.Tile;
        if(tilePreview)tilePreview.color=new Color(mod.tileColor.r,mod.tileColor.g,mod.tileColor.b,1);
        onSelected = callback;
        nameText.text = mod.modifierName;
        rarityText.text = mod.rarity.ToString().ToUpperInvariant();
        EnsureBuffText();
        buffText.text = offer.Modifiers.Count == 1 ? TileBuffText.Amount(offer.Modifiers[0]) : TileBuffText.Modifiers(offer.Modifiers);
        if(effectNameText) effectNameText.text=offer.Modifiers.Count>0?TileBuffText.Name(offer.Modifiers[0].statType):"Özel etki";
        int rarity=(int)mod.rarity;
        if(rarityMaterials!=null && rarity<rarityMaterials.Length)cardImage.material=rarityMaterials[rarity];
        if(sparkles)sparkles.SetRarity(mod.rarity);
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
        button.onClick.AddListener(() => onSelected?.Invoke(currentOffer));
    }

    private void EnsureBuffText()
    {
        if (buffText != null) return;
        buffText = Instantiate(rarityText, rarityText.transform.parent);
        buffText.name = "Buff Value";
        var rect = buffText.rectTransform;
        rect.anchorMin = new Vector2(.16f, .17f);
        rect.anchorMax = new Vector2(.84f, .24f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        buffText.fontSizeMin = 14f;
        buffText.fontSizeMax = 22f;
        buffText.enableAutoSizing = true;
        buffText.alignment = TextAlignmentOptions.Center;
        buffText.color = Color.white;
        buffText.raycastTarget = false;
    }
}
