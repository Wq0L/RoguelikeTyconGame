using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // Sınırlı uzunlukta olay kaydı. Yalnız GÖZLEMDİR: oyunun gözlem olaylarını (PlantHealth.AnyDamaged / AnyHarvested,
    // HarvestChain.Traced, BehaviorEchoes.Traced, RunEffects.Cued, FieldEventDirector.Cued) ve saldırı bildirimini dinler; hiçbir
    // şeyi tetiklemez, değiştirmez ya da geciktirmez.
    // - Kapalıyken hiçbir olaya abone değildir (kayıt maliyeti yok). Console'a satır yazmaz.
    // - Halka tampon: en çok Capacity kayıt; dolunca en eskisi düşer.
    // - Dinleyicinin hatası oynanışı kesmez: gözlem olaylarında oyun zaten yalıtır (ObserverEvents); saldırı ve davranış
    //   bildirimlerinde burada yakalanır ve sayılır (Faults).
    // - Saldırı kimliği: oyunun saldırı sıra numarası (AttackFeed.Serial). Zincir kökü taşıyan vuruşlar kendi saldırısına kesin
    //   bağlanır; kök taşımayan gecikmeli vuruşlar (kasırga, artçı, anten …) "son saldırıdan sonra" diye işaretlenir (Linked false).
    public static class TestLog
    {
        [Flags]
        public enum Kind
        {
            None = 0,
            Attack = 1 << 0,     // doğrudan saldırı (başlangıç ve özet)
            Crit = 1 << 1,       // kritik vuruş
            Damage = 1 << 2,     // uygulanan hasar
            Harvest = 1 << 3,    // gerçek hasat
            Behavior = 1 << 4,   // başlayan normal davranış
            Chain = 1 << 5,      // zincir denemesi / tetiği
            Extra = 1 << 6,      // artçı, yankı, ödül kaynaklı ek darbe
            Rejected = 1 << 7,   // kapasite ya da koşul nedeniyle reddedilen / düşen tetik
            Reward = 1 << 8,     // ödül edinme
            Stat = 1 << 9,       // stat değişikliği
            Event = 1 << 10,     // boss ve tarla olayı
            Tool = 1 << 11,      // test aracının kendi komutları
            Buff = 1 << 12,      // geçici / koşullu etkinin yaşam döngüsü
            All = (1 << 13) - 1,
        }

        // Kapalı: hiçbir olaya abone olunmaz. İşlem özeti: saldırı, hasat, davranış, ödül, buff, olay ve komutlar. Ayrıntılı
        // sayısal iz: buna ek olarak her vuruş, kritik, zincir zarı, stat modifier değişikliği ve doğrudan vuruşun hesap satırı.
        public enum LogLevel { Off, Summary, Detailed }
        private const Kind DetailOnly = Kind.Damage | Kind.Crit | Kind.Chain | Kind.Stat;

        public readonly struct Entry
        {
            public readonly int Index, Frame, Attack;
            public readonly bool Linked;
            public readonly double Time;
            public readonly Kind Kind;
            public readonly string Text;
            public Entry(int index, int frame, double time, int attack, bool linked, Kind kind, string text)
            { Index = index; Frame = frame; Time = time; Attack = attack; Linked = linked; Kind = kind; Text = text; }
        }

        public const int Capacity = 2000;
        private static readonly Entry[] ring = new Entry[Capacity];
        private static int start, count, total;
        private static readonly Dictionary<int, int> attackOfRoot = new();
        private static readonly Dictionary<BossRewardSO, int> knownStacks = new();
        private static readonly Listener listener = new();
        private static StatManager watchedStats;
        private static int currentAttack, attackFrame;
        private static int tornadoRejected, boomerangRejected, droppedTimers, bossVersion = -1;
        private static readonly int[] skipped = new int[8];

        public static LogLevel Level { get; private set; } = LogLevel.Off;
        public static bool Enabled => Level != LogLevel.Off;
        public static int Version { get; private set; }
        public static int Count => count;
        public static int Total => total;
        public static int Dropped => total - count;
        public static int Faults { get; private set; }

        private sealed class Listener : DirectAttackListener
        {
            public override void Begin(DirectAttackReport attack, ref float multiplier) => Guard(() => OnAttackBegin(attack));
            public override void End(DirectAttackReport attack) => Guard(() => OnAttackEnd(attack));
        }

        public static void SetEnabled(bool enabled) => SetLevel(enabled ? (Level == LogLevel.Off ? LogLevel.Detailed : Level) : LogLevel.Off);

        // Kullanıcının panelden seçtiği seviye: oturumlar arasında hatırlanır (yalnız bu makinenin Editor tercihi; oyuna ve projeye
        // yazılmaz). Seçilmediyse "Ayrıntılı sayısal iz". Batch koşularında tercih okunmaz ve yazılmaz.
        private const string LevelKey = "MechanicsTestLab.LogLevel";
        public static LogLevel Preferred
        {
            get => Application.isBatchMode ? LogLevel.Detailed : (LogLevel)Mathf.Clamp(EditorPrefs.GetInt(LevelKey, (int)LogLevel.Detailed), 0, 2);
            set { if (!Application.isBatchMode) EditorPrefs.SetInt(LevelKey, (int)value); }
        }

        public static void SetLevel(LogLevel level)
        {
            bool enabled = level != LogLevel.Off, was = Enabled;
            Level = level;
            Version++;
            // Doğrudan vuruşun hesap satırı yalnız ayrıntılı izde dinlenir.
            if (level == LogLevel.Detailed && !detailSubscribed) { EditorNumericTrace.DirectHitObserved += OnDirectDetail; detailSubscribed = true; }
            else if (level != LogLevel.Detailed && detailSubscribed) { EditorNumericTrace.DirectHitObserved -= OnDirectDetail; detailSubscribed = false; }
            BuffWatch.SetAttached(enabled);
            if (enabled == was) return;
            if (enabled)
            {
                PlantHealth.AnyDamaged += OnDamaged;
                PlantHealth.AnyHarvested += OnHarvested;
                HarvestChain.Traced += OnChain;
                BehaviorEchoes.Traced += OnEcho;
                RunEffects.Cued += OnRewardCue;
                FieldEventDirector.Cued += OnEventCue;
                AttackFeed.BehaviorTriggered += OnBehavior;
                AttackFeed.Add(listener);
                TestTime.FrameEnded += Poll;
                SyncCounters();
                WatchStats();
            }
            else
            {
                PlantHealth.AnyDamaged -= OnDamaged;
                PlantHealth.AnyHarvested -= OnHarvested;
                HarvestChain.Traced -= OnChain;
                BehaviorEchoes.Traced -= OnEcho;
                RunEffects.Cued -= OnRewardCue;
                FieldEventDirector.Cued -= OnEventCue;
                AttackFeed.BehaviorTriggered -= OnBehavior;
                AttackFeed.Remove(listener);
                TestTime.FrameEnded -= Poll;
                UnwatchStats();
            }
            Version++;
        }

        // Yeni sahne: sayaçların karşılaştırma tabanı ve StatManager örneği yenilenir. Abonelikler tektir, çoğalmaz.
        public static void OnSceneRebuilt()
        {
            attackOfRoot.Clear();
            knownStacks.Clear();
            currentAttack = 0;
            if (!Enabled) return;
            UnwatchStats();
            WatchStats();
            SyncCounters();
            BuffWatch.Sync();
        }

        public static void Clear()
        {
            start = count = total = 0;
            attackOfRoot.Clear();
            Version++;
        }

        // Test aracının kendi kaydı (komut, ödül kurulumu …). Kayıt kapalıyken yazılmaz.
        public static void Note(Kind kind, string text) { if (Enabled) Add(kind, text, currentAttack, false); }

        // Geçici / koşullu etkinin yaşam döngüsü (BuffWatch).
        public static void Buff(string text) { if (Enabled) Add(Kind.Buff, text, currentAttack, false); }

        private static bool detailSubscribed;

        // Ayrıntılı iz: doğrudan vuruşun gerçek ara değerleri (oyunun bildirdiği), vuruş uygulanmadan hemen önce.
        private static void OnDirectDetail(EditorNumericTrace.DirectHit hit) =>
            Add(Kind.Damage, $"  hesap: taban {hit.BaseDamage:0.###} × sapma {hit.Variance:0.####}" +
                             (Mathf.Approximately(hit.RhythmMultiplier, 1f) ? "" : $" × Hasat Ritmi {hit.RhythmMultiplier:0.###}") +
                             (Mathf.Approximately(hit.FeedMultiplier, 1f) ? "" : $" × ödül saldırı çarpanı {hit.FeedMultiplier:0.###}") +
                             $" (+ doğrudan çarpanlar) → {hit.Damage}" + (hit.Crit ? $" × kritik {hit.CritMultiplier:0.###} → {hit.FinalDamage}" : "") +
                             $" → saksı bonusundan sonra {hit.Applied}", currentAttack, AttackFeed.InAttack);

        private static void Add(Kind kind, string text, int attack, bool linked)
        {
            // İşlem özeti seviyesinde yalnız ayrıntı türünden oluşan satırlar yazılmaz (metin de üretilmemiş olur: çağıranlar aşağıda süzülür).
            if (Level == LogLevel.Summary && (kind & ~DetailOnly) == 0) return;
            int index = total++;
            var entry = new Entry(index, Time.frameCount, Time.timeAsDouble, attack, linked, kind, text);
            if (count < Capacity) ring[(start + count++) % Capacity] = entry;
            else { ring[start] = entry; start = (start + 1) % Capacity; }
            Version++;
        }

        // mask: gösterilecek türler; search: metin süzgeci (boş: hepsi); attack: yalnız bu saldırı kimliği (0: hepsi).
        public static void Collect(Kind mask, string search, int attack, List<Entry> into)
        {
            into.Clear();
            bool hasSearch = !string.IsNullOrWhiteSpace(search);
            for (int i = 0; i < count; i++)
            {
                Entry entry = ring[(start + i) % Capacity];
                if ((entry.Kind & mask) == 0) continue;
                if (attack != 0 && entry.Attack != attack) continue;
                if (hasSearch && entry.Text.IndexOf(search, StringComparison.CurrentCultureIgnoreCase) < 0) continue;
                into.Add(entry);
            }
        }

        public static string Format(Entry entry) =>
            $"{entry.Time,8:0.00}s k{entry.Frame,-6} {(entry.Attack == 0 ? "   —  " : (entry.Linked ? " S" : "~S") + entry.Attack.ToString().PadRight(4))} {entry.Text}";

        public static Result Export(string path = null)
        {
            try
            {
                path ??= Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "MechanicsTestLab")),
                    $"olay-kaydi-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var text = new StringBuilder();
                text.AppendLine($"Mechanics Test Lab olay kaydı · {DateTime.Now:yyyy-MM-dd HH:mm:ss} · senaryo: {(TestSession.Current != null ? TestSession.Current.name : "?")}");
                text.AppendLine($"{count} kayıt (toplam {total}; düşen {Dropped}; dinleyici hatası {Faults})");
                text.AppendLine("Sütunlar: oyun zamanı · kare · saldırı kimliği (S = kesin bağ, ~S = o saldırıdan sonra, bağ kanıtı yok) · olay");
                for (int i = 0; i < count; i++) text.AppendLine(Format(ring[(start + i) % Capacity]));
                File.WriteAllText(path, text.ToString());
                return Result.Success("Olay kaydı dışarı aktarıldı: " + path);
            }
            catch (Exception e) { return Result.Fail("Olay kaydı aktarılamadı: " + e.Message); }
        }

        // ---------------------------------------------------------------- dinleyiciler
        private static void Guard(Action body)
        {
            try { body(); }
            catch (Exception) { Faults++; }
        }

        private static string CellText(Vector3 world)
        {
            GridSystem grid = FieldCells.Grid;
            if (grid == null) return "(?)";
            GridPosition cell = grid.GetGridPosition(world);
            return $"({cell.x}, {cell.z})";
        }

        private static string TypeName(DamageType type, HarvestLink link) => type switch
        {
            DamageType.Direct => "Doğrudan vuruş",
            DamageType.Explosion => link.Echo ? "Artçı Patlama" : "Patlama",
            DamageType.Electric => link.Echo ? "Çifte Akım (ikinci dalga)" : "Elektrik",
            DamageType.Tornado => "Kasırga",
            DamageType.Boomerang => "Bumerang",
            DamageType.Reward => "Ödül darbesi · " + link.Source,
            _ => type.ToString(),
        };

        private static void OnAttackBegin(DirectAttackReport attack)
        {
            currentAttack = attack.Serial;
            attackFrame = Time.frameCount;
            Add(Kind.Attack, $"SALDIRI S{attack.Serial} başladı · merkez ({attack.Center.x:0.##}, {attack.Center.z:0.##}) · temas yarıçapı {attack.Radius:0.##} · " +
                             $"{attack.Cells.Count} hücre · canlı hedef {attack.LivingTargets}", attack.Serial, true);
        }

        private static void OnAttackEnd(DirectAttackReport attack)
        {
            var crits = new StringBuilder();
            foreach (DirectHit hit in attack.Hits)
            {
                if (hit.Plant != null && hit.Plant.LastHitLink.Root != 0) attackOfRoot[hit.Plant.LastHitLink.Root] = attack.Serial;
                if (!hit.Crit) continue;
                GridPosition cell = hit.Cell != null ? hit.Cell.GetGridPosition() : default;
                crits.Append(crits.Length > 0 ? " " : "").Append($"({cell.x}, {cell.z})={hit.Damage}");
            }
            if (crits.Length > 0) Add(Kind.Crit, $"Kritik vuruşlar (saksı bonusundan önceki hasar): {crits}", attack.Serial, true);
            Add(Kind.Attack, $"SALDIRI S{attack.Serial} bitti · {attack.Hits.Count} vuruş · {attack.Crits} kritik · {attack.DirectHarvests} doğrudan hasat · " +
                             $"ödül saldırı çarpanı ×{attack.Multiplier:0.###}" + (attack.TouchedLivingPlant ? "" : " · boş savuruş"), attack.Serial, true);
        }

        // Vuruşun saldırısı: zincir kökü varsa kesin; saldırının vuruşları uygulanırken (InAttack) kesin; diğerleri "sonra".
        private static int AttackOf(HarvestLink link, out bool linked)
        {
            if (link.Root != 0 && attackOfRoot.TryGetValue(link.Root, out int serial)) { linked = true; return serial; }
            linked = AttackFeed.InAttack || (link.Root != 0 && Time.frameCount == attackFrame);
            return currentAttack;
        }

        private static void OnDamaged(PlantHealth plant, int damage, DamageType type)
        {
            HarvestLink link = plant.LastHitLink;
            // Zincir kökü saldırıya ilk doğrudan vuruşta bağlanır: aynı saldırının içinde doğan zincir kararları da kesin bağlanır.
            if (type == DamageType.Direct && link.Root != 0 && AttackFeed.InAttack) attackOfRoot[link.Root] = currentAttack;
            if (Level != LogLevel.Detailed) return;   // vuruş satırı yalnız ayrıntılı izde üretilir
            int attack = AttackOf(link, out bool linked);
            Kind kind = Kind.Damage;
            if (type == DamageType.Reward || link.Echo) kind |= Kind.Extra;
            else if (link.IsChain) kind |= Kind.Chain;
            string name = plant.Data != null ? plant.Data.name : "bitki";
            // Gözlem, hasar candan düşülmeden önce gelir: kalan can burada hesaplanır.
            int after = plant.CurrentHealth - damage;
            Add(kind, $"{TypeName(type, link)} → {CellText(plant.transform.position)} {name} · −{damage} · can {Mathf.Max(0, after)}/{plant.MaxHealth}" +
                      (link.IsChain ? $" · zincir nesli {link.Generation}" : "") + (after <= 0 ? $" · ÖLDÜ (taşan {-after})" : ""), attack, linked);
        }

        private static void OnHarvested(PlantHealth plant)
        {
            HarvestLink link = plant.KillLink;
            int attack = AttackOf(link, out bool linked);
            string name = plant.Data != null ? $"{plant.Data.name} [{plant.Data.rarity}]" : "bitki";
            Add(Kind.Harvest, $"HASAT {CellText(plant.transform.position)} {name} · öldüren: {TypeName(plant.KilledBy, link)}" +
                              (link.Root != 0 ? $" · zincir kökü {link.Root} nesil {link.Generation}" : ""), attack, linked);
        }

        private static void OnBehavior(PlanterBrain planter, DamageType type, GridObject cell, Vector3 position) => Guard(() =>
            Add(Kind.Behavior, $"Davranış başladı (normal tetik): {TypeName(type, HarvestLink.None)} · kaynak {CellText(position)}", currentAttack, AttackFeed.InAttack));

        private static void OnChain(HarvestChain.Trace trace)
        {
            if (Level != LogLevel.Detailed) return;
            int attack = attackOfRoot.TryGetValue(trace.Root, out int serial) ? serial : currentAttack;
            bool linked = attackOfRoot.ContainsKey(trace.Root);
            string type = TypeName(trace.Type, HarvestLink.None);
            switch (trace.Kind)
            {
                case HarvestChain.TraceKind.Attempt:
                    Add(Kind.Chain, $"Zincir denemesi: {type} · nesil {trace.Generation} · şans {trace.Chance:0.###} → {(trace.Success ? "TUTTU" : "tutmadı")}", attack, linked);
                    break;
                case HarvestChain.TraceKind.Fired:
                    Add(Kind.Chain, $"Zincir tetiği çalıştı: {type} · nesil {trace.Generation}", attack, linked);
                    break;
                case HarvestChain.TraceKind.Waiting:
                    Add(Kind.Chain | Kind.Rejected, $"Zincir işi kapasite bekliyor: {type} · nesil {trace.Generation} (iş düşmedi, yerinde bekliyor)", attack, linked);
                    break;
                default:
                    Add(Kind.Chain | Kind.Rejected, $"Zincir reddi: {type} · nesil {trace.Generation} · neden {RejectText(trace.Reason)}", attack, linked);
                    break;
            }
        }

        private static string RejectText(HarvestChain.Reject reason) => reason switch
        {
            HarvestChain.Reject.Repeat => "aynı kökte bu saksı + davranış zaten denendi",
            HarvestChain.Reject.Generation => "nesil sınırı",
            HarvestChain.Reject.Budget => "kök bütçesi doldu",
            HarvestChain.Reject.InvalidSource => "kaynak saksı geçersiz (satıldı / yerinde değil)",
            HarvestChain.Reject.RoundEnd => "round bitti",
            HarvestChain.Reject.Excluded => "zincir dışı vuruş (artçı / ek proc / ödül darbesi)",
            _ => reason.ToString(),
        };

        private static void OnEcho(string phase, DamageType type, int id, float delay, int frames)
        {
            string name = type == DamageType.Explosion ? "Artçı Patlama" : "Çifte Akım";
            switch (phase)
            {
                case "plan": Add(Kind.Extra, $"{name} planlandı · iş #{id} · gecikme {delay:0.##} sn", currentAttack, AttackFeed.InAttack); break;
                case "uygula": Add(Kind.Extra, $"{name} uygulandı · iş #{id} · {frames} kare sonra", currentAttack, false); break;
                default: Add(Kind.Extra | Kind.Rejected, $"{name} düştü · iş #{id} ({phase})", currentAttack, false); break;
            }
        }

        private static void OnRewardCue(RewardCue cue)
        {
            Kind kind = cue.Kind == RewardCueKind.OverloadCancelled || cue.Kind == RewardCueKind.GreedLost ? Kind.Extra | Kind.Rejected : Kind.Extra;
            Add(kind, $"Ödül etkisi: {cue.Kind} · {CellText(cue.From)}" + (cue.To != cue.From ? " → " + CellText(cue.To) : "") + $" · değer {cue.Value}", currentAttack, false);
        }

        private static void OnEventCue(FieldEventCue cue) =>
            Add(Kind.Event, $"Tarla olayı: {(cue.Event != null ? cue.Event.Title : "?")} · {cue.Kind} · sıra {cue.Index} · ({cue.Cell.x}, {cue.Cell.z}) · değer {cue.Value}", currentAttack, false);

        // ---------------------------------------------------------------- sayaç izleme (kare sonu)
        private static void SyncCounters()
        {
            tornadoRejected = TornadoManager.Instance != null ? TornadoManager.Instance.RejectedTriggers : 0;
            boomerangRejected = HarvestBehaviorManager.Instance != null ? HarvestBehaviorManager.Instance.RejectedBoomerangTriggers : 0;
            droppedTimers = RunEffects.Instance != null ? RunEffects.Instance.DroppedTimers : 0;
            for (int i = 0; i <= (int)DamageType.Reward; i++) skipped[i] = HarvestBehaviorStats.Skipped((DamageType)i);
            bossVersion = -1;
            knownStacks.Clear();
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss == null) return;
            bossVersion = boss.Version;
            foreach (BossRewardSO reward in boss.Taken) knownStacks[reward] = boss.Stacks(reward);
        }

        private static void Poll()
        {
            if (!Enabled) return;
            TornadoManager tornado = TornadoManager.Instance;
            if (tornado != null && tornado.RejectedTriggers != tornadoRejected)
            {
                Add(Kind.Rejected, $"Kasırga tetiği reddedildi: kapasite dolu ({tornado.ActiveCount}/{tornado.ActiveLimit}) · +{tornado.RejectedTriggers - tornadoRejected}", currentAttack, false);
                tornadoRejected = tornado.RejectedTriggers;
            }
            HarvestBehaviorManager behaviors = HarvestBehaviorManager.Instance;
            if (behaviors != null && behaviors.RejectedBoomerangTriggers != boomerangRejected)
            {
                Add(Kind.Rejected, $"Bumerang tetiği reddedildi: kapasite dolu ({behaviors.ActiveBoomerangs}/{behaviors.BoomerangLimit}) · +{behaviors.RejectedBoomerangTriggers - boomerangRejected}", currentAttack, false);
                boomerangRejected = behaviors.RejectedBoomerangTriggers;
            }
            for (int i = (int)DamageType.Explosion; i <= (int)DamageType.Electric; i++)
            {
                int now = HarvestBehaviorStats.Skipped((DamageType)i);
                if (now == skipped[i]) continue;
                Add(Kind.Rejected, $"Şansı tutan {TypeName((DamageType)i, HarvestLink.None)} tetiği başlatılamadı (kapasite ya da geçersiz hedef) · +{now - skipped[i]}", currentAttack, false);
                skipped[i] = now;
            }
            RunEffects effects = RunEffects.Instance;
            if (effects != null && effects.DroppedTimers != droppedTimers)
            {
                Add(Kind.Rejected, $"Ödül etkisi zamanlayıcısı düştü (kuyruk dolu {RunEffects.TimerCapacity}) · +{effects.DroppedTimers - droppedTimers}", currentAttack, false);
                droppedTimers = effects.DroppedTimers;
            }
            BossRewardManager boss = BossRewardManager.Instance;
            if (boss == null || boss.Version == bossVersion) return;
            bossVersion = boss.Version;
            foreach (BossRewardSO reward in boss.Taken)
            {
                int stacks = boss.Stacks(reward);
                knownStacks.TryGetValue(reward, out int known);
                if (stacks > known) Add(Kind.Reward, $"Ödül edinildi: {reward.displayName} · stack {stacks}/{reward.maxStacks}", currentAttack, false);
                knownStacks[reward] = stacks;
            }
        }

        // ---------------------------------------------------------------- stat değişikliği
        private static void WatchStats()
        {
            watchedStats = StatManager.Instance;
            if (watchedStats == null) return;
            watchedStats.OnGlobalModifierAdded += OnModifierAdded;
            watchedStats.OnGlobalModifierRemoved += OnModifierRemoved;
        }

        private static void UnwatchStats()
        {
            if (watchedStats != null)
            {
                watchedStats.OnGlobalModifierAdded -= OnModifierAdded;
                watchedStats.OnGlobalModifierRemoved -= OnModifierRemoved;
            }
            watchedStats = null;
        }

        private static void OnModifierAdded(StatModifier modifier) => Guard(() => { if (Level == LogLevel.Detailed) Add(Kind.Stat, "Stat modifier eklendi: " + TestStats.Describe(modifier), currentAttack, false); });
        private static void OnModifierRemoved(StatModifier modifier) => Guard(() => { if (Level == LogLevel.Detailed) Add(Kind.Stat, "Stat modifier kaldırıldı: " + TestStats.Describe(modifier), currentAttack, false); });
    }
}
