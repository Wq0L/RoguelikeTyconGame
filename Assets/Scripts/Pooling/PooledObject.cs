using UnityEngine;

public class PooledObject : MonoBehaviour
{
    private ObjectPool parentPool;
    private MineHealth mineHealth;

    public void Initialize(ObjectPool pool)
    {
        parentPool = pool;
        mineHealth = GetComponent<MineHealth>();
        
        if (mineHealth != null)
        {
            mineHealth.OnDied += ReturnToPool;
        }
    }

    private void ReturnToPool()
    {
        if (mineHealth != null)
        {
            mineHealth.OnDied -= ReturnToPool;
        }
        parentPool.ReturnObject(gameObject);
    }

    private void OnDestroy()
    {
        if (mineHealth != null)
        {
            mineHealth.OnDied -= ReturnToPool;
        }
    }
}
