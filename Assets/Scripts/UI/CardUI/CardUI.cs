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
        TileRarity rarityType = offer.Rarity;
        Color tint = mod != null ? mod.tileColor : new Color(1f, .82f, .32f);
        if(tilePreview)tilePreview.color=new Color(tint.r,tint.g,tint.b,1);
        onSelected = callback;
        EnsureBuffText();
        string rarityName = rarityType.ToString().ToUpperInvariant();
        if (offer.IsUpgrade)
        {
            // Grid dolu: kart, üç rastgele adaydan birini seviye atlatır.
            GroundCell cell = offer.UpgradeTarget;
            nameText.text = mod.modifierName;
            rarityText.text = $"+{offer.LevelGain} SEVİYE · {rarityName}";
            buffText.text = $"Sv {cell.Level} → Sv {cell.Level + offer.LevelGain}\n" +
                (offer.Modifiers.Count == 1 ? TileBuffText.Amount(offer.Modifiers[0]) : TileBuffText.Modifiers(offer.Modifiers));
            if(effectNameText) effectNameText.text=$"{RoundMapUI.CellName(cell.GetGridPosition())} · " + (offer.Modifiers.Count>0?TileBuffText.Name(offer.Modifiers[0].statType):"Özel etki");
        }
        else if (offer.IsBaseStat)
        {
            // Yükseltilecek tile kalmadı: kalıcı temel güç.
            nameText.text = "Temel güç";
            rarityText.text = rarityName;
            buffText.text = TileBuffText.Modifiers(offer.Modifiers);
            if(effectNameText) effectNameText.text=offer.BaseStatName;
        }
        else
        {
            nameText.text = mod.modifierName;
            rarityText.text = rarityName;
            buffText.text = offer.Modifiers.Count == 1 ? TileBuffText.Amount(offer.Modifiers[0]) : TileBuffText.Modifiers(offer.Modifiers);
            if(effectNameText) effectNameText.text=offer.Modifiers.Count>0?TileBuffText.Name(offer.Modifiers[0].statType):"Özel etki";
        }
        int rarity=(int)rarityType;
        if(rarityMaterials!=null && rarity<rarityMaterials.Length)cardImage.material=rarityMaterials[rarity];
        if(sparkles)sparkles.SetRarity(rarityType);
        cardImage.sprite = rarityType switch
        {
            TileRarity.Rare => rarePlate,
            TileRarity.Epic => epicPlate,
            TileRarity.Legendary => legendaryPlate,
            _ => commonPlate
        };
        rarityText.color = rarityType switch
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
