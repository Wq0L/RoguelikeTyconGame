using UnityEngine;
using TMPro;

public class GridTileToolTipContent : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private TMP_Text valueText;

    public void SetName(string name) => nameText.text = name;
    public void SetRarity(string rarity) => rarityText.text = rarity;
    public void SetValues(string values) => valueText.text = values;
}