using System;
using UnityEngine;

public class PlantHealth : MonoBehaviour, IDamageable
{
    public event Action OnDied;

    private int maxHealth;
    private int currentHealth;
    private bool isDead;
    private bool killedByExplosion;

    public bool KilledByExplosion => killedByExplosion;


    public void Initialize(int health)
    {
        maxHealth = health;
        currentHealth = maxHealth;
        isDead = false;
    }

   public void TakeDamage(int damage, bool fromExplosion = false)
    {
        if (isDead) return;

        currentHealth -= damage;

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