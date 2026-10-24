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
    public Vector3 CursorWorldPosition { get; private set; }
    public float CursorRadius { get; private set; }

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
#if UNITY_EDITOR
        if (EditorAim.HasValue) return EditorAim.Value;
#endif
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

    // Bu karedeki gerçek saldırı aralığı (sn): stat, oyunun alt sınırı ve round temposu.
    private float AttackInterval()
    {
        float attackSpeed = StatManager.Instance.GetFinalStat(
            StatType.AttackSpeed,
            StatTarget.Player
        );

        // 60 sn'yi aşan round süresi hıza dönüşür (RoundManager.TempoMultiplier). Taban önce uygulanır:
        // 60 sn'lik hızlı round, 90 sn'lik normal round'la aynı sayıda vuruş yapar.
        float tempo = RoundManager.Instance != null ? RoundManager.Instance.TempoMultiplier : 1f;
        return Mathf.Max(attackSpeed, MinAttackInterval) / tempo;
    }

    // Sunum için salt okunur saldırı ritmi (oynanışa etkisi yok): bu karenin aralığı, sıradaki saldırıya ilerleme (0–1) ve
    // atılan saldırı sayısı. Açgözlü Hasat ağzı animasyonunu bu ritme uydurur.
    public float AttackIntervalNow { get; private set; }
    public float AttackProgress => AttackIntervalNow > 0f ? Mathf.Clamp01(attackTimer / AttackIntervalNow) : 0f;
    public int AttacksPerformed { get; private set; }

    private void HandleAutoAttack(Vector3 mouseWorldPos)
    {
        float attackSpeed = AttackInterval();
        AttackIntervalNow = attackSpeed;

        // Doğrudan saldırı duraklaması (Bölüm 3.8, Dolunay): yalnız bu zamanlayıcı bekler; imleç ve oyunun geri kalanı sürer.
        if (DirectAttackPause.Tick(Time.deltaTime)) return;

#if UNITY_EDITOR
        // Test aracı: otomatik saldırı kapalıyken bekleme yine oyun zamanıyla dolar ama vuruş yalnız EditorAttackNow ile atılır.
        if (!EditorAutoAttack)
        {
            attackTimer = Mathf.Min(attackTimer + Time.deltaTime, attackSpeed);
            return;
        }
#endif
        if (AdvanceAttackTimer(ref attackTimer, Time.deltaTime, attackSpeed)) PerformAttack(mouseWorldPos);
    }

    private void PerformAttack(Vector3 center)
    {
        AttacksPerformed++;
        bool anyCrit = AttackInRadius(center);

        // Ring and targeting share the radius this attack really used; touched cell surfaces are included.
        VFXManager.Instance.PlayAttackRing(center, gridSystem.HarvestReach(LastAttackRadius), anyCrit);
        if (cursorVisual != null) cursorVisual.Pulse(anyCrit);
    }

#if UNITY_EDITOR
    // ---- Yalnız Editor (Mechanics Test Lab). Saldırı yolu aynıdır (PerformAttack → AttackInRadius); burada yalnız "ne zaman ve
    // nereye" sorusu test aracına bırakılır. Bu blok build'e girmez.
    // false: otomatik saldırı zamanlayıcısı vuruş üretmez (bekleme dolmaya devam eder).
    public static bool EditorAutoAttack = true;
    // Dolu: fare yerine bu dünya konumu nişan alınır (imleç halkası da oraya gider). null: normal fare kontrolü.
    public static Vector3? EditorAim;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void EditorResetTestInput()
    {
        EditorAutoAttack = true;
        EditorAim = null;
    }

    public float EditorAttackInterval => AttackInterval();
    // Bir sonraki saldırıya kalan bekleme (oyun saniyesi). 0: hazır.
    public float EditorAttackCooldown => Mathf.Max(0f, AttackInterval() - attackTimer);

    // Tek gerçek doğrudan saldırı. Normal kural atlanmaz: round aktif değilse, Dolunay duraklaması sürüyorsa ya da bekleme
    // dolmadıysa saldırı yapılmaz ve nedeni döner.
    public bool EditorAttackNow(Vector3 center, out string refusal)
    {
        refusal = null;
        if (gridSystem == null || GameManager.Instance == null || GameManager.Instance.CurrentState != GameStates.Round ||
            (RoundManager.Instance != null && !RoundManager.Instance.IsRoundActive)) refusal = "round aktif değil";
        else if (DirectAttackPause.Active) refusal = $"doğrudan saldırı duraklaması sürüyor ({DirectAttackPause.Remaining:0.00} sn)";
        else if (EditorAttackCooldown > 1e-4f) refusal = $"saldırı beklemesi dolmadı ({EditorAttackCooldown:0.00} sn kaldı)";
        if (refusal != null) return false;
        attackTimer = 0f;
        PerformAttack(center);
        return true;
    }

    // Ayrı test komutu: beklemeyi doldurur (bir sonraki saldırı hemen atılabilir).
    public void EditorResetAttackCooldown() => attackTimer = AttackInterval();
