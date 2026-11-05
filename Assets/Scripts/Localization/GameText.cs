using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

// Oyunun bütün oyuncuya görünen yazıları (4–5 Kasım 2026). Kod, sahne ve veri yalnız anahtarı bilir; yazı buradan gelir.
// ANA LİSTE: Assets/Localization/GameText.csv ("key,note,en,tr,…"). Yazı ve çeviri yalnız orada düzenlenir.
//   Editör, CSV kaydedilince onu Unity Localization'ın "GameText" tablosuna aktarır (LocalizationSetup / GameTextHotReload):
//   not sütunu Unity'de satır yorumu olur, boş hücre tabloya girmez (yedek dile düşer). Oyun Unity tablolarını okur
//   (Addressables ile paketlenir; tablolar önceden yüklenir, ilk erişim eşzamanlıdır).
// CSV yolu yalnız geliştirme içindir: test CSV'yi zorladığında (UseCsv) ya da editörde kurulum eksikken. İkinci durumda açık
// uyarı verilir: paketlenmiş oyunda CSV yoktur, tablo eksikse yazılar anahtar olarak görünür (derleme öncesi denetim:
// LocalizationBuildCheck; gerçek derlemede dil testi: LocalizationSmokeTest).
// Kurallar:
// - Kaynak dil İngilizce (5 Kasım kullanıcı kararı; çevirmenler İngilizceden çevirir): her satırda dolu olmak zorunda; boşsa
//   liste denetimden geçmez ve tablolara aktarılmaz.
//   Açılış dili (StartLanguage): oyuncunun ayarlardan seçtiği dil (GameSettings.Language) → bilgisayarın dili (tabloda varsa)
//   → İngilizce. Otomatik testlerde (batch) Türkçe, oyuncu tercihlerinden bağımsız.
// - Dil zinciri tek yönlü: her dil → İngilizce → bitti (döngü yok; Unity döngülü dil bağlantısını reddeder). Unity'de dil yedeği,
//   CSV yolunda hücre hücre.
// - Metin parça parça birleştirilmez: değişken yer tutucuyla ({0}) tek anahtarda yazılır, Format ile doldurulur. Adlı yer
//   tutucu ya da çoğul kalıbı ({0:plural:1 round|{} rounds}) içeren satır Unity'de "Smart" işaretlenir (dile göre çoğul kuralları).
// - "meta." ile başlayan satırlar dil ayarıdır (meta.speed: konuşma yazı hızı çarpanı); yazı sayılmaz.
// - Sahte dil (qps): kaynak metin aksanlanır, ~%35 uzar, [ ] içine alınır; bağlanmamış yazıları ve taşan kutuları göstermek için.
// Dil ya da tablo değişince Changed yayınlanır (LocalizedText ve açık konuşma kutuları yenilenir). Karede iş yok: yazı hızı
// çarpanı önbellekte, yazılar yalnız olaylarda okunur.
public static class GameText
{
    public const string TableName = "GameText";
    public const string MasterCsvPath = "Assets/Localization/GameText.csv";
    // Kaynak (çeviri kaynağı, Unity proje dili; zincirin sonu), otomatik testlerin dili, sahte dil.
    public const string SourceLanguage = "en", BatchLanguage = "tr", PseudoLanguage = "qps";
    public const string NoteColumn = "note", MetaPrefix = "meta.", SpeedKey = "meta.speed";
    // Dil sütunu sayılmayan bilgi sütunları.
    private static readonly HashSet<string> InfoColumns = new() { NoteColumn, "context", "max", "where" };

    private static Dictionary<string, string> csvTable;
    private static readonly HashSet<string> warned = new();
    private static readonly List<string> languages = new();
    private static string language;   // null: henüz belirlenmedi (StartLanguage)
    private static string sourceOverride;
    private static bool pseudo, speedValid, subscribed;
    // Bizim seçtiğimiz Unity dili: Unity değişiklik haberini sonraki karede yayınlar, yenileme hemen burada yapılır; gelen haber
    // ikinci kez yenilemez.
    private static Locale applied;
    private static float speed = 1f;
    private static int unityState;   // 0 bakılmadı, 1 Unity tabloları hazır, -1 yok (CSV yolu)

