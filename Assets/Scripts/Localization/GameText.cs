using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Oyunun bütün oyuncuya görünen yazıları tek tabloda (4 Kasım 2026; Faz 2'nin diyalog tablosu genelleştirildi).
// Tablo: Resources/Localization/GameText.csv. İlk satır başlık: "key,note,tr,en,de,…". Her satır bir anahtar.
// - Anahtar kalıcıdır, metin değişse de değişmez; kod ve sahneler yalnız anahtarı bilir (yazıyı tablodan çeker).
// - "note" sütunu çevirmen içindir (kim söylüyor, hangi ekran, yer kısıtı, yer tutucuların anlamı); oyunda okunmaz.
//   Dil sütunları ISO kodudur (tr, en, de, pt-br …). Sütun sırası önemli değildir, adıyla bulunur.
// - Kaynak dil Türkçedir. Etkin dilde hücre boşsa önce İngilizceye, o da boşsa Türkçeye düşülür.
// - Metin parça parça birleştirilmez (diller sözcük sırasını değiştirir): değişken gerekirse yer tutucuyla ({0}, {1}) tek
//   anahtarda yazılır ve Format ile doldurulur. Sayı biçimi şimdilik değişmez kültürdedir (ForceInvariantCulture).
// - "meta." ile başlayan satırlar metin değil dil ayarıdır (ör. meta.speed: konuşma yazı hızı çarpanı).
// - Sahte dil (PseudoLanguage, "qps"): tablodan değil kaynak metinden üretilir; harfler aksanlanır, metin ~%35 uzar, köşeli
//   ayraç içine alınır. Çeviriye bağlanmamış (sabit) yazılar ve sığmayan kutular böyle hemen görünür. Ayarlar listesinde yoktur.
// Tablo bir kez yüklenir; dil ya da tablo değişince (editörde CSV kaydedilince, GameTextHotReload) yeniden kurulur ve Changed
// olayı yayınlanır: LocalizedText bileşenleri ve açık konuşma kutuları kendini yeniler. Karede iş ve bellek ayırma yok.
public static class GameText
{
    public const string ResourcePath = "Localization/GameText";
    public const string SourceLanguage = "tr", FallbackLanguage = "en", PseudoLanguage = "qps";
    public const string NoteColumn = "note", MetaPrefix = "meta.", SpeedKey = "meta.speed";
    // Dil sütunu sayılmayan bilgi sütunları (çevirmen notu, yer kısıtı, bağlam).
    private static readonly HashSet<string> InfoColumns = new() { NoteColumn, "context", "max", "where" };

    private static Dictionary<string, string> table;
    private static readonly Dictionary<string, string> meta = new();
    private static readonly HashSet<string> warned = new();
    private static readonly List<string> languages = new();
    private static string language = SourceLanguage;
    private static string sourceOverride;
    private static float speed = 1f;

    // Dil ya da tablo değişti (yazılar yeniden okunmalı).
    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        table = null; meta.Clear(); warned.Clear(); languages.Clear();
        language = SourceLanguage; sourceOverride = null; speed = 1f; Changed = null;
    }

    public static string Language
    {
        get => language;
        set
        {
            string code = string.IsNullOrWhiteSpace(value) ? SourceLanguage : value.Trim().ToLowerInvariant();
            if (code == language) return;
            language = code;
            Reload();
        }
    }

    // Tablodaki dil sütunları (başlık sırasıyla). Ayarlar menüsü bunu listeler. Sahte dil burada yoktur.
    public static IReadOnlyList<string> Languages { get { Ensure(); return languages; } }
    // Konuşma yazı hızı çarpanı (meta.speed; yoğun yazılarda düşük).
    public static float SpeedScale { get { Ensure(); return speed; } }
    public static int Count { get { Ensure(); return table.Count; } }

    public static bool Has(string key) { Ensure(); return key != null && table.ContainsKey(key); }

    public static string Get(string key)
    {
        Ensure();
        if (key != null && table.TryGetValue(key, out string text)) return text;
        if (Application.isEditor && key != null && warned.Add(key)) Debug.LogWarning("Çeviri tablosunda anahtar yok: " + key);
        return key ?? "";
    }

    // Yer tutuculu metin: "{0} altın" → "12 altın". Biçim hatası oyunu durdurmaz, ham metin döner (geliştirmede uyarı).
    public static string Format(string key, params object[] args)
    {
        string pattern = Get(key);
        if (args == null || args.Length == 0) return pattern;
        try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
        catch (FormatException)
        {
            if (Application.isEditor && warned.Add("format:" + key)) Debug.LogWarning("Çeviri yer tutucusu hatalı: " + key + " → " + pattern);
            return pattern;
        }
    }

    // Tablo kaydedildi (editörde) ya da test tabloyu değiştirdi: yeniden kur ve haber ver.
    public static void Reload()
    {
        table = null;
        Changed?.Invoke();
    }

    // Test ve araçlar: tabloyu verilen CSV metninden kurar; null: Resources'taki tabloya döner.
    public static void UseCsv(string csv)
    {
        sourceOverride = csv;
        Reload();
    }

    // Kaynak CSV metni (araçlar ve denetim).
    public static string SourceCsv
    {
        get
        {
            if (sourceOverride != null) return sourceOverride;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            return asset != null ? asset.text : null;
        }
    }

    private static void Ensure()
    {
        if (table != null) return;
        table = new Dictionary<string, string>();
        meta.Clear();
        languages.Clear();
        warned.Clear();
        speed = 1f;
        string csv = SourceCsv;
        if (csv == null) { Debug.LogWarning("Çeviri tablosu bulunamadı: " + ResourcePath); return; }
        bool pseudo = language == PseudoLanguage;
        Load(csv, table, pseudo ? SourceLanguage : language, languages);
        foreach (string key in new List<string>(table.Keys))
            if (key.StartsWith(MetaPrefix, StringComparison.Ordinal)) { meta[key] = table[key]; table.Remove(key); }
            else if (pseudo) table[key] = Pseudo(table[key]);
        if (meta.TryGetValue(SpeedKey, out string scale) && float.TryParse(scale, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0f)
            speed = Mathf.Clamp(s, .2f, 3f);
    }

    public static bool IsLanguageColumn(string header) =>
        header.Length > 0 && !InfoColumns.Contains(header) && header != "key" && !header.StartsWith("_", StringComparison.Ordinal);

    // Verilen CSV metnini bir sözlüğe okur. Her anahtar için etkin dil → İngilizce → Türkçe, ilk dolu hücre.
    public static void Load(string csv, Dictionary<string, string> into, string lang = SourceLanguage, List<string> columns = null)
    {
        List<List<string>> rows = Parse(csv);
        if (rows.Count == 0) return;
        List<string> header = rows[0];
        for (int c = 0; c < header.Count; c++) header[c] = header[c].Trim().ToLowerInvariant();
        if (columns != null)
            for (int c = 1; c < header.Count; c++)
                if (IsLanguageColumn(header[c])) columns.Add(header[c]);
        int active = header.IndexOf(lang), fallback = header.IndexOf(FallbackLanguage), source = header.IndexOf(SourceLanguage);
        if (active < 1 && fallback < 1 && source < 1) return;
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;
            string text = Cell(row, active);
            if (text.Length == 0) text = Cell(row, fallback);
            if (text.Length == 0) text = Cell(row, source);
            into[row[0].Trim()] = text;
        }
    }

    // Çeviri denetimi: verilen dilde boş kalan anahtarlar (meta satırları hariç).
    public static List<string> Missing(string csv, string lang)
    {
        var missing = new List<string>();
        List<List<string>> rows = Parse(csv);
        if (rows.Count == 0) return missing;
        int column = rows[0].FindIndex(h => h.Trim().ToLowerInvariant() == lang);
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]) || row[0].StartsWith(MetaPrefix, StringComparison.Ordinal)) continue;
            if (column < 1 || Cell(row, column).Length == 0) missing.Add(row[0].Trim());
        }
        return missing;
    }

    private static string Cell(List<string> row, int column) => column >= 1 && column < row.Count ? row[column] : "";

    // Sahte dil: harfleri aksanlı benzerleriyle değiştirir, ~%35 uzatır, [ ] içine alır. Yer tutucular ({0}) ve zengin metin
    // etiketleri (<color=…>) olduğu gibi kalır.
    public static string Pseudo(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var sb = new StringBuilder(text.Length * 2 + 4);
        sb.Append('[');
        int letters = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{' || c == '<')
            {
                char close = c == '{' ? '}' : '>';
                int end = text.IndexOf(close, i);
                if (end > i) { sb.Append(text, i, end - i + 1); i = end; continue; }
            }
            int k = PseudoFrom.IndexOf(c);
            sb.Append(k >= 0 ? PseudoTo[k] : c);
            if (char.IsLetter(c)) letters++;
        }
        for (int i = 0; i < Mathf.CeilToInt(letters * .35f); i++) sb.Append(i % 2 == 0 ? '~' : '·');
        sb.Append(']');
        return sb.ToString();
    }

    // Latin-1 ve Türkçe harfler (oyunun fontlarında bulunma olasılığı yüksek; olmayan harf kutu görünür, sahte dilde sorun değil).
    private const string PseudoFrom = "aceinosuyAEIOUCNSY";
    private const string PseudoTo = "åçéîñøşûýÅÉÎØÛÇÑŞÝ";

    // RFC 4180 CSV: alanlar virgülle ayrılır; virgül, tırnak ya da satır sonu içeren alan çift tırnak içinde ("" = tek tırnak).
    public static List<List<string>> Parse(string csv)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, any = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char c = csv[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < csv.Length && csv[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }
            if (c == '"') { quoted = true; any = true; }
            else if (c == ',') { row.Add(field.ToString()); field.Clear(); any = true; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                if (any || field.Length > 0) { row.Add(field.ToString()); rows.Add(row); }
                row = new List<string>(); field.Clear(); any = false;
            }
            else if (c != '﻿') { field.Append(c); any = true; }
        }
        if (any || field.Length > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
