using System.Collections.Generic;
using UnityEngine;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private GridManager gridManager;

    private GridSystem gridSystem;
    private PlanterSO selectedPlanter;
    private int refundAmount;
    private GameObject ghostObject;

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
        if (GameManager.Instance.CurrentState != GameStates.Placing)
            return;

        MoveGhostToMouse();
        HandlePlacementClick();
        HandleCancel();
    }

    public void StartPlacement(PlanterSO planterData)
    {
        selectedPlanter = planterData;
        refundAmount = planterData.cost / 2;

        // Ghost spawn et
        ghostObject = Instantiate(selectedPlanter.prefab);
        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetGhostMode(true);

        GameManager.Instance.StartPlacement();
    }

    private void MoveGhostToMouse()
    {
        if (ghostObject == null) return;

        GridObject gridObject = GetMouseGridObject();
        if (gridObject == null) return;

        GroundCell groundCell = gridObject.GetGroundCellCached();
        if (groundCell == null) return;

        GridPosition origin = groundCell.GetGridPosition();

        float offsetX = (selectedPlanter.sizeX - 1) * gridManager.GetCellSize() / 2f;
        float offsetZ = (selectedPlanter.sizeZ - 1) * gridManager.GetCellSize() / 2f;

        ghostObject.transform.position = groundCell.transform.position + 
            new Vector3(offsetX, 0, offsetZ);

        // Geçerli mi kontrol et, rengi güncelle
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
            GridPosition checkPos = new GridPosition(origin.x + x, origin.z + z);
            GridObject gridObj = gridSystem.GetGridObject(checkPos);

            if (gridObj == null || gridObj.HasPlanterObject())
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

        if (gridObject.HasPlanterObject())
        {
            Debug.Log("Bu tile dolu.");
            return;
        }

        PlacePlanter(gridObject);
    }

    private void PlacePlanter(GridObject originGridObject)
    {
        GroundCell groundCell = originGridObject.GetGroundCellCached();
        if (groundCell == null) return;

        GridPosition origin = groundCell.GetGridPosition();

        // Boş alan kontrolü
        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                GridPosition checkPos = new GridPosition(origin.x + x, origin.z + z);
                GridObject gridObj = gridSystem.GetGridObject(checkPos);

                if (gridObj == null || gridObj.HasPlanterObject())
                {
                    Debug.Log("Yeterli boş alan yok.");
                    return;
                }
            }
        }

        // Ghost'u gerçek modele çevir
        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetGhostMode(false);

        // PlanterBrain'e gridleri ver
        List<GridObject> occupiedGrids = new List<GridObject>();

        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                GridPosition pos = new GridPosition(origin.x + x, origin.z + z);
                GridObject gridObj = gridSystem.GetGridObject(pos);
                gridObj.SetPlanterObject(ghostObject);
                occupiedGrids.Add(gridObj);
            }
        }

        PlanterBrain planterBrain = ghostObject.GetComponent<PlanterBrain>();
        planterBrain?.Initialize(occupiedGrids);

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

        ResourceManager.Instance.AddResource(ResourceType.Gold, refundAmount);
        EndPlacement();
    }

    private void EndPlacement()
    {
        selectedPlanter = null;
        refundAmount = 0;
        GameManager.Instance.OpenShop();
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