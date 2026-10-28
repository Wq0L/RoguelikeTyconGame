using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Diyalog kataloğu asset'i (Faz 1 deneme satırları; Faz 2 öğretim satırları T1–T11, 26 Ekim 2026). Yalnız izole kopyada çalışır:
//   Tools/run-isolated-verification.ps1 -Method DialogueCatalogBuilder.BuildBatch -Full
// Çıktı: Assets/Resources/Dialogue/DialogueCatalog.asset; ana projeye kopyalanır. Sonraki düzenlemeler Inspector'dan yapılabilir.
// Metinler Resources/Dialogue/dialogue_text.csv'de; burada yalnız anahtarlar.
public static class DialogueCatalogBuilder
{
    const string AssetPath = "Assets/Resources/Dialogue/DialogueCatalog.asset";

    public static void BuildBatch()
    {
        var log = new List<string>();
        try
        {
            if (!Application.dataPath.Contains("VerificationProject")) throw new Exception("isolated only");
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
                Outcome(Entry("end.victory", DialogueCategory.Guide, DialogueTrigger.RunEnded, DialogueSize.Big, Line("end.victory.0", 11), Line("end.victory.1", 12)), RunOutcome.Victory),
                // Gezegendeki Eri'nin kısa tepkileri (tekrar eder).
                Repeat(Entry("react.setup", DialogueCategory.Reaction, DialogueTrigger.RunSetupOpened, DialogueSize.Planet, Line("react.setup.0", 8)), 300f),
                Require(Repeat(Entry("react.period", DialogueCategory.Reaction, DialogueTrigger.PeriodClosed, DialogueSize.Planet, Line("react.period.0", 8)), 120f), "tut.period"),
            };
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            foreach (DialogueEntry e in catalog.entries)
                foreach (DialogueLine l in e.lines)
                    if (!DialogueText.Has(l.textKey)) throw new Exception("missing text " + l.textKey);
            log.Add("PASS");
            log.Add($"{catalog.entries.Length} entries, {DialogueText.Count} text keys");
            string csv = Resources.Load<TextAsset>(DialogueText.ResourcePath).text;
            log.Add("missing en: " + string.Join(", ", DialogueText.Missing(csv, "en")));
        }
        catch (Exception e) { log.Insert(0, "FAIL " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/DialogueCatalogBuilder.txt", log);
        EditorApplication.Exit(log.Count > 0 && log[0] == "PASS" ? 0 : 1);
    }

    static DialogueLine Line(string key, int face) => new DialogueLine { textKey = key, face = face };

    static DialogueEntry Entry(string id, DialogueCategory category, DialogueTrigger trigger, DialogueSize size, params DialogueLine[] lines) =>
        new DialogueEntry { id = id, category = category, trigger = trigger, size = size, lines = lines };

    static DialogueEntry Require(DialogueEntry e, string seen) { e.requiresSeen = seen; return e; }
    static DialogueEntry Outcome(DialogueEntry e, RunOutcome outcome) { e.outcome = outcome; return e; }
    static DialogueEntry Priority(DialogueEntry e, int priority) { e.priority = priority; return e; }
    static DialogueEntry Repeat(DialogueEntry e, float cooldown) { e.frequency = DialogueFrequency.Repeat; e.cooldownSeconds = cooldown; return e; }
}
