using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Boss ödülü ekranı (RoundChoice durumu): geçilen boss'tan sonra en çok üç farklı ödül gösterir, biri alınır.
// Yalnız gösterir ve isteği iletir: teklif, tek sefer uygulama ve birikme BossRewardManager'da. UIManager kurar ve açıp kapatır.
// Kart: etki (tek alış), şu anki birikim ve seçince oluşacak toplam. Uygun ödül kalmadıysa "DEVAM" ile geçilir.
// Aşamalı havuzda (Bölüm 3.7.3) başlık teklifin aşamasını ("GÜÇLÜ AŞAMA · BOSS ÖDÜLÜ"), kartın üstündeki etiket ödülün kendi
// sınıfını ve ilk sunulduğu boss'u yazar. Aşama ve round bilgisi BossRewardManager'dan okunur; burada round eşiği hesaplanmaz.
// Bedelli ödülde (Bölüm 3.7.4) kart kazanç ve bedeli ayrı yazar: satırlar ödül verisinden, "3 → 4" gerçek seçim hakkından üretilir;
// altında geçerlilik notu ve birlikte alınamayan ödül durur. Diğer kartların düzeni aynıdır.
// Item kutusu (11 Kasım 2026, kullanıcı: "kart gibi olmayacak; eski küçük ödül gibi ama item seçiyormuşuz gibi kutu, kare"):
// ödül kareye yakın bir kutudur (eski kartın oranı). Sol üstte kare item yuvası (ödülün görseli; yoksa etkisinden çizilen ikon),
// yanında ad plakası; altında eski üç yazı bölgesi ve "AL". Yazı bölgeleri yalnız başlık satırının farkı kadar (ImageShift) aşağıda;
// ölçüleri, puntoları ve düğmeye mesafeleri aynı. Ödül mantığı (BossRewardManager) değişmedi. Kit varsa comic kağıt görünümü.
public sealed class BossRewardPanelUI : MonoBehaviour
{
    private const float CardWidth = 440f, CardHeight = 466f, Gap = 36f;
    // Başlık satırı: 20 px pay + item yuvası. Eski 76 px'lik başlığa göre yazılar ImageShift kadar aşağıda başlar.
    private const float HeaderHeight = 116f, ImageShift = 36f, ItemSize = 96f, HeaderPad = 20f;
    private static readonly Color Ink = new Color32(54, 39, 54, 255);
    private static readonly Color RewardGold = new Color32(247, 192, 93, 255);   // altın = ödül (renk kuralı)
    private static readonly Color Plus = new Color32(46, 125, 50, 255);
    private static readonly Color Muted = new Color32(110, 96, 110, 255);
    private const string GainColor = "#2E7D32", CostColor = "#B4231A";
    private readonly List<string> gains = new(), costs = new();
    private readonly List<BossRewardSO> exclusive = new();

    private sealed class Card
    {
        public RectTransform root;
        public TextMeshProUGUI title, effect, note, stack, stage;
        public Button button;
        public BossRewardSO reward;
        public Image itemImage;
        public ResonanceBadgeGraphic itemBadge;
    }

    private readonly List<Card> cards = new();
    private RectTransform row;
    private ComicUITheme theme;
    private TextMeshProUGUI title, subtitle, empty, takenList;
    private Button continueButton;
    private bool requested;
    private int shownVersion = -1;
    // Test ve doğrulama için: ekrandaki başlık, alt başlık ve kartların yazıları.
    public string TitleText => title != null ? title.text : null;
    public string SubtitleText => subtitle != null ? subtitle.text : null;
    public int ShownCards { get; private set; }
    public IEnumerable<TextMeshProUGUI> CardTexts(int index)
    {
        Card card = cards[index];
        yield return card.title; yield return card.effect; yield return card.note; yield return card.stack; yield return card.stage;
    }
    public string StageTagText(int index) => cards[index].stage.gameObject.activeSelf ? cards[index].stage.text : null;
    public string NoteText(int index) => cards[index].note.text;

    public static BossRewardPanelUI Attach(Transform canvasRoot)
    {
        if (canvasRoot == null) return null;
        var existing = canvasRoot.GetComponentInChildren<BossRewardPanelUI>(true);
        if (existing != null) return existing;
        var go = new GameObject("Boss Reward Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = canvasRoot.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(canvasRoot, false);
        FeelOverlay.Stretch(rect);
        var dim = go.GetComponent<Image>();
        dim.color = new Color(0.05f, 0.04f, 0.08f, 0.72f);
        dim.raycastTarget = true; // arkadaki panellere tıklanmasın
        var panel = go.AddComponent<BossRewardPanelUI>();
        go.SetActive(false);
        return panel;
    }

    private void Awake()
    {
        theme = FeelOverlay.Theme;
        var root = (RectTransform)transform;
        CardSelectionVignette.Attach(root, CardSelectionVignette.Palette.Boss);
        bool kit = ComicKit.Ready;
        title = CreateText(root, "Title", 46f, TextAlignmentOptions.Center, Color.white);
        if (kit) ComicKit.Display(title, true);   // karartılmış zeminde beyaz, mürekkep konturlu Bungee
        Place(title.rectTransform, new Vector2(0f, 330f), new Vector2(1500f, 70f));
        subtitle = CreateText(root, "Subtitle", 26f, TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.78f));
        Place(subtitle.rectTransform, new Vector2(0f, 275f), new Vector2(1500f, 40f));
        row = new GameObject("Options", typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(transform, false);
        Place(row, new Vector2(0f, -20f), new Vector2(3 * CardWidth + 2 * Gap, CardHeight));
        // Yazısı Fill'de kurulur: dil değişince yenilenir.
        empty = CreateText(root, "Empty", 32f, TextAlignmentOptions.Center, new Color(1f, 0.93f, 0.78f));
        Place(empty.rectTransform, new Vector2(0f, 30f), new Vector2(1200f, 120f));
        continueButton = CreateButton(root, "Continue", "", "green", new Vector2(260f, 80f));
        if (kit) ComicKit.StyleButton(continueButton, ComicKit.Blue);   // mavi = ilerle
        TMP_Text continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
        if (continueLabel != null) LocalizedText.Bind(continueLabel, "ui.reward.panel.continue");
        Place((RectTransform)continueButton.transform, new Vector2(0f, -90f), new Vector2(260f, 80f));
        continueButton.onClick.AddListener(RequestContinue);
        takenList = CreateText(root, "Taken", 22f, TextAlignmentOptions.Top, new Color(0.82f, 0.95f, 0.74f));
        takenList.textWrappingMode = TextWrappingModes.Normal;
        Place(takenList.rectTransform, new Vector2(0f, -356f), new Vector2(1500f, 150f));
    }

    private void OnEnable()
    {
        requested = false;
        // Dil değişince kart yazıları (veriden kurulanlar dahil) yeniden yazılır; karede yazı kurulmaz.
        GameText.Changed += Fill;
        Fill();
    }

    private void OnDisable() => GameText.Changed -= Fill;

    private void Update()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (manager != null && manager.Version != shownVersion) Fill();
    }

    private void Fill()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        RoundManager rounds = RoundManager.Instance;
        shownVersion = manager != null ? manager.Version : -1;
        IReadOnlyList<BossRewardSO> offer = manager != null ? manager.Offer : null;
        int count = offer != null ? offer.Count : 0;
        int next = rounds != null ? rounds.CurrentRound + 1 : 0;
        // Run'ın son boss'u (açık boss takvimi): sonraki round yok; seçimden sonra zafer ekranı gelir.
        bool last = rounds != null && rounds.CurrentRound >= rounds.MaxRounds;
        // Aşamalı havuz: başlık teklifin aşamasıdır; "boss geçildi" bilgisi alt satıra iner. Düz havuzda metinler eskisi gibi.
        BossRewardStage offerStage = manager != null ? manager.OfferStage : null;
        // Aşamalı başlıkta alt satır "Boss geçildi · yalnız biri alınır …" diye sürer (ayrı anahtarlar: dilde büyük / küçük harf).
        string tail = last ? "ui.reward.panel.last_tail" : "ui.reward.panel.tail";
        title.text = offerStage != null ? BossRewardText.StageHeader(offerStage)
            : GameText.Get(last ? "ui.reward.panel.title_last" : "ui.reward.panel.title");
        subtitle.text = count == 0 ? GameText.Get("ui.reward.panel.none")
            : offerStage == null ? GameText.Format(tail, next)
            : GameText.Format(last ? "ui.reward.panel.passed_last" : "ui.reward.panel.passed", GameText.Format(tail + ".lower", next));
        ShownCards = count;
        float total = count * CardWidth + Mathf.Max(0, count - 1) * Gap;
        for (int i = 0; i < count; i++)
        {
            if (i == cards.Count) cards.Add(CreateCard(i));
            Card card = cards[i];
            BossRewardSO reward = offer[i];
            card.reward = reward;
            card.root.gameObject.SetActive(true);
            card.root.anchoredPosition = new Vector2(-total * 0.5f + CardWidth * 0.5f + i * (CardWidth + Gap), 0f);
            int have = manager.Stacks(reward);
            card.title.text = reward.DisplayName;
            // Item görseli: ödülün kendi sprite'ı; yoksa etkisinden çizilen yer tutucu ikon.
            bool picture = reward.icon != null;
            card.itemImage.sprite = reward.icon;
            card.itemImage.gameObject.SetActive(picture);
            card.itemBadge.gameObject.SetActive(!picture);
            if (!picture) card.itemBadge.SetRecipe(BossRewardText.IconRecipe(reward));
            bool trade = BossRewardText.IsTrade(reward);
            manager.ExclusiveWith(reward, exclusive);
            // Etkili ödülün kural metni uzundur: bedelli kartın geniş etki bölgesini kullanır.
            Arrange(card, trade || reward.effect != null);
            if (trade)
            {
                BossRewardText.TradeLines(reward, rounds != null ? rounds.ChoicesPerLevel : 0, gains, costs);
                card.effect.text = TradeText(gains, costs);
                string note = manager.NoteFor(reward);
                card.note.text = reward.levelChoiceDelta != 0 ? (BossRewardText.PendingChoicesNote + " " + note).Trim() : note;
                card.stack.text = TradeStackText(reward, have, exclusive);
                card.stack.textWrappingMode = TextWrappingModes.Normal;
            }
            else if (reward.effect != null)
            {
                // Çalışma zamanı etkisi olan ödül (Bölüm 3.8): kural metni etkinin verisinden; altında ön koşul ve yerleşim adımı.
                card.effect.text = BossRewardText.Effect(reward);
                card.note.text = manager.NoteFor(reward);
                card.stack.text = BossRewardText.EffectStackText(reward, exclusive);
                card.stack.textWrappingMode = TextWrappingModes.Normal;
            }
            else
            {
                card.effect.text = BossRewardText.Effect(reward);
                card.note.text = manager.NoteFor(reward);
                card.stack.text = exclusive.Count > 0 ? TradeStackText(reward, have, exclusive) : StackText(reward, have);
                if (exclusive.Count > 0) card.stack.textWrappingMode = TextWrappingModes.Normal;
                else KeepTwoLines(card.stack);
            }
            // Ödülün kendi sınıfı (teklifin aşamasından düşük olabilir) ve ilk sunulduğu boss.
            BossRewardStage stage = manager.StageOf(reward);
            card.stage.gameObject.SetActive(stage != null);
            if (stage != null)
            {
                card.stage.text = BossRewardText.StageTag(stage, manager.FirstOfferRound(stage));
                card.stage.color = stage.color;
            }
            card.button.interactable = manager.IsPending && !requested;
        }
        for (int i = count; i < cards.Count; i++) cards[i].root.gameObject.SetActive(false);
        bool none = manager != null && manager.IsPending && count == 0;
        if (none) empty.text = $"{GameText.Get("ui.reward.panel.empty")}\n<size=70%>{GameText.Get("ui.reward.panel.empty.detail")}</size>";
        empty.gameObject.SetActive(none);
        continueButton.gameObject.SetActive(none);
        takenList.text = TakenText(manager);
    }

    // Birikim yazısı eskisi gibi iki satırdır ("Şu an …" / "Seçince …"): satır kırmadan, gerekirse en küçük puntoya kadar küçülür.
    // O puntoda da kart genişliğine sığmıyorsa (Hasat Ritmi) satır kırılır; taşmak yerine bölgesinin içinde üç satır olur.
    private static void KeepTwoLines(TextMeshProUGUI text)
    {
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.ForceMeshUpdate();
        if (!text.isTextTruncated && !text.isTextOverflowing && text.textBounds.size.x <= text.rectTransform.rect.width + .5f) return;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.ForceMeshUpdate();
    }

    // Bedelli kartın düzeni: kazanç + bedel dört satır tutar, o yüzden etki bölgesi büyür, açıklama ve alt bölge küçülür.
    // Diğer kartlar 3.7.3'teki üç bölgeyi aynen kullanır. Bölgeler item yuvasının altından başlar (ImageShift).
    private static void Arrange(Card card, bool trade)
    {
        float top = CardHeight * 0.5f - ImageShift;
        Place(card.effect.rectTransform, new Vector2(0f, top - (trade ? 143f : 124f)), new Vector2(CardWidth - 36f, trade ? 122f : 70f));
        Place(card.note.rectTransform, new Vector2(0f, top - (trade ? 234f : 199f)), new Vector2(CardWidth - 40f, trade ? 56f : 76f));
        Place(card.stack.rectTransform, new Vector2(0f, top - (trade ? 298f : 286f)), new Vector2(CardWidth - 28f, trade ? 68f : 92f));
        card.effect.fontSizeMin = trade ? 17f : 20f;
        card.effect.fontSizeMax = trade ? 24f : 27f;
    }

    // "KAZANÇ / Gelecekteki her level: 3 → 4 seçim / BEDEL / Doğrudan vuruş hasarı ×0,80": başlıklar küçük, satırlar renkli.
    private static string TradeText(List<string> gains, List<string> costs)
    {
        var text = new System.Text.StringBuilder();
        void Block(string caption, string color, List<string> lines)
        {
            if (lines.Count == 0) return;
            if (text.Length > 0) text.Append('\n');
            text.Append("<color=").Append(color).Append("><size=62%>").Append(caption).Append("</size>");
            foreach (string line in lines) text.Append('\n').Append(line);
            text.Append("</color>");
        }
        Block(GameText.Get("ui.reward.panel.gain"), GainColor, gains);
        Block(GameText.Get("ui.reward.panel.cost"), CostColor, costs);
        return text.ToString();
    }

    // Bedelli kartın alt satırları: kaç kez alınabildiği ve birlikte alınamayan ödül (karşılıklı dışlama).
    public static string TradeStackText(BossRewardSO reward, int have, List<BossRewardSO> exclusive)
    {
        string count = reward.maxStacks == 1 ? GameText.Get("ui.reward.once") : GameText.Format("ui.reward.panel.have", have, reward.maxStacks);
        if (exclusive == null || exclusive.Count == 0) return $"<color=#6E606E>{count}</color>";
        var names = new List<string>();
        foreach (BossRewardSO other in exclusive) names.Add(other.DisplayName);
        return $"<color=#6E606E>{count}</color>\n<color=#8A3A12>{GameText.Format("ui.reward.excludes", string.Join(", ", names))}</color>";
    }

    // "Şu an 1/3: ×1,15" ve "Seçince 2/3: ×1,32" — art arda alışların nasıl birleştiği kartta görünür.
    public static string StackText(BossRewardSO reward, int have)
    {
        string now = have > 0 ? GameText.Format("ui.reward.panel.have_total", have, reward.maxStacks, BossRewardText.Value(reward, have))
            : GameText.Format("ui.reward.panel.have_none", reward.maxStacks);
        string after = GameText.Format("ui.reward.panel.after_total", have + 1, reward.maxStacks, BossRewardText.Value(reward, have + 1));
        return $"<color=#6E606E>{now}</color>\n<color=#2E7D32>{after}</color>";
    }

    private static string TakenText(BossRewardManager manager)
    {
        if (manager == null || manager.Taken.Count == 0) return "";
        var lines = new List<string>();
        foreach (BossRewardSO reward in manager.Taken) lines.Add(BossRewardText.ListLine(reward, manager.Stacks(reward)));
        return GameText.Get("ui.reward.panel.taken") + "\n<size=82%>" + string.Join("   ·   ", lines) + "</size>";
    }

    // Çift tıklamaya karşı arayüzde de tek istek; asıl koruma BossRewardManager.Choose'da.
    private void Request(Card card)
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (requested || manager == null || card.reward == null) return;
        // Yerleşim isteyen ödül (anten): ödül henüz alınmaz; konum seçimi açılır (RewardPlacementUI). Vazgeçilirse bu panel aynı
        // teklifle yeniden açılır (OnEnable isteği sıfırlar).
        if (manager.NeedsPlacement(card.reward)) { manager.BeginPlacement(card.reward); return; }
        requested = true;
        foreach (Card c in cards) c.button.interactable = false;
        if (!manager.Choose(card.reward)) requested = false;
    }

    private void RequestContinue()
    {
        BossRewardManager manager = BossRewardManager.Instance;
        if (requested || manager == null) return;
        requested = true;
        if (!manager.ContinueWithoutReward()) requested = false;
    }

    private Card CreateCard(int index)
    {
        var root = new GameObject("Reward " + index, typeof(RectTransform)).GetComponent<RectTransform>();
        root.SetParent(row, false);
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = new Vector2(CardWidth, CardHeight);
        // Comic kağıt kiti (Docs/UI-Dili.md): krem kutu; ad plakası altına boyanmış etiket şeridi (altın = ödül). Kit yoksa eski
        // kâğıt plakaları (başlık bandı yuva satırı kadar).
        bool kit = ComicKit.Ready && ComicKit.Get("label_cream") != null && ComicKit.Get("slot_gold") != null;
        float top = CardHeight * 0.5f;
        float slotX = -CardWidth * 0.5f + 28f + ItemSize * 0.5f, rowY = top - HeaderPad - ItemSize * 0.5f;
        float nameLeft = slotX + ItemSize * 0.5f + 12f, nameRight = CardWidth * 0.5f - 22f;
        if (kit)
        {
            Image box = ComicKit.Sliced(root, "Comic Panel", "panel_cream", 768f / CardWidth);
            var boxRect = box.rectTransform;
            boxRect.anchorMin = Vector2.zero; boxRect.anchorMax = Vector2.one;
            boxRect.offsetMin = new Vector2(-6f, -8f); boxRect.offsetMax = new Vector2(8f, 6f);
            Image plate = ComicKit.Sliced(root, "Name Plate", "label_cream", 176f / 66f);
            plate.color = RewardGold;
            Place(plate.rectTransform, new Vector2((nameLeft + nameRight) * 0.5f, rowY), new Vector2(nameRight - nameLeft, 66f));
        }
        else
        {
            Plate(root, "Ink shadow", new Color32(40, 29, 43, 220), new Vector2(7f, -8f), new Vector2(7f, -8f));
            Plate(root, "Ink outline", Ink, Vector2.zero, Vector2.zero);
            Plate(root, "Cream paper", new Color32(255, 242, 210, 255), new Vector2(4f, 4f), new Vector2(-4f, -4f));
            var header = Plate(root, "Green header", new Color32(150, 214, 120, 255), new Vector2(7f, 0f), new Vector2(-7f, -7f));
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.offsetMin = new Vector2(7f, -HeaderHeight);
        }

        var card = new Card { root = root };
        card.title = CreateText(root, "Name", 28f, TextAlignmentOptions.Center, Ink);
        if (kit) ComicKit.Display(card.title, false);   // altın plakada koyu mürekkep Bungee
        // Ad plakaya sığmazsa önce iki satıra iner, sonra küçülür (yalnız yazı değişince, karede değil); kesilmez.
        card.title.enableAutoSizing = true;
        card.title.fontSizeMin = 14f;
        card.title.fontSizeMax = kit ? 24f : 28f;
        card.title.overflowMode = TextOverflowModes.Overflow;
        card.title.textWrappingMode = TextWrappingModes.Normal;
        Place(card.title.rectTransform, new Vector2((nameLeft + nameRight) * 0.5f - 2f, rowY + 2f), new Vector2(nameRight - nameLeft - 44f, 52f));
        CreateItemSlot(card, new Vector2(slotX, rowY), kit);
        // Üç metin bölgesi alt alta, üst üste binmeden: etki (item yuvasının altı), açıklama, birikim (düğmenin üstüne kadar).
        // Uzun metin satır kırar; sığmazsa bölgenin en küçük puntosuna kadar küçülür, kartın dışına taşmaz.
        card.effect = CreateText(root, "Effect", 27f, TextAlignmentOptions.Center, Plus);
        Wrap(card.effect, 20f, 27f);
        Place(card.effect.rectTransform, new Vector2(0f, top - ImageShift - 124f), new Vector2(CardWidth - 36f, 70f));
        card.note = CreateText(root, "Note", 19f, TextAlignmentOptions.Center, Muted);
        Wrap(card.note, 15f, 19f);
        Place(card.note.rectTransform, new Vector2(0f, top - ImageShift - 199f), new Vector2(CardWidth - 40f, 76f));
        // Şu anki birikim ve seçince oluşacak toplam. Uzun etki (Hasat Ritmi) ikinci satırı kırar.
        card.stack = CreateText(root, "Stack", 21f, TextAlignmentOptions.Center, Ink);
        Wrap(card.stack, 15f, 21f);
        Place(card.stack.rectTransform, new Vector2(0f, top - ImageShift - 286f), new Vector2(CardWidth - 28f, 92f));
        // Aşama etiketi kartın üstünde durur (yalnız aşamalı havuzda görünür).
        card.stage = CreateText(root, "Stage", 18f, TextAlignmentOptions.Center, Color.white);
        card.stage.enableAutoSizing = true;
        card.stage.fontSizeMin = 14f;
        card.stage.fontSizeMax = 18f;
        Place(card.stage.rectTransform, new Vector2(0f, top + 20f), new Vector2(CardWidth, 28f));
        card.stage.gameObject.SetActive(false);
        // Düğme kartın altına göre durur: birikim yazısıyla arasındaki 6 px pay kart uzasa da aynı kalır.
        card.button = CreateButton(root, "Choose", "", "green", new Vector2(220f, 76f));
        if (kit) ComicKit.StyleButton(card.button, ComicKit.Gold);   // altın = ödül al
        TMP_Text chooseLabel = card.button.GetComponentInChildren<TMP_Text>(true);
        if (chooseLabel != null) LocalizedText.Bind(chooseLabel, "ui.reward.panel.choose");
        Place((RectTransform)card.button.transform, new Vector2(0f, -top + 54f), new Vector2(220f, 76f));
        card.button.onClick.AddListener(() => Request(card));
        return card;
    }

    // Item yuvası kutunun sol üstünde: kitin altın karesi (altın = ödül). İçinde iki görsel: ödülün sprite'ı ya da yer tutucu ikon
    // (Fill seçer).
    private static void CreateItemSlot(Card card, Vector2 position, bool kit)
    {
        Image slot = ComicKit.Sliced(card.root, "Item Slot", kit ? "slot_gold" : "slot_white", 256f / ItemSize);
        if (slot.sprite == null) slot.color = new Color32(240, 222, 182, 255);   // kit yok: kâğıttan biraz koyu düz kare
        Place(slot.rectTransform, position, new Vector2(ItemSize, ItemSize));
        card.itemImage = Inset<Image>(slot.rectTransform, "Item Image", 13f);
        card.itemImage.preserveAspect = true;
        card.itemImage.gameObject.SetActive(false);
        card.itemBadge = Inset<ResonanceBadgeGraphic>(slot.rectTransform, "Item Badge", 15f);
    }

    // Yuvayı her kenardan inset kadar içeriden dolduran, tıklama almayan görsel.
    private static T Inset<T>(RectTransform parent, string name, float inset) where T : Graphic
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        var graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    private static void Wrap(TextMeshProUGUI text, float min, float max)
    {
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Truncate;
        text.enableAutoSizing = true;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
    }

    private Button CreateButton(RectTransform parent, string name, string caption, string palette, Vector2 size)
    {
        if (theme != null) return theme.CreateButton(parent, name, caption, palette, size);
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        return go.GetComponent<Button>();
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static ComicPopupPlate Plate(RectTransform parent, string name, Color color, Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var plate = go.AddComponent<ComicPopupPlate>();
        plate.color = color;
        plate.raycastTarget = false;
        return plate;
    }

    private TextMeshProUGUI CreateText(RectTransform parent, string name, float size, TextAlignmentOptions alignment, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        if (theme != null) theme.StylePopupText(text);
        text.color = color;
        text.fontSize = size;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }
}
