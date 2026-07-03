using UnityEngine;

[CreateAssetMenu(menuName = "Game/Plant")]
public class PlantSO : ScriptableObject
{
    [Header("Bilgi")]
    public string plantName;
    public PlantRarity rarity;

    [Header("Görsel")]
    public GameObject prefab;

    [Header("Health")]
    public int maxHealth = 10;

    [Header("Reward")]
    public ResourceType resourceType;
    public int rewardAmount;
    public int xpAmount;

    [Header("VFX")]
    public Color hitFlashColor = Color.white;
    public Color deathParticleColor = Color.green; 
}