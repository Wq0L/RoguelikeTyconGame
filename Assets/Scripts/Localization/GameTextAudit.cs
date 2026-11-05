using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.Localization.SmartFormat;
using UnityEngine.Localization.SmartFormat.Core.Parsing;
using UnityEngine.Localization.SmartFormat.Core.Settings;

// Ana listenin (GameText.csv) denetimi (4–5 Kasım 2026). Liste Unity tablolarına aktarılmadan ÖNCE çalışır: hata varsa aktarım
// yapılmaz, mevcut tablolar korunur (LocalizationSetup.Import). Editör menüsü ve testler de kullanır; oyunda çalışmaz.
// HATA (aktarımı durdurur):
//   - yapı: kapanmamış tırnak, başlıktan fazla sütun, hücrede satır sonu (satır kırmak için <br>), boş anahtarlı dolu satır,
//     başlıkta 'key' ilk sütun değil, kaynak dil sütunu yok, aynı dil sütunu iki kez
//   - yinelenen anahtar; kaynak dil (İngilizce) hücresi boş
//   - yer tutucu: süslü parantez dengesiz; bir çeviride yer tutucular kaynaktan farklı (sayılı {0} ve adlı {player} karşılaştırılır,
//     çoğul gibi biçimli alanlarda ':' öncesindeki seçici: {0:plural:…} → 0)
//   - Smart (çoğul / adlı) metin, Unity Localization'ın SmartFormat ayrıştırıcısında hata veriyor
// UYARI (aktarımı durdurmaz): zengin metin etiketleri (<color> vb.) kaynaktan farklı, anahtar biçimi (küçük harf, rakam, . _ -).
// Eksik çeviri hata değildir: o dilde İngilizce görünür (dil başına eksik sayısı raporlanır).
public static class GameTextAudit
{
    public sealed class Report
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Warnings = new();
        public readonly Dictionary<string, int> MissingPerLanguage = new();
        public int Keys;
        public bool Ok => Errors.Count == 0;
        public string Summary(int max = 5) =>
            Ok ? "hata yok" : string.Join("\n", Errors.GetRange(0, Math.Min(max, Errors.Count))) + (Errors.Count > max ? $"\n… (+{Errors.Count - max})" : "");
    }

    private static readonly Regex Tag = new(@"</?([a-zA-Z#][\w\-]*)");
    private static readonly Regex KeyShape = new(@"^[a-z0-9][a-z0-9._\-]*$");
    private static SmartFormatter strictFormatter;

    public static Report Run(string csv)
    {
        var report = new Report();
        if (string.IsNullOrEmpty(csv)) { report.Errors.Add("liste boş ya da bulunamadı"); return report; }
        List<List<string>> rows = GameText.Parse(csv, out bool unterminated);
        if (unterminated) report.Errors.Add("kapanmamış tırnak: bir hücrenin tırnağı kapanmamış, sonraki satırlar o hücreye yutulmuş");
        if (rows.Count == 0) { report.Errors.Add("liste boş"); return report; }
        List<string> header = rows[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        if (header.Count == 0 || header[0] != "key") report.Errors.Add("ilk sütun 'key' olmalı");
        int source = header.IndexOf(GameText.SourceLanguage);
        if (source < 1) { report.Errors.Add("kaynak dil sütunu yok: " + GameText.SourceLanguage); return report; }
        var langColumns = new List<int>();
        var seenColumns = new HashSet<string>();
        for (int c = 1; c < header.Count; c++)
        {
            if (!GameText.IsLanguageColumn(header[c])) continue;
            if (!seenColumns.Add(header[c])) report.Errors.Add("aynı dil sütunu iki kez: " + header[c]);
            if (c != source) { langColumns.Add(c); report.MissingPerLanguage[header[c]] = 0; }
        }

        var seen = new HashSet<string>();
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            int line = r + 1;
            if (row.Count > header.Count)
                report.Errors.Add($"satır {line}: başlıktan fazla sütun ({row.Count}/{header.Count}); tırnak ya da virgül hatası olabilir");
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
            {
                if (row.Exists(c => c.Trim().Length > 0)) report.Errors.Add($"satır {line}: anahtar boş ama satır dolu");
                continue;
            }
            string key = row[0].Trim();
            for (int c = 0; c < row.Count; c++)
                if (row[c].IndexOf('\n') >= 0 || row[c].IndexOf('\r') >= 0)
                {
                    report.Errors.Add($"satır {line} ({key}): hücrede satır sonu var; satır kırmak için <br> yazın (çoğunlukla kapanmamış tırnak)");
                    break;
                }
            if (!seen.Add(key)) { report.Errors.Add($"yinelenen anahtar: {key} (satır {line})"); continue; }
            bool isMeta = key.StartsWith(GameText.MetaPrefix, StringComparison.Ordinal);
            if (!isMeta) report.Keys++;
            if (!KeyShape.IsMatch(key)) report.Warnings.Add($"anahtar biçimi: {key} (küçük harf, rakam ve . _ - kullanın)");
            string src = Cell(row, source);
            if (src.Length == 0) { report.Errors.Add($"kaynak ({GameText.SourceLanguage}) boş: {key}"); continue; }
            if (isMeta) continue;

            string srcHoles = Placeholders(src, out string srcBrace);
            if (srcBrace != null) report.Errors.Add($"süslü parantez [{GameText.SourceLanguage}]: {key}: {srcBrace}");
            CheckSmart(report, key, GameText.SourceLanguage, src);
            string srcTags = Tags(src);
            foreach (int c in langColumns)
            {
                string text = Cell(row, c);
                if (text.Length == 0) { report.MissingPerLanguage[header[c]]++; continue; }
                string holes = Placeholders(text, out string brace);
                if (brace != null) report.Errors.Add($"süslü parantez [{header[c]}]: {key}: {brace}");
                else if (srcBrace == null && holes != srcHoles)
                    report.Errors.Add($"yer tutucu farklı [{header[c]}]: {key} (kaynak {Show(srcHoles)}, çeviri {Show(holes)})");
                CheckSmart(report, key, header[c], text);
                if (Tags(text) != srcTags) report.Warnings.Add($"etiket farklı [{header[c]}]: {key}");
            }
        }
        // Yalnız başlık (ya da yalnız meta satırı) kalmış liste: aktarılsaydı bütün anahtarlar "listeden kaldırılmış" sayılıp silinirdi.
        if (report.Keys == 0) report.Errors.Add("listede hiç metin satırı yok (yalnız başlık): dosya yanlışlıkla boşaltılmış olabilir");
        return report;
    }

    private static string Show(string holes) => holes.Length == 0 ? "yok" : "{" + holes.Replace(",", "} {") + "}";

    // Bütün seviyelerdeki yer tutucu seçicileri, sıralı ve tekil: "{1} … {player} … {0:plural:1 {name}|{} {name}s}" →
    // "0,1,name,player". Seçici ':' ya da ',' öncesidir ({0:0.0}, {0,5} → 0). Çoğul seçeneklerindeki boş {} (o anki değer) sayılmaz.
    // İç içe adlı alan da karşılaştırılır: çoğulun içindeki {player} çeviride {name} olursa fark yakalanır. Üst seviyede {{ ve }}
    // düz yazıdır. brace: parantez dengesizse açıklama, değilse null.
    public static string Placeholders(string text, out string brace)
    {
        brace = null;
        var set = new SortedSet<string>(StringComparer.Ordinal);
        var starts = new Stack<int>();
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (starts.Count == 0 && i + 1 < text.Length && (c == '{' && text[i + 1] == '{' || c == '}' && text[i + 1] == '}')) { i++; continue; }
            if (c == '{') { starts.Push(i + 1); continue; }
            if (c != '}') continue;
            if (starts.Count == 0) { brace = $"kapanış '}}' açılışsız (konum {i})"; return string.Join(",", set); }
            int start = starts.Pop();
            string inner = text.Substring(start, i - start);
            int cut = inner.IndexOfAny(new[] { ':', ',', '{' });
            string selector = (cut >= 0 ? inner.Substring(0, cut) : inner).Trim();
            if (selector.Length > 0) set.Add(selector);
        }
        if (starts.Count != 0) brace = "kapanmamış '{'";
        return string.Join(",", set);
    }

    // Smart metin Unity Localization'ın SmartFormat ayrıştırıcısıyla denetlenir (oyunda kullanılan ayrıştırıcının aynısı, hata
    // vermeye ayarlı kopyası).
    private static void CheckSmart(Report report, string key, string lang, string text)
    {
        if (!GameText.LooksSmart(text)) return;
        string error = SmartError(text);
        if (error != null) report.Errors.Add($"Smart metin hatalı [{lang}]: {key}: {error}");
    }

    public static string SmartError(string text)
    {
        if (strictFormatter == null)
        {
            strictFormatter = Smart.CreateDefaultSmartFormat();
            strictFormatter.Settings.ParseErrorAction = ErrorAction.ThrowError;
        }
        try
        {
            strictFormatter.Parser.ParseFormat(text, strictFormatter.GetNotEmptyFormatterExtensionNames());
            return null;
        }
        catch (ParsingErrors e) { return FirstLine(e.Message); }
        catch (Exception e) { return FirstLine(e.Message); }
    }

    private static string FirstLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "ayrıştırma hatası";
        int n = s.IndexOf('\n');
        return n > 0 ? s.Substring(0, n).Trim() : s.Trim();
    }

    private static string Tags(string text)
    {
        var list = new List<string>();
        foreach (Match m in Tag.Matches(text)) list.Add(m.Value.ToLowerInvariant());
        list.Sort(StringComparer.Ordinal);
        var sb = new StringBuilder();
        foreach (string t in list) sb.Append(t).Append(',');
        return sb.ToString();
    }

    private static string Cell(List<string> row, int column) => column >= 1 && column < row.Count ? row[column].Trim() : "";
}
