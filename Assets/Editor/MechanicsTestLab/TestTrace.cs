using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace MechanicsTestLab
{
    // Hesap bakiyelerinin anlık kopyası (canlı referans değil).
    public readonly struct Economy
    {
        public readonly double XpEarned, XpStored;
        public readonly int Level, PendingChoices, GrantedChoices, Gold, Iron, Stone;
        public readonly long Score;

        private Economy(double earned, double stored, int level, int pending, int granted, int gold, int iron, int stone, long score)
        { XpEarned = earned; XpStored = stored; Level = level; PendingChoices = pending; GrantedChoices = granted; Gold = gold; Iron = iron; Stone = stone; Score = score; }

        public static Economy Read()
        {
            ProgressionManager progression = ProgressionManager.Instance;
            RoundManager rounds = RoundManager.Instance;
            ResourceManager resources = ResourceManager.Instance;
            HarvestScoreManager score = HarvestScoreManager.Instance;
            return new Economy(progression != null ? progression.TotalXPEarned : 0d, progression != null ? progression.StoredXP : 0d,
                progression != null ? progression.CurrentLevel : 0, rounds != null ? rounds.PendingCardSelections : 0, rounds != null ? rounds.CardChoicesGranted : 0,
                resources != null ? resources.GetResourceAmount(ResourceType.Gold) : 0, resources != null ? resources.GetResourceAmount(ResourceType.Iron) : 0,
                resources != null ? resources.GetResourceAmount(ResourceType.Stone) : 0, score != null ? score.TotalScore : 0L);
        }

        public int Resource(ResourceType type) => type == ResourceType.Gold ? Gold : type == ResourceType.Iron ? Iron : Stone;
    }

    // Bir davranışın vuruş başına hasarının hesaplandığı an (oyunun bildirdiği gerçek değerler).
    public sealed class TraceEmission
    {
        public DamageType Type;
        public GridPosition SourceCell;
        public bool HasSource, Precomputed, Echo;
        public int Root, Generation, BaseDamage, Damage, Frame;
        public float TypeCoefficient, ChainFactor;
        public readonly List<string> Multipliers = new();
        public float MultiplierProduct = 1f;
    }

    public sealed class TraceHit
    {
        public int Index, Frame;
        public double Time;
        // direct / davranış / zincir / artçı / ödül darbesi
        public string Kind = "";
        public DamageType Type;
        public RewardSource Reward;
        public int Generation, Root;
        public bool Echo, Linked, Crit, Killed, Foreign;
        public string Source = "", Plant = "";
        public PlantRarity Rarity;
        public GridPosition Target;
        // Hesaplanan darbe hasarı (uygulanan) ≠ gerçek can azalması: 10 canlı bitkiye 100 hasar 10 can düşürür, 90 taşar.
        public int HealthBefore, HealthAfter, MaxHealth, Computed, Decrease, Overkill;
        // Doğrudan vuruşun gerçek ara değerleri (yalnız doğrudan vuruşta) ve davranışın hesap anı (yalnız davranışta).
        public bool HasDirect;
        public float BaseDamage, Variance, RhythmMultiplier, FeedMultiplier, CritChance, CritMultiplier, PlanterMultiplier;
        public int DamageBeforeCrit, DamageAfterCrit;
        public TraceEmission Emission;
        public readonly List<string> Calculation = new();
    }

    public sealed class TraceHarvest
    {
        public int Index, Frame;
        public double Time;
        public GridPosition Cell;
        public string Plant = "", KilledBy = "";
        public PlantRarity Rarity;
        public bool Linked, HasReward, HasScore, Duplicated, Bounty;
        public ResourceType Resource;
        public int BaseReward, RewardRounded, RewardAfterDuplicate, RewardAfterSpecialization, RewardFinal, BaseXp, ScoreAward, ScoreFinal;
        public float ResourceMultiplier, XpMultiplier, RarityXpMultiplier, DuplicateChance, ScorePlayerMultiplier, ScorePlanterMultiplier;
        public double XpRounded, XpFinal;
    }

    public sealed class TraceRecord
    {
        public int Id, AttackSerial, ChainRoot, StartFrame, Cells, LivingTargets;
        public double StartTime, EndTime;
        public Vector3 Center;
        public float Radius;
        public bool Complete, Truncated;
        public string TruncatedReason = "";
        public Economy Before, After;
        public readonly List<TraceHit> Hits = new();
        public readonly List<TraceHarvest> Harvests = new();
        public readonly List<string> Notes = new();
        public readonly List<string> Interference = new();
        public int Events => Hits.Count + Harvests.Count + Notes.Count;
    }

    // "Bir sonraki saldırıyı kaydet": tek bir oyuncu saldırısının ve ondan doğan devam olaylarının sayısal izi.
    // Yalnız gözlemdir: oyunun gözlem olaylarını ve Editor gözlem noktalarını (EditorNumericTrace) dinler; hiçbir hesabı kendisi
    // yapmaz, hiçbir şeyi tetiklemez ve rastgele sayı tüketmez. Kurulu ya da kayıtta değilken hiçbir olaya abone değildir.
    // Kayıt, bekleyen iş (zincir, artçı, ödül zamanlayıcısı, kasırga, bumerang, hilal dalgası) kalmayana ve kısa bir sessizlik
    // geçene kadar açık kalır; olay ya da süre sınırına ulaşırsa "kesildi, sonuç eksik" diye kapanır.
    public static class TestTrace
    {
        public const int MaxEvents = 400, Kept = 20;
        public const float MaxSeconds = 15f, QuietSeconds = 0.25f;

        private static readonly List<TraceRecord> records = new();
        private static readonly List<TraceEmission> emissions = new();
        private static readonly List<PlanterBrain> echoSources = new();
        private static readonly List<StatSource> sources = new();
        private static readonly Listener listener = new();
        private static bool subscribed, hasPendingDirect, foreignAttack;
        private static EditorNumericTrace.DirectHit pendingDirect;
        private static TraceHarvest pendingHarvest;
        private static double lastEventTime;
        private static int nextId = 1, ownAttackFrame = -1;

        public static bool Armed { get; private set; }
        public static TraceRecord Current { get; private set; }
        public static TraceRecord Last => records.Count > 0 ? records[records.Count - 1] : null;
        public static IReadOnlyList<TraceRecord> Records => records;
        public static int Version { get; private set; }
        public static int Faults { get; private set; }

        private sealed class Listener : DirectAttackListener
        {
            public override void Begin(DirectAttackReport attack, ref float multiplier) => Guard(() => OnAttackBegin(attack));
            public override void End(DirectAttackReport attack) => Guard(() => OnAttackEnd(attack));
        }

        public static void Arm()
        {
            if (Current != null) return;
            Armed = true;
            Subscribe(true);
            Version++;
        }

        public static void Cancel()
        {
            Armed = false;
            if (Current != null) Close(true, "kullanıcı durdurdu");
            Subscribe(false);
            Version++;
        }

        // Yeni sahne / oturum sonu: açık kayıt eksik olarak kapanır, abonelik kalkar.
        public static void Reset(bool clearRecords)
        {
            Armed = false;
            if (Current != null) Close(true, "oturum yeniden kuruldu");
            Subscribe(false);
            emissions.Clear(); echoSources.Clear();
            hasPendingDirect = false; pendingHarvest = null;
            if (clearRecords) records.Clear();
            Version++;
        }

        private static void Subscribe(bool wanted)
        {
            if (wanted == subscribed) return;
            subscribed = wanted;
            if (wanted)
            {
                EditorNumericTrace.DirectHitObserved += OnDirectHit;
                EditorNumericTrace.BehaviorDamageObserved += OnBehaviorDamage;
                EditorNumericTrace.HarvestRewardObserved += OnHarvestReward;
                EditorNumericTrace.HarvestScoreObserved += OnHarvestScore;
                PlantHealth.AnyDamaged += OnDamaged;
                PlantHealth.AnyHarvested += OnHarvested;
                HarvestChain.Traced += OnChain;
                BehaviorEchoes.Traced += OnEcho;
                RunEffects.Cued += OnRewardCue;
                AttackFeed.BehaviorTriggered += OnBehaviorTriggered;
                AttackFeed.Add(listener);
                TestTime.FrameEnded += OnFrameEnd;
            }
            else
            {
                EditorNumericTrace.DirectHitObserved -= OnDirectHit;
                EditorNumericTrace.BehaviorDamageObserved -= OnBehaviorDamage;
                EditorNumericTrace.HarvestRewardObserved -= OnHarvestReward;
                EditorNumericTrace.HarvestScoreObserved -= OnHarvestScore;
                PlantHealth.AnyDamaged -= OnDamaged;
                PlantHealth.AnyHarvested -= OnHarvested;
                HarvestChain.Traced -= OnChain;
                BehaviorEchoes.Traced -= OnEcho;
                RunEffects.Cued -= OnRewardCue;
                AttackFeed.BehaviorTriggered -= OnBehaviorTriggered;
                AttackFeed.Remove(listener);
                TestTime.FrameEnded -= OnFrameEnd;
            }
        }

        private static void Guard(Action body)
        {
            try { body(); }
            catch (Exception) { Faults++; }
        }

        private static GridPosition CellOf(Vector3 world) => FieldCells.Grid != null ? FieldCells.Grid.GetGridPosition(world) : default;
        private static string CellText(GridPosition cell) => $"({cell.x}, {cell.z})";
        private static void Touch() { lastEventTime = Time.timeAsDouble; Version++; }

        // ---------------------------------------------------------------- saldırı
        private static void OnAttackBegin(DirectAttackReport attack)
        {
            if (Current != null)
            {
                foreignAttack = true;
                Current.Interference.Add($"Kayıt sürerken başka bir oyuncu saldırısı başladı (S{attack.Serial}): onun vuruşları \"başka saldırı\" diye işaretlendi; " +
                                         "XP / kaynak / skor farkı yalnız bu saldırıya ait değildir.");
                return;
            }
            if (!Armed) return;
            Armed = false;
            emissions.Clear(); echoSources.Clear();
            hasPendingDirect = false; pendingHarvest = null; foreignAttack = false;
            ownAttackFrame = Time.frameCount;
            Current = new TraceRecord
            {
                Id = nextId++, AttackSerial = attack.Serial, StartFrame = Time.frameCount, StartTime = Time.timeAsDouble, Center = attack.Center,
                Radius = attack.Radius, Cells = attack.Cells.Count, LivingTargets = attack.LivingTargets, Before = Economy.Read(),
            };
            if (TestAttack.AutoAttack) Current.Interference.Add("Otomatik saldırı açık: kayıt sürerken başka saldırılar araya girebilir (temiz ölçüm için kapat).");
            if (!TestAttack.ProductionPaused) Current.Interference.Add("Üretim açık: kayıt sürerken yeni bitki doğabilir (hasar ve ödül kayıtlarını değiştirmez; hedef sayısını değiştirebilir).");
            Touch();
        }

        private static void OnAttackEnd(DirectAttackReport attack)
        {
            if (Current == null) return;
            if (foreignAttack) { foreignAttack = false; return; }
            if (attack.Serial != Current.AttackSerial) return;
            Current.Notes.Add($"{Stamp()} saldırı bitti: {attack.Hits.Count} vuruş · {attack.Crits} kritik · {attack.DirectHarvests} doğrudan hasat · ödül saldırı çarpanı ×{attack.Multiplier:0.###}");
            Touch();
        }

        private static string Stamp() => Current != null ? $"+{Time.timeAsDouble - Current.StartTime:0.000}s" : "";

        private static void OnDirectHit(EditorNumericTrace.DirectHit hit)
        {
            if (Current == null) return;
            pendingDirect = hit;
            hasPendingDirect = true;
        }

        private static void OnBehaviorDamage(EditorNumericTrace.BehaviorDamage damage)
        {
            if (Current == null) return;
            var emission = new TraceEmission
            {
                Type = damage.Type, HasSource = damage.Source != null, Precomputed = damage.Precomputed, Echo = damage.Link.Echo, Root = damage.Link.Root,
                Generation = damage.Link.Generation, BaseDamage = damage.BaseDamage, Damage = damage.Damage, Frame = Time.frameCount,
                TypeCoefficient = damage.TypeCoefficient, ChainFactor = damage.ChainFactor,
            };
            if (damage.Source != null)
            {
                emission.SourceCell = damage.Source.EditorOrigin;
                // Çarpanlar hesabın yapıldığı anda oyunun kendi alıcılarından okunur (aynı karede, aynı çağrıda).
                StatSources.CollectBehavior(damage.Source, damage.Type, sources);
                foreach (StatSource source in sources) { emission.Multipliers.Add(source.Describe()); emission.MultiplierProduct *= source.Value; }
            }
            emissions.Add(emission);
            if (emissions.Count > 64) emissions.RemoveAt(0);
        }

        // Vuruşun davranış hesabı: aynı tür, aynı bağlam ve aynı hasarla en son bildirilen hesap.
        private static TraceEmission Match(DamageType type, HarvestLink link, int damage, out int candidates)
        {
            TraceEmission found = null;
            candidates = 0;
            for (int i = emissions.Count - 1; i >= 0; i--)
            {
                TraceEmission e = emissions[i];
                if (e.Type != type || e.Root != link.Root || e.Generation != link.Generation || e.Echo != link.Echo || e.Damage != damage) continue;
                candidates++;
                found ??= e;
            }
            return found;
        }

        private static void OnDamaged(PlantHealth plant, int damage, DamageType type)
        {
            TraceRecord record = Current;
            if (record == null) return;
            HarvestLink link = plant.LastHitLink;
            int before = plant.CurrentHealth;
            var hit = new TraceHit
            {
                Index = record.Hits.Count + 1, Frame = Time.frameCount, Time = Time.timeAsDouble - record.StartTime, Type = type, Reward = link.Source,
                Generation = link.Generation, Root = link.Root, Echo = link.Echo, Target = CellOf(plant.transform.position),
                Plant = plant.Data != null ? plant.Data.name : "bitki", Rarity = plant.Data != null ? plant.Data.rarity : PlantRarity.Common,
                HealthBefore = before, HealthAfter = Mathf.Max(0, before - damage), MaxHealth = plant.MaxHealth, Computed = damage,
                Decrease = Mathf.Min(before, damage), Overkill = Mathf.Max(0, damage - before), Killed = before - damage <= 0, Foreign = foreignAttack,
            };
            bool sameFrame = Time.frameCount == ownAttackFrame;
            if (type == DamageType.Direct)
            {
                hit.Kind = foreignAttack ? "direct (başka saldırı)" : "direct";
                hit.Source = "oyuncu";
                hit.Linked = !foreignAttack;
                if (!foreignAttack && link.Root != 0) record.ChainRoot = link.Root;
                if (hasPendingDirect && pendingDirect.Target == plant) FillDirect(hit, plant);
                hasPendingDirect = false;
            }
            else if (type == DamageType.Reward)
            {
                hit.Kind = "ödül darbesi";
                hit.Source = "ödül etkisi · " + link.Source;
                // Saldırının sonunda, aynı karede çalışan etki bu saldırıya bağlıdır; gecikmeli darbe için bağ kanıtı yoktur.
                hit.Linked = sameFrame && !foreignAttack;
                hit.Calculation.Add("Ödül etkisinin kendi hesabı: " + RewardSummary(link.Source));
                hit.Calculation.Add($"Uygulanan darbe hasarı {damage}. Bu etkinin çarpan dökümü izlenmiyor (oyun yalnız sonucu bildiriyor).");
                hit.Calculation.Add("Doğrudan vuruş değildir: kritik atmaz, doğrudan çarpanları ve saksı hasar çarpanını almaz, davranış tetiklemez.");
            }
            else if (link.Echo && BehaviorEchoes.IsExecuting)
            {
                hit.Kind = "artçı";
                PlanterBrain source = BehaviorEchoes.ExecutingSource;
                hit.Source = source != null ? "saksı " + CellText(source.EditorOrigin) : "saksı (satılmış)";
                hit.Linked = source != null && echoSources.Contains(source);
                hit.Emission = Match(type, link, damage, out _);
                FillEcho(hit, type, source);
            }
            else
            {
                hit.Kind = link.Echo ? "ödül kaynaklı davranış (zincir dışı)" : link.IsChain ? "zincir" : "davranış";
                hit.Emission = Match(type, link, damage, out int candidates);
                hit.Source = hit.Emission != null && hit.Emission.HasSource ? "saksı " + CellText(hit.Emission.SourceCell) : "bilinmiyor";
                if (candidates > 1) hit.Source += $" (aynı hasarlı {candidates} aktif davranıştan en sonuncusu; kaynak kesin değil)";
                hit.Linked = (link.Root != 0 && link.Root == record.ChainRoot) || (AttackFeed.InAttack && !foreignAttack);
                FillBehavior(hit, type);
            }
            if (plant.Owner != null && type != DamageType.Direct) hit.PlanterMultiplier = 1f;
            Append(record, hit);
        }

        private static void Append(TraceRecord record, TraceHit hit)
        {
            if (record.Events >= MaxEvents) { Close(true, $"olay sınırı ({MaxEvents})"); return; }
            record.Hits.Add(hit);
            Touch();
        }

        private static void FillDirect(TraceHit hit, PlantHealth plant)
        {
            EditorNumericTrace.DirectHit d = pendingDirect;
            hit.HasDirect = true;
            hit.BaseDamage = d.BaseDamage; hit.Variance = d.Variance; hit.RhythmMultiplier = d.RhythmMultiplier; hit.FeedMultiplier = d.FeedMultiplier;
            hit.CritChance = d.CritChance; hit.CritMultiplier = d.CritMultiplier; hit.Crit = d.Crit;
            hit.DamageBeforeCrit = d.Damage; hit.DamageAfterCrit = d.FinalDamage;
            hit.PlanterMultiplier = plant.Owner != null ? plant.Owner.GetFinalStat(StatType.PlanterDamageMultiplier) : 1f;
            hit.Calculation.Add($"Taban (hasat hasarı stat'ı) {d.BaseDamage:0.####} × bu vuruşun sapması {d.Variance:0.####}");
            StatSources.CollectDirect(hit.Rarity, sources);
            foreach (StatSource source in sources) hit.Calculation.Add("  " + source.Describe());
            if (!Mathf.Approximately(d.RhythmMultiplier, 1f)) hit.Calculation.Add($"  [Hasat Ritmi] bu saldırı güçlendi: ×{d.RhythmMultiplier:0.####}");
            if (!Mathf.Approximately(d.FeedMultiplier, 1f)) hit.Calculation.Add($"  [ödül saldırı çarpanı] Açgözlü Hasat: ×{d.FeedMultiplier:0.####}");
            hit.Calculation.Add($"→ tek yuvarlama (en yakın tam sayı; ,5 çifte; en az 1): {d.Damage}");
            hit.Calculation.Add(d.Crit
                ? $"Kritik (şans {d.CritChance * 100f:0.##}%): {d.Damage} × kritik çarpanı {d.CritMultiplier:0.####} → ikinci yuvarlama: {d.FinalDamage}"
                : $"Kritik değil (şans {d.CritChance * 100f:0.##}%): {d.FinalDamage}");
            hit.Calculation.Add($"Saksı hasar çarpanı (yalnız doğrudan vuruş) ×{hit.PlanterMultiplier:0.####} → uygulanan darbe hasarı {d.Applied}");
        }

        private static void FillBehavior(TraceHit hit, DamageType type)
        {
            TraceEmission e = hit.Emission;
            if (e == null)
            {
                hit.Calculation.Add("Bu vuruşun hesap anı kayıtta yok (davranış kayıt başlamadan önce başlamış olabilir).");
                return;
            }
            hit.Calculation.Add(e.Precomputed
                ? $"Hasar çağırana hesaplanmış geldi ({e.BaseDamage}): davranış katsayıları burada yeniden uygulanmadı (zincir nesli ya da ikinci dalga)."
                : $"Taban (hasat hasarı stat'ı, tam sayı) {e.BaseDamage}");
            if (!e.Precomputed)
            {
                if (!Mathf.Approximately(e.TypeCoefficient, 1f)) hit.Calculation.Add($"  × davranışın kendi katsayısı {e.TypeCoefficient:0.####} → yuvarlama (en az 1)");
                foreach (string multiplier in e.Multipliers) hit.Calculation.Add("  " + multiplier);
                if (e.Multipliers.Count == 0) hit.Calculation.Add("  davranış çarpanı yok (rezonans, uzmanlaşma ve boss ödülü ×1)");
                if (!Mathf.Approximately(e.ChainFactor, 1f)) hit.Calculation.Add($"  × zincir nesli çarpanı {e.ChainFactor:0.####} (nesil {e.Generation}; önceki nesille birikmez)");
            }
            hit.Calculation.Add($"→ vuruş başına hesaplanmış hasar {e.Damage}");
            hit.Calculation.Add("Doğrudan vuruş çarpanları, kritik ve saksı hasar çarpanı davranışa uygulanmaz.");
        }

        private static void FillEcho(TraceHit hit, DamageType type, PlanterBrain source)
        {
            if (!RunPower.TryGetEcho(type, out BehaviorEcho echo)) return;
            hit.Calculation.Add($"İkinci darbe: ilk darbenin HESAPLANMIŞ hasarı × oran {echo.Damage:0.####} (ödül verisi); katsayılar yeniden uygulanmaz, yeni şans zarı yok.");
            hit.Calculation.Add($"→ uygulanan darbe hasarı {hit.Computed} · gecikme {echo.Delay:0.###} sn (ödül verisi)");
            hit.Calculation.Add("Öldürdüğü bitki zincir başlatmaz.");
        }

        private static string RewardSummary(RewardSource source)
        {
            RunEffects effects = RunEffects.Instance;
            if (effects == null) return source.ToString();
            RunEffect effect = source switch
            {
                RewardSource.Antenna => effects.Find<AntennaEffect>(),
                RewardSource.Line => effects.Find<ConductiveLineEffect>(),
                RewardSource.LineExtra => effects.Find<ClosedCircuitEffect>(),
                RewardSource.Crescent or RewardSource.CrescentReturn => effects.Find<CrescentEffect>(),
                RewardSource.CritArc => effects.Find<CritArcEffect>(),
                RewardSource.HarvestEcho => effects.Find<HarvestEchoEffect>(),
                RewardSource.Overflow => effects.Find<OverflowEffect>(),
                RewardSource.CritBurst => effects.Find<CritBurstEffect>(),
                RewardSource.Overload => effects.Find<OverloadEffect>(),
                _ => null,
            };
            return effect != null && effect.Data != null ? effect.Data.Summary : source.ToString();
        }

        // ---------------------------------------------------------------- hasat ve ödül
        private static void OnHarvested(PlantHealth plant)
        {
            TraceRecord record = Current;
            if (record == null) return;
            TraceHit last = record.Hits.Count > 0 ? record.Hits[record.Hits.Count - 1] : null;
            var harvest = new TraceHarvest
            {
                Index = record.Harvests.Count + 1, Frame = Time.frameCount, Time = Time.timeAsDouble - record.StartTime, Cell = CellOf(plant.transform.position),
                Plant = plant.Data != null ? plant.Data.name : "bitki", Rarity = plant.Data != null ? plant.Data.rarity : PlantRarity.Common,
                KilledBy = last != null ? last.Kind : plant.KilledBy.ToString(), Linked = last != null && last.Linked,
                Resource = plant.Data != null ? plant.Data.resourceType : ResourceType.Gold, BaseReward = plant.Data != null ? plant.Data.rewardAmount : 0,
                BaseXp = plant.Data != null ? plant.Data.xpAmount : 0,
            };
            pendingHarvest = harvest;
            if (record.Events >= MaxEvents) { Close(true, $"olay sınırı ({MaxEvents})"); return; }
            record.Harvests.Add(harvest);
            Touch();
        }

        private static void OnHarvestReward(EditorNumericTrace.HarvestReward reward)
        {
            TraceHarvest harvest = pendingHarvest;
            if (Current == null || harvest == null || harvest.HasReward) return;
            harvest.HasReward = true;
            harvest.ResourceMultiplier = reward.ResourceMultiplier; harvest.RewardRounded = reward.Rounded; harvest.DuplicateChance = reward.DuplicateChance;
            harvest.Duplicated = reward.Duplicated; harvest.RewardAfterDuplicate = reward.AfterDuplicate; harvest.RewardAfterSpecialization = reward.AfterSpecialization;
            harvest.Bounty = reward.Bounty; harvest.RewardFinal = reward.Final; harvest.XpMultiplier = reward.XpMultiplier;
            harvest.RarityXpMultiplier = reward.RarityXpMultiplier; harvest.XpRounded = reward.XpRounded; harvest.XpFinal = reward.XpFinal;
        }

        private static void OnHarvestScore(EditorNumericTrace.HarvestScore score)
        {
            TraceHarvest harvest = pendingHarvest;
            if (Current == null || harvest == null || harvest.HasScore) return;
            harvest.HasScore = true;
            harvest.ScorePlayerMultiplier = score.PlayerMultiplier; harvest.ScorePlanterMultiplier = score.PlanterMultiplier;
            harvest.ScoreAward = score.Award; harvest.ScoreFinal = score.Final;
        }

        // ---------------------------------------------------------------- devam olayları
        private static void Note(string text)
        {
            if (Current == null) return;
            if (Current.Events >= MaxEvents) { Close(true, $"olay sınırı ({MaxEvents})"); return; }
            Current.Notes.Add(Stamp() + " " + text);
            Touch();
        }

        private static void OnBehaviorTriggered(PlanterBrain planter, DamageType type, GridObject cell, Vector3 position) => Guard(() =>
        {
            if (Current == null) return;
            // Bu saldırının normal tetiği: o saksının ikinci darbesi (varsa) bu saldırıya bağlıdır.
            if (AttackFeed.InAttack && !foreignAttack && planter != null && !echoSources.Contains(planter)) echoSources.Add(planter);
            Note($"davranış başladı (normal tetik): {type} · kaynak saksı {CellText(planter != null ? planter.EditorOrigin : default)}");
        });

        private static void OnChain(HarvestChain.Trace trace)
        {
            if (Current == null || (Current.ChainRoot != 0 && trace.Root != Current.ChainRoot)) return;
            string planter = trace.Planter != null ? CellText(trace.Planter.EditorOrigin) : "?";
            Note(trace.Kind switch
            {
                HarvestChain.TraceKind.Attempt => $"zincir zarı: {trace.Type} · saksı {planter} · nesil {trace.Generation} · şans {trace.Chance:0.###} → {(trace.Success ? "tuttu" : "tutmadı")}",
                HarvestChain.TraceKind.Fired => $"zincir tetiği çalıştı: {trace.Type} · saksı {planter} · nesil {trace.Generation}",
                HarvestChain.TraceKind.Waiting => $"zincir işi kapasite bekliyor: {trace.Type} · saksı {planter}",
                _ => $"zincir reddi: {trace.Type} · saksı {planter} · nesil {trace.Generation} · neden {trace.Reason}",
            });
        }

        private static void OnEcho(string phase, DamageType type, int id, float delay, int frames) =>
            Note(phase == "plan" ? $"ikinci darbe planlandı: {type} · iş #{id} · gecikme {delay:0.###} sn"
                : phase == "uygula" ? $"ikinci darbe uygulandı: {type} · iş #{id} · {frames} kare sonra" : $"ikinci darbe düştü: {type} · iş #{id}");

        private static void OnRewardCue(RewardCue cue) => Note($"ödül etkisi bildirimi: {cue.Kind} · değer {cue.Value}");

        // ---------------------------------------------------------------- tamamlanma
        // Bekleyen iş varken kayıt "tamamlandı" sayılmaz.
        public static string PendingWork()
        {
            var text = new StringBuilder();
            void Add(bool pending, string what) { if (pending) text.Append(text.Length > 0 ? ", " : "").Append(what); }
            HarvestChain chain = HarvestChain.Instance;
            BehaviorEchoes echoes = BehaviorEchoes.Instance;
            RunEffects effects = RunEffects.Instance;
            TornadoManager tornado = TornadoManager.Instance;
            HarvestBehaviorManager behaviors = HarvestBehaviorManager.Instance;
            Add(chain != null && chain.Pending > 0, "zincir işi");
            Add(echoes != null && echoes.Pending > 0, "ikinci darbe");
            Add(effects != null && effects.PendingTimers > 0, "ödül etkisi zamanlayıcısı");
            Add(tornado != null && tornado.ActiveCount > 0, "kasırga");
            Add(behaviors != null && behaviors.ActiveBoomerangs > 0, "bumerang");
            CrescentEffect crescent = effects != null ? effects.Find<CrescentEffect>() : null;
            Add(crescent != null && crescent.ActiveWaves > 0, "hilal dalgası");
            return text.ToString();
        }

        private static void OnFrameEnd()
        {
            TraceRecord record = Current;
            if (record == null) return;
            double now = Time.timeAsDouble;
            RoundManager rounds = RoundManager.Instance;
            if (!TestSession.Ready || rounds == null || !rounds.IsRoundActive) { Close(true, "round ya da oturum bitti"); return; }
            if (now - record.StartTime >= MaxSeconds) { Close(true, $"süre sınırı ({MaxSeconds:0} sn oyun zamanı); hâlâ bekleyen: {PendingWork()}"); return; }
            if (PendingWork().Length > 0) return;
            if (now - lastEventTime < QuietSeconds) return;
            Close(false, "");
        }

        private static void Close(bool truncated, string reason)
        {
            TraceRecord record = Current;
            if (record == null) return;
            Current = null;
            record.Complete = !truncated;
            record.Truncated = truncated;
            record.TruncatedReason = reason;
            record.After = Economy.Read();
            record.EndTime = Time.timeAsDouble;
            records.Add(record);
            while (records.Count > Kept) records.RemoveAt(0);
            pendingHarvest = null; hasPendingDirect = false; foreignAttack = false;
            if (!Armed) Subscribe(false);
            TestLog.Note(TestLog.Kind.Tool, $"Saldırı izi #{record.Id} kapandı: {record.Hits.Count} vuruş, {record.Harvests.Count} hasat" + (truncated ? " · KESİLDİ (" + reason + ")" : ""));
            Version++;
        }
    }
}
