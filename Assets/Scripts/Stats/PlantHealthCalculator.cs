using UnityEngine;

public static class PlantHealthCalculator
{
    private static PlantHealthScalingSO rules;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => rules = null;

    public static int Calculate(PlantSO plant, int round)
    {
        if (rules == null) rules = Resources.Load<PlantHealthScalingSO>("PlantHealthScaling");
        return rules != null ? rules.Calculate(plant, round) : Mathf.Max(1, plant.maxHealth);
    }
}
