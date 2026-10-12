// Hasarın kaynağı. Yeni kaynak eklerken: enum'a SONA ekle + aşağıdaki kurallara bak.
public enum DamageType
{
    Direct,     
    Explosion,
    Tornado,
    Boomerang,
    Electric,
    // Boss ödülü etkisinin vuruşu (Bölüm 3.8: anten, hat, hilal, ark, yankı, taşma, kritik patlama). Doğrudan vuruş değildir ve
    // dört davranıştan biri de değildir: saksı bonusu almaz, davranış tetiklemez, davranış rezonansı ve davranış çarpanı uygulanmaz.
    // Hangi etkiden geldiği vuruşun bağlamındadır (HarvestLink.Source).
    Reward
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
