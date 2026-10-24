using UnityEditor;
using UnityEngine;

namespace MechanicsTestLab
{
    // A · Oturum ve zaman: oturumu başlatma / yeniden kurma, round akışı, zaman, saldırı ve görsel test kontrolleri.
    public sealed partial class LabWindow
    {
        private int jumpRound = 1;
        private bool evaluateTargets;

        private void DrawSession()
        {
            if (!EditorApplication.isPlaying)
            {
                Header("Test oturumunu başlat");
                EditorGUILayout.LabelField($"Sahne: {Lab.ScenePath}" + (TestSession.SceneExists ? "" : "  (YOK)"));
                Scenario startScenario = TestSession.Pending;
                EditorGUILayout.LabelField("Başlangıç senaryosu: " + (startScenario != null ? startScenario.name : "00 · Boş başlangıç") + "  ('G · Senaryolar'dan değiştirilir)");
                using (new EditorGUI.DisabledScope(!TestSession.SceneExists))
                {
                    if (GUILayout.Button("Test oturumunu başlat (açık sahne değişmez; Play'den çıkınca geri gelir)", GUILayout.Height(28f)))
                        Say(TestSession.StartPlay(startScenario));
                    if (GUILayout.Button("MechanicsTestScene'i aç (kaydedilmemiş değişiklik varsa Unity sorar)")) Say(TestSession.OpenScene());
                }
                if (!TestSession.SceneExists && GUILayout.Button("Test sahnesini oluştur")) TestSceneBuilder.Build();
                EditorGUILayout.HelpBox("Oturum gerçek GameScene'i yükler; profil çalışma zamanında kopyalanır, kayıt yalnız bellekte tutulur. " +
                                        "Sahne açıkken doğrudan Play'e basmak da aynı oturumu başlatır.", MessageType.None);
                return;
            }
            if (!TestSession.Active) return;

            Header("Senaryo ve round");
            if (TestSession.SetupReport.Count > 0)
            {
                text.Clear();
                foreach (Result result in TestSession.SetupReport) text.Append(result).Append('\n');
                EditorGUILayout.HelpBox("Kurulum raporu:\n" + text.ToString().TrimEnd('\n'), TestSession.SetupReport.Exists(r => !r.Ok) ? MessageType.Warning : MessageType.None);
            }
            if (GUILayout.Button("Senaryoyu yeniden kur (temiz oturum: ödül, stat, davranış, olay, efekt ve kayıt sıfırlanır)", GUILayout.Height(26f)))
            {
                TestLog.Clear();
                Say(TestSession.Rebuild());
            }
            if (!TestSession.Ready) return;
            RoundManager rounds = RoundManager.Instance;
            GameManager game = GameManager.Instance;
            if (rounds == null || game == null) return;
            EditorGUILayout.LabelField("Durum", $"Round {rounds.CurrentRound}/{rounds.MaxRounds} · {game.CurrentState} · {(rounds.IsRoundActive ? "aktif" : "round dışında")} · kalan {rounds.RemainingTime:0.0} sn");
            if (GUILayout.Button("Round'u yeniden başlat (oyunun round sonu temizliği: aktif davranışlar, bekleyen işler, olay, VFX; tarla ve ödüller kalır)"))
                Say(rounds.EditorJumpToRound(rounds.CurrentRound) ? Result.Success($"Round {rounds.CurrentRound} yeniden başladı.") : Result.Fail("Round yeniden başlatılamadı."));
            EditorGUILayout.BeginHorizontal();
            jumpRound = Mathf.Clamp(EditorGUILayout.IntField("Round'a git", jumpRound), 1, rounds.MaxRounds);
            if (GUILayout.Button("Git", GUILayout.Width(60f)))
                Say(rounds.EditorJumpToRound(jumpRound) ? Result.Success($"Round {rounds.CurrentRound} başladı (atlanan round'ların ödülü verilmez; tarla ve güçler korunur).") : Result.Fail("Round'a gidilemedi."));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Boss round'ları: " + TestRewards.BossRounds(rounds) + ". Round'a gitmek ve round'u yeniden başlatmak bekleyen kart seçimlerini sıfırlar; " +
                                       "R26 ve sonrasında oyunun kendi takvimi o round'un tarla olayını kurabilir.", EditorStyles.wordWrappedMiniLabel);
            evaluateTargets = EditorGUILayout.ToggleLeft("Round bitişinde kota ve boss hasadını GERÇEKTEN değerlendir (tutmazsa run biter)", evaluateTargets);
            using (new EditorGUI.DisabledScope(!rounds.IsRoundActive))
                if (GUILayout.Button($"Round'u şimdi bitir (normal round sonu akışı: {rounds.PendingCardSelections} bekleyen kart seçimi → varsa boss ödülü → özet)"))
                    Say(rounds.EditorEndRound(evaluateTargets) ? Result.Success("Round oyunun normal akışıyla bitti; devamı Game görünümündeki ekranlarda.") : Result.Fail("Round bitirilemedi."));
            bool frozen = EditorGUILayout.ToggleLeft("Round sayacını dondur (round kendiliğinden bitmez)", TestAttack.RoundTimerFrozen);
            if (frozen != TestAttack.RoundTimerFrozen) TestAttack.RoundTimerFrozen = frozen;
            bool production = EditorGUILayout.ToggleLeft("Bitki üretimini duraklat (yeni bitki çıkmaz; mevcut bitkiler kalır)", TestAttack.ProductionPaused);
            if (production != TestAttack.ProductionPaused) TestAttack.ProductionPaused = production;

            Header("Zaman (oyunun kendi saati; ayrı sayaç yok)");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(TestTime.Paused ? "▶ Sürdür" : "❚❚ Duraklat", GUILayout.Width(110f))) TestTime.SetPaused(!TestTime.Paused);
            foreach (float speed in TestTime.Speeds)
            {
                bool current = !TestTime.Paused && Mathf.Approximately(TestTime.Speed, speed);
                if (GUILayout.Toggle(current, "×" + speed.ToString("0.##"), EditorStyles.miniButton) && !current) { TestTime.SetSpeed(speed); TestTime.SetPaused(false); }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            float stepMs = EditorGUILayout.FloatField("Tek adım (ms)", TestTime.StepSeconds * 1000f);
            TestTime.StepSeconds = Mathf.Clamp(stepMs, 1f, 1000f) / 1000f;
            using (new EditorGUI.DisabledScope(!TestTime.Paused))
                if (GUILayout.Button("Tek adım ilerlet", GUILayout.Width(130f))) Say(TestTime.Step());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"Zaman ölçeği ×{Time.timeScale:0.##} · oyun zamanı {Time.time:0.00} sn · atılan adım {TestTime.StepsDone}" +
                                       (TestTime.InRound ? "" : " · round dışında hız oyuna aittir (menü ekranları duraklatır)"), EditorStyles.miniLabel);

