using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Çeviri tablosuna bağlanmamış oyuncu yazılarını bulur (4 Kasım 2026): çeviri listesinin taslağı ve taşıma işinin yapılacaklar
// listesi. Dört kaynak:
//   sahne   : sahnelerdeki TMP yazıları (LocalizedText bileşeni olmayan, içi gerçek yazı olanlar)
//   prefab  : prefab'lardaki TMP yazıları
//   veri    : ScriptableObject'lerdeki ad / etiket / açıklama metinleri
//   kod     : koddaki yazı atamaları (.text = "…", SetText("…")) ve büyük harfli etiket sabitleri
// Sayı, tek simge, boşluk ve özel kullanım ikon karakterleri yazı sayılmaz. TextMesh Pro örnekleri ve kurtarma sahnesi taranmaz;
// geliştirici araçları (Mechanics Test Lab, test sahnesi) "gelistirici" kategorisine ayrılır (çevrilmesi gerekmez).
// Kod taraması sezgiseldir: kesin liste değil, bakılacak yerler listesidir.
public static class HardcodedTextScanner
{
    public sealed class Item
    {
        public string Category, Location, Field, Text, SuggestedKey;
    }

    private static readonly Regex Trivial = new(@"^[\s\d.,:;%+\-x×/()<>\[\]#*'""!?=_|~·•…]*$");
    private static readonly Regex HexColor = new(@"^#?[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$");
    private static readonly Regex CodeAssign = new(@"(?:\.text\s*=\s*|SetText\(\s*)(\$?""(?:[^""\\]|\\.)*"")");
    private static readonly Regex CodeCaps = new(@"""([A-ZÇĞİÖŞÜ][A-ZÇĞİÖŞÜ0-9]{2,}(?:[ .!?:'][A-ZÇĞİÖŞÜ0-9!?.']+)*)""");
    private static readonly string[] FieldHints = { "name", "label", "text", "title", "description", "rule", "prompt", "tooltip", "subtitle" };
    private static readonly string[] FieldSkips = { "m_name", "id", "key", "guid", "path", "resource", "sound", "clip", "trigger", "sprite", "icon" };

    public static List<Item> Scan()
    {
        var items = new List<Item>();
        ScanScenes(items);
        ScanPrefabs(items);
        ScanData(items);
        ScanCode(items);
        return items;
    }

    public static string Summary(List<Item> items) =>
        string.Join(", ", items.GroupBy(i => i.Category).OrderBy(g => g.Key).Select(g => $"{g.Key}: {g.Count()}"));

