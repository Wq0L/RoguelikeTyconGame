using System;
using UnityEngine;

public class PlantHealth : MonoBehaviour, IDamageable
{
    public event Action OnDied;

    private int maxHealth;
    private int currentHealth;
    private bool isDead;
    private bool killedByExplosion;
    private PlantSO plantData;
    private Renderer plantRenderer;
    private PlanterBrain planter;
    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public int SpawnRound { get; private set; }

    public bool KilledByExplosion => killedByExplosion;
    public bool IsDead => isDead;
    public PlanterBrain Owner => planter;

    private void Awake()
    {
        plantRenderer = GetComponentInChildren<Renderer>();
    }

    public void Initialize(PlantSO data, PlanterBrain owner = null)
    {
        planter = owner;
        plantData = data;
        SpawnRound = RoundManager.Instance != null ? RoundManager.Instance.CurrentRound : 1;
        maxHealth = PlantHealthCalculator.Calculate(data, SpawnRound);
        currentHealth = maxHealth;
        isDead = false;
        killedByExplosion = false;
    }

    public void TakeDamage(int damage, bool fromExplosion = false)
    {
        if (isDead) return;
        damage = GetIncomingDamage(damage, fromExplosion);

        currentHealth -= damage;

        // Hit flash
        VFXManager.Instance.PlayHitFlash(plantRenderer, plantData.hitFlashColor);

        // Hit particle
        VFXManager.Instance.PlayHitParticle(transform.position, plantData.hitFlashColor);

        if (currentHealth <= 0)
        {
            killedByExplosion = fromExplosion;
            Die();
        }
    }

    public int GetIncomingDamage(int damage, bool fromExplosion = false)
    {
        // Only direct hits get the target planter's bonus. Explosions retain their existing semantics.
        double multiplier = !fromExplosion && planter != null
            ? planter.GetFinalStat(StatType.PlanterDamageMultiplier) : 1d;
        return (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round(damage * multiplier)));
    }

    private void Die()
    {
        isDead = true;
        OnDied?.Invoke();
        Destroy(gameObject);
    }
}
