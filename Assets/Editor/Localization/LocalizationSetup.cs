using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

// Unity Localization kurulumu ve CSV → tablo aktarımı (5 Kasım 2026). Her çağrıda eksik olanı tamamlar, var olanı bozmaz.
// Kurulum:
//   - ayar dosyası:    Assets/Localization/Settings/LocalizationSettings.asset (etkin ayar olarak kaydedilir)
//   - diller:          CSV'deki her dil sütunu için bir Locale (Assets/Localization/Locales); kaynak (proje) dili en
//   - dil yedeği:      tek yönlü, her dil → en → bitti (boş çeviri İngilizceye düşer). Döngü yok: Unity'nin FallbackLocale denetimi
//                      döngülü zincirde yeni dil bağlantısını sessizce reddeder. StringDatabase.UseFallback açık
//   - açılış dili:     Unity tarafında bilgisayarın dili, yoksa en. Asıl karar GameText.StartLanguage'da: oyuncunun ayarlardan
//                      seçtiği dil (GameSettings.Language) → bilgisayarın dili → en
//   - tablo:           "GameText" (Assets/Localization/Tables), her dil önceden yüklenir; ilk erişim eşzamanlı (InitializeSynchronously)
//   - Addressables:    oyuncu derlemesinde tablolar kendiliğinden paketlenir (BuildAddressablesWithPlayerBuild = BuildWithPlayer)
// Aktarım (GameText.csv → GameText tablosu): ÖNCE liste denetlenir (GameTextAudit); hata varsa hiçbir tabloya dokunulmaz ve
// InvalidListException atılır (bozuk bir tırnak sağlam satırları "silinmiş" gösterip tablodan sildirmesin). Geçerliyse: anahtar
// eklenir / güncellenir / CSV'de olmayan silinir; "note" satır yorumu olur; boş hücre tabloya girmez; adlı yer tutucu ya da
// biçimli alan ({ad}, {0:plural:…}) içeren hücre Smart işaretlenir.
public static class LocalizationSetup
{
    public const string Root = "Assets/Localization", SettingsPath = Root + "/Settings/LocalizationSettings.asset";
    public const string LocalesDir = Root + "/Locales", TablesDir = Root + "/Tables";

    public sealed class ImportReport { public int Added, Updated, Removed, Smart, Unchanged; public List<string> Languages = new(); }

    // Liste denetimden geçmedi: aktarım yapılmadı, tablolar olduğu gibi.
    public sealed class InvalidListException : Exception
    {
        public readonly GameTextAudit.Report Report;
        public InvalidListException(GameTextAudit.Report report)
            : base($"Çeviri listesi hatalı, tablolar DEĞİŞTİRİLMEDİ ({report.Errors.Count} hata):\n{report.Summary()}") => Report = report;
    }

    public static GameTextAudit.Report Validate(string csv)
    {
        GameTextAudit.Report report = GameTextAudit.Run(csv);
        if (!report.Ok) throw new InvalidListException(report);
        return report;
    }

    // Kurulum tamam mı: etkin ayar, kaynak dil ve GameText tablosu var.
    public static bool IsComplete =>
        LocalizationEditorSettings.ActiveLocalizationSettings != null
        && LocalizationEditorSettings.GetLocales().Any(l => l.Identifier.Code == GameText.SourceLanguage)
        && LocalizationEditorSettings.GetStringTableCollection(GameText.TableName) != null;

