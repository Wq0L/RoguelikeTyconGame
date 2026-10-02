using System.Collections.Generic;
using UnityEngine;

// Boss sonrası bedelli uzmanlaşma (run başına bir seçim). RoundManager kurar; ayarlar aktif run profilinden gelir.
// - Profilin segmenti kotayla kapanınca (run'ın son round'u değilse) seçim bekler; kart seçimlerinden sonra açılır.
// - Seçim tek seferdir: bekleyen seçim yokken ya da seçilmişken istek reddedilir (çift tıklama, panelin yeniden açılması).
// - Etki hasar katsayıları ve hasat kaynağı çarpanıdır; yeni run'da (OnRunStarted), ana menüde ve yok edilmede aynı ClearAll ile
//   temizlenir (kaynak kalanları dahil).
// Arayüz seçenekleri gösterir ve Choose ister; kural burada.
[DisallowMultipleComponent]
public sealed class SpecializationManager : MonoBehaviour, IRoundChoice
{
    public static SpecializationManager Instance { get; private set; }

    private RoundManager rounds;
    private GameManager game;
    private bool resolved;

    private HarvestResourceScale harvestResources => RunPower.Resources;

    public SpecializationSO Chosen { get; private set; }
    public bool IsPending { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyList<SpecializationSO> Options =>
        rounds != null && rounds.Profile != null ? rounds.Profile.specializationOptions : (IReadOnlyList<SpecializationSO>)System.Array.Empty<SpecializationSO>();

    public static float DirectMultiplier => Instance != null && Instance.Chosen != null ? Instance.Chosen.directDamageMultiplier : 1f;
    public static float BehaviorMultiplier => Instance != null && Instance.Chosen != null ? Instance.Chosen.behaviorDamageMultiplier : 1f;
    public static float HarvestResourceMultiplier => Instance != null && Instance.Chosen != null ? Instance.Chosen.harvestResourceMultiplier : 1f;
    public HarvestResourceScale HarvestResources => harvestResources;

    // Yalnız hasat ödülü (PlantResource) çağırır: duplicate dahil hesaplanmış ödüle bir kez. Diğer kaynak eklemeleri buradan geçmez.
    // Oran = uzmanlaşma × başlangıç seçimi (Tüccar gibi); tek oran, tek kesir taşıma.
    public static int ScaleHarvestResource(ResourceType type, int reward) =>
        RunPower.ScaleHarvestResource(type, reward);

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += ClearAll;
        rounds.OnRoundEnded += HandleRoundEnded;
        rounds.RegisterRoundChoice(this);
    }

    private void OnDisable()
    {
        if (rounds != null)
        {
            rounds.OnRunStarted -= ClearAll;
            rounds.OnRoundEnded -= HandleRoundEnded;
            rounds.UnregisterRoundChoice(this);
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        ClearAll();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu) ClearAll();
    }

    // Kota değerlendirmesi OnRoundEnded'den önce yapılır; burada sonucu okunur.
    private void HandleRoundEnded()
    {
        RunProfileSO profile = rounds.Profile;
        if (resolved || IsPending || profile == null || profile.specializationAfterSegment <= 0 || profile.specializationOptions.Count == 0) return;
        int round = rounds.CurrentRound;
        if (!rounds.IsQuotaSegmentEnd(round) || round / rounds.QuotaSegmentRounds != profile.specializationAfterSegment) return;
        // Kota tutmadıysa ödül yok; run'ın son round'uysa kullanılamayacak seçim açılmaz.
        if (rounds.RunFailed || round >= rounds.MaxRounds) return;
        IsPending = true;
        Version++;
    }

    public bool Choose(SpecializationSO option)
    {
        if (!IsPending || resolved || option == null || !ContainsOption(option)) return false;
        Chosen = option;
        IsPending = false;
        resolved = true;
        Version++;
        rounds.ContinueAfterRoundChoice();
        return true;
    }

    private bool ContainsOption(SpecializationSO option)
    {
        foreach (SpecializationSO o in Options) if (o == option) return true;
        return false;
    }

    // Tek temizlik yolu: yeni run, ana menü, yok edilme (yeniden başlatma ve sahne değişimi).
    public void ClearAll()
    {
        Chosen = null;
        IsPending = false;
        resolved = false;
        harvestResources.Clear();
        Version++;
    }
}

// Hasat kaynak çarpanı, kaynak başına run toplamı üzerinden: ödenen toplam = round(ham toplam × oran).
// Ödüller küçük tam sayılar (2, 4 …) olduğu için tek tek yuvarlama cezayı yok eder ya da (aşağı yuvarlamada) katlar;
// burada kesirli kalan taşınır, sonuç deterministiktir ve toplam hiçbir anda hedeften yarım birimden fazla sapmaz.
public sealed class HarvestResourceScale
{
    private const long Scale = 10000;
    private static readonly int Count = System.Enum.GetValues(typeof(ResourceType)).Length;
    private readonly long[] raw = new long[Count], paid = new long[Count];
    private long rate = Scale;

    public long Raw(ResourceType type) => raw[(int)type];
    public long Paid(ResourceType type) => paid[(int)type];

    public int Apply(ResourceType type, int reward, float multiplier)
    {
        if (reward <= 0) return reward;
        long r = (long)System.Math.Round(multiplier * (double)Scale);
        if (r == Scale) return reward;
        if (r != rate) { Clear(); rate = r; } // oran değişirse (yeni seçim) eski kalanlar taşınmaz
        int i = (int)type;
        raw[i] += reward;
        long due = (raw[i] * rate + Scale / 2) / Scale;
        int give = (int)(due - paid[i]);
        paid[i] = due;
        return give;
    }

    public void Clear()
    {
        System.Array.Clear(raw, 0, raw.Length);
        System.Array.Clear(paid, 0, paid.Length);
        rate = Scale;
    }
}
