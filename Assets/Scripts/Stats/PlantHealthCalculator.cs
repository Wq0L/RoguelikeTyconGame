using UnityEngine;

public static class PlantHealthCalculator
{
    private static PlantHealthScalingSO rules;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => rules = null;

    public static int Calculate(PlantSO plant, int round)
    {
        // Run profilinin denge seti kendi can eğrisini getirebilir (Bölüm 3.5); yoksa ortak eğri.
        RunBalanceSO balance = RunBalanceSO.Active;
        if (balance != null && balance.plantHealth != null) return balance.plantHealth.Calculate(plant, round);
        if (rules == null) rules = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        return rules != null ? rules.Calculate(plant, round) : Mathf.Max(1, plant.maxHealth);
    }
}
