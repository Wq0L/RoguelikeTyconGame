// Bir vuruşun zincir bağlamı (Bölüm 3.7.6): hangi doğrudan saldırıdan (kök) ve hangi nesilden geldiği. Vuruşla birlikte
// taşınır; bitki ölünce onu öldüren vuruşun bağlamı saklanır (PlantHealth.KillLink). Gecikmeli vuran kasırga ve bumerang kendi
// bağlamını taşır; genel bir "şu an zincirdeyiz" durumu yoktur.
// Root 0: zincir kökü yok (ödül alınmamış, ya da kök dışı vuruş). Generation: −1 doğrudan vuruş, 0 doğrudan hasadın normal
// davranışı, 1 … zincirden doğan davranış. Echo: Artçı Patlama / Çifte Akım ikinci darbesi; öldürdüğü bitki zincir başlatmaz.
public readonly struct HarvestLink
{
    public readonly int Root;
    public readonly sbyte Generation;
    public readonly bool Echo;

    private HarvestLink(int root, int generation, bool echo)
    {
        Root = root;
        Generation = (sbyte)generation;
        Echo = echo;
    }

    public static readonly HarvestLink None = new(0, 0, false);
    public static readonly HarvestLink Aftershock = new(0, 0, true);
    public static HarvestLink Direct(int root) => new(root, -1, false);
    public static HarvestLink Behavior(int root, int generation) => new(root, generation, false);

    public bool IsDirect => Generation < 0;
    public bool IsChain => Generation > 0;
}
