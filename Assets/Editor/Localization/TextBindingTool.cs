using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Sahne ve prefab yazılarını çeviri listesine bağlama aracı (5 Kasım 2026). İki adım:
//   1. PLAN (Tools > Çeviri > Sahne ve prefab yazıları: plan çıkar): oyuna giren sahnelerdeki (derleme listesi + demo) ve oyuna
//      giren prefab'lardaki her TMP yazısı için Docs/Ceviri/Baglama_Plani.csv'ye bir satır yazar: işlem, nerede, ne yazıyor,
//      önerilen anahtar, İngilizce, Türkçe. İşlem:
//        bagla   : sabit yazı; bağlanacak
//        kod     : bir oyun betiği bu yazıyı alanında tutuyor, metni kod yazıyor → kod adımında taşınır (bağlanırsa dil değişince
//                  kodun yazdığı değerin üstüne yazılırdı)
//        kod?    : aynı nesnede ya da üstünde yazı değiştirebilecek bir oyun betiği var → elle karar (varsayılan: bağlanmaz)
//        prefab  : sahnedeki prefab kopyasının parçası → prefab dosyasının kendisinde bağlanır
//      Mevcut metnin dili tahmin edilir (Türkçe harf / sözcük); öteki dil boş kalır, gözden geçirmede doldurulur.
//      Aynı yol + aynı metin aynı anahtarı paylaşır (GameScene ile kopyası DemoScene).
//   2. UYGULA (… : planı uygula): plandaki "bagla" satırlarından İngilizcesi dolu olanları ana listeye ekler (anahtar zaten varsa
//      dokunmaz) ve yazı nesnesine LocalizedText bileşenini anahtarla ekler; sahneleri / prefab'ları kaydeder. Liste önce denetlenir.
// Nesne yolu "Ad#sıra/Ad#sıra/…" biçimindedir (aynı adlı kardeşler sıra numarasıyla ayrılır); uygulama ad ve sırayı birlikte doğrular.
public static class TextBindingTool
{
    public const string PlanFile = "Docs/Ceviri/Baglama_Plani.csv";
    public const string Bind = "bagla", Code = "kod", CodeMaybe = "kod?", PrefabPart = "prefab";
    private static readonly string[] Header = { "islem", "yer", "nesne", "metin", "anahtar", "en", "tr", "algilanan", "not" };
    private static readonly HashSet<string> Generic = new(StringComparer.OrdinalIgnoreCase)
        { "text", "text (tmp)", "tmp", "label", "value", "caption", "txt", "texttmp", "text tmp", "name" };
    private static readonly string[] TurkishWords =
        { "ve", "bir", "için", "geri", "ayarlar", "başlat", "kilitli", "açık", "kapalı", "satış", "dükkan", "varsayılan", "çık",
          "oyna", "tamam", "seç", "yeni", "devam", "atla", "round", "tur", "saksı", "tarla", "hasat", "kart", "boss" };

    public sealed class Row
    {
        public string Action, Location, ObjectPath, Text, Key, En, Tr, Detected, Note;
    }

    public static string PlanPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", PlanFile));

