using System;
using UnityEngine;

public class PlantHealth : MonoBehaviour, IDamageable
{
    public event Action OnDied;
    // Oynanış: bitki hasat edildi (görev sayacı). Dinleyicinin hatası gerçek bir oynanış hatasıdır ve yukarı çıkar; bitki yine havuza
    // döner (Die).
    public static event Action<PlantHealth> Harvested;
    // Gözlem (ölçüm / test): her hasatta (KilledBy doğrudan mı davranış mı). Oynanış buna bağlı değildir; dinleyicinin hatası ölümü,
    // ödülü ve havuza dönüşü engellemez (ObserverEvents).
    private static Action<PlantHealth>[] harvestObservers = Array.Empty<Action<PlantHealth>>();
    public static event Action<PlantHealth> AnyHarvested
    {
        add => ObserverEvents.Add(ref harvestObservers, value);
        remove => ObserverEvents.Remove(ref harvestObservers, value);
    }
    // Gözlem: bitkiye uygulanan her hasar (saksı bonusu dahil, hesaplanmış değer) ve türü. Dinleyicinin hatası hasarı engellemez.
    private static Action<PlantHealth, int, DamageType>[] damageObservers = Array.Empty<Action<PlantHealth, int, DamageType>>();
    public static event Action<PlantHealth, int, DamageType> AnyDamaged
    {
        add => ObserverEvents.Add(ref damageObservers, value);
        remove => ObserverEvents.Remove(ref damageObservers, value);
    }
    // Görsel tepkiler için (PlantJuice squash). Parametre: crit mi.
    public event Action<bool> OnDamaged;

    private int maxHealth;
    private int currentHealth;
    private bool isDead;
    private DamageType killedBy;
    private PlantSO plantData;
    private Renderer plantRenderer;
    private PlanterBrain planter;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int SpawnRound { get; private set; }
    public uint LifetimeVersion { get; private set; }

    public DamageType KilledBy => killedBy;
    // Öldüren vuruşun zincir bağlamı (Bölüm 3.7.6): kök ve nesil. Bu yaşam için; havuzdan yeniden doğunca sıfırlanır.
    public HarvestLink KillLink { get; private set; }
    // Ölçüm için: son vuruşun bağlamı (AnyDamaged sırasında okunur). Oynanış buna bağlı değildir.
    public HarvestLink LastHitLink { get; private set; }
    public bool IsDead => isDead;
    // Bu yaşamdaki bitki verisi (AnyHarvested sırasında dolu; havuza dönünce null).
    public PlantSO Data => plantData;
    public PlanterBrain Owner => planter;
    public float KillingElectricXPMultiplier { get; private set; } = 1f;
    // Olay can çarpanı (Sert Kabuk): bu yaşam için en fazla bir kez uygulanır. 1 = yok.
    public float HealthMultiplier { get; private set; } = 1f;
    private int baseMaxHealth;

    private void Awake()
    {
        plantRenderer = GetComponentInChildren<Renderer>();
    }

    public void Initialize(PlantSO data, PlanterBrain owner = null, float healthMultiplier = 1f)
    {
        LifetimeVersion++;
        planter = owner;
        plantData = data;
        SpawnRound = RoundManager.Instance != null ? RoundManager.Instance.CurrentRound : 1;
        // Havuzdan yeniden doğan bitki her seferinde tabandan başlar: önceki yaşamın çarpanı taşınmaz.
        baseMaxHealth = PlantHealthCalculator.Calculate(data, SpawnRound);
        maxHealth = baseMaxHealth;
        currentHealth = maxHealth;
        HealthMultiplier = 1f;
        isDead = false;
        killedBy = DamageType.Direct;
        KillLink = LastHitLink = HarvestLink.None;
        KillingElectricXPMultiplier = 1f;
        ApplyHealthMultiplier(healthMultiplier);
    }

