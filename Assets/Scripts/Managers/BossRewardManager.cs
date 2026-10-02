using System.Collections.Generic;
using UnityEngine;

// Boss ödülleri (run buff'ları). RoundManager kurar; havuz aktif run profilinden gelir (RunProfileSO.bossRewards).
// - Boss round'u iki koşulla (segment kotası + boss hasadı) geçilince, run'ın son round'u değilse, teklif hazırlanır ve
//   seçim bekler (IRoundChoice): kartlardan sonra açılır, seçilmeden sonraki round başlamaz.
// - Bir boss'tan tek ödül: bekleyen teklif yokken ya da teklifte olmayan ödül için istek reddedilir (çift tıklama, yeniden açılma).
// - Teklif: uygun ödüllerden ağırlıklı, tekrarsız en çok N seçenek. Uygun ödül azsa o kadar; hiç yoksa boş teklif "devam" ile geçilir.
// - Rastgelelik run'ın boss seed'i + segmentten türeyen ayrı bir System.Random'dır; UnityEngine.Random akışına dokunmaz.
// - Etkiler: stat modifier'ları StatManager'a eklenir ve sahiplenilir; doğrudan vuruş katsayısı PlayerController'da bir kez çarpılır.
// - Temizlik tek yol: yeni run, ana menü, yok edilme. Yalnız bu sistemin eklediği modifier'lar kaldırılır. Kalıcı kayda yazılmaz.
[DisallowMultipleComponent]
public sealed class BossRewardManager : MonoBehaviour, IRoundChoice
{
    public static BossRewardManager Instance { get; private set; }

    private const float AttackIntervalFloor = 0.1f; // PlayerController tabanı

    private RoundManager rounds;
    private GameManager game;
    private StatManager stats;
    private readonly List<BossRewardSO> offer = new();
    private readonly List<BossRewardSO> taken = new();
    private readonly Dictionary<BossRewardSO, int> stacks = new();
    private readonly List<StatModifier> owned = new();
    private float direct = 1f;
    private float behavior = 1f;
    // Nadirliğe bağlı doğrudan vuruş çarpanı: dizin PlantRarity; "Rare ve üstü ×1,5" → Rare, Epic, Legendary hücreleri.
    private readonly float[] rareDirect = { 1f, 1f, 1f, 1f, 1f };
    // Kırılma etkileri (Bölüm 3.6): alınmış ödülün ayarı. Etkiyi BehaviorEchoes (ikinci darbe) ve RunPower.Rhythm (Hasat Ritmi) uygular.
    private BehaviorEcho explosionEcho, electricEcho;
    private bool hasExplosionEcho, hasElectricEcho, hasRhythm;
    private HarvestRhythmConfig rhythm;
    private int rewardedRound;
    private int offerRound;
    private bool offerPrepared;
    // Tarla doluluğu: round sırasında yarım saniyede bir, bitkisi olan üretim noktalarının oranı. "Tarla boşalıyor" koşulu okur.
    private float occupancySum, nextOccupancySample;
    private int occupancySamples;
    public float LastRoundOccupancy { get; private set; } = 1f;

    public bool IsPending { get; private set; }
    public int Version { get; private set; }
    // Bekleyen teklif. Bekleme varken boşsa: uygun ödül kalmadı (ContinueWithoutReward).
    public IReadOnlyList<BossRewardSO> Offer { get { PrepareOffer(); return offer; } }
    // Alınan ödüller, ilk alınış sırasıyla (her ödül bir kez; adet Stacks ile).
    public IReadOnlyList<BossRewardSO> Taken => taken;
    public IReadOnlyList<StatModifier> OwnedModifiers => owned;
    public int Stacks(BossRewardSO reward) => reward != null && stacks.TryGetValue(reward, out int n) ? n : 0;

    public static float DirectDamageMultiplier => Instance != null ? Instance.direct : 1f;
    // Davranış hasarı çarpanı (Yıkım Gücü): PlanterBrain.BehaviorDamageMultiplier bir kez çarpar; doğrudan vuruşa girmez.
    public static float BehaviorDamageMultiplier => Instance != null ? Instance.behavior : 1f;
    // Nadirliğe bağlı doğrudan vuruş çarpanı (Altın Hedef): PlayerController hedef başına bir kez çarpar.
    public static float RareDirectMultiplier(PlantRarity rarity)
    {
        int index = (int)rarity;
        return Instance != null && index >= 0 && index < Instance.rareDirect.Length ? Instance.rareDirect[index] : 1f;
    }

