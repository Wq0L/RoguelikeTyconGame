using UnityEngine;

public interface IDamageable
{
   void TakeDamage(int damage, DamageType type = DamageType.Direct);
    
}
