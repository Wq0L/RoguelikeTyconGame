using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Diyalog altyapısı doğrulaması: Faz 1 (25 Ekim 2026) + Faz 2 birim kontrolleri (hazırlık tetiği, kutunun yeri, çeviri tablosu).
// Gerçek oyun akışı ayrı: DialogueFlowVerification. Plan: Docs/DiyalogPlani. Yalnız izole kopyada; kullanıcının kaydına dokunmaz
// (kalıcılık testleri geçici klasörde, sonra yeniden yalnız bellek). Zamanlamalı kontroller kutunun saatini elle sürer
// (DialogueDirector.Paused = true iken Update ilerletmez, Step doğrudan çalışır); "menü zamanı akar" kontrolü gerçek karelerle.
[InitializeOnLoad]
public static class DialogueVerification
{
    const string Key = "DialogueVerification";
    static int step = -1, visibleMark, dropMark;
    static double next, started;
    static readonly List<string> notes = new();
    static DialogueDirector D => DialogueDirector.Instance;
    static DialogueVerification() { EditorApplication.update += Tick; }
    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        GecOyunV1Verification.RunBatch(); SessionState.SetBool("GecOyunV1Verification", false); SessionState.SetBool(Key, true);
    }
    static object Lab(string n, params object[] a) => typeof(GecOyunV1Verification).GetMethod(n, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, a);
    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }
    static DialogueEntry E(string id, DialogueCategory c, DialogueTrigger t, DialogueSize s, string key) =>
        new DialogueEntry { id = id, category = c, trigger = t, size = s, lines = new[] { new DialogueLine { textKey = key, face = 8 } } };
    static void SetOutcome(RunOutcome o) => typeof(RoundManager).GetProperty("Outcome").GetSetMethod(true).Invoke(RoundManager.Instance, new object[] { o });

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (next == 0) { next = EditorApplication.timeSinceStartup + 2; started = next; return; }
        if (EditorApplication.timeSinceStartup < next) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        try
        {
            if (EditorApplication.timeSinceStartup - started > 420) throw new Exception("timeout step " + step);
            switch (step)
            {
                case -1:
                    if (double.IsNaN((double)Lab("Lab"))) return;
                    Check(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "real save disabled");
                    Persistence();
                    break;
                case 0:
                {
                    DialogueDirector.BatchEnabled = true;
                    DialogueDirector.Paused = true;   // elle sürülen saat
                    DialogueDirector d = DialogueDirector.Ensure();
                    Check(d != null && d.BigBox != null && d.SmallBox != null, "director and both boxes are built once");
                    Check(d.BigBox.GetComponent<GraphicRaycaster>() != null && d.SmallBox.GetComponent<GraphicRaycaster>() == null,
                        "big box is clickable; small box has no UI raycaster (cursor over it is not 'over UI', kapris hits still count)");
                    Check(d.BigBox.Blocker != null && d.BigBox.Blocker.raycastTarget && d.BigBox.Blocker.GetComponent<Button>() != null &&
                          !d.BigBox.Blocker.canvasRenderer.cullTransparentMesh &&
                          d.BigBox.Blocker.transform.GetSiblingIndex() < d.BigBox.Panel.GetSiblingIndex() && d.SmallBox.Blocker == null,
                        "big box is modal: a full-screen curtain under the box takes every click and advances; the small box has none");
                    Check(Object.FindObjectsByType<DialogueDirector>(FindObjectsSortMode.None).Length == 1, "single director instance");
                    break;
                }
                case 1:
                {
                    DialogueDirector d = D; DialogueBox big = d.BigBox;
                    EventSystem es = EventSystem.current;
                    bool navBefore = es == null || es.sendNavigationEvents;
                    d.ForceState(GameStates.MainMenu);
                    d.Raise(DialogueTrigger.SelectionOpened);
                    Check(big.IsOpen && d.Current != null && d.Current.id == "intro.select" && DialogueProgress.Seen("intro.select"),
                        "selection screen opens Eri's first conversation in the big box and it counts as seen");
                    Check(big.VisibleCharacters == 0 && big.Typing, "text starts empty");
                    big.Step(.1f);
                    int v = big.VisibleCharacters;
                    Check(v > 0 && v < 10, $"typewriter reveals letters over time ({v} after 0.1 s)");
                    big.Advance();
                    Check(!big.Typing && big.LineIndex == 0, "first press completes the line");
                    big.Advance();
                    Check(big.LineIndex == 1 && big.Typing, "second press goes to the next line");
                    Check(es == null || !es.sendNavigationEvents, "while the box is open Space/Enter cannot submit another selected button");
                    Check(GreenhouseMascot.Talking, "planet Eri is flagged as talking while the box speaks");
                    { big.Step(.001f); long before = GC.GetAllocatedBytesForCurrentThread(); for (int i = 0; i < 300; i++) big.Step(.004f); long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                      Check(bytes == 0, $"300 typing/idle steps allocate {bytes} bytes"); }
                    big.Skip();
                    Check(!big.IsOpen && d.Current == null && !GreenhouseMascot.Talking, "'Atla' closes the whole conversation");
                    Check(es == null || es.sendNavigationEvents == navBefore, "UI navigation restored after close");
                    d.Raise(DialogueTrigger.SelectionOpened);
                    Check(!big.IsOpen, "a seen conversation does not repeat");
                    // Run sonu: neden süzgeci, sayaç.
                    int finishedBefore = MetaSave.Data.runsFinished;
                    SetOutcome(RunOutcome.None);
                    d.ForceState(GameStates.RunComplete);
                    Check(!big.IsOpen && MetaSave.Data.runsFinished == finishedBefore + 1, "run end counts the run; quota line does not show for another ending");
                    SetOutcome(RunOutcome.QuotaFailed);
                    d.ForceState(GameStates.RoundEnd);
                    d.ForceState(GameStates.RunComplete);
                    Check(big.IsOpen && d.Current.id == "end.quota", "Adak failure shows its own run-end lines");
                    // Gerçek karelerle: menü zamanı (timeScale 0) akarken yazı ilerlemeli.
                    Time.timeScale = 0f;
                    DialogueDirector.Paused = false;
                    visibleMark = big.VisibleCharacters;
                    break;
                }
                case 2:
                {
                    DialogueBox big = D.BigBox;
                    Check(big.VisibleCharacters > visibleMark, $"menu time (timeScale 0) still types ({visibleMark} → {big.VisibleCharacters})");
                    DialogueDirector.Paused = true;
                    visibleMark = big.VisibleCharacters;
                    break;
                }
                case 3:
                {
                    DialogueBox big = D.BigBox;
                    Check(big.VisibleCharacters == visibleMark, "pause freezes the text (distinct from menu time)");
                    Check(DialogueDirector.ClockDelta == 0f, "paused clock is zero");
                    Time.timeScale = 1f;
                    big.Skip();
                    // Round: küçük kutu, kendiliğinden kapanma, sıradaki öğretim.
                    DialogueDirector d = D; DialogueBox small = d.SmallBox;
                    // Hazırlık ekranı (Faz 2): menüdeki "Başla" durumu menü sahnesinde RunSetup'a çevirir; run kurulmadan konuşulmaz.
                    d.ForceState(GameStates.MainMenu);
                    d.ForceState(GameStates.RunSetup);
                    Check(!big.IsOpen && !small.IsOpen && !DialogueProgress.Seen("tut.setup"), "RunSetup in the menu scene (before the run exists) says nothing");
                    d.ForceRunStarted();
                    Check(big.IsOpen && d.Current.id == "tut.setup", "run built while already in RunSetup: the planter lesson opens at once");
                    { float a0 = big.Blocker.color.a; big.Step(DialogueBox.DimFadeSeconds * .5f); float a1 = big.Blocker.color.a; big.Step(DialogueBox.DimFadeSeconds);
                      Check(a0 == 0f && a1 > 0f && a1 < DialogueBox.DimAlpha && Mathf.Approximately(big.Blocker.color.a, DialogueBox.DimAlpha),
                          $"the curtain fades in ({a0:0.00} → {a1:0.00} → {big.Blocker.color.a:0.00})"); }
                    big.Skip();
                    d.ForceState(GameStates.Placing);
                    Check(big.IsOpen && d.Current.id == "tut.place", "placement lesson");
                    big.Skip();
                    d.ForceState(GameStates.MainMenu);
                    d.ForceRunStarted();
                    d.ForceState(GameStates.RunSetup);
                    string reaction = (string)typeof(GreenhouseMascot).GetField("pendingReaction", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    Check(reaction == DialogueText.Get("react.setup.0") && !big.IsOpen && !small.IsOpen, "next run: planet reactions go to Eri on the planet, not to the box");
                    // Lab round 26 bir Sınama round'u ve dönem sonu: o öğretimler akış testinde (DialogueFlowVerification) sınanır.
                    DialogueProgress.MarkSeen("tut.boss"); DialogueProgress.MarkSeen("tut.lastround");
                    d.ForceState(GameStates.Round);
                    Check(small.IsOpen && !big.IsOpen && d.Current.id == "tut.hit", "round start shows the hit lesson in the small box");
                    small.Advance();
                    Check(small.IsOpen && !small.Typing, "Space completes the small box text");
                    small.Step(DialogueBox.SmallCloseSeconds - .1f);
                    Check(small.IsOpen, "small box waits ~6 s after the text is complete");
                    small.Step(.2f);
                    Check(small.IsOpen && d.Current != null && d.Current.id == "tut.hit" && small.LineIndex == 1 && small.Typing,
                        "after ~6 s the small box moves to its second line by itself (T4 is two lines)");
                    small.Advance();
                    small.Step(DialogueBox.SmallCloseSeconds + .1f);
                    Check(small.IsOpen && d.Current != null && d.Current.id == "tut.adak", "small box closes by itself, then the next lesson (Adak) follows in the round");
                    small.Close();
                    Check(!small.IsOpen && d.Current == null, "nothing else is pending");
                    d.ForceState(GameStates.RoundEnd);
                    d.ForceState(GameStates.Round);
                    Check(!small.IsOpen, "lessons are not repeated in the next round / run (event based, seen once)");
                    // Öncelik ve geçerlilik: test kataloğu.
                    var test = ScriptableObject.CreateInstance<DialogueCatalogSO>();
                    test.entries = new[]
                    {
                        E("t.small", DialogueCategory.Guide, DialogueTrigger.RoundStarted, DialogueSize.Small, "tut.hit.0"),
                        E("t.kapris", DialogueCategory.Guide, DialogueTrigger.FieldEventPrepared, DialogueSize.Small, "tut.adak.0"),
                        E("t.story", DialogueCategory.Story, DialogueTrigger.RunEnded, DialogueSize.Big, "end.quota.1"),
                        E("t.guideEnd", DialogueCategory.Guide, DialogueTrigger.RunEnded, DialogueSize.Big, "end.quota.0"),
                    };
                    d.UseCatalog(test);
                    if (FieldEventDirector.Instance != null && FieldEventDirector.Instance.Current != null) FieldEventDirector.Instance.End();
                    dropMark = d.DroppedStale;
                    d.ForceState(GameStates.RoundEnd);
                    d.ForceState(GameStates.Round);
                    Check(small.IsOpen && d.Current.id == "t.small", "test catalog: round line showing");
                    d.Raise(DialogueTrigger.FieldEventPrepared);
                    Check(d.Queued != null && d.Queued.id == "t.kapris", "a second line waits in the single queue slot");
                    small.Close();
                    Check(!small.IsOpen && d.DroppedStale == dropMark + 1 && !DialogueProgress.Seen("t.kapris"),
                        "a queued line whose moment passed (no kapris active) is not shown and stays unseen");
                    d.ForceState(GameStates.RunComplete);
                    Check(big.IsOpen && d.Current.id == "t.guideEnd" && DialogueProgress.PendingStory.Contains("t.story"),
                        "required lesson beats story at the same moment; the story line is deferred, not lost");
                    big.Close();
                    Check(big.IsOpen && d.Current.id == "t.story" && !DialogueProgress.PendingStory.Contains("t.story"),
                        "deferred story shows at the next calm moment");
                    big.Close();
                    Object.DestroyImmediate(test);
                    // Ekran görüntüleri için gerçek satırlar (görülmüş olsalar da tekrar gösterilsin diye ayrı kimlik).
                    var shots = ScriptableObject.CreateInstance<DialogueCatalogSO>();
                    shots.entries = new[]
                    {
                        E("shot.big", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, "intro.select.0"),
                        E("shot.small", DialogueCategory.Guide, DialogueTrigger.RoundStarted, DialogueSize.Small, "tut.hit.0"),
                    };
                    d.UseCatalog(shots);
                    d.ForceState(GameStates.MainMenu);
                    d.Raise(DialogueTrigger.SelectionOpened);
                    big.Advance();
                    break;
                }
                case 4:
                    Lab("Capture", "Dialogue_01_Big");
                    D.BigBox.Close();
                    D.ForceState(GameStates.Round);
                    D.SmallBox.Advance();
                    break;
                case 5:
                    Lab("Capture", "Dialogue_02_Small");
                    D.SmallBox.Close();
                    // Mechanics Test Lab oturumu: kendiliğinden konuşmaz; lab araçları (LabEnabled) açınca konuşur.
                    {
                        var lab = ScriptableObject.CreateInstance<DialogueCatalogSO>();
                        lab.entries = new[] { E("lab.big", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, "intro.select.1") };
                        D.UseCatalog(lab);
                        D.ForceState(GameStates.MainMenu);
                        DialogueDirector.LabSession = true;
                        D.Raise(DialogueTrigger.SelectionOpened);
                        Check(DialogueDirector.Suspended && !D.BigBox.IsOpen && !DialogueProgress.Seen("lab.big"), "lab session keeps the dialogue silent (test scene protected)");
                        DialogueDirector.LabEnabled = true;
                        D.Raise(DialogueTrigger.SelectionOpened);
                        Check(!DialogueDirector.Suspended && D.BigBox.IsOpen, "future lab tools can turn it on explicitly");
                        D.BigBox.Close();
                        DialogueDirector.LabEnabled = DialogueDirector.LabSession = false;
                        Object.DestroyImmediate(lab);
                    }
                    Translation();
                    D.UseCatalog(Resources.Load<DialogueCatalogSO>(DialogueCatalogSO.ResourcePath));
                    DialogueDirector.Paused = false;
                    break;
                case 6:
                    // Duraklatılmış saatle kapanan kutuların çıkışı gerçek zamanla biter: biraz bekle (sınır: genel zaman aşımı).
                    if (D.SmallBox.Exiting || D.BigBox.Exiting) { next = EditorApplication.timeSinceStartup + .1; EditorApplication.QueuePlayerLoopUpdate(); return; }
                    Motion();
                    break;
                case 7: Paper(); break;
                case 8: Finish(null); return;
            }
            step++; next = EditorApplication.timeSinceStartup + .05;
            File.WriteAllLines("Logs/DialogueVerification.txt", new[] { "RUNNING step " + step }.Concat(notes));
        }
        catch (Exception ex) { Finish(ex.InnerException ?? ex); return; }
        EditorApplication.QueuePlayerLoopUpdate();
    }

    // Çeviri (Faz 2): sütunlar başlıktan, boş hücre İngilizceye sonra Türkçeye düşer, dil değişince kutu etiketleri yenilenir,
    // dilin yazı hızı çarpanı ve Çince/Japonca noktalamada duraklama.
    static void Translation()
    {
        var map = new Dictionary<string, string>(); var columns = new List<string>();
        const string csv = @"key,note,tr,en,de,ja
meta.speed,not text,1,1,1,0.5
a,n,Bir,One,Eins,イチ
b,n,İki,Two,,
c,""x, y"",Üç,,,
";
        DialogueText.Load(csv, map, "de", columns);
        Check(columns.SequenceEqual(new[] { "tr", "en", "de", "ja" }) && map["a"] == "Eins" && map["b"] == "Two" && map["c"] == "Üç",
            "translation table: language columns by header name (translator note skipped); an empty cell falls back to English, then Turkish");
        Check(DialogueText.Missing(csv, "de").SequenceEqual(new[] { "b", "c" }) && DialogueText.Missing(csv, "fr").Count == 3, "missing-translation report per language");

        DialogueDirector d = D; DialogueBox big = d.BigBox;
        var cat = ScriptableObject.CreateInstance<DialogueCatalogSO>();
        cat.entries = new[] { E("t.lang", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, "tut.hit.0"),
                              E("t.cjk", DialogueCategory.Guide, DialogueTrigger.LevelUp, DialogueSize.Big, "t.cjk") };
        d.UseCatalog(cat);
        d.ForceState(GameStates.MainMenu);
        DialogueText.Language = "en";
        d.Raise(DialogueTrigger.SelectionOpened);
        Check(big.IsOpen && big.SkipText == "Skip" && big.NameText == "Eri" && big.ShownText == DialogueText.Get("tut.hit.0") && DialogueText.Get("tut.hit.0").StartsWith("Hasat"),
            "language switched to English: button label follows; an untranslated line falls back to Turkish instead of showing a key");
        DialogueText.Language = "tr";
        Check(big.SkipText == "Atla", "language change refreshes an open box's labels");
        big.Close();

        DialogueText.UseCsv(@"key,note,tr,ja
meta.speed,,1,0.5
ui.skip,,Atla,スキップ
t.cjk,,x,はい。そうです
");
        DialogueText.Language = "ja";
        Check(Mathf.Approximately(DialogueText.SpeedScale, .5f), "per-language typing speed factor read from the table");
        d.ForceState(GameStates.CardSelection);
        d.Raise(DialogueTrigger.LevelUp);
        Check(big.IsOpen && big.SkipText == "スキップ" && big.NameText == "Eri", "Japanese labels; a missing name key keeps 'Eri'");
        big.Step(.1f);
        Check(big.VisibleCharacters == 2, $"half speed in Japanese ({big.VisibleCharacters} characters after 0.1 s, Turkish would show 4)");
        big.Step(.03f); big.Step(.03f);
        int atStop = big.VisibleCharacters;
        big.Step(.1f);
        Check(atStop == 3 && big.VisibleCharacters == 3, "Japanese full stop (。) pauses the typewriter like a Latin period");
        big.Step(.1f); big.Step(.1f);
        Check(big.VisibleCharacters > 3, "typing resumes after the pause");
        big.Close();
        DialogueText.UseCsv(null);
        DialogueText.Language = "tr";
        Check(DialogueText.Get("ui.skip") == "Atla" && DialogueText.Languages.Contains("en"), "back to the real Turkish table");
        Object.DestroyImmediate(cat);
    }

    // Hareket kaydı (göz kontrolü için): büyük kutunun girişi → yazı → çıkışı, sonra küçük kutunun kayarak girişi → çıkışı.
    // Saat elle sürülür (karede .02 sn); kareler Logs/DialogueMotion/ altına, 50 kare/sn video için. Çıkışta kutu tık almaz.
    static void Motion()
    {
        DialogueDirector d = D; DialogueBox big = d.BigBox, small = d.SmallBox;
        DialogueDirector.Paused = true;
        var cat = ScriptableObject.CreateInstance<DialogueCatalogSO>();
        cat.entries = new[] { E("motion.big", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, "tut.setup.0"),
                              E("motion.small", DialogueCategory.Guide, DialogueTrigger.RoundStarted, DialogueSize.Small, "tut.hit.0") };
        d.UseCatalog(cat);
        string dir = "Logs/DialogueMotion";
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        Check(!small.Exiting && !small.gameObject.activeSelf && !big.Exiting && !big.gameObject.activeSelf,
            "motion: boxes closed earlier while the dialogue clock was paused have finished their exit (cleanup runs on real time)");
        int frame = 0;
        void Shot(DialogueBox box, int frames) { for (int i = 0; i < frames; i++) { Lab("Capture", "DialogueMotion/f" + (frame++).ToString("000")); box.Step(.02f); } }
        d.ForceState(GameStates.RoundEnd);
        d.Raise(DialogueTrigger.SelectionOpened);
        Check(big.IsOpen && big.EnterProgress == 0f && big.Blocker.color.a == 0f, "motion: the big box starts below, the curtain clear");
        Shot(big, 70);
        Check(big.EnterProgress >= 1f && Mathf.Approximately(big.Blocker.color.a, DialogueBox.DimAlpha), "motion: entrance done, curtain at full strength");
        big.Advance(); Shot(big, 15);
        big.Advance();
        Check(!big.IsOpen && big.Exiting && !big.BlocksInput, "motion: on close the box stops taking clicks at once and plays its exit");
        Shot(big, 12);
        Check(!big.Exiting && !big.gameObject.activeSelf, "motion: exit finished, box hidden");
        d.ForceState(GameStates.Round);
        Check(small.IsOpen && small.EnterProgress == 0f, "motion: the small box slides in from the left");
        Shot(small, 60);
        small.Advance(); small.Step(DialogueBox.SmallCloseSeconds);
        Check(!small.IsOpen && small.Exiting, "motion: the small box slides out after its wait");
        Shot(small, 12);
        Check(!small.Exiting, "motion: small exit finished");
        notes.Add($"NOTE motion frames: {frame} in {dir}");
        d.UseCatalog(Resources.Load<DialogueCatalogSO>(DialogueCatalogSO.ResourcePath));
        Object.DestroyImmediate(cat);
        DialogueDirector.Paused = false;
    }

    // Büyük kutunun son hâli (26 Ekim: kâğıtlar kaldırıldı; karartma + üst köşe tramı + kutu). Boşluk'a art arda basan oyuncu
    // tek satırlık konuşmayı iki basışta geçer ve ekran o anda geri gelir. Görüntü: Logs/Box_Final.png.
    static void Paper()
    {
        DialogueDirector d = D; DialogueBox big = d.BigBox;
        DialogueDirector.Paused = true;
        var cat = ScriptableObject.CreateInstance<DialogueCatalogSO>();
        cat.entries = new[] { E("box.a", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, "tut.setup.0"),
                              E("box.b", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, "tut.place.0") };
        d.UseCatalog(cat);
        d.ForceState(GameStates.RoundEnd);
        d.Raise(DialogueTrigger.SelectionOpened);
        big.Settle();
        if (big.Typing) big.Advance();
        Check(big.IsOpen && Mathf.Approximately(big.Blocker.color.a, DialogueBox.DimAlpha), "final look: curtain at full strength after settling");
        Lab("Capture", "Box_Final");
        big.Skip();
        big.Step(DialogueBox.ExitSeconds + .01f);
        // Boşluk yağmuru: açılıştan .05 sn sonra iki basış.
        d.Raise(DialogueTrigger.SelectionOpened);
        big.Step(.05f);
        Check(big.IsOpen && big.EnterProgress < 1f && big.Typing, "spam test: box mid-entrance, text typing");
        big.Advance();
        Check(big.IsOpen && big.EnterProgress >= 1f && !big.Typing, "first press settles the box at once and completes the text");
        big.Advance();
        Check(!big.IsOpen && !big.BlocksInput && big.Exiting, "second press closes a one-line conversation; the screen takes clicks again at once while the box exits");
        big.Step(DialogueBox.ExitSeconds + .01f);
        Check(!big.gameObject.activeSelf, "exit finished");
        d.UseCatalog(Resources.Load<DialogueCatalogSO>(DialogueCatalogSO.ResourcePath));
        Object.DestroyImmediate(cat);
        DialogueDirector.Paused = false;
    }

    // Kalıcılık: geçici klasörlerde yaz → yeniden yükle; eski (1) ve yeni (3) sürüm kayıtlar; satır görülünce diske hemen yazılmaz.
    static void Persistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "ClickerDialogueTest_" + Guid.NewGuid().ToString("N"));
        string a = Path.Combine(root, "a"), v1 = Path.Combine(root, "v1"), v3 = Path.Combine(root, "v3");
        Directory.CreateDirectory(a); Directory.CreateDirectory(v1); Directory.CreateDirectory(v3);
        try
        {
            MetaSave.UseDirectory(a); MetaSave.Load();
            Check(MetaSave.LastLoad == MetaSave.LoadResult.Fresh, "fresh save in a temporary folder");
            DialogueProgress.MarkSeen("p.a");
            Check(!File.Exists(MetaSave.FilePath) && MetaSave.HasPendingChanges, "seeing a line only marks pending changes (no disk write per line)");
            DialogueProgress.RunStarted();
            DialogueProgress.AddMilestone(DialogueProgress.FirstBoss);
            Check(DialogueProgress.Stage == 0, "milestone does not change the stage mid-run");
            Check(DialogueProgress.RunFinished() && DialogueProgress.Stage == 1 && File.Exists(MetaSave.FilePath), "stage advances at run end and is written immediately");
            MetaSave.UseDirectory(a);
            Check(DialogueProgress.Seen("p.a") && DialogueProgress.Stage == 1 && MetaSave.Data.runsStarted == 1 && MetaSave.Data.runsFinished == 1 && MetaSave.LastLoad == MetaSave.LoadResult.Loaded,
                "seen lines, stage and run counters survive a reload");
            Check(File.ReadAllText(MetaSave.FilePath).Contains("\"version\": 2"), "file is written as version 2");

            File.WriteAllText(Path.Combine(v1, MetaSave.FileName), "{\"version\":1,\"farmer\":\"f1\",\"scythe\":\"s1\",\"unlocked\":[\"u1\"],\"quests\":[]}");
            MetaSave.UseDirectory(v1);
            MetaSaveData d1 = MetaSave.Data;
            Check(MetaSave.LastLoad == MetaSave.LoadResult.Loaded && d1.version == 2 && d1.farmer == "f1" && d1.unlocked.Contains("u1") &&
                  d1.seenDialogue != null && d1.seenDialogue.Count == 0 && d1.pendingStory != null && d1.storyStage == 0 && !DialogueProgress.Seen("p.a"),
                "a version-1 save opens: old fields kept, dialogue fields start empty");
            Check(MetaSave.Save() && File.ReadAllText(MetaSave.FilePath).Contains("seenDialogue"), "the migrated save is written with the new fields");

            File.WriteAllText(Path.Combine(v3, MetaSave.FileName), "{\"version\":3,\"farmer\":\"f3\",\"scythe\":\"s3\",\"unlocked\":[],\"quests\":[],\"futureField\":7}");
            MetaSave.UseDirectory(v3);
            _ = MetaSave.Data;
            Check(!MetaSave.Save() && File.ReadAllText(MetaSave.FilePath).Contains("futureField"), "a newer-version save is read but never overwritten");
        }
        finally
        {
            MetaSave.UseMemoryOnly(); MetaSave.Load();
            try { Directory.Delete(root, true); } catch (Exception) { }
        }
        Check(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly && !DialogueProgress.Seen("p.a"), "test returns to memory-only; the real save was never touched");
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        Time.timeScale = 1f;
        DialogueDirector.Paused = false;
        File.WriteAllLines("Logs/DialogueVerification.txt", new[] { e == null ? "PASS " + notes.Count + " checks" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }
}