    public static event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        if (subscribed) LocalizationSettings.SelectedLocaleChanged -= OnUnityLocaleChanged;
        subscribed = false;
        csvTable = null; warned.Clear(); languages.Clear();
        language = null; sourceOverride = null; pseudo = false; speedValid = false; speed = 1f; unityState = 0; applied = null; culture = null; groupSeparator = '\0';
        Changed = null;
    }

    // Yazılar Unity Localization tablolarından mı geliyor (kurulum yapılmış ve test CSV zorlamıyor).
    public static bool UsingUnityTables => sourceOverride == null && UnityReady();

    private static string Current => language ??= StartLanguage();

    // Açılış dili: oyuncunun seçtiği → bilgisayarın dili (tabloda varsa) → İngilizce. Batch (otomatik test): Türkçe.
    public static string StartLanguage() =>
        Application.isBatchMode ? BatchLanguage : StartLanguageFor(GameSettings.Language, Application.systemLanguage);

    // Kural (test edilebilir): seçilmiş dil → bilgisayarın dili (tabloda varsa) → İngilizce.
    public static string StartLanguageFor(string chosen, SystemLanguage system)
    {
        if (!string.IsNullOrWhiteSpace(chosen)) return chosen.Trim().ToLowerInvariant();
        return SystemLanguageIfAvailable(system) ?? SourceLanguage;
    }

    // Bilgisayarın dili tabloda varsa kodu (tr, en, de …), yoksa null.
    private static string SystemLanguageIfAvailable(SystemLanguage system)
    {
        string code = new LocaleIdentifier(system).Code;
        if (string.IsNullOrEmpty(code)) return null;
        code = code.ToLowerInvariant();
        foreach (string available in LanguageList())
            if (available == code || code.StartsWith(available + "-", StringComparison.Ordinal)) return available;
        return null;
    }

    // Dil adı oyuncuya kendi dilinde: "English", "Türkçe", "Deutsch" …
    public static string NativeName(string code)
    {
        try
        {
            string name = CultureInfo.GetCultureInfo(code).NativeName;
            int paren = name.IndexOf(" (", StringComparison.Ordinal);
            if (paren > 0) name = name.Substring(0, paren);
            return name.Length > 0 ? char.ToUpper(name[0], CultureInfo.GetCultureInfo(code)) + name.Substring(1) : code;
        }
        catch (CultureNotFoundException) { return code.ToUpperInvariant(); }
    }

    public static string Language
    {
        get => Current;
        set
        {
            string code = string.IsNullOrWhiteSpace(value) ? StartLanguage() : value.Trim().ToLowerInvariant();
            if (code == Current) return;
            language = code;
            pseudo = code == PseudoLanguage;
            // Tabloda olmayan dil: kaynak (İngilizce) seçilir.
            if (!pseudo && UsingUnityTables) Apply(FindLocale(code) ?? FindLocale(SourceLanguage));
            Invalidate();
        }
    }

    // Seçilebilir diller (ayarlar menüsü): kaynak dil (İngilizce) başta, sonra tablodaki sırayla. Sahte dil yoktur.
    public static IReadOnlyList<string> Languages => LanguageList();

    // Etkin dile bakmadan okunur (açılış dili bu listeye göre seçilir).
    private static List<string> LanguageList()
    {
        languages.Clear();
        if (UsingUnityTables)
        {
            foreach (Locale l in LocalizationSettings.AvailableLocales.Locales)
                if (l != null && l is not UnityEngine.Localization.Pseudo.PseudoLocale) languages.Add(l.Identifier.Code.ToLowerInvariant());
        }
        else if (SourceCsv is string csv)
        {
            int end = csv.IndexOf('\n');
            List<List<string>> head = Parse(end < 0 ? csv : csv.Substring(0, end));
            if (head.Count > 0)
                for (int c = 1; c < head[0].Count; c++)
                {
                    string h = head[0][c].Trim().ToLowerInvariant();
                    if (IsLanguageColumn(h)) languages.Add(h);
                }
        }
        int source = languages.IndexOf(SourceLanguage);
        if (source > 0) { languages.RemoveAt(source); languages.Insert(0, SourceLanguage); }
        return languages;
    }

    // Konuşma yazı hızı çarpanı (meta.speed). Önbellekte: konuşma kutusu her karede okur.
    public static float SpeedScale
    {
        get
        {
            if (speedValid) return speed;
            speedValid = true;
            speed = 1f;
            if (TryGet(SpeedKey, null, out string s) && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) && v > 0f)
                speed = Mathf.Clamp(v, .2f, 3f);
            return speed;
        }
    }

    public static int Count
    {
        get
        {
            if (UsingUnityTables)
            {
                int n = 0;
                foreach (SharedTableData.SharedTableEntry e in SharedData.Entries)
                    if (!e.Key.StartsWith(MetaPrefix, StringComparison.Ordinal)) n++;
                return n;
            }
            EnsureCsv(null);
            return csvTable.Count;
        }
    }

    public static bool Has(string key)
    {
        if (key == null) return false;
        if (UsingUnityTables) return SharedData.Contains(key);
        EnsureCsv(null);
        return csvTable.ContainsKey(key);
    }

    public static string Get(string key)
    {
        if (key == null) return "";
        if (TryGet(key, null, out string text)) return pseudo ? Pseudo(text) : text;
        if (Application.isEditor && warned.Add(key)) Debug.LogWarning("Çeviri tablosunda anahtar yok: " + key);
        return key;
    }

    // Alan bir anahtar da olabilir düz yazı da (geçiş dönemi: eski sahnelerde düz yazı, kurucu yeniden çalışınca anahtar).
    // Anahtarsa çevirisi, değilse olduğu gibi döner. Uyarı vermez.
    public static string Resolve(string keyOrText) =>
        string.IsNullOrEmpty(keyOrText) ? keyOrText : Has(keyOrText) ? Get(keyOrText) : keyOrText;

    // Veri yazısı (ScriptableObject alanı: ad, oyun tarzı, görev …): anahtar tabloda varsa çevirisi, yoksa asset'teki yazı
    // (listeye henüz eklenmemiş içerik Türkçe görünür; veri denetimi eksik anahtarları listeler). Uyarı vermez.
    public static string Data(string key, string fallback) =>
        !string.IsNullOrEmpty(key) && Has(key) ? Get(key) : fallback;

    // Kimliği (id) olmayan veri için anahtar parçası, yazının kendisinden: "Keskin Başlangıç" → "keskin_baslangic".
    // Türkçe harfler sadeleşir, harf / rakam dışı her dizi tek "_" olur. Aynı yazı aynı anahtarı verir (iki denge sürümündeki
    // aynı adlı düğüm tek satırdır). Önbellekte: aynı yazı ikinci kez hesaplanmaz.
    public static string Slug(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (slugs.TryGetValue(text, out string slug)) return slug;
        var sb = new StringBuilder(text.Length);
        bool gap = false;
        foreach (char raw in text)
        {
            char c = raw switch
            {
                'ç' or 'Ç' => 'c', 'ğ' or 'Ğ' => 'g', 'ı' or 'I' or 'İ' => 'i', 'ö' or 'Ö' => 'o', 'ş' or 'Ş' => 's', 'ü' or 'Ü' => 'u',
                'â' or 'Â' => 'a', 'î' or 'Î' => 'i', 'û' or 'Û' => 'u', '×' => 'x', _ => char.ToLowerInvariant(raw)
            };
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                if (gap && sb.Length > 0) sb.Append('_');
                sb.Append(c);
                gap = false;
            }
            else gap = true;
        }
        slug = sb.ToString();
        slugs[text] = slug;
        return slug;
    }

    private static readonly Dictionary<string, string> slugs = new();

    // Uzun veri yazısı (ödül notu …) için kısa, kararlı anahtar eki: FNV-1a, 6 onaltılık hane. Aynı yazı aynı eki verir; yazı
    // değişirse ek değişir (satır yeniden yazılır, kapsam testi yakalar). .NET GetHashCode kullanılmaz: kararlılığı garanti değil.
    public static string ShortHash(string text)
    {
        uint h = 2166136261;
        foreach (char c in text ?? "") { h ^= c; h *= 16777619; }
        return (h & 0xFFFFFF).ToString("x6");
    }

    // Sayı biçimi etkin dile göre (ondalık ayırıcı: en "1.25", tr "1,25"). Sahte dil kaynağın biçimini kullanır.
    public static CultureInfo Culture
    {
        get
        {
            if (culture != null) return culture;
            // Bölgeli kültür (tr → tr-TR): yalın dil kültürünün sayı biçimi bazı çalışma zamanlarında eksik.
            try { culture = CultureInfo.GetCultureInfo(CultureInfo.CreateSpecificCulture(pseudo ? SourceLanguage : Current).Name); }
            catch (CultureNotFoundException) { culture = CultureInfo.InvariantCulture; }
            return culture;
        }
    }

    private static CultureInfo culture;
    private static char groupSeparator;

    // Etkin dilin sayı biçimiyle: Number(1.5f, "0.##") → en "1.5", tr "1,5".
    public static string Number(double value, string format) => value.ToString(format, Culture);

    // Binlik ayırıcı (en "12,345", tr "12.345"): sayıyı karakter karakter yazan sayaçlar için (karede ayırma yok).
    public static char GroupSeparator
    {
        get
        {
            if (groupSeparator != '\0') return groupSeparator;
            string s = Culture.NumberFormat.NumberGroupSeparator;
            groupSeparator = string.IsNullOrEmpty(s) || char.IsWhiteSpace(s[0]) ? ' ' : s[0];
            return groupSeparator;
        }
    }

    // Cümle başı büyük harf, etkin dilin kuralıyla (Türkçede i → İ).
    public static string Capitalize(string text) =>
        string.IsNullOrEmpty(text) ? text : char.ToUpper(text[0], Culture) + text.Substring(1);

    // Yer tutuculu metin: "{0} altın" → "12 altın". Biçim hatası oyunu durdurmaz, ham metin döner (geliştirmede uyarı).
    public static string Format(string key, params object[] args)
    {
        if (args == null || args.Length == 0) return Get(key);
        if (key == null) return "";
        if (!TryGet(key, args, out string text))
        {
            if (Application.isEditor && warned.Add(key)) Debug.LogWarning("Çeviri tablosunda anahtar yok: " + key);
            return key;
        }
        return pseudo ? Pseudo(text) : text;
    }

    // Tablo kaydedildi (editörde CSV yeniden aktarıldı) ya da test tabloyu değiştirdi: yeniden okunur, haber verilir.
    public static void Reload()
    {
        unityState = 0;
        Invalidate();
    }

    // Test ve araçlar: yazıyı verilen CSV metninden okur (Unity tabloları kullanılmaz); null: normal yola döner.
    public static void UseCsv(string csv)
    {
        sourceOverride = csv;
        Reload();
    }

    // Ana liste (CSV) metni: test CSV'si, yoksa editörde Assets/Localization/GameText.csv. Oyun derlemesinde yok (null).
    public static string SourceCsv
    {
        get
        {
            if (sourceOverride != null) return sourceOverride;
#if UNITY_EDITOR
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(MasterCsvPath);
            return asset != null ? asset.text : null;
#else
            return null;
#endif
        }
    }

    private static void Invalidate()
    {
        csvTable = null;
        culture = null;
        groupSeparator = '\0';
        speedValid = false;
        warned.Clear();
        Changed?.Invoke();
    }

    // Unity'nin dil değişikliği haberi. Haberler gecikmeli ve art arda değişimde sırasız gelebilir: şu an seçili dille uyuşmayan
    // haber eskidir, yok sayılır (yoksa eski haber dil bilgisini geri çevirir). Bizim seçtiğimiz dilin haberi gelirse yenileme
    // zaten yapılmıştır (Language / Apply).
    private static void OnUnityLocaleChanged(Locale locale)
    {
        Locale now = LocalizationSettings.SelectedLocale;
        if (locale != now) return;
        if (now == applied) { applied = null; return; }
        applied = null;
        if (!pseudo && now != null) language = now.Identifier.Code.ToLowerInvariant();
        Invalidate();
    }

    // Unity'nin seçili dilini değiştirir; haber geldiğinde ikinci kez yenilenmez.
    private static void Apply(Locale locale)
    {
        // Editör modunda (araçlar) Unity'nin seçili diline dokunulmaz; okumalar dili zaten açıkça verir (Target).
        if (!Application.isPlaying) return;
        if (locale == null || LocalizationSettings.SelectedLocale == locale) return;
        applied = locale;
        LocalizationSettings.SelectedLocale = locale;
    }

    private static bool UnityReady()
    {
        if (unityState != 0) return unityState > 0;
        unityState = -1;
        string problem = null;
        if (!LocalizationSettings.HasSettings) problem = "Localization ayarı yok";
        else
        {
            try
            {
                var init = LocalizationSettings.InitializationOperation;
                if (!init.IsDone) init.WaitForCompletion();
                // Dil açıkça verilir: editör modunda (kurucular, araçlar) Unity'nin "seçili dili" yoktur, seçili dile göre istemek
                // "SelectedLocale is null" hatası verir (5 Kasım, ayarlar menüsü kurucusunda görüldü).
                if (LocalizationSettings.StringDatabase?.GetTable(TableName, Target) == null) problem = TableName + " tablosu yüklenemedi";
            }
            catch (Exception e) { problem = e.Message; }
        }
        if (problem != null)
        {
            // Açıkça söylenir: editörde yazılar CSV'den görünür ama paketlenmiş oyunda CSV yoktur. Editör modundaki araçlar için
            // (Play dışı) sessiz: ana liste zaten CSV, oyunla ilgisi yok.
            if (Application.isPlaying && Application.isEditor)
                Debug.LogWarning($"Çeviri tabloları kullanılamıyor ({problem}). Yazılar YALNIZ EDİTÖRDE CSV'den okunuyor; paketlenmiş oyunda " +
                                 "anahtar olarak görünürler. Düzeltme: Tools > Çeviri > Kurulum.");
            else if (!Application.isEditor) Debug.LogError($"Çeviri tabloları yüklenemedi ({problem}); yazılar anahtar olarak görünecek.");
            return false;
        }
        if (!subscribed) { LocalizationSettings.SelectedLocaleChanged += OnUnityLocaleChanged; subscribed = true; }
        unityState = 1;
        // İstenen dil (Unity hazır olmadan seçilmiş olabilir) Unity'ye bildirilir.
        if (!pseudo) Apply(FindLocale(Current) ?? FindLocale(SourceLanguage));
        return true;
    }

    private static SharedTableData SharedData => LocalizationSettings.StringDatabase.GetTable(TableName, Target).SharedData;

    private static Locale FindLocale(string code) =>
        string.IsNullOrEmpty(code) || code == PseudoLanguage ? null : LocalizationSettings.AvailableLocales.GetLocale(code);

    // Okunacak dil her çağrıda açıkça verilir (Unity'nin seçili diline bağlı kalınmaz: editör modunda o yoktur). Sahte dil kaynaktan;
    // tabloda olmayan dil kaynağa (İngilizce) düşer. Unity'nin dil yedeği bu dilden başlar.
    private static Locale Target => pseudo ? FindLocale(SourceLanguage) : FindLocale(Current) ?? FindLocale(SourceLanguage);

    private static bool TryGet(string key, object[] args, out string text)
    {
        text = null;
        if (UsingUnityTables)
        {
            StringTableEntry entry = LocalizationSettings.StringDatabase.GetTableEntry(TableName, key, Target).Entry;
            if (entry == null || string.IsNullOrEmpty(entry.Value)) return false;
            try { text = args == null ? entry.GetLocalizedString() : entry.GetLocalizedString(args); }
            catch (FormatException) { text = entry.Value; WarnFormat(key, text); }
            catch (Exception)
            {
                // Editör modunda Smart biçimlendirici seçili dil olmadan çalışmayabilir: ham metin ya da düz biçim.
                text = args == null ? entry.Value : SafeFormat(key, entry.Value, args);
            }
            return true;
        }
        EnsureCsv(null);
        if (!csvTable.TryGetValue(key, out string pattern)) return false;
        text = args == null ? pattern : SafeFormat(key, pattern, args);
        return true;
    }

    private static string SafeFormat(string key, string pattern, object[] args)
    {
        try { return string.Format(CultureInfo.InvariantCulture, pattern, args); }
        catch (FormatException) { WarnFormat(key, pattern); return pattern; }
    }

    private static void WarnFormat(string key, string pattern)
    {
        if (Application.isEditor && warned.Add("format:" + key)) Debug.LogWarning("Çeviri yer tutucusu hatalı: " + key + " → " + pattern);
    }

    private static void EnsureCsv(List<string> columns)
    {
        if (csvTable != null && columns == null) return;
        var table = new Dictionary<string, string>();
        string csv = SourceCsv;
        if (csv == null) { if (warned.Add("table")) Debug.LogWarning("Çeviri tablosu bulunamadı: " + MasterCsvPath); }
        else Load(csv, table, pseudo ? SourceLanguage : Current, columns);
        csvTable = table;
    }

    public static bool IsLanguageColumn(string header) =>
        header.Length > 0 && !InfoColumns.Contains(header) && header != "key" && !header.StartsWith("_", StringComparison.Ordinal);

    // Verilen CSV metnini bir sözlüğe okur. Her anahtar için etkin dil → İngilizce (kaynak), ilk dolu hücre. İkisi de boşsa
    // anahtar eklenmez (eksik sayılır; liste denetimi bunu hata olarak bildirir).
    public static void Load(string csv, Dictionary<string, string> into, string lang = SourceLanguage, List<string> columns = null)
    {
        List<List<string>> rows = Parse(csv);
        if (rows.Count == 0) return;
        List<string> header = rows[0];
        for (int c = 0; c < header.Count; c++) header[c] = header[c].Trim().ToLowerInvariant();
        if (columns != null)
            for (int c = 1; c < header.Count; c++)
                if (IsLanguageColumn(header[c])) columns.Add(header[c]);
        int active = header.IndexOf(lang), source = header.IndexOf(SourceLanguage);
        if (active < 1 && source < 1) return;
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0])) continue;
            string text = Cell(row, active);
            if (IsBlank(text)) text = Cell(row, source);
            if (!IsBlank(text)) into[row[0].Trim()] = text;
        }
    }

    // Boş hücre: hiç yazı yok ya da yalnız boşluk. Denetim, aktarım, derleme denetimi ve CSV okuyucusu aynı tanımı kullanır
    // (boşluklu hücre tabloya girmez, o dilde İngilizce görünür).
    public static bool IsBlank(string cell) => string.IsNullOrWhiteSpace(cell);

    // Çoğul / adlı yer tutucu içeren metin (Unity'de Smart işaretlenir): {0:…} ya da {ad…}. Düz {0} normal biçimdir.
    public static bool LooksSmart(string text) => SmartPattern.IsMatch(text ?? "");
    private static readonly System.Text.RegularExpressions.Regex SmartPattern = new(@"\{(\d+:|[A-Za-z_])");

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
            if (column < 1 || IsBlank(Cell(row, column))) missing.Add(row[0].Trim());
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
    public static List<List<string>> Parse(string csv) => Parse(csv, out _);

    // unterminated: dosya bir tırnak içindeyken bitti (kapanmamış tırnak: sonraki satırlar tek hücreye yutulmuştur).
    public static List<List<string>> Parse(string csv, out bool unterminated)
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
        unterminated = quoted;
        return rows;
    }
}
