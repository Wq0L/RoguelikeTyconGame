// Bir vuruşun zincir bağlamı (Bölüm 3.7.6): hangi doğrudan saldırıdan (kök) ve hangi nesilden geldiği. Vuruşla birlikte
// taşınır; bitki ölünce onu öldüren vuruşun bağlamı saklanır (PlantHealth.KillLink). Gecikmeli vuran kasırga ve bumerang kendi
// bağlamını taşır; genel bir "şu an zincirdeyiz" durumu yoktur.
// Root 0: zincir kökü yok (ödül alınmamış, ya da kök dışı vuruş). Generation: −1 doğrudan vuruş, 0 doğrudan hasadın normal
// davranışı, 1 … zincirden doğan davranış. Echo: Artçı Patlama / Çifte Akım ikinci darbesi; öldürdüğü bitki zincir başlatmaz.
// Source (Bölüm 3.8): vuruş bir boss ödülü etkisinden geldiyse hangisi (RewardSource; 0: değil). Ödül etkisi vuruşları kök
// taşımaz (Root 0): öldürdükleri bitki zincir başlatmaz ve zincir sayaçlarına girmez.
public readonly struct HarvestLink
{
    public readonly int Root;
    public readonly sbyte Generation;
    public readonly bool Echo;
    public readonly RewardSource Source;

    private HarvestLink(int root, int generation, bool echo, RewardSource source = RewardSource.None)
    {
        Root = root;
        Generation = (sbyte)generation;
        Echo = echo;
        Source = source;
    }

    public static readonly HarvestLink None = new(0, 0, false);
    public static readonly HarvestLink Aftershock = new(0, 0, true);
    public static HarvestLink Direct(int root) => new(root, -1, false);
    public static HarvestLink Behavior(int root, int generation) => new(root, generation, false);
    // Boss ödülü etkisinin vuruşu (DamageType.Reward): kök yok, zincire katılmaz.
    public static HarvestLink Reward(RewardSource source) => new(0, 0, false, source);
    // Ödül etkisinin çalıştırdığı gerçek davranış (Aşırı Yükleme'nin ek proc'u): artçı gibi zincir dışıdır.
    public static HarvestLink Excluded(RewardSource source) => new(0, 0, true, source);

    public bool IsDirect => Generation < 0;
    public bool IsChain => Generation > 0;
}

// Boss ödülü etkisinden gelen vuruşun kaynağı (ölçüm, test ve Play test penceresi). Sona eklenir.
public enum RewardSource : byte
{
    None = 0,
    Antenna = 1,
    Line = 2,
    LineExtra = 3,
    Crescent = 4,
    CrescentReturn = 5,
    CritArc = 6,
    HarvestEcho = 7,
    Overflow = 8,
    CritBurst = 9,
    Overload = 10,
}
