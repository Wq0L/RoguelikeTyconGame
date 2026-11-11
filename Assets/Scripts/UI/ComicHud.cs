using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD tek stil (H1–H4, 6 Kasım 2026). UIManager.Start kurar (kullanıcı: "elimizde olanları uygula"). Kurallar: Docs/UI-Dili.md.
// Parçalar kullanıcının ChatGPT ile ürettiği kart karakterli UI takımından (Docs/HUD-Toon/Kaynak/ui_kit_gpt.png), kesim:
// Tools/Art/HudKit/hudkit_slice.ps1 → Assets/Resources/HudKit/hud_*.png (9-dilim sınırları meta'da).
// - Round + süre üst ortada: mavi panel ("ROUND" + "3 / 50"), altına asılı lacivert plakada süre; sağa kayık (tarlanın en üst
//   koordinat rozetiyle çakışmaz).
// - Gold / Iron / Stone sağ üstte gri plakalar, ikonlu (ComicUITheme ikonları).
// - Level + XP sağ altta: lacivert "Lv." plakası + mavi dolum çubuğu (boş çerçeve + Filled dolgu; dolgu XpUI'nin Slider'ıyla).
// - Kota kartı sol üstte: sekmeli krem panel (9-dilim; yüksekliği içerikle değişir), yazıları aynı.
// Oyunun betikleri yazıları kendileri günceller (RoundUI / GoldUI / XpUI / QuotaHUD); burada yalnız yer, ebeveyn ve görünüş
// değişir. Bir kez kurulur; karede iş yok (XP dolgusu Slider'ın kendi olayından).
public static class ComicHud
{
    public const float Margin = 24f, DisplayScale = .82f;
    private const string Kit = "HudKit/hud_";

    // Kurulduğu sahnedeki RoundUI: sahne yeniden yüklenince (yeni run) nesne yok olur, HUD yeniden kurulur.
    private static RoundUI installedOn;
    public static bool Installed => installedOn != null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] private static void ResetStatics() => installedOn = null;

    public static void Install()
    {
        if (Installed) return;
        RoundUI round = Object.FindAnyObjectByType<RoundUI>(FindObjectsInactive.Include);
        XpUI xp = Object.FindAnyObjectByType<XpUI>(FindObjectsInactive.Include);
        GoldUI[] resources = Object.FindObjectsByType<GoldUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (round == null || xp == null || resources.Length == 0 || Load("panel_blue") == null) return;
        installedOn = round;
        ComicUITheme theme = FeelOverlay.Theme;
        Round(round, theme);
        Resources(resources, theme);
        Level(xp, theme);
    }

    private static Sprite Load(string name) => UnityEngine.Resources.Load<Sprite>(Kit + name);

    // ---------------------------------------------------------------- round + süre (üst orta), kota kartı (sol üst)
    private static void Round(RoundUI round, ComicUITheme theme)
    {
        var root = (RectTransform)round.transform;
        Stretch(root);
        QuotaHUD quota = root.GetComponentInChildren<QuotaHUD>(true) ?? QuotaHUD.Attach(root);
        if (quota != null) Quota((RectTransform)quota.transform);

        RectTransform card = Piece(root, "Round Card", "panel_blue", new Vector2(.5f, 1f), new Vector2(0f, -12f), new Vector2(290f, 128f));
        TMP_Text header = Text(card, "Round Header", theme, 22f, new Vector2(.08f, .62f), new Vector2(.9f, .9f));
        LocalizedText.Bind(header, "ui.hud.round_label");
        TMP_Text rounds = Find(root, "Rounds Text");
        if (rounds != null)
        {
            Into(rounds, card, theme, 42f, new Vector2(.06f, .16f), new Vector2(.92f, .66f));
            round.UseCompactRound();
        }
        TMP_Text timer = Find(root, "Round Timer Text");
        if (timer != null)
        {
            RectTransform band = Piece(card, "Timer Plate", "plate_navy", new Vector2(.5f, 0f), new Vector2(104f, -34f), new Vector2(176f, 66f));
            Into(timer, band, theme, 22f, new Vector2(.12f, .16f), new Vector2(.9f, .86f));
            round.SetTimerRestColor(Color.white);
        }
    }

    // Kota kartının yumuşak krem plakaları sekmeli krem panelle değiştirilir (yazılar ve çubuk aynı).
    private static void Quota(RectTransform quota)
    {
        quota.anchorMin = quota.anchorMax = quota.pivot = new Vector2(0f, 1f);
        quota.anchoredPosition = new Vector2(Margin, -Margin - 12f);
        foreach (string name in new[] { "Ink shadow", "Ink outline", "Cream paper" })
        {
            Transform old = quota.Find(name);
            if (old != null) old.gameObject.SetActive(false);
        }
        // Sekme kartın üstünden taşar; panel kartın biraz dışına uzanır (kesik kenarlar yazıya değmesin).
        Image panel = Sliced(quota, "Card", "card_tab_cream", 1.7f);
        var rect = panel.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(-10f, -10f); rect.offsetMax = new Vector2(12f, 30f);
        rect.SetAsFirstSibling();
    }

    // ---------------------------------------------------------------- kaynaklar (sağ üst)
    private static void Resources(GoldUI[] resources, ComicUITheme theme)
    {
        // Soldan sağa Gold, Iron, Stone (eski HUD sırası).
        System.Array.Sort(resources, (a, b) => Rank(a.ResourceType).CompareTo(Rank(b.ResourceType)));
        var container = (RectTransform)resources[0].transform.parent;
        container.anchorMin = container.anchorMax = container.pivot = new Vector2(1f, 1f);
        // Genişlik 6 haneyi taşır ("12.345"); daha uzun sayıda yazı küçülür (yalnız sayı değişince, karede iş yok).
        const float width = 180f, height = 62f, gap = 8f;
        container.sizeDelta = new Vector2(resources.Length * width + (resources.Length - 1) * gap, height);
        container.anchoredPosition = new Vector2(-Margin + 8f, -14f);
        for (int i = 0; i < resources.Length; i++)
        {
            GoldUI resource = resources[i];
            var rect = (RectTransform)resource.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(i * (width + gap), 0f);
            rect.sizeDelta = new Vector2(width, height);
            var box = resource.GetComponent<Image>();
            if (box != null) box.enabled = false;
            RectTransform card = Piece(rect, "Card", "plate_grey", new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, height));
            card.SetAsFirstSibling();
            Sprite icon = theme != null ? theme.ResourceIcon(resource.ResourceType) : null;
            if (icon != null)
            {
                var iconRect = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
                iconRect.gameObject.layer = rect.gameObject.layer;
                iconRect.SetParent(card, false);
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0f, .5f);
                iconRect.anchoredPosition = new Vector2(22f, 2f);
                iconRect.sizeDelta = new Vector2(38f, 38f);
                var image = iconRect.GetComponent<Image>();
                image.sprite = icon; image.preserveAspect = true; image.raycastTarget = false;
            }
            TMP_Text amount = resource.AmountText;
            if (amount != null)
            {
                Into(amount, card, theme, 30f, new Vector2(.34f, .14f), new Vector2(.88f, .86f));
                amount.alignment = TextAlignmentOptions.MidlineRight;
                amount.enableAutoSizing = true; amount.fontSizeMax = amount.fontSize; amount.fontSizeMin = 16f;
            }
            resource.UseIconLayout();
        }
    }

    private static int Rank(ResourceType type) => type == ResourceType.Gold ? 0 : type == ResourceType.Iron ? 1 : 2;

    // ---------------------------------------------------------------- level + XP (sağ alt)
    private static void Level(XpUI xp, ComicUITheme theme)
    {
        var root = (RectTransform)xp.transform;
        Stretch(root);
        var group = new GameObject("Level Card", typeof(RectTransform)).GetComponent<RectTransform>();
        group.gameObject.layer = root.gameObject.layer;
        group.SetParent(root, false);
        group.anchorMin = group.anchorMax = group.pivot = new Vector2(1f, 0f);
        group.anchoredPosition = new Vector2(-Margin, 22f);
        group.sizeDelta = new Vector2(380f, 70f);
        // Çubuk: boş çerçeve (Simple, doğal oran) + aynı dikdörtgende yatay Filled dolgu.
        RectTransform bar = Piece(group, "XP Bar", "bar_blue_frame", new Vector2(1f, .5f), new Vector2(0f, 0f), new Vector2(286f, 59f));
        Slider slider = root.GetComponentInChildren<Slider>(true);
        if (slider != null)
        {
            var sliderRect = (RectTransform)slider.transform;
            sliderRect.SetParent(bar, false);
            sliderRect.anchorMin = Vector2.zero; sliderRect.anchorMax = Vector2.one; sliderRect.offsetMin = sliderRect.offsetMax = Vector2.zero;
            sliderRect.localRotation = Quaternion.identity;
            Image background = slider.transform.Find("Background")?.GetComponent<Image>();
            if (background != null) background.enabled = false;
            if (slider.fillRect != null)
            {
                var fillImage = slider.fillRect.GetComponent<Image>();
                if (fillImage != null) fillImage.enabled = false;   // Slider değeri tutar, dolguyu aşağıdaki Filled görsel çizer
            }
            var fill = new GameObject("XP Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            fill.gameObject.layer = bar.gameObject.layer;
            fill.rectTransform.SetParent(bar, false);
            fill.rectTransform.anchorMin = Vector2.zero; fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            fill.sprite = Load("bar_blue_fill");
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0;
            fill.raycastTarget = false;
            fill.fillAmount = slider.normalizedValue;
            slider.onValueChanged.AddListener(_ => fill.fillAmount = slider.normalizedValue);
        }
        RectTransform badge = Piece(group, "Level Plate", "plate_navy", new Vector2(0f, .5f), new Vector2(0f, 2f), new Vector2(118f, 62f));
        TMP_Text level = Find(root, "Level Text");
        if (level != null) Into(level, badge, theme, 28f, new Vector2(.14f, .14f), new Vector2(.86f, .86f));
    }

    // ---------------------------------------------------------------- yapı taşları
    // Takım parçası: doğal oranına yakın boyutta Simple görsel (kesik kenarlar ve tram bozulmaz).
    private static RectTransform Piece(RectTransform parent, string name, string sprite, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = rect.GetComponent<Image>();
        image.sprite = Load(sprite);
        image.raycastTarget = false;
        return rect;
    }

    // Boyu değişen yerler için 9-dilim; multiplier: kenarların ölçeği (büyük değer = ince kenar).
    private static Image Sliced(RectTransform parent, string name, string sprite, float multiplier)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        var image = rect.GetComponent<Image>();
        image.sprite = Load(sprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.raycastTarget = false;
        return image;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static TMP_Text Find(Transform root, string name)
    {
        foreach (TMP_Text t in root.GetComponentsInChildren<TMP_Text>(true)) if (t.name == name) return t;
        return null;
    }

    // Kartın sayıları gibi: başlık yazı tipi, beyaz, mürekkep konturlu malzeme.
    // Bungee (theme.displayFont) varsa onunla: harfleri geniş ve büyük olduğundan boy %82'ye iner (aynı görsel ağırlık).
    private static void Style(TMP_Text text, ComicUITheme theme, float size)
    {
        if (ComicKit.Display(text, true, theme)) size *= DisplayScale;
        else
        {
            if (theme != null && theme.headingFont != null) text.font = theme.headingFont;
            text.fontSharedMaterial = theme != null && theme.outlinedText != null ? theme.outlinedText : text.font.material;
        }
        // Dil değişince (uzun çeviri) yazı plakadan taşmaz: sığmazsa küçülür. Yalnız yazı değişince çalışır (süre: saniyede bir).
        text.fontSize = size;
        text.fontSizeMax = size;
        text.fontSizeMin = Mathf.Max(12f, size * .55f);
        text.enableAutoSizing = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
    }

    private static TMP_Text Text(RectTransform parent, string name, ComicUITheme theme, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        var text = rect.GetComponent<TextMeshProUGUI>();
        Place(rect, anchorMin, anchorMax);
        Style(text, theme, size);
        return text;
    }

    // Var olan yazı parçanın içine taşınır (betiği güncellemeye devam eder).
    private static void Into(TMP_Text text, RectTransform parent, ComicUITheme theme, float size, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = text.rectTransform;
        rect.SetParent(parent, false);
        rect.localRotation = Quaternion.identity; rect.localScale = Vector3.one;
        Place(rect, anchorMin, anchorMax);
        Style(text, theme, size);
        rect.SetAsLastSibling();
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
