using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Yerelleştirme çekirdeği doğrulaması (4 Kasım 2026): tek tablo (GameText), denetim, yer tutucu, sahte dil, LocalizedText bileşeni,
// tablo kaydedilince otomatik yenilenme (gerçek CSV dosyası değiştirilip yeniden içe aktarılır) ve sabit yazı taraması.
// Yalnız izole kopyada. Çalıştırma:
//   Tools/run-isolated-verification.ps1 -Method LocalizationVerification.RunBatch -Full
// Çıktı: Logs/SabitYazilar.csv (çeviri listesi taslağı), Logs/LocalizationVerification.txt.
[InitializeOnLoad]
public static class LocalizationVerification
{
    const string Key = "LocalizationVerification", TablePath = "Assets/Resources/Localization/GameText.csv", EditNotes = "Logs/LocalizationVerification_edit.txt";
    static readonly List<string> notes = new();
    static int step;
    static double next;

    static LocalizationVerification() { EditorApplication.update += Tick; }

    static void Check(bool value, string text) { if (!value) throw new Exception(text); notes.Add("PASS " + text); }

    public static void RunBatch()
    {
        if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
        EditorUtility.audioMasterMute = true;
        try
        {
            EditModeChecks();
            ScannerChecks();
        }
        catch (Exception ex) { Finish(ex); return; }
        // Play'e geçişte alan yeniden yüklenir, bellekteki notlar silinir: düzenleme modu sonuçları dosyada bekler.
        Directory.CreateDirectory("Logs");
        File.WriteAllLines(EditNotes, notes);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }

    // ---- edit mode: tablo, denetim, biçim, sahte dil ----
    static void EditModeChecks()
    {
        GameText.UseCsv(null);
        GameText.Language = "tr";
        GameText.Reload();
        Check(GameText.Get("ui.skip") == "Atla" && GameText.Get("tut.hit.0").StartsWith("Hasat"), "the shared table holds the former dialogue lines (Resources/Localization/GameText.csv)");
        Check(Resources.Load<TextAsset>("Dialogue/dialogue_text") == null, "old dialogue table is gone (one list only)");
        Check(GameText.Languages.SequenceEqual(new[] { "tr", "en" }) && !GameText.Has("meta.speed") && Mathf.Approximately(GameText.SpeedScale, 1f),
            "language columns tr+en; meta rows are settings, not text");

        GameTextAudit.Report real = GameTextAudit.Run(GameText.SourceCsv);
        Check(real.Ok, "real table passes the audit: " + string.Join(" | ", real.Errors));
        notes.Add($"INFO real table: {real.Keys} keys, missing en: {real.MissingPerLanguage.GetValueOrDefault("en")}, warnings: {real.Warnings.Count}");

        GameTextAudit.Report bad = GameTextAudit.Run("key,note,tr,en\na,,Bir,One\na,,Yine,Again\nb,,,Two\nc,,{0} altın,{1} gold\nd,,<b>Kalın</b>,Bold\nE x,,Ok,Ok\n");
        Check(bad.Errors.Any(e => e.Contains("yinelenen anahtar: a")) && bad.Errors.Any(e => e.Contains("Türkçe (kaynak) boş: b"))
              && bad.Errors.Any(e => e.Contains("yer tutucu farklı [en]: c")) && bad.Warnings.Any(w => w.Contains("etiket farklı [en]: d"))
              && bad.Warnings.Any(w => w.Contains("anahtar biçimi: E x")), "audit catches duplicate key, empty source, placeholder and tag mismatch, bad key shape");

        GameText.UseCsv("key,note,tr,en\ngold,,{0} altın,{0} gold\nbroken,,{0 bozuk,{0 broken\nmix,,<color=red>{1}</color> / {0},<color=red>{1}</color> / {0}\n");
        Check(GameText.Format("gold", 12) == "12 altın", "placeholder filled: {0} altın → 12 altın");
        Check(GameText.Format("broken", 1) == "{0 bozuk", "a broken pattern does not throw; the raw text is shown");
        GameText.Language = "en";
        Check(GameText.Format("gold", 3) == "3 gold", "same key, English pattern");
        GameText.Language = GameText.PseudoLanguage;
        string pseudo = GameText.Get("mix");
        Check(pseudo.StartsWith("[") && pseudo.EndsWith("]") && pseudo.Contains("<color=red>{1}</color>") && pseudo.Contains("{0}"),
            $"pseudo language keeps placeholders and rich-text tags ({pseudo})");
        string longer = GameText.Pseudo("Round'u başlat");
        Check(longer.Length >= "Round'u başlat".Length * 1.3f && longer.Contains("å") && !longer.Contains("Round"),
            $"pseudo text is accented and ~35 % longer ({longer})");
        Check(!GameText.Languages.Contains(GameText.PseudoLanguage), "pseudo language is not offered as a real language");
        GameText.Language = "tr";
        GameText.UseCsv(null);
    }

