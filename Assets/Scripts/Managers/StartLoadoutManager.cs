using System.Collections.Generic;
using UnityEngine;

// Run başı seçimin (çiftçi + tırpan) etkilerinin tek sahibi. RoundManager kurar (SpecializationManager gibi).
// - Yeni run'da (OnRunStarted) kayıttaki seçim çözülür: bilinmeyen, silinmiş ya da kilitli id → Bahçıvan + Standart.
//   Seçim o anda sabitlenir; run sürerken kayıt ya da menü değişse de aktif build değişmez.
// - Stat etkileri StatManager global modifier olarak eklenir; yalnız bu sistemin eklediği kaldırılır.
//   StatManager listeyi toptan temizlerse (ana menü) sahip olunan kayıtlar unutulur, başka sistemin modifier'ı silinmez.
// - Katsayılar tek noktada, bir kez: doğrudan vuruş (PlayerController), hasat kaynağı (SpecializationManager.ScaleHarvestResource
//   uzmanlaşmayla tek oran), hasat skoru (HarvestScoreManager). Kaynak ve skorda kesirli kalan taşınır.
// - Temizlik yolu tek: yeni run başı, ana menü, yok edilme (yeniden başlatma ve sahne değişimi).
[DisallowMultipleComponent]
public sealed class StartLoadoutManager : MonoBehaviour
{
    public static StartLoadoutManager Instance { get; private set; }

    public FarmerSO Farmer { get; private set; }
    public ScytheSO Scythe { get; private set; }
    // Kayıttaki id geçersiz ya da kilitli olduğu için varsayılana dönüldü.
    public bool UsedFallback { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyList<StatModifier> OwnedModifiers => owned;

    public static float DirectDamageMultiplier => Instance != null ? Instance.direct : 1f;
    public static float HarvestResourceMultiplier => Instance != null ? Instance.resource : 1f;
    public static float HarvestScoreMultiplier => Instance != null ? Instance.scoreMultiplier : 1f;

    // Yalnız hasat skoru (HarvestScoreManager.AddScore) çağırır.
    public static int ScaleScore(int award) => Instance != null ? Instance.score.Apply(award, Instance.scoreMultiplier) : award;

    private RoundManager rounds;
    private GameManager game;
    private StatManager stats;
    private readonly List<StatModifier> owned = new();
    private readonly FractionalScale score = new();
    private float direct = 1f, resource = 1f, scoreMultiplier = 1f;

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds != null) rounds.OnRunStarted += Apply;
    }

    private void OnDisable()
    {
        if (rounds != null) rounds.OnRunStarted -= Apply;
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        Clear();
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
        if (state == GameStates.MainMenu) Clear();
    }

    // Kayıttaki seçim → geçerli içerik. Katalog varsayılanları her zaman açık ve nötrdür.
    public static (FarmerSO farmer, ScytheSO scythe, bool fallback) Resolve(StartCatalogSO catalog)
    {
        if (DemoSceneSettings.IsDemo && DemoSceneSettings.Instance.defaultLoadoutOnly)
            return (catalog.defaultFarmer, catalog.defaultScythe, false);
        MetaSaveData save = MetaSave.Data;
        FarmerSO farmer = catalog.Farmer(save.farmer);
        ScytheSO scythe = catalog.Scythe(save.scythe);
        bool fallback = false;
        if (farmer == null || !MetaSave.IsUnlocked(farmer)) { fallback |= !string.IsNullOrEmpty(save.farmer); farmer = catalog.defaultFarmer; }
        if (scythe == null || !MetaSave.IsUnlocked(scythe)) { fallback |= !string.IsNullOrEmpty(save.scythe); scythe = catalog.defaultScythe; }
        return (farmer, scythe, fallback);
    }

    public void Apply()
    {
        Clear();
        StartCatalogSO catalog = StartCatalogSO.Active;
        if (catalog == null) return;
        (Farmer, Scythe, UsedFallback) = Resolve(catalog);
        stats = StatManager.Instance;
        if (stats != null) stats.OnGlobalModifiersCleared += Forget;
        foreach (StartOptionSO option in new StartOptionSO[] { Farmer, Scythe })
        {
            if (option == null) continue;
            foreach (StatModifier modifier in option.modifiers)
            {
                if (stats == null) break;
                stats.AddGlobalModifier(modifier);
                owned.Add(modifier);
            }
            direct *= option.directDamageMultiplier;
            resource *= option.harvestResourceMultiplier;
            scoreMultiplier *= option.harvestScoreMultiplier;
        }
        Version++;
    }

    public void Clear()
    {
        // StatModifier değer tipidir: aynı değerde başka bir modifier varsa biri silinir, hesap aynı kalır (çoklu küme).
        // Sahip olunmayan bir kopya asla silinmez: toptan temizlikten sonra owned boşaltılır.
        if (stats != null)
            foreach (StatModifier modifier in owned)
                if (Contains(stats.GlobalModifiers, modifier)) stats.RemoveGlobalModifier(modifier);
        owned.Clear();
        Unwatch();
        direct = resource = scoreMultiplier = 1f;
        score.Clear();
        Farmer = null;
        Scythe = null;
        UsedFallback = false;
        Version++;
    }

    private void Unwatch()
    {
        if (stats != null) stats.OnGlobalModifiersCleared -= Forget;
        stats = null;
    }

    private void Forget() => owned.Clear();

    private static bool Contains(IReadOnlyList<StatModifier> list, StatModifier modifier)
    {
        for (int i = 0; i < list.Count; i++) if (list[i].Equals(modifier)) return true;
        return false;
    }
}

// Tek kanallı kesirli çarpan (HarvestResourceScale ile aynı yaklaşım): ödenen toplam = round(ham toplam × oran), kalan taşınır.
// Küçük değerlerde (Common skoru 1 × 0,9) tek tek yuvarlama bedeli ya da bonusu yok etmesin.
public sealed class FractionalScale
{
    private const long Scale = 10000;
    private const long Limit = long.MaxValue / (Scale * 4);
    private long raw, paid, rate = Scale;

    public int Apply(int value, float multiplier)
    {
        if (value <= 0) return value;
        long r = (long)System.Math.Round(multiplier * (double)Scale);
        if (r == Scale) return value;
        if (r != rate || raw > Limit) { Clear(); rate = r; }
        raw += value;
        long due = (raw * rate + Scale / 2) / Scale;
        int give = NumericSafety.ToInt((double)(due - paid), 0, NumericSite.Score);
        paid = due;
        return give;
    }

    public void Clear()
    {
        raw = paid = 0;
        rate = Scale;
    }
}
