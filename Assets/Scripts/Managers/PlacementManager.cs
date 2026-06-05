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

        HandlePlacementClick();
        HandleCancel();
    }

    public void StartPlacement(PlanterSO planterData)
    {
        Debug.Log("StartPlacement çağrıldı: " + (planterData == null ? "NULL" : planterData.planterName));

        selectedPlanter = planterData;
        refundAmount = planterData.cost / 2;
        GameManager.Instance.StartPlacement();
    }

    private void HandlePlacementClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        Debug.Log("Tık algılandı");

        GridObject gridObject = GetMouseGridObject();
        Debug.Log("GridObject: " + (gridObject == null ? "NULL" : "BULUNDU"));
        if (gridObject == null) return;

        if (!gridObject.HasGroundObject())
        {
            Debug.Log("Ground yok.");
            return;
        }

        if (gridObject.HasPlanterObject())
        {
            Debug.Log("Bu tile dolu.");
            return;
        }

        Debug.Log("PlacePlanter çağrılıyor.");
        PlacePlanter(gridObject);
    }

    private void PlacePlanter(GridObject originGridObject)
    {
        Debug.Log("selectedPlanter: " + (selectedPlanter == null ? "NULL" : selectedPlanter.planterName));

        GroundCell groundCell = originGridObject.GetGroundCellCached();
        if (groundCell == null) return;

        GridPosition origin = groundCell.GetGridPosition();

        // Önce tüm tile'ların boş olduğunu kontrol et
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

        // Planter'ı spawn et
        float offsetX = (selectedPlanter.sizeX - 1) * gridManager.GetCellSize() / 2f;
        float offsetZ = (selectedPlanter.sizeZ - 1) * gridManager.GetCellSize() / 2f;

        Vector3 spawnPos = groundCell.transform.position + new Vector3(offsetX, 0, offsetZ);

        GameObject planterObj = Instantiate(
            selectedPlanter.prefab,
            spawnPos,
            Quaternion.identity
        );

        // Tüm tile'ları topla ve kaydet
        List<GridObject> occupiedGrids = new List<GridObject>();

        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                GridPosition pos = new GridPosition(origin.x + x, origin.z + z);
                GridObject gridObj = gridSystem.GetGridObject(pos);
                gridObj.SetPlanterObject(planterObj);
                occupiedGrids.Add(gridObj);
            }
        }

        // PlanterBrain'e tüm gridleri ver
        PlanterBrain planterBrain = planterObj.GetComponent<PlanterBrain>();
        planterBrain?.Initialize(occupiedGrids);

        EndPlacement();
    }

    private void HandleCancel()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    public void CancelPlacement()
    {
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