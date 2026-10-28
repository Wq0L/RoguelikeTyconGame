using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Diyalog metin tablosu (Resources/Dialogue/dialogue_text.csv): ilk satır başlık, sonraki her satır bir anahtar.
// Çeviriye hazır (Faz 2, 15–30 dil hedefi): başlık "key,note,tr,en,de,…" biçimindedir; dil sütunları ISO kodudur, "note" sütunu
// çevirmen içindir (kim konuşuyor, hangi ekran, kutu boyu) ve oyunda okunmaz. Sütun sırası önemli değildir, adıyla bulunur.
// - Kaynak dil Türkçedir (SourceLanguage). Etkin dilde hücre boşsa önce İngilizceye (FallbackLanguage), o da boşsa Türkçeye düşülür.
// - Metin parça parça birleştirilmez: her satır tam cümledir (diller sözcük sırasını değiştirir). Değişken gerekirse yer tutucuyla
//   ({0}) tek anahtarda yazılır.
// - "meta.speed" satırı dil başına yazı hızı çarpanıdır (boş: 1; Çince/Japonca/Korece gibi yoğun yazılarda düşük tutulur).
// Alanlar virgülle ayrılır; virgül, tırnak ya da satır sonu içeren alan çift tırnak içinde yazılır ("" = tek tırnak).
// Tablo bir kez yüklenir, dil değişince yeniden kurulur. Eksik anahtar geliştirmede uyarı verir; oyunda anahtarın kendisi görünür.
public static class DialogueText
{
    public const string ResourcePath = "Dialogue/dialogue_text";
    public const string SourceLanguage = "tr", FallbackLanguage = "en", NoteColumn = "note", SpeedKey = "meta.speed";
    private static Dictionary<string, string> table;
    private static string language = SourceLanguage;
    private static float speed = 1f;
    private static string sourceOverride;
    private static readonly List<string> languages = new();

    // Dil değişince açık kutular etiketlerini yeniler (ileride ayarlar menüsü bağlanır, Faz 4).
    public static event Action LanguageChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { table = null; language = SourceLanguage; speed = 1f; languages.Clear(); LanguageChanged = null; sourceOverride = null; }

    // Test ve araçlar (ileride Mechanics Test Lab'de çeviri önizlemesi): tabloyu verilen CSV metninden kurar; null: Resources'a döner.
    public static void UseCsv(string csv)
    {
        sourceOverride = csv;
        table = null;
        LanguageChanged?.Invoke();
    }

    public static string Language
    {
        get => language;
        set
        {
            string code = string.IsNullOrWhiteSpace(value) ? SourceLanguage : value.Trim().ToLowerInvariant();
            if (code == language) return;
            language = code;
            table = null;
            LanguageChanged?.Invoke();
        }
    }

    // Tablodaki dil sütunları (başlık sırasıyla). Ayarlar menüsü bunu listeler.
    public static IReadOnlyList<string> Languages { get { Ensure(); return languages; } }
    public static float SpeedScale { get { Ensure(); return speed; } }
    public static int Count { get { Ensure(); return table.Count; } }

    public static bool Has(string key) { Ensure(); return key != null && table.ContainsKey(key); }

    public static string Get(string key)
    {
        Ensure();
        if (key != null && table.TryGetValue(key, out string text)) return text;
        if (Application.isEditor) Debug.LogWarning("Diyalog metni eksik: " + key);
        return key ?? "";
    }

    private static void Ensure()
    {
        if (table != null) return;
        table = new Dictionary<string, string>();
        languages.Clear();
        speed = 1f;
        string csv = sourceOverride;
        if (csv == null)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) { Debug.LogWarning("Diyalog metin tablosu bulunamadı: " + ResourcePath); return; }
            csv = asset.text;
        }
        Load(csv, table, language, languages);
        if (table.TryGetValue(SpeedKey, out string scale) && float.TryParse(scale, NumberStyles.Float, CultureInfo.InvariantCulture, out float s) && s > 0f)
            speed = Mathf.Clamp(s, .2f, 3f);
        table.Remove(SpeedKey);
    }

    // Test ve araçlar için: verilen CSV metnini bir sözlüğe okur. Her anahtar için etkin dil → İngilizce → Türkçe, ilk dolu hücre.
    public static void Load(string csv, Dictionary<string, string> into, string lang = SourceLanguage, List<string> columns = null)
    {
        List<List<string>> rows = Parse(csv);
        if (rows.Count == 0) return;
        List<string> header = rows[0];
        for (int c = 0; c < header.Count; c++) header[c] = header[c].Trim().ToLowerInvariant();
        if (columns != null)
            for (int c = 1; c < header.Count; c++)
                if (header[c].Length > 0 && header[c] != NoteColumn) columns.Add(header[c]);
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

    // Çeviri denetimi (araçlar ve testler): verilen dilde boş kalan anahtarlar (meta satırları hariç).
    public static List<string> Missing(string csv, string lang)
    {
        var missing = new List<string>();
        List<List<string>> rows = Parse(csv);
        if (rows.Count == 0) return missing;
        int column = rows[0].FindIndex(h => h.Trim().ToLowerInvariant() == lang);
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]) || row[0].StartsWith("meta.")) continue;
            if (column < 1 || Cell(row, column).Length == 0) missing.Add(row[0].Trim());
        }
        return missing;
    }

    private static string Cell(List<string> row, int column) => column >= 1 && column < row.Count ? row[column] : "";

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
