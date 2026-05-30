using UnityEngine;

[CreateAssetMenu(menuName = "Game/Plant")]
public class PlantSO : ScriptableObject
{
    [Header("Bilgi")]
    public string plantName;
    public PlantRarity rarity;

    [Header("Görsel")]
    public GameObject prefab;

    [Header("Reward")]
    public ResourceType resourceType;
    public int rewardAmount;
}