    public static bool TryGetEcho(DamageType type, out BehaviorEcho echo)
    {
        echo = default;
        if (Instance == null) return false;
        if (type == DamageType.Explosion && Instance.hasExplosionEcho) { echo = Instance.explosionEcho; return true; }
        if (type == DamageType.Electric && Instance.hasElectricEcho) { echo = Instance.electricEcho; return true; }
        return false;
    }

    public static bool TryGetRhythm(out HarvestRhythmConfig config)
    {
        config = Instance != null ? Instance.rhythm : default;
        return Instance != null && Instance.hasRhythm;
    }

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

    private void Update()
    {
        if (game == null || game.CurrentState != GameStates.Round || Time.time < nextOccupancySample) return;
        nextOccupancySample = Time.time + .5f;
        int points = 0, filled = 0;
        foreach (PlantSpawner spawner in PlantSpawner.Active)
        {
            if (spawner.GridObject == null) continue;
            points++;
            if (spawner.GridObject.HasPlantObject()) filled++;
        }
        if (points == 0) return;
        occupancySum += filled / (float)points;
        occupancySamples++;
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

    // Kota ve boss hedefi OnRoundEnded'den önce değerlendirilir; burada sonucu okunur.
    private void HandleRoundEnded()
    {
        LastRoundOccupancy = occupancySamples > 0 ? occupancySum / occupancySamples : 1f;
        occupancySum = 0f; occupancySamples = 0;
        RunProfileSO profile = rounds.Profile;
        if (IsPending || profile == null || profile.bossRewards == null) return;
        int round = rounds.CurrentRound;
        if (!rounds.IsBossRound(round) || rewardedRound == round) return;
        // Başarısız boss ödül vermez; run'ın son round'unda kullanılamayacak buff sunulmaz.
        if (rounds.RunFailed || round >= rounds.MaxRounds) return;
        rewardedRound = round;
        offerRound = round;
        offerPrepared = false;
        offer.Clear();
        IsPending = true;
        Version++;
    }

    // Cards (and specialization) can change eligibility. Freeze the offer only when reward selection is reached.
    public void PrepareOffer()
    {
        if (!IsPending || offerPrepared || GameManager.Instance == null ||
            GameManager.Instance.CurrentState != GameStates.RoundChoice ||
            (SpecializationManager.Instance != null && SpecializationManager.Instance.IsPending)) return;
        BuildOffer(rounds.Profile.bossRewards, HarvestQuota.SegmentOf(offerRound, rounds.QuotaSegmentRounds));
        offerPrepared = true;
        Version++;
    }

    private void BuildOffer(BossRewardPoolSO pool, int segment)
    {
        offer.Clear();
        var candidates = new List<BossRewardSO>();
        // Ödülün sunulabildiği boss round'ları veride (minRound / maxRound): erken ve geç ödüllerin gücü farklı olabilir.
        foreach (BossRewardSO reward in pool.rewards)
            if (IsEligible(reward) && reward.OfferedAt(offerRound) && !candidates.Contains(reward)) candidates.Add(reward);
        // Kırılma ödülleri (Bölüm 3.6): sunulabilir ve alınmamış olan varsa bir slot onlardan gelir.
        var breakthroughs = new List<BossRewardSO>();
        if (pool.breakthroughs != null)
            foreach (BossRewardSO reward in pool.breakthroughs)
                if (IsEligible(reward) && reward.OfferedAt(offerRound) && !breakthroughs.Contains(reward)) breakthroughs.Add(reward);
        SegmentEventDirector events = SegmentEventDirector.Instance;
        int seed = events != null ? events.RunSeed : 0;
        var rng = new System.Random(SegmentEventDirector.Mix(seed, segment, 0x52574452)); // 'RWDR'
        int slots = Mathf.Max(1, pool.choices);
        BossRewardSO featured = null;
        if (breakthroughs.Count > 0)
        {
            featured = breakthroughs[Pick(breakthroughs, rng)];
            candidates.Remove(featured);
            slots--;
        }
        int count = Mathf.Min(slots, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int pick = Pick(candidates, rng);
            offer.Add(candidates[pick]);
            candidates.RemoveAt(pick);
        }
        // Kırılma ödülünün ekrandaki yeri de rastgeledir (hep aynı kartta durmasın).
        if (featured != null) offer.Insert(rng.Next(offer.Count + 1), featured);
    }

    // Ağırlıklı tek seçim. Havuzda kırılma ödülü yokken çağrı sırası ve sayısı eskisiyle aynıdır (aynı seed aynı teklif).
    private static int Pick(List<BossRewardSO> candidates, System.Random rng)
    {
        double total = 0;
        foreach (BossRewardSO c in candidates) total += c.weight;
        double roll = rng.NextDouble() * total;
        for (int k = 0; k < candidates.Count; k++)
        {
            roll -= candidates[k].weight;
            if (roll < 0) return k;
        }
        return candidates.Count - 1;
    }

    // Sunulabilir mi: sınırına ulaşmamış, koşulu sağlanan ve en az bir etkisi şu an bir şey değiştirebilen ödül.
    public bool IsEligible(BossRewardSO reward)
    {
        if (reward == null || reward.weight <= 0f || Stacks(reward) >= reward.maxStacks) return false;
        if (reward.condition == BossRewardCondition.AnyPlanterHasModifiedStat && !AnyPlanterHasStat(reward)) return false;
        if (reward.condition == BossRewardCondition.AnyBehaviorPlanter && !AnyPlanter(HasBehavior, false)) return false;
        if (reward.condition == BossRewardCondition.FieldRunsLow && LastRoundOccupancy >= reward.conditionThreshold) return false;
        if (reward.condition == BossRewardCondition.AnyPlanterHasConditionStat &&
            !AnyPlanter(p => p.GetFinalStat(reward.conditionStat) > 0f, false)) return false;
        // Kırılma ödülü: etkisi stat ya da çarpan değil, ayrı bir mekaniktir; koşulu sağlanıyorsa sunulabilir.
        if (reward.IsBreakthrough) return true;
        if (!Mathf.Approximately(reward.directDamageMultiplier, 1f) || !Mathf.Approximately(reward.rareDirectMultiplier, 1f) ||
            !Mathf.Approximately(reward.behaviorDamageMultiplier, 1f)) return true;
        foreach (StatModifier modifier in reward.modifiersPerStack)
            if (CanStillChange(modifier)) return true;
        return false;
    }

    private static bool AnyPlanterHasStat(BossRewardSO reward)
    {
        foreach (PlanterBrain planter in FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None))
        {
            if (planter.OccupiedGrids.Count == 0) continue;
            foreach (StatModifier modifier in reward.modifiersPerStack)
                if (planter.GetFinalStat(modifier.statType) > 0f) return true;
        }
        return false;
    }