    // Olay can çarpanı: azami ve mevcut can aynı oranda büyür (yaralı bitki dolmaz). Bu yaşamda zaten çarpan varsa uygulanmaz.
    // Geçersiz çarpan (NaN, sonsuz) uygulanmaz ve raporlanır; büyük can int sınırında doyar (eskiden 1'e düşebiliyordu).
    public bool ApplyHealthMultiplier(float multiplier)
    {
        if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) { NumericSafety.ReportInvalid(NumericSite.PlantHealth, multiplier); return false; }
        if (isDead || multiplier <= 0f || Mathf.Approximately(multiplier, 1f) || !Mathf.Approximately(HealthMultiplier, 1f)) return false;
        int scaledMax = NumericSafety.ToInt(baseMaxHealth * multiplier, 1, NumericSite.PlantHealth);
        currentHealth = NumericSafety.ToInt((double)currentHealth * scaledMax / Mathf.Max(1, maxHealth), 1, NumericSite.PlantHealth);
        maxHealth = scaledMax;
        HealthMultiplier = multiplier;
        return true;
    }

    // Olay bitti: çarpan aynı oranla geri alınır (yaralı bitki yine aynı oranda yaralı kalır).
    public bool ClearHealthMultiplier()
    {
        if (isDead || Mathf.Approximately(HealthMultiplier, 1f)) return false;
        currentHealth = NumericSafety.ToInt((double)currentHealth * baseMaxHealth / Mathf.Max(1, maxHealth), 1, NumericSite.PlantHealth);
        maxHealth = baseMaxHealth;
        HealthMultiplier = 1f;
        return true;
    }

    public void TakeDamage(int damage, DamageType type = DamageType.Direct)
        => TakeDamage(damage, type, false);

    public void TakeDamage(int damage, DamageType type, bool isCrit, float sourceElectricXP = 1f)
        => TakeDamage(damage, type, isCrit, sourceElectricXP, HarvestLink.None);

    // link: vuruşun zincir bağlamı (kök, nesil). Bitki bu vuruşla ölürse saklanır; zincir kararı onu okur.
    public void TakeDamage(int damage, DamageType type, bool isCrit, float sourceElectricXP, HarvestLink link)
    {
        if (isDead) return;
        damage = GetIncomingDamage(damage, type);

        LastHitLink = link;
        ObserverEvents.Raise(damageObservers, this, damage, type);
        currentHealth -= damage;

        // Hit flash
        VFXManager.Instance?.PlayHitFlash(plantRenderer, plantData.hitFlashColor);

        // Hit particle
        VFXManager.Instance?.PlayHitParticle(transform.position, plantData.rarity, isCrit);
        OnDamaged?.Invoke(isCrit);

        if (currentHealth <= 0)
        {
            killedBy = type;
            KillLink = link;
            KillingElectricXPMultiplier = type == DamageType.Electric ? Mathf.Max(1f, sourceElectricXP) : 1f;
            Die();
        }
    }

    public int GetIncomingDamage(int damage, DamageType type = DamageType.Direct)
    {
        // Saksı bonusunu alıp almayacağı DamageTypeRules'tan gelir
        double multiplier = type.UsesPlanterBonus() && planter != null
            ? planter.GetFinalStat(StatType.PlanterDamageMultiplier) : 1d;
        return NumericSafety.ToInt(damage * multiplier, 0, NumericSite.IncomingDamage);
    }

    private void Die()
    {
        isDead = true;
        ObserverEvents.Raise(harvestObservers, this);
        // Oynanış dinleyicileri (görev sayacı, ödül, davranış tetiği): hata yukarı çıkar ama bitki her durumda havuza döner.
        try
        {
            Harvested?.Invoke(this);
            OnDied?.Invoke();
        }
        finally { PlantPool.Release(gameObject); }
    }

    private void OnDisable()
    {
        VFXManager.Instance?.CancelHitFlash(plantRenderer);
        isDead = true;
        planter = null;
        plantData = null;
    }
}
