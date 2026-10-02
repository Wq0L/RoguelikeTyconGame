using System;
using UnityEngine;

public class PlantHealth : MonoBehaviour, IDamageable
{
    public event Action OnDied;
    // Ölçüm için: her hasatta (KilledBy doğrudan mı davranış mı). Oynanış buna bağlı değildir.
    public static event Action<PlantHealth> AnyHarvested;
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
        KillingElectricXPMultiplier = 1f;
        ApplyHealthMultiplier(healthMultiplier);
    }

    // Olay can çarpanı: azami ve mevcut can aynı oranda büyür (yaralı bitki dolmaz). Bu yaşamda zaten çarpan varsa uygulanmaz.
    public bool ApplyHealthMultiplier(float multiplier)
    {
        if (isDead || multiplier <= 0f || Mathf.Approximately(multiplier, 1f) || !Mathf.Approximately(HealthMultiplier, 1f)) return false;
        int scaledMax = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * multiplier));
        currentHealth = Mathf.Max(1, (int)System.Math.Round((double)currentHealth * scaledMax / Mathf.Max(1, maxHealth)));
        maxHealth = scaledMax;
        HealthMultiplier = multiplier;
        return true;
    }

    // Olay bitti: çarpan aynı oranla geri alınır (yaralı bitki yine aynı oranda yaralı kalır).
    public bool ClearHealthMultiplier()
    {
        if (isDead || Mathf.Approximately(HealthMultiplier, 1f)) return false;
        currentHealth = Mathf.Max(1, (int)System.Math.Round((double)currentHealth * baseMaxHealth / Mathf.Max(1, maxHealth)));
        maxHealth = baseMaxHealth;
        HealthMultiplier = 1f;
        return true;
    }

    public void TakeDamage(int damage, DamageType type = DamageType.Direct)
        => TakeDamage(damage, type, false);

    public void TakeDamage(int damage, DamageType type, bool isCrit, float sourceElectricXP = 1f)
    {
        if (isDead) return;
        damage = GetIncomingDamage(damage, type);

        currentHealth -= damage;

        // Hit flash
        VFXManager.Instance?.PlayHitFlash(plantRenderer, plantData.hitFlashColor);

        // Hit particle
        VFXManager.Instance?.PlayHitParticle(transform.position, plantData.rarity, isCrit);
        OnDamaged?.Invoke(isCrit);

        if (currentHealth <= 0)
        {
            killedBy = type;
            KillingElectricXPMultiplier = type == DamageType.Electric ? Mathf.Max(1f, sourceElectricXP) : 1f;
            Die();
        }
    }

    public int GetIncomingDamage(int damage, DamageType type = DamageType.Direct)
    {
        // Saksı bonusunu alıp almayacağı DamageTypeRules'tan gelir
        double multiplier = type.UsesPlanterBonus() && planter != null
            ? planter.GetFinalStat(StatType.PlanterDamageMultiplier) : 1d;
        return (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round(damage * multiplier)));
    }

    private void Die()
    {
        isDead = true;
        AnyHarvested?.Invoke(this);
        try { OnDied?.Invoke(); }
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