    // Kurulum var ama bugünkü kurallarla aynı değil mi (eski sürümle yapılmış kurulum). Otomatik kurulum ve derleme denetimi bakar;
    // yalnız "var mı" bakılsaydı eski bir kurulum (ör. 5 Kasım öncesi en → tr döngüsü) hiç düzelmezdi.
    public static List<string> ConfigProblems()
    {
        var problems = new List<string>();
        LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null) { problems.Add("Localization ayarı etkin değil"); return problems; }
        Locale source = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code == GameText.SourceLanguage);
        if (source == null) { problems.Add("kaynak dil yok: " + GameText.SourceLanguage); return problems; }
        foreach (Locale locale in LocalizationEditorSettings.GetLocales())
        {
            Locale fallback = locale.Metadata.GetMetadata<FallbackLocale>()?.Locale;
            if (locale == source && fallback != null) problems.Add($"kaynak dilin ({source.Identifier.Code}) yedeği olmamalı (şu an {fallback.Identifier.Code}: döngü)");
            if (locale != source && fallback != source) problems.Add($"{locale.Identifier.Code} dilinin yedeği {GameText.SourceLanguage} değil ({(fallback != null ? fallback.Identifier.Code : "yok")})");
        }
        var so = new SerializedObject(settings);
        if (!so.FindProperty("m_InitializeSynchronously").boolValue) problems.Add("tablolar eşzamanlı başlamıyor");
        if (so.FindProperty("m_ProjectLocaleIdentifier.m_Code").stringValue != GameText.SourceLanguage) problems.Add("proje dili kaynak dil değil");
        if (!settings.GetStringDatabase().UseFallback) problems.Add("eksik çeviri yedeğe düşmüyor (UseFallback kapalı)");
        List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
        if (!(selectors.Count == 2 && selectors[0] is SystemLocaleSelector && selectors[1] is SpecificLocaleSelector s && s.LocaleId.Code == GameText.SourceLanguage))
            problems.Add("açılış dili kuralı güncel değil");
        return problems;
    }

    public static bool IsUpToDate => IsComplete && ConfigProblems().Count == 0;

    public static ImportReport Run()
    {
        string csv = File.Exists(GameText.MasterCsvPath) ? File.ReadAllText(GameText.MasterCsvPath) : null;
        if (csv == null) throw new Exception("Ana liste yok: " + GameText.MasterCsvPath);
        Validate(csv);   // hatalı listeyle kurulum da yapılmaz
        List<string> codes = LanguageCodes(csv);

        LocalizationSettings settings = EnsureSettings();
        var locales = new Dictionary<string, Locale>();
        foreach (string code in codes) locales[code] = EnsureLocale(code);
        // Önce kaynağın (eski sürümden kalma) yedeği kaldırılır, sonra diğerleri kaynağa bağlanır: döngü hiç oluşmaz.
        EnsureFallback(GameText.SourceLanguage, locales[GameText.SourceLanguage], locales);
        foreach (var pair in locales)
            if (pair.Key != GameText.SourceLanguage) EnsureFallback(pair.Key, pair.Value, locales);
        ConfigureSettings(settings, locales[GameText.SourceLanguage]);

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(GameText.TableName)
            ?? LocalizationEditorSettings.CreateStringTableCollection(GameText.TableName, TablesDir);
        foreach (Locale locale in locales.Values)
            if (collection.GetTable(locale.Identifier) == null) collection.AddNewTable(locale.Identifier);
        foreach (StringTable table in collection.StringTables)
            if (table != null) LocalizationEditorSettings.SetPreloadTableFlag(table, true);

        ImportReport report = Import(csv, collection);
        report.Languages.AddRange(codes);
        ConfigureAddressables();
        AssetDatabase.SaveAssets();
        return report;
    }

    private static List<string> LanguageCodes(string csv)
    {
        List<List<string>> rows = GameText.Parse(csv);
        var codes = new List<string>();
        if (rows.Count == 0) return codes;
        foreach (string h in rows[0].Skip(1))
        {
            string code = h.Trim().ToLowerInvariant();
            if (GameText.IsLanguageColumn(code) && !codes.Contains(code)) codes.Add(code);
        }
        if (!codes.Contains(GameText.SourceLanguage)) throw new Exception("Ana listede kaynak dil sütunu yok: " + GameText.SourceLanguage);
        return codes;
    }

    private static LocalizationSettings EnsureSettings()
    {
        LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = AssetDatabase.FindAssets("t:LocalizationSettings").Select(g => AssetDatabase.LoadAssetAtPath<LocalizationSettings>(AssetDatabase.GUIDToAssetPath(g)))
                .FirstOrDefault(s => s != null);
            if (settings == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "LocalizationSettings";
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }
        return settings;
    }

    private static Locale EnsureLocale(string code)
    {
        Locale locale = LocalizationEditorSettings.GetLocales().FirstOrDefault(l => l.Identifier.Code.ToLowerInvariant() == code);
        if (locale != null) return locale;
        string path = $"{LocalesDir}/{code}.asset";
        locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
        if (locale == null)
        {
            Directory.CreateDirectory(LocalesDir);
            locale = Locale.CreateLocale(code);
            AssetDatabase.CreateAsset(locale, path);
        }
        LocalizationEditorSettings.AddLocale(locale);
        return locale;
    }

    // Dil yedeği: kaynak (en) yedeksiz; her diğer dil → en.
    private static void EnsureFallback(string code, Locale locale, Dictionary<string, Locale> all)
    {
        FallbackLocale current = locale.Metadata.GetMetadata<FallbackLocale>();
        if (code == GameText.SourceLanguage)
        {
            if (current == null) return;
            locale.Metadata.RemoveMetadata(current);
            EditorUtility.SetDirty(locale);
            return;
        }
        Locale wanted = all[GameText.SourceLanguage];
        if (current != null && current.Locale == wanted) return;
        if (current != null) locale.Metadata.RemoveMetadata(current);
        var link = new FallbackLocale(wanted);
        if (link.Locale != wanted) throw new Exception($"Dil yedeği kurulamadı: {code} → {GameText.SourceLanguage} (Unity döngü buldu).");
        locale.Metadata.AddMetadata(link);
        EditorUtility.SetDirty(locale);
    }

    // Proje dili = kaynak (en). Unity'nin açılış dili: bilgisayarın dili, yoksa kaynak. Oyuncunun seçimini GameText uygular
    // (GameSettings.Language; Unity'nin kendi PlayerPrefs seçicisi kullanılmaz, ayarlar tek yerde kalsın).
    private static void ConfigureSettings(LocalizationSettings settings, Locale source)
    {
        var so = new SerializedObject(settings);
        so.FindProperty("m_InitializeSynchronously").boolValue = true;
        so.FindProperty("m_ProjectLocaleIdentifier.m_Code").stringValue = source.Identifier.Code;
        so.ApplyModifiedPropertiesWithoutUndo();
        LocalizedStringDatabase db = settings.GetStringDatabase();
        db.UseFallback = true;
        db.MissingTranslationState = MissingTranslationBehavior.PrintWarning;
        List<IStartupLocaleSelector> selectors = settings.GetStartupLocaleSelectors();
        bool ok = selectors.Count == 2 && selectors[0] is SystemLocaleSelector
                  && selectors[1] is SpecificLocaleSelector specific && specific.LocaleId.Code == source.Identifier.Code;
        if (!ok)
        {
            selectors.Clear();
            selectors.Add(new SystemLocaleSelector());
            selectors.Add(new SpecificLocaleSelector { LocaleId = source.Identifier });
        }
        EditorUtility.SetDirty(settings);
    }

    private static void ConfigureAddressables()
    {
        AddressableAssetSettings aa = AddressableAssetSettingsDefaultObject.GetSettings(true);
        if (aa == null || aa.BuildAddressablesWithPlayerBuild == AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer) return;
        aa.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
        EditorUtility.SetDirty(aa);
    }

    // Liste önce denetlenir; hatalıysa InvalidListException (hiçbir tabloya dokunulmaz).
    public static ImportReport Import(string csv, StringTableCollection collection)
    {
        Validate(csv);
        var report = new ImportReport();
        List<List<string>> rows = GameText.Parse(csv);
        if (rows.Count == 0) return report;
        List<string> header = rows[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        int note = header.IndexOf(GameText.NoteColumn);
        var columns = new List<(int index, StringTable table)>();
        for (int c = 1; c < header.Count; c++)
        {
            if (!GameText.IsLanguageColumn(header[c])) continue;
            if (collection.GetTable(new LocaleIdentifier(header[c])) is StringTable table) columns.Add((c, table));
        }

        SharedTableData shared = collection.SharedData;
        var keys = new HashSet<string>();
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;
            string key = row[0].Trim();
            if (!keys.Add(key)) continue;   // yineleneni denetim bildirir; ilk satır geçerli
            SharedTableData.SharedTableEntry entry = shared.GetEntry(key);
            bool changed = false, added = entry == null;
            if (added) entry = shared.AddKey(key);
            string comment = note >= 1 && note < row.Count ? row[note].Trim() : "";
            Comment existing = entry.Metadata.GetMetadata<Comment>();
            if (comment.Length == 0 && existing != null) { entry.Metadata.RemoveMetadata(existing); changed = true; }
            else if (comment.Length > 0 && existing == null) { entry.Metadata.AddMetadata(new Comment { CommentText = comment }); changed = true; }
            else if (existing != null && existing.CommentText != comment) { existing.CommentText = comment; changed = true; }

            foreach ((int index, StringTable table) in columns)
            {
                string text = index < row.Count ? row[index] : "";
                StringTableEntry cell = table.GetEntry(key);
                if (GameText.IsBlank(text))
                {
                    if (cell != null) { table.RemoveEntry(key); changed = true; EditorUtility.SetDirty(table); }
                    continue;
                }
                bool smart = GameText.LooksSmart(text);
                if (cell == null) { cell = table.AddEntry(key, text); changed = true; EditorUtility.SetDirty(table); }
                else if (cell.Value != text) { cell.Value = text; changed = true; EditorUtility.SetDirty(table); }
                if (cell.IsSmart != smart) { cell.IsSmart = smart; changed = true; EditorUtility.SetDirty(table); }
                if (smart) report.Smart++;
            }
            if (added) report.Added++;
            else if (changed) report.Updated++;
            else report.Unchanged++;
        }

        foreach (string stale in shared.Entries.Select(e => e.Key).Where(k => !keys.Contains(k)).ToList())
        {
            collection.RemoveEntry(stale);
            report.Removed++;
        }
        EditorUtility.SetDirty(shared);
        foreach ((int _, StringTable table) in columns) EditorUtility.SetDirty(table);
        return report;
    }
}
