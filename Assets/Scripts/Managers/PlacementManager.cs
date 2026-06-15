using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private GridManager gridManager;

    private GridSystem gridSystem;
    private PlanterSO selectedPlanter;
    private int refundAmount;
    private int currentRotation = 0;
    private GameObject ghostObject;
    private ResourceType selectedCostResource;


    private bool isSellMode = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState == GameStates.Placing)
        {
            HandleRotation();
            MoveGhostToMouse();
            HandlePlacementClick();
            HandleCancel();
        }
        if (isSellMode)
        {
            HandleSellClick();
            HandleSellExit();
        }
    }

    public void StartPlacement(PlanterSO planterData)
    {
        if (GameManager.Instance.CurrentState == GameStates.Placing)
        {
            Debug.Log("Önce mevcut yerleştirmeyi iptal et.");
            return;
        }

        selectedPlanter = planterData;
        selectedCostResource = planterData.costType;
        refundAmount = planterData.cost / 2;
        currentRotation = 0;

        ghostObject = Instantiate(selectedPlanter.prefab);

        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetGhostMode(true);

        GameManager.Instance.StartPlacement();
    }

    private void HandleRotation()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            currentRotation = (currentRotation + 90) % 360;

            if (ghostObject != null)
                ghostObject.transform.rotation = Quaternion.Euler(0, currentRotation, 0);
        }
    }

    private Vector2Int GetRotatedOffset(int x, int z)
    {
        switch (currentRotation)
        {
            case 0: return new Vector2Int(x, z);
            case 90: return new Vector2Int(z, -x);
            case 180: return new Vector2Int(-x, -z);
            case 270: return new Vector2Int(-z, x);
        }

        return new Vector2Int(x, z);
    }

    private Vector3 GetGhostCenterOffset()
    {
        float cellSize = gridManager.GetCellSize();

        Vector2Int min = Vector2Int.zero;
        Vector2Int max = Vector2Int.zero;

        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                Vector2Int offset = GetRotatedOffset(x, z);

                min = Vector2Int.Min(min, offset);
                max = Vector2Int.Max(max, offset);
            }
        }

        Vector2 center = ((Vector2)min + (Vector2)max) / 2f;
        return new Vector3(center.x * cellSize, 0, center.y * cellSize);
    }

    private void MoveGhostToMouse()
    {
        if (ghostObject == null) return;

        GridObject gridObject = GetMouseGridObject();
        if (gridObject == null) return;

        GroundCell groundCell = gridObject.GetGroundCellCached();
        if (groundCell == null) return;

        GridPosition origin = groundCell.GetGridPosition();

        ghostObject.transform.position =
            groundCell.transform.position + GetGhostCenterOffset();

        bool isValid = IsPlacementValid(origin);

        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetColor(isValid);
    }

    private bool IsPlacementValid(GridPosition origin)
    {
        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                Vector2Int offset = GetRotatedOffset(x, z);

                GridPosition checkPos = new GridPosition(
                    origin.x + offset.x,
                    origin.z + offset.y
                );

                GridObject gridObj = gridSystem.GetGridObject(checkPos);

                if (gridObj == null || gridObj.HasPlanterObject())
                    return false;

                GroundCell cell = gridObj.GetGroundCellCached();
                if (cell == null || cell.IsLocked)
                    return false;
            }
        }

        return true;
    }

    private void HandlePlacementClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        GridObject gridObject = GetMouseGridObject();
        if (gridObject == null) return;

        if (!gridObject.HasGroundObject()) return;

        GroundCell groundCell = gridObject.GetGroundCellCached();
        if (groundCell == null) return;

        if (!IsPlacementValid(groundCell.GetGridPosition()))
        {
            Debug.Log("Yeterli boş alan yok.");
            return;
        }

        PlacePlanter(gridObject);
    }

    private void PlacePlanter(GridObject originGridObject)
    {
        GroundCell groundCell = originGridObject.GetGroundCellCached();
        if (groundCell == null) return;

        GridPosition origin = groundCell.GetGridPosition();

        List<GridObject> occupiedGrids = new List<GridObject>();

        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                Vector2Int offset = GetRotatedOffset(x, z);

                GridPosition pos = new GridPosition(
                    origin.x + offset.x,
                    origin.z + offset.y
                );

                GridObject gridObj = gridSystem.GetGridObject(pos);

                if (gridObj == null || gridObj.HasPlanterObject())
                {
                    Debug.Log("Yeterli boş alan yok.");
                    return;
                }

                occupiedGrids.Add(gridObj);
            }
        }

        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetGhostMode(false);

        foreach (GridObject gridObj in occupiedGrids)
        {
            gridObj.SetPlanterObject(ghostObject);
        }

        PlanterBrain planterBrain = ghostObject.GetComponent<PlanterBrain>();

        if (planterBrain != null)
        {
            planterBrain.Initialize(occupiedGrids);

            foreach (GridObject gridObj in occupiedGrids)
            {
                GroundCell cell = gridObj.GetGroundCellCached();

                if (cell != null && cell.CurrentModifier != null)
                {
                    planterBrain.ApplyBuff(cell.CurrentModifier);
                }
            }
        }


        Debug.Log($"Toplam modifier sayısı: {planterBrain.ActiveModifiers.Count}");
        foreach (var mod in planterBrain.ActiveModifiers)
        {
            Debug.Log($"  → {mod.statType} | {mod.operation} | {mod.value}");
        }

        ghostObject = null;
        EndPlacement();
    }

    private void HandleCancel()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    public void CancelPlacement()
    {
        if (ghostObject != null)
        {
            Destroy(ghostObject);
            ghostObject = null;
        }

        ResourceManager.Instance.AddResource(selectedCostResource, refundAmount);
        EndPlacement();
    }

    private void EndPlacement()
    {
        selectedPlanter = null;
        selectedCostResource = default;
        refundAmount = 0;
        currentRotation = 0;
        GameManager.Instance.OpenShop();
    }

    public void EnterSellMode()
    {
        isSellMode = true;
        Debug.Log("Sell modu açık.");
    }

    public void ExitSellMode()
    {
        isSellMode = false;
        Debug.Log("Sell modu kapandı.");
        GameManager.Instance.OpenShop();
    }

    private void HandleSellClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject()) return;

        GridObject gridObject = GetMouseGridObject();
        if (gridObject == null) return;

        if (!gridObject.HasPlanterObject()) return;

        GameObject planterObj = gridObject.GetPlanterObject();
        PlanterBrain planterBrain = planterObj?.GetComponent<PlanterBrain>();
        planterBrain?.RemoveSelf();
    }

    private void HandleSellExit()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            ExitSellMode();
    }

    private GridObject GetMouseGridObject()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayerMask))
            return null;

        GroundCell groundCell = hit.collider.GetComponentInParent<GroundCell>();
        if (groundCell == null) return null;

        GridPosition gridPos = groundCell.GetGridPosition();
        return gridSystem.GetGridObject(gridPos);
    }
}