using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Kart seçim ekranının ATLA butonunu comic kitine geçirir (22 Kasım 2026; kullanıcı: "atla butonu sıkıntılı, tipini düzelteceğiz,
// yazısını düzelteceğiz, dil değişse de taşmaya uygun olacak"). Docs/UI-Dili.md:
//  - mor kit butonu (ikincil eylem: kart almak ana eylem), 340×76, hâller SpriteSwap (eski yeşil jöle / ComicButtonVisual kalkar);
//  - yazı Bungee beyaz + mürekkep kontur, yalnız "ATLA" (ui.game.skip_cards); kutuya göre küçülür, gerekirse iki satır, kesilmez;
//  - kalan hak köşede altın kare rozette (sayı; dilden bağımsız). CardSelectionUI.skipCount'a bağlanır.
//  - altındaki "KALAN SEÇİM" plakası (kullanıcı: "köşeleri yumuşak, bizim packteki köşeli sprite olsun, font da bizimki"): krem kit
//    şeridi (label_cream) + Bungee koyu mürekkep; eskiden çalışma anında yumuşak köşeli düz plakayla kuruluyordu.
//    CardSelectionUI.remainingLabel'a bağlanır; sayıyı / son seçim yazısını oyun yazar.
//  - kartların yazıları (kullanıcı: "kartlardaki yazıların fontunu da bizim font yapalım"): Grid Mutation Card prefab'ındaki her
//    yazı Bungee; açık renkli yazı beyaz/renkli + mürekkep kontur, koyu yazı fontun kendi malzemesi. Bungee geniş: tasarım boyu
//    ~%12 küçük, kutuya göre küçülür (en az 12), gerekirse alt satıra geçer, kesilmez. Sahnelerde kart yazısı ezilmiyor (yalnız
//    konum), değişiklik prefab'dan iki sahneye de geçer.
// Görünüm sahneye kaydedilir (GameScene, DemoScene), editörde düzenlenir; oyun yalnız sayıyı ve Türkçe büyük İ'yi yazar.
// Tekrar çalıştırılabilir (rozet bir kez kurulur, yeniden çalışınca güncellenir).
public static class CardSkipButtonInstaller
{
    const string Pack = "Assets/Resources/ComicPack/";
    static readonly string[] Scenes = { "Assets/Scenes/GameScene.unity", "Assets/Scenes/DemoScene.unity" };
    static readonly Vector2 Size = new(340f, 76f);
    const float Badge = 46f;
    static readonly Color Ink = new Color32(54, 39, 54, 255);

    [MenuItem("Tools/Comic UI/Kart Seçimi: Kartlar + ATLA + Kalan Seçim + Bilgi Kutusu")]
    public static void InstallAll()
    {
        if (EditorApplication.isPlaying) { EditorUtility.DisplayDialog("Kart seçimi", "Play modundan çıkıp tekrar dene.", "Tamam"); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string start = SceneManager.GetActiveScene().path;
        var report = new System.Text.StringBuilder();
        report.AppendLine(CardPrefab + ": " + (RestyleCardPrefab() ?? "kart yazıları Bungee"));
        foreach (string path in Scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string result = InstallInOpenScene();
            if (result == null) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.AppendLine(path + ": kuruldu"); }
            else report.AppendLine(path + ": " + result);
        }
        if (!string.IsNullOrEmpty(start) && SceneManager.GetActiveScene().path != start) EditorSceneManager.OpenScene(start, OpenSceneMode.Single);
        Debug.Log("CARD_SKIP_INSTALL\n" + report);
    }

    // Editör kapalıyken ana projeye uygulamak için (batch): yalnız iki sahne (ATLA + kalan seçim + bilgi kutusu); kart prefab'ına
    // dokunmaz. Hata olursa 1 ile çıkar.
    //   Unity.exe -batchmode -projectPath <proje> -executeMethod CardSkipButtonInstaller.InstallScenesBatch -logFile <log>
    public static void InstallScenesBatch()
    {
        bool ok = true;
        var report = new System.Text.StringBuilder();
        foreach (string path in Scenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) { report.AppendLine(path + ": yok"); continue; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            string result = InstallInOpenScene();
            if (result == null) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); report.AppendLine(path + ": kuruldu"); }
            else { ok = false; report.AppendLine(path + ": " + result); }
        }
        Debug.Log("CARD_SKIP_INSTALL\n" + report);
        EditorApplication.Exit(ok ? 0 : 1);
    }

    // Açık sahnedeki CardSelectionUI'ın ATLA butonunu kurar. Başarıda null, yoksa nedenini döner. Sahneyi kaydetmez.
    public static string InstallInOpenScene()
    {
        var theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
        if (theme == null || theme.displayFont == null) return "ComicUITheme / Bungee yok";
        var cards = Object.FindFirstObjectByType<CardSelectionUI>(FindObjectsInactive.Include);
        if (cards == null) return "CardSelectionUI yok";
        var so = new SerializedObject(cards);
        var button = so.FindProperty("skipButton").objectReferenceValue as Button;
        if (button == null) return "skipButton bağlı değil";

        // Buton: mor kit sprite'ı, dört hâl aynı silüetten (SpriteSwap); eski palet bileşeni kalkar.
        foreach (var old in button.GetComponents<ComicButtonVisual>()) Object.DestroyImmediate(old, true);
        var image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        var rect = (RectTransform)button.transform;
        rect.sizeDelta = Size;   // pivot altta: buton yukarı doğru büyür, alttaki kalan seçim plakasına binmez
        image.material = null;
        image.color = Color.white;
        image.sprite = Sprite("button_purple_normal");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = image.sprite.rect.height / Size.y;
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Sprite("button_purple_hover"),
            pressedSprite = Sprite("button_purple_pressed"),
            selectedSprite = image.sprite,   // tıklanınca "seçili" kalıp parlamasın
            disabledSprite = Sprite("button_purple_disabled"),
        };
        if (button.GetComponent<ComicHoverMotion>() == null) button.gameObject.AddComponent<ComicHoverMotion>();

        // Yazı: yalnız "ATLA" (hak sayısı rozette). Bungee, beyaz + kontur, sprite'ın üst ~%80'inde (alt şerit 3B kenar).
        TMP_Text label = null;
        foreach (TMP_Text text in button.GetComponentsInChildren<TMP_Text>(true))
            if (text.name != "Count") { label = text; break; }
        if (label == null) return "buton yazısı yok";
        Display(label, theme, true, Color.white, 34f, 14f, true);
        var loc = label.GetComponent<LocalizedText>() ?? label.gameObject.AddComponent<LocalizedText>();
        var locSo = new SerializedObject(loc);
        locSo.FindProperty("key").stringValue = "ui.game.skip_cards";
        locSo.ApplyModifiedPropertiesWithoutUndo();
        label.text = GameText.Get("ui.game.skip_cards");
        label.margin = Vector4.zero;
        label.alignment = TextAlignmentOptions.Center;
        Inset(label.rectTransform, 30f, 30f, Size.y * .2f, 6f);

        // Rozet: sağ üst köşede altın kare, içinde kalan hak (koyu mürekkep Bungee). Köşeye taşar, butonun yazısına binmez.
        Transform existing = button.transform.Find("Count Badge");
        var badge = existing != null ? (RectTransform)existing : new GameObject("Count Badge", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        badge.gameObject.layer = button.gameObject.layer;
        badge.SetParent(button.transform, false);
        badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f);
        badge.pivot = new Vector2(.5f, .5f);
        badge.anchoredPosition = new Vector2(-12f, -6f);
        badge.sizeDelta = new Vector2(Badge, Badge);
        badge.localRotation = Quaternion.Euler(0f, 0f, -6f);
        var badgeImage = badge.GetComponent<Image>();
        badgeImage.sprite = Sprite("slot_gold");
        badgeImage.type = Image.Type.Sliced;
        badgeImage.pixelsPerUnitMultiplier = badgeImage.sprite.rect.height / Badge;
        badgeImage.raycastTarget = false;
        Transform countTransform = badge.Find("Count");
        var count = countTransform != null ? countTransform.GetComponent<TextMeshProUGUI>()
            : new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        count.gameObject.layer = button.gameObject.layer;
        count.rectTransform.SetParent(badge, false);
        Display(count, theme, false, Ink, 26f, 12f, false);
        count.alignment = TextAlignmentOptions.Center;
        count.margin = Vector4.zero;
        count.raycastTarget = false;
        count.text = "1";
        Inset(count.rectTransform, 6f, 6f, 9f, 5f);   // karenin alt 3B kenarı
        badge.SetAsLastSibling();

        so.FindProperty("skipCount").objectReferenceValue = count;
        so.FindProperty("remainingLabel").objectReferenceValue = RemainingPlate((RectTransform)cards.transform, theme);
        InfoBox((RectTransform)cards.transform, theme, out TextMeshProUGUI infoTitle, out TextMeshProUGUI infoBody);
        so.FindProperty("infoTitle").objectReferenceValue = infoTitle;
        so.FindProperty("infoBody").objectReferenceValue = infoBody;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(button);
        EditorUtility.SetDirty(cards);
        return null;
    }

    // Kart ekranının altındaki kalan seçim plakası: krem kit şeridi, ortada Bungee. Başta kapalı (oyun seçim sırasında açar).
    static TextMeshProUGUI RemainingPlate(RectTransform parent, ComicUITheme theme)
    {
        Transform existing = parent.Find("Remaining Choices");
        if (existing != null && existing.GetComponent<Image>() == null) { Object.DestroyImmediate(existing.gameObject); existing = null; }
        var plate = existing != null ? (RectTransform)existing
            : new GameObject("Remaining Choices", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement)).GetComponent<RectTransform>();
        plate.gameObject.layer = parent.gameObject.layer;
        plate.SetParent(parent, false);
        plate.GetComponent<LayoutElement>().ignoreLayout = true;
        plate.anchorMin = plate.anchorMax = new Vector2(.5f, 0f);
        plate.pivot = new Vector2(.5f, 0f);
        plate.anchoredPosition = new Vector2(0f, 20f);
        plate.sizeDelta = new Vector2(500f, 60f);
        var image = plate.GetComponent<Image>();
        image.sprite = Sprite("label_cream");
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = image.sprite.rect.height / 60f;
        image.color = Color.white;
        image.raycastTarget = false;
        Transform labelTransform = plate.Find("Label");
        var label = labelTransform != null ? labelTransform.GetComponent<TextMeshProUGUI>()
            : new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.gameObject.layer = parent.gameObject.layer;
        label.rectTransform.SetParent(plate, false);
        Display(label, theme, false, Ink, 26f, 13f, false);
        label.alignment = TextAlignmentOptions.Center;
        label.margin = Vector4.zero;
        label.text = GameText.Format("ui.card.choices_left", 3);
        Inset(label.rectTransform, 28f, 34f, 10f, 6f);   // şeridin sağ kesiği ve alt 3B kenarı
        plate.gameObject.SetActive(false);
        return label;
    }

    // Kartların üstündeki bilgi kutusu (24 Kasım 2026, test geri bildirimi: "kartın adı üstte yazmalı, mouse üstüne gelince kartın
    // özelliği yazmalı"). Yalnız yeni tile kartlarında görünür; yükseltme ve Temel güç kartlarında oyun kutuyu kapatır (kullanıcı,
    // 24 Kasım). Tooltip'lerle aynı dil (Docs/UI-Dili.md): krem panel + amber şerit, başlık Bungee koyu mürekkep, gövde
    // tooltip yazısı. Kart dizilimine girmez; kart sırasının ortasına bağlı (ekran oranı değişince kartlarla birlikte kalır), alt kenarı
    // kartların üst kenarının üstünde. Tıklamayı ve fareyi engellemez. Editörde boş ekran yazısıyla görünür; oyun yalnız yazıyı değiştirir.
    // Ölçüler (24 Kasım, 9 ekran boyutunda ölçüldü): fare üstündeki kart %3,5 büyür, alanının üstü ortadan ~321 birim yukarıda; kutunun
    // altı 334'te (≈ 13 birim boşluk). Üstü 458'de: 21:9'da (tuval ~935 birim boy) tepede ~10 birim pay kalır.
    public const float InfoWidth = 920f, InfoHeight = 124f, InfoLift = 334f, InfoHeader = 52f, InfoBodyTop = 51f, InfoBodyBottom = 23f;
    static readonly Color Amber = new Color32(247, 192, 93, 255);

    static void InfoBox(RectTransform parent, ComicUITheme theme, out TextMeshProUGUI title, out TextMeshProUGUI body)
    {
        var box = Child(parent, "Card Info", typeof(Image), typeof(LayoutElement));
        box.GetComponent<LayoutElement>().ignoreLayout = true;
        box.anchorMin = box.anchorMax = new Vector2(.5f, .5f);
        box.pivot = new Vector2(.5f, 0f);
        box.anchoredPosition = new Vector2(0f, InfoLift);
        box.sizeDelta = new Vector2(InfoWidth, InfoHeight);
        box.localRotation = Quaternion.identity;
        box.localScale = Vector3.one;
        Kit(box.GetComponent<Image>(), "panel_cream", Color.white, 1.6f);

        var header = Child(box, "Header", typeof(Image));
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(.5f, 1f);
        header.offsetMin = new Vector2(7f, -InfoHeader);
        header.offsetMax = new Vector2(-7f, -7f);
        Kit(header.GetComponent<Image>(), "label_cream", Amber, 2.2f);

        title = Child(header, "Title", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        Display(title, theme, false, Ink, 26f, 14f, false);
        title.alignment = TextAlignmentOptions.Center;
        title.margin = Vector4.zero;
        title.text = GameText.Get("ui.card.info.idle_title");
        Inset(title.rectTransform, 28f, 34f, 9f, 5f);   // şeridin sağ kesiği ve alt 3B kenarı

        body = Child(box, "Body", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        theme.StylePopupText(body);
        body.color = Ink;
        body.fontSize = 24f;
        body.enableAutoSizing = true;
        body.fontSizeMax = 24f;
        body.fontSizeMin = 16f;
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.alignment = TextAlignmentOptions.Center;
        body.margin = Vector4.zero;
        body.raycastTarget = false;
        body.text = GameText.Get("ui.card.info.idle");
        // Gövde, şeridin altı ile panelin iç çizgisi arasındaki krem alanda dikeyde ortalı (24 Kasım, kullanıcı: "yazı biraz alta
        // kalmış"; çekimden ölçüldü: krem alan kutunun üstünden 51–117 px, alttaki iç çizgi + 3B kenar ~23 px).
        Inset(body.rectTransform, 34f, 40f, InfoBodyBottom, InfoBodyTop);
    }

    // Adıyla bulunan ya da yeni kurulan çocuk (tekrar çalışınca çoğalmaz); eksik bileşen eklenir.
    static RectTransform Child(Transform parent, string name, params System.Type[] components)
    {
        Transform found = parent.Find(name);
        GameObject go = found != null ? found.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        foreach (System.Type type in components)
            if (go.GetComponent(type) == null) go.AddComponent(type);
        return (RectTransform)go.transform;
    }

    static void Kit(Image image, string sprite, Color color, float multiplier)
    {
        image.sprite = Sprite(sprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.color = color;
        image.raycastTarget = false;
    }

    public const string CardPrefab = "Assets/Prefabs/UI/Grid Mutation Card.prefab";

    // Kart prefab'ının bütün yazıları Bungee. Başarıda null. Tekrar çalışınca boylar yeniden küçülmesin diye eski boy işaretlenir
    // (yazı zaten Bungee ise boy olduğu gibi kalır).
    public static string RestyleCardPrefab()
    {
        var theme = AssetDatabase.LoadAssetAtPath<ComicUITheme>("Assets/Resources/ComicUITheme.asset");
        if (theme == null || theme.displayFont == null) return "ComicUITheme / Bungee yok";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CardPrefab) == null) return "kart prefab'ı yok";
        GameObject root = PrefabUtility.LoadPrefabContents(CardPrefab);
        try
        {
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                bool already = text.font == theme.displayFont;
                Color c = text.color;
                bool light = c.r * .299f + c.g * .587f + c.b * .114f > .5f;
                float max = already ? text.fontSizeMax : Mathf.Round((text.enableAutoSizing ? text.fontSizeMax : text.fontSize) * .88f);
                Display(text, theme, light, c, max, Mathf.Min(12f, max), text.textWrappingMode != TextWrappingModes.NoWrap);
            }
            PrefabUtility.SaveAsPrefabAsset(root, CardPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return null;
    }

    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Pack + name + ".png");

    static void Display(TMP_Text text, ComicUITheme theme, bool outlined, Color color, float max, float min, bool wrap)
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

    static void Inset(RectTransform rect, float left, float right, float bottom, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
}
