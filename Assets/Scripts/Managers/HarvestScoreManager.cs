using UnityEngine;

public class HarvestScoreManager : MonoBehaviour
{
    public static HarvestScoreManager Instance { get; private set; }

    // long: segment kotası ve sıralama için; int tavanında (2,1 milyar) ilerleme durmaz.
    private long totalScore;
    public long TotalScore => totalScore;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void AddScore(PlantRarity rarity, PlanterBrain planter = null)
    {
        int scoreToAdd = GetScoreForRarity(rarity);
        
        float multiplier = StatManager.Instance.GetFinalStat(
            StatType.HarvestScoreMultiplier, 
            StatTarget.Player
        );
        
        // Başlangıç seçiminin skor çarpanı (Tüccar ×0,9) en sonda, kesirli kalan taşınarak: Common'ın 1 puanı da bedeli öder.
        int final = StartLoadoutManager.ScaleScore(CalculateAward(rarity, multiplier, planter != null ? planter.GetHarvestScore(rarity) : 1f));
        totalScore = totalScore > long.MaxValue - final ? long.MaxValue : totalScore + final;

        // Debug.Log($"Score +{final} ({rarity}) | Toplam: {totalScore}");
    }

    public static int CalculateAward(PlantRarity rarity, float playerMultiplier, float planterMultiplier) =>
        (int)System.Math.Min(int.MaxValue, System.Math.Max(0, System.Math.Round(GetScoreForRarity(rarity) * (double)playerMultiplier * planterMultiplier)));

    private static int GetScoreForRarity(PlantRarity rarity)
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
            // Debug.Log($"Current Score: {totalScore}");
        }
    }
}
