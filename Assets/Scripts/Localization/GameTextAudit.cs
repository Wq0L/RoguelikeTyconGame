using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Çeviri tablosunun denetimi (4 Kasım 2026): editör menüsü (Tools > Çeviri > Tabloyu denetle) ve testler kullanır; oyunda çalışmaz.
// Hata: yinelenen anahtar, Türkçe (kaynak) hücresi boş, bir dilde yer tutucu ({0}) kaynakla aynı değil.
// Uyarı: zengin metin etiketleri (<color> vb.) kaynakla aynı değil, anahtar biçimi (küçük harf, rakam, . _ -) dışında.
public static class GameTextAudit
{
    public sealed class Report
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public readonly Dictionary<string, int> MissingPerLanguage = new();
        public int Keys;
        public bool Ok => Errors.Count == 0;
    }

    private static readonly Regex Placeholder = new(@"\{(\d+)(?:[,:][^}]*)?\}");
    private static readonly Regex Tag = new(@"</?([a-zA-Z#][\w\-]*)");
    private static readonly Regex KeyShape = new(@"^[a-z0-9][a-z0-9._\-]*$");

    public static Report Run(string csv)
    {
        var report = new Report();
        if (string.IsNullOrEmpty(csv)) { report.Errors.Add("tablo boş ya da bulunamadı"); return report; }
        List<List<string>> rows = GameText.Parse(csv);
        if (rows.Count == 0) { report.Errors.Add("tablo boş"); return report; }
        List<string> header = rows[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        if (header.Count == 0 || header[0] != "key") report.Errors.Add("ilk sütun 'key' olmalı");
        int source = header.IndexOf(GameText.SourceLanguage);
        if (source < 1) { report.Errors.Add("kaynak dil sütunu yok: " + GameText.SourceLanguage); return report; }
        var langColumns = new List<int>();
        for (int c = 1; c < header.Count; c++)
            if (GameText.IsLanguageColumn(header[c]) && c != source) { langColumns.Add(c); report.MissingPerLanguage[header[c]] = 0; }

        var seen = new HashSet<string>();
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;
            string key = row[0].Trim();
            if (!seen.Add(key)) { report.Errors.Add($"yinelenen anahtar: {key} (satır {r + 1})"); continue; }
            bool isMeta = key.StartsWith(GameText.MetaPrefix, StringComparison.Ordinal);
            if (!isMeta) report.Keys++;
            if (!KeyShape.IsMatch(key)) report.Warnings.Add($"anahtar biçimi: {key} (küçük harf, rakam ve . _ - kullanın)");
            string src = Cell(row, source);
            if (src.Length == 0) { report.Errors.Add($"Türkçe (kaynak) boş: {key}"); continue; }
            if (isMeta) continue;
            string srcHoles = Holes(src), srcTags = Tags(src);
            foreach (int c in langColumns)
            {
                string text = Cell(row, c);
                if (text.Length == 0) { report.MissingPerLanguage[header[c]]++; continue; }
                if (Holes(text) != srcHoles) report.Errors.Add($"yer tutucu farklı [{header[c]}]: {key} (kaynak {srcHoles}, çeviri {Holes(text)})");
                if (Tags(text) != srcTags) report.Warnings.Add($"etiket farklı [{header[c]}]: {key}");
            }
        }
        return report;
    }

    // Yer tutucu numaraları sıralı ve tekil: "{1} … {0} … {0}" → "0,1".
    private static string Holes(string text)
    {
        var set = new SortedSet<int>();
        foreach (Match m in Placeholder.Matches(text)) set.Add(int.Parse(m.Groups[1].Value));
        return string.Join(",", set);
    }

    private static string Tags(string text)
    {
        var list = new List<string>();
        foreach (Match m in Tag.Matches(text)) list.Add(m.Value.ToLowerInvariant());
        list.Sort(StringComparer.Ordinal);
        return string.Join(",", list);
    }

    private static string Cell(List<string> row, int column) => column >= 1 && column < row.Count ? row[column].Trim() : "";
}