            Header("Saldırı (oyunun saldırı yolu)");
            bool auto = EditorGUILayout.ToggleLeft("Otomatik oyuncu saldırısı", TestAttack.AutoAttack);
            if (auto != TestAttack.AutoAttack) TestAttack.AutoAttack = auto;
            EditorGUILayout.LabelField("Nişan", TestAttack.Aim.HasValue ? $"sabit · ({TestAttack.Aim.Value.x:0.##}, {TestAttack.Aim.Value.z:0.##})" : "fare (normal oyun kontrolü)");
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(!hasSelection))
                if (GUILayout.Button("Nişanı seçili hücreye al")) { TestAttack.AimAt(anchor); Say(Result.Success($"Nişan ({anchor.x}, {anchor.z}) hücresinde sabit.")); }
            if (GUILayout.Button("Fareyle normal kontrole dön")) { TestAttack.UseMouse(); Say(Result.Success("Nişan fareye bırakıldı.")); }
            EditorGUILayout.EndHorizontal();
            float cooldown = TestAttack.Cooldown;
            EditorGUILayout.LabelField("Saldırı beklemesi", cooldown > 1e-4f ? $"{cooldown:0.00} sn kaldı (aralık {TestAttack.Interval:0.###} sn)" : $"hazır (aralık {TestAttack.Interval:0.###} sn)" +
                                                                                                                                              (DirectAttackPause.Active ? $" · DURAKLAMA {DirectAttackPause.Remaining:0.00} sn" : ""));
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Tek saldırı (nişana)", GUILayout.Height(24f)))
            {
                PlayerController player = TestAttack.Player;
                Vector3 target = TestAttack.Aim ?? (hasSelection ? TestAttack.WorldOf(anchor) : player != null ? player.CursorWorldPosition : Vector3.zero);
                Say(TestAttack.AttackOnce(target));
            }
            if (GUILayout.Button("Beklemeyi sıfırla (test komutu)", GUILayout.Height(24f))) Say(TestAttack.ResetCooldown());
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Tek saldırı normal kuralı atlamaz: bekleme dolmadıysa ya da Dolunay duraklaması sürüyorsa reddedilir ve nedeni yazılır. " +
                                       "Nişan sabit değilse seçili hücreye, o da yoksa imlecin olduğu yere vurur.", EditorStyles.wordWrappedMiniLabel);

            Header("Görsel test kontrolleri (yalnız gösterim)");
            bool numbers = EditorGUILayout.ToggleLeft("Hasar sayıları (oyun ayarı; yalnız bu oturumda, kayda yazılmaz)", TestVisuals.DamageNumbers);
            if (numbers != TestVisuals.DamageNumbers) TestVisuals.DamageNumbers = numbers;
            bool outlines = EditorGUILayout.ToggleLeft("Tarla olayı kesin alan çizgisi (test görünümü; varsayılan kapalı)", TestVisuals.EventOutlines);
            if (outlines != TestVisuals.EventOutlines) TestVisuals.EventOutlines = outlines;
            bool overlay = EditorGUILayout.ToggleLeft("Scene görünümünde gerçek saldırı dairesi ve hedef hücreleri (varsayılan kapalı)", TestVisuals.TargetOverlay);
            if (overlay != TestVisuals.TargetOverlay) { TestVisuals.TargetOverlay = overlay; SceneView.RepaintAll(); }
            EditorGUILayout.LabelField("Oyun HUD'unu gizle (yalnız çizim; HUD script'leri, yöneticiler, dünya VFX'i ve gerçek olaylar çalışmaya devam eder; oturum bitince geri gelir):", EditorStyles.wordWrappedMiniLabel);
            foreach ((TestHud.Part part, string label, string detail) in TestHud.Parts)
            {
                bool hidden = EditorGUILayout.ToggleLeft(new GUIContent("Gizle: " + label, detail), TestHud.IsHidden(part));
                if (hidden != TestHud.IsHidden(part)) Say(TestHud.SetHidden(part, hidden));
            }
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Scene görünümünü oyun kamerasının açısına getir"))
            {
                Camera camera = Camera.main;
                if (camera != null && SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);
            }
            using (new EditorGUI.DisabledScope(!hasSelection))
                if (GUILayout.Button("Scene görünümünü seçime odakla") && SceneView.lastActiveSceneView != null)
                    SceneView.lastActiveSceneView.Frame(new Bounds(TestAttack.WorldOf(anchor), Vector3.one * 8f), false);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Oyun kamerası sabit açıdadır ve test aracı onu hareket ettirmez; oyunda güvenli bir yakınlaştırma kontrolü yoktur. " +
                                       "Bu çizgiler ve halkalar yalnız test göstergesidir; oyunun VFX'inin yerine geçmez ve mekaniğin çalıştığını kanıtlamaz.", EditorStyles.wordWrappedMiniLabel);
        }
    }
}