    public static bool IsText(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || Trivial.IsMatch(text) || HexColor.IsMatch(text.Trim())) return false;
        // Özel kullanım alanı (ikon fontları) dışında en az bir harf.
        return text.Any(c => char.IsLetter(c) && (c < '' || c > ''));
    }

    private static bool Excluded(string path) =>
        path.Contains("TextMesh Pro") || path.Contains("/_Recovery/") || path.Contains("/SampleScene");

    private static bool DeveloperOnly(string path) =>
        path.Contains("MechanicsTest") || path.Contains("/Tests/") || path.Contains("TestLab") || path.Contains("Debug")
        || path.Contains("PerformanceTrend") || path.Contains("SkillNodeTest");

    private static void ScanScenes(List<Item> items)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Excluded(path)) continue;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            string sceneName = Path.GetFileNameWithoutExtension(path);
            foreach (GameObject root in scene.GetRootGameObjects())
                AddLabels(items, root, DeveloperOnly(path) ? "gelistirici" : "sahne", sceneName, "ui." + Slug(sceneName));
            if (opened) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ScanPrefabs(List<Item> items)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Excluded(path)) continue;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null || prefab.GetComponentInChildren<TMP_Text>(true) == null) continue;
            string name = Path.GetFileNameWithoutExtension(path);
            AddLabels(items, prefab, DeveloperOnly(path) ? "gelistirici" : "prefab", path, "ui." + Slug(name));
        }
    }

    private static void AddLabels(List<Item> items, GameObject root, string category, string location, string keyPrefix)
    {
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label.GetComponent<LocalizedText>() != null) continue;
            string text = label.text;
            if (!IsText(text)) continue;
            string objectPath = PathOf(label.transform);
            items.Add(new Item
            {
                Category = DeveloperOnly(objectPath) ? "gelistirici" : category,
                Location = location + " :: " + objectPath,
                Field = "TMP",
                Text = text,
                SuggestedKey = keyPrefix + "." + Slug(label.gameObject.name == "Text" || label.gameObject.name.StartsWith("Text (")
                    ? label.transform.parent != null ? label.transform.parent.name : label.gameObject.name
                    : label.gameObject.name),
            });
        }
    }

    private static void ScanData(List<Item> items)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/ScriptableObjects" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) continue;
            var so = new SerializedObject(asset);
            SerializedProperty p = so.GetIterator();
            while (p.Next(true))
            {
                if (p.propertyType != SerializedPropertyType.String) continue;
                string field = p.name.ToLowerInvariant();
                if (FieldSkips.Any(s => field == s || field.EndsWith(s))) continue;
                if (!FieldHints.Any(h => field.Contains(h))) continue;
                if (!IsText(p.stringValue)) continue;
                items.Add(new Item
                {
                    Category = DeveloperOnly(path) ? "gelistirici" : "veri",
                    Location = path,
                    Field = p.propertyPath,
                    Text = p.stringValue,
                    SuggestedKey = "data." + Slug(asset.name) + "." + Slug(p.propertyPath),
                });
            }
        }
    }

    private static void ScanCode(List<Item> items)
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        if (!Directory.Exists(root)) return;
        foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = "Assets" + file.Substring(Application.dataPath.Length).Replace('\\', '/');
            if (rel.Contains("/Localization/")) continue;
            string category = DeveloperOnly(rel) ? "gelistirici" : "kod";
            string[] lines = File.ReadAllLines(file);
            for (int n = 0; n < lines.Length; n++)
            {
                string line = lines[n];
                string trimmed = line.TrimStart();
                if (trimmed.StartsWith("//") || line.Contains("Debug.Log") || line.Contains("Exception(") || line.Contains("[MenuItem")
                    || line.Contains("[Tooltip") || line.Contains("[Header") || line.Contains("new GameObject(") || line.Contains("Find(")) continue;
                var found = new HashSet<string>();
                foreach (Match m in CodeAssign.Matches(line))
                {
                    string literal = m.Groups[1].Value;
                    if (IsText(Unquote(literal)) && found.Add(literal)) items.Add(CodeItem(category, rel, n, "atama", Unquote(literal)));
                }
                foreach (Match m in CodeCaps.Matches(line))
                {
                    string literal = m.Groups[1].Value;
                    if (found.Any(f => f.Contains(literal))) continue;
                    if (IsText(literal) && found.Add(literal)) items.Add(CodeItem(category, rel, n, "büyük harf", literal));
                }
            }
        }
    }

    private static Item CodeItem(string category, string file, int line, string kind, string text) => new()
    {
        Category = category,
        Location = $"{file}:{line + 1}",
        Field = kind,
        Text = text,
        SuggestedKey = "code." + Slug(Path.GetFileNameWithoutExtension(file)) + "." + Slug(text.Length > 24 ? text.Substring(0, 24) : text),
    };

    private static string Unquote(string literal)
    {
        string s = literal.StartsWith("$") ? literal.Substring(1) : literal;
        return s.Length >= 2 ? s.Substring(1, s.Length - 2) : s;
    }

    private static string PathOf(Transform t)
    {
        var parts = new List<string>();
        for (; t != null; t = t.parent) parts.Add(t.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    // Anahtar önerisi: küçük harf, Türkçe harfler sadeleşir, harf / rakam dışı "_" olur.
    public static string Slug(string s)
    {
        if (string.IsNullOrEmpty(s)) return "x";
        var sb = new StringBuilder(s.Length);
        foreach (char raw in s.ToLowerInvariant())
        {
            char c = raw switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'İ' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', _ => raw };
            if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') sb.Append(c);
            else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
        }
        return sb.ToString().Trim('_') is { Length: > 0 } slug ? slug : "x";
    }

    // Excel'in Türkçe harfleri doğru açması için UTF-8 BOM; RFC 4180 tırnaklama.
    public static void WriteCsv(List<Item> items, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var sb = new StringBuilder();
        sb.Append("kategori,yer,alan,metin,onerilen_anahtar\n");
        foreach (Item i in items.OrderBy(i => i.Category).ThenBy(i => i.Location))
            sb.Append(Q(i.Category)).Append(',').Append(Q(i.Location)).Append(',').Append(Q(i.Field)).Append(',')
              .Append(Q(i.Text)).Append(',').Append(Q(i.SuggestedKey)).Append('\n');
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Q(string s)
    {
        s ??= "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
