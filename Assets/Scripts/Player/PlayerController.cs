using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;

    private GridSystem gridSystem;

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Playing)
        {
            return;
        }

        HandleMouseClick();
    }

    private void HandleMouseClick()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
            return;

        GroundCell groundCell = hit.collider.GetComponentInParent<GroundCell>();

        if (groundCell == null)
        {
            Debug.Log("Grid olmayan yere tıklandı.");
            return;
        }

        GridPosition gridPosition = groundCell.GetGridPosition();
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);

        if (gridObject == null)
        {
            Debug.Log("GridObject bulunamadı.");
            return;
        }

        Debug.Log("Tıklanan grid: " + gridPosition.x + ", " + gridPosition.z);

        if (!gridObject.HasMineObject())
        {
            Debug.Log("Bu gridde mine yok.");
            return;
        }

        GameObject mineObject = gridObject.GetMineObject();

        if (mineObject == null)
        {
            Debug.Log("Mine referansı boş.");
            return;
        }

        if (!mineObject.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            Debug.LogWarning("Mine üzerinde IDamageable yok.");
            return;
        }

        int damage = Mathf.RoundToInt(
            StatManager.Instance.GetStat(StatType.ClickDamage)
        );

        damageable.TakeDamage(damage);

        Debug.Log("Mine hasar aldı: " + damage);
    }
}