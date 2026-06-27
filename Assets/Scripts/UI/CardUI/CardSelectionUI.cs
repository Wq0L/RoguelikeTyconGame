using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSelectionUI : MonoBehaviour
{
    [SerializeField] private List<TileModifierSO> allModifiers; // tüm SO'lar buraya
    [SerializeField] private List<CardUI> cardSlots;            // 3 kart slotu
    [SerializeField] private Button skipButton;

    // Tip ağırlıkları — sabit
    private Dictionary<TileModifierType, float> typeWeights = new()
    {
        { TileModifierType.Fertile,   25f },
        { TileModifierType.Water,     20f },
        { TileModifierType.Crystal,   20f },
        { TileModifierType.Energy,    15f },
        { TileModifierType.Explosive, 12f },
        { TileModifierType.Duplicate,  8f }
    };

    private Dictionary<TileRarity, int> skipBaseRewards = new()
    {
        { TileRarity.Common,    2 },
        { TileRarity.Rare,      4 },
        { TileRarity.Epic,      7 },
        { TileRarity.Legendary, 10 }
    };
    private List<TileModifierSO> currentCards = new();
    

    // UIManager her kart seçim ekranı açılışında bunu çağırır
    public void RefreshCards()
    {
        currentCards.Clear();

        for (int i = 0; i < cardSlots.Count; i++)
        {
            TileModifierSO rolled = RollCard();
            currentCards.Add(rolled);
            cardSlots[i].Setup(rolled, OnCardSelected);
        }

        if(skipButton != null)
        {
            bool canSkip = RoundManager.Instance.SkipUsesRemaining > 0;
            skipButton.gameObject.SetActive(canSkip);
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(OnSkipPressed);
        }
    }

    private TileModifierSO RollCard()
    {
        TileModifierType selectedType = RollType();

        List<TileModifierSO> filtered = allModifiers.FindAll(m => m.modifierType == selectedType);
        if (filtered.Count == 0) return allModifiers[Random.Range(0, allModifiers.Count)];

        return RollRarity(filtered);
    }

    private TileModifierType RollType()
    {
        float total = 0f;
        foreach (var w in typeWeights) total += w.Value;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        foreach (var w in typeWeights)
        {
            cumulative += w.Value;
            if (roll <= cumulative) return w.Key;
        }

        return TileModifierType.Fertile;
    }

    private TileModifierSO RollRarity(List<TileModifierSO> filtered)
    {
        float luck = StatManager.Instance.GetFinalStat(StatType.MutationLuck, StatTarget.Mutation);

        Dictionary<TileRarity, float> rarityWeights = new()
        {
            { TileRarity.Common,    Mathf.Max(10f, 60f - luck * 30f) },
            { TileRarity.Rare,      25f + luck * 10f },
            { TileRarity.Epic,      12f + luck * 12f },
            { TileRarity.Legendary,  3f + luck * 8f  }
        };

        float total = 0f;
        foreach (var w in rarityWeights) total += w.Value;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        TileRarity selectedRarity = TileRarity.Common;
        foreach (var w in rarityWeights)
        {
            cumulative += w.Value;
            if (roll <= cumulative) { selectedRarity = w.Key; break; }
        }

        List<TileModifierSO> rarityFiltered = filtered.FindAll(m => m.rarity == selectedRarity);
        if (rarityFiltered.Count == 0) rarityFiltered = filtered;

        return rarityFiltered[Random.Range(0, rarityFiltered.Count)];
    }

    private void OnCardSelected(TileModifierSO modifier)
    {
        // Sadece CardSelection state'inde çalış
        if (GameManager.Instance.CurrentState != GameStates.CardSelection)
            return;

        ProgressionManager.Instance.ApplyRandomEligibleCell(modifier);

        bool hasMore = RoundManager.Instance.OnCardSelectionComplete();

        if (hasMore)
            RefreshCards();
    }

    private void OnSkipPressed()
    {
        if (!RoundManager.Instance.TryUseSkip()) return;

        // 3 karttan en rare olanı bul
        TileRarity highest = TileRarity.Common;
        foreach (var card in currentCards)
            if (card.rarity > highest) highest = card.rarity;

        // Ödül = base × (1 + round × 0.1)
        int round = RoundManager.Instance.CurrentRound;
        int baseReward = skipBaseRewards[highest];
        int reward = Mathf.RoundToInt(baseReward * (1f + round * 0.1f));

        // Random resource seç
        ResourceType[] resources = { ResourceType.Gold, ResourceType.Iron, ResourceType.Stone };
        ResourceType chosen = resources[Random.Range(0, resources.Length)];

        ResourceManager.Instance.AddResource(chosen, reward);
        Debug.Log($"Skip! {highest} → {chosen} x{reward}");

        // Level takas — seçimi tüket
        bool hasMore = RoundManager.Instance.OnCardSelectionComplete();
        if (hasMore)
            RefreshCards();
    }
}