    // Mevcut sınır kurallarıyla: tabandaki saldırı aralığı, tavandaki nadirlik bonusu ve %100'deki şans daha fazla değişmez.
    private static bool CanStillChange(StatModifier modifier)
    {
        StatManager manager = StatManager.Instance;
        if (manager == null) return true;
        switch (modifier.statType)
        {
            case StatType.AttackSpeed:
                return manager.GetFinalStat(StatType.AttackSpeed, StatTarget.Player) > AttackIntervalFloor + 1e-4f;
            case StatType.RareSpawnChance:
                return AnyPlanter(p => p.GetFinalStat(StatType.RareSpawnChance) < StatCalculator.ClampStat(StatType.RareSpawnChance, float.MaxValue) - 1e-3f, true);
            case StatType.ExplosionChance:
            case StatType.TornadoChance:
            case StatType.BoomerangChance:
            case StatType.ElectricChance:
            case StatType.DuplicateChance:
                return AnyPlanter(p => { float chance = p.GetFinalStat(modifier.statType); return chance > 0f && chance < 1f - 1e-4f; }, false);
            default:
                return true;
        }
    }

    private static bool HasBehavior(PlanterBrain planter) =>
        planter.GetFinalStat(StatType.ExplosionChance) > 0f || planter.GetFinalStat(StatType.TornadoChance) > 0f ||
        planter.GetFinalStat(StatType.BoomerangChance) > 0f || planter.GetFinalStat(StatType.ElectricChance) > 0f;

