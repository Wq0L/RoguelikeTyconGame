using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private LineRenderer radiusIndicator;
    [SerializeField] private int circleSegments = 64;

    private GridSystem gridSystem;
    private float attackTimer;

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
        SetupRadiusIndicator();
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Round)
        {
            radiusIndicator.enabled = false;
            return;
        }

        radiusIndicator.enabled = true;

        Vector3 mouseWorldPos = GetMouseWorldPosition();
        UpdateRadiusVisual(mouseWorldPos);
        HandleAutoAttack(mouseWorldPos);
    }

    private Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, groundLayerMask))
            return hit.point;

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float enter))
            return ray.GetPoint(enter);

        return Vector3.zero;
    }

    private void HandleAutoAttack(Vector3 mouseWorldPos)
    {
        float attackSpeed = StatManager.Instance.GetFinalStat(
            StatType.AttackSpeed,
            StatTarget.Player
        );

        attackSpeed = Mathf.Max(attackSpeed, 0.1f);

        attackTimer += Time.deltaTime;

        if (attackTimer >= attackSpeed)
        {
            attackTimer = 0f;
            AttackInRadius(mouseWorldPos);
        }
    }

    private void AttackInRadius(Vector3 center)
    {
        float radius = StatManager.Instance.GetFinalStat(
            StatType.AreaRadius,
            StatTarget.Player
        );

        int damage = Mathf.RoundToInt(
            StatManager.Instance.GetFinalStat(
                StatType.HarvestDamage,
                StatTarget.Player
            )
        );

        List<GridObject> targets = gridSystem.GetGridObjectsInRadius(center, radius);

        foreach (GridObject gridObject in targets)
        {
            if (!gridObject.HasPlantObject()) continue;

            GameObject plantObj = gridObject.GetPlantObject();
            if (plantObj == null) continue;

            if (plantObj.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                damageable.TakeDamage(damage);
                Debug.Log($"Attacked {plantObj.name} for {damage} damage.");
            }
        }
    }

    private void SetupRadiusIndicator()
    {
        if (radiusIndicator == null) return;

        radiusIndicator.loop = true;
        radiusIndicator.useWorldSpace = true;
        radiusIndicator.positionCount = circleSegments;
    }

    private void UpdateRadiusVisual(Vector3 center)
    {
        if (radiusIndicator == null) return;

        float radius = StatManager.Instance.GetFinalStat(
            StatType.AreaRadius,
            StatTarget.Player
        );

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;

            radiusIndicator.SetPosition(i, new Vector3(x, center.y + 0.05f, z));
        }
    }
}