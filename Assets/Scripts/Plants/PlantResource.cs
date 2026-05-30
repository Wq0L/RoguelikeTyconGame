using UnityEngine;

public class PlantResource : MonoBehaviour
{
   
    [SerializeField] private PlantHealth plantHealth;
    [SerializeField] private ResourceType resourceType;
    [SerializeField] private int rewardAmount = 1;

    private void OnEnable()
    {
        plantHealth.OnDied += GiveReward;
    }

    private void OnDisable()
    {
        plantHealth.OnDied -= GiveReward;
    }

    private void GiveReward()
    {
        ResourceManager.Instance.AddResource(resourceType, rewardAmount);
    }

}
