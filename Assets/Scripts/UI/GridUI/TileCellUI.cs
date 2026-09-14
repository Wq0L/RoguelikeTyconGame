using UnityEngine;

public class TileCellUI : MonoBehaviour, ITooltipProvider
{
    [SerializeField] private GameObject tooltipPrefab;

    private GroundCell groundCell;

    public void Setup(GroundCell cell)
    {
        groundCell = cell;
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
        content.SetValues(BuildValues());
    }

    private string BuildValues()
    {
        string result = "Bu tile\n" + TileBuffText.Modifiers(groundCell.RolledModifiers);

        if (groundCell.Planter != null)
        {
            if (groundCell.Planter.ActiveResonances.Count > 0) result += "\n\nSaksının rezonansları";
            foreach (ActiveResonance resonance in groundCell.Planter.ActiveResonances)
                result += $"\n{resonance.resonanceName} ({resonance.tileCount} tile): {TileBuffText.Resonance(resonance)}";
        }
        return result.TrimEnd();
    }
}
