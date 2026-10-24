using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlacementManager : MonoBehaviour
{
    public static PlacementManager Instance { get; private set; }

    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private GridManager gridManager;
    [SerializeField] private Texture2D sellCursor;
    [SerializeField] private Vector2 sellCursorHotspot = new Vector2(2, 2);

    private GridSystem gridSystem;
    private PlanterSO selectedPlanter;
    private int refundAmount;
    private int currentRotation = 0;
    private GameObject ghostObject;
    private ResonancePreviewUI resonancePreview;
    private readonly List<GridObject> previewFootprint = new();
    private ResourceType selectedCostResource;

    private bool isSellMode = false;
    private GameManager observedGameManager;
    private int sellModeEntryFrame;

    private void OnEnable() => BindGameState();

    private void BindGameState()
    {
        if (observedGameManager != null || GameManager.Instance == null) return;
        observedGameManager = GameManager.Instance;
        observedGameManager.OnGameStateChanged += HandleGameStateChanged;
        HandleGameStateChanged(observedGameManager.CurrentState);
    }

    private void HandleGameStateChanged(GameStates state)
    {
        bool selling = state == GameStates.Selling;
        if (selling == isSellMode) return;
        isSellMode = selling;
        if (selling) sellModeEntryFrame = Time.frameCount;
        Cursor.SetCursor(selling ? sellCursor : null,
            selling ? sellCursorHotspot : Vector2.zero, CursorMode.Auto);
    }

    private void OnDisable()
    {
        if (resonancePreview != null) resonancePreview.Hide();
        if (observedGameManager != null)
            observedGameManager.OnGameStateChanged -= HandleGameStateChanged;
        observedGameManager = null;
        if (isSellMode) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        isSellMode = false;
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
        BindGameState();
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
        }
    }

    public void StartPlacement(PlanterSO planterData)
    {
        if (planterData == null || !planterData.IsUnlocked) return;
        if (GameManager.Instance.CurrentState == GameStates.Placing)
        {
            // Debug.Log("Önce mevcut yerleştirmeyi iptal et.");
            return;
        }
        
        selectedPlanter = planterData;
        selectedCostResource = planterData.PriceType;
        refundAmount = planterData.Price / 2;
        currentRotation = 0;

        ghostObject = Instantiate(selectedPlanter.prefab);
        ghostObject.transform.rotation = Quaternion.identity;

        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetGhostMode(true);
        resonancePreview = ghostObject.AddComponent<ResonancePreviewUI>();

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

        Vector3 center = new Vector3(
            (selectedPlanter.sizeX - 1) * cellSize * 0.5f,
            0f,
            (selectedPlanter.sizeZ - 1) * cellSize * 0.5f);
        return Quaternion.Euler(0, currentRotation, 0) * center;
    }

    private void MoveGhostToMouse()
    {
        if (ghostObject == null) return;

        GridObject gridObject = GetMouseGridObject();
        bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        if (gridObject == null || overUI) { ghostObject.SetActive(false); resonancePreview?.Hide(); return; }

        GroundCell groundCell = gridObject.GetGroundCellCached();
        if (groundCell == null) { ghostObject.SetActive(false); resonancePreview?.Hide(); return; }

        GridPosition origin = groundCell.GetGridPosition();

        ghostObject.transform.position =
            groundCell.transform.position + GetGhostCenterOffset();
        ghostObject.SetActive(true);

        bool isValid = IsPlacementValid(origin);

        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetColor(isValid);
        previewFootprint.Clear();
        if (isValid)
            for (int x = 0; x < selectedPlanter.sizeX; x++)
                for (int z = 0; z < selectedPlanter.sizeZ; z++)
                {
                    var offset = GetRotatedOffset(x, z);
                    previewFootprint.Add(gridSystem.GetGridObject(new GridPosition(origin.x + offset.x, origin.z + offset.y)));
                }
        var brain = ghostObject.GetComponent<PlanterBrain>();
        resonancePreview?.Show(previewFootprint, brain != null ? brain.Rules : ResonanceManager.DefaultRules, isValid && !overUI);
    }

    private bool IsPlacementValid(GridPosition origin)
    {
        for (int x = 0; x < selectedPlanter.sizeX; x++)
        {
            for (int z = 0; z < selectedPlanter.sizeZ; z++)
            {
                Vector2Int offset = GetRotatedOffset(x, z);
                GridPosition checkPos = new GridPosition(origin.x + offset.x, origin.z + offset.y);
                GridObject gridObj = gridSystem.GetGridObject(checkPos);

                if (gridObj == null)
                {
                    // Debug.Log($"[VALID] {checkPos}: gridObj NULL");
                    return false;
                }
                if (gridObj.HasPlanterObject())
                {
                    // Debug.Log($"[VALID] {checkPos}: PLANTER var (temizlenmemiş!)");
                    return false;
                }

                GroundCell cell = gridObj.GetGroundCellCached();
                if (cell == null)
                {
                    // Debug.Log($"[VALID] {checkPos}: cell NULL");
                    return false;
                }
                if (cell.IsLocked)
                {
                    // Debug.Log($"[VALID] {checkPos}: cell KİLİTLİ");
                    return false;
                }
            }
        }
        return true;
    }

    private void HandlePlacementClick()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        GridObject gridObject = GetMouseGridObject();
        if (gridObject == null || !gridObject.HasGroundObject())
        { SoundManager.Ensure().Play(GameSound.InvalidPlacement); return; }

        GroundCell groundCell = gridObject.GetGroundCellCached();
        if (groundCell == null || !IsPlacementValid(groundCell.GetGridPosition()))
        {
            SoundManager.Ensure().Play(GameSound.InvalidPlacement);
            return;
        }

        PlacePlanter(gridObject);
    }

    private void PlacePlanter(GridObject originGridObject)
    {
        if (CommitPlacement(originGridObject)) EndPlacement();
        else SoundManager.Ensure().Play(GameSound.InvalidPlacement);
    }

    // Hayalet saksıyı tarlaya bağlar (hücre sahipliği, üretim noktaları, tile bonusları). true: yerleşti.
    private bool CommitPlacement(GridObject originGridObject)
    {
        GroundCell groundCell = originGridObject.GetGroundCellCached();
        if (groundCell == null) return false;

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
                    // Debug.Log("Yeterli boş alan yok.");
                    return false;
                }

                occupiedGrids.Add(gridObj);
            }
        }

        PlanterBrain planterBrain = ghostObject.GetComponent<PlanterBrain>();
        if (planterBrain != null && !planterBrain.ValidateSpawnPoints(occupiedGrids, out string error))
        {
            Debug.LogError(error, planterBrain);
            ghostObject.GetComponent<GhostController>()?.SetColor(false);
            return false;
        }

        GhostController ghost = ghostObject.GetComponent<GhostController>();
        ghost?.SetGhostMode(false);
        if (resonancePreview != null) { resonancePreview.Hide(); Destroy(resonancePreview); resonancePreview = null; }

        foreach (GridObject gridObj in occupiedGrids)
        {
            gridObj.SetPlanterObject(ghostObject);
        }

        if (planterBrain != null)
        {
            planterBrain.Initialize(selectedPlanter, occupiedGrids);
#if UNITY_EDITOR
            planterBrain.EditorRecordPlacement(origin, currentRotation);
#endif

            foreach (GridObject gridObj in occupiedGrids)
            {
                gridObj.SetPlanterBrain(planterBrain);//yeni

            }

            // Debug.Log($"Toplam local modifier sayısı: {planterBrain.LocalModifiers.Count}");

            // foreach (StatModifier mod in planterBrain.LocalModifiers)
            //     Debug.Log($"  → {mod.statType} | {mod.target} | {mod.operation} | {mod.value}");
        }

        PlanterFeel.PlayLanding(ghostObject, occupiedGrids);
        ghostObject = null;
        return true;
    }

