using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private GridManager gridManager;
    [SerializeField] private LayerMask groundLayerMask;
    [SerializeField] private LineRenderer radiusIndicator;
    [SerializeField] private int circleSegments = 64;
    [SerializeField] private HarvestCursorVisual cursorVisualPrefab;
    [SerializeField] private Material radiusOverlayMaterial;
    private HarvestCursorVisual cursorVisual;

    private GridSystem gridSystem;
    private float attackTimer;

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
        SetupRadiusIndicator();
        if (cursorVisualPrefab != null) cursorVisual = Instantiate(cursorVisualPrefab, transform);
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Round)
        {
            if (radiusIndicator != null) radiusIndicator.enabled = false;
            if (cursorVisual != null) cursorVisual.gameObject.SetActive(false);
            return;
        }

        if (radiusIndicator != null) radiusIndicator.enabled = true;
        if (cursorVisual != null) cursorVisual.gameObject.SetActive(true);

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

    // Aralığı aşan süre sonraki vuruşa aktarılır: kare hızı vuruş sayısını düşürmez.
    // Bir karede en fazla bir vuruş; taşan birikim en fazla bir aralık (uzun karede telafi döngüsü yok).
    public static bool AdvanceAttackTimer(ref float timer, float deltaTime, float interval)
    {
        timer += deltaTime;
        if (timer < interval) return false;
        timer = Mathf.Min(timer - interval, interval);
        return true;
    }

    private void HandleAutoAttack(Vector3 mouseWorldPos)
    {
        float attackSpeed = StatManager.Instance.GetFinalStat(
            StatType.AttackSpeed,
            StatTarget.Player
        );

        // 60 sn'yi aşan round süresi hıza dönüşür (RoundManager.TempoMultiplier). Taban önce uygulanır:
        // 60 sn'lik hızlı round, 90 sn'lik normal round'la aynı sayıda vuruş yapar.
        float tempo = RoundManager.Instance != null ? RoundManager.Instance.TempoMultiplier : 1f;
        attackSpeed = Mathf.Max(attackSpeed, 0.1f) / tempo;

        if (AdvanceAttackTimer(ref attackTimer, Time.deltaTime, attackSpeed))
        {

            float radius = StatManager.Instance.GetFinalStat(
                StatType.AreaRadius,
                StatTarget.Player
            );

            bool anyCrit = AttackInRadius(mouseWorldPos);

            VFXManager.Instance.PlayAttackRing(mouseWorldPos, radius, anyCrit);
            if (cursorVisual != null) cursorVisual.Pulse(anyCrit);
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
                // Uzmanlaşma doğrudan katsayısı yalnız burada: sapmayla birlikte tek yuvarlama; kritik ve saksı bonusu
                // (Odak) sonra mevcut kurallarıyla uygulanır. Davranışların tabanı HarvestDamage stat'ıdır, bu değil.
                int damage = Mathf.Max(1, Mathf.RoundToInt(baseDamage * variance * SpecializationManager.DirectMultiplier));

                bool isCrit = Random.value <= critChance;
                if (isCrit)
                {
                    damage = Mathf.RoundToInt(damage * critMultiplier);
                    anyCrit = true;
                }

                int displayedDamage = damageable is PlantHealth health ? health.GetIncomingDamage(damage) : damage;
                if (damageable is PlantHealth hitPlant)
                    hitPlant.TakeDamage(damage, DamageType.Direct, isCrit);
                else
                    damageable.TakeDamage(damage);
                VFXManager.Instance.PlayHit(plantObj.transform.position, displayedDamage, isCrit);
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
        if (radiusOverlayMaterial != null) radiusIndicator.sharedMaterial = radiusOverlayMaterial;
        radiusIndicator.sortingOrder = 100;
    }

    private void UpdateRadiusVisual(Vector3 center)
    {
        if (radiusIndicator == null) return;

        float radius = StatManager.Instance.GetFinalStat(
            StatType.AreaRadius,
            StatTarget.Player
        );

        if (cursorVisual != null) cursorVisual.Show(center, radius);

        for (int i = 0; i < circleSegments; i++)
        {
            float angle = (float)i / circleSegments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(angle) * radius;
            float z = center.z + Mathf.Sin(angle) * radius;

            radiusIndicator.SetPosition(i, new Vector3(x, center.y + 0.05f, z));
        }
    }
}