#endif

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
        // Boss ödülü etkileri ve tarla olayları (Bölüm 3.8, AttackFeed): dinleyici yokken çarpan 1'dir ve hiçbir şey kaydedilmez.
        // Çarpan (Açgözlü Hasat) yalnız bu saldırının doğrudan hasarına, Hasat Ritmi'yle aynı çarpımda bir kez girer.
        float feedMultiplier = AttackFeed.Begin(center, radius, attackTargets);
        float attackMultiplier = attack.DamageMultiplier * feedMultiplier;
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
                int damage = NumericSafety.ToInt(RunPower.DirectDamage(baseDamage, variance, rarity, attackMultiplier), 1, NumericSite.DirectDamage);
#if UNITY_EDITOR
                int editorDamageBeforeCrit = damage;
#endif

                bool isCrit = Random.value <= critChance;
                if (isCrit)
                {
                    damage = NumericSafety.ToInt(damage * critMultiplier, 0, NumericSite.CritDamage);
                    anyCrit = true;
                }

                int displayedDamage = damageable is PlantHealth health ? health.GetIncomingDamage(damage) : damage;
                if (damageable is PlantHealth hitPlant)
                {
                    Vector3 hitPoint = hitPlant.transform.position;
                    bool damagedLivingPlant = !hitPlant.IsDead && displayedDamage > 0;
                    touchedLivingPlant = true;
#if UNITY_EDITOR
                    // Sayısal iz (yalnız gözlem): bu vuruşun gerçek ara değerleri, hasar uygulanmadan hemen önce.
                    if (EditorNumericTrace.Listening)
                        EditorNumericTrace.Report(new EditorNumericTrace.DirectHit(hitPlant, baseDamage, variance, attack.DamageMultiplier, feedMultiplier,
                            editorDamageBeforeCrit, isCrit, critChance, critMultiplier, damage, displayedDamage));
#endif
                    hitPlant.TakeDamage(damage, DamageType.Direct, isCrit, 1f, HarvestLink.Direct(root));
                    // Bu vuruşla ölen bitki doğrudan hasattır (ölümün tetiklediği davranış hasatları başka bitkilerdir).
                    if (hitPlant.IsDead) directHarvests++;
                    AttackFeed.Hit(gridObject, hitPlant, damage, isCrit, damagedLivingPlant, hitPoint);
                }
                else
                    damageable.TakeDamage(damage);
                VFXManager.Instance.PlayHit(plantObj.transform.position, displayedDamage, isCrit);
            }
        }

        HarvestChain.EndRoot(root);
        RunPower.Rhythm.Complete(attack, directHarvests, touchedLivingPlant);
        // Bütün doğrudan vuruşlar uygulandı: ödül etkileri ve tarla olayları bu saldırının kaydını şimdi okur.
        AttackFeed.End(directHarvests, touchedLivingPlant);
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

        CursorWorldPosition = center;
        CursorRadius = radius;
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
