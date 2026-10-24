using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Batch (izole kopya): Mechanics Test Lab'ın SAYISAL DOĞRULAMA katmanının araç doğrulaması. İki ayrı şey ölçülür ve ayrı
// raporlanır:
//  1) ARAÇ: görüntüler canlı referansla birlikte değişmiyor mu, katkılar doğru kapsamda mı, saldırı izi gerçek can değişimiyle
//     ve hesap bakiyeleriyle eşleşiyor mu, buff yaşam döngüsü yazılıyor mu, yeniden kurulumda sızıntı var mı, kayıt açık / kapalı
//     oynanış sonucunu değiştiriyor mu, asset / profil seçimi / oyuncu kaydı değişmedi mi.
//  2) OYUN: panelin beklenen / gerçekleşen kontrol kataloğu (NumericChecks) eksiksiz çalıştırılır; her kontrolün PASS / FAIL /
//     İNCELENMEDİ / manuel / sistem yok sonucu olduğu gibi yazılır. Hiçbir beklenti PASS çıksın diye gevşetilmez.
// Denge ölçümü ve insan Play testi DEĞİLDİR. Zaman: sabit kare adımı (1 / 30 sn).
[InitializeOnLoad]
public static class MechanicsNumericVerification
{
    const string Key = "MechanicsNumericVerification";
    const string LogFile = "Logs/MechanicsNumericVerification.txt";
    const string ChecksFile = "Logs/MechanicsNumericChecks.txt";
    const string HashFile = "Logs/MechanicsNumericVerification.hashes.txt";
    const string ShotFolder = "Logs/MechanicsNumericPanel";
    const string ExportFolder = "Logs/MechanicsNumericExport";
    const string SelectionPath = "Assets/Resources/RunProfileSelection.asset";
    const double Again = double.NaN;
    const float Dt = 1f / 30f;
    const int C = 5;

    static readonly List<string> notes = new();
    static readonly Queue<(string name, Func<double> run)> steps = new();
    static double nextAt, stepSince;
    static int stepIndex, shownStep = -1, errors;
    static bool planned, exiting;

    static MechanicsNumericVerification() { EditorApplication.update += Tick; }

    // Yalnız seçilen kontroller (hızlı tekrar): MTL_CHECKS ortam değişkeni, virgülle ayrılmış kimlikler (ör. "A03,S09").
    public static void RunChecksBatch()
    {
        SessionState.SetString(Key + ".ids", Environment.GetEnvironmentVariable("MTL_CHECKS") ?? "");
        Start("checks");
    }

    public static void RunBatch() => Start("");

    static bool ChecksOnly => SessionState.GetString(Key + ".mode", "") == "checks";

