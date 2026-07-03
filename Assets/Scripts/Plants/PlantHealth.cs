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

    public bool KilledByExplosion => killedByExplosion;

    private void Awake()
    {
        plantRenderer = GetComponentInChildren<Renderer>();
    }

    public void Initialize(PlantSO data)
    {
        plantData = data;
        maxHealth = data.maxHealth;
        currentHealth = maxHealth;
        isDead = false;
        killedByExplosion = false;
    }

    public void TakeDamage(int damage, bool fromExplosion = false)
    {
        if (isDead) return;

        currentHealth -= damage;

        // Hit flash
        VFXManager.Instance.PlayHitFlash(plantRenderer, plantData.hitFlashColor);

        if (currentHealth <= 0)
        {
            killedByExplosion = fromExplosion;
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        OnDied?.Invoke();
        Destroy(gameObject);
    }
}