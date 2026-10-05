using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Batch (izole kopya): Bölüm 3.7.6.2 — GameScene'deki doğrulanmış kalıntıları temizler ve kaydeder (asıl projeye sonuç dosyası elle
// taşınır; açık editöre komut gönderilmez). Tek seferlik bir araçtır: temizlenmiş sahnede yeniden çalıştırılırsa hedefleri
// bulamaz ve hiçbir şey değiştirmeden başarısız biter. Silmeden önce bağımlılık yeniden denetlenir: silinecek alt ağaçların dışındaki bir
// bileşen onlara (ya da içlerindeki bir bileşene) referans veriyorsa hiçbir şey silinmez, sahne kaydedilmez, iş başarısız biter.
// Silinenler: kapalı beş eski "Sellect Grass planter…" butonu (alt nesneleriyle), görünmeyen "Legacy Stats" metni, çağrılmayan
// "Sound Manager". Bağlanan: UIManager.resourcesUI (çalışma anındaki yedeğin bulduğu nesne: GoldUI'nin ebeveyni "ResourcesUI").
// Korunanlar (sayıları önce / sonra karşılaştırılır): zemin hücreleri (GroundCell), skill UI yuvaları (SkillNodeUI).
// Kodda karşılığı kalmamış serileştirilmiş alanlar (RoundManager exhaust*, ProgressionManager.possibleModifiers,
// PlanterShopPanelUI.stats) sahne kaydedilirken Unity tarafından yazılmaz.
public static class SceneCleanup3762
{
    const string ScenePath = "Assets/Scenes/GameScene.unity";
    const string Report = "Logs/SceneCleanup3762.txt";
    static readonly string[] Buttons = { "Sellect Grass planter", "Sellect Grass planter 1x2", "Sellect Grass planter 1x3", "Sellect Grass planter 2x2", "Sellect Grass planter 2x3" };

    public static void RunBatch()
    {
        var log = new StringBuilder();
        int code = 1;
        try
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject).ToList();
            int cells0 = Count<GroundCell>(scene), slots0 = Count<SkillNodeUI>(scene);

            var targets = new List<GameObject>();
            foreach (string name in Buttons) targets.Add(Single(all, name, go => !go.activeSelf));
            targets.Add(Single(all, "Legacy Stats", go => !go.activeSelf));
            // Tür adıyla: SoundManager sınıfı temizlikten sonra silinir, bu araç yine derlenmelidir.
            targets.Add(Single(all, "Sound Manager", go => go.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "SoundManager")));

            // Silinecek her şey: hedeflerin alt ağaçlarındaki nesneler ve bileşenler.
            var doomed = new HashSet<Object>();
            foreach (var root in targets)
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    doomed.Add(t.gameObject);
                    foreach (var c in t.GetComponents<Component>()) if (c != null) doomed.Add(c);
                }

            // Bağımlılık: kalan bileşenlerin bütün nesne referansları (yapısal Transform bağları hariç).
            var references = new List<string>();
            foreach (var go in all.Where(g => !doomed.Contains(g)))
                foreach (var component in go.GetComponents<Component>())
                {
                    if (component == null) continue;
                    var so = new SerializedObject(component);
                    var it = so.GetIterator();
                    while (it.Next(true))
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue == null) continue;
                        if (component is Transform && (it.propertyPath.StartsWith("m_Children") || it.propertyPath == "m_Father")) continue;
                        if (doomed.Contains(it.objectReferenceValue))
                            references.Add($"{Path(go)} · {component.GetType().Name}.{it.propertyPath} → {it.objectReferenceValue.name} ({it.objectReferenceValue.GetType().Name})");
                    }
                }

            log.AppendLine("Silinecek alt ağaçlar:");
            foreach (var root in targets)
                log.AppendLine($"  {Path(root)} · etkin {root.activeSelf} · {root.GetComponentsInChildren<Transform>(true).Length} nesne · bileşenler: " +
                               string.Join(", ", root.GetComponentsInChildren<Component>(true).Select(c => c.GetType().Name).Distinct()));
            log.AppendLine($"Dışarıdan referans: {references.Count}");
            foreach (string r in references) log.AppendLine("  " + r);
            if (references.Count > 0) throw new Exception("silinecek nesnelere referans var; hiçbir şey silinmedi");

            // UIManager.resourcesUI: çalışma anındaki yedeğin seçtiği nesne açıkça bağlanır.
            var ui = all.Select(g => g.GetComponent<UIManager>()).Single(c => c != null);
            var gold = all.Select(g => g.GetComponent<GoldUI>()).First(c => c != null);
            var resources = gold.transform.parent as RectTransform;
            if (resources == null || resources.name != "ResourcesUI") throw new Exception("GoldUI'nin ebeveyni ResourcesUI değil: " + (resources != null ? resources.name : "yok"));
            var uiObject = new SerializedObject(ui);
            var field = uiObject.FindProperty("resourcesUI");
            log.AppendLine($"UIManager.resourcesUI: önce {(field.objectReferenceValue != null ? field.objectReferenceValue.name : "boş")}, sonra {Path(resources.gameObject)}");
            field.objectReferenceValue = resources;
            uiObject.ApplyModifiedPropertiesWithoutUndo();

            foreach (var root in targets) Object.DestroyImmediate(root);
            int cells1 = Count<GroundCell>(scene), slots1 = Count<SkillNodeUI>(scene);
            log.AppendLine($"Zemin hücresi {cells0} → {cells1} · skill UI yuvası {slots0} → {slots1}");
            if (cells0 != cells1 || slots0 != slots1) throw new Exception("korunması gereken nesne sayısı değişti");
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("sahne kaydedilemedi");
            log.Insert(0, "DONE: GameScene temizlendi ve kaydedildi\n");
            code = 0;
        }
        catch (Exception e)
        {
            log.Insert(0, "FAIL: " + e.Message + "\n");
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, log.ToString(), new UTF8Encoding(false));
        EditorApplication.Exit(code);
    }

    static GameObject Single(List<GameObject> all, string name, Func<GameObject, bool> expected)
    {
        var found = all.Where(g => g.name == name).ToList();
        if (found.Count != 1) throw new Exception($"'{name}': {found.Count} nesne bulundu (1 bekleniyordu)");
        if (!expected(found[0])) throw new Exception($"'{name}': beklenen durumda değil");
        return found[0];
    }

    static int Count<T>(UnityEngine.SceneManagement.Scene scene) where T : Component =>
        scene.GetRootGameObjects().Sum(r => r.GetComponentsInChildren<T>(true).Length);

    static string Path(GameObject go)
    {
        string path = go.name;
        for (var t = go.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
        return path;
    }
}
