// Hasarın kaynağı. Yeni kaynak eklerken: enum'a SONA ekle + aşağıdaki kurallara bak.
public enum DamageType
{
    Direct,     
    Explosion,
    Tornado,
    Boomerang,
    Electric
}

// Hasar tipine göre davranış kuralları TEK yerde.
// PlantHealth / PlantSpawner tip tip if yazmaz, buraya sorar.
// "this DamageType" = extension method → type.UsesPlanterBonus() diye çağırıyorsun.
public static class DamageTypeRules
{
    // Odak tile'ının PlanterDamageMultiplier bonusu bu hasara uygulanır mı?
    public static bool UsesPlanterBonus(this DamageType type)
    {
        return type == DamageType.Direct;
    }

    // Bu hasarla ölen bitki patlama/tornado tetikleyebilir mi? (zincir koruması)
    public static bool CanTriggerBehaviors(this DamageType type)
    {
        return type == DamageType.Direct;
    }
}
