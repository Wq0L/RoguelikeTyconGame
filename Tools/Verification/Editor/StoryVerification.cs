using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Hikâye taşları (Faz 3, 5 Kasım 2026 kesin plan v3: Docs/DiyalogPlani/Faz3-KilometreTaslari-Taslak.md). Gerçek katalog, gerçek
// oyun sahnesi (GecOyunV1 kurulumu, R50 takvimi: ilk Sınama R3). Durumlar yönetici yoluyla sürülür (ForceState), round numarası
// RoundManager'a yansıtılarak yazılır. Sınananlar:
// - 14 konuşma sabit sırayla, her biri bir kez; kazanılmış en ileri taşa kadar görülmemiş ilk konuşma çıkar (atlanmaz).
// - Bir sakin anda en çok bir hikâye; dükkândan dönüş aynı an sayılır.
// - R25 konuşması açılırken Eri griye döner (aşama 2), zafer itirafının 2. satırında kırmızıya (aşama 3); aşama geri gitmez.
// - Taş 1'in cevabı kaydedilir ve itirafta ona göre satır çıkar; R25'in cevabı kaydedilmez.
// - İlk tam zaferde end.victory yerine itiraf; zaferden sonraki ilk run başında kapanış; kayıp run sonunda hikâye yok.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method StoryVerification.RunBatch (önce DialogueCatalogBuilder.BuildBatch).
[InitializeOnLoad]
public static class StoryVerification
{
    const string Key = "StoryVerification";
    static int step = -1;
    static double next, started;
    static readonly List<string> notes = new();
    static readonly List<string> order = new();
    static DialogueDirector D => DialogueDirector.Instance;
    static RoundManager RM => RoundManager.Instance;

