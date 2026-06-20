using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int damage, bool fromExplosion = false);
    
}
