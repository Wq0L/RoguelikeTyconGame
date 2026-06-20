using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardSelectionUI : MonoBehaviour
{
    [SerializeField] private List<TileModifierSO> allModifiers; // tüm SO'lar buraya
    [SerializeField] private List<CardUI> cardSlots;            // 3 kart slotu

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

    // UIManager her kart seçim ekranı açılışında bunu çağırır
    public void RefreshCards()
    {
        for (int i = 0; i < cardSlots.Count; i++)
        {
            TileModifierSO rolled = RollCard();
            cardSlots[i].Setup(rolled, OnCardSelected);
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
}