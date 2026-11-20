using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MechanicsTestLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;

// "Açılışta fare kartın üstündeyse kart sarı kalıyor / üstüne gelince tepki vermiyor" (12 Kasım 2026, kullanıcı: saksı dükkânı ve
// level kart seçimi). Gerçek GameScene + Mechanics Test Lab; SANAL FARE (Input System) gerçek arayüz modülünden olay üretir.
// Senaryo: fare kartın üstündeyken panel açılır → fare çekilir → geri gelir; level kartında ayrıca tıklama ve yeni dağıtım.
// Her adımda kartın düğme durumu, ölçeği ve görünen sprite'ı kaydedilir; beklenen: fare gidince normal, gelince vurgulu.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method HoverOpenVerification.RunBatch
[InitializeOnLoad]
public static class HoverOpenVerification
{
    const string Key = "HoverOpenVerification";
    static readonly List<string> lines = new();
    static int passed, failed, phase;
    static double next, started;
    static Mouse mouse;
    static Vector2 away = new(6f, 6f);
    static Vector2 target;
    static HoverOpenVerification() { EditorApplication.update += Tick; }

    static void Check(bool ok, string text) { lines.Add((ok ? "PASS " : "FAIL ") + text); if (ok) passed++; else failed++; }
    static void Info(string text) => lines.Add("INFO " + text);

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        var pipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline;
        if (!TestSession.SceneExists) TestSceneBuilder.Build();
        EditorSceneManager.OpenScene(Lab.GameScenePath);
        SessionState.SetBool(Key, true);
        Scenario s = ScenarioPresets.New("Açılışta hover", "", "", "", 3);
        s.autoAttack = false;
        Result r = TestSession.StartPlay(s);
        if (!r.Ok) throw new Exception(r.Message);
    }

    static RoundManager RM => RoundManager.Instance;
    static void Call(object target, string method, params object[] args) =>
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, args);
    static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(target);

    static void Move(Vector2 position) { InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); }
    static void Press(Vector2 position, bool down)
    {
        var state = new MouseState { position = position };
        if (down) state = state.WithButton(MouseButton.Left);
        InputSystem.QueueStateEvent(mouse, state);
    }
    static Vector2 Center(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

    static string SelState(Selectable s) =>
        typeof(Selectable).GetProperty("currentSelectionState", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(s).ToString();

    static CardSelectionUI Cards => UnityEngine.Object.FindAnyObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
    static CardUI Slot(int i) => Field<List<CardUI>>(Cards, "cardSlots")[i];

    static string CardInfo(CardUI card)
    {
        var hover = card.GetComponent<ComicHoverMotion>();
        var button = Field<Button>(card, "button");
        string flags = hover == null ? "no hover" : $"over={Field<bool>(hover, "over")} selected={Field<bool>(hover, "selected")} rest={hover.RestScale.x:0.000}";
        return $"scale {card.transform.localScale.x:0.000}, {flags}, button {SelState(button)}, selected object '{EventSystem.current.currentSelectedGameObject?.name ?? "-"}'";
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        Application.runInBackground = true;
        EditorApplication.QueuePlayerLoopUpdate();
        double now = EditorApplication.timeSinceStartup;
        if (started == 0) { started = now; next = now + 2; return; }
        if (now - started > 150) { Check(false, "timeout at phase " + phase); Finish(); return; }
        if (now < next || !TestSession.Ready) return;
        try
        {
            switch (phase)
            {
                case 0:
                {
                    // Toplu kipte pencere odaksız: sanal farenin girdisi yine işlensin (yalnız bellekte).
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    mouse = InputSystem.AddDevice<Mouse>();
                    Move(away);
                    Info($"screen {Screen.width}×{Screen.height}, module {EventSystem.current?.currentInputModule?.GetType().Name}");
                    var progress = ProgressionManager.Instance;
                    progress.AddXP(progress.XPToNextLevel); progress.AddXP(progress.XPToNextLevel);   // 6 seçim
                    Call(RM, "EndRound");
                    phase = 1; next = now + 1.5; return;
                }
                case 1:
                {
                    // Kart yuvasının yeri; panel kapanıp fare kartın üstündeyken yeniden açılır.
                    target = Center((RectTransform)Slot(1).transform);
                    Info($"card slot 1 at {target}");
                    Cards.gameObject.SetActive(false);
                    Move(target);
                    phase = 2; next = now + .4; return;
                }
                case 2:
                    UnityEngine.Object.FindAnyObjectByType<UIManager>().ShowCardSelectionUI();
                    phase = 3; next = now + 1.2; return;
                case 3:
                    Info("level card, opened under the mouse: " + CardInfo(Slot(1)));
                    Check(Slot(1).transform.localScale.x > Slot(1).GetComponent<ComicHoverMotion>().RestScale.x * 1.02f, "level card opened under the mouse shows the hover look");
                    Move(away);
                    phase = 4; next = now + .6; return;
                case 4:
                {
                    Info("level card, mouse moved away: " + CardInfo(Slot(1)));
                    var hover = Slot(1).GetComponent<ComicHoverMotion>();
                    Check(Mathf.Abs(Slot(1).transform.localScale.x - hover.RestScale.x) < .01f, "mouse leaves: level card returns to rest");
                    Move(target);
                    phase = 5; next = now + .6; return;
                }
                case 5:
                {
                    Info("level card, mouse back: " + CardInfo(Slot(1)));
                    var hover = Slot(1).GetComponent<ComicHoverMotion>();
                    Check(Slot(1).transform.localScale.x > hover.RestScale.x * 1.02f, "mouse comes back: level card reacts (hover look)");
                    Press(target, true);
                    phase = 6; next = now + .15; return;
                }
                case 6:
                    Press(target, false);   // tıklama: kart seçilir, yeni kartlar dağıtılır, fare yerinde
                    phase = 7; next = now + 1.4; return;
                case 7:
                {
                    Info($"after click (pending {RM.PendingCardSelections}), new card under the mouse: " + CardInfo(Slot(1)));
                    Move(away);
                    phase = 8; next = now + .6; return;
                }
                case 8:
                {
                    Info("after click, mouse moved away: " + CardInfo(Slot(1)));
                    var hover = Slot(1).GetComponent<ComicHoverMotion>();
                    Check(Mathf.Abs(Slot(1).transform.localScale.x - hover.RestScale.x) < .01f, "after a click and a new deal, mouse leaves: the card under it returns to rest (not stuck)");
                    Move(target);
                    phase = 9; next = now + .6; return;
                }
                case 9:
                {
                    Info("after click, mouse back: " + CardInfo(Slot(1)));
                    var hover = Slot(1).GetComponent<ComicHoverMotion>();
                    Check(Slot(1).transform.localScale.x > hover.RestScale.x * 1.02f, "after a click, mouse comes back: the card reacts again");
                    Move(away);
                    // kalan seçimleri al: round sonu, sonra dükkân
                    int guard = 0;
                    while (GameManager.Instance.CurrentState == GameStates.CardSelection && guard++ < 10)
                    {
                        var offers = Field<System.Collections.IList>(Cards, "currentCards");
                        Call(Cards, "OnCardSelected", offers[0]);
                    }
                    UnityEngine.Object.FindAnyObjectByType<UIManager>().OpenPlacementShop();
                    phase = 10; next = now + 1.2; return;
                }
                case 10:
                {
                    var shop = UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include);
                    var cards = Field<System.Collections.IList>(shop, "cards");
                    var card = cards[1];
                    target = Center(Field<RectTransform>(card, "rect"));
                    Info($"shop open {shop.isActiveAndEnabled}, card 1 at {target}");
                    shop.gameObject.SetActive(false);
                    Move(target);
                    phase = 11; next = now + .4; return;
                }
                case 11:
                    UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include).gameObject.SetActive(true);
                    phase = 12; next = now + 1.2; return;
                case 12: ShopStep("opened under the mouse", "Highlighted", 13, away); return;
                case 13: ShopStep("mouse moved away", "Normal", 14, target); return;
                case 14: ShopStep("mouse back", "Highlighted", 15, target); return;
                case 15: Press(target, true); phase = 16; next = now + .15; return;
                case 16: Press(target, false); phase = 17; next = now + 1.0; return;   // 1×2'ye tıklandı (detay açılır)
                case 17:
                {
                    var shop = UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include);
                    Info($"after clicking the 1x2 card: selected object '{EventSystem.current.currentSelectedGameObject?.name ?? "-"}'");
                    shop.gameObject.SetActive(false);   // dükkân kapanır
                    Move(away);
                    phase = 18; next = now + .4; return;
                }
                case 18:
                    UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include).gameObject.SetActive(true);
                    phase = 19; next = now + 1.0; return;
                case 19:
                {
                    var shop = UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include);
                    var cards = Field<System.Collections.IList>(shop, "cards");
                    var bg = Field<Image>(cards[1], "background");
                    string shown = bg.overrideSprite != null ? bg.overrideSprite.name : bg.sprite.name;
                    int golden = 0;
                    foreach (var c in cards) { var im = Field<Image>(c, "background"); string n = im.overrideSprite != null ? im.overrideSprite.name : im.sprite != null ? im.sprite.name : ""; if (n.EndsWith("_selected")) golden++; }
                    Info($"reopened after a click (mouse away): 1x2 shows '{shown}', golden cards {golden}, selected object '{EventSystem.current.currentSelectedGameObject?.name ?? "-"}'");
                    Check(!shown.EndsWith("_selected") && golden == 1, "reopened shop: only the chosen card (1x1) is golden; the clicked 1x2 is not stuck golden");
                    Move(target);
                    phase = 20; next = now + .6; return;
                }
                case 20: ShopStep("reopened, mouse over 1x2", "Highlighted", 21, away); return;
                case 21: ShopStep("reopened, mouse away", "Normal", 22, away); return;
                default: Finish(); return;
            }
        }
        catch (Exception e) { Check(false, $"phase {phase} threw: {e}"); Finish(); }

        void ShopStep(string label, string expected, int nextPhase, Vector2 moveTo)
        {
            var shop = UnityEngine.Object.FindAnyObjectByType<PlanterShopPanelUI>(FindObjectsInactive.Include);
            var card = Field<System.Collections.IList>(shop, "cards")[1];
            var button = Field<Button>(card, "button");
            var image = Field<Image>(card, "background");
            string shown = image.overrideSprite != null ? image.overrideSprite.name : image.sprite != null ? image.sprite.name : "-";
            string state = SelState(button);
            Info($"shop card 1x2, {label}: button {state}, shows '{shown}', scale {Field<RectTransform>(card, "rect").localScale.x:0.000}, selected object '{EventSystem.current.currentSelectedGameObject?.name ?? "-"}'");
            Check(state == expected, $"shop card, {label}: {expected} (is {state})");
            Move(moveTo);
            phase = nextPhase; next = EditorApplication.timeSinceStartup + .6;
        }
    }

    static void Finish()
    {
        SessionState.SetBool(Key, false);
        if (mouse != null) InputSystem.RemoveDevice(mouse);
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/HoverOpenVerification.txt", new[] { $"{(failed == 0 ? "PASS" : "FAIL")} {passed} passed, {failed} failed" }.Concat(lines));
        EditorApplication.Exit(failed == 0 ? 0 : 1);
    }
}
