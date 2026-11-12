using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Yetenek ağacı ikon listesi (12 Kasım 2026; kullanıcı: "skill tree'de kullanılan ikonların listesini çıkar, yandan yaptırayım").
// Yalnız OKUR (yetenek verisine dokunmaz). Etkin run profilinin (Lab varsayılanı) ağaç setindeki düğümleri, oyunun ikon
// sınıflandırmasıyla (SkillIconCatalog.For) gruplar; adlar çeviri tablosundan (GameText.csv). Çıktı: Logs/SkillIcons.csv.
// Çalıştırma: Tools/run-isolated-verification.ps1 -Method SkillIconListDump.RunBatch
public static class SkillIconListDump
{
    public static void RunBatch()
    {
        int code = 0;
        try { Dump(); }
        catch (Exception e) { Debug.LogException(e); code = 1; }
        EditorApplication.Exit(code);
    }

    static void Dump()
    {
        var texts = new Dictionary<string, (string en, string tr)>();
        foreach (string line in File.ReadAllLines("Assets/Localization/GameText.csv").Skip(1))
        {
            var cells = SplitCsv(line);
            if (cells.Count >= 4) texts[cells[0]] = (cells[2], cells[3]);
        }
        var profiles = AssetDatabase.FindAssets("t:RunProfileSO").Select(g => AssetDatabase.LoadAssetAtPath<RunProfileSO>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null).ToList();
        RunProfileSO profile = profiles.FirstOrDefault(p => p.name == MechanicsTestLab.Lab.DefaultProfile);
        if (profile == null || profile.balance == null || profile.balance.skillTree == null) throw new Exception("default profile / skill tree set not found");
        List<SkillNodeSO> nodes = profile.balance.skillTree.Nodes();
        var sb = new StringBuilder("icon,node_asset,name_tr,name_en,unlock,first_stat,tiers\n");
        foreach (var node in nodes.Where(n => n != null).OrderBy(n => SkillIconCatalog.For(n)).ThenBy(n => n.gridPosition.y).ThenBy(n => n.gridPosition.x))
        {
            string key = node.DisplayNameKey;
            var tr = key != null && texts.TryGetValue(key, out var t) ? t.tr : node.nodeName;
            var en = key != null && texts.TryGetValue(key, out var t2) ? t2.en : node.nodeName;
            string firstStat = node.tiers?.SelectMany(x => x.effects ?? new List<StatModifier>()).Select(e => e.statType.ToString()).FirstOrDefault() ?? "";
            sb.Append(SkillIconCatalog.For(node)).Append(',').Append(Csv(node.name)).Append(',').Append(Csv(tr)).Append(',').Append(Csv(en)).Append(',')
              .Append(node.unlockType).Append(',').Append(firstStat).Append(',').Append(node.tiers?.Count ?? 0).Append('\n');
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/SkillIcons.csv", sb.ToString(), new UTF8Encoding(false));
        Debug.Log($"SkillIconListDump: profile {profile.name}, set {profile.balance.skillTree.name}, {nodes.Count} nodes");
    }

    static string Csv(string s) => s != null && (s.Contains(',') || s.Contains('"')) ? "\"" + s.Replace("\"", "\"\"") + "\"" : s ?? "";

    static List<string> SplitCsv(string line)
    {
        var result = new List<string>(); var cur = new StringBuilder(); bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted) { if (c == '"') { if (i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else quoted = false; } else cur.Append(c); }
            else if (c == '"') quoted = true;
            else if (c == ',') { result.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        result.Add(cur.ToString());
        return result;
    }
}