    private static bool AnyPlanter(System.Func<PlanterBrain, bool> test, bool whenNone)
    {
        bool any = false;
        foreach (PlanterBrain planter in FindObjectsByType<PlanterBrain>(FindObjectsSortMode.None))
        {
            if (planter.OccupiedGrids.Count == 0) continue;
            any = true;
            if (test(planter)) return true;
        }
        return !any && whenNone;
    }

    public bool Choose(BossRewardSO reward)
    {
        PrepareOffer();
        if (!IsPending || !offerPrepared || reward == null || !offer.Contains(reward) || Stacks(reward) >= reward.maxStacks) return false;
        Apply(reward);
        Resolve();
        return true;
    }

    // Uygun ödül kalmadığında (boş teklif) oyuncuyu kilitlemeden devam.
    public bool ContinueWithoutReward()
    {
        PrepareOffer();
        if (!IsPending || !offerPrepared || offer.Count > 0) return false;
        Resolve();
        return true;
    }

    private void Resolve()
    {
        IsPending = false;
        offer.Clear();
        Version++;
        rounds.ContinueAfterRoundChoice();
    }

    private void Apply(BossRewardSO reward)
    {
        if (stats == null)
        {
            stats = StatManager.Instance;
            if (stats != null) stats.OnGlobalModifiersCleared += Forget;
        }
        foreach (StatModifier modifier in reward.modifiersPerStack)
        {
            if (stats == null) break;
            stats.AddGlobalModifier(modifier);
            owned.Add(modifier);
        }
        direct *= reward.directDamageMultiplier;
        behavior *= reward.behaviorDamageMultiplier;
        if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f))
            for (int i = Mathf.Max(0, (int)reward.rareDirectFrom); i < rareDirect.Length; i++) rareDirect[i] *= reward.rareDirectMultiplier;
        if (reward.echo == BossRewardEcho.Explosion)
        { explosionEcho = new BehaviorEcho(reward.echoDelay, reward.echoDamage, reward.echoRadius); hasExplosionEcho = true; }
        if (reward.echo == BossRewardEcho.Electric)
        { electricEcho = new BehaviorEcho(reward.echoDelay, reward.echoDamage, 1f); hasElectricEcho = true; }
        if (reward.rhythmHarvests > 0)
        { rhythm = new HarvestRhythmConfig(reward.rhythmHarvests, reward.rhythmDamage, reward.rhythmRadius); hasRhythm = true; }
        if (!taken.Contains(reward)) taken.Add(reward);
        stacks[reward] = Stacks(reward) + 1;
    }

    // Tek temizlik yolu: yeni run, ana menü, yok edilme (yeniden başlatma ve sahne değişimi).
    public void ClearAll()
    {
        if (stats != null)
        {
            // Aynı ödül birden çok alındıysa aynı değer listede birden çok kez durur; her biri bir kez kaldırılır.
            foreach (StatModifier modifier in owned)
                if (Contains(stats.GlobalModifiers, modifier)) stats.RemoveGlobalModifier(modifier);
            stats.OnGlobalModifiersCleared -= Forget;
        }
        stats = null;
        owned.Clear();
        direct = 1f;
        behavior = 1f;
        for (int i = 0; i < rareDirect.Length; i++) rareDirect[i] = 1f;
        hasExplosionEcho = hasElectricEcho = hasRhythm = false;
        explosionEcho = electricEcho = default;
        rhythm = default;
        offerRound = 0;
        offerPrepared = false;
        occupancySum = 0f; occupancySamples = 0; LastRoundOccupancy = 1f;
        stacks.Clear();
        taken.Clear();
        offer.Clear();
        IsPending = false;
        rewardedRound = 0;
        Version++;
    }

    // StatManager listeyi toptan temizledi: sahiplik unutulur (başka sistemin modifier'ı sonradan silinmesin).
    // Alınan ödül listesi run sonu ekranı için kalır.
    private void Forget() => owned.Clear();

    private static bool Contains(IReadOnlyList<StatModifier> list, StatModifier modifier)
    {
        for (int i = 0; i < list.Count; i++) if (list[i].Equals(modifier)) return true;
        return false;
    }
}

