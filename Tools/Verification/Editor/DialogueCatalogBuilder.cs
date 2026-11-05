using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Diyalog kataloğu asset'i (Faz 1 deneme satırları; Faz 2 öğretim satırları T1–T11, 26 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method DialogueCatalogBuilder.BuildBatch -Full
// Çıktı: Assets/Resources/Dialogue/DialogueCatalog.asset; ana projeye kopyalanır. Sonraki düzenlemeler Inspector'dan yapılabilir.
// Metinler Assets/Localization/GameText.csv'de; burada yalnız anahtarlar.
public static class DialogueCatalogBuilder
{
    const string AssetPath = "Assets/Resources/Dialogue/DialogueCatalog.asset";

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
            // Kopyadaki çeviri tabloları listeyle aynı olsun (satır denetimi tablodan okur).
            LocalizationSetup.Run();
            GameText.Reload();
            if (!AssetDatabase.IsValidFolder("Assets/Resources/Dialogue")) AssetDatabase.CreateFolder("Assets/Resources", "Dialogue");
            var catalog = AssetDatabase.LoadAssetAtPath<DialogueCatalogSO>(AssetPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<DialogueCatalogSO>(); AssetDatabase.CreateAsset(catalog, AssetPath); }
            catalog.entries = new[]
            {
                // Öğretim (T1–T11, Faz 2 taslak metinler: Docs/DiyalogPlani/Faz2-Durum.md). Kimlikler kayıtta tutulur: değiştirilmez.
                Entry("intro.select", DialogueCategory.Guide, DialogueTrigger.SelectionOpened, DialogueSize.Big, Line("intro.select.0", 8), Line("intro.select.1", 9), Line("intro.select.2", 10)),
                Entry("tut.setup", DialogueCategory.Guide, DialogueTrigger.RunSetupOpened, DialogueSize.Big, Line("tut.setup.0", 9)),
                Entry("tut.place", DialogueCategory.Guide, DialogueTrigger.PlacingEntered, DialogueSize.Big, Line("tut.place.0", 10)),
                Entry("tut.hit", DialogueCategory.Guide, DialogueTrigger.RoundStarted, DialogueSize.Small, Line("tut.hit.0", 8), Line("tut.hit.1", 8)),
                Require(Entry("tut.adak", DialogueCategory.Guide, DialogueTrigger.RoundIdle, DialogueSize.Small, Line("tut.adak.0", 9)), "tut.hit"),
                Entry("tut.level", DialogueCategory.Guide, DialogueTrigger.LevelUp, DialogueSize.Big, Line("tut.level.0", 10)),
                Entry("tut.lastround", DialogueCategory.Guide, DialogueTrigger.PeriodLastRound, DialogueSize.Small, Line("tut.lastround.0", 12)),
                Entry("tut.period", DialogueCategory.Guide, DialogueTrigger.PeriodClosed, DialogueSize.Big, Line("tut.period.0", 11), Line("tut.period.1", 8)),
                Priority(Entry("tut.boss", DialogueCategory.Guide, DialogueTrigger.BossRound, DialogueSize.Small, Line("tut.boss.0", 9)), 10),
                Entry("tut.kapris", DialogueCategory.Guide, DialogueTrigger.FieldEventPrepared, DialogueSize.Small, Line("tut.kapris.0", 12)),
                Outcome(Entry("end.quota", DialogueCategory.Guide, DialogueTrigger.RunEnded, DialogueSize.Big, Line("end.quota.0", 13), Line("end.quota.1", 8)), RunOutcome.QuotaFailed),
                Outcome(Entry("end.boss", DialogueCategory.Guide, DialogueTrigger.RunEnded, DialogueSize.Big, Line("end.boss.0", 13), Line("end.boss.1", 8)), RunOutcome.BossFailed),
                // İlk tam zaferde hikâyenin itirafı (story.victory) oynar; bu kısa konuşma sonraki zaferde (kırmızı Eri'nin açlığı).
                Require(Outcome(Entry("end.victory", DialogueCategory.Guide, DialogueTrigger.RunEnded, DialogueSize.Big, Line("end.victory.0", 11), Line("end.victory.1", 12)), RunOutcome.Victory), "story.victory"),
                // Gezegendeki Eri'nin kısa tepkileri (tekrar eder).
                Repeat(Entry("react.setup", DialogueCategory.Reaction, DialogueTrigger.RunSetupOpened, DialogueSize.Planet, Line("react.setup.0", 8)), 300f),
                Require(Repeat(Entry("react.period", DialogueCategory.Reaction, DialogueTrigger.PeriodClosed, DialogueSize.Planet, Line("react.period.0", 8)), 120f), "tut.period"),
                // Hikâye (Faz 3, kesin plan v3: Docs/DiyalogPlani/Faz3-KilometreTaslari-Taslak.md). 14 taş, sırası DialogueProgress.StoryOrder;
                // her biri bir kez. Kimlikler kayıtta tutulur: değiştirilmez.
                Story("story.trial1", Line("story.trial1.0"),
                    Choice(Line("story.trial1.1"), "story.trial1.choice.a", "story.trial1.reply.a", "story.trial1.choice.b", "story.trial1.reply.b", save: true),
                    Line("story.trial1.2")),
                Story("story.reward1", Line("story.reward1.0"), Line("story.reward1.1")),
                Story("story.r5", Line("story.r5.0"), Line("story.r5.1")),
                Story("story.r10", Line("story.r10.0"), Line("story.r10.1")),
                Story("story.r15", Line("story.r15.0"), Line("story.r15.1")),
                Story("story.r20", Line("story.r20.0"), Line("story.r20.1")),
                Story("story.r25", Line("story.r25.0"), Line("story.r25.1"),
                    Choice(Line("story.r25.2"), "story.r25.choice.a", "story.r25.reply.a", "story.r25.choice.b", "story.r25.reply.b", save: false)),
                Story("story.r30", Line("story.r30.0"), Line("story.r30.1")),
                Story("story.r35", Line("story.r35.0"), Line("story.r35.1")),
                Story("story.r40", Line("story.r40.0"), Line("story.r40.1")),
                Story("story.r45", Line("story.r45.0"), Line("story.r45.1")),
                Story("story.r50", Line("story.r50.0"), Line("story.r50.1")),
                // Zafer itirafı: 1. satır üzgün, 2. satırda maske düşer (aşama 3, kırmızı); 4. satır Taş 1'in seçimine göre.
                Story("story.victory", Line("story.victory.0"), Stage(Line("story.victory.1"), 3), Line("story.victory.2"),
                    OnlyIf(Line("story.victory.3a"), "story.trial1:A"), OnlyIf(Line("story.victory.3b"), "story.trial1:B"),
                    Line("story.victory.4"), Line("story.victory.5")),
                Story("story.after", Line("story.after.0"), Line("story.after.1")),
            };
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            foreach (DialogueEntry e in catalog.entries)
                foreach (DialogueLine l in e.lines)
                    foreach (string key in new[] { l.textKey, l.choiceA, l.choiceB, l.replyA, l.replyB })
                        if (!string.IsNullOrEmpty(key) && !GameText.Has(key)) throw new Exception("missing text " + key);
            log.Add("PASS");
            log.Add($"{catalog.entries.Length} entries, {GameText.Count} text keys");
            string csv = GameText.SourceCsv;
            log.Add("missing en: " + string.Join(", ", GameText.Missing(csv, "en")));
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/DialogueCatalogBuilder.txt", log);
        EditorApplication.Exit(log.Count > 0 && log[0] == "PASS" ? 0 : 1);
    }

    static DialogueLine Line(string key, int face) => new DialogueLine { textKey = key, face = face };
    static DialogueLine Line(string key) => Line(key, 8);
    static DialogueLine Stage(DialogueLine l, int stage) { l.setStage = stage; return l; }
    static DialogueLine OnlyIf(DialogueLine l, string choice) { l.onlyIfChoice = choice; return l; }
    static DialogueLine Choice(DialogueLine l, string a, string replyA, string b, string replyB, bool save)
    {
        l.choiceA = a; l.replyA = replyA; l.choiceB = b; l.replyB = replyB; l.saveChoice = save;
        return l;
    }
    static DialogueEntry Story(string id, params DialogueLine[] lines) =>
        Entry(id, DialogueCategory.Story, DialogueTrigger.StoryBeat, DialogueSize.Big, lines);

    static DialogueEntry Entry(string id, DialogueCategory category, DialogueTrigger trigger, DialogueSize size, params DialogueLine[] lines) =>
        new DialogueEntry { id = id, category = category, trigger = trigger, size = size, lines = lines };

    static DialogueEntry Require(DialogueEntry e, string seen) { e.requiresSeen = seen; return e; }
    static DialogueEntry Outcome(DialogueEntry e, RunOutcome outcome) { e.outcome = outcome; return e; }
    static DialogueEntry Priority(DialogueEntry e, int priority) { e.priority = priority; return e; }
    static DialogueEntry Repeat(DialogueEntry e, float cooldown) { e.frequency = DialogueFrequency.Repeat; e.cooldownSeconds = cooldown; return e; }
}