    [MenuItem("Tools/Çeviri/Sahne ve prefab yazıları: plan çıkar", priority = 10)]
    private static void PlanMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        List<Row> rows = BuildPlan();
        WritePlan(rows, PlanPath);
        EditorUtility.DisplayDialog("Çeviri", $"Plan yazıldı: {rows.Count} yazı.\n{Summary(rows)}\n\n{PlanPath}", "Tamam");
        EditorUtility.RevealInFinder(PlanPath);
    }

    public static string Summary(List<Row> rows) => string.Join(", ", rows.GroupBy(r => r.Action).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}"));

    // ---------- plan ----------
    public static List<Row> BuildPlan()
    {
        var rows = new List<Row>();
        var keyText = new Dictionary<string, string>();   // anahtar → kaynak metin (çakışma denetimi)
        Dictionary<string, (string en, string tr)> existing = ExistingTexts();
        HashSet<string> shipped = HardcodedTextScanner.ShippedAssets();

        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes) if (s.enabled) scenes.Add(s.path);
        if (File.Exists(HardcodedTextScanner.DemoScene) && !scenes.Contains(HardcodedTextScanner.DemoScene)) scenes.Add(HardcodedTextScanner.DemoScene);
        foreach (string path in scenes)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            string scope = Path.GetFileNameWithoutExtension(path) == "MenuScene" ? "ui.menu" : "ui.game";
            GameObject[] roots = scene.GetRootGameObjects();
            HashSet<UnityEngine.Object> referenced = ReferencedByGameScripts(roots);
            foreach (GameObject root in roots)
                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                    AddRow(rows, keyText, existing, label, path, scope, referenced, PrefabUtility.IsPartOfPrefabInstance(label));
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!shipped.Contains(path) || path.Contains("TextMesh Pro")) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<TMP_Text>(true) == null) continue;
            HashSet<UnityEngine.Object> referenced = ReferencedByGameScripts(new[] { prefab });
            string scope = "ui." + HardcodedTextScanner.Slug(Path.GetFileNameWithoutExtension(path));
            foreach (TMP_Text label in prefab.GetComponentsInChildren<TMP_Text>(true))
                AddRow(rows, keyText, existing, label, path, scope, referenced, false);
        }
        return rows;
    }

    private static void AddRow(List<Row> rows, Dictionary<string, string> keyText, Dictionary<string, (string en, string tr)> existing,
        TMP_Text label, string location, string scope, HashSet<UnityEngine.Object> referenced, bool prefabInstance)
    {
        if (label.GetComponent<LocalizedText>() != null) return;
        string text = label.text;
        if (!HardcodedTextScanner.IsText(text)) return;
        string action = prefabInstance ? PrefabPart
            : referenced.Contains(label) || referenced.Contains(label.gameObject) ? Code
            : HasGameScriptNear(label) ? CodeMaybe : Bind;
        string detected = LooksTurkish(text) ? "tr" : "en";
        string key = UniqueKey(keyText, scope + "." + ElementName(label.transform), text);
        string en = detected == "en" ? text : "", tr = detected == "tr" ? text : "";
        if (existing.TryGetValue(key, out var have)) { en = have.en; tr = have.tr; }
        rows.Add(new Row
        {
            Action = action, Location = location, ObjectPath = PathOf(label.transform), Text = text, Key = key,
            En = en, Tr = tr, Detected = detected, Note = "",
        });
    }

    // Oyun betiklerinin (Assembly-CSharp) serileştirilmiş alanlarında tuttuğu nesneler: bu yazıları kod yönetir.
    private static HashSet<UnityEngine.Object> ReferencedByGameScripts(IEnumerable<GameObject> roots)
    {
        var set = new HashSet<UnityEngine.Object>();
        foreach (GameObject root in roots)
            foreach (MonoBehaviour mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null || mb is LocalizedText || !IsGameScript(mb)) continue;
                SerializedProperty p = new SerializedObject(mb).GetIterator();
                while (p.Next(true))
                    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue != null)
                        set.Add(p.objectReferenceValue);
            }
        return set;
    }

    private static bool IsGameScript(MonoBehaviour mb) => mb.GetType().Assembly.GetName().Name == "Assembly-CSharp";

    // Yazının kendisinde ya da üst nesnelerinde (prefab / sahne kökene kadar) bir oyun betiği var mı (GetComponentInChildren ile
    // yazı değiştirebilir). Yalnız görsel / düzen betikleri (Comic*, Option*) sayılmaz.
    private static bool HasGameScriptNear(TMP_Text label)
    {
        for (Transform t = label.transform; t != null; t = t.parent)
            foreach (MonoBehaviour mb in t.GetComponents<MonoBehaviour>())
            {
                if (mb == null || mb is LocalizedText || !IsGameScript(mb)) continue;
                string n = mb.GetType().Name;
                if (n.StartsWith("Comic", StringComparison.Ordinal) || n.StartsWith("Option", StringComparison.Ordinal)) continue;
                return true;
            }
        return false;
    }

    private static bool LooksTurkish(string text)
    {
        if (text.IndexOfAny("çğıöşüÇĞİÖŞÜ".ToCharArray()) >= 0) return true;
        string lower = text.ToLowerInvariant();
        foreach (string w in lower.Split(new[] { ' ', '\n', '/', '-', '!', '?', '.', ',', ':' }, StringSplitOptions.RemoveEmptyEntries))
            if (Array.IndexOf(TurkishWords, w) >= 0) return true;
        return false;
    }

    // Anahtarın öğe kısmı: yazı nesnesinden yukarı, genel olmayan en fazla iki ad ("Play Button/Text (TMP)" → play_button).
    private static string ElementName(Transform t)
    {
        var names = new List<string>();
        for (; t != null && names.Count < 2; t = t.parent)
        {
            string n = t.name.Trim();
            int paren = n.IndexOf(" (", StringComparison.Ordinal);
            string bare = paren > 0 ? n.Substring(0, paren) : n;
            if (Generic.Contains(n) || Generic.Contains(bare) || bare.Equals("Canvas", StringComparison.OrdinalIgnoreCase)) continue;
            names.Insert(0, n);
        }
        return HardcodedTextScanner.Slug(names.Count > 0 ? string.Join("_", names) : "text");
    }

    // Aynı anahtar başka bir metne verilmişse sonuna sayı eklenir; aynı metinse paylaşılır.
    private static string UniqueKey(Dictionary<string, string> keyText, string key, string text)
    {
        string candidate = key;
        for (int i = 2; keyText.TryGetValue(candidate, out string other) && other != text; i++) candidate = key + "_" + i;
        keyText[candidate] = text;
        return candidate;
    }

    private static Dictionary<string, (string en, string tr)> ExistingTexts()
    {
        var map = new Dictionary<string, (string, string)>();
        string csv = File.Exists(GameText.MasterCsvPath) ? File.ReadAllText(GameText.MasterCsvPath) : null;
        if (csv == null) return map;
        List<List<string>> rows = GameText.Parse(csv);
        List<string> header = rows[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        int en = header.IndexOf("en"), tr = header.IndexOf("tr");
        for (int r = 1; r < rows.Count; r++)
            if (rows[r].Count > 0 && rows[r][0].Trim().Length > 0)
                map[rows[r][0].Trim()] = (Cell(rows[r], en), Cell(rows[r], tr));
        return map;
    }

    // ---------- yol ----------
    public static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent)
            parts.Insert(0, t.name.Replace("/", "∕") + "#" + (t.parent != null ? t.GetSiblingIndex() : RootIndex(t)));
        return string.Join("/", parts);
    }

    private static int RootIndex(Transform root)
    {
        if (!root.gameObject.scene.IsValid()) return 0;   // prefab kökü
        GameObject[] roots = root.gameObject.scene.GetRootGameObjects();
        return Array.IndexOf(roots, root.gameObject);
    }

    public static Transform Resolve(IList<GameObject> roots, string path)
    {
        string[] parts = path.Split('/');
        if (parts.Length == 0) return null;
        (string name, int index) Split(string part)
        {
            int hash = part.LastIndexOf('#');
            return (part.Substring(0, hash).Replace("∕", "/"), int.Parse(part.Substring(hash + 1)));
        }
        var (rootName, rootIndex) = Split(parts[0]);
        Transform current = rootIndex < roots.Count && roots[rootIndex].name == rootName ? roots[rootIndex].transform
            : roots.FirstOrDefault(r => r.name == rootName)?.transform;
        for (int i = 1; current != null && i < parts.Length; i++)
        {
            var (name, index) = Split(parts[i]);
            Transform child = index < current.childCount ? current.GetChild(index) : null;
            current = child != null && child.name == name ? child : null;
        }
        return current;
    }

    // ---------- dosya ----------
    public static void WritePlan(List<Row> rows, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var sb = new StringBuilder();
        sb.Append(string.Join(",", Header)).Append('\n');
        foreach (Row r in rows.OrderBy(r => r.Action != Bind).ThenBy(r => r.Action).ThenBy(r => r.Key, StringComparer.Ordinal))
            sb.Append(string.Join(",", new[] { r.Action, r.Location, r.ObjectPath, r.Text, r.Key, r.En, r.Tr, r.Detected, r.Note }.Select(Q))).Append('\n');
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static List<Row> ReadPlan(string path)
    {
        var rows = new List<Row>();
        List<List<string>> table = GameText.Parse(File.ReadAllText(path));
        if (table.Count == 0) return rows;
        List<string> header = table[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        int Col(string name) => header.IndexOf(name);
        for (int i = 1; i < table.Count; i++)
        {
            List<string> c = table[i];
            if (c.Count == 0 || c.All(string.IsNullOrWhiteSpace)) continue;
            rows.Add(new Row
            {
                Action = Cell(c, Col("islem")), Location = Cell(c, Col("yer")), ObjectPath = Cell(c, Col("nesne")), Text = Cell(c, Col("metin")),
                Key = Cell(c, Col("anahtar")), En = Cell(c, Col("en")), Tr = Cell(c, Col("tr")), Detected = Cell(c, Col("algilanan")), Note = Cell(c, Col("not")),
            });
        }
        return rows;
    }

    private static string Cell(List<string> row, int column) => column >= 0 && column < row.Count ? row[column].Trim() : "";

    private static string Q(string s)
    {
        s ??= "";
        s = s.Replace("\r", "").Replace("\n", "<br>");
        return s.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    // ---------- uygula ----------
    public sealed class ApplyReport
    {
        public int KeysAdded, KeysKept, Bound, AlreadyBound;
        public readonly List<string> Skipped = new();
        public override string ToString() =>
            $"listeye {KeysAdded} yeni anahtar ({KeysKept} zaten vardı), {Bound} yazı bağlandı ({AlreadyBound} zaten bağlıydı), {Skipped.Count} atlandı";
    }

    [MenuItem("Tools/Çeviri/Sahne ve prefab yazıları: planı uygula", priority = 11)]
    private static void ApplyMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(PlanPath)) { EditorUtility.DisplayDialog("Çeviri", "Plan yok: önce 'plan çıkar'.", "Tamam"); return; }
        List<Row> rows = ReadPlan(PlanPath);
        int count = rows.Count(r => r.Action == Bind);
        if (!EditorUtility.DisplayDialog("Çeviri", $"{count} 'bagla' satırı uygulanacak: listeye eklenir, yazılara LocalizedText bağlanır, sahneler ve prefab'lar kaydedilir.", "Uygula", "Vazgeç")) return;
        try
        {
            ApplyReport report = Apply(rows);
            Debug.Log("Yazı bağlama: " + report + (report.Skipped.Count > 0 ? "\n- " + string.Join("\n- ", report.Skipped) : ""));
            EditorUtility.DisplayDialog("Çeviri", report + (report.Skipped.Count > 0 ? "\nAtlananlar Console'da." : ""), "Tamam");
        }
        catch (LocalizationSetup.InvalidListException e) { EditorUtility.DisplayDialog("Çeviri listesi hatalı", e.Message, "Tamam"); }
    }

    // "bagla" satırları: önce liste (denetlenip yazılır), sonra sahneler / prefab'lar. Nesnenin bugünkü metni plandakiyle aynı
    // değilse dokunulmaz (yanlış nesneye bağlanmasın; plan eskimiş olabilir).
    public static ApplyReport Apply(List<Row> rows)
    {
        var report = new ApplyReport();
        List<Row> bind = rows.Where(r => r.Action == Bind).ToList();
        foreach (Row r in bind.Where(r => string.IsNullOrEmpty(r.Key) || string.IsNullOrEmpty(r.En)).ToList())
        {
            report.Skipped.Add($"{r.Location} {r.ObjectPath}: anahtar ya da İngilizce boş");
            bind.Remove(r);
        }
        AddToList(bind, report);

        foreach (IGrouping<string, Row> group in bind.GroupBy(r => r.Location))
        {
            string path = group.Key;
            if (path.EndsWith(".unity", StringComparison.Ordinal))
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                bool opened = !scene.isLoaded;
                if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                if (BindAll(group, scene.GetRootGameObjects(), report)) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
            else if (path.EndsWith(".prefab", StringComparison.Ordinal))
            {
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try { if (BindAll(group, new[] { root }, report)) PrefabUtility.SaveAsPrefabAsset(root, path); }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            else foreach (Row r in group) report.Skipped.Add($"{path}: sahne ya da prefab değil");
        }
        AssetDatabase.SaveAssets();
        return report;
    }

    private static bool BindAll(IEnumerable<Row> rows, IList<GameObject> roots, ApplyReport report)
    {
        bool changed = false;
        foreach (Row r in rows)
        {
            Transform t = Resolve(roots, r.ObjectPath);
            TMP_Text label = t != null ? t.GetComponent<TMP_Text>() : null;
            if (label == null) { report.Skipped.Add($"{r.Location} {r.ObjectPath}: nesne bulunamadı"); continue; }
            if (Normalize(label.text) != r.Text) { report.Skipped.Add($"{r.Location} {r.ObjectPath}: metin plandakiyle aynı değil ('{Normalize(label.text)}')"); continue; }
            LocalizedText loc = label.GetComponent<LocalizedText>();
            if (loc != null && loc.Key == r.Key) { report.AlreadyBound++; continue; }
            if (loc == null) loc = label.gameObject.AddComponent<LocalizedText>();
            var so = new SerializedObject(loc);
            so.FindProperty("key").stringValue = r.Key;
            so.ApplyModifiedPropertiesWithoutUndo();
            report.Bound++;
            changed = true;
        }
        return changed;
    }

    private static string Normalize(string s) => (s ?? "").Replace("\r", "").Replace("\n", "<br>").Trim();

    // Yeni anahtarlar ana listenin sonuna eklenir (çevirmen notuyla). Var olan anahtara dokunulmaz. Yeni liste önce denetlenir.
    private static void AddToList(List<Row> bind, ApplyReport report)
    {
        string csv = File.ReadAllText(GameText.MasterCsvPath);
        List<List<string>> table = GameText.Parse(csv);
        List<string> header = table[0].ConvertAll(h => h.Trim().ToLowerInvariant());
        var have = new HashSet<string>(table.Skip(1).Where(r => r.Count > 0).Select(r => r[0].Trim()));
        var sb = new StringBuilder(csv.TrimEnd('\r', '\n')).Append('\n');
        bool any = false;
        foreach (IGrouping<string, Row> g in bind.GroupBy(r => r.Key))
        {
            if (have.Contains(g.Key)) { report.KeysKept++; continue; }
            Row r = g.First();
            string place = Path.GetFileNameWithoutExtension(r.Location) + " > " +
                           string.Join("/", r.ObjectPath.Split('/').Reverse().Take(2).Reverse().Select(p => p.Substring(0, Math.Max(0, p.LastIndexOf('#'))).Replace("∕", "/")));
            string note = string.IsNullOrEmpty(r.Note) ? $"UI label ({place}). Keep about the same length." : r.Note;
            var cells = new string[header.Count];
            for (int c = 0; c < header.Count; c++)
                cells[c] = header[c] switch { "key" => g.Key, GameText.NoteColumn => note, "en" => r.En, "tr" => r.Tr, _ => "" };
            sb.Append(string.Join(",", cells.Select(Q))).Append('\n');
            report.KeysAdded++;
            any = true;
        }
        if (!any) return;
        string next = sb.ToString();
        LocalizationSetup.Validate(next);   // hatalıysa hiçbir şey yazılmaz
        File.WriteAllText(GameText.MasterCsvPath, next, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(GameText.MasterCsvPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
    }

    // Batch: verilen planı uygular (izole kopya). Plan yolu: CLICKER_BIND_PLAN ortam değişkeni ya da Docs/Ceviri/Baglama_Plani.csv.
    public static void ApplyBatch()
    {
        int exit = 1;
        try
        {
            string plan = Environment.GetEnvironmentVariable("CLICKER_BIND_PLAN");
            if (string.IsNullOrEmpty(plan)) plan = PlanPath;
            ApplyReport report = Apply(ReadPlan(plan));
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/Baglama_Uygula.txt", "PASS " + report + (report.Skipped.Count > 0 ? "\n- " + string.Join("\n- ", report.Skipped) : ""));
            exit = 0;
        }
        catch (Exception e) { File.WriteAllText("Logs/Baglama_Uygula.txt", "FAIL " + e); }
        EditorApplication.Exit(exit);
    }

    // Batch: planı çıkarır (izole kopya; Logs/Baglama_Plani.csv).
    public static void PlanBatch()
    {
        int exit = 1;
        try
        {
            List<Row> rows = BuildPlan();
            Directory.CreateDirectory("Logs");
            WritePlan(rows, Path.GetFullPath("Logs/Baglama_Plani.csv"));
            File.WriteAllText("Logs/Baglama_Plani.txt", $"PASS {rows.Count} rows: {Summary(rows)}");
            exit = 0;
        }
        catch (Exception e) { File.WriteAllText("Logs/Baglama_Plani.txt", "FAIL " + e); }
        EditorApplication.Exit(exit);
    }
}
