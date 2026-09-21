using UnityEngine;
using UnityEngine.UI;

public class RoundMapUI : MonoBehaviour
{
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Transform gridContainer;
    [SerializeField] private Color emptyColor = Color.white;
    [SerializeField] private Color lockedColor = Color.black;

    private void OnEnable()
    {
        BuildGrid();
    }

    public void BuildGrid()
    {
        int width = GridManager.Instance.GetWidth();
        int height = GridManager.Instance.GetHeight();

        foreach (Transform child in gridContainer)
            Destroy(child.gameObject);

        GridSystem gridSystem = GridManager.Instance.GetGridSystem();

        for (int z = height - 1; z >= 0; z--)
        {
            for (int x = 0; x < width; x++)
            {
                GameObject cell = Instantiate(cellPrefab, gridContainer);
                cell.name = $"Cell_x{x}_z{z}";
                Image img = cell.GetComponent<Image>();

                GridPosition pos = new GridPosition(x, z);
                GridObject gridObj = gridSystem.GetGridObject(pos);

                Color color = lockedColor;

                if (gridObj != null)
                {
                    GroundCell groundCell = gridObj.GetGroundCellCached();

                    if (groundCell == null || groundCell.IsLocked)
                        color = lockedColor;
                    else if (groundCell.CurrentModifier != null)
                        color = groundCell.CurrentModifier.tileColor;
                    else
                        color = emptyColor;
                }

                img.color = color;
                TileCellUI cellUI = cell.GetComponent<TileCellUI>();
                if (cellUI != null && gridObj != null)
                {
                    GroundCell gc = gridObj.GetGroundCellCached();
                    cellUI.Setup(gc);
                }
            }
        }
    }
}