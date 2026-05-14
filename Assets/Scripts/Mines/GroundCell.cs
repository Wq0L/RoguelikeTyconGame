using UnityEngine;

public class GroundCell : MonoBehaviour
{
    [SerializeField] private Transform mineSpawnPoint;

    public Transform GetMineSpawnPoint()
    {
        return mineSpawnPoint;
    }
}