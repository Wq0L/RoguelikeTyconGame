using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Eri'nin konuşma kutusu (25 Ekim 2026, plan: Docs/DiyalogPlani). İki tür, aynı çizgi roman dili (UI art direction: kalın mürekkep,
// kesik köşeli kâğıt, sert gölge, kısa köşe çizgileri, seyrek köşe tramı):
// - Büyük, "sahne" (round dışı ekranlar; 26 Ekim kullanıcı kararı): alt ortada büyük kutu. Açıkken arkası vinyetle kararır (kenarlar
//   koyu, kutunun çevresi açık), üst köşelerde ince çizgi roman tramı belirir; arkadaki panel tıklanmaz. Ekranın herhangi bir yerine
//   tık, Boşluk ya da Enter ilerletir; "Atla" bütün konuşmayı geçer. Kutu alttan taşarak yükselir, isim plakası gecikmeli açılır.
//   İlk basış hareketi hemen yerine oturtur ve yazıyı tamamlar (Boşluk'a art arda basan oyuncu her satırı iki basışta geçer).
//   Konuşma bitince perde tık almayı hemen bırakır, kutu kısa bir çıkışla kaybolur, arkadaki panel kullanılır.
//   (Ekran kenarı kâğıt kolajı, defter sayfaları ve bantlı not denendi; kullanıcı kâğıtlardan tamamen vazgeçti, 26 Ekim.)
// - Küçük, "fısıltı" (round içi): soldan kayarak girer, yazı bittikten SmallCloseSeconds sonra ya da Boşluk ile soldan çıkar;
//   tıklamayla ilerlemez. Canvas'ında arayüz ışını yoktur: imleç üstündeyken "arayüzde" sayılmaz, kapris vuruşları düşmez.
// Eri (28 Ekim, kullanıcının çizdiği düzen): kutunun içinde portre çerçevesi yok; Eri kutunun sol ucunda, kutunun ÖNÜNDE, yan dönük
// (kutuya bakan uykulu kare, EriFrame), kutudan biraz yüksek: kubbesi kutunun üstünden taşar, ayakları kutunun alt kenarı hizasında.
// Kutu Eri'nin arkasına uzanır (sol ucu kubbenin arkasında kalır). Eri'nin sağ yanında kutunun üstünde camgöbeği bir kontur fırça
// çizgisi ve sol alt köşede açık mavi tram vardır (kullanıcının "mavi polish"i: Eri'nin cam kubbesinin rengi). Eri SABİTTİR: kendi animasyonu yok (konuşma kıpırtısı yok, satıra göre kare değişmez; kutuyla birlikte açılır / kapanır).
// Kare ve mavi cam kubbe çizimi olduğu gibi kullanılır (rengine / ışığına dokunulmaz). Büyük ve küçük kutu aynı düzen, küçük ufak.
// Vinyet ve tram sabittir (yalnız açılışta bir kez belirir; nabız yok). Açıkken Boşluk / Enter'in seçili başka bir arayüz düğmesine
// de basmaması için olay sisteminin gezinme olayları kapatılır.
// Yazı harf harf dizilir (TMP maxVisibleCharacters). Bütün hareketler Step'te, kutunun kendi saatiyle (DialogueDirector.ClockDelta:
// menü ekranlarında akar, duraklatma ya da pencere arka plandayken durur); karede bellek ayırma yok. Kapalı kutuda Update çalışmaz.
// Çeviri (Faz 2): isim plakası ve "Atla" de metin tablosundan gelir, plaka ve düğme yazıya göre genişler; yazı hızı dilin
// çarpanıyla (DialogueText.SpeedScale) çarpılır; duraklama işaretleri Çince/Japonca, Arapça ve Hintçe noktalamayı da kapsar.
// Sağdan sola yazılar (Arapça, İbranice) ve Latin dışı font yedekleri henüz yok: çeviri işiyle birlikte kararlaştırılacak.
public sealed class DialogueBox : MonoBehaviour
{
    public const float CharsPerSecond = 45f, PunctuationPause = .16f, SmallCloseSeconds = 6f;
    public const string PauseMarks = ",.!?…;:、。！？，；：،؛؟।";
    public const string NameKey = "ui.name.eri", SkipKey = "ui.skip";
    // Büyük kutu alt ortada (1920×1080 referans). DimAlpha: perdenin en koyu yeri (kenarlar); merkez DialogueArt.VignetteFloor kadarı.
    public const float BigWidth = 960f, BigHeight = 250f, BigBottom = 40f, DimAlpha = .9f, DimFadeSeconds = .2f;
    public const float EnterSeconds = .26f, ExitSeconds = .18f;
    // Eri: boy kutu yüksekliğinin 1,4 katı (kullanıcının görseli). Genişliğinin EriTuck kadarı kutunun üstündedir (kutunun sol ucu
    // kubbenin arkasında kalır, köşesi görünmez). Kontur (webtoon tarzı arka kenar): Eri'nin silüeti (EriSilhouette, kare 0'dan türetildi)
    // camgöbeğine boyanıp RimGap + RimWidth kadar sağa kaydırılır, üstüne RimGap kadar kaydırılmış kâğıt renginde silüet gelir: dış
    // hattan RimGap uzakta, RimWidth kalınlığında bir şerit kalır (kullanıcının görselindeki ölçü). Yalnız kutunun kâğıdı üstünde ve
    // taban halkasının üstünde görünür; üstü ve altı yumuşakça solar (RectMask2D softness). Silüet olduğu için dış hattı izler.
    public const int EriFrame = 0;
    private const float BigEri = 350f, SmallEri = 150f, EriTuck = .45f;
    // Kubbenin en sağ noktası (kare 0'dan ölçüldü, kare genişliğine oranla) ve konturun görüneceği en alt yükseklik (taban halkası).
    private const float DomeEdgeMax = .787f, RimFloor = .27f;
    // Görünen kutu genişliği (kontur çizgisinden sağ uca kadar ölçülmez; Eri'nin arkasındaki kısım ayrıca eklenir).
    private const float SmallWidth = 600f, SmallHeight = 108f, SmallBottom = 40f, SkipWidth = 116f, PanelTilt = -.6f;
    private const float BigRise = 70f, PlateDelay = .08f, PlatePop = .18f;
    private static readonly Color Polish = new Color32(64, 172, 196, 255), PolishDots = new Color32(126, 198, 222, 150);
    // Eri'nin genişliği karenin oranından (ayaklar açık: biraz geniş).
    // Eri hafif basık çizilir (28 Ekim kullanıcı: "cam biraz uzun"; ×1,10 / ×0,92 "çok şişko" bulundu): yatayda ×1,06, dikeyde ×0,95.
    private const float EriStretchX = 1.06f, EriSquashY = .95f;
    private static float EriWidth(float height) { Vector2 s = MascotFrames.Size(EriFrame); return height * s.x / s.y * EriStretchX; }
    private float EriSize => Big ? BigEri : SmallEri;
    private float EriHeight => EriSize * EriSquashY;
    private float Tuck => EriWidth(EriSize) * EriTuck;
    // Kontur ölçüleri kullanıcının görselinden (aynı ölçeğe getirilip ölçüldü): dış hatla arası, kalınlığı, uçlarındaki solma.
    private float RimGap => Big ? 4f : 2f;   // 8 px "boşluk fazla" bulundu
    private float RimWidth => Big ? 10f : 4.5f;
    private int RimFade => Big ? 24 : 10;
    // Konturun en sağ noktası (yazı ve plaka buna göre yerleşir).
    private float LineX => EriWidth(EriSize) * (EriTuck - (1f - DomeEdgeMax)) + RimGap + RimWidth;
    // Büyük: Eri + kutu birlikte ekranın ortasında. Küçük: grup sol kenardan 24 px içeride; soldan kayarak girer.
    private static float BigCenterX => EriWidth(BigEri) * (1f - EriTuck) * .5f;
    private static float SmallLeft => 24f + EriWidth(SmallEri) * (1f - EriTuck);
    private static float SmallSlide => SmallLeft + SmallWidth + EriWidth(SmallEri) * EriTuck + 40f;
    private static readonly Color DotInk = new Color32(6, 3, 8, 255);
    private static readonly Color Ink = new Color32(36, 26, 43, 255), Paper = new Color32(255, 244, 214, 255),
        ShadowColor = new Color32(26, 15, 31, 235), Amber = new Color32(247, 192, 93, 255);
    // Köşe kesikleri (sol üst, sağ üst, sağ alt, sol alt): her parça bilerek farklı.
    private static readonly Vector4 BigCuts = new(26f, 10f, 30f, 8f), SmallCuts = new(18f, 6f, 20f, 4f),
        NameCuts = new(0f, 12f, 0f, 7f), SkipCuts = new(7f, 0f, 9f, 0f);

    public bool Big { get; private set; }
    public bool IsOpen { get; private set; }
    public bool Typing => IsOpen && visible < total;
    public int LineIndex => lineIndex;
    public float IdleSeconds => idle;
    public string ShownText => text != null ? text.text : "";
    public int VisibleCharacters => visible;
    public RectTransform Panel => panel;
    // Büyük kutunun tam ekran tıklama perdesi (küçük kutuda yok).
    public RawImage Blocker => blocker;
    // Kutu tık alıyor mu (çıkış hareketinde almaz).
    public bool BlocksInput => isActiveAndEnabled && group.blocksRaycasts;
    // Test / araç: giriş ve çıkış hareketlerinin ilerlemesi (0–1).
    public float EnterProgress => Mathf.Clamp01(enterTime / EnterSeconds);
    public bool Exiting => exiting;
    public event Action Finished;

    private DialogueEntry entry;
    private int lineIndex, visible, total;
    private float reveal, pauseLeft, idle, enterTime, exitTime;
    private bool exiting, posed;
    private CanvasGroup cornerDots;
    private Canvas canvas;
    private CanvasGroup group;
    private RectTransform panel, portraitRect, indicator, namePlate, skipRect;
    private TMP_Text text, nameLabel, skipLabel;
    private string labelLanguage;
    private Image portrait;
    private RawImage blocker;
    private EventSystem navigationOwner;
    private bool navigationWas;

    public static DialogueBox Create(Transform parent, bool big, int sortingOrder)
    {
        RectTransform root = FeelOverlay.CreateCanvas(big ? "Dialogue Box (big)" : "Dialogue Box (small)", sortingOrder);
        root.SetParent(parent, false);
        var box = root.gameObject.AddComponent<DialogueBox>();
        box.Big = big;
        box.canvas = root.GetComponent<Canvas>();
        box.group = root.gameObject.AddComponent<CanvasGroup>();
        if (big) root.gameObject.AddComponent<GraphicRaycaster>();   // küçük kutuda bilerek yok (kapris vuruşları)
        box.Build(root);
        DialogueText.LanguageChanged += box.InvalidateLabels;
        root.gameObject.SetActive(false);
        box.enabled = false;
        return box;
    }

    private void Build(RectTransform root)
    {
        ComicUITheme theme = FeelOverlay.Theme;
        if (Big) BuildCurtain(root);

        Vector4 cuts = Big ? BigCuts : SmallCuts;
        panel = Rect("Panel", root, Big ? new Vector2(BigWidth + Tuck, BigHeight) : new Vector2(SmallWidth + Tuck, SmallHeight));
        panel.anchorMin = panel.anchorMax = panel.pivot = Big ? new Vector2(.5f, 0f) : Vector2.zero;
        panel.anchoredPosition = RestPosition;
        panel.localRotation = Quaternion.Euler(0f, 0f, PanelTilt);
        Plate("Shadow", panel, ShadowColor, new Vector2(9f, -9f), Vector2.zero, cuts);
        Plate("Ink", panel, Ink, Vector2.zero, Vector2.zero, cuts);
        RectTransform sheet = Plate("Paper", panel, Paper, Vector2.zero, new Vector2(6f, 6f), Inner(cuts, 3f)).rectTransform;
        // Seyrek köşe tramı (sağ alt) ve kısa mürekkep köşe çizgileri: baskı izi, okunurluğu bozmayacak kadar hafif.
        Halftone("Halftone", sheet, Big ? 190f : 96f, new Vector2(1f, 0f), new Rect(1f, 0f, -1f, 1f), new Color(Ink.r, Ink.g, Ink.b, .09f));
        if (Big) Stroke(sheet, new Vector2(1f, 1f), new Vector2(-170f, -10f), new Vector2(44f, 11f), 180f);
        else Stroke(sheet, new Vector2(1f, 1f), new Vector2(-14f, -7f), new Vector2(26f, 8f), 180f);

        // Mavi polish: Eri'nin ayağının yanında açık mavi tram (sol alt) ve dış hattında camgöbeği kontur.
        float dots = Big ? 230f : 96f;
        HalftoneImage("Polish Dots", panel, dots, Vector2.zero, new Rect(0f, 0f, 1f, 1f), PolishDots, DialogueArt.HalftoneFine)
            .anchoredPosition = new Vector2(LineX - dots * .3f, Big ? 7f : 5f);
        // Kontur: kâğıdın içinde, taban halkasının üstünde kırpılır, uçları solar. Sağa kayık camgöbeği silüet, üstünde biraz daha az
        // kayık kâğıt renginde silüet (aradaki krem boşluğu da yapar, camın yarı saydam yerlerinden mavi görünmez); Eri en öndedir.
        float rimFloor = EriHeight * RimFloor;
        RectTransform rim = Rect("Rim", panel, Vector2.zero);
        rim.anchorMin = Vector2.zero;
        rim.anchorMax = Vector2.one;
        rim.offsetMin = new Vector2(0f, rimFloor);
        rim.offsetMax = new Vector2(-6f, Big ? -8f : -4f);   // kutunun üstünden başlar, solarak belirir
        rim.gameObject.AddComponent<RectMask2D>().softness = new Vector2Int(0, RimFade);
        Silhouette(EriRect("Rim Color", rim, new Vector2(RimGap + RimWidth, -rimFloor)), Polish);
        Silhouette(EriRect("Rim Knockout", rim, new Vector2(RimGap, -rimFloor)), Paper);
        RimTaper(rim, rimFloor);

        // Eri: kutunun sol ucunda, önünde; sabit kare; ayakları kutunun alt kenarı hizasında (sprite'ların ortak ayak hizası).
        portraitRect = EriRect("Eri", panel, Vector2.zero);
        portrait = portraitRect.gameObject.AddComponent<Image>();
        portrait.preserveAspect = false;   // dikdörtgen zaten karenin oranında, üstüne hafif basıklık
        portrait.raycastTarget = false;
        portrait.sprite = MascotFrames.Get(EriFrame);
        portrait.enabled = portrait.sprite != null;

        // İsim plakası.
        namePlate = Rect("Name", panel, Big ? new Vector2(124f, 50f) : new Vector2(70f, 30f));
        namePlate.anchorMin = namePlate.anchorMax = new Vector2(0f, 1f);
        namePlate.pivot = new Vector2(.5f, .5f);
        namePlate.localRotation = Quaternion.Euler(0f, 0f, -4f);
        Plate("Ink", namePlate, Ink, Vector2.zero, Vector2.zero, NameCuts);
        Plate("Plate", namePlate, Amber, Vector2.zero, new Vector2(4f, 4f), Inner(NameCuts, 2f));
        nameLabel = Label("Label", namePlate, theme != null ? theme.headingFont : null, Big ? 34f : 20f);
        nameLabel.alignment = TextAlignmentOptions.Center;
        nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
        PlaceNamePlate();

        // Metin.
        float left = LineX + (Big ? 30f : 16f);
        RectTransform body = Rect("Text", panel, Vector2.zero);
        body.anchorMin = Vector2.zero;
        body.anchorMax = Vector2.one;
        body.offsetMin = new Vector2(left, Big ? 34f : 12f);
        body.offsetMax = new Vector2(Big ? -40f : -18f, Big ? -50f : -16f);
        text = body.gameObject.AddComponent<TextMeshProUGUI>();
        if (theme != null && theme.bodyFont != null) text.font = theme.bodyFont;
        text.color = Ink;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.alignment = Big ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.MidlineLeft;
        text.enableAutoSizing = true;
        text.fontSizeMax = Big ? 40f : 27f;
        text.fontSizeMin = Big ? 26f : 19f;

        if (!Big) return;
        // "Devam" işareti: yazı bitince köşede zıplayan küçük baklava.
        indicator = Rect("Continue", panel, new Vector2(20f, 20f));
        indicator.anchorMin = indicator.anchorMax = indicator.pivot = new Vector2(1f, 0f);
        indicator.anchoredPosition = new Vector2(-30f, 22f);
        indicator.localRotation = Quaternion.Euler(0f, 0f, 45f);
        var mark = indicator.gameObject.AddComponent<Image>();
        mark.color = Ink;
        mark.raycastTarget = false;
        indicator.gameObject.SetActive(false);

        // "Atla": bütün konuşmayı geçer.
        skipRect = Rect("Skip", panel, new Vector2(SkipWidth, 42f));
        skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = new Vector2(1f, 1f);
        skipRect.anchoredPosition = new Vector2(-14f, 20f);
        skipRect.localRotation = Quaternion.Euler(0f, 0f, 3f);
        DialoguePlateGraphic skipInk = Plate("Ink", skipRect, Ink, Vector2.zero, Vector2.zero, SkipCuts);
        skipInk.raycastTarget = true;
        Plate("Paper", skipRect, Paper, Vector2.zero, new Vector2(3f, 3f), Inner(SkipCuts, 1.5f));
        skipLabel = Label("Label", skipRect, theme != null ? theme.headingFont : null, 26f);
        skipLabel.alignment = TextAlignmentOptions.Center;
        skipLabel.textWrappingMode = TextWrappingModes.NoWrap;
        var skipButton = skipRect.gameObject.AddComponent<Button>();
        skipButton.transition = Selectable.Transition.None;
        skipButton.navigation = new Navigation { mode = Navigation.Mode.None };
        skipButton.targetGraphic = skipInk;
        skipButton.onClick.AddListener(Skip);
    }

    // Tam ekran perde: vinyet (kenarlar koyu, kutunun çevresi açık), üst köşelerde tram deseni. Arkadaki paneli kapatır, her tık
    // konuşmayı ilerletir.
    private void BuildCurtain(RectTransform root)
    {
        RectTransform rect = Rect("Blocker", root, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        blocker = rect.gameObject.AddComponent<RawImage>();
        blocker.texture = DialogueArt.Vignette;
        blocker.color = new Color(ShadowColor.r, ShadowColor.g, ShadowColor.b, 0f);
        blocker.raycastTarget = true;
        // Saydam açılışta da tık yakalasın: saydam ağ atlanırsa (yeni CanvasRenderer'da varsayılan) perdenin çizim sırası olmaz ve
        // karartma gelene kadar tık arkadaki panele geçer.
        blocker.canvasRenderer.cullTransparentMesh = false;
        var advance = rect.gameObject.AddComponent<Button>();
        advance.transition = Selectable.Transition.None;
        advance.navigation = new Navigation { mode = Navigation.Mode.None };
        advance.targetGraphic = blocker;
        advance.onClick.AddListener(Advance);

        // Üst köşe tramı (kullanıcının seçtiği sade hâl): ince, köşeden içeri seyrelen noktalar. Renk: perdeden koyu, neredeyse siyah
        // mürekkep (26 Ekim kullanıcı: sarı yerine siyah); sarı gibi dikkat çekmez, köşeleri koyulaştıran bir baskı dokusu gibi durur.
        RectTransform dots = Rect("Corner Dots", rect, Vector2.zero);
        dots.anchorMin = Vector2.zero;
        dots.anchorMax = Vector2.one;
        dots.offsetMin = dots.offsetMax = Vector2.zero;
        cornerDots = dots.gameObject.AddComponent<CanvasGroup>();
        cornerDots.blocksRaycasts = false;
        cornerDots.interactable = false;
        Color dotTone = new(DotInk.r, DotInk.g, DotInk.b, .7f);
        HalftoneImage("Dots TL", dots, 380f, new Vector2(0f, 1f), new Rect(0f, 1f, 1f, -1f), dotTone, DialogueArt.HalftoneFine);
        HalftoneImage("Dots TR", dots, 380f, new Vector2(1f, 1f), new Rect(1f, 1f, -1f, -1f), dotTone, DialogueArt.HalftoneFine);
    }

    // Eri'nin dikdörtgeni: kutunun sol ucunda, sağ kenarı Tuck'ta, ayakları alt kenarda; kutu hafif eğik, Eri dik durur.
    // parent kutunun sol altından başlamıyorsa shift o farkı da taşır.
    private RectTransform EriRect(string name, RectTransform parent, Vector2 shift)
    {
        RectTransform rect = Rect(name, parent, new Vector2(EriWidth(EriSize), EriHeight));
        rect.anchorMin = rect.anchorMax = Vector2.zero;
        rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(Tuck, 0f) + shift;
        rect.localRotation = Quaternion.Euler(0f, 0f, -PanelTilt);
        return rect;
    }

    // Konturun üst ucu sivrilir (kullanıcının görseli): kâğıt renginde bir kama şeridin dış yanını yukarı doğru giderek örter, uçta
    // yalnız iç kenar kalır. Kubbenin kenarı kutunun üst kısmında neredeyse dik (DomeEdgeMax); Eri'nin dik durması için verilen hafif
    // dönüş (kutunun eğiminin tersi) yüksekliğe göre x'i kaydırır, kama da aynı kaymayla çizilir.
    private void RimTaper(RectTransform rim, float rimFloor)
    {
        float width = EriWidth(EriSize), edge = Tuck - width * (1f - DomeEdgeMax);
        float inner = edge + RimGap, outer = inner + RimWidth, pad = Big ? 4f : 2f, taper = Big ? 46f : 18f;
        float top = (Big ? BigHeight : SmallHeight) - (Big ? 8f : 4f), lean = Mathf.Sin(-PanelTilt * Mathf.Deg2Rad);
        Vector2 At(float x, float y) => new(x - y * lean, y - rimFloor);
        RectTransform rect = Rect("Rim Taper", rim, new Vector2(outer + pad, top + pad - rimFloor));
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        var cover = rect.gameObject.AddComponent<DialoguePlateGraphic>();
        cover.SetPolygon(new[]
        {
            At(outer, top - taper), At(outer + pad, top - taper), At(outer + pad, top + pad), At(inner - 1f, top + pad),
        });
        cover.color = Paper;
        cover.raycastTarget = false;
    }

    private static void Silhouette(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = DialogueArt.EriSilhouette;
        image.enabled = image.sprite != null;
        image.preserveAspect = false;
        image.color = color;
        image.raycastTarget = false;
    }

    private Vector2 RestPosition => Big ? new Vector2(BigCenterX, BigBottom) : new Vector2(SmallLeft, SmallBottom);

    private void PlaceNamePlate()
    {
        Vector2 size = namePlate.sizeDelta;
        // Sol üst köşeye taşan plaka; pivot ortada olduğu için "pop" ölçeği kendi merkezinden açılır.
        namePlate.anchoredPosition = new Vector2(LineX - (Big ? 4f : 2f) + size.x * .5f, (Big ? 28f : 18f) - size.y * .5f);
    }

    // ---- açma / ilerletme / kapatma ----
    public void Show(DialogueEntry shown)
    {
        entry = shown;
        lineIndex = 0;
        IsOpen = true;
        exiting = posed = false;
        enterTime = exitTime = 0f;
        gameObject.SetActive(true);
        enabled = true;
        group.alpha = 1f;
        group.blocksRaycasts = true;
        SuspendNavigation();
        RefreshLabels();
        BeginLine();
        Animate();
    }

    // Giriş hareketini hemen bitirir (Boşluk / tık ile "otur").
    public void Settle()
    {
        if (!IsOpen || posed) return;
        enterTime = Mathf.Max(enterTime, EnterSeconds);
        Animate();
    }

    // Plaka ve "Atla" etiketi: dil değiştiyse yeniden yazılır, kutu yazıya göre genişler (yalnız açılışta, dil değişince).
    private void RefreshLabels()
    {
        if (labelLanguage == DialogueText.Language) return;
        labelLanguage = DialogueText.Language;
        nameLabel.text = DialogueText.Has(NameKey) ? DialogueText.Get(NameKey) : "Eri";
        namePlate.sizeDelta = new Vector2(Mathf.Max(Big ? 124f : 70f, nameLabel.preferredWidth + (Big ? 36f : 22f)), namePlate.sizeDelta.y);
        PlaceNamePlate();
        if (skipLabel == null) return;
        skipLabel.text = DialogueText.Has(SkipKey) ? DialogueText.Get(SkipKey) : "Atla";
        skipRect.sizeDelta = new Vector2(Mathf.Max(SkipWidth, skipLabel.preferredWidth + 32f), skipRect.sizeDelta.y);
    }

    // Dil ya da tablo değişti: açıksa hemen, değilse bir sonraki açılışta yeniden yazılır.
    private void InvalidateLabels()
    {
        labelLanguage = null;
        if (IsOpen) RefreshLabels();
    }

    private void OnDestroy() => DialogueText.LanguageChanged -= InvalidateLabels;

    // Test / araç: etiketlerin şu anki metni.
    public string NameText => nameLabel != null ? nameLabel.text : "";
    public string SkipText => skipLabel != null ? skipLabel.text : "";

    // İlk basış: giriş hareketini oturtur ve yazıyı tamamlar. Yazı tamamsa sonraki satıra geçer; son satırsa kapatır.
    public void Advance()
    {
        if (!IsOpen) return;
        bool settling = !posed;
        Settle();
        if (visible < total)
        {
            visible = total;
            text.maxVisibleCharacters = total;
            pauseLeft = 0f;
            return;
        }
        if (settling) return;
        lineIndex++;
        if (entry != null && lineIndex < entry.lines.Length) BeginLine();
        else Close();
    }

    public void Skip() => Close();

    // Konuşma hemen biter (sonraki satır aynı karede açılabilir); kutu tık almayı bırakır ve kısa bir çıkışla kaybolur.
    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        entry = null;
        RestoreNavigation();
        group.blocksRaycasts = false;
        exiting = gameObject.activeInHierarchy;
        exitTime = 0f;
        if (!exiting) Hide();
        Finished?.Invoke();
    }

    private void Hide()
    {
        exiting = false;
        enabled = false;
        gameObject.SetActive(false);
    }

    private void BeginLine()
    {
        DialogueLine line = entry.lines[lineIndex];
        text.text = DialogueText.Get(line.textKey);
        text.maxVisibleCharacters = 0;
        text.ForceMeshUpdate();
        total = text.textInfo.characterCount;
        visible = 0;
        reveal = 0f;
        pauseLeft = 0f;
        idle = 0f;
        if (indicator != null) indicator.gameObject.SetActive(false);
    }

    // Test / araç: saati elle ilerletir (Update ile aynı yol).
    public void Step(float delta)
    {
        if (exiting && !IsOpen)
        {
            exitTime += delta;
            Animate();
            if (exitTime >= ExitSeconds) Hide();
            return;
        }
        if (!IsOpen) return;
        enterTime += delta;
        Animate();
        if (visible < total)
        {
            if (pauseLeft > 0f) { pauseLeft -= delta; return; }
            reveal += delta * CharsPerSecond * DialogueText.SpeedScale;
            int target = Mathf.Min(total, (int)reveal);
            if (target == visible) return;
            visible = target;
            text.maxVisibleCharacters = visible;
            // Kaynak metindeki karakter: fontta karşılığı olmayan harf (henüz yedek fontu olmayan dil) kutu işaretine dönse de
            // noktalama doğru tanınır; zengin metin etiketleri sayılmaz.
            TMP_CharacterInfo info = text.textInfo.characterInfo[visible - 1];
            string source = text.text;
            char last = info.index >= 0 && info.index < source.Length ? source[info.index] : info.character;
            if (visible < total && PauseMarks.IndexOf(last) >= 0)
            {
                pauseLeft = PunctuationPause;
                reveal = visible;
            }
            return;
        }
        idle += delta;
        if (indicator != null)
        {
            if (!indicator.gameObject.activeSelf) indicator.gameObject.SetActive(true);
            indicator.anchoredPosition = new Vector2(-30f, 22f + 5f * Mathf.Abs(Mathf.Sin(idle * 5f)));
        }
        if (!Big && idle >= SmallCloseSeconds) Advance();
    }

    // Giriş / çıkış ve konuşma kıpırtısı: yalnız dönüşüm ve renk yazılır. Giriş bitince poz bir kez yazılır, sonra kareler dokunmaz
    // (arayüz her karede yeniden dizilmesin).
    private void Animate()
    {
        Vector2 rest = RestPosition;
        if (exiting && !IsOpen)
        {
            float x = Mathf.Clamp01(exitTime / ExitSeconds), ease = x * x;
            group.alpha = 1f - ease * ease;
            panel.anchoredPosition = Big ? rest + new Vector2(0f, -BigRise * .7f * ease) : rest + new Vector2(-SmallSlide * ease, 0f);
            return;
        }
        if (!posed) Pose(rest);
    }

    private void Pose(Vector2 rest)
    {
        float e = BackOut(Mathf.Clamp01(enterTime / EnterSeconds));
        if (Big)
        {
            panel.anchoredPosition = rest + new Vector2(0f, -BigRise * (1f - e));
            float s = Mathf.LerpUnclamped(.94f, 1f, e);
            panel.localScale = new Vector3(s, s, 1f);
            panel.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(-3f, PanelTilt, e));
            float c = Mathf.Clamp01(enterTime / DimFadeSeconds);
            Color tint = blocker.color;
            float fade = 1f - (1f - c) * (1f - c);
            tint.a = DimAlpha * fade;
            cornerDots.alpha = fade;
            blocker.color = tint;
        }
        else
        {
            panel.anchoredPosition = rest + new Vector2(-SmallSlide * (1f - e), 0f);
            panel.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(2.5f, PanelTilt, e));
        }
        float plate = BackOut(Mathf.Clamp01((enterTime - PlateDelay) / PlatePop));
        namePlate.localScale = new Vector3(plate, plate, 1f);
        posed = enterTime >= EnterSeconds;
    }

    // Hedefi biraz aşıp yerine oturan yumuşama (0 → 1).
    private static float BackOut(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private void Update()
    {
        // Çıkış bir temizliktir: duraklatma ya da konuşma saatinden bağımsız, gerçek zamanla biter (yarım kutu ekranda asılı kalmaz).
        if (exiting && !IsOpen) { Step(Mathf.Min(Time.unscaledDeltaTime, .1f)); return; }
        if (!IsOpen) return;
        if (!DialogueDirector.Paused)
        {
            bool key = Input.GetKeyDown(KeyCode.Space) || (Big && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)));
            if (key) Advance();
        }
        Step(DialogueDirector.ClockDelta);
    }

    // Boşluk / Enter, kutu açıkken seçili başka bir düğmeye "gönder" olmasın.
    private void SuspendNavigation()
    {
        EventSystem system = EventSystem.current;
        if (system == null || navigationOwner == system) return;
        RestoreNavigation();
        navigationOwner = system;
        navigationWas = system.sendNavigationEvents;
        system.sendNavigationEvents = false;
        system.SetSelectedGameObject(null);
    }

    private void RestoreNavigation()
    {
        if (navigationOwner != null) navigationOwner.sendNavigationEvents = navigationWas;
        navigationOwner = null;
    }

    private void OnDisable() { if (!IsOpen) RestoreNavigation(); }

    // ---- kurulum yardımcıları ----
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.sizeDelta = size;
        return rect;
    }

    private static Vector4 Inner(Vector4 cuts, float by) =>
        new(Mathf.Max(0f, cuts.x - by), Mathf.Max(0f, cuts.y - by), Mathf.Max(0f, cuts.z - by), Mathf.Max(0f, cuts.w - by));

    private static DialoguePlateGraphic Plate(string name, RectTransform parent, Color color, Vector2 offset, Vector2 inset, Vector4 cuts)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offset + inset;
        rect.offsetMax = offset - inset;
        var plate = rect.gameObject.AddComponent<DialoguePlateGraphic>();
        plate.Set(DialoguePlateGraphic.Shape.CutRect, cuts);
        plate.color = color;
        plate.raycastTarget = false;
        return plate;
    }

    // Kısa üçgen mürekkep çizgisi (köşe süsü).
    private static void Stroke(RectTransform parent, Vector2 anchor, Vector2 position, Vector2 size, float angle)
    {
        RectTransform rect = Rect("Stroke", parent, size);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        var stroke = rect.gameObject.AddComponent<DialoguePlateGraphic>();
        stroke.Set(DialoguePlateGraphic.Shape.Triangle, Vector4.zero);
        stroke.color = Ink;
        stroke.raycastTarget = false;
    }

    // Köşe tramı: iri noktalar köşede. uv, ortak dokunun yoğun köşesini istenen köşeye çevirir.
    private static void Halftone(string name, RectTransform parent, float size, Vector2 corner, Rect uv, Color color) =>
        HalftoneImage(name, parent, size, corner, uv, color, DialogueArt.Halftone);

    private static RectTransform HalftoneImage(string name, RectTransform parent, float size, Vector2 corner, Rect uv, Color color, Texture2D texture)
    {
        RectTransform rect = Rect(name, parent, new Vector2(size, size));
        rect.anchorMin = rect.anchorMax = rect.pivot = corner;
        rect.anchoredPosition = Vector2.zero;
        var dots = rect.gameObject.AddComponent<RawImage>();
        dots.texture = texture;
        dots.uvRect = uv;
        dots.color = color;
        dots.raycastTarget = false;
        return rect;
    }

    private static TMP_Text Label(string name, RectTransform parent, TMP_FontAsset font, float fontSize)
    {
        RectTransform rect = Rect(name, parent, Vector2.zero);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) label.font = font;
        label.fontSize = fontSize;
        label.color = Ink;
        label.raycastTarget = false;
        return label;
    }
}
