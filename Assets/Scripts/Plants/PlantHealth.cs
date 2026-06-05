using System;
using UnityEngine;

public class PlantHealth : MonoBehaviour, IDamageable
{
    public event Action OnDied;

    [SerializeField] private int maxHealth = 3;

    private int currentHealth;
    private bool isDead;

    private void Awake()
    {
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        OnDied?.Invoke();
        Debug.Log("Plant destroyed!");
        Destroy(gameObject);
    }
}