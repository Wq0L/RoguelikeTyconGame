using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // PASS / FAIL yalnız otomatik karşılaştırılan satırlar içindir. İNCELENMEDİ: ön koşul sağlanamadı ya da olay gerçekleşmedi.
    // Manuel: otomatik doğrulanamıyor, gözle bakılmalı. Sistem yok: oyunda henüz olmayan sistem.
    // UYARI: sayı oyunun kuralına uyuyor ama oyuncuya yazılan değerin bir kısmı kullanılamıyor ya da yazım yanıltıcı (tasarım kararı gerekir).
    public enum CheckStatus { NotRun, Running, Pass, Fail, NotChecked, Manual, Missing, Warning }

    // Kapsam matrisi: otomatik doğrulanan · yalnız gözlemlenebilen · eksik.
    public enum CheckCoverage { Auto, Observed, Missing }

    public sealed class CheckLine
    {
        public string Name = "", Expected = "", Actual = "", Tolerance = "";
        public CheckStatus Status;
    }

    public sealed class CheckResult
    {
        public string Id = "", Group = "", Title = "", Precondition = "", Action = "", Note = "";
        public CheckCoverage Coverage;
        public CheckStatus Status = CheckStatus.NotRun;
        public readonly List<CheckLine> Lines = new();
        public int Round;
        public double GameTime;
    }

    public sealed class CheckDefinition
    {
        public string Id = "", Group = "", Title = "", Precondition = "", Action = "", Note = "";
        public CheckCoverage Coverage = CheckCoverage.Auto;
        // null: çalıştırılacak bir şey yok (manuel inceleme ya da henüz olmayan sistem).
        public Func<CheckContext, IEnumerator> Body;
    }

    // Tek iş parçacıklı, kare kare ilerleyen küçük yürütücü (kontroller ve şans örneklemesi). Editor güncellemesinde adım atar;
    // beklemeler oyun karesi / oyun zamanı / koşul üzerindendir. Aynı anda tek iş çalışır.
    public static class LabRoutine
    {
        private static readonly Stack<IEnumerator> stack = new();
        private static Action<Exception> onError;
        private static Action onDone;
        private static bool hooked;

        public static bool Running => stack.Count > 0;
        public static string Name { get; private set; } = "";

        public static bool Start(string name, IEnumerator body, Action done = null, Action<Exception> error = null)
        {
            if (Running) return false;
            Name = name;
            onDone = done;
            onError = error;
            stack.Push(body);
            if (!hooked) { hooked = true; EditorApplication.update += Step; }
            return true;
        }

        public static void Stop()
        {
            stack.Clear();
            Name = "";
            if (hooked) { hooked = false; EditorApplication.update -= Step; }
        }

        private static void Step()
        {
            if (!Running) { Stop(); return; }
            if (!EditorApplication.isPlaying || !TestSession.Active) { Finish(new Exception("test oturumu kapandı")); return; }
            try
            {
                // Aynı güncellemede iç içe yürütücülere inilir; "yield return null" bir sonraki güncellemeyi bekler.
                for (int guard = 0; guard < 64 && stack.Count > 0; guard++)
                {
                    IEnumerator top = stack.Peek();
                    if (!top.MoveNext()) { stack.Pop(); continue; }
                    if (top.Current is IEnumerator nested) { stack.Push(nested); continue; }
                    return;
                }
                if (stack.Count == 0) Finish(null);
            }
            catch (Exception e) { Finish(e); }
        }

        private static void Finish(Exception error)
        {
            Action done = onDone;
            Action<Exception> failed = onError;
            Stop();
            if (error != null) failed?.Invoke(error);
            done?.Invoke();
        }

        // ---------------------------------------------------------------- beklemeler
        public static IEnumerator Frames(int count)
        {
            int target = Time.frameCount + Mathf.Max(1, count);
            while (Time.frameCount < target) yield return null;
        }

        // Oyun zamanı (ölçekli). Simülasyon duraklatılmışsa ilerlemez: çağıran duraklatmayı kaldırmış olmalıdır.
        public static IEnumerator Seconds(double seconds)
        {
            double until = Time.timeAsDouble + seconds;
            while (Time.timeAsDouble < until) yield return null;
        }

        // Koşul sağlanana kadar bekler; gerçek zaman sınırı aşılırsa timedOut çağrılır ve bekleme biter.
        public static IEnumerator Until(Func<bool> condition, double realSeconds, Action timedOut = null)
        {
            double deadline = EditorApplication.timeSinceStartup + realSeconds;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup > deadline) { timedOut?.Invoke(); yield break; }
                yield return null;
            }
        }
    }

    // Bağımsız, açık aritmetik: beklenen değerler burada, oyunun hesap fonksiyonları ÇAĞRILMADAN üretilir. Girdiler asset
    // değerleri (ödül / plan / boss verisi) ve işlemden ÖNCE ölçülmüş gerçek değerlerdir.
    public static class Arith
    {
        // Oyunun tam sayıya çevirme kuralının bağımsız yazımı: en yakın tam sayı, ,5 çifte; en az "minimum".
        public static long Round(double value, long minimum = 0)
        {
            if (double.IsNaN(value)) return minimum;
            double rounded = Math.Round(value, MidpointRounding.ToEven);
            if (rounded > int.MaxValue) rounded = int.MaxValue;
            return Math.Max(minimum, (long)rounded);
        }

        // Stat kuralı: (taban ya da Set + düz toplam) × (1 + toplanan yüzde) × ayrı çarpanların çarpımı.
        public static double Stat(double baseValue, IEnumerable<StatModifier> modifiers, StatType stat)
        {
            double flat = 0d, percent = 0d, more = 1d, start = baseValue;
            foreach (StatModifier modifier in modifiers)
            {
                if (modifier.statType != stat) continue;
                switch (modifier.operation)
                {
                    case ModifierOperation.Flat: flat += modifier.value; break;
                    case ModifierOperation.AddPercent: percent += modifier.value; break;
                    case ModifierOperation.MorePercent: more *= 1d + modifier.value; break;
                    default: start = modifier.value; break;
                }
            }
            return (start + flat) * (1d + percent) * more;
        }

        // Oyunun stat sınırlarının bağımsız yazımı (alt / üst sınır). Listede olmayan stat sınırsızdır.
        public static double Clamp(StatType stat, double value) => stat switch
        {
            StatType.HarvestDamage or StatType.AreaRadius => Math.Max(0d, value),
            StatType.AttackSpeed => Math.Max(0.05d, value),
            StatType.CritChance or StatType.ExplosionChance or StatType.DuplicateChance or StatType.TornadoChance or StatType.BoomerangChance or StatType.ElectricChance
                => Math.Min(1d, Math.Max(0d, value)),
            StatType.CritMultiplier or StatType.GridUnlockSize or StatType.RoundDuration => Math.Max(1d, value),
            StatType.PlantSpawnRate => Math.Max(0.5d, value),
            StatType.RareSpawnChance => Math.Min(95d, Math.Max(0d, value)),
            StatType.GoldGainMultiplier or StatType.IronGainMultiplier or StatType.StoneGainMultiplier or StatType.XPGainMultiplier or StatType.HarvestScoreMultiplier
                or StatType.PlanterDamageMultiplier or StatType.MutationLuck or StatType.StartingGoldBonus or StatType.StartingPlanterCount or StatType.ExtraCardChoice
                or StatType.RerollCard or StatType.CardSkip => Math.Max(0d, value),
            _ => value,
        };

        // Bir stat'ın ham tabandan ve modifier listelerinden yeniden hesabı (sınırsız): hedefi "All" ya da bağlamla aynı olan
        // katkılar sayılır. includeAllTarget false: "All" hedefli global katkılar sayılmaz (saksının skor çarpanında olduğu gibi).
        public static double Resolve(double baseValue, StatType stat, StatTarget context, IEnumerable<StatModifier> globals, IEnumerable<StatModifier> locals, bool includeAllTarget = true)
        {
            var matching = new List<StatModifier>();
            if (globals != null)
                foreach (StatModifier modifier in globals)
                    if (modifier.statType == stat && ((modifier.target == StatTarget.All && includeAllTarget) || modifier.target == context)) matching.Add(modifier);
            if (locals != null)
                foreach (StatModifier modifier in locals)
                    if (modifier.statType == stat && (modifier.target == StatTarget.All || modifier.target == context)) matching.Add(modifier);
            return Stat(baseValue, matching, stat);
        }

        // Apply'ın tersi: tek bir katkı çıkarıldığında kalan değer (o stat'ta başka yüzde katkısı yokken).
        public static double Unapply(double after, StatModifier modifier) => modifier.operation switch
        {
            ModifierOperation.Flat => after - modifier.value,
            ModifierOperation.AddPercent or ModifierOperation.MorePercent => after / (1d + modifier.value),
            _ => after,
        };

        // Tek bir yeni katkının, ölçülmüş önceki değere etkisi (o stat'ta başka yüzde katkısı yoksa düz katkı doğrudan eklenir).
        public static double Apply(double before, StatModifier modifier) => modifier.operation switch
        {
            ModifierOperation.Flat => before + modifier.value,
            ModifierOperation.AddPercent => before * (1d + modifier.value),
            ModifierOperation.MorePercent => before * (1d + modifier.value),
            _ => modifier.value,
        };
    }

    public sealed class CheckContext
    {
        public readonly CheckResult Result;
        public TraceRecord Trace;
        // Ön koşul sağlanamadı: kontrolün kalanı çalıştırılmaz.
        public bool Aborted;

        public CheckContext(CheckResult result) { Result = result; }

        private CheckLine Add(string name, string expected, string actual, string tolerance, CheckStatus status)
        {
            var line = new CheckLine { Name = name, Expected = expected, Actual = actual, Tolerance = tolerance, Status = status };
            Result.Lines.Add(line);
            return line;
        }

        // Sayısal karşılaştırma. tolerance 0: tam eşitlik (kayan nokta gürültüsü için 1e-6 göreli pay).
        public bool Near(string name, double expected, double actual, double tolerance = 0d, string reason = "", string format = "0.####")
        {
            double allowed = tolerance > 0d ? tolerance : 1e-6 * Math.Max(1d, Math.Abs(expected));
            bool ok = !double.IsNaN(actual) && Math.Abs(expected - actual) <= allowed;
            Add(name, expected.ToString(format), actual.ToString(format), tolerance > 0d ? $"±{tolerance.ToString("0.####")} ({reason})" : "tam", ok ? CheckStatus.Pass : CheckStatus.Fail);
            return ok;
        }

        public bool Exact(string name, long expected, long actual)
        {
            Add(name, expected.ToString(), actual.ToString(), "tam", expected == actual ? CheckStatus.Pass : CheckStatus.Fail);
            return expected == actual;
        }

        public bool True(string name, string expected, string actual, bool ok)
        {
            Add(name, expected, actual, "tam", ok ? CheckStatus.Pass : CheckStatus.Fail);
            return ok;
        }

        // Hesap kurala uygun, ama yazan değer ile oyuncunun gerçekte aldığı arasında fark var: FAIL değil, karar gerektiren bulgu.
        public void Warn(string name, string expected, string actual) => Add(name, expected, actual, "—", CheckStatus.Warning);

        // Otomatik doğrulanamayan kısım: sonucu etkilemez, raporda "manuel inceleme gerekli" diye durur.
        public void Manual(string name, string why) => Add(name, "manuel inceleme gerekli", why, "—", CheckStatus.Manual);

        // Ön koşul sağlanamadı ya da olay gerçekleşmedi: PASS sayılmaz.
        public void Skip(string name, string why)
        {
            Add(name, "ön koşul", why, "—", CheckStatus.NotChecked);
            Aborted = true;
        }

        // Olay gerçekleşmediği için bakılamayan tek satır (kontrol sürer).
        public void Unseen(string name, string why) => Add(name, "olay gözlenmeli", why, "—", CheckStatus.NotChecked);

        // ---------------------------------------------------------------- eylemler (panelin kullandığı aynı komutlar)
        public IEnumerator Build(Scenario scenario)
        {
            Result rebuilt = TestSession.Rebuild(scenario);
            if (!rebuilt.Ok) { Skip("senaryo kurulumu", rebuilt.Message); yield break; }
            yield return LabRoutine.Frames(1);
            bool timedOut = false;
            yield return LabRoutine.Until(() => TestSession.Ready, 90d, () => timedOut = true);
            if (timedOut) { Skip("senaryo kurulumu", "oturum zamanında hazır olmadı: " + TestSession.Status); yield break; }
            var failures = new StringBuilder();
            foreach (Result step in TestSession.SetupReport) if (!step.Ok) failures.Append(failures.Length > 0 ? " | " : "").Append(step.Message);
            if (failures.Length > 0) { Skip("senaryo kurulumu", "kurulamayan adım: " + failures); yield break; }
            TestTime.SetPaused(false);
            TestTime.SetSpeed(1f);
            // Kurulumdan sonra bir kare: yöneticilerin kare sonu işleri otursun.
            yield return LabRoutine.Frames(2);
        }

        // Tek gerçek doğrudan saldırı + sayısal iz. seed ≠ 0: saldırıdan hemen önce oyunun rastgele akışı bu seed'le başlatılır
        // (açık test eylemi; aynı seed aynı sapmayı ve aynı kritik zarını verir).
        public IEnumerator Attack(int x, int z, int seed = 0, bool waitForClose = true)
        {
            Trace = null;
            TestTrace.Reset(false);
            TestTrace.Arm();
            TestAttack.ResetCooldown();
            if (seed != 0) UnityEngine.Random.InitState(seed);
            Result attacked = TestAttack.AttackOnce(TestAttack.WorldOf(new GridPosition(x, z)));
            if (!attacked.Ok) { TestTrace.Cancel(); Skip("saldırı", attacked.Message); yield break; }
            if (!waitForClose) yield break;
            yield return WaitTrace();
        }

        public IEnumerator WaitTrace()
        {
            bool timedOut = false;
            yield return LabRoutine.Until(() => TestTrace.Current == null && !TestTrace.Armed, 60d, () => timedOut = true);
            if (timedOut) { TestTrace.Cancel(); Skip("saldırı izi", "iz zamanında kapanmadı; bekleyen: " + TestTrace.PendingWork()); yield break; }
            Trace = TestTrace.Last;
            if (Trace == null) Skip("saldırı izi", "iz kaydı oluşmadı");
            else if (Trace.Truncated) Skip("saldırı izi", "kayıt kesildi, sonuç eksik: " + Trace.TruncatedReason);
        }

        public BossRewardSO Reward(string id)
        {
            BossRewardManager boss = BossRewardManager.Instance;
            BossRewardSO reward = boss != null ? LabCatalog.Reward(boss.Pool, id) : null;
            if (reward == null) Skip("ödül", $"{id} bu profilin ödül havuzunda yok");
            return reward;
        }

        // Kontrollü kurulum (oyunun alınabilirlik denetimiyle). Reddedilirse kontrol İNCELENMEDİ olur.
        public BossRewardSO Grant(string id)
        {
            BossRewardSO reward = Reward(id);
            if (reward == null) return null;
            Result granted = TestRewards.Grant(reward);
            if (!granted.Ok) { Skip("ödül kurulumu", granted.Message); return null; }
            return reward;
        }

        public T Effect<T>(BossRewardSO reward) where T : RunEffectSO
        {
            if (reward != null && reward.effect is T data) return data;
            Skip("ödül verisi", $"{(reward != null ? reward.id : "?")} beklenen etki verisini taşımıyor ({typeof(T).Name})");
            return null;
        }

        public static PlantHealth Plant(int x, int z) => TestField.PlantAt(new GridPosition(x, z));
        public static PlanterBrain Planter(int x, int z) => TestField.PlanterAt(new GridPosition(x, z));
        public static PlantSpawner Spawner(int x, int z) => TestField.SpawnerAt(new GridPosition(x, z));

        public TraceHit Direct(int x, int z)
        {
            if (Trace == null) return null;
            foreach (TraceHit hit in Trace.Hits)
                if (hit.Type == DamageType.Direct && !hit.Foreign && hit.Target.x == x && hit.Target.z == z) return hit;
            return null;
        }

        public void Hits(DamageType type, List<TraceHit> into, RewardSource? source = null)
        {
            into.Clear();
            if (Trace == null) return;
            foreach (TraceHit hit in Trace.Hits)
                if (hit.Type == type && (!source.HasValue || hit.Reward == source.Value)) into.Add(hit);
        }
    }

    // Beklenen / gerçekleşen kontrollerin yürütücüsü ve sonuç defteri. Kontrollerin kendisi NumericChecks.Catalog.cs'dedir.
    public static partial class NumericChecks
    {
        private static readonly List<CheckResult> results = new();
        private static List<CheckDefinition> catalog;
        private static bool pausedBefore;
        private static float speedBefore;
        private static Scenario scenarioBefore;

        public static IReadOnlyList<CheckResult> Results => results;
        public static IReadOnlyList<CheckDefinition> Catalog => catalog ??= BuildCatalog();
        public static bool Running => LabRoutine.Running && LabRoutine.Name == RoutineName;
        public static string CurrentId { get; private set; } = "";
        public static int Done { get; private set; }
        public static int Planned { get; private set; }
        public static int Version { get; private set; }
        private const string RoutineName = "kontroller";

        public static string StatusText(CheckStatus status) => status switch
        {
            CheckStatus.Pass => "PASS",
            CheckStatus.Fail => "FAIL",
            CheckStatus.NotChecked => "İNCELENMEDİ",
            CheckStatus.Manual => "manuel inceleme gerekli",
            CheckStatus.Missing => "sistem henüz yok",
            CheckStatus.Running => "çalışıyor",
            CheckStatus.Warning => "UYARI",
            _ => "çalıştırılmadı",
        };

        public static string CoverageText(CheckCoverage coverage) => coverage switch
        {
            CheckCoverage.Auto => "otomatik doğrulanan",
            CheckCoverage.Observed => "yalnız gözlemlenebilen",
            _ => "eksik",
        };

        public static CheckResult ResultOf(string id) => results.Find(r => r.Id == id);

        public static void Clear() { results.Clear(); Version++; }

        public static Result Run(IReadOnlyList<CheckDefinition> selection)
        {
            if (!TestSession.Ready) return Result.Fail("Test oturumu hazır değil.");
            if (LabRoutine.Running) return Result.Fail("Başka bir iş sürüyor: " + LabRoutine.Name + ".");
            if (selection == null || selection.Count == 0) return Result.Fail("Çalıştırılacak kontrol yok.");
            pausedBefore = TestTime.Paused; speedBefore = TestTime.Speed;
            scenarioBefore = TestSession.Current != null ? TestSession.Current.Clone() : null;
            Planned = selection.Count;
            Done = 0;
            var list = new List<CheckDefinition>(selection);
            LabRoutine.Start(RoutineName, RunAll(list), Finished);
            return Result.Success($"{list.Count} kontrol başlatıldı. Kontroller senaryoyu yeniden kurar; bitince önceki senaryo geri kurulur.");
        }

        public static void Stop()
        {
            if (!Running) return;
            LabRoutine.Stop();
            foreach (CheckResult result in results) if (result.Status == CheckStatus.Running) { result.Status = CheckStatus.NotChecked; result.Note = "kullanıcı durdurdu"; }
            Finished();
        }

        private static void Finished()
        {
            CurrentId = "";
            TestTrace.Reset(false);
            Version++;
            if (!TestSession.Active || !EditorApplication.isPlaying) return;
            // Kontrol öncesi senaryo temiz oturuma geri kurulur (kontrolün kurduğu ödül / stat / tarla kalmaz).
            if (scenarioBefore != null) TestSession.Rebuild(scenarioBefore);
            TestTime.SetSpeed(speedBefore);
            TestTime.SetPaused(pausedBefore);
            LabReport.Remember("Beklenen / gerçekleşen kontroller", Summary() + "\n" + AllText());
        }

        private static IEnumerator RunAll(List<CheckDefinition> list)
        {
            foreach (CheckDefinition definition in list)
            {
                yield return RunOne(definition);
                Done++;
            }
        }

        private static IEnumerator RunOne(CheckDefinition definition)
        {
            var result = new CheckResult
            {
                Id = definition.Id, Group = definition.Group, Title = definition.Title, Precondition = definition.Precondition, Action = definition.Action,
                Note = definition.Note, Coverage = definition.Coverage, Status = CheckStatus.Running,
            };
            results.RemoveAll(r => r.Id == definition.Id);
            results.Add(result);
            CurrentId = definition.Id;
            Version++;
            if (definition.Body == null)
            {
                result.Status = definition.Coverage == CheckCoverage.Missing ? CheckStatus.Missing : CheckStatus.Manual;
                yield break;
            }
            var context = new CheckContext(result);
            IEnumerator body = Guarded(definition.Body(context), context);
            yield return body;
            RoundManager rounds = RoundManager.Instance;
            result.Round = rounds != null ? rounds.CurrentRound : 0;
            result.GameTime = Time.timeAsDouble;
            result.Status = Aggregate(result);
            TestLog.Note(TestLog.Kind.Tool, $"Kontrol {result.Id}: {StatusText(result.Status)} — {result.Title}");
            Version++;
        }

        // Kontrolün içindeki istisna oyunu ve sıradaki kontrolleri kesmez: FAIL satırı olarak yazılır.
        private static IEnumerator Guarded(IEnumerator body, CheckContext context)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(body);
            while (stack.Count > 0)
            {
                if (context.Aborted) yield break;
                IEnumerator top = stack.Peek();
                bool moved;
                try { moved = top.MoveNext(); }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Mechanics Test Lab] Kontrol {context.Result.Id} istisnayla kesildi: {e}");
                    context.True("kontrol yürütmesi", "istisnasız", "istisna: " + e.Message, false);
                    yield break;
                }
                if (!moved) { stack.Pop(); continue; }
                if (top.Current is IEnumerator nested) stack.Push(nested);
                else yield return null;
            }
        }

        private static CheckStatus Aggregate(CheckResult result)
        {
            bool pass = false, unchecked_ = false, manual = false, warning = false;
            foreach (CheckLine line in result.Lines)
            {
                if (line.Status == CheckStatus.Fail) return CheckStatus.Fail;
                if (line.Status == CheckStatus.Pass) pass = true;
                else if (line.Status == CheckStatus.NotChecked) unchecked_ = true;
                else if (line.Status == CheckStatus.Manual) manual = true;
                else if (line.Status == CheckStatus.Warning) warning = true;
            }
            if (unchecked_) return CheckStatus.NotChecked;
            if (warning) return CheckStatus.Warning;
            if (pass) return CheckStatus.Pass;
            return manual ? CheckStatus.Manual : CheckStatus.NotChecked;
        }

        // ---------------------------------------------------------------- metin
        public static string Summary()
        {
            int pass = 0, fail = 0, notChecked = 0, manual = 0, missing = 0, warning = 0;
            foreach (CheckResult result in results)
                switch (result.Status)
                {
                    case CheckStatus.Pass: pass++; break;
                    case CheckStatus.Warning: warning++; break;
                    case CheckStatus.Fail: fail++; break;
                    case CheckStatus.NotChecked: notChecked++; break;
                    case CheckStatus.Manual: manual++; break;
                    case CheckStatus.Missing: missing++; break;
                }
            return $"Kontrol özeti: {pass} PASS · {warning} UYARI · {fail} FAIL · {notChecked} İNCELENMEDİ · {manual} manuel inceleme gerekli · {missing} sistem henüz yok (toplam {results.Count} / katalog {Catalog.Count})";
        }

        public static string ToText(CheckResult result)
        {
            var text = new StringBuilder();
            text.Append($"[{StatusText(result.Status)}] {result.Id} · {result.Group} · {result.Title} ({CoverageText(result.Coverage)})\n");
            if (result.Precondition.Length > 0) text.Append("   Ön koşul: ").Append(result.Precondition).Append('\n');
            if (result.Action.Length > 0) text.Append("   İşlem: ").Append(result.Action).Append('\n');
            foreach (CheckLine line in result.Lines)
                text.Append($"   [{StatusText(line.Status)}] {line.Name} | beklenen: {line.Expected} | gerçekleşen: {line.Actual} | tolerans: {line.Tolerance}\n");
            if (result.Note.Length > 0) text.Append("   Not: ").Append(result.Note).Append('\n');
            return text.ToString();
        }

        public static string AllText()
        {
            var text = new StringBuilder();
            foreach (CheckResult result in results) text.Append(ToText(result));
            return text.ToString();
        }

        public static string ToCsv()
        {
            var text = new StringBuilder("kimlik;grup;baslik;kapsam;durum;satir;beklenen;gerceklesen;tolerans;satir_durumu;on_kosul;islem;not\n");
            foreach (CheckResult result in results)
            {
                if (result.Lines.Count == 0)
                    text.Append(Row(result, null));
                foreach (CheckLine line in result.Lines) text.Append(Row(result, line));
            }
            return text.ToString();
        }

        private static string Row(CheckResult result, CheckLine line) =>
            Snapshots.Csv(result.Id) + ";" + Snapshots.Csv(result.Group) + ";" + Snapshots.Csv(result.Title) + ";" + Snapshots.Csv(CoverageText(result.Coverage)) + ";" +
            Snapshots.Csv(StatusText(result.Status)) + ";" + Snapshots.Csv(line != null ? line.Name : "") + ";" + Snapshots.Csv(line != null ? line.Expected : "") + ";" +
            Snapshots.Csv(line != null ? line.Actual : "") + ";" + Snapshots.Csv(line != null ? line.Tolerance : "") + ";" + Snapshots.Csv(line != null ? StatusText(line.Status) : "") + ";" +
            Snapshots.Csv(result.Precondition) + ";" + Snapshots.Csv(result.Action) + ";" + Snapshots.Csv(result.Note) + "\n";

        // Kapsam matrisi (katalogdan; çalıştırılmış sonuç varsa durumuyla).
        public static string Matrix()
        {
            var text = new StringBuilder("KAPSAM MATRİSİ\n");
            foreach (CheckCoverage coverage in new[] { CheckCoverage.Auto, CheckCoverage.Observed, CheckCoverage.Missing })
            {
                text.Append('\n').Append(CoverageText(coverage).ToUpperInvariant()).Append('\n');
                foreach (CheckDefinition definition in Catalog)
                {
                    if (definition.Coverage != coverage) continue;
                    CheckResult result = ResultOf(definition.Id);
                    text.Append($"  {definition.Id} · {definition.Group} · {definition.Title}" + (result != null ? $" → {StatusText(result.Status)}" : "") + "\n");
                }
            }
            return text.ToString();
        }
    }
}
