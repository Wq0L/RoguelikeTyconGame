using UnityEngine;

public class MineResource : MonoBehaviour
{
   
    [SerializeField] private MineHealth mineHealth;
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int rewardAmount = 1;

    private void OnEnable()
    {
        mineHealth.OnDied += GiveReward;
    }

    private void OnDisable()
    {
        mineHealth.OnDied -= GiveReward;
    }

    private void GiveReward()
    {
        ResourceManager.Instance.AddResource(resourceType, rewardAmount);
    }

}
