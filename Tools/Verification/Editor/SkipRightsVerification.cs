using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// Kart atlama hakkı (12 Kasım 2026; kullanıcı: "RerollCard'ı silmeyeceğiz, skip'te kullanacağız; hak her round yenilensin; kalan
// hak sayısı gözüksün"). Gerçek GameScene, Mechanics Test Lab oturumu. Sınananlar:
//  - Temel: round başında 1 hak; kart seçim ekranında düğme "… (1)"; kullanınca düğme kapanır, ikinci basış bir şey yapmaz.
//  - CardSkip +1 ve RerollCard +1 (eski ad): round başında 3 hak; düğme 3 → 2 → 1 diye azalır, bitince kapanır.
//  - Sonraki round başında hak yeniden dolar.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method SkipRightsVerification.RunBatch
[InitializeOnLoad]
public static class SkipRightsVerification
{
    const string Key = "SkipRightsVerification";
    static readonly List<string> lines = new();
    static int passed, failed, phase;
    static double next, started;
    static SkipRightsVerification() { EditorApplication.update += Tick; }

    static void Check(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (ok) passed++; else failed++; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Atlama hakkı", "", "", "", 3);
        s.autoAttack = false;
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static RoundManager RM => RoundManager.Instance;
    static CardSelectionUI Cards => UnityEngine.Object.FindAnyObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static Button SkipButton => (Button)typeof(CardSelectionUI).GetField("skipButton", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Cards);
    static string SkipText => SkipButton.GetComponentInChildren<TMP_Text>(true).text;
    static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);

    // Bir level: üç seçim kuyruğa girer; round biter, kart seçimi açılır.
    static void LevelAndEndRound()
    {
        var progress = ProgressionManager.Instance;
        progress.AddXP(progress.XPToNextLevel);
        Call(RM, "EndRound");
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 120) { Check(false, "timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            switch (phase)
            {
                case 0:
                {
                    Check(RM.SkipUsesRemaining == 1, $"base: one skip right at round start ({RM.SkipUsesRemaining})");
                    LevelAndEndRound();
                    Check(GameManager.Instance.CurrentState == GameStates.CardSelection && SkipButton.gameObject.activeSelf && SkipText.Contains("(1)"),
                          $"card choice opens; the skip button shows the rights left: '{SkipText}'");
                    int pending = RM.PendingCardSelections;
                    Call(Cards, "OnSkipPressed");
                    Check(RM.SkipUsesRemaining == 0 && RM.PendingCardSelections == pending - 1 && !SkipButton.gameObject.activeSelf,
                          "using the only skip consumes that choice and closes the button");
                    Call(Cards, "OnSkipPressed");
                    Check(RM.PendingCardSelections == pending - 1, "pressing skip with no rights left changes nothing");
                    // kalan seçimleri al, round sonu
                    int guard = 0;
                    while (GameManager.Instance.CurrentState == GameStates.CardSelection && guard++ < 10)
                    {
                        var offers = (System.Collections.IList)typeof(CardSelectionUI).GetField("currentCards", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(Cards);
                        Call(Cards, "OnCardSelected", offers[0]);
                    }
                    // yetenek etkisi: CardSkip +1 ve eski ad RerollCard +1
                    StatManager.Instance.AddGlobalModifiers(new List<StatModifier>
                    {
                        new StatModifier { statType = StatType.CardSkip, target = StatTarget.All, operation = ModifierOperation.Flat, value = 1f },
                        new StatModifier { statType = StatType.RerollCard, target = StatTarget.All, operation = ModifierOperation.Flat, value = 1f },
                    });
                    phase = 1; next = now + .5; return;
                }
                case 1:
                {
                    RM.StartNextRound();
                    phase = 2; next = now + 1; return;
                }
                case 2:
                {
                    Check(RM.IsRoundActive && RM.SkipUsesRemaining == 3, $"next round: rights refill to 1 + CardSkip 1 + RerollCard 1 = 3 ({RM.SkipUsesRemaining})");
                    LevelAndEndRound();
                    var seen = new List<string> { SkipText };
                    Call(Cards, "OnSkipPressed"); seen.Add(SkipButton.gameObject.activeSelf ? SkipText : "kapalı");
                    Call(Cards, "OnSkipPressed"); seen.Add(SkipButton.gameObject.activeSelf ? SkipText : "kapalı");
                    bool counts = seen[0].Contains("(3)") && seen[1].Contains("(2)") && seen[2].Contains("(1)");
                    Check(counts, $"the button counts down: {string.Join(" → ", seen)}");
                    Call(Cards, "OnSkipPressed");
                    Check(RM.SkipUsesRemaining == 0 && (GameManager.Instance.CurrentState != GameStates.CardSelection || !SkipButton.gameObject.activeSelf),
                          "after the third skip the button is closed (or the choices are over)");
                    Finish(); return;
                }
            }
        }
        catch (Exception e) { Check(false, $"phase {phase} threw: {e}"); Finish(); }
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/SkipRightsVerification.txt", new[] { $"{(failed == 0 ? "PASS" : "FAIL")} {passed} passed, {failed} failed" }.Concat(lines));
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
