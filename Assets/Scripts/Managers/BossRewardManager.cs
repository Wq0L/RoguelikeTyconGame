using System.Collections.Generic;
using UnityEngine;

// Boss ödülleri (run buff'ları). RoundManager kurar; havuz aktif run profilinden gelir (RunProfileSO.bossRewards).
// - Boss round'u iki koşulla (segment kotası + boss hasadı) geçilince teklif hazırlanır ve seçim bekler (IRoundChoice):
//   kartlardan sonra açılır, seçilmeden sonraki round başlamaz. Run'ın son round'unda teklif yalnız açık boss takviminde
//   (son round boss round'u; Bölüm 3.7.2) açılır: seçimden sonra zafer ekranı gelir, alınan ödül run sonu listesinde görünür.
// - Bir boss'tan tek ödül: bekleyen teklif yokken ya da teklifte olmayan ödül için istek reddedilir (çift tıklama, yeniden açılma).
// - Teklif: uygun ödüllerden ağırlıklı, tekrarsız en çok N seçenek. Uygun ödül azsa o kadar; hiç yoksa boş teklif "devam" ile geçilir.
// - Aşamalı havuzda (BossRewardPoolSO.stages, Bölüm 3.7.3) adaylar boss round'unun aşamasından ve daha önce açılmış aşamalardan
//   gelir; mevcut aşamada uygun aday varsa bir slot ona ayrılır. Aşama round'ları ve ağırlık çarpanları havuz verisidir; aşama
//   yalnız teklifin round'undan çözülür (ayrı bir aşama durumu tutulmaz). Düz havuzlarda teklif kuralı eskisiyle aynıdır.
// - Rastgelelik run'ın boss seed'i + segmentten türeyen ayrı bir System.Random'dır; UnityEngine.Random akışına dokunmaz.
// - Etkiler: stat modifier'ları StatManager'a eklenir ve sahiplenilir; doğrudan vuruş katsayısı PlayerController'da bir kez çarpılır.
// - Kazanç / bedel (Bölüm 3.7.4): ödül level başına seçim hakkını değiştirebilir (levelChoiceDelta) ve bir dışlama grubuna ait
//   olabilir (exclusiveGroup). Alınabilirlik tek yerde denetlenir (BlockOf): stack sınırı, çakışan ödül, seçim hakkı aralığı, koşul.
//   Aynı denetim teklif hazırlanırken ve ödül alınırken çalışır. Uygulama tek işlemdir (TryApply): önce her şey doğrulanır,
//   sonra bütün etkiler bir kez yazılır; arada hata olursa eklenenler geri alınır ve hiçbir kayıt değişmez.
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
    // Level başına seçim hakkındaki aktif değişim: alınan ödüllerin levelChoiceDelta toplamı (RunPower.LevelChoices okur).
    private int levelChoices;
    // Nadirliğe bağlı doğrudan vuruş çarpanı: dizin PlantRarity; "Rare ve üstü ×1,5" → Rare, Epic, Legendary hücreleri.
    private readonly float[] rareDirect = { 1f, 1f, 1f, 1f, 1f };
    // Kırılma etkileri (Bölüm 3.6): alınmış ödülün ayarı. Etkiyi BehaviorEchoes (ikinci darbe) ve RunPower.Rhythm (Hasat Ritmi) uygular.
    private BehaviorEcho explosionEcho, electricEcho;
    private bool hasExplosionEcho, hasElectricEcho, hasRhythm, hasChain;
    private HarvestRhythmConfig rhythm;
    // Zincir Hasat (Bölüm 3.7.6): alınmış ödülün ayarı. Kuralları HarvestChain uygular.
    private HarvestChainConfig chain;
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
    // Bu run'ın ödül havuzu (profil vermediyse null).
    public BossRewardPoolSO Pool => rounds != null && rounds.Profile != null ? rounds.Profile.bossRewards : null;
    // Bekleyen teklifin aşaması (aşamalı havuzda; düz havuzda ya da teklif yokken null). Arayüz başlığı bunu okur.
    public BossRewardStage OfferStage => IsPending && Pool != null ? Pool.StageAt(offerRound) : null;
    // Ödülün yazıldığı aşama (aşamalı havuzda; yoksa null). Kartın sınıf etiketi bunu okur.
    public BossRewardStage StageOf(BossRewardSO reward)
    {
        BossRewardPoolSO pool = Pool;
        int index = pool != null ? pool.StageIndexOf(reward) : -1;
        return index >= 0 ? pool.stages[index] : null;
    }
    // Aşamanın ödüllerinin ilk sunulabildiği boss round'u: aşamanın ilk round'undan itibaren ilk boss (run takviminden).
    public int FirstOfferRound(BossRewardStage stage) => stage != null && rounds != null ? rounds.Calendar.NextBossRound(stage.firstRound) : 0;
    // Kartta gösterilen açıklama: aşamalı havuzun kendi metni varsa o, yoksa ödül asset'indeki.
    public string NoteFor(BossRewardSO reward) => Pool != null ? Pool.NoteFor(reward) : reward != null ? reward.note : "";
    public IReadOnlyList<StatModifier> OwnedModifiers => owned;
    public int Stacks(BossRewardSO reward) => reward != null && stacks.TryGetValue(reward, out int n) ? n : 0;

    // Aynı dışlama grubundaki diğer ödüller (havuzdan): kart "bunu alırsan şu sunulmaz" bilgisini buradan yazar.
    public void ExclusiveWith(BossRewardSO reward, List<BossRewardSO> into)
    {
        into.Clear();
        BossRewardPoolSO pool = Pool;
        if (pool != null && reward != null) pool.CollectExclusive(reward, into);
    }

    public static float DirectDamageMultiplier => Instance != null ? Instance.direct : 1f;
    // Level başına seçim hakkına run'daki aktif ek (alınan ödüllerden). Bekleyen seçimleri değiştirmez.
    public static int LevelChoiceDelta => Instance != null ? Instance.levelChoices : 0;
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

    public static bool TryGetChain(out HarvestChainConfig config)
    {
        config = Instance != null ? Instance.chain : default;
        return Instance != null && Instance.hasChain;
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
        // Başarısız boss ödül vermez. Eşit segmentli profillerde run'ın son round'unda kullanılamayacak buff sunulmaz;
        // açık takvimde son boss'un ödülü zaferden önce seçilir.
        if (rounds.RunFailed || (round >= rounds.MaxRounds && !rounds.Calendar.FinalRoundHasChoices)) return;
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
        BuildOffer(rounds.Profile.bossRewards, rounds.Calendar.PeriodOf(offerRound));
        offerPrepared = true;
        Version++;
    }

    private void BuildOffer(BossRewardPoolSO pool, int segment)
    {
        offer.Clear();
        if (pool.IsStaged) { BuildStagedOffer(pool, segment); return; }
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

    // Aşamalı teklif (Bölüm 3.7.3). Adaylar: boss round'unun aşaması ve daha önce açılmış aşamalardaki uygun ödüller (tekrarsız).
    // 1) Mevcut aşamada uygun aday varsa biri ödülün kendi ağırlığıyla seçilir ve bir slot ona ayrılır.
    // 2) Kalan slotlar, kalan bütün adaylardan ağırlık × aşama uzaklığı çarpanı ile dolar (mevcut ×1, bir önceki, iki önceki …).
    // 3) Ayrılan slotun ekrandaki yeri rastgeledir. Aday azsa o kadar gösterilir; aynı ödül iki kartta gösterilmez.
    // Sunulma zamanı yalnız aşamadan gelir: ödülün minRound / maxRound alanları burada okunmaz.
    private void BuildStagedOffer(BossRewardPoolSO pool, int segment)
    {
        int current = pool.StageIndexAt(offerRound);
        var candidates = new List<BossRewardSO>();
        var stageOf = new List<int>();
        for (int s = 0; s <= current; s++)
            foreach (BossRewardStageEntry entry in pool.stages[s].rewards)
                if (IsEligible(entry.reward) && !candidates.Contains(entry.reward)) { candidates.Add(entry.reward); stageOf.Add(s); }
        SegmentEventDirector events = SegmentEventDirector.Instance;
        int seed = events != null ? events.RunSeed : 0;
        var rng = new System.Random(SegmentEventDirector.Mix(seed, segment, 0x52574452)); // 'RWDR'
        int slots = Mathf.Max(1, pool.choices);
        var weights = new List<double>();
        BossRewardSO featured = null;
        if (pool.reserveCurrentStageSlot)
        {
            for (int i = 0; i < candidates.Count; i++) weights.Add(stageOf[i] == current ? candidates[i].weight : 0.0);
            int pick = PickWeighted(weights, rng);
            if (pick >= 0)
            {
                featured = candidates[pick];
                candidates.RemoveAt(pick); stageOf.RemoveAt(pick);
                slots--;
            }
        }
        for (int n = 0; n < slots; n++)
        {
            weights.Clear();
            for (int i = 0; i < candidates.Count; i++) weights.Add(candidates[i].weight * pool.DistanceWeight(current - stageOf[i]));
            int pick = PickWeighted(weights, rng);
            if (pick < 0) break;
            offer.Add(candidates[pick]);
            candidates.RemoveAt(pick); stageOf.RemoveAt(pick);
        }
        if (featured != null) offer.Insert(rng.Next(offer.Count + 1), featured);
    }

    // Ağırlıklı tek seçim; ağırlığı 0 olan aday seçilmez. Seçilebilecek aday yoksa −1 (zar atılmaz).
    private static int PickWeighted(List<double> weights, System.Random rng)
    {
        double total = 0; int last = -1;
        for (int i = 0; i < weights.Count; i++)
            if (weights[i] > 0) { total += weights[i]; last = i; }
        if (last < 0) return -1;
        double roll = rng.NextDouble() * total;
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0) continue;
            roll -= weights[i];
            if (roll < 0) return i;
        }
        return last;
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

    // Sunulabilir mi: alınabilir (BlockOf) ve en az bir etkisi şu an bir şey değiştirebilen ödül.
    public bool IsEligible(BossRewardSO reward)
    {
        if (reward == null || reward.weight <= 0f || BlockOf(reward, true) != BossRewardBlock.None) return false;
        // Kırılma ödülü: etkisi stat ya da çarpan değil, ayrı bir mekaniktir; koşulu sağlanıyorsa sunulabilir.
        if (reward.IsBreakthrough) return true;
        if (!Mathf.Approximately(reward.directDamageMultiplier, 1f) || !Mathf.Approximately(reward.rareDirectMultiplier, 1f) ||
            !Mathf.Approximately(reward.behaviorDamageMultiplier, 1f) || reward.levelChoiceDelta != 0) return true;
        foreach (StatModifier modifier in reward.modifiersPerStack)
            if (CanStillChange(modifier)) return true;
        return false;
    }

    // Ödülün şu an neden alınamayacağı (None: alınabilir). Teklif hazırlanırken koşul dahil hepsi aranır. Ödül alınırken stack,
    // çakışma ve seçim hakkı aralığı her ödülde, koşul ise bedeli olan ödülde yeniden aranır: bedel ödenip kazancın boşa
    // düşeceği durum (ör. davranışı kalmamış tarla) reddedilir. Aralığı aşan seçim hakkı kırpılmaz, ödül reddedilir.
    public BossRewardBlock BlockOf(BossRewardSO reward, bool checkCondition)
    {
        if (reward == null) return BossRewardBlock.Missing;
        if (DemoSceneSettings.Blocks(reward)) return BossRewardBlock.Condition;
        if (Stacks(reward) >= reward.maxStacks) return BossRewardBlock.StackLimit;
        if (ExcludedByTaken(reward)) return BossRewardBlock.Exclusive;
        if (reward.levelChoiceDelta != 0 && (rounds == null || !RunPower.ValidLevelChoices(rounds.ChoicesPerLevel + reward.levelChoiceDelta)))
            return BossRewardBlock.LevelChoiceRange;
        if (checkCondition && !ConditionHolds(reward)) return BossRewardBlock.Condition;
        return BossRewardBlock.None;
    }

    // Aynı dışlama grubundan başka bir ödül bu run'da alınmış mı (ödülün kendi önceki alışları sayılmaz: onu stack sınırı keser).
    private bool ExcludedByTaken(BossRewardSO reward)
    {
        if (string.IsNullOrEmpty(reward.exclusiveGroup)) return false;
        foreach (BossRewardSO other in taken)
            if (other != reward && other.exclusiveGroup == reward.exclusiveGroup) return true;
        return false;
    }

    private bool ConditionHolds(BossRewardSO reward)
    {
        if (reward.condition == BossRewardCondition.AnyPlanterHasModifiedStat) return AnyPlanterHasStat(reward);
        if (reward.condition == BossRewardCondition.AnyBehaviorPlanter) return AnyPlanter(HasBehavior, false);
        if (reward.condition == BossRewardCondition.FieldRunsLow) return LastRoundOccupancy < reward.conditionThreshold;
        if (reward.condition == BossRewardCondition.AnyPlanterHasConditionStat) return AnyPlanter(p => p.GetFinalStat(reward.conditionStat) > 0f, false);
        return true;
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

    // Tek işlem: önce bütün koşullar doğrulanır (teklifte mi, stack, çakışan ödül, seçim hakkı aralığı, bedelli ödülde koşul);
    // biri tutmazsa hiçbir şey değişmez. Uygulama başarılıysa teklif kapanır: ikinci istek ve çift tıklama reddedilir.
    public bool Choose(BossRewardSO reward)
    {
        PrepareOffer();
        if (!IsPending || !offerPrepared || reward == null || !offer.Contains(reward)) return false;
        if (BlockOf(reward, reward.HasCost) != BossRewardBlock.None) return false;
        if (!TryApply(reward)) return false;
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

#if UNITY_EDITOR
    // Yalnız Editor (Bölüm 3.7.6.2; ChainDebugMenu'nün karşılaştırma yolu): boss teklifi olmadan bir ödülü bu run'a verir. Ayrı bir
    // uygulama yolu yoktur: alınabilirlik oyunun kendi denetimiyle (BlockOf: stack sınırı, çakışan ödül, seçim hakkı aralığı, koşul)
    // ve uygulama oyunun tek işlemli yoluyla (TryApply: geçersiz veride hiçbir şey değişmez) yapılır. Bekleyen bir boss teklifi
    // varken reddeder. refusal: neden verilmediği. Bu blok build'e girmez.
    public bool EditorGrant(BossRewardSO reward, out string refusal)
    {
        refusal = null;
        if (reward == null) refusal = "ödül yok";
        else if (IsPending) refusal = "bekleyen bir boss teklifi var";
        else
        {
            BossRewardBlock block = BlockOf(reward, true);
            if (block != BossRewardBlock.None) refusal = "alınamaz: " + block;
        }
        if (refusal != null) return false;
        if (!TryApply(reward)) { refusal = "uygulanamadı (ödül verisi geçersiz)"; return false; }
        Version++;
        return true;
    }
#endif

    // Ödülün bütün etkileri (kazanç ve bedel) tek işlem olarak uygulanır. Hata verebilecek tek adım stat modifier'larının
    // StatManager'a eklenmesidir (dinleyicileri çağırır): o adım önce yapılır; yarıda kesilirse eklenenler geri alınır ve
    // çarpanlar, seçim hakkı, stack ve alınan ödül kaydı hiç değişmemiş olur. Kalan adımlar yalnız alan atamasıdır.
    private bool TryApply(BossRewardSO reward)
    {
        // Zincir ayarı önce doğrulanır: tutarsız veride ödül hiç uygulanmaz (değer kırpılmaz).
        HarvestChainConfig chainConfig = default;
        if (reward.chain && !HarvestChainConfig.TryCreate(reward, out chainConfig, out string chainError))
        {
            Debug.LogError($"Boss ödülü uygulanamadı: '{reward.displayName}' — zincir ayarı geçersiz ({chainError}). Hiçbir etkisi bırakılmadı; ödül alınmadı.", reward);
            return false;
        }
        if (stats == null)
        {
            stats = StatManager.Instance;
            if (stats != null) stats.OnGlobalModifiersCleared += Forget;
        }
        int count = reward.modifiersPerStack.Count;
        if (count > 0 && stats == null) return false;
        int attempted = 0;
        try
        {
            for (; attempted < count; attempted++) stats.AddGlobalModifier(reward.modifiersPerStack[attempted]);
        }
        catch (System.Exception error)
        {
            // Yarıda kalan modifier listeye girmiş olabilir (ekleme, bildirimden önce yapılır): o da geri alınır.
            for (int i = Mathf.Min(attempted, count - 1); i >= 0; i--)
            {
                if (!Contains(stats.GlobalModifiers, reward.modifiersPerStack[i])) continue;
                try { stats.RemoveGlobalModifier(reward.modifiersPerStack[i]); }
                catch (System.Exception) { /* dinleyici yine hata verdi; modifier listeden çıktı */ }
            }
            Debug.LogError($"Boss ödülü uygulanamadı: '{reward.displayName}' — {error.Message}. Hiçbir etkisi bırakılmadı; ödül alınmadı.", reward);
            return false;
        }
        for (int i = 0; i < count; i++) owned.Add(reward.modifiersPerStack[i]);
        direct *= reward.directDamageMultiplier;
        behavior *= reward.behaviorDamageMultiplier;
        if (!Mathf.Approximately(reward.rareDirectMultiplier, 1f))
            for (int i = Mathf.Max(0, (int)reward.rareDirectFrom); i < rareDirect.Length; i++) rareDirect[i] *= reward.rareDirectMultiplier;
        levelChoices += reward.levelChoiceDelta;
        if (reward.echo == BossRewardEcho.Explosion)
        { explosionEcho = new BehaviorEcho(reward.echoDelay, reward.echoDamage, reward.echoRadius); hasExplosionEcho = true; }
        if (reward.echo == BossRewardEcho.Electric)
        { electricEcho = new BehaviorEcho(reward.echoDelay, reward.echoDamage, 1f, reward.echoReach); hasElectricEcho = true; }
        if (reward.rhythmHarvests > 0)
        { rhythm = new HarvestRhythmConfig(reward.rhythmHarvests, reward.rhythmDamage, reward.rhythmRadius); hasRhythm = true; }
        if (reward.chain) { chain = chainConfig; hasChain = true; }
        if (!taken.Contains(reward)) taken.Add(reward);
        stacks[reward] = Stacks(reward) + 1;
        return true;
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
        levelChoices = 0;
        for (int i = 0; i < rareDirect.Length; i++) rareDirect[i] = 1f;
        hasExplosionEcho = hasElectricEcho = hasRhythm = hasChain = false;
        explosionEcho = electricEcho = default;
        rhythm = default;
        chain = default;
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

// Bir ödülün alınamama nedeni (BossRewardManager.BlockOf).
public enum BossRewardBlock
{
    None = 0,
    Missing = 1,
    // Stack sınırı dolu.
    StackLimit = 2,
    // Aynı dışlama grubundan başka bir ödül alınmış.
    Exclusive = 3,
    // Level başına seçim hakkı geçerli aralığın (1–5) dışına çıkardı.
    LevelChoiceRange = 4,
    // Ödülün koşulu sağlanmıyor (ör. davranışı olan saksı yok).
    Condition = 5,
}
