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
    private readonly List<GridObject> attackTargets = new();
    // Son saldırının gerçek temas yarıçapı (Hasat Ritmi güçlendirmesi dahil); vuruş halkası bunu çizer.
    public float LastAttackRadius { get; private set; }
    // Hazır Hasat Ritmi hakkının imleç vurgusu.
    private static readonly Color ChargedRing = new Color(1f, 0.84f, 0.25f, 1f);
    private Color ringStart = Color.white, ringEnd = Color.white;
    private bool ringCharged;
    public bool ShowsChargedCursor => ringCharged;

    private void Start()
    {
        gridSystem = gridManager.GetGridSystem();
        SetupRadiusIndicator();
        if (cursorVisualPrefab != null) cursorVisual = Instantiate(cursorVisualPrefab, transform);
    }

    private void Update()
    {
        // Round süresi bitip kart kararı bekleyen level işini beklerken (RoundManager) saldırı yok.
        if (GameManager.Instance.CurrentState != GameStates.Round || (RoundManager.Instance != null && !RoundManager.Instance.IsRoundActive))
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

    // Saldırı aralığının oyundaki alt sınırı (sn). Stat bunun altına inse de saldırı hızlanmaz.
    public const float MinAttackInterval = 0.1f;

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
        attackSpeed = Mathf.Max(attackSpeed, MinAttackInterval) / tempo;

        if (AdvanceAttackTimer(ref attackTimer, Time.deltaTime, attackSpeed))
        {

            bool anyCrit = AttackInRadius(mouseWorldPos);

            // Ring and targeting share the radius this attack really used; touched cell surfaces are included.
            VFXManager.Instance.PlayAttackRing(mouseWorldPos, gridSystem.HarvestReach(LastAttackRadius), anyCrit);
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

        // Saldırı bağlamı (Hasat Ritmi): hazır hak varsa yalnız BU saldırının hasarı ve yarıçapı büyür. Genel AreaRadius stat'ı
        // değişmez, davranışlar bu bağlamı görmez. Ödül alınmadıysa bağlam nötrdür (×1, ×1).
        DirectAttack attack = RunPower.Rhythm.Next();
        radius *= attack.RadiusMultiplier;
        LastAttackRadius = radius;
        int directHarvests = 0;
        bool touchedLivingPlant = false;

        gridSystem.GetGridObjectsInRadius(center, radius, attackTargets);
        bool anyCrit = false;
        // Zincir kökü (Bölüm 3.7.6): bu saldırının bütün vuruşları ve doğurdukları davranışlar aynı kökü taşır. Zincir Hasat
        // alınmadıysa 0 (bağlam açılmaz). Saldırı bitince saldırının kök üzerindeki tutumu bırakılır.
        int root = HarvestChain.BeginRoot();

        foreach (GridObject gridObject in attackTargets)
        {
            if (!gridObject.HasPlantObject()) continue;

            GameObject plantObj = gridObject.GetPlantObject();
            if (plantObj == null) continue;

            if (plantObj.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                float variance = Random.Range(0.85f, 1.15f);
                // Doğrudan vuruş katsayıları yalnız burada, bir kez: uzmanlaşma × başlangıç (tırpan) × boss ödülü (Keskin Bıçak),
                // sapmayla birlikte tek yuvarlama; kritik ve saksı bonusu (Odak) sonra mevcut kurallarıyla uygulanır.
                // Davranışların tabanı HarvestDamage stat'ıdır, bu değil.
                // Nadirliğe bağlı boss ödülü (Altın Hedef) da aynı çarpımda, hedef başına bir kez.
                PlantRarity? rarity = damageable is PlantHealth target && target.Data != null ? target.Data.rarity : (PlantRarity?)null;
                // int'e dönüşüm NumericSafety'de: int sınırını aşan hasar doyar (eskiden 1'e düşüyordu), kritik negatife taşmaz.
                int damage = NumericSafety.ToInt(RunPower.DirectDamage(baseDamage, variance, rarity, attack.DamageMultiplier), 1, NumericSite.DirectDamage);

                bool isCrit = Random.value <= critChance;
                if (isCrit)
                {
                    damage = NumericSafety.ToInt(damage * critMultiplier, 0, NumericSite.CritDamage);
                    anyCrit = true;
                }

                int displayedDamage = damageable is PlantHealth health ? health.GetIncomingDamage(damage) : damage;
                if (damageable is PlantHealth hitPlant)
                {
                    touchedLivingPlant = true;
                    hitPlant.TakeDamage(damage, DamageType.Direct, isCrit, 1f, HarvestLink.Direct(root));
                    // Bu vuruşla ölen bitki doğrudan hasattır (ölümün tetiklediği davranış hasatları başka bitkilerdir).
                    if (hitPlant.IsDead) directHarvests++;
                }
                else
                    damageable.TakeDamage(damage);
                VFXManager.Instance.PlayHit(plantObj.transform.position, displayedDamage, isCrit);
            }
        }

        HarvestChain.EndRoot(root);
        RunPower.Rhythm.Complete(attack, directHarvests, touchedLivingPlant);
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
        ringStart = radiusIndicator.startColor;
        ringEnd = radiusIndicator.endColor;
    }

    private void UpdateRadiusVisual(Vector3 center)
    {
        if (radiusIndicator == null) return;

        // A cell need not have its centre inside this circle: touching its surface is enough.
        // Hazır Hasat Ritmi hakkı varsa halka, sıradaki saldırının gerçek (büyümüş) temas alanını gösterir ve altın rengine döner.
        DirectAttack next = RunPower.Rhythm.Next();
        float radius = gridSystem.HarvestReach(StatManager.Instance.GetFinalStat(
            StatType.AreaRadius,
            StatTarget.Player
        ) * next.RadiusMultiplier);
        if (next.Empowered != ringCharged)
        {
            ringCharged = next.Empowered;
            radiusIndicator.startColor = ringCharged ? ChargedRing : ringStart;
            radiusIndicator.endColor = ringCharged ? ChargedRing : ringEnd;
            if (cursorVisual != null) cursorVisual.SetCharged(ringCharged);
        }

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
