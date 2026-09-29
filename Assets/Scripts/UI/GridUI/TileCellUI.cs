using UnityEngine;

public class TileCellUI : MonoBehaviour, ITooltipProvider
{
    [SerializeField] private GameObject tooltipPrefab;

    private GroundCell groundCell;
    private FreshTileRing freshRing;

    public void Setup(GroundCell cell)
    {
        groundCell = cell;
    }

    // Bu round'un kart seçimi bu tile'a düştüyse parlar. Hücre round'lar arasında tekrar kullanılır.
    public void SetFresh(bool fresh, float delay, string label = "YENİ")
    {
        if (!fresh)
        {
            if (freshRing != null) freshRing.gameObject.SetActive(false);
            return;
        }
        if (freshRing == null) freshRing = FreshTileRing.Create(transform);
        freshRing.Show(delay, label);
    }

    public bool ShouldShowTooltip()
    {
        return groundCell != null
            && !groundCell.IsLocked
            && groundCell.CurrentModifier != null;
    }

    public GameObject GetTooltipPrefab()
    {
        return tooltipPrefab;
    }

    public Vector3 GetTooltipPosition()
    {
        return Input.mousePosition + new Vector3(-10f, 30f, 0f);
    }

    public void FillTooltip(GameObject instance)
    {
        GridTileToolTipContent content = instance.GetComponent<GridTileToolTipContent>();  // ← senin ismin
        if (content == null) return;

        TileModifierSO modifier = groundCell.CurrentModifier;

        content.SetName(modifier.modifierName);
        content.SetRarity(modifier.rarity.ToString());
        content.SetCell(groundCell);
    }

}