    // ---- sabit yazı taraması (çeviri listesi taslağı) ----
    static void ScannerChecks()
    {
        var items = HardcodedTextScanner.Scan();
        HardcodedTextScanner.WriteCsv(items, "Logs/SabitYazilar.csv");
        notes.Add("INFO hard-coded texts: " + items.Count + " (" + HardcodedTextScanner.Summary(items) + ")");
        Check(items.Count > 0 && File.Exists("Logs/SabitYazilar.csv"), "scanner lists hard-coded player texts into a CSV");
        Check(items.All(i => HardcodedTextScanner.IsText(i.Text)) && !HardcodedTextScanner.IsText("12") && !HardcodedTextScanner.IsText(" + ")
              && !HardcodedTextScanner.IsText("") && !HardcodedTextScanner.IsText("B8322A") && HardcodedTextScanner.IsText("START ROUND"),
            "numbers, symbols, colour codes and icon glyphs are not counted as text");
        Check(HardcodedTextScanner.Slug("Saksı Dükkânı!") == "saksi_dukk_ni" || HardcodedTextScanner.Slug("Saksı Dükkânı!").StartsWith("saksi_dukk"),
            "suggested keys are lower-case ASCII");
        Check(items.All(i => !i.Location.Contains("TextMesh Pro")), "TextMesh Pro samples are skipped");
    }

    // ---- play mode: LocalizedText ve tablo kaydedilince yenilenme ----
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            switch (step++)
            {
                case 0: next = EditorApplication.timeSinceStartup + 1; return;
                case 1: PlayChecks(); break;
                case 2: HotReloadChecks(); break;
                default: Finish(null); return;
            }
            next = EditorApplication.timeSinceStartup + .2;
        }
        catch (Exception ex) { Finish(ex); }
    }

    static GameObject canvasObject;

    static TMP_Text MakeLabel(string name, string placeholder)
    {
        if (canvasObject == null) canvasObject = new GameObject("Loc Test Canvas", typeof(Canvas));
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvasObject.transform, false);
        var label = go.GetComponent<TMP_Text>();
        label.text = placeholder;
        return label;
    }

    static void PlayChecks()
    {
        int listenersBefore = Listeners();
        TMP_Text skip = MakeLabel("Skip", "SKIP (placeholder)");
        var loc = skip.gameObject.AddComponent<LocalizedText>();
        loc.SetKey("ui.skip");
        Check(skip.text == "Atla", "LocalizedText pulls its text from the table by key (placeholder replaced)");
        GameText.Language = "en";
        Check(skip.text == "Skip", "language change updates bound labels at once");
        GameText.Language = GameText.PseudoLanguage;
        Check(skip.text.StartsWith("[") && skip.text.Contains("Åtlå"), $"pseudo language shows on bound labels ({skip.text})");
        GameText.Language = "tr";

        GameText.UseCsv("key,note,tr,en\nui.skip,,Geç,Skip\nui.gold,,{0} altın,{0} gold\n");
        Check(skip.text == "Geç", "a changed table updates bound labels (same path as saving the CSV)");
        TMP_Text gold = MakeLabel("Gold", "0 GOLD");
        gold.gameObject.AddComponent<LocalizedText>().SetKey("ui.gold", 25);
        Check(gold.text == "25 altın", "LocalizedText with a value: {0} altın → 25 altın");
        GameText.Language = "en";
        Check(gold.text == "25 gold", "values are kept across a language change");

        skip.gameObject.SetActive(false);
        GameText.Language = "tr";
        Check(skip.text == "Skip", "a disabled label does not listen (no work while hidden)");
        skip.gameObject.SetActive(true);
        Check(skip.text == "Geç", "re-enabled label catches up with the current language");

        Check(typeof(LocalizedText).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null
              && typeof(LocalizedText).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) == null,
            "LocalizedText has no per-frame work");
        Object.Destroy(canvasObject);
        canvasObject = null;
        GameText.UseCsv(null);
        notes.Add($"INFO listeners before {listenersBefore}");
    }

    static void HotReloadChecks()
    {
        Check(Listeners() == baseline(), $"destroyed labels unsubscribe (listeners {Listeners()})");
        TMP_Text hot = MakeLabel("Hot", "?");
        hot.gameObject.AddComponent<LocalizedText>().SetKey("test.hot");
        string original = File.ReadAllText(TablePath);
        try
        {
            File.WriteAllText(TablePath, original.TrimEnd('\r', '\n') + "\ntest.hot,,Sıcak satır,Hot line\n", new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(TablePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Check(GameText.Has("test.hot") && hot.text == "Sıcak satır", $"saving the CSV re-imports it and bound labels change by themselves ({hot.text})");
        }
        finally
        {
            File.WriteAllText(TablePath, original, new System.Text.UTF8Encoding(false));
            AssetDatabase.ImportAsset(TablePath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        Check(!GameText.Has("test.hot"), "restored table is re-imported too");
        Object.Destroy(canvasObject);
        canvasObject = null;
    }

    static int baselineListeners = -1;
    static int baseline() => baselineListeners;

    static int Listeners()
    {
        var field = typeof(GameText).GetField("Changed", BindingFlags.Static | BindingFlags.NonPublic);
        int n = (field?.GetValue(null) as Delegate)?.GetInvocationList().Length ?? 0;
        if (baselineListeners < 0) baselineListeners = n;
        return n;
    }

    static void Finish(Exception ex)
    {
        SessionState.SetBool(Key, false);
        if (ex != null) notes.Add("FAIL " + ex.Message);
        Directory.CreateDirectory("Logs");
        if (EditorApplication.isPlaying && File.Exists(EditNotes)) notes.InsertRange(0, File.ReadAllLines(EditNotes));
        File.WriteAllLines("Logs/LocalizationVerification.txt", notes);
        notes.Add($"SUMMARY {notes.Count(n => n.StartsWith("PASS"))} PASS, {notes.Count(n => n.StartsWith("FAIL"))} FAIL");
        File.AppendAllLines("Logs/LocalizationVerification.txt", new[] { notes[notes.Count - 1] });
        foreach (string n in notes) Debug.Log("[Loc] " + n);
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        EditorApplication.Exit(ex == null ? 0 : 1);
    }
}
