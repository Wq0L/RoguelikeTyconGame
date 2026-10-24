using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MechanicsTestLab
{
    // Test oturumunun yaşam döngüsü ve güvenliği. Oturum YALNIZ Play, MechanicsTestScene'den başladığında açılır; başka sahnede
    // (GameScene, DemoScene, MenuScene) Active false kalır ve panelin hiçbir komutu çalışmaz.
    // Oturum başında:
    //  - Seçili run profili DEĞİŞMEZ: senaryonun profili çalışma zamanında kopyalanır ve yalnız bu Play oturumu için geçerli kılınır
    //    (RunProfileSelectionSO.OverrideForSession; seçim dosyasına yazılmaz). Seed ve başlangıç kaynakları kopyaya yazılır.
    //  - Gerçek oyuncu kaydı okunur ama bu oturumda hiç yazılmaz (MetaSave.EditorProtectTestSession); çiftçi / tırpan seçimi ve
    //    görev ilerlemesi yalnız bellekte kalır.
    //  - Gerçek GameScene aynen yüklenir (kopya sahne yoktur): aynı prefablar, aynı yöneticiler, aynı arayüz.
    // Oturum sonunda (Play'den çıkış): zaman ölçeği, giriş bayrakları, profil geçersiz kılması ve kopyalar geri alınır.
    // "Senaryoyu yeniden kur", oyunun kendi yeniden başlatma yoludur (sahneyi yeniden yükler): ödüller, statlar, davranışlar,
    // bekleyen işler, olaylar, VFX ve ses oyunun tek temizlik yollarıyla sıfırlanır; ardından senaryo temiz oturuma kurulur.
    [InitializeOnLoad]
    public static class TestSession
    {
        private const string PendingKey = "MechanicsTestLab.PendingScenario";
        private const string StartSceneKey = "MechanicsTestLab.OwnsStartScene";

        public static bool Active { get; private set; }
        public static bool Ready { get; private set; }
        public static RunProfileSO Profile { get; private set; }
        public static RunProfileSO SourceProfile { get; private set; }
        public static Scenario Current { get; private set; }
        public static string Status { get; private set; } = "";
        // Her kurulumda artar: panel önbelleklerini ve canlı referanslarını bununla yeniler.
        public static int Generation { get; private set; }
        public static int Rebuilds { get; private set; }
        public static readonly List<Result> SetupReport = new();

        private static readonly List<Object> copies = new();
        private static bool waitingForScene, pendingSetup;
        private static int loadFrame;
        private static double setupDeadline;

        static TestSession()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Update;
        }

        public static bool SceneExists => AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab.ScenePath) != null;
        public static bool TestSceneOpen => SceneManager.GetActiveScene().path == Lab.ScenePath;

        // Panelden: açık sahneyi değiştirmeden test sahnesiyle Play başlatır (Unity açık sahneyi ve kaydedilmemiş değişiklikleri
        // Play'den çıkınca geri getirir).
        public static Result StartPlay(Scenario scenario)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Result.Fail("Zaten Play modunda ya da geçiş sürüyor.");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Lab.ScenePath);
            if (scene == null) return Result.Fail($"Test sahnesi yok: {Lab.ScenePath}. '{Lab.Menu}/Test sahnesini (yeniden) oluştur' ile kur.");
            SetPending(scenario);
            SessionState.SetBool(StartSceneKey, true);
            EditorSceneManager.playModeStartScene = scene;
            EditorApplication.EnterPlaymode();
            return Result.Success("Test oturumu başlatılıyor…");
        }

        // Test sahnesini açar; açık sahnede kaydedilmemiş değişiklik varsa Unity'nin normal kaydetme sorusu çıkar.
        public static Result OpenScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return Result.Fail("Play modunda sahne açılmaz.");
            if (!SceneExists) return Result.Fail($"Test sahnesi yok: {Lab.ScenePath}.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return Result.Fail("Sahne açma iptal edildi.");
            EditorSceneManager.OpenScene(Lab.ScenePath);
            return Result.Success("MechanicsTestScene açıldı. Play'e bas.");
        }

        // Bir sonraki test Play'inin (panelden ya da sahne açıkken Play'e basınca) kullanacağı senaryo. null: varsayılan.
        public static void SetPending(Scenario scenario) => SessionState.SetString(PendingKey, scenario != null ? JsonUtility.ToJson(scenario) : "");

        public static Scenario Pending
        {
            get
            {
                string json = SessionState.GetString(PendingKey, "");
                return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<Scenario>(json);
            }
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    if (TestSceneOpen) Begin();
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                    End();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    if (SessionState.GetBool(StartSceneKey, false))
                    {
                        SessionState.EraseBool(StartSceneKey);
                        EditorSceneManager.playModeStartScene = null;
                    }
                    break;
            }
        }

        private static void Begin()
        {
            Active = true;
            Rebuilds = 0;
            Scenario scenario = Pending;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TestTime.Install();
            TestLog.SetLevel(TestLog.Preferred);
            StartRun(scenario ?? ScenarioPresets.Default());
        }

        // Temiz oturum: profil kopyası, kayıt koruması ve gerçek GameScene'in yeniden yüklenmesi.
        private static void StartRun(Scenario scenario)
        {
            Ready = false;
            pendingSetup = false;
            Current = scenario;
            SetupReport.Clear();
            TestStats.Forget();
            TestRewards.Forget();
            // Açık iz eksik olarak kapanır; önceki oturumun görüntüleri yeni oturumla karşılaştırılmaz.
            TestTrace.Reset(false);
            Comparison.Clear();

            SourceProfile = LabCatalog.Profile(scenario.profile);
            if (SourceProfile == null)
            {
                SetupReport.Add(Result.Fail($"Profil bulunamadı: {scenario.profile}. {Lab.DefaultProfile} kullanıldı."));
                SourceProfile = LabCatalog.Profile(Lab.DefaultProfile);
            }
            if (SourceProfile == null)
            {
                Status = "Profil yok: oturum kurulamadı.";
                return;
            }
            // Eski kopya, eski sahne boşalana kadar yaşar (yöneticileri onu okuyor); hepsi oturum sonunda yok edilir.
            Profile = Object.Instantiate(SourceProfile);
            Profile.name = SourceProfile.name + " (Mechanics Test runtime)";
            Profile.displayName = "TEST · " + SourceProfile.displayName;
            Profile.bossSeed = scenario.seed != 0 ? scenario.seed : 1;
            if (scenario.gold >= 0) Profile.startingGold = scenario.gold;
            if (scenario.iron >= 0) Profile.startingIron = scenario.iron;
            if (scenario.stone >= 0) Profile.startingStone = scenario.stone;
            copies.Add(Profile);
            RunProfileSelectionSO.OverrideForSession(Profile);

            MetaSave.EditorProtectTestSession();
            ApplyLoadout(scenario);
            if (GameManager.Instance == null) new GameObject("Game Manager (Mechanics Test)").AddComponent<GameManager>();

            Status = "GameScene yükleniyor…";
            waitingForScene = true;
            setupDeadline = EditorApplication.timeSinceStartup + 60d;
            EditorSceneManager.LoadSceneInPlayMode(Lab.GameScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        }

        // Başlangıç seçimi yalnız bellekteki kayda yazılır (disk yazımı bu oturumda kapalıdır). Boş: kataloğun varsayılanı.
        private static void ApplyLoadout(Scenario scenario)
        {
            StartCatalogSO catalog = StartCatalogSO.Active;
            if (catalog == null) return;
            FarmerSO farmer = string.IsNullOrEmpty(scenario.farmer) ? catalog.defaultFarmer : catalog.Farmer(scenario.farmer);
            ScytheSO scythe = string.IsNullOrEmpty(scenario.scythe) ? catalog.defaultScythe : catalog.Scythe(scenario.scythe);
            if (farmer == null) { SetupReport.Add(Result.Fail($"Çiftçi bulunamadı: {scenario.farmer}. Varsayılan kullanıldı.")); farmer = catalog.defaultFarmer; }
            if (scythe == null) { SetupReport.Add(Result.Fail($"Tırpan bulunamadı: {scenario.scythe}. Varsayılan kullanıldı.")); scythe = catalog.defaultScythe; }
            MetaSaveData data = MetaSave.Data;
            foreach (StartOptionSO option in new StartOptionSO[] { farmer, scythe })
                if (option != null && !option.OpenAtStart && !data.unlocked.Contains(option.id)) data.unlocked.Add(option.id);
            if (farmer != null) data.farmer = farmer.id;
            if (scythe != null) data.scythe = scythe.id;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Active) return;
            if (scene.path != Lab.GameScenePath)
            {
                // Oyun sahnesinden çıkıldı (ör. run sonu ekranından ana menü): komutlar durur; oyuna dönülünce senaryo yeniden kurulur.
                if (mode == LoadSceneMode.Single)
                {
                    Ready = false;
                    pendingSetup = false;
                    Status = "Oyun sahnesi açık değil (" + scene.name + "). GameScene yeniden yüklenince senaryo temiz oturuma kurulur.";
                }
                return;
            }
            // Oyunun kendi yeniden başlatması da (run sonu ekranı, menüden dönüş) buraya düşer: aynı senaryo temiz oturuma kurulur.
            if (!waitingForScene)
            {
                TestStats.Forget();
                TestRewards.Forget();
                TestTrace.Reset(false);
                Comparison.Clear();
                SetupReport.Clear();
                setupDeadline = EditorApplication.timeSinceStartup + 60d;
            }
            waitingForScene = false;
            Ready = false;
            pendingSetup = true;
            loadFrame = Time.frameCount;
        }

        private static void Update()
        {
            if (!Active || !pendingSetup || !EditorApplication.isPlaying) return;
            RoundManager rounds = RoundManager.Instance;
            GameManager game = GameManager.Instance;
            // Yöneticilerin Start'ı (run başlangıcı, grid açılışı) bitmiş olmalı: yüklemeden sonra en az iki kare.
            bool started = rounds != null && game != null && game.CurrentState == GameStates.RunSetup && Time.frameCount >= loadFrame + 2 &&
                           GridManager.Instance != null && TestField.GridSize > 0;
            if (!started)
            {
                if (rounds != null && Time.frameCount >= loadFrame + 2)
                {
                    string error = rounds.CalendarError ?? rounds.RewardPoolError ?? rounds.DurationError;
                    if (error != null) { pendingSetup = false; Status = "Run başlamadı: profil verisi geçersiz — " + error; return; }
                }
                if (EditorApplication.timeSinceStartup > setupDeadline) { pendingSetup = false; Status = "GameScene hazır olmadı (zaman aşımı). Console'a bak."; }
                return;
            }
            pendingSetup = false;
            Setup();
        }

        private static void Setup()
        {
            // Kayıt burada temizlenir (sahne yüklendikten sonra): eski oturumun kapanış bildirimleri (ör. kaldırılan anten) yeni
            // oturumun kaydına taşınmaz.
            TestLog.Clear();
            TestLog.OnSceneRebuilt();
            TestHud.OnSceneRebuilt();
            TestLog.Note(TestLog.Kind.Tool, $"Senaryo kuruluyor: {Current.name} · profil {SourceProfile.name} · seed {Profile.bossSeed}");
            try { ScenarioRunner.Apply(Current, SetupReport); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                SetupReport.Add(Result.Fail("Senaryo kurulumu istisnayla kesildi: " + e.Message));
            }
            Ready = true;
            Generation++;
            int failed = SetupReport.FindAll(r => !r.Ok).Count;
            Status = failed == 0 ? $"Hazır · {Current.name}" : $"Hazır · {Current.name} · {failed} adım kurulamadı (rapora bak)";
            TestLog.Note(TestLog.Kind.Tool, "Senaryo kuruldu: " + Status);
        }

        // Senaryoyu temiz oturumdan yeniden kurar (aynı ya da değiştirilmiş senaryo).
        public static Result Rebuild(Scenario scenario = null)
        {
            if (!Active || !EditorApplication.isPlaying) return Result.Fail("Test oturumu açık değil.");
            if (waitingForScene || pendingSetup) return Result.Fail("Önceki kurulum henüz bitmedi.");
            Rebuilds++;
            StartRun((scenario ?? Current).Clone());
            return Result.Success("Temiz oturum kuruluyor…");
        }

        private static void End()
        {
            if (!Active) return;
            Active = Ready = false;
            waitingForScene = pendingSetup = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            LabRoutine.Stop();
            TestTrace.Reset(true);
            Comparison.Clear();
            LabReport.Forget();
            TestHud.Restore();
            TestLog.SetEnabled(false);
            TestTime.Restore();
            TestVisuals.Restore();
            PlayerController.EditorAutoAttack = true;
            TestAttack.UseMouse();
            PlantSpawner.EditorPauseProduction = false;
            RoundManager.EditorFreezeTimer = false;
            RunProfileSelectionSO.ClearSessionOverride();
            foreach (Object copy in copies) if (copy != null) Object.Destroy(copy);
            copies.Clear();
            Profile = SourceProfile = null;
            Status = "";
        }
    }
}