    static StoryVerification() { EditorApplication.update += Tick; }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        GecOyunV1Verification.RunBatch(); SessionState.SetBool("GecOyunV1Verification", false); SessionState.SetBool(Key, true);
    }

    static object Lab(string n, params object[] a) => typeof(GecOyunV1Verification).GetMethod(n, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, a);
    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }
    static void SetRound(int round) => typeof(RoundManager).GetProperty("CurrentRound").GetSetMethod(true).Invoke(RM, new object[] { round });
    static void SetOutcome(RunOutcome o) => typeof(RoundManager).GetProperty("Outcome").GetSetMethod(true).Invoke(RM, new object[] { o });
    static string Id => D.Current != null ? D.Current.id : null;

    // Eri'nin görünüşü (B adımı): yüklü tablo, bellekteki Eri tablosu sayısı, kutudaki portre ve gezegendeki Eri'nin dokusu.
    static int MascotTexturesInMemory() => Resources.FindObjectsOfTypeAll<Texture2D>().Count(t => t != null && t.name.StartsWith("GreenhouseMascotFrames"));
    static Texture BoxPortrait()
    {
        var image = D.BigBox.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(i => i.gameObject.name == "Eri");
        return image != null && image.sprite != null ? image.sprite.texture : null;
    }
    static Texture PlanetPicture()
    {
        var mascot = UnityEngine.Object.FindAnyObjectByType<GreenhouseMascot>(FindObjectsInactive.Include);
        var field = typeof(GreenhouseMascot).GetField("picture", BindingFlags.Instance | BindingFlags.NonPublic);
        var image = mascot != null ? field.GetValue(mascot) as UnityEngine.UI.Image : null;
        return image != null && image.sprite != null ? image.sprite.texture : null;
    }
    static void Look(int mood, string when)
    {
        string name = "GreenhouseMascotFrames_" + MascotFrames.MoodNames[mood];
        Texture planet = PlanetPicture();
        Check(MascotFrames.LoadedMood == mood && MascotFrames.LoadedTexture != null && MascotFrames.LoadedTexture.name == name && MascotTexturesInMemory() == 1 &&
              BoxPortrait() == MascotFrames.LoadedTexture && (planet == null || planet == MascotFrames.LoadedTexture) &&
              DialogueArt.EriSilhouette != null && DialogueArt.EriSilhouette.texture.name == "EriSilhouette_" + MascotFrames.MoodNames[mood],
            $"{when}: Eri is drawn from {name} in the dialogue box{(planet != null ? " and on the planet" : "")}, with its own outline silhouette; only one Eri sheet is in memory " +
            $"(loaded {MascotFrames.LoadedMood}/{(MascotFrames.LoadedTexture != null ? MascotFrames.LoadedTexture.name : "-")}, in memory " +
            string.Join("+", Resources.FindObjectsOfTypeAll<Texture2D>().Where(t => t != null && t.name.StartsWith("GreenhouseMascotFrames")).Select(t => t.name)) +
            $", box {(BoxPortrait() != null ? BoxPortrait().name : "-")}, planet {(planet != null ? planet.name : "-")}, outline {(DialogueArt.EriSilhouette != null ? DialogueArt.EriSilhouette.texture.name : "-")})");
        UiLook(mood, when);
    }

    // C adımı: üzgün aşamada konuşma kutusunun arkası %80 griye solar (ortak gölgelendirici değeri), mutlu ve kırmızı aşamada normal
    // renkler. Yalnız konuşma kutusunun plakaları solan gölgelendiriciyi (UI/ComicMood) kullanır; bilgi pencereleri değişmez.
    static void UiLook(int mood, string when)
    {
        float want = mood == 1 ? UiMood.SadGrey : 0f;
        var plates = D.BigBox.GetComponentsInChildren<DialoguePlateGraphic>(true);
        var knockout = D.BigBox.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(i => i.gameObject.name == "Rim Knockout");
        var rimColour = D.BigBox.GetComponentsInChildren<UnityEngine.UI.Image>(true).FirstOrDefault(i => i.gameObject.name == "Rim Color");
        var probe = new GameObject("UiMood probe", typeof(RectTransform)).AddComponent<ComicPopupPlate>();
        string popupShader = probe.material != null ? probe.material.shader.name : "-";
        UnityEngine.Object.DestroyImmediate(probe.gameObject);
        Check(Mathf.Abs(UiMood.Applied - want) < .001f && Mathf.Abs(UiMood.Grey - want) < .001f && plates.Length > 0 &&
              plates.All(p => p.material != null && p.material.shader.name == "UI/ComicMood") && popupShader != "UI/ComicMood" &&
              knockout != null && knockout.material.shader.name == "UI/ComicMood" && rimColour != null && rimColour.material.shader.name != "UI/ComicMood",
            $"{when}: dialogue grey {UiMood.Applied:0.00} (want {want:0.00}); {plates.Length} dialogue plates and the paper-coloured rim knockout use UI/ComicMood, " +
            $"the blue rim keeps its colour ({(rimColour != null ? rimColour.material.shader.name : "-")}), other UI plates keep the plain UI shader (info popup {popupShader})");
    }

    // Gözle bakmak için ekran görüntüsü (Logs/Story/…png): ekran kaplamalı tuvaller bir anlığına kameraya çizilir.
    // Gölgelendirici derlenmezse (magenta piksel) test düşer.
    static void Capture(string name)
    {
        Directory.CreateDirectory("Logs/Story");
        Camera camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindFirstObjectByType<Camera>();
        bool made = camera == null;
        if (made)
        {
            camera = new GameObject("Story capture camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.06f, .08f, .09f);
        }
        Canvas[] canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
        foreach (Canvas c in canvases) { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = camera.nearClipPlane + .01f; }
        var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32); target.Create();
        RenderTexture old = RenderTexture.active;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases();
        RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
        RenderTexture.active = target;
        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
        File.WriteAllBytes("Logs/Story/" + name, image.EncodeToPNG());
        int magenta = image.GetPixels32().Count(p => p.r > 240 && p.b > 240 && p.g < 15);
        camera.targetTexture = null; RenderTexture.active = old; target.Release();
        UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
        foreach (Canvas c in canvases) c.renderMode = RenderMode.ScreenSpaceOverlay;
        if (made) UnityEngine.Object.DestroyImmediate(camera.gameObject);
        Check(magenta < 100, $"{name}: captured, no shader error pixels ({magenta} magenta)");
    }

    // Hazırlık ekranına gel (round sonu: CurrentRound = biten round).
    static void Prep(int finishedRound) { SetRound(finishedRound); D.ForceState(GameStates.Round); D.ForceState(GameStates.RoundEnd); }

    // Açık konuşmayı sonuna kadar oynatır: her satırın yazısı toplanır; şık çıkınca verilen cevap seçilir.
    static List<string> Talk(int choice = 0)
    {
        var lines = new List<string>();
        DialogueBox box = D.BigBox;
        if (D.Current != null) order.Add(D.Current.id);
        string last = null;
        for (int guard = 0; guard < 200 && box.IsOpen; guard++)
        {
            if (box.ChoicePending) { lines.Add("[" + box.ChoiceText(0) + " | " + box.ChoiceText(1) + "]"); box.Choose(choice); continue; }
            string shown = box.ShownText;
            if (shown != last) { lines.Add(shown); last = shown; }
            box.Advance();
        }
        Check(!box.IsOpen, "conversation ends");
        return lines;
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (next == 0) { next = EditorApplication.timeSinceStartup + 2; started = next; return; }
        if (EditorApplication.timeSinceStartup < next) { EditorApplication.QueuePlayerLoopUpdate(); return; }
        try
        {
            if (EditorApplication.timeSinceStartup - started > 300) throw new Exception("timeout step " + step);
            if (step == -1)
            {
                if (double.IsNaN((double)Lab("Lab"))) return;
                Run();
                Finish(null);
                return;
            }
        }
        catch (Exception e) { Finish(e); }
    }

    static void Run()
    {
        step = 0;
        Check(MetaSave.LastLoad == MetaSave.LoadResult.MemoryOnly, "batch keeps the save in memory (the real save is never touched)");
        DialogueDirector.BatchEnabled = true;
        DialogueDirector.Paused = true;   // elle sürülen saat
        DialogueDirector d = DialogueDirector.Ensure();
        var catalog = Resources.Load<DialogueCatalogSO>(DialogueCatalogSO.ResourcePath);
        d.UseCatalog(catalog);
        Check(DialogueProgress.StoryOrder.All(id => catalog.entries.Any(e => e.id == id && e.trigger == DialogueTrigger.StoryBeat && e.category == DialogueCategory.Story)),
            "the catalog holds all 14 story conversations (story beat trigger)");
        Check(catalog.entries.SelectMany(e => e.lines).SelectMany(l => new[] { l.textKey, l.choiceA, l.choiceB, l.replyA, l.replyB })
                .Where(k => !string.IsNullOrEmpty(k)).All(GameText.Has), "every story line, answer and reply is in the text list");
        // Öğretim ve tepkiler bu testin konusu değil: görülmüş sayılır.
        foreach (DialogueEntry e in catalog.entries) if (e.trigger != DialogueTrigger.StoryBeat) DialogueProgress.MarkSeen(e.id);
        Check(DialogueProgress.Stage == 0 && DialogueProgress.NextStory() == null && RM.MaxRounds == 50 && RM.IsBossRound(3) && !RM.IsBossRound(2),
            "fresh story: stage 0, nothing reached; R50 calendar with the first Trial at round 3");
        Look(0, "stage 0");
        Vector2 size = MascotFrames.Size(MascotFrames.Front);
        Check(size.y > 250f && size.y < 330f && Mathf.Abs(size.x / size.y - 0.98f) < .15f,
            $"the 2880 px sheets are sized on the 1254 reference: front frame {size.x:0}×{size.y:0} (the old sheet's was 295×289), so Eri keeps her size");
        d.ForceRunStarted();
        d.ForceState(GameStates.RunSetup);
        Check(Id == null, "run start: no story yet");

        // R2 hazırlığı: taş yok.
        Prep(2);
        Check(Id == null, "after round 2 (no Trial yet): no story");

        // 1) İlk Sınama geçildi (R3) — cevap B ("Emin misin?") kaydedilir.
        Prep(3);
        Check(Id == DialogueProgress.TrialPassed && DialogueProgress.Stage == 1, "after the first Trial (R3): story 1 opens");
        List<string> t1 = Talk(choice: 1);
        Check(t1.Any(l => l.Contains("Peki, öyle olsun.") && l.Contains("Emin misin?")) && t1.Any(l => l.StartsWith("Iıımm")) && !t1.Any(l => l.StartsWith("Bana güvendiğin")) &&
              t1.Last().StartsWith("Çok güçlenme ama") && DialogueProgress.Choice(DialogueProgress.TrialPassed) == "B",
            "story 1: two answer buttons; answer 2 gets its own reply; the conversation goes on; the answer is saved (" + string.Join(" / ", t1) + ")");
        // Aynı anda ilk ödül de alındı: aynı sakin anda ikinci hikâye çıkmaz.
        DialogueProgress.AddMilestone(DialogueProgress.FirstReward);
        d.ForceStoryCheck();
        Check(Id == null, "one story per calm moment: the first-reward story waits for the next preparation screen");
        d.ForceState(GameStates.Shop); d.ForceState(GameStates.RoundEnd);
        Check(Id == null, "coming back from the shop is the same moment: still nothing");

        // 2) İlk ödül (R4 hazırlığı = R5'ten önceki ekran: R5 taşı da kazanıldı; sıra korunur, önce ödül).
        Prep(4);
        Check(Id == DialogueProgress.FirstReward && DialogueProgress.HasMilestone("story.r5"), "before round 5: the first-reward story comes first (order kept), R5 already reached");
        Talk();
        Prep(5);
        Check(Id == "story.r5", "next preparation screen: the R5 story");
        Talk();

        // Uzun sıçrama: tek seferde R25'e kadar taş kazanılır; konuşmalar atlanmadan, her hazırlıkta bir tane, sırayla gelir.
        Prep(24);
        Check(Id == "story.r10" && DialogueProgress.HasMilestone("story.r25") && DialogueProgress.Stage == 1, "jumping ahead to R25: the next unseen story (R10) plays, none is skipped");
        Talk();
        Prep(25); Check(Id == "story.r15", "then R15"); Talk();
        Prep(26); Check(Id == "story.r20" && DialogueProgress.Stage == 1, "then R20, Eri still happy"); Capture("story_ui_1_happy.png"); Talk();
        Prep(27);
        Check(Id == DialogueProgress.SadBeat && DialogueProgress.Stage == DialogueProgress.Sad, "the R25 story opens with Eri grey and sad (stage 2 at that moment)");
        Look(1, "R25 story");
        Capture("story_ui_2_sad.png");
        List<string> r25 = Talk(choice: 0);
        Check(r25.Any(l => l.Contains("Ne sorusu?")) && r25.Any(l => l.StartsWith("…Boş ver. Hasat et.")) && DialogueProgress.Choice(DialogueProgress.SadBeat) == null,
            "story 7: its answer changes only the reply, nothing is saved");

        // R30–R50.
        Prep(45); Check(Id == "story.r30", "R30"); Talk();
        Prep(46); Check(Id == "story.r35", "R35"); Talk();
        Prep(47); Check(Id == "story.r40", "R40"); Talk();
        Prep(48); Check(Id == "story.r45", "R45"); Talk();
        Prep(49); Check(Id == "story.r50" && DialogueProgress.HasMilestone("story.r50"), "before round 50 (the last Trial): R50 story"); Talk();
        Prep(49); Check(Id == null, "same moment again: nothing (every story plays once)");

        // Kayıp run sonu: hikâye yok.
        SetOutcome(RunOutcome.QuotaFailed); D.ForceState(GameStates.RunComplete);
        Check(Id == null && !DialogueProgress.HasMilestone(DialogueProgress.Victory), "a lost run end shows no story");

        // İlk tam zafer: end.victory değil itiraf; 2. satırda kırmızı; Taş 1'in cevabına göre satır.
        d.ForceRunStarted(); d.ForceState(GameStates.RunSetup); SetRound(50); D.ForceState(GameStates.Round);
        SetOutcome(RunOutcome.Victory); D.ForceState(GameStates.RunComplete);
        Check(Id == DialogueProgress.Victory && DialogueProgress.Stage == DialogueProgress.Sad, "first full victory: the confession opens (not the short victory talk), line 1 still grey");
        DialogueBox box = D.BigBox;
        for (int i = 0; i < 6 && DialogueProgress.Stage < DialogueProgress.Revealed; i++) box.Advance();
        Check(DialogueProgress.Stage == DialogueProgress.Revealed && box.ShownText.StartsWith("O zaman oyun bitti"), "line 2: the mask falls, Eri turns red (stage 3)");
        Look(2, "confession line 2");
        Capture("story_ui_3_red.png");
        List<string> victory = Talk();
        Check(victory.Any(l => l.StartsWith("Benden şüphelenmiştin")) && !victory.Any(l => l.StartsWith("Bana güvenmiştin")) &&
              victory.Any(l => l.StartsWith("Evet. Dünya'nın çekirdeğini")) && victory.Last().StartsWith("Hasat et. Bir sonraki gezegen"),
            "the confession calls back the saved answer (doubt) and skips the other variant (" + string.Join(" / ", victory) + ")");
        DialogueProgress.AdvanceStage(DialogueProgress.Sad);
        Check(DialogueProgress.Stage == DialogueProgress.Revealed, "the stage never goes back");

        // Zaferden sonraki ilk run başı: kapanış; sonra hikâye biter.
        d.ForceRunStarted(); d.ForceState(GameStates.RunSetup);
        Check(Id == DialogueProgress.AfterVictory, "first run after the victory: the closing story");
        Talk();
        d.ForceRunStarted(); d.ForceState(GameStates.RunSetup);
        Prep(3); Prep(49);
        Check(Id == null && DialogueProgress.NextStory() == null, "the story is over: nothing more, nothing repeats");
        Check(order.SequenceEqual(DialogueProgress.StoryOrder) && DialogueProgress.StoryOrder.All(DialogueProgress.Seen),
            "all 14 stories played exactly once, in order: " + string.Join(", ", order));
        // İkinci zafer: kısa zafer konuşması (kırmızı Eri'nin açlığı).
        MetaSave.Data.seenDialogue.Remove("end.victory");
        typeof(DialogueProgress).GetField("cached", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        SetOutcome(RunOutcome.Victory); D.ForceState(GameStates.RunComplete);
        Check(Id == "end.victory", "a later victory plays the short victory talk instead");
        Talk();
    }

    static void Finish(Exception e)
    {
        SessionState.SetBool(Key, false);
        DialogueDirector.Paused = false;
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/StoryVerification.txt", new[] { e == null ? "PASS " + notes.Count + " checks" : "FAIL " + e }.Concat(notes));
        EditorApplication.Exit(e == null ? 0 : 1);
    }
}
