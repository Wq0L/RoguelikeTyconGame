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

            float radius = StatManager.Instance.GetFinalStat(
                StatType.AreaRadius,
                StatTarget.Player
            );

            bool anyCrit = AttackInRadius(mouseWorldPos);

            VFXManager.Instance.PlayAttackRing(mouseWorldPos, radius, anyCrit);
            VFXManager.Instance.ShakeCamera(anyCrit ? 0.08f : 0.03f);
        }
    }

    private bool AttackInRadius(Vector3 center)
    {
        float radius = StatManager.Instance.GetFinalStat(
            StatType.AreaRadius,
            StatTarget.Player
        );

        float baseDamage = StatManager.Instance.GetFinalStat(
            StatType.HarvestDamage,
            StatTarget.Player
        );

        float critChance = StatManager.Instance.GetFinalStat(
            StatType.CritChance,
            StatTarget.Player
        );

        float critMultiplier = StatManager.Instance.GetFinalStat(
            StatType.CritMultiplier,
            StatTarget.Player
        );

        List<GridObject> targets = gridSystem.GetGridObjectsInRadius(center, radius);
        bool anyCrit = false;

        foreach (GridObject gridObject in targets)
        {
            if (!gridObject.HasPlantObject()) continue;

            GameObject plantObj = gridObject.GetPlantObject();
            if (plantObj == null) continue;

            if (plantObj.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                float variance = Random.Range(0.85f, 1.15f);
                int damage = Mathf.RoundToInt(baseDamage * variance);

                bool isCrit = Random.value <= critChance;
                if (isCrit)
                {
                    damage = Mathf.RoundToInt(damage * critMultiplier);
                    anyCrit = true;
                }

                damageable.TakeDamage(damage);
                VFXManager.Instance.PlayHit(plantObj.transform.position, damage, isCrit);
            }
        }

        return anyCrit;
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