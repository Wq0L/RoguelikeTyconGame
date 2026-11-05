using UnityEngine;

[CreateAssetMenu(menuName = "Game/Plant")]
public class PlantSO : ScriptableObject
{
    [Header("Bilgi")]
    public string plantName;
    // Oyuncuya görünen ad: çeviri tablosundan (data.plant.<ad>.name); anahtar yoksa asset'teki yazı.
    public string DisplayName => GameText.Data(NameKey, plantName);
    public string NameKey => "data.plant." + GameText.Slug(plantName) + ".name";
    public PlantRarity rarity;

    [Header("Görsel")]
    public GameObject prefab;

    [Header("Health")]
    [Tooltip("Round HP reference: 10 = normal curve, 20 = twice the curve. See Resources/PlantHealthScaling.")]
    public int maxHealth = 10;

    [Header("Reward")]
    public ResourceType resourceType;
    public int rewardAmount;
    public int xpAmount;

    [Header("VFX")]
    public Color hitFlashColor = Color.white;
    public Color deathParticleColor = Color.green; 
}
