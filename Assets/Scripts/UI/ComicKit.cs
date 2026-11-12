using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Comic kağıt UI kiti (6 Kasım 2026): kullanıcının "Comic Paper UI Pack v2" paketinden hazırlanan sprite'lar
// (Tools/Art/ComicPack/comicpack_prepare.ps1 → Assets/Resources/ComicPack). Kurallar: Docs/UI-Dili.md, envanter: Docs/UI-Envanter.md.
// - Butonlar: renk görevine göre (yeşil satın al, altın yetenek, mavi ilerle, mor incele, kırmızı tehlike). Dört hâl aynı
//   silüetten türetildi; Unity'nin kendi SpriteSwap geçişi kullanılır (karede kod çalışmaz, malzeme yok).
// - Kartlar: krem panel + lacivert başlık şeridi (9-dilim).
// Sprite'lar bir kez yüklenir (önbellek); hiçbir şey karede ayrılmaz.
public static class ComicKit
{
    private const string Root = "ComicPack/";
    public const string Green = "green", Gold = "gold", Blue = "blue", Purple = "purple", Red = "red";
    private static readonly Dictionary<string, Sprite> cache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cache.Clear();

    public static Sprite Get(string name)
    {
        if (!cache.TryGetValue(name, out Sprite sprite))
        {
            sprite = Resources.Load<Sprite>(Root + name);
            cache[name] = sprite;
        }
        return sprite;
    }

    // Kitin sprite'ları projede mi (yoksa her yer eski görünümünde kalır).
    public static bool Ready => Get("button_blue_normal") != null && Get("panel_cream") != null;

    // ------------------------------------------------------------------ butonlar
    // Eski görünüm (yuvarlak toon sprite + palet malzemesi, ComicButtonVisual) kaldırılır; hover / basılı / pasif hâller
    // SpriteSwap ile. Yazı, ikon ve hover hareketi (ComicHoverMotion) olduğu gibi kalır.
    public static bool StyleButton(Button button, string colour)
    {
        if (button == null) return false;
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        Sprite normal = Get($"button_{colour}_normal");
        if (image == null || normal == null) return false;
        ComicButtonVisual visual = button.GetComponent<ComicButtonVisual>();
        if (visual != null) Object.Destroy(visual);
        image.material = null;
        image.sprite = normal;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.pixelsPerUnitMultiplier = Multiplier(normal, image.rectTransform, 2f);
        button.transition = Selectable.Transition.SpriteSwap;
        button.spriteState = new SpriteState
        {
            highlightedSprite = Get($"button_{colour}_hover"),
            pressedSprite = Get($"button_{colour}_pressed"),
            selectedSprite = normal,   // tıklanınca "seçili" kalıp parlamasın
            disabledSprite = Get($"button_{colour}_disabled"),
        };
        // Yazı Bungee, beyaz + kontur; Bungee geniş olduğundan sığmazsa küçülür (yalnız yazı değişince, karede değil).
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (!Display(label, true)) continue;
            label.color = Color.white;
            if (!label.enableAutoSizing) { label.fontSizeMax = label.fontSize; label.enableAutoSizing = true; }
            label.fontSizeMin = Mathf.Min(12f, label.fontSizeMax);
            // Bungee'nin satır yüksekliği büyük: küçük butonda (Temizle) "kes" kipi yazının tamamını siliyordu. Yazı her zaman çizilir.
            label.overflowMode = TextOverflowModes.Overflow;
        }
        return true;
    }

    // Hazırlık ekranının dört butonu (sahnedeki adlarıyla; "Placment" sahnedeki yazımdır). Bulunanların sayısını döner.
    public static int StyleRoundEnd(GameObject roundEndPanel)
    {
        if (roundEndPanel == null || !Ready) return 0;
        int styled = 0;
        foreach (Button button in roundEndPanel.GetComponentsInChildren<Button>(true))
        {
            string colour = button.name switch
            {
                "Next Round Button" => Blue,
                "Placment Shop Button" => Green,
                "Skill Shop Button" => Gold,
                "Preview Button" => Purple,
                _ => null,
            };
            if (colour != null && StyleButton(button, colour)) styled++;
        }
        return styled;
    }

    // ------------------------------------------------------------------ kartlar
    // Krem panel (kartın tamamı) + başlık şeridi. Eski düz plakaların (mürekkep gölgesi / kontur / kâğıt / başlık) yerine.
    // headerSprite: "header_navy" (bilgi kartı, beyaz konturlu başlık) ya da "label_cream" (renkle boyanan başlık).
    public static void Card(RectTransform card, float headerHeight, string headerSprite, Color headerTint)
    {
        Image body = Sliced(card, "Comic Panel", "panel_cream", 768f / Mathf.Max(1f, card.rect.width > 1f ? card.rect.width : card.sizeDelta.x));
        Place(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(-6f, -8f), new Vector2(8f, 6f));
        Image header = Sliced(card, "Comic Header", headerSprite, 2.2f);
        header.color = headerTint;
        Place(header.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(10f, -headerHeight - 2f), new Vector2(-6f, -6f));
        // Kartın en altında: önce panel, üstünde başlık; yazılar ve satırlar bunların üstünde kalır.
        body.rectTransform.SetAsFirstSibling();
        header.rectTransform.SetSiblingIndex(1);
    }

    // Kart başlığı lacivert şeritte: beyaz, mürekkep konturlu başlık yazısı.
    public static void StyleHeaderText(TMP_Text text, ComicUITheme theme)
    {
        if (text == null) return;
        if (!Display(text, true, theme))
        {
            if (theme != null && theme.headingFont != null) text.font = theme.headingFont;
            if (theme != null && theme.outlinedText != null) text.fontSharedMaterial = theme.outlinedText;
        }
        text.color = Color.white;
    }

    // ------------------------------------------------------------------ yazı (Bungee)
    // Kitin başlık / buton / sayı fontu (theme.displayFont). outlined: koyu zeminde beyaz + mürekkep kontur; değilse fontun kendi
    // malzemesi (krem zeminde koyu yazı). Bungee küçük harfi de büyük çizer: Türkçede "i" noktasız "I" çıkmasın diye yazı
    // TurkishCaps'ten geçer. Font yoksa hiçbir şey değişmez.
    public static bool Display(TMP_Text text, bool outlined, ComicUITheme theme = null)
    {
        if (text == null) return false;
        theme ??= FeelOverlay.Theme;
        if (theme == null || theme.displayFont == null) return false;
        text.font = theme.displayFont;
        text.fontSharedMaterial = outlined && theme.displayOutline != null ? theme.displayOutline : theme.displayFont.material;
        text.textPreprocessor = TurkishCaps.Instance;
        return true;
    }

    public static bool HasDisplayFont => FeelOverlay.Theme != null && FeelOverlay.Theme.displayFont != null;

    // Türkçe ve Azerice: "i" → "İ" (Bungee'nin "i"si noktasız büyük I gibi çizilir; "ı" zaten noktasız). Zengin yazı etiketlerine
    // (<size=72%>, <link=…>) dokunulmaz. Yazı değişince çalışır (karede değil); "i" yoksa ya da dil başkaysa yeni yazı ayrılmaz.
    public sealed class TurkishCaps : ITextPreprocessor
    {
        public static readonly TurkishCaps Instance = new();
        private readonly System.Text.StringBuilder builder = new(64);

        public static bool Active
        {
            get { string language = GameText.Language; return language == "tr" || language == "az"; }
        }

        public string PreprocessText(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('i') < 0 || !Active) return text;
            builder.Clear();
            bool tag = false;
            foreach (char c in text)
            {
                if (c == '<') tag = true;
                else if (c == '>') tag = false;
                builder.Append(!tag && c == 'i' ? 'İ' : c);
            }
            return builder.ToString();
        }
    }

    // TurkishCaps + sondaki satır sonunu atar: sahnede eski yerleşim için "Oyna<br>" yazan etiketler (ana menü) kitin butonunda
    // tek satır çizilir. Yazının kaynağı (çeviri tablosu, text alanı) değişmez.
    public sealed class SingleLineCaps : ITextPreprocessor
    {
        public static readonly SingleLineCaps Instance = new();
        private const string Break = "<br>";

        public string PreprocessText(string text)
        {
            if (!string.IsNullOrEmpty(text) && text.EndsWith(Break, System.StringComparison.Ordinal)) text = text.Substring(0, text.Length - Break.Length);
            return TurkishCaps.Instance.PreprocessText(text);
        }
    }

    // ------------------------------------------------------------------ yapı taşları
    public static Image Sliced(RectTransform parent, string name, string sprite, float multiplier)
    {
        var rect = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<RectTransform>();
        rect.gameObject.layer = parent.gameObject.layer;
        rect.SetParent(parent, false);
        var image = rect.GetComponent<Image>();
        image.sprite = Get(sprite);
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = multiplier;
        image.raycastTarget = false;
        return image;
    }

    // Dikeyde 1:1 ölçek (yükseklik sprite'ın kendi oranında), yatayda orta parça uzar. Boyutu henüz bilinmeyen için yedek değer.
    public static float Multiplier(Sprite sprite, RectTransform rect, float fallback)
    {
        float height = rect != null ? (rect.rect.height > 1f ? rect.rect.height : rect.sizeDelta.y) : 0f;
        return sprite != null && height > 1f ? Mathf.Max(.5f, sprite.rect.height / height) : fallback;
    }

    private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = new Vector2(.5f, .5f);
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }
}
