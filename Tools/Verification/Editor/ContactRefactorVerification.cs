using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class ContactRefactorVerification
{
    static int checks;
    static readonly List<string> notes = new();
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++; notes.Add("ok: " + message);
    }

    public static void RunBatch()
    {
        try
        {
            Geometry(); Persistence(); CacheAndText();
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/ContactRefactorVerification.txt", new[] { $"PASS: {checks} checks" }.ConcatLines(notes));
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/ContactRefactorVerification.txt", "FAIL: " + e + "\n" + string.Join("\n", notes));
            EditorApplication.Exit(1);
        }
    }

    static IEnumerable<string> ConcatLines(this string[] header, List<string> body)
    { foreach (var s in header) yield return s; foreach (var s in body) yield return s; }

    static void Geometry()
    {
        var grid = new GridSystem(11, 11, 2f);
        var hits = new List<GridObject>(121);
        Vector3 center = new Vector3(10, 0, 10);
        grid.GetGridObjectsInRadius(center, 1f, hits);
        Check(hits.Count == 5, "Exact edge contact hits centre + four adjacent cells, not diagonal");
        grid.GetGridObjectsInRadius(center, .99f, hits);
        Check(hits.Count == 1, "Separated edge is excluded");
        grid.GetGridObjectsInRadius(center, 1.42f, hits);
        Check(hits.Count == 9, "Corner contact includes diagonal even though cell centre is outside circle");
        Check(grid.HarvestReach(1f) == 1f, "Ring uses raw radius, no extra half cell");
        var rng = new System.Random(412);
        for (int n = 0; n < 150; n++)
        {
            Vector3 c = new Vector3((float)rng.NextDouble() * 26 - 3, 5, (float)rng.NextDouble() * 26 - 3);
            float r = (float)rng.NextDouble() * 5;
            grid.GetGridObjectsInRadius(c, r, hits);
            int expected = 0;
            for (int x = 0; x < 11; x++) for (int z = 0; z < 11; z++)
            {
                // Independent closest-point oracle, including off-grid circle centres.
                float dx = c.x - Mathf.Clamp(c.x, x * 2 - 1, x * 2 + 1);
                float dz = c.z - Mathf.Clamp(c.z, z * 2 - 1, z * 2 + 1);
                if (dx * dx + dz * dz <= r * r + 1e-6f) expected++;
            }
            Check(hits.Count == expected, "Bounded query matches full-grid oracle " + n);
        }
        foreach (float r in new[] { .75f, 1f, 1.3f, 1.42f, 1.84f, 2.2f, 3f })
        {
            int best = 0;
            foreach (Vector3 offset in new[] { Vector3.zero, new Vector3(1, 0, 0), new Vector3(1, 0, 1) })
            { grid.GetGridObjectsInRadius(center + offset, r, hits); best = Math.Max(best, hits.Count); }
            Check(best == HarvestArea.BestCells(r, 2f), "Fog counting and targeting agree " + r);
        }
        for (int i = 0; i < 100; i++) grid.GetGridObjectsInRadius(center, 2f, hits);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) grid.GetGridObjectsInRadius(center, 2f, hits);
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "10,000 warmed target queries allocate " + bytes + " bytes");
    }

    static void Persistence()
    {
        string root = Path.Combine(Path.GetTempPath(), "ClickerContactTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string blocked = Path.Combine(root, "blocked");
        var quest = ScriptableObject.CreateInstance<QuestSO>(); quest.id = "retry-test"; quest.target = 5;
        var farmer = ScriptableObject.CreateInstance<FarmerSO>(); farmer.id = "retry-farmer";
        try
        {
            File.WriteAllText(blocked, "Block directory creation to simulate an I/O failure");
            MetaSave.UseDirectory(blocked); MetaSave.Load();
            Check(MetaSave.ReportProgress(quest, 2), "Progress updates memory");
            Check(MetaSave.HasPendingChanges, "Progress is dirty, not synchronously written");
            Check(MetaSave.CompleteQuest(quest, new[] { farmer }), "Unlock completes once in memory despite failed storage");
            Check(MetaSave.HasPendingChanges, "Failed write retains pending state");
            File.Delete(blocked); Directory.CreateDirectory(blocked);
            Check(MetaSave.FlushPending() && !MetaSave.HasPendingChanges, "Retry persists pending unlock");
            MetaSave.Load();
            Check(MetaSave.IsQuestDone(quest) && MetaSave.Data.unlocked.Contains(farmer.id), "Unlock survives reload after retry");
            Check(!MetaSave.CompleteQuest(quest, new[] { farmer }), "Retry does not award twice");
        }
        finally
        {
            MetaSave.UseMemoryOnly();
            UnityEngine.Object.DestroyImmediate(quest); UnityEngine.Object.DestroyImmediate(farmer);
            // Only files owned by this unique test directory.
            foreach (string name in new[] { "meta.json", "meta.json.bak", "meta.json.tmp" })
                if (File.Exists(Path.Combine(blocked, name))) File.Delete(Path.Combine(blocked, name));
            if (Directory.Exists(blocked)) Directory.Delete(blocked);
            if (File.Exists(blocked)) File.Delete(blocked);
            Directory.Delete(root);
        }
    }

    static void CacheAndText()
    {
        var go = new GameObject("Stat cache verification");
        var manager = go.AddComponent<StatManager>();
        var core = ScriptableObject.CreateInstance<CoreStatsSO>();
        core.stats.Add(new StatEntry { statType = StatType.HarvestDamage, value = 10 });
        typeof(StatManager).GetField("coreStatsSO", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(manager, core);
        var reward = ScriptableObject.CreateInstance<BossRewardSO>();
        try
        {
            Check(manager.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) == 10, "Cache initial value");
            var modifier = new StatModifier { statType = StatType.HarvestDamage, target = StatTarget.Player, operation = ModifierOperation.Flat, value = 5 };
            manager.AddGlobalModifier(modifier);
            Check(manager.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) == 15, "Cache invalidates after add");
            manager.RemoveGlobalModifier(modifier);
            Check(manager.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) == 10, "Cache invalidates after removal");
            core.stats[0].value = 20;
            Check(manager.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) == 20, "Cache detects edited base value");
            manager.AddGlobalModifier(modifier); manager.ClearGlobalModifiers();
            Check(manager.GetFinalStat(StatType.HarvestDamage, StatTarget.Player) == 20, "Cache invalidates after reset");
            reward.directDamageMultiplier = 1.5f;
            reward.behaviorDamageMultiplier = 1.25f;
            reward.modifiersPerStack.Add(new StatModifier { statType = StatType.CardSkip, operation = ModifierOperation.Set, value = 2 });
            string text = BossRewardText.Value(reward, 3);
            Check(text.Contains("Doğrudan") && text.Contains("Davranış") && text.Contains("= 2,00") && !text.Contains("= 6"), "Composite reward describes every effect; Set does not stack additively");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(core); UnityEngine.Object.DestroyImmediate(reward); }
    }
}