#if UNITY_EDITOR
    // Yalnız Editor (Mechanics Test Lab): fare, bedel ve kilit olmadan bir saksıyı verilen hücreye kurar. Geometri kuralları
    // oyununkidir: aynı doğrulama (IsPlacementValid: tarla dışı, kilitli hücre, çakışma) ve aynı bağlama (CommitPlacement).
    // Oyun durumu değişmez (mağaza açılmaz). rotation: 0 / 90 / 180 / 270. Bu blok build'e girmez.
    public PlanterBrain EditorPlace(PlanterSO planter, GridPosition origin, int rotation, out string error)
    {
        error = null;
        if (planter == null || planter.prefab == null) { error = "saksı verisi ya da prefabı yok"; return null; }
        if (gridSystem == null) { error = "grid hazır değil"; return null; }
        if (ghostObject != null || selectedPlanter != null) { error = "devam eden bir yerleştirme var"; return null; }
        rotation = ((rotation % 360) + 360) % 360;
        if (rotation % 90 != 0) { error = "yön 0 / 90 / 180 / 270 olmalı"; return null; }
        GridObject originObject = gridSystem.GetGridObject(origin);
        GroundCell originCell = originObject?.GetGroundCellCached();
        if (originCell == null) { error = $"({origin.x}, {origin.z}) tarlanın dışında"; return null; }
        selectedPlanter = planter;
        currentRotation = rotation;
        try
        {
            if (!IsPlacementValid(origin))
            {
                error = $"({origin.x}, {origin.z}) konumuna {planter.sizeX}×{planter.sizeZ} saksı {rotation}° ile sığmıyor " +
                        "(tarla dışı, kilitli hücre ya da dolu hücre)";
                return null;
            }
            ghostObject = Instantiate(planter.prefab);
            ghostObject.transform.SetPositionAndRotation(originCell.transform.position + GetGhostCenterOffset(), Quaternion.Euler(0, rotation, 0));
            PlanterBrain brain = ghostObject.GetComponent<PlanterBrain>();
            if (!CommitPlacement(originObject))
            {
                error = "saksının üretim noktaları hücrelere oturmuyor (ayrıntı Console'da)";
                Destroy(ghostObject);
                return null;
            }
            return brain;
        }
        finally
        {
            ghostObject = null;
            selectedPlanter = null;
            currentRotation = 0;
        }
    }
#endif

    private void HandleCancel()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            CancelPlacement();
    }

    public void CancelPlacement()
    {
        if (selectedPlanter == null || ghostObject == null) return;
        SoundManager.Ensure().Play(GameSound.CancelPlacement);
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
        if (resonancePreview != null) resonancePreview.Hide();
        resonancePreview = null;
        selectedPlanter = null;
        selectedCostResource = default;
        refundAmount = 0;
        currentRotation = 0;
        GameManager.Instance.OpenShop();
    }

    public void EnterSellMode()
    {
        if (GameManager.Instance.CurrentState != GameStates.Shop) return;
        GameManager.Instance.EnterSellMode();
    }

    public void ExitSellMode()
    {
        if (GameManager.Instance.CurrentState != GameStates.Selling) return;
        GameManager.Instance.OpenShop();
    }

    private void HandleSellClick()
    {
        // Closing the shop must not sell the planter under the entry click.
        if (Time.frameCount == sellModeEntryFrame) return;
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
