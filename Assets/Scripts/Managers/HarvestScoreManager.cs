using UnityEngine;

public class HarvestScoreManager : MonoBehaviour
{
    public static HarvestScoreManager Instance { get; private set; }

    private int totalScore;
    public int TotalScore => totalScore;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddScore(PlantRarity rarity)
    {
        int scoreToAdd = GetScoreForRarity(rarity);
        
        float multiplier = StatManager.Instance.GetFinalStat(
            StatType.HarvestScoreMultiplier, 
            StatTarget.Player
        );
        
        int final = Mathf.RoundToInt(scoreToAdd * multiplier);
        totalScore += final;

        Debug.Log($"Score +{final} ({rarity}) | Toplam: {totalScore}");
    }

    private int GetScoreForRarity(PlantRarity rarity)
    {
        switch (rarity)
        {
            case PlantRarity.Common:    return 1;
            case PlantRarity.Uncommon:  return 3;
            case PlantRarity.Rare:      return 10;
            case PlantRarity.Epic:      return 30;
            case PlantRarity.Legendary: return 100;
            default:                    return 1;
        }
    }

    public void ResetScore()
    {
        totalScore = 0;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log($"Current Score: {totalScore}");
        }
    }
}