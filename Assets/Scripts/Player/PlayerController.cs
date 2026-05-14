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
        {
            return;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (!Physics.Raycast(ray, out RaycastHit hit))
        {
            return;
        }

        GridPosition gridPosition = gridSystem.GetGridPosition(hit.point);
        GridObject gridObject = gridSystem.GetGridObject(gridPosition);

        if (gridObject == null)
        {
            Debug.Log("Grid olmayan yere tıklandı.");
            return;
        }

        if (!gridObject.HasGroundObject())
        {
            Debug.Log("Bu hücre boş.");
            return;
        }

        Debug.Log("Bu hücre var: " + gridPosition.x + ", " + gridPosition.z);

        if (!gridObject.HasMineObject())
        {
            Debug.Log("Bu hücrede mine yok.");
            return;
        }

        GameObject mineObject = gridObject.GetMineObject();

        if (mineObject.TryGetComponent<IDamageable>(out IDamageable damageable))
        {
            damageable.TakeDamage(1);
        }
    }
}