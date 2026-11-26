using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Run sonu ekranını comic kitine geçirir (22 Kasım 2026, v3). Kullanıcı: "emsallere fotoyla bak, benzer kur; kazanınca da aynı his
// veriyor; açıklama yazıları rahatsız etti; kaybedince nasıl kaybettiğimizi anlamıyoruz". Emsal: Balatro (hedef ile skor yan yana,
// "seni yenen" rozeti, kayıp / zafer bütün ekranın rengini değiştirir, tek kompakt panel), Slay the Spire (öldüren sahnede kalır).
// Docs/UI-Dili.md renk görevleri:
//  - kararma: tram kenarlardan kapanır; kayıpta kırmızı-mürekkep, sahne kararır; zaferde altın, sahne açık (RunEndVignette);
//  - damga başlık: kayıp kırmızı uyarı plakası "RUN BİTTİ", zafer altın plaka "ZAFER!";
//  - tek lacivert panel: solda NEDEN kartı (SENİ DURDURAN / SON KOTA; boss rozeti boss'un kendi renginde; KOTA ve BOSS HASADI
//    çubukları: hedef / değer, tutmayan kırmızı, tutan yeşil), sağda kısa tablo (skor, round, boss ödülü, süre), altta TEKRAR (mavi)
//    ve ANA MENÜ (kırmızı). Açıklama cümlesi, başlangıç satırı, süre cümlesi ve uzun ödül listesi yok.
// Her yazı kutusuna göre küçülür, kesilmez. Görünüm sahneye kaydedilir (GameScene, DemoScene), editörde düzenlenir; RunCompleteUI
// yalnız değerleri / çubukları / plaka rengini, RunEndVignette yalnız hareketi ve kayıp / zafer rengini verir. Tekrar çalıştırılabilir.
public static class RunEndScreenInstaller
{
    const string Pack = "Assets/Resources/ComicPack/", Hud = "Assets/Resources/HudKit/", Art = "Assets/Art/UI/ComicPack/";
    const string ShaderPath = "Assets/Shaders/UI/HalftoneVignette.shader", MaterialPath = "Assets/Materials/UI/RunEndVignette.mat";
    static readonly string[] Scenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/DemoScene.unity" };
    static readonly Color Ink = new Color32(54, 39, 54, 255);
    static readonly Color Cream = new Color32(255, 242, 210, 255);
    static readonly Color CreamDim = new Color32(226, 210, 184, 255);
    static readonly Color InkDark = new Color32(30, 21, 32, 255);
    const float CardCorners = 2.2f;   // krem kartların 9-dilim çarpanı: köşe süsleri küçük kalsın, yazıya binmesin
    static ComicUITheme theme;

    [MenuItem("Tools/Comic UI/Run Sonu Ekranı")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Run sonu", "Play modundan çıkıp tekrar dene.", "Tamam"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = SceneManager.GetActiveScene().path;
        var report = new System.Text.StringBuilder();
        foreach (string path in Scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string error = InstallInOpenScene();
            if (error == null) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.AppendLine(path + ": kuruldu"); }
            else report.AppendLine(path + ": HATA " + error);
        }
        if (!string.IsNullOrEmpty(start) && SceneManager.GetActiveScene().path != start) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        Debug.Log("RUN_END_INSTALL\n" + report);
    }

    // Komut satırı (Unity kapalıyken): -batchmode -executeMethod RunEndScreenInstaller.InstallScenesBatch. Hata varsa çıkış kodu 1.
    public static void InstallScenesBatch()
    {
        try
        {
            int failed = 0;
            var report = new System.Text.StringBuilder();
            foreach (string path in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                string error = InstallInOpenScene();
                if (error == null) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.AppendLine(path + ": kuruldu"); }
                else { failed++; report.AppendLine(path + ": HATA " + error); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RUN_END_INSTALL\n" + report);
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
        catch (System.Exception e) { Debug.LogError("RUN_END_INSTALL hata: " + e); EditorApplication.Exit(1); }
    }

    // Açık sahnenin run sonu panelini kurar. Başarıda null. Sahneyi kaydetmez (kararma malzemesi yoksa asset olarak oluşturur).
    public static string InstallInOpenScene()
    {
        theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
        if (theme == null || theme.displayFont == null || theme.bodyFont == null) return "ComicUITheme / fontlar yok";
        Material vignetteMaterial = VignetteMaterial();
        if (vignetteMaterial == null) return "HalftoneVignette shader'ı yok";
        var ui = Object.FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        if (ui == null) return "UIManager yok";
        var panelObject = new SerializedObject(ui).FindProperty("runCompletePanel").objectReferenceValue as GameObject;
        if (panelObject == null) return "runCompletePanel yok";
        var screen = panelObject.GetComponentInChildren<RunCompleteUI>(true);
        if (screen == null) return "RunCompleteUI yok";
        Transform panel = screen.transform;

        // TEKRAR / ANA MENÜ önceki kurulumda panelin içine taşınmıştı: konteyner silinmeden önce dışarı alınır (26 Kasım 2026:
        // ikinci çalıştırma iki butonu da siliyordu). Butonlar aşağıda yeniden panele yerleşir; bağlantıları ve yazıları korunur.
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
            if (button.GetComponent<RestartButtonUI>() != null || button.GetComponent<MainMenuButtonUI>() != null)
                button.transform.SetParent(panel, false);

        // Önceki kit kurulumu ve eski çalışma anı yerleşimi kalkar; eski yazı kapanır (alan eski yol için kalır).
        foreach (string name in new[] { "Backdrop", "Result Window", "Result Content", "Result Panel", "Result backdrop", "Result paper", "Result scrollbar" })
        {
            Transform old = panel.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var legacy = panel.GetComponent<RunResultLayout>();
        if (legacy != null) Object.DestroyImmediate(legacy);
        var screenSo = new SerializedObject(screen);
        var oldText = screenSo.FindProperty("scoreText").objectReferenceValue as TextMeshProUGUI;
        if (oldText != null)
        {
            Transform holder = oldText.transform.parent != panel ? oldText.transform.parent : oldText.transform;
            holder.gameObject.SetActive(false);
        }

        // Kararma: tam ekran tram (rengi kayıp / zafere göre RunEndVignette verir); tıklamayı tutar (arkadaki sahneye basılmaz).
        var backdrop = Rect("Backdrop", panel, Vector2.zero, Vector2.zero);
        Stretch(backdrop);
        var dim = backdrop.gameObject.AddComponent<Image>();
        dim.material = vignetteMaterial;
        dim.color = InkDark;
        dim.raycastTarget = true;
        backdrop.SetAsFirstSibling();
        var content = Rect("Result Content", panel, Vector2.zero, Vector2.zero);
        Stretch(content);
        content.SetSiblingIndex(1);
        var contentGroup = content.gameObject.AddComponent<CanvasGroup>();
        var pops = new List<RectTransform>();

        // Tek panel (Balatro gibi ortada, kompakt): lacivert pencere.
        const float panelWidth = 1180f, panelHeight = 640f;
        var window = Rect("Result Panel", content, new Vector2(0f, -36f), new Vector2(panelWidth, panelHeight));
        Sliced("Frame", window, Pack + "window_navy.png", Vector2.zero, new Vector2(panelWidth, panelHeight), 1.15f).raycastTarget = true;
        pops.Add(window);

        // Damga başlık: kayıp kırmızı uyarı plakası, zafer altın plaka (sprite'ı RunCompleteUI verir).
        var plate = Sliced("Title Plate", content, Hud + "hud_plate_red.png", new Vector2(0f, 330f), new Vector2(860f, 136f), 0f);
        plate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -2f);
        var title = Label("Title", plate.transform, 70f, 24f, true, Color.white, TextAlignmentOptions.Center, false);
        Inset(title.rectTransform, 56f, 66f, 26f, 16f);
        pops.Insert(0, plate.rectTransform);

        // Sol: NEDEN bloğu (krem kart): başlık, boss rozeti, KOTA ve BOSS HASADI çubukları.
        var cause = Sliced("Cause Card", window, Pack + "panel_cream.png", new Vector2(-282f, 26f), new Vector2(540f, 440f), CardCorners);
        var causeHeader = Label("Header", cause.transform, 30f, 14f, false, Ink, TextAlignmentOptions.Center, false);
        Top(causeHeader.rectTransform, 60f, 70f, 30f, 44f);
        var badge = Sliced("Boss Badge", cause.transform, Hud + "hud_plate_navy.png", Vector2.zero, new Vector2(400f, 70f), 0f);
        var badgeRect = badge.rectTransform;
        badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(.5f, 1f); badgeRect.pivot = new Vector2(.5f, 1f);
        badgeRect.anchoredPosition = new Vector2(-4f, -86f);
        badgeRect.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
        var bossName = Label("Name", badge.transform, 34f, 14f, true, Color.white, TextAlignmentOptions.Center, false);
        Inset(bossName.rectTransform, 28f, 34f, 12f, 8f);
        (TMP_Text value, RectTransform fill, Image fillImage, RectTransform row) Row(string name, string key, float y)
        {
            var rowRect = Rect(name, cause.transform, Vector2.zero, new Vector2(420f, 110f));
            rowRect.anchorMin = rowRect.anchorMax = new Vector2(.5f, 1f); rowRect.pivot = new Vector2(.5f, 1f);
            rowRect.anchoredPosition = new Vector2(-4f, y);
            var label = Label("Label", rowRect, 24f, 12f, false, Ink, TextAlignmentOptions.MidlineLeft, false);
            Top(label.rectTransform, 0f, 230f, 0f, 38f);    // etiket 0–190, sayı 200–420: iki kutu ayrık (26 Kasım: üst üste biniyordu)
            Loc(label, key);
            var value = Label("Value", rowRect, 34f, 14f, false, Ink, TextAlignmentOptions.MidlineRight, false);
            Top(value.rectTransform, 200f, 0f, -4f, 44f);
            var track = Sliced("Track", rowRect, Art + "bar_track.png", Vector2.zero, Vector2.zero, 116f / 46f);
            Top(track.rectTransform, 0f, 0f, 52f, 46f);
            var fillRect = Rect("Fill", track.transform, Vector2.zero, Vector2.zero);
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = new Vector2(.6f, 1f); fillRect.pivot = new Vector2(0f, .5f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            var fillImage = fillRect.gameObject.AddComponent<Image>();
            fillImage.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "bar_fill.png");
            fillImage.type = Image.Type.Sliced;
            fillImage.pixelsPerUnitMultiplier = 116f / 46f;
            fillImage.raycastTarget = false;
            return (value, fillRect, fillImage, rowRect);
        }
        var quota = Row("Quota Row", "ui.runend.quota_label", -176f);
        var bossBar = Row("Boss Row", "ui.runend.boss_label", -296f);
        // Gösterilecek hedef yoksa (zafer, son round'da kota yok): tek satır yeşil not.
        var note = Label("All Met", cause.transform, 34f, 14f, false, new Color32(62, 122, 44, 255), TextAlignmentOptions.Center, true);
        Inset(note.rectTransform, 60f, 70f, 120f, 150f);
        Loc(note, "ui.runend.all_quotas_met");

        // Sağ: kısa tablo (krem şeritler; etiket solda, değer sağda).
        string[] statKeys = { "ui.runend.score_label", "ui.runend.round_label", "ui.runend.rewards_label", "ui.runend.time_label" };
        var values = new TMP_Text[statKeys.Length];
        for (int i = 0; i < statKeys.Length; i++)
        {
            var strip = Sliced("Stat " + i, window, Pack + "label_cream.png", new Vector2(282f, 190f - i * 104f), new Vector2(520f, 88f), 0f);
            if (i % 2 == 1) strip.rectTransform.localScale = new Vector3(-1f, 1f, 1f);   // aynı kesik art arda tekrar etmesin
            var row = Rect("Row " + i, window, new Vector2(282f, 190f - i * 104f), new Vector2(520f, 88f));
            var label = Label("Label", row, 22f, 11f, false, Ink, TextAlignmentOptions.MidlineLeft, true);
            Inset(label.rectTransform, 34f, 264f, 14f, 8f);   // etiket 34–256, sayı 264–480: ayrık
            Loc(label, statKeys[i]);
            var value = Label("Value", row, 44f, 16f, false, Ink, TextAlignmentOptions.MidlineRight, false);
            Inset(value.rectTransform, 264f, 40f, 12f, 6f);
            value.text = "0";
            values[i] = value;
        }

        // Butonlar: panelin içinde, altta; TEKRAR mavi (ana akış), ANA MENÜ kırmızı (çık).
        foreach (Button button in panel.GetComponentsInChildren<Button>(true))
        {
            bool restart = button.GetComponent<RestartButtonUI>() != null;
            bool menu = button.GetComponent<MainMenuButtonUI>() != null;
            if (!restart && !menu) continue;
            button.transform.SetParent(window, false);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(restart ? -282f : 282f, -248f);
            rect.sizeDelta = new Vector2(500f, 92f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            KitButton(button, restart ? "blue" : "red");
            pops.Add(rect);
        }

        var vignette = panel.GetComponent<RunEndVignette>();
        if (vignette == null) vignette = panel.gameObject.AddComponent<RunEndVignette>();
        vignette.Setup(dim, contentGroup, pops.ToArray());
        EditorUtility.SetDirty(vignette);

        foreach (string unused in new[] { "reasonText", "detailText", "rewardsHeader", "rewardsText", "timesText", "rewardsScroll" })
        {
            SerializedProperty property = screenSo.FindProperty(unused);
            if (property != null) property.objectReferenceValue = null;
        }
        Set(screenSo, "titleText", title); Set(screenSo, "titlePlate", plate);
        Set(screenSo, "failPlate", AssetDatabase.LoadAssetAtPath<Sprite>(Hud + "hud_plate_red.png"));
        Set(screenSo, "winPlate", AssetDatabase.LoadAssetAtPath<Sprite>(Hud + "hud_panel_yellow.png"));
        Set(screenSo, "scoreValue", values[0]); Set(screenSo, "roundValue", values[1]);
        Set(screenSo, "rewardCountValue", values[2]); Set(screenSo, "timeValue", values[3]);
        Set(screenSo, "causeHeader", causeHeader); Set(screenSo, "bossBadge", badge.gameObject); Set(screenSo, "bossName", bossName);
        Set(screenSo, "quotaValue", quota.value); Set(screenSo, "quotaFill", quota.fill); Set(screenSo, "quotaFillImage", quota.fillImage);
        Set(screenSo, "bossRow", bossBar.row.gameObject); Set(screenSo, "bossValue", bossBar.value);
        Set(screenSo, "bossFill", bossBar.fill); Set(screenSo, "bossFillImage", bossBar.fillImage);
        Set(screenSo, "allMetNote", note.gameObject);
        screenSo.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(screen);
        return null;
    }

    // Kararma malzemesi (asset): yoksa shader'dan oluşturulur; ayarları editörde değiştirilebilir.
    static Material VignetteMaterial()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        if (shader == null) return null;
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null)
        {
            if (material.shader == shader) return material;
            // Shader'ın kimliği değiştiyse (ör. .meta yeniden üretildi) malzeme hata shader'ına düşer ve ayarları sıfırlanır (köşe biçimi 0
            // olunca bütün ekran mürekkep olur): silinip shader'ın varsayılanlarıyla yeniden oluşturulur.
            AssetDatabase.DeleteAsset(MaterialPath);
        }
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MaterialPath));
        material = new Material(shader) { name = "RunEndVignette" };
        material.SetColor("_Color", Color.white);   // renk Image'tan (RunEndVignette kayıp / zafere göre verir)
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }

    static void KitButton(Button button, string colour)
    {
        foreach (var old in button.GetComponents<ComicButtonVisual>()) Object.DestroyImmediate(old, true);
        var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        image.material = null;
        image.color = Color.white;
        image.sprite = PackSprite($"button_{colour}_normal");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = image.sprite.rect.height / ((RectTransform)button.transform).sizeDelta.y;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = PackSprite($"button_{colour}_hover"),
            pressedSprite = PackSprite($"button_{colour}_pressed"),
            selectedSprite = image.sprite,
            disabledSprite = PackSprite($"button_{colour}_disabled"),
        };
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            Display(label, true, Color.white, 38f, 16f, true);
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Center;
            Inset(label.rectTransform, 30f, 30f, 20f, 6f);
        }
        if (button.GetComponent<ComicHoverMotion>() == null) button.gameObject.AddComponent<ComicHoverMotion>();
    }

    // ---------------------------------------------------------------- yapı taşları
    static Sprite PackSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Pack + name + ".png");

    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)) { layer = 5 }.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    // 9-dilim; multiplier 0: dikeyde 1:1 (sprite yüksekliği / hedef yükseklik).
    static Image Sliced(string name, Transform parent, string spritePath, Vector2 position, Vector2 size, float multiplier)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier > 0f ? multiplier : image.sprite.rect.height / size.y;
        image.raycastTarget = false;
        return image;
    }

    static TMP_Text Label(string name, Transform parent, float max, float min, bool outlined, Color color, TextAlignmentOptions alignment, bool wrap)
    {
        var text = Rect(name, parent, Vector2.zero, Vector2.zero).gameObject.AddComponent<TextMeshProUGUI>();
        Display(text, outlined, color, max, min, wrap);
        text.alignment = alignment;
        return text;
    }

    // Gövde fontu (okunacak yazı): kutuya göre küçülür, alt satıra geçer, kesilmez.
    static TMP_Text Body(string name, Transform parent, Vector2 position, Vector2 size, float max, float min, Color color, TextAlignmentOptions alignment)
    {
        var text = Rect(name, parent, position, size).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = theme.bodyFont;
        text.fontSharedMaterial = theme.bodyFont.material;
        text.color = color;
        text.fontSize = max;
        text.enableAutoSizing = true;
        text.fontSizeMax = max;
        text.fontSizeMin = min;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    static void Display(TMP_Text text, bool outlined, Color color, float max, float min, bool wrap)
    {
        text.font = theme.displayFont;
        text.fontSharedMaterial = outlined && theme.displayOutline != null ? theme.displayOutline : theme.displayFont.material;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.fontSize = max;
        text.enableAutoSizing = true;
        text.fontSizeMax = max;
        text.fontSizeMin = min;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.extraPadding = true;
        text.raycastTarget = false;
    }

    static void Loc(TMP_Text label, string key)
    {
        var loc = label.GetComponent<LocalizedText>();
        if (loc == null) loc = label.gameObject.AddComponent<LocalizedText>();
        var so = new SerializedObject(loc);
        so.FindProperty("key").stringValue = key;
        so.ApplyModifiedPropertiesWithoutUndo();
        label.text = GameText.Get(key);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static void Inset(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
    }

    // Üst kenara yaslı şerit: soldan / sağdan içe, üstten uzaklık, yükseklik.
    static void Top(RectTransform rect, float left, float right, float top, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1f);
        rect.offsetMin = new Vector2(left, -top - height); rect.offsetMax = new Vector2(-right, -top);
    }

    static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
}