    static void Start(string mode)
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        SessionState.SetString(Key + ".mode", mode);
        var pipeline = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        if (!TestSession.SceneExists) throw new Exception("test scene could not be built");
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        SessionState.SetString(Key + ".selection", selection != null && selection.active != null ? selection.active.name : "");
        SessionState.SetString(Key + ".save", SaveStamp());
        Directory.CreateDirectory("Logs");
        File.WriteAllText(HashFile, Hashes());
        EditorSceneManager.OpenScene(Lab.ScenePath);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    static string SaveStamp()
    {
        string path = Path.Combine(Application.persistentDataPath, MetaSave.FileName);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path).Ticks + ":" + new FileInfo(path).Length : "none";
    }

    static string Hashes()
    {
        var text = new StringBuilder();
        using MD5 md5 = MD5.Create();
        var files = new List<string>();
        foreach (string root in new[] { "Assets/ScriptableObjects", "Assets/Resources", "Assets/Scenes" })
            files.AddRange(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta") && (f.EndsWith(".asset") || f.EndsWith(".unity"))));
        files.Sort(StringComparer.Ordinal);
        foreach (string file in files)
            text.Append(file.Replace('\\', '/')).Append(' ').Append(BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(file)))).Append('\n');
        text.Append("BUILD ").Append(string.Join("|", EditorBuildSettings.scenes.Select(s => s.path)));
        return text.ToString();
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling) return;
        try
        {
            if (exiting)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                AfterPlay();
                Finish(null);
                return;
            }
            if (!EditorApplication.isPlaying) return;
            double now = EditorApplication.timeSinceStartup;
            if (!planned)
            {
                planned = true; nextAt = now + 1; stepSince = now;
                Application.logMessageReceived += CountLogs;
                Time.captureDeltaTime = Dt;
                Plan();
                return;
            }
            if (now < nextAt) { EditorApplication.QueuePlayerLoopUpdate(); return; }
            if (steps.Count == 0) { exiting = true; EditorApplication.ExitPlaymode(); return; }
            if (stepIndex != shownStep)
            {
                shownStep = stepIndex; stepSince = now;
                File.WriteAllLines(LogFile, new[] { $"RUNNING step {stepIndex}: {steps.Peek().name}" }.Concat(notes));
            }
            else if (now - stepSince > 2400) throw new Exception($"step {stepIndex} ({steps.Peek().name}) stuck · session status: {TestSession.Status} · routine: {LabRoutine.Name} {NumericChecks.CurrentId}");
            double wait = steps.Peek().run();
            if (double.IsNaN(wait)) { nextAt = now; EditorApplication.QueuePlayerLoopUpdate(); return; }
            steps.Dequeue(); stepIndex++;
            nextAt = now + wait;
        }
        catch (Exception ex) { Finish(ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex); }
    }

    static void CountLogs(string message, string stack, LogType type)
    {
        if (stack != null && stack.Contains("UnityEditor.Search")) return;
        if (type != LogType.Exception && type != LogType.Error) return;
        errors++; notes.Add("   LOG " + type + ": " + message);
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= CountLogs;
        Time.captureDeltaTime = 0f; Time.timeScale = 1f;
        Directory.CreateDirectory("Logs");
        bool pass = ex == null && errors == 0 && catalogFails == 0;
        string head = pass ? "PASS: " + notes.Count(n => n.StartsWith("ok")) + " tool checks · catalog: " + catalogSummary
            : ex != null ? "FAIL: " + ex : errors > 0 ? $"FAIL: {errors} error log(s) during the run" : $"FAIL: {catalogFails} catalog check(s) reported FAIL · " + catalogSummary;
        File.WriteAllLines(LogFile, new[] { head }.Concat(notes));
        UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null; QualitySettings.renderPipeline = null;
        EditorApplication.Exit(pass ? 0 : 1);
    }

    // ---------------------------------------------------------------- yardımcılar
    static void Ok(bool c, string m) { if (!c) throw new Exception("CHECK FAILED: " + m); notes.Add("ok: " + m); }
    static void Must(bool c, string m) { if (!c) throw new Exception("SETUP FAILED: " + m); }
    static void Note(string m) => notes.Add("   " + m);
    static void Add(string name, Func<double> run) => steps.Enqueue((name, run));
    static GridPosition P(int x, int z) => new GridPosition(x, z);
    static RoundManager Rounds => RoundManager.Instance;
    static BossRewardManager Boss => BossRewardManager.Instance;
    static StatManager Stats => StatManager.Instance;
    static BossRewardSO Reward(string id) { BossRewardSO r = LabCatalog.Reward(Boss.Pool, id); Must(r != null, "reward in pool: " + id); return r; }
    static StatModifier Mod(StatType s, StatTarget t, ModifierOperation op, float v) => new StatModifier { statType = s, target = t, operation = op, value = v };
    static Scenario Preset(string prefix) { Scenario s = ScenarioPresets.All().FirstOrDefault(p => p.name.StartsWith(prefix)); Must(s != null, "preset " + prefix); return s; }
    static string Failures() => string.Join(" | ", TestSession.SetupReport.Where(r => !r.Ok).Select(r => r.Message));
    static List<TestLog.Entry> Log(TestLog.Kind mask, string search = null) { var list = new List<TestLog.Entry>(); TestLog.Collect(mask, search, 0, list); return list; }
    static PlantHealth Plant(int x, int z) => TestField.PlantAt(P(x, z));
    static PlanterBrain Planter(int x, int z) => TestField.PlanterAt(P(x, z));
    static Result Attack(int x, int z) { TestAttack.ResetCooldown(); return TestAttack.AttackOnce(TestAttack.WorldOf(P(x, z))); }
    // Sayı metinleri kültüre göre virgül ya da nokta kullanır: karşılaştırma ikisini de kabul eder.
    static bool Starts(string text, string start) => text != null && text.Replace(',', '.').StartsWith(start);

    static int waitFrame = -1;
    static double Frames(int count)
    {
        if (waitFrame < 0) waitFrame = Time.frameCount + count;
        if (Time.frameCount < waitFrame) return Again;
        waitFrame = -1;
        return 0;
    }
    static void AddFrames(int count) => Add($"bekle {count} kare", () => Frames(count));
    static void AddReady(string what) => Add("hazır: " + what, () =>
    {
        if (!TestSession.Ready) return Again;
        Must(TestSession.SetupReport.All(r => r.Ok), what + " setup failures: " + Failures());
        return 0;
    });
    static void AddRebuild(string what, Func<Scenario> scenario)
    {
        Add("kur: " + what, () => { Must(TestSession.Rebuild(scenario()).Ok, "rebuild " + what); return 0; });
        AddFrames(1);
        AddReady(what);
        AddFrames(2);
    }
    static void AddTraceClosed(string what) => Add("iz kapandı: " + what, () => TestTrace.Current == null && !TestTrace.Armed ? 0 : Again);

    // Kontrol düzeni: 3 × 3 saksı, merkezde patlama tile'ı, sağda Damage tile'ı, hep aynı Common bitki, elle saldırı.
    static Scenario Basic()
    {
        Scenario s = ScenarioPresets.Manual(ScenarioPresets.Field(ScenarioPresets.New("Doğrulama · temel düzen", "", "", "", 5), 1), C, C);
        ScenarioPresets.Radius(s, ScenarioPresets.OneCell);
        ScenarioPresets.Tile(s, C, C, TileModifierType.Explosive, TileRarity.Rare);
        ScenarioPresets.Tile(s, C + 1, C, TileModifierType.Damage, TileRarity.Rare);
        s.plantFill = ScenarioPresets.CommonPlant();
        s.pauseProduction = true;
        return s;
    }

    // ---------------------------------------------------------------- plan
    static void Plan()
    {
        Add("oturum hazır", () => TestSession.Ready ? 0 : Again);
        if (ChecksOnly)
        {
            var ids = new HashSet<string>(SessionState.GetString(Key + ".ids", "").Split(',').Select(id => id.Trim()).Where(id => id.Length > 0));
            selectedChecks = NumericChecks.Catalog.Where(d => ids.Contains(d.Id)).ToList();
            Must(selectedChecks.Count > 0, "MTL_CHECKS names no catalog check");
            AddRebuild("seçili kontroller öncesi", Basic);
            Add("seçili kontroller", CatalogRun);
            AddFrames(2);
            AddReady("seçili kontroller sonrası geri kurulum");
            Add("seçili kontroller: sonuç", SelectedResult);
            return;
        }
        AddRebuild("temel düzen", Basic);
        Add("görüntü: canlı referans yok", Snapshot);
        Add("kaynaklar: doğru kapsam", Scopes);
        Add("makbuz: başarı, ret, koşullu etki, dışlama, seçim hakkı", Receipts);

        AddRebuild("iz düzeni", TraceScenario);
        Add("iz: gerçek can ve bakiye ile eşleşme (başlat)", TraceStart);
        AddTraceClosed("eşleşme");
        Add("iz: gerçek can ve bakiye ile eşleşme (sonuç)", TraceResult);
        AddRebuild("kasırga", () => Preset("04"));
        Add("iz: bekleyen iş varken kapanmaz (başlat)", PendingStart);
        AddFrames(12);
        Add("iz: bekleyen iş varken kapanmaz", PendingOpen);
        AddTraceClosed("kasırga bitti");
        Add("iz: kasırga bitince tamamlandı", PendingDone);
        Add("iz: kullanıcı durdurunca \"kesildi\"", Truncated);

        AddRebuild("kayıt seviyeleri", Basic);
        Add("kayıt: kapalı", LevelOff);
        AddFrames(4);
        Add("kayıt: işlem özeti", LevelSummary);
        AddFrames(4);
        Add("kayıt: ayrıntılı sayısal iz", LevelDetailed);
        AddFrames(4);
        Add("kayıt: ayrıntılı sonuç", LevelDetailedResult);

        AddRebuild("14 · Nadir Reaktör", () => Preset("14"));
        Add("buff: reaktör eklenir", ReactorAdd);
        AddFrames(30);
        Add("buff: reaktör yenilenir", ReactorRefresh);
        Add("buff: reaktör süresi biter", ReactorExpire);
        AddRebuild("06 · Hasat Ritmi", () => Preset("06"));
        Add("buff: ritim hakkı hazırlanır", RhythmReady);
        AddFrames(4);
        Add("buff: ritim hakkı harcanır", RhythmSpent);
        AddFrames(4);
        Add("buff: ritim kaydı", RhythmLogged);
        AddRebuild("22 · rezonans", () => Preset("22"));
        Add("buff: rezonans şartı bozulunca kalkar", ResonanceStart);
        AddFrames(4);
        Add("buff: rezonans ve test katkısı", ResonanceResult);

        Add("HUD: gizle", HudHide);
        AddFrames(3);
        Add("HUD: gizliyken oyun çalışır", HudHidden);
        AddRebuild("HUD yeniden kurulum", Basic);
        Add("HUD: yeniden kurulumda korunur, sonra göster", HudShow);
        AddFrames(2);
        Add("HUD: eski hâline döndü", HudShown);

        AddRebuild("şans · patlama", () => ChanceSampler.Preset(DamageType.Explosion, TileRarity.Rare));
        Add("şans: reddedilen koşul ve ilk örnek", ChanceStart);
        Add("şans: ilk örnek bitti", () => ChanceSampler.Running ? Again : 0);
        AddRebuild("şans · patlama (aynı seed)", () => ChanceSampler.Preset(DamageType.Explosion, TileRarity.Rare));
        Add("şans: aynı seed", ChanceAgain);
        Add("şans: aynı seed bitti", () => ChanceSampler.Running ? Again : 0);
        Add("şans: sonuç", ChanceResult);
        AddRebuild("şans · kasırga", () => ChanceSampler.Preset(DamageType.Tornado, TileRarity.Legendary));
        Add("şans: kasırga", ChanceTornado);
        Add("şans: kasırga bitti", () => ChanceSampler.Running ? Again : 0);
        Add("şans: kapasite reddi ayrı sayılır", ChanceTornadoResult);

        AddDeterminism("A (kayıt kapalı)", TestLog.LogLevel.Off, false);
        AddDeterminism("B (kayıt kapalı, tekrar)", TestLog.LogLevel.Off, false);
        AddDeterminism("C (ayrıntılı iz + saldırı izi + görüntü)", TestLog.LogLevel.Detailed, true);
        Add("kayıt açık / kapalı: aynı oynanış sonucu", DeterminismResult);

        AddRebuild("katalog öncesi", Basic);
        Add("katalog: bütün beklenen / gerçekleşen kontroller", CatalogRun);
        AddFrames(2);
        AddReady("katalog sonrası geri kurulum");
        Add("katalog: sonuç ve rapor", CatalogResult);

        Add("dışa aktarım", Export);
        Add("sızıntı: yeniden kurulum 1", LeakRebuild);
        AddFrames(1); AddReady("sızıntı 1"); AddFrames(2);
        Add("sızıntı: yeniden kurulum 2", LeakRebuild);
        AddFrames(1); AddReady("sızıntı 2"); AddFrames(2);
        Add("sızıntı: yeniden kurulum 3", LeakRebuild);
        AddFrames(1); AddReady("sızıntı 3"); AddFrames(2);
        Add("sızıntı: abonelik ve kalıntı yok", LeakResult);
        Add("panel: gösterilecek gerçek iz", PanelTrace);
        AddTraceClosed("panel izi");
        Add("panel: sekizinci sekme ve alt sekmeler", Panel);
    }

    // ---------------------------------------------------------------- görüntü
    static double Snapshot()
    {
        Ok(Planter(C, C) != null && Planter(C - 1, C) != null && Plant(C, C) != null, "temel düzen: saksılar ve bitki kuruldu");
        Must(Comparison.TakeBefore(P(C, C)).Ok, "take before");
        NumericSnapshot before = Comparison.Before;
        SnapshotRow damage = before.Rows.First(r => r.Key == "p." + StatType.HarvestDamage), chance = before.Rows.First(r => r.Key == "s." + StatType.ExplosionChance);
        SnapshotRow direct = before.Rows.First(r => r.Key == "p.direct.Common");
        double damageBefore = damage.Value, chanceBefore = chance.Value, directBefore = direct.Value;
        int sourcesBefore = damage.Sources.Count, rowsBefore = before.Rows.Count;
        Ok(chanceBefore > 0d && damageBefore > 0d, $"\"önce\" görüntüsü gerçek değerleri taşıyor (hasat hasarı {damageBefore:0.##}, patlama şansı {chanceBefore:0.####})");
        Ok(before.Rows.Any(r => r.Raw && r.Key == "p.raw." + StatType.HarvestDamage) && !damage.Raw, "ham taban ile çözülmüş değer ayrı satırlarda");

        Must(TestStats.Add(Mod(StatType.HarvestDamage, StatTarget.Player, ModifierOperation.MorePercent, 0.5f)).Ok, "add damage contribution");
        Must(TestStats.Add(Mod(StatType.ExplosionChance, StatTarget.Planter, ModifierOperation.MorePercent, 1f)).Ok, "add chance contribution");
        Must(TestRewards.Grant(Reward("keskin_bicak")).Ok, "grant keskin_bicak");
        Ok(Math.Abs(Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) - damageBefore * 1.5d) < 1e-3, "oyun değişti: hasat hasarı gerçekten ×1,5 oldu");
        Ok(damage.Value == damageBefore && chance.Value == chanceBefore && direct.Value == directBefore && damage.Sources.Count == sourcesBefore && before.Rows.Count == rowsBefore,
            "\"önce\" görüntüsü oyun değişince DEĞİŞMEDİ (canlı referans yok: sayı ve metin kopyası)");

        Must(Comparison.TakeAfter(true).Ok, "take after");
        NumericSnapshot after = Comparison.After;
        Ok(!ReferenceEquals(before, after) && !before.Rows.Any(r => after.Rows.Contains(r)) && damage.Value == damageBefore, "\"sonra\" ayrı bir kopya; ortak satır nesnesi yok");
        List<DiffRow> rows = Comparison.Rows;
        DiffRow damageRow = rows.FirstOrDefault(r => r.Label.StartsWith("Doğrudan hasar")), chanceRow = rows.FirstOrDefault(r => r.Label.StartsWith("Patlama şansı"));
        DiffRow multiplier = rows.FirstOrDefault(r => r.Label.StartsWith("Doğrudan vuruş · boss ödülü çarpanı"));
        Ok(damageRow != null && Starts(damageRow.Ratio, "×1.5") && damageRow.Source.Contains("TEST KATKISI"), $"fark tablosu: hasat hasarı oranı {damageRow?.Ratio} · kaynak \"{damageRow?.Source}\"");
        Ok(chanceRow != null && chanceRow.Delta.Contains("yüzde puanı") && Starts(chanceRow.Ratio, "×2"), $"şans farkı yüzde puanı olarak ({chanceRow?.Delta}), oran ayrı ({chanceRow?.Ratio})");
        Ok(multiplier != null && Starts(multiplier.After, "×1.25") && multiplier.Delta.Contains("çarpan farkı"), $"çarpan satırı çarpan olarak: {multiplier?.Before} → {multiplier?.After} ({multiplier?.Delta})");
        DiffRow directRow = rows.FirstOrDefault(r => r.Label.StartsWith("Doğrudan vuruş hasarı (Common"));
        Ok(directRow != null && directRow.Source.Contains("Keskin Bıçak") && directRow.Note.Contains("sapma"), "türetilmiş satır bağlamını ve kaynağını yazıyor: " + directRow?.Source);
        Ok(rows.All(r => r.Changed) && rows.Count < after.Rows.Count, $"yalnız değişen satırlar listelendi ({rows.Count} / {after.Rows.Count})");

        // Önce 0 olan değer: oran uydurulmaz.
        Must(Comparison.TakeBefore(P(C - 1, C)).Ok, "before on plain planter");
        Must(TestStats.Add(Mod(StatType.ElectricChance, StatTarget.Planter, ModifierOperation.Flat, 0.2f)).Ok, "flat chance");
        Must(Comparison.TakeAfter(true).Ok, "after on plain planter");
        DiffRow zero = Comparison.Rows.FirstOrDefault(r => r.Label.StartsWith("Elektrik şansı"));
        Ok(zero != null && zero.Ratio.Contains("hesaplanamaz") && zero.Delta.Contains("+20 yüzde puanı"), $"önce 0 olan şans: oran \"{zero?.Ratio}\", fark \"{zero?.Delta}\"");
        string csv = Snapshots.ToCsv(Comparison.Rows);
        Ok(csv.StartsWith("stat;kapsam;birim") && csv.Split('\n').Length == Comparison.Rows.Count + 2, "fark tablosu CSV'ye çevrildi");
        return 0;
    }

    // ---------------------------------------------------------------- kaynak kapsamı
    static double Scopes()
    {
        var sources = new List<StatSource>();
        StatSources.Collect(StatType.HarvestDamage, StatTarget.Player, null, sources);
        Ok(sources.Any(s => s.Kind == SourceKind.Base) && sources.Count(s => s.Kind == SourceKind.Test) == 1 && sources.First(s => s.Kind == SourceKind.Test).Operation == ModifierOperation.MorePercent,
            "oyuncu hasat hasarı: taban + tek test katkısı (çarpımsal)");
        StatSources.Collect(StatType.ExplosionChance, StatTarget.Planter, Planter(C, C), sources);
        Ok(sources.Any(s => s.Kind == SourceKind.Tile) && sources.Count(s => s.Kind == SourceKind.Test) == 1 && !sources.Any(s => s.Stat == StatType.HarvestDamage),
            "tile'lı saksının patlama şansı: tile + şans test katkısı; doğrudan hasar katkısı davranış şansına sızmadı");
        StatSources.Collect(StatType.ExplosionChance, StatTarget.Planter, Planter(C - 1, C), sources);
        Ok(!sources.Any(s => s.Kind == SourceKind.Tile) && sources.Count(s => s.Kind == SourceKind.Test) == 1, "tile'sız saksı: tile kaynağı yok (tile yalnız üstündeki saksıya), global test katkısı var");
        Ok(Planter(C - 1, C).GetFinalStat(StatType.ExplosionChance) == 0f, "tile'sız saksının patlama şansı 0 kaldı (çarpan 0'ı büyütmez)");
        StatSources.Collect(StatType.PlanterDamageMultiplier, StatTarget.Planter, Planter(C + 1, C), sources);
        int damageTile = sources.Count(s => s.Kind == SourceKind.Tile);
        StatSources.Collect(StatType.PlanterDamageMultiplier, StatTarget.Planter, Planter(C, C), sources);
        Ok(damageTile >= 1 && !sources.Any(s => s.Kind == SourceKind.Tile), "Damage tile'ı yalnız kendi saksısının kaynağı");
        StatSources.CollectDirect(PlantRarity.Common, sources);
        Ok(sources.Any(s => s.Kind == SourceKind.BossReward && s.Name.Contains("Keskin Bıçak") && !s.IsModifier), "doğrudan vuruş çarpanı kaynağı: boss ödülü Keskin Bıçak (stat dışı çarpan)");
        StatSources.CollectBehavior(Planter(C, C), DamageType.Explosion, sources);
        Ok(!sources.Any(s => s.Name.Contains("Keskin Bıçak")), "doğrudan vuruş ödülü davranış hasarı kaynaklarında yok");
        Ok(StatSources.OrderNote.Contains("hesap sırası DEĞİLDİR"), "kaynak listesi hesap sırası olmadığını açıkça yazıyor");
        return 0;
    }

    // ---------------------------------------------------------------- makbuz
    static double Receipts()
    {
        RewardReceipt blade = TestRewards.Receipts.LastOrDefault(r => r.Id == "keskin_bicak");
        Ok(blade != null && blade.Success && blade.StacksBefore == 0 && blade.StacksAfter == 1 && blade.Verdict.StartsWith("KURULDU") &&
           blade.Changes.Any(r => r.Label.StartsWith("Doğrudan vuruş · boss ödülü çarpanı")) && !blade.Changes.Any(r => r.Label.Contains("Davranış hasarı")),
            "Keskin Bıçak makbuzu: başarılı, stack 0 → 1, ölçülen değişim doğrudan vuruş çarpanı; davranış hasarı değişmedi");
        Ok(blade.Gains.Count > 0, "makbuz oyunun kart metnindeki kazanç satırını taşıyor: " + string.Join(" · ", blade.Gains));

        int count = TestRewards.Receipts.Count;
        Result refused = TestRewards.Grant(Reward("bereketli_toprak"));
        RewardReceipt soil = TestRewards.LastReceipt;
        Ok(!refused.Ok && TestRewards.Receipts.Count == count + 1 && !soil.Success && soil.Refusal.Length > 0 && soil.Changes.Count == 0 && soil.Verdict.StartsWith("REDDEDİLDİ"),
            "koşulu sağlanmayan ödül: makbuz ret nedenini yazıyor, hiçbir değer değişmedi — " + soil.Refusal);

        Must(TestRewards.Grant(Reward("hasat_ritmi")).Ok, "grant hasat_ritmi");
        RewardReceipt rhythm = TestRewards.LastReceipt;
        Ok(rhythm.Success && rhythm.Mechanics.Any(m => m.Contains("koşullu etki, henüz tetiklenmedi")) && rhythm.Verdict.StartsWith("KURULDU") &&
           !rhythm.Changes.Any(r => r.Label.StartsWith("Doğrudan vuruş hasarı")),
            "koşullu ödül: \"koşullu etki, henüz tetiklenmedi\" — vuruş hasarı hemen değişmedi ve bu hata sayılmadı");
        Ok(rhythm.NewlyExcluded.Any(n => n.Contains("Zincir")), "dışlama grubuna etkisi: " + string.Join(", ", rhythm.NewlyExcluded));

        int choices = Rounds.ChoicesPerLevel;
        Must(TestRewards.Grant(Reward("bereketli_ogrenim")).Ok, "grant bereketli_ogrenim");
        RewardReceipt learning = TestRewards.LastReceipt;
        Ok(learning.ChoicesBefore == choices && learning.ChoicesAfter == choices + 1 && Rounds.ChoicesPerLevel == choices + 1 && learning.Costs.Count > 0 &&
           learning.Changes.Any(r => r.Label.StartsWith("Level başına kart seçim hakkı")),
            $"bedelli ödül: seçim hakkı {choices} → {choices + 1}, bedel satırı: " + string.Join(" · ", learning.Costs));
        string text = learning.ToText();
        Ok(text.Contains("Gelecek level seçim hakkına etkisi") && text.Contains("Dışlama grubuna etkisi") && text.Contains("Ölçülen değişim"), "makbuz metni bütün bölümleri içeriyor");

        RunEffects effects = RunEffects.Instance;
        Must(TestRewards.Grant(Reward("kritik_ark")).Ok, "grant kritik_ark");
        RewardReceipt arc = TestRewards.LastReceipt;
        Ok(arc.Success && arc.Mechanics.Any(m => m.Contains("RunEffects")) && effects.Has(Reward("kritik_ark").effect), "etki ödülü: oyundaki çalışma zamanı kaydı makbuzda (ad listesi değil, gerçek kayıt)");
        return 0;
    }

    // ---------------------------------------------------------------- iz ↔ gerçek durum
    static Scenario TraceScenario()
    {
        Scenario s = ScenarioPresets.Manual(ScenarioPresets.Field(ScenarioPresets.New("Doğrulama · iz", "", "", "", 5), 2), C, C);
        ScenarioPresets.Radius(s, ScenarioPresets.OneCell);
        ScenarioPresets.Tile(s, C, C, TileModifierType.Explosive, TileRarity.Legendary);
        ScenarioPresets.Boost(s, StatType.ExplosionChance);
        ScenarioPresets.Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, 0f);
        ScenarioPresets.Reward(s, "artci_patlama");
        s.plantFill = ScenarioPresets.CommonPlant();
        s.pauseProduction = true;
        s.round = 21;
        return s;
    }

    readonly struct RealHit
    {
        public readonly PlantHealth Plant; public readonly int Damage, Before; public readonly DamageType Type;
        public RealHit(PlantHealth plant, int damage, int before, DamageType type) { Plant = plant; Damage = damage; Before = before; Type = type; }
    }
    static readonly List<RealHit> real = new();
    static readonly Dictionary<PlantHealth, int> healthAtStart = new();
    static Economy economyAtStart;
    static int harvestsReal;
    static void OnRealDamage(PlantHealth plant, int damage, DamageType type) => real.Add(new RealHit(plant, damage, plant.CurrentHealth, type));
    static void OnRealHarvest(PlantHealth plant) => harvestsReal++;

    static double TraceStart()
    {
        Must(Rounds.CurrentRound == 21 && Plant(C, C) != null, "trace scenario at R21");
        int health = Plant(C, C).MaxHealth;
        Must(TestStats.Add(Mod(StatType.HarvestDamage, StatTarget.Player, ModifierOperation.Set, Mathf.Max(20, health / 10))).Ok, "damage contribution");
        TestField.SetPlantHealth(P(C, C), 1);
        healthAtStart.Clear(); real.Clear(); harvestsReal = 0;
        var planters = new List<PlanterBrain>(); TestField.Planters(planters);
        foreach (PlanterBrain brain in planters)
            foreach (PlantSpawner spawner in brain.EditorSpawners)
                if (spawner.SpawnedPlant != null && spawner.SpawnedPlant.TryGetComponent(out PlantHealth plant)) healthAtStart[plant] = plant.CurrentHealth;
        // İzden bağımsız gözlem: oyunun gözlem olaylarına doğrudan abone olunur ve bitkilerin canı ayrıca okunur.
        PlantHealth.AnyDamaged += OnRealDamage; PlantHealth.AnyHarvested += OnRealHarvest;
        economyAtStart = Economy.Read();
        TestTrace.Arm();
        Ok(TestTrace.Armed && EditorNumericTrace.Listening, "iz kuruldu: gözlem noktaları dinleniyor");
        Must(Attack(C, C).Ok, "attack centre");
        return 0;
    }

    static double TraceResult()
    {
        PlantHealth.AnyDamaged -= OnRealDamage; PlantHealth.AnyHarvested -= OnRealHarvest;
        TraceRecord record = TestTrace.Last;
        Must(record != null, "trace record");
        Ok(record.Complete && !record.Truncated && record.Interference.Count == 0, "iz tamamlandı (kesilmedi, karışma uyarısı yok)");
        Ok(record.Hits.Count == real.Count && real.Count >= 6, $"izdeki vuruş sayısı = oyunun bildirdiği gerçek vuruş sayısı ({real.Count})");
        bool same = true;
        for (int i = 0; i < real.Count && same; i++)
            same = record.Hits[i].Computed == real[i].Damage && record.Hits[i].HealthBefore == real[i].Before && record.Hits[i].Type == real[i].Type &&
                   record.Hits[i].Decrease == Mathf.Min(real[i].Before, real[i].Damage) && record.Hits[i].Overkill == Mathf.Max(0, real[i].Damage - real[i].Before);
        Ok(same, "her vuruşta: hesaplanan darbe, önceki can, gerçek can azalması ve taşan hasar bağımsız gözlemle aynı");
        // Sağ kalan bitkilerin şimdiki gerçek canı = başlangıç canı − o bitkiye izde yazılan toplam can azalması.
        int checkedPlants = 0, wrong = 0;
        foreach (KeyValuePair<PlantHealth, int> entry in healthAtStart)
        {
            if (entry.Key.IsDead) continue;
            GridPosition cell = FieldCells.Grid.GetGridPosition(entry.Key.transform.position);
            int decrease = record.Hits.Where(h => h.Target.x == cell.x && h.Target.z == cell.z).Sum(h => h.Decrease);
            if (decrease == 0) continue;
            checkedPlants++;
            if (entry.Key.CurrentHealth != entry.Value - decrease) wrong++;
        }
        Ok(checkedPlants >= 4 && wrong == 0, $"{checkedPlants} sağ kalan bitkinin gerçek canı = önceki can − izdeki toplam can azalması");
        Ok(record.Hits.Any(h => h.Kind == "direct") && record.Hits.Any(h => h.Kind == "davranış") && record.Hits.Any(h => h.Kind == "artçı"),
            "iz doğrudan → davranış → artçı zincirini türleriyle ayırdı: " + string.Join(", ", record.Hits.Select(h => h.Kind).Distinct()));
        TraceHit direct = record.Hits.First(h => h.Kind == "direct"), behavior = record.Hits.First(h => h.Kind == "davranış"), echo = record.Hits.First(h => h.Kind == "artçı");
        Ok(direct.HasDirect && direct.Calculation.Count >= 3 && behavior.Emission != null && behavior.Calculation.Any(l => l.Contains("Taban")) && echo.Calculation.Any(l => l.Contains("İkinci darbe")),
            "her hasar türü kendi gerçek hesap yolunu yazıyor (doğrudan: sapma ve yuvarlama; davranış: taban ve çarpanlar; artçı: ilk darbenin oranı)");
        Ok(direct.Overkill == direct.Computed - 1 && direct.Killed && direct.Decrease == 1, $"1 canlı bitkiye {direct.Computed} hasar: gerçek can azalması 1, taşan {direct.Overkill}");

        Economy now = Economy.Read();
        LabReport.EconomySums sums = LabReport.Sum(record);
        Ok(record.Harvests.Count == harvestsReal && harvestsReal >= 1, $"izdeki hasat sayısı = gerçek hasat sayısı ({harvestsReal})");
        Ok(record.Harvests.All(h => h.HasReward && h.HasScore), "her hasadın kaynak, XP ve skor hesabı kayıtta");
        Ok(sums.Gold == now.Gold - economyAtStart.Gold && sums.Iron == now.Iron - economyAtStart.Iron && sums.Stone == now.Stone - economyAtStart.Stone,
            $"kayıtlı kaynak toplamı = gerçek bakiye farkı (altın {sums.Gold}, demir {sums.Iron}, taş {sums.Stone})");
        Ok(Math.Abs(sums.Xp - (now.XpEarned - economyAtStart.XpEarned)) < 0.01 && sums.Score == now.Score - economyAtStart.Score,
            $"kayıtlı XP ve Harvest Score = gerçek artış (XP {sums.Xp:0.##}, skor {sums.Score})");
        Ok(record.After.Gold == now.Gold && record.Before.Gold == economyAtStart.Gold && Math.Abs(record.After.XpEarned - now.XpEarned) < 0.01, "izdeki önce / sonra bakiyeleri gerçek bakiyelerle aynı");
        string text = LabReport.TraceText(record);
        Ok(text.Contains("TAMAMLANDI") && text.Contains("EŞLEŞİYOR") && !text.Contains("FARKLI (") && text.Contains("Saklı XP"), "iz metni: tamamlandı, bakiyeler eşleşiyor, kazanılan ve saklı XP ayrı");
        return 0;
    }

    static double PendingStart()
    {
        TestTrace.Arm();
        Must(Attack(C, C).Ok, "attack tornado planter");
        return 0;
    }

    static double PendingOpen()
    {
        Ok(TornadoManager.Instance.ActiveCount > 0 && TestTrace.Current != null && TestTrace.PendingWork().Contains("kasırga"),
            "hareketli davranış sürerken iz AÇIK kaldı (bekleyen: " + TestTrace.PendingWork() + ")");
        return 0;
    }

    static double PendingDone()
    {
        TraceRecord record = TestTrace.Last;
        Ok(record != null && record.Complete && TornadoManager.Instance.ActiveCount == 0 && record.EndTime - record.StartTime > 0.5d, $"kasırga bitince iz tamamlandı ({record.EndTime - record.StartTime:0.00} sn)");
        return 0;
    }

    static double Truncated()
    {
        ScenarioRunner.ApplyPlants(TestSession.Current, new List<Result>());
        TestTrace.Arm();
        Must(Attack(C, C).Ok, "attack again");
        Must(TestTrace.Current != null, "recording");
        TestTrace.Cancel();
        TraceRecord record = TestTrace.Last;
        Ok(record.Truncated && !record.Complete && record.TruncatedReason.Length > 0 && LabReport.TraceText(record).Contains("KAYIT KESİLDİ, SONUÇ EKSİK") && LabReport.AnyTruncated(),
            "durdurulan kayıt \"kesildi, sonuç eksik\" diye işaretlendi: " + record.TruncatedReason);
        Ok(TestTrace.Current == null && !TestTrace.Armed, "durdurunca iz boşta");
        return 0;
    }

    // ---------------------------------------------------------------- kayıt seviyeleri
    static int logCount;

    static double LevelOff()
    {
        TestTrace.Reset(true);
        TestLog.SetLevel(TestLog.LogLevel.Off);
        TestLog.Clear();
        Ok(!TestLog.Enabled && !EditorNumericTrace.Listening, "kayıt kapalı ve iz boşta: Editor gözlem noktalarında dinleyici yok (çağıran yer hiçbir şey kurmaz)");
        Must(Attack(C, C).Ok, "attack with log off");
        TestLog.Note(TestLog.Kind.Tool, "kapalıyken yazılmamalı");
        Ok(TestLog.Count == 0 && TestLog.Total == 0, "kapalıyken saldırı ve komut kayda yazılmadı");
        TestLog.SetLevel(TestLog.LogLevel.Summary);
        ScenarioRunner.ApplyPlants(TestSession.Current, new List<Result>());
        Must(Attack(C, C).Ok, "attack with summary");
        return 0;
    }

    static double LevelSummary()
    {
        Ok(TestLog.Level == TestLog.LogLevel.Summary && Log(TestLog.Kind.Attack).Count >= 1, "işlem özeti: saldırı özeti yazıldı");
        Ok(Log(TestLog.Kind.All, "hesap:").Count == 0 && !EditorNumericTrace.Listening, "işlem özeti: vuruşun hesap satırı yok, sayısal gözlem noktası dinlenmiyor");
        logCount = TestLog.Count;
        TestLog.SetLevel(TestLog.LogLevel.Detailed);
        ScenarioRunner.ApplyPlants(TestSession.Current, new List<Result>());
        Must(Attack(C, C).Ok, "attack with detail");
        return 0;
    }

    static double LevelDetailed() => 0;

    static double LevelDetailedResult()
    {
        Ok(Log(TestLog.Kind.All, "hesap:").Count >= 1 && EditorNumericTrace.Listening && TestLog.Count > logCount, "ayrıntılı sayısal iz: doğrudan vuruşun hesap satırı yazıldı");
        Ok(TestLog.Count <= TestLog.Capacity && TestLog.Capacity == 2000 && TestTrace.MaxEvents == 400 && TestTrace.Kept == 20 && TestRewards.KeptReceipts == 30, "tamponlar sınırlı (olay 2000, iz 400 olay × 20, makbuz 30)");
        return 0;
    }

    // ---------------------------------------------------------------- buff yaşam döngüsü
    static double reactorChance;

    static double ReactorAdd()
    {
        TestLog.SetLevel(TestLog.LogLevel.Summary);
        RareReactorEffect reactor = RunEffects.Instance.Find<RareReactorEffect>();
        Must(reactor != null && !reactor.IsActive, "reactor installed, idle");
        reactorChance = Planter(C - 1, C).GetFinalStat(StatType.ExplosionChance);
        Must(reactorChance > 0d, "explosive planter has chance");
        Must(Attack(C, C).Ok, "harvest rare");
        Must(reactor.IsActive, "reactor active after direct rare harvest");
        return 0;
    }

    static double ReactorRefresh()
    {
        RareReactorEffect reactor = RunEffects.Instance.Find<RareReactorEffect>();
        Ok(Log(TestLog.Kind.Buff, "Nadir Reaktör EKLENDİ").Count == 1, "buff kaydı: Nadir Reaktör EKLENDİ (bir kez)");
        Ok(BuffWatch.ReactorModifiers(out float bonus) == 4 && Math.Abs(Planter(C - 1, C).GetFinalStat(StatType.ExplosionChance) - Math.Min(1d, reactorChance * (1d + bonus))) < 1e-4,
            "aktifken dört şans çarpanı var; tile'lı saksının şansı ×(1 + bonus)");
        var sources = new List<StatSource>();
        StatSources.Collect(StatType.ExplosionChance, StatTarget.Planter, Planter(C - 1, C), sources);
        Ok(sources.Any(s => s.Kind == SourceKind.Temporary && s.Remaining > 0f), "kaynak dökümü: geçici etki, kalan süresiyle");
        string rare = TestSession.Current.plants.First(p => !string.IsNullOrEmpty(p.plant)).plant;
        Must(TestField.SetPlant(P(C, C), LabCatalog.Plant(rare)).Ok, "respawn rare");
        TestField.SetPlantHealth(P(C, C), 1);
        Must(Attack(C, C).Ok, "second rare harvest");
        return 0;
    }

    static double ReactorExpire()
    {
        RareReactorEffect reactor = RunEffects.Instance.Find<RareReactorEffect>();
        if (reactor.IsActive) return Again;
        if (Frames(3) != 0) return Again;
        Ok(Log(TestLog.Kind.Buff, "YENİLENDİ").Count == 1 && Log(TestLog.Kind.Buff, "çarpan katlanmadı").Count == 1 && Log(TestLog.Kind.Buff, "Nadir Reaktör EKLENDİ").Count == 1,
            "buff kaydı: süre YENİLENDİ, çarpan katlanmadı (ikinci kez \"eklendi\" yazılmadı)");
        Ok(Log(TestLog.Kind.Buff, "süresi BİTTİ").Count == 1 && BuffWatch.ReactorModifiers(out _) == 0 && Math.Abs(Planter(C - 1, C).GetFinalStat(StatType.ExplosionChance) - reactorChance) < 1e-5,
            "buff kaydı: süresi BİTTİ; şans eski değerinde, geride çarpan yok");
        return 0;
    }

    static double RhythmReady()
    {
        Ok(Log(TestLog.Kind.Buff, "Hasat Ritmi koşullu etkisi KURULDU").Count == 1 && Log(TestLog.Kind.Buff, "HAZIRLANDI").Count == 0 && RunPower.Rhythm.Enabled,
            "buff kaydı: Hasat Ritmi koşullu etkisi KURULDU (bir kez); henüz hak yok");
        Must(Attack(C, C).Ok && RunPower.Rhythm.Ready, "first attack prepares the charge");
        return 0;
    }

    static double RhythmSpent()
    {
        ScenarioRunner.ApplyPlants(TestSession.Current, new List<Result>());
        Must(Attack(C, C).Ok && !RunPower.Rhythm.Ready, "second attack spends the charge");
        return 0;
    }

    static double RhythmLogged()
    {
        Ok(Log(TestLog.Kind.Buff, "Hasat Ritmi hakkı HAZIRLANDI").Count == 1 && Log(TestLog.Kind.Buff, "Hasat Ritmi hakkı HARCANDI").Count == 1, "buff kaydı: Hasat Ritmi hakkı HAZIRLANDI → HARCANDI");
        return 0;
    }

    static string resonanceBefore;
    static PlanterBrain resonant;

    static double ResonanceStart()
    {
        var planters = new List<PlanterBrain>(); TestField.Planters(planters);
        resonant = planters.FirstOrDefault(p => p.ActiveResonances.Count > 0);
        Must(resonant != null, "preset 22 has a planter with an active resonance");
        PlanterBrain brain = resonant;
        resonanceBefore = string.Join(",", brain.ActiveResonances.Select(r => r.resonanceName));
        int contributions = TestStats.Contributions.Count;
        Must(contributions > 0, "preset 22 has test contributions");
        // Şart bozulur: rezonansı kuran tile'lar kaldırılır.
        foreach (GridObject grid in brain.OccupiedGrids) Must(TestField.ClearTile(grid.GetGridPosition()).Ok, "clear tile");
        float before = Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player);
        Must(TestStats.Clear().Ok, "clear test contributions");
        Ok(TestStats.Contributions.Count == 0 && Stats.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) < before && Boss.Stacks(Reward("keskin_bicak")) == 1,
            "test katkısı sıfırlandı: stat geri döndü, boss ödülü yerinde");
        return 0;
    }

    static double ResonanceResult()
    {
        PlanterBrain brain = resonant;
        Ok(brain.ActiveResonances.Count == 0 && Log(TestLog.Kind.Buff, "Rezonans saksı").Count >= 1 && Log(TestLog.Kind.Buff, "şart bozuldu").Count >= 1,
            $"şart bozulunca rezonans ({resonanceBefore}) kalktı ve kayda yazıldı");
        var text = new StringBuilder();
        BuffWatch.Describe(text);
        Ok(!text.ToString().Contains("Test katkısı"), "aktif etki özeti artık test katkısı göstermiyor");
        return 0;
    }

    // ---------------------------------------------------------------- HUD
    static double HudHide()
    {
        QuotaHUD quota = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        Must(quota != null, "QuotaHUD exists in GameScene");
        Ok(quota.GetComponent<Canvas>() == null, "başlangıçta HUD nesnesinde ek Canvas yok");
        Result hidden = TestHud.SetHidden(TestHud.Part.Quota, true);
        Canvas canvas = quota.GetComponent<Canvas>();
        Ok(hidden.Ok && canvas != null && !canvas.enabled && quota.gameObject.activeInHierarchy && quota.enabled, "kota HUD'u gizlendi: yalnız çizim kapalı; GameObject ve script etkin — " + TestHud.LastReport);
        Result counters = TestHud.SetHidden(TestHud.Part.Counters, true), fieldEvent = TestHud.SetHidden(TestHud.Part.FieldEvent, true);
        Note("HUD · göstergeler: " + counters.Message);
        Note("HUD · tarla olayı: " + fieldEvent.Message);
        Must(Attack(C, C).Ok, "attack while HUD hidden");
        return 0;
    }

    static double HudHidden()
    {
        Ok(Rounds.IsRoundActive && RoundManager.Instance != null && HarvestScoreManager.Instance != null && VFXManager.Instance != null && VFXManager.Instance.isActiveAndEnabled,
            "HUD gizliyken round, skor ve VFX yöneticileri çalışıyor (kök nesne kapatılmadı)");
        Ok(TestHud.IsHidden(TestHud.Part.Quota) && TestHud.HiddenCount >= 1, $"gizli öğe sayısı {TestHud.HiddenCount}");
        return 0;
    }

    static double HudShow()
    {
        QuotaHUD quota = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        Canvas canvas = quota != null ? quota.GetComponent<Canvas>() : null;
        Ok(quota != null && canvas != null && !canvas.enabled && TestHud.IsHidden(TestHud.Part.Quota), "yeniden kurulumda (yeni sahne, yeni HUD nesneleri) gizleme yeniden uygulandı");
        foreach ((TestHud.Part part, string _, string _) in TestHud.Parts) Must(TestHud.SetHidden(part, false).Ok, "show " + part);
        Ok(TestHud.HiddenCount == 0 && !TestHud.IsHidden(TestHud.Part.Quota), "\"göster\": gizli öğe kaydı kalmadı");
        return 0;
    }

    // Eklenen Canvas'ı Unity kare sonunda kaldırır: sonuç bir kare sonra okunur.
    static double HudShown()
    {
        QuotaHUD quota = Object.FindFirstObjectByType<QuotaHUD>(FindObjectsInactive.Include);
        RoundUI round = Object.FindFirstObjectByType<RoundUI>(FindObjectsInactive.Include);
        var graphic = quota.GetComponentInChildren<UnityEngine.UI.Graphic>(true);
        Ok(quota.GetComponent<Canvas>() == null && round.GetComponent<Canvas>() == null && Object.FindFirstObjectByType<FieldEventHud>(FindObjectsInactive.Include).GetComponent<Canvas>() == null,
            "\"göster\" eklenen Canvas'ları kaldırdı: HUD nesneleri eski hâlinde");
        Ok(graphic != null && graphic.canvas != null && graphic.canvas.enabled && graphic.canvas.gameObject != quota.gameObject, "kota HUD'unun çizimi yeniden oyunun kendi Canvas'ında ve açık");
        return 0;
    }

    // ---------------------------------------------------------------- şans
    static ChanceSample first;

    static double ChanceStart()
    {
        Result refused = ChanceSampler.Run(P(0, 0), DamageType.Explosion, 50, 1);
        Result noChance = ChanceSampler.Run(P(C, C), DamageType.Electric, 50, 1);
        Ok(!refused.Ok && !noChance.Ok && ChanceSampler.Samples.Count == 0, "koşul sağlanmadan örnekleme yapılmadı: " + noChance.Message);
        Must(ChanceSampler.Run(P(C, C), DamageType.Explosion, 400, 2024).Ok, "start sample");
        return 0;
    }

    static double ChanceAgain()
    {
        first = ChanceSampler.Samples.Last();
        Must(first.Complete, "first sample complete");
        Must(ChanceSampler.Run(P(C, C), DamageType.Explosion, 400, 2024).Ok, "start same seed");
        return 0;
    }

    static double ChanceResult()
    {
        ChanceSample second = ChanceSampler.Samples.Last();
        double chance = Planter(C, C).GetFinalStat(StatType.ExplosionChance);
        Ok(first.Trials == 400 && first.Opportunities == 400 && first.NoOpportunity == 0, "400 deneme = 400 gerçek fırsat (her denemede gerçek doğrudan hasat)");
        Ok(Math.Abs(first.Expected - chance) < 1e-6 && !first.ExpectedMoved && chance > 0d && chance < 1d, $"beklenen şans gerçek hesabın kullandığı değer: {chance * 100d:0.##}% (test şans katkısı yok)");
        Ok(first.Started == first.Rolled && first.Rejected == 0 && first.Rolled > 0 && first.Rolled < 400, $"patlama: başarılı zar {first.Rolled} = başlatılan {first.Started} (kapasite sınırı yok)");
        Ok(first.Low <= first.Observed && first.Observed <= first.High && first.Low < first.High, $"gözlenen oran {first.Observed * 100d:0.##}% · %95 aralık {first.Low * 100d:0.#}–{first.High * 100d:0.#}%");
        Note($"şans örneği: beklenen {first.Expected * 100d:0.##}% aralığın {(first.Inside ? "içinde" : "DIŞINDA")} · {first.Verdict()}");
        Ok(second.Rolled == first.Rolled && second.Opportunities == first.Opportunities, $"aynı seed, aynı koşul → aynı sonuç ({second.Rolled} = {first.Rolled}): seed gerçekten zar akışını belirliyor");
        Ok(first.ToText().Contains("Gerçek fırsat sayısı") && first.ToText().Contains("Başarılı zar sayısı") && first.ToText().Contains("Gerçekten başlatılan"), "örnek metni istenen sayıları ayrı ayrı yazıyor");
        var small = new ChanceSample { Opportunities = 20, Expected = 0.1, Rolled = 2 };
        Ok(small.SmallSample && small.Verdict().Contains("KÜÇÜK ÖRNEK") && !first.SmallSample, "küçük örnek ayrıca işaretlenir (kesin PASS / FAIL gibi sunulmaz)");
        return 0;
    }

    static double ChanceTornado()
    {
        Must(ChanceSampler.Run(P(C, C), DamageType.Tornado, 300, 7).Ok, "start tornado sample");
        return 0;
    }

    static double ChanceTornadoResult()
    {
        ChanceSample sample = ChanceSampler.Samples.Last();
        Must(sample.Complete, "tornado sample complete");
        Ok(sample.Started == sample.Rolled - sample.Rejected && sample.Rolled > 0, $"kasırga: fırsat {sample.Opportunities} · başarılı zar {sample.Rolled} · başlatılan {sample.Started} · kapasite reddi {sample.Rejected}");
        Ok(TornadoManager.Instance.ActiveCount <= TornadoManager.Instance.ActiveLimit, "örnek boyunca aktif kasırga sınırı aşılmadı");
        Note(sample.Rejected > 0 ? "kasırga örneğinde kapasite reddi gözlendi: \"zar başarılı\" ≠ \"davranış başlatıldı\"" : "kasırga örneğinde kapasite reddi oluşmadı (sınır dolmadı)");
        Ok(!TestAttack.AutoAttack && TestAttack.ProductionPaused, "örnek bitince otomatik saldırı ve üretim ayarı önceki hâline döndü");
        return 0;
    }

    // ---------------------------------------------------------------- kayıt açık / kapalı: aynı oynanış sonucu
    static readonly Dictionary<string, string> fingerprints = new();
    static readonly int[] behaviorBase = new int[8];
    static readonly DamageType[] Behaviors = { DamageType.Explosion, DamageType.Electric, DamageType.Tornado, DamageType.Boomerang };

    static Scenario Dense()
    {
        Scenario s = ScenarioPresets.Manual(ScenarioPresets.Field(ScenarioPresets.New("Doğrulama · yoğun düzen", "", "", "", 5), 2), C, C);
        ScenarioPresets.Radius(s, ScenarioPresets.ThreeByThree);
        TileModifierType[] tiles = { TileModifierType.Explosive, TileModifierType.Electric, TileModifierType.Tornado, TileModifierType.Boomerang, TileModifierType.Duplicate };
        for (int x = C - 2; x <= C + 2; x++)
            for (int z = C - 2; z <= C + 2; z++) ScenarioPresets.Tile(s, x, z, tiles[(x * 3 + z) % tiles.Length], TileRarity.Legendary);
        foreach (StatType chance in new[] { StatType.ExplosionChance, StatType.ElectricChance, StatType.TornadoChance, StatType.BoomerangChance })
            ScenarioPresets.Stat(s, chance, StatTarget.Planter, ModifierOperation.MorePercent, 1.5f);
        ScenarioPresets.Stat(s, StatType.CritChance, StatTarget.Player, ModifierOperation.Set, 0.5f);
        foreach (string id in new[] { "zincir_hasat", "artci_patlama", "cifte_akim", "kritik_ark", "tasan_hasar", "keskin_bicak", "asiri_yukleme", "nadir_reaktor" }) ScenarioPresets.Reward(s, id);
        s.plantFill = ScenarioPresets.CommonPlant();
        s.plantHealth = 3;
        s.pauseProduction = true;
        return s;
    }

    static void AddDeterminism(string name, TestLog.LogLevel level, bool observe)
    {
        Add("eşdeğerlik " + name + ": seviye", () => { TestTrace.Reset(true); TestLog.SetLevel(level); return 0; });
        AddRebuild("eşdeğerlik " + name, Dense);
        (int x, int z)[] aims = { (C, C), (C - 1, C + 1), (C + 1, C - 1), (C, C) };
        for (int i = 0; i < aims.Length; i++)
        {
            int index = i;
            Add($"eşdeğerlik {name}: saldırı {i + 1}", () =>
            {
                if (index == 0)
                {
                    UnityEngine.Random.InitState(4242);
                    for (int b = 0; b < Behaviors.Length; b++) behaviorBase[b] = HarvestBehaviorStats.Triggered(Behaviors[b]) * 1000 + HarvestBehaviorStats.Skipped(Behaviors[b]);
                }
                else ScenarioRunner.ApplyPlants(TestSession.Current, new List<Result>());
                if (observe)
                {
                    Comparison.TakeBefore(P(C, C));
                    TestTrace.Arm();
                }
                Must(Attack(aims[index].x, aims[index].z).Ok, "scripted attack");
                if (observe) Comparison.TakeAfter(false);
                return 0;
            });
            AddFrames(75);
        }
        Add("eşdeğerlik " + name + ": parmak izi", () =>
        {
            var text = new StringBuilder();
            Economy e = Economy.Read();
            text.Append($"xp {e.XpEarned:0.###}/{e.XpStored:0.###} lvl {e.Level} pend {e.PendingChoices} gold {e.Gold} iron {e.Iron} stone {e.Stone} score {e.Score} | ");
            for (int b = 0; b < Behaviors.Length; b++)
                text.Append(Behaviors[b]).Append(' ').Append(HarvestBehaviorStats.Triggered(Behaviors[b]) * 1000 + HarvestBehaviorStats.Skipped(Behaviors[b]) - behaviorBase[b]).Append(' ');
            text.Append("| ");
            for (int x = C - 2; x <= C + 2; x++)
                for (int z = C - 2; z <= C + 2; z++) { PlantHealth plant = Plant(x, z); text.Append(plant != null ? plant.CurrentHealth.ToString() : "-").Append(','); }
            RunEffects effects = RunEffects.Instance;
            text.Append(" | effects ");
            foreach (RewardSource source in Enum.GetValues(typeof(RewardSource))) text.Append(effects.Hits(source)).Append('/').Append(effects.Kills(source)).Append(' ');
            text.Append("| rng ").Append(JsonUtility.ToJson(UnityEngine.Random.state));
            fingerprints[name] = text.ToString();
            if (observe)
            {
                // Hareketli davranışlar sürdüğü için ilk iz hâlâ açık olabilir: kapanmış ya da açık en az bir iz vuruş kaydetmiş olmalı.
                int traced = TestTrace.Records.Sum(r => r.Hits.Count) + (TestTrace.Current != null ? TestTrace.Current.Hits.Count : 0);
                Must(traced >= 1 && Log(TestLog.Kind.All, "hesap:").Count >= 1 && Comparison.After != null, $"observation really ran in the observed run (traced hits {traced}, log {TestLog.Count})");
                Note($"gözlemli koşu: izlenen vuruş {traced} ({TestTrace.Records.Count} kapanmış iz{(TestTrace.Current != null ? " + 1 açık iz" : "")}), {TestLog.Count} kayıt satırı, {TestRewards.Receipts.Count} makbuz");
            }
            return 0;
        });
    }

    static double DeterminismResult()
    {
        string[] keys = fingerprints.Keys.ToArray();
        Must(keys.Length == 3, "three fingerprints");
        string a = fingerprints[keys[0]], b = fingerprints[keys[1]], c = fingerprints[keys[2]];
        Note("parmak izi A: " + a);
        if (a != b) Note("parmak izi B: " + b);
        if (a != c) Note("parmak izi C: " + c);
        Ok(a.Contains("rng {") && !a.Contains("score 0 "), "senaryo gerçekten oynandı (skor kazanıldı, zar akışı okundu)");
        Ok(a == b, "taban: kayıt kapalı iki koşu birebir aynı (seed'li senaryo tekrarlanabilir)");
        Ok(a == c, "kayıt kapalı ile ayrıntılı iz + saldırı izi + görüntü açıkken OYNANIŞ SONUCU AYNI: bakiyeler, bitki canları, davranış sayaçları, ödül vuruşları ve oyunun rastgele akışının son durumu (kayıt ek zar atmıyor)");
        TestLog.SetLevel(TestLog.LogLevel.Detailed);
        return 0;
    }

    // ---------------------------------------------------------------- katalog
    static bool catalogStarted;
    static int catalogFails;
    static string catalogSummary = "not run";
    static List<CheckDefinition> selectedChecks;

    static double SelectedResult()
    {
        catalogSummary = NumericChecks.Summary();
        File.WriteAllText(ChecksFile, catalogSummary + "\n\n" + NumericChecks.AllText());
        foreach (CheckResult result in NumericChecks.Results)
        {
            Note($"[{NumericChecks.StatusText(result.Status)}] {result.Id} · {result.Title}");
            foreach (CheckLine line in result.Lines) Note($"      [{NumericChecks.StatusText(line.Status)}] {line.Name} | beklenen {line.Expected} | gerçekleşen {line.Actual} | tolerans {line.Tolerance}");
        }
        catalogFails = NumericChecks.Results.Count(r => r.Status == CheckStatus.Fail);
        Ok(NumericChecks.Results.Count == selectedChecks.Count, $"seçilen {selectedChecks.Count} kontrol işlendi");
        return 0;
    }

    static double CatalogRun()
    {
        if (!catalogStarted)
        {
            catalogStarted = true;
            NumericChecks.Clear();
            Result started = NumericChecks.Run(selectedChecks ?? (IReadOnlyList<CheckDefinition>)NumericChecks.Catalog);
            Must(started.Ok, "catalog start: " + started.Message);
            return Again;
        }
        if (NumericChecks.Running) return Again;
        return 0;
    }

    static double CatalogResult()
    {
        IReadOnlyList<CheckResult> results = NumericChecks.Results;
        catalogSummary = NumericChecks.Summary();
        File.WriteAllText(ChecksFile, catalogSummary + "\n\n" + NumericChecks.AllText() + "\n" + NumericChecks.Matrix());
        Ok(results.Count == NumericChecks.Catalog.Count, $"kataloğun {NumericChecks.Catalog.Count} kontrolünün hepsi işlendi");
        Ok(results.All(r => r.Status != CheckStatus.Running && r.Status != CheckStatus.NotRun), "yarım kalan kontrol yok");
        Ok(results.Where(r => r.Coverage == CheckCoverage.Missing).All(r => r.Status == CheckStatus.Missing) && results.Any(r => r.Status == CheckStatus.Missing),
            "final / endless gibi olmayan sistemler \"sistem henüz yok\" olarak işaretli (PASS değil)");
        Ok(results.Where(r => r.Status == CheckStatus.Pass).All(r => r.Lines.Any(l => l.Status == CheckStatus.Pass) && r.Lines.All(l => l.Status != CheckStatus.Fail && l.Status != CheckStatus.NotChecked)),
            "PASS yalnız en az bir karşılaştırması geçen ve FAIL / İNCELENMEDİ satırı olmayan kontrollere verildi");
        var rewards = new List<BossRewardSO>(); TestRewards.Catalog(rewards);
        var uncovered = rewards.Where(r => !CatalogCovers(r)).Select(r => r.id).ToList();
        Ok(uncovered.Count == 0, $"kontrol matrisi ödül kataloğunun tamamını kapsıyor ({rewards.Count} ödül)" + (uncovered.Count > 0 ? " — eksik: " + string.Join(", ", uncovered) : ""));
        foreach (CheckResult result in results)
        {
            Note($"[{NumericChecks.StatusText(result.Status)}] {result.Id} · {result.Group} · {result.Title} ({NumericChecks.CoverageText(result.Coverage)})");
            foreach (CheckLine line in result.Lines)
                if (line.Status == CheckStatus.Fail || line.Status == CheckStatus.NotChecked)
                    Note($"      [{NumericChecks.StatusText(line.Status)}] {line.Name} | beklenen {line.Expected} | gerçekleşen {line.Actual} | tolerans {line.Tolerance}");
        }
        catalogFails = results.Count(r => r.Status == CheckStatus.Fail);
        Note(catalogSummary);
        Ok(TestSession.Current.name.StartsWith("Doğrulama · temel") && TestRewards.Receipts.Count == 0 && TestStats.Contributions.Count == TestSession.Current.stats.Count,
            "kontroller bitince önceki senaryo temiz oturuma geri kuruldu (kontrolün ödülü / statı kalmadı)");
        return 0;
    }

    // Ödül kimliği → onu doğrulayan kontrol (katalogdaki kontrol gövdeleri bu kimliklerle çalışır).
    static readonly Dictionary<string, string> RewardChecks = new()
    {
        { "hizli_bilek", "S01" }, { "firtina_bilegi", "S02" }, { "genis_savurus", "S03" }, { "bilgi_filizi", "S04" }, { "agir_darbe", "S05" }, { "kritik_goz", "S06" }, { "kivilcim", "S07" },
        { "nadir_tohum", "S08" }, { "bereketli_toprak", "S09" }, { "keskin_bicak", "D03" }, { "canavar_kesimi", "D04" }, { "altin_hedef", "D05" }, { "bereketli_ogrenim", "D06" },
        { "yikim_gucu", "B02" }, { "davranisa_adanis", "B03" }, { "artci_patlama", "K01" }, { "cifte_akim", "K02" }, { "hasat_ritmi", "K03" }, { "zincir_hasat", "K04" },
        { "kritik_ark", "E01" }, { "tasan_hasar", "E02" }, { "hasat_yankisi", "E03" }, { "nadir_reaktor", "E04" }, { "acgozlu_hasat", "E05" }, { "kritik_patlama", "E06" }, { "asiri_yukleme", "E07" },
        { "elektrik_anteni", "A01" }, { "iletken_hat", "A02" }, { "kapali_devre", "A03" }, { "hilal_darbesi", "H01" }, { "buyuyen_ay", "H02" }, { "dolunay", "H03" },
    };

    static bool CatalogCovers(BossRewardSO reward) =>
        RewardChecks.TryGetValue(reward.id, out string id) && NumericChecks.Catalog.Any(d => d.Id == id) && NumericChecks.ResultOf(id) != null;

    // ---------------------------------------------------------------- dışa aktarım
    static double Export()
    {
        Result toSave = LabReport.Export(Application.persistentDataPath);
        Result toAssets = LabReport.Export(Path.Combine(Application.dataPath, "Editor"));
        Ok(!toSave.Ok && !toAssets.Ok, "gerçek oyuncu kaydının klasörüne ve Assets'e rapor yazılmadı: " + toSave.Message);
        Must(TestRewards.Grant(Reward("keskin_bicak")).Ok && Comparison.TakeBefore(P(C, C)).Ok, "something to report");
        Must(TestStats.Add(Mod(StatType.CritChance, StatTarget.Player, ModifierOperation.Flat, 0.1f)).Ok && Comparison.TakeAfter(true).Ok, "comparison to report");
        string folder = Path.GetFullPath(ExportFolder);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Result exported = LabReport.Export(folder);
        Ok(exported.Ok && Directory.Exists(LabReport.LastExportFolder), exported.Message);
        string target = LabReport.LastExportFolder;
        string[] expected = { "rapor.txt", "rapor.json", "once-sonra.csv", "saldiri-izi-vuruslar.csv", "saldiri-izi-hasatlar.csv", "kontroller.csv", "sans.csv" };
        Ok(expected.All(f => File.Exists(Path.Combine(target, f)) && new FileInfo(Path.Combine(target, f)).Length > 0), "rapor klasöründe yedi dosya var: " + string.Join(", ", expected));
        string text = File.ReadAllText(Path.Combine(target, "rapor.txt")), json = File.ReadAllText(Path.Combine(target, "rapor.json"));
        Ok(text.Contains("Senaryo: Doğrulama · temel") && text.Contains("Profil: " + Lab.DefaultProfile) && text.Contains("seed") && text.Contains("Aktif ödüller: Keskin Bıçak ×1") &&
           text.Contains("Test katkıları:") && text.Contains("Başlangıç koşulları:") && text.Contains("Kayıt kesildi mi:"), "rapor başlığı: senaryo, profil + seed, round, aktif ödüller, test katkıları, başlangıç koşulları, kesilme bilgisi");
        Ok(text.Contains("=== BEKLENEN / GERÇEKLEŞEN KONTROLLER ===") && text.Contains("=== ŞANS İSTATİSTİKLERİ ===") && text.Contains("=== ÖDÜL MAKBUZLARI ===") && text.Contains("ÖDÜL MAKBUZU · Keskin Bıçak"),
            "okunabilir rapor bütün bölümleri içeriyor");
        Ok(json.Contains("\"scenario\"") && json.Contains("\"checks\"") && json.Contains("\"chance\"") && json.Contains("\"receipts\"") && json.TrimStart().StartsWith("{") && JsonUtility.FromJson<JsonProbe>(json).tool == "Mechanics Test Lab",
            "JSON raporu okunabiliyor ve aynı bölümleri taşıyor");
        string checksCsv = File.ReadAllText(Path.Combine(target, "kontroller.csv"));
        Ok(checksCsv.Split('\n').Length > NumericChecks.Catalog.Count && checksCsv.Contains("D01"), "kontrol tablosu CSV'de");
        Ok(SaveStamp() == SessionState.GetString(Key + ".save", "?"), "dışa aktarım gerçek kayıt dosyasına dokunmadı");
        Result console = LabReport.LastToConsole();
        Ok(console.Ok && LabReport.LastTitle.Length > 0, "\"Son işlemi Console'a yaz\" çalıştı: " + LabReport.LastTitle);
        return 0;
    }

    [Serializable] sealed class JsonProbe { public string tool; }

    // ---------------------------------------------------------------- sızıntı
    static double LeakRebuild()
    {
        // Kirli durum: açık iz, ayrıntılı kayıt, alınmış görüntü, gizli HUD.
        TestLog.SetLevel(TestLog.LogLevel.Detailed);
        Comparison.TakeBefore(P(C, C));
        TestTrace.Arm();
        Must(Attack(C, C).Ok && TestTrace.Current != null, "open trace before rebuild");
        Must(TestSession.Rebuild(TraceScenario()).Ok, "rebuild with open trace");
        return 0;
    }

    static double LeakResult()
    {
        Ok(TestTrace.Current == null && !TestTrace.Armed && TestTrace.Records.Count >= 3 && TestTrace.Records.Skip(TestTrace.Records.Count - 3).All(r => r.Truncated),
            "yeniden kurulumda açık izler \"kesildi\" diye kapandı; yeni oturuma açık iz taşınmadı");
        Ok(Comparison.Before == null && Comparison.Rows.Count == 0, "önceki oturumun görüntüsü yeni oturuma taşınmadı");
        Ok(TestRewards.Receipts.Count == TestSession.Current.rewards.Count && TestStats.Contributions.Count == TestSession.Current.stats.Count, "makbuz ve test katkısı yalnız yeni senaryonunkiler");
        Ok(BuffWatch.ReactorModifiers(out _) == 0 && !RunPower.Rhythm.Enabled && RunEffects.Instance.Active.Count == 0 && Boss.Taken.Count == 1, "önceki oturumların ödül / buff etkisi kalmadı");
        // Çift abonelik olsaydı: kayıt ve iz kapatıldığında dinleyici kalırdı.
        Must(Attack(C, C).Ok, "attack after rebuilds");
        int hesap = Log(TestLog.Kind.All, "hesap:").Count;
        Ok(hesap == 1, $"üç yeniden kurulumdan sonra tek saldırı tek hesap satırı yazdı ({hesap}): abonelik çoğalmadı");
        TestTrace.Reset(true);
        TestLog.SetLevel(TestLog.LogLevel.Off);
        Ok(!EditorNumericTrace.Listening, "kayıt ve iz kapatılınca Editor gözlem noktalarında dinleyici kalmadı");
        TestLog.SetLevel(TestLog.LogLevel.Detailed);
        return 0;
    }

    // ---------------------------------------------------------------- panel
    static double PanelTrace()
    {
        TestField.SetPlantHealth(P(C, C), 1);
        TestTrace.Arm();
        Must(Attack(C, C).Ok, "attack for the panel trace");
        return 0;
    }

    static double Panel()
    {
        int before = errors;
        // Panelde gösterilecek gerçek içerik: karşılaştırma, iz, makbuz.
        Must(Comparison.TakeBefore(P(C, C)).Ok, "panel before");
        TestRewards.Grant(Reward("keskin_bicak"));
        Must(Comparison.TakeAfter(true).Ok, "panel after");
        LabWindow window = EditorWindow.GetWindow<LabWindow>();
        window.position = new Rect(40f, 40f, 1180f, 900f);
        BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        typeof(LabWindow).GetField("hasSelection", flags).SetValue(window, true);
        typeof(LabWindow).GetField("anchor", flags).SetValue(window, P(C, C));
        typeof(LabWindow).GetField("corner", flags).SetValue(window, P(C, C));
        // Açılır bölümler de görünsün: bir kontrolün satırları ve ilk vuruşun hesabı.
        ((HashSet<string>)typeof(LabWindow).GetField("openChecks", flags).GetValue(window)).Add("D03");
        typeof(LabWindow).GetField("traceHit", flags).SetValue(window, 0);
        string drawn = "", shots = "";
        Directory.CreateDirectory(ShotFolder);
        try
        {
            for (int tab = 0; tab < 8; tab++)
            {
                typeof(LabWindow).GetField("tab", flags).SetValue(window, tab);
                window.Repaint();
                window.SendEvent(EditorGUIUtility.CommandEvent("MechanicsNumericSmoke"));
                drawn += tab + " ";
                if ((tab == 0 || tab == 5) && !Grab(window, Path.Combine(ShotFolder, $"sekme-{tab}.png"), out string reason)) Note($"panel görüntüsü alınamadı (sekme {tab}): {reason}");
            }
            typeof(LabWindow).GetField("tab", flags).SetValue(window, 7);
            for (int sub = 0; sub < 6; sub++)
            {
                typeof(LabWindow).GetField("numericTab", flags).SetValue(window, sub);
                window.Repaint();
                window.SendEvent(EditorGUIUtility.CommandEvent("MechanicsNumericSmoke"));
                drawn += "H" + sub + " ";
                if (Grab(window, Path.Combine(ShotFolder, $"sayisal-{sub}.png"), out string why)) shots += sub + " ";
                else Note($"panel görüntüsü alınamadı (alt sekme {sub}): {why}");
            }
            // Dar pencere: tablolar pencere genişliğine ölçeklenmeli (yatay kaydırma çıkmamalı).
            window.position = new Rect(40f, 40f, 760f, 900f);
            foreach (int sub in new[] { 0, 1, 3, 4 })
            {
                typeof(LabWindow).GetField("numericTab", flags).SetValue(window, sub);
                window.Repaint();
                window.SendEvent(EditorGUIUtility.CommandEvent("MechanicsNumericSmoke"));
                drawn += "N" + sub + " ";
                if (!Grab(window, Path.Combine(ShotFolder, $"dar-{sub}.png"), out string why)) Note($"panel görüntüsü alınamadı (dar, alt sekme {sub}): {why}");
            }
        }
        catch (Exception e) { Note("panel smoke could not send events in batch mode: " + e.GetType().Name + " " + e.Message); }
        window.Close();
        if (drawn.Trim().Split(' ').Length == 18 && errors == before) Ok(true, "panelin sekiz sekmesi ve 'H · Sayısal'ın altı alt sekmesi canlı oturumda, gerçek içerikle hata vermeden çizildi");
        else Note($"panel smoke incomplete: drawn [{drawn.Trim()}], new errors {errors - before} — panel drawing is NOT verified by this run");
        Note(shots.Length > 0 ? $"panel PNG'leri yazıldı ({ShotFolder}): alt sekmeler {shots.Trim()}" : "PANEL GÖRSELİ ALINAMADI: batch modda pencere pikselleri okunamıyor; görünüş bu koşuda incelenmedi");
        return 0;
    }

    // Editor penceresinin gerçek piksellerini okumayı dener (iç API: GUIView.GrabPixels). Olmazsa nedeni döner; uydurma görüntü üretmez.
    static bool Grab(EditorWindow window, string path, out string why)
    {
        why = "";
        try
        {
            object parent = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(window);
            if (parent == null) { why = "pencerenin görünümü yok (m_Parent null)"; return false; }
            MethodInfo grab = null, repaint = null;
            for (Type type = parent.GetType(); type != null && (grab == null || repaint == null); type = type.BaseType)
            {
                grab ??= type.GetMethod("GrabPixels", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                repaint ??= type.GetMethod("RepaintImmediately", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            }
            if (grab == null) { why = "GUIView.GrabPixels bulunamadı"; return false; }
            int width = Mathf.Max(64, (int)window.position.width), height = Mathf.Max(64, (int)window.position.height);
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            target.Create();
            repaint?.Invoke(parent, null);
            grab.Invoke(parent, new object[] { target, new Rect(0f, 0f, width, height) });
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            Color32[] pixels = image.GetPixels32();
            int different = 0;
            for (int i = 0; i < pixels.Length; i += 97)
                if (pixels[i].r != pixels[0].r || pixels[i].g != pixels[0].g || pixels[i].b != pixels[0].b) different++;
            // Üst üste çevrik gelebilir: dikey çevir.
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < height; y++) Array.Copy(pixels, y * width, flipped, (height - 1 - y) * width, width);
            image.SetPixels32(flipped);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
            if (different < 20) { why = "okunan görüntü tek renk (pencere batch modda çizilmedi)"; File.Delete(path); return false; }
            return true;
        }
        catch (Exception e)
        {
            why = (e is TargetInvocationException && e.InnerException != null ? e.InnerException : e).GetType().Name + ": " + (e.InnerException ?? e).Message;
            return false;
        }
    }

    // ---------------------------------------------------------------- Play'den sonra
    static void AfterPlay()
    {
        Ok(!TestSession.Active && !TestSession.Ready && !RunProfileSelectionSO.HasSessionOverride && TestSession.Profile == null, "oturum kapandı: profil geçersiz kılması kaldırıldı");
        Ok(!TestLog.Enabled && !EditorNumericTrace.Listening && TestTrace.Current == null && !TestTrace.Armed && TestTrace.Records.Count == 0 && !LabRoutine.Running && TestHud.HiddenCount == 0,
            "oturum bitince kayıt, iz, yürütücü ve HUD gizlemesi kapandı; gözlem noktalarında dinleyici yok");
        Ok(Time.timeScale == 1f && PlayerController.EditorAutoAttack && PlayerController.EditorAim == null && !PlantSpawner.EditorPauseProduction && !RoundManager.EditorFreezeTimer,
            "zaman ölçeği ve saldırı / üretim / sayaç bayrakları geri yüklendi");
        var selection = AssetDatabase.LoadAssetAtPath<RunProfileSelectionSO>(SelectionPath);
        Ok((selection != null && selection.active != null ? selection.active.name : "") == SessionState.GetString(Key + ".selection", "?"), "seçili run profili değişmedi: " + SessionState.GetString(Key + ".selection", "?"));
        Ok(SaveStamp() == SessionState.GetString(Key + ".save", "?"), "gerçek oyuncu kayıt dosyası değişmedi (" + SaveStamp() + ")");
        string before = File.ReadAllText(HashFile), after = Hashes();
        if (before != after)
        {
            var a = before.Split('\n'); var b = after.Split('\n');
            foreach (string line in b.Except(a)) Note("CHANGED " + line);
        }
        Ok(before == after, $"{before.Split('\n').Length - 1} asset ve sahne dosyası ile Build Settings sahne listesi değişmedi (kaynak ScriptableObject'ler, GameScene, DemoScene dahil)");
    }
}
