using UnityEngine;

// Run süre sayaçları (Bölüm 3.7.8). "50–60 dakika aktif oynanış" hedefi yalnız round'ların kendisini sayar: kart ve boss ödülü
// seçimi, mağaza, hazırlık, level hesaplama bekleyişi ve duraklama AYRI sayaçlardadır ve aktif süreyi doldurmaz.
// - Aktif süre iki ayrı sayıdır: round sayacından düşen süre (oyun zamanı; biten round'ların süre toplamına eşittir) ve o sırada
//   gerçekte geçen süre (round sonu yavaşlaması gerçek süreyi biraz uzatır). İkisini RoundManager her round karesinde bildirir.
// - Diğer sayaçlar gerçek (ölçeklenmemiş) süredir ve oyun durumundan okunur.
// - Duraklama: uygulamanın gerçekten durduğu süre (pencere odağını kaybetti / uygulama askıya alındı ve o arada kare işlenmedi).
//   Editörde odak kaybında oyun çalışmaya devam eder; o süre duraklama sayılmaz, içinde bulunulan ekranın sayacına yazılır.
// RoundManager kurar; run başında sıfırlanır. Yalnız sayar: oyun akışını değiştirmez.
[DisallowMultipleComponent]
public sealed class RunClock : MonoBehaviour
{
    public static RunClock Instance { get; private set; }

    // Round sayacından düşen süre (oyun zamanı).
    public double ActiveGameSeconds { get; private set; }
    // Aynı karelerde gerçekte geçen süre.
    public double ActiveRealSeconds { get; private set; }
    // Round süresi bitti, kart kararı bekleyen level işini bekliyor (RoundManager.IsAwaitingLevels).
    public double LevelWorkSeconds { get; private set; }
    public double CardSelectionSeconds { get; private set; }
    // Boss ödülü ve uzmanlaşma seçimi (round sonu seçimi).
    public double RoundChoiceSeconds { get; private set; }
    // Mağaza: ağaç, saksı alımı, yerleştirme ve satış.
    public double ShopSeconds { get; private set; }
    // Hazırlık: run başı hazırlığı ve round özeti / önizleme ekranı.
    public double PreparationSeconds { get; private set; }
    // Run sonu ekranı ve yukarıdakilere girmeyen her şey.
    public double OtherSeconds { get; private set; }
    public double PausedSeconds { get; private set; }
    public int RoundsTimed { get; private set; }
    // Son biten round: sayaçtaki süre ve gerçekte geçen süre.
    public double LastRoundGameSeconds { get; private set; }
    public double LastRoundRealSeconds { get; private set; }

    // Seçim ekranları (kart + boss ödülü) ve round dışı toplam (duraklama hariç).
    public double ChoiceSeconds => CardSelectionSeconds + RoundChoiceSeconds;
    public double OutsideRoundSeconds => LevelWorkSeconds + ChoiceSeconds + ShopSeconds + PreparationSeconds;

    private RoundManager rounds;
    private int activeFrame = -1;
    private double roundGame, roundReal;
    private bool suspended, tickedWhileSuspended;
    private double suspendedAt, pendingGap;

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += ResetClock;
        rounds.OnRoundEnded += CloseRound;
    }

    private void OnDisable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted -= ResetClock;
        rounds.OnRoundEnded -= CloseRound;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetClock()
    {
        ActiveGameSeconds = ActiveRealSeconds = LevelWorkSeconds = CardSelectionSeconds = RoundChoiceSeconds = 0d;
        ShopSeconds = PreparationSeconds = OtherSeconds = PausedSeconds = 0d;
        LastRoundGameSeconds = LastRoundRealSeconds = roundGame = roundReal = pendingGap = 0d;
        RoundsTimed = 0;
        activeFrame = -1;
        suspended = tickedWhileSuspended = false;
    }

    // RoundManager: aktif round'un bu karesi. gameSeconds sayaçtan düşen, realSeconds gerçekte geçen süredir.
    public void AddActive(double gameSeconds, double realSeconds)
    {
        realSeconds = WithoutPause(realSeconds);
        ActiveGameSeconds += gameSeconds;
        ActiveRealSeconds += realSeconds;
        roundGame += gameSeconds;
        roundReal += realSeconds;
        activeFrame = Time.frameCount;
    }

    private void CloseRound()
    {
        LastRoundGameSeconds = roundGame;
        LastRoundRealSeconds = roundReal;
        roundGame = roundReal = 0d;
        RoundsTimed++;
    }

    private void Update()
    {
        if (suspended) tickedWhileSuspended = true;
        GameManager game = GameManager.Instance;
        // Bu karenin süresini aktif round aldıysa (aynı karede round bitip ekran değişse de) ikinci kez yazılmaz.
        if (game == null || rounds == null || activeFrame == Time.frameCount) return;
        Advance(game.CurrentState, rounds.IsRoundActive, rounds.IsAwaitingLevels, FrameRealSeconds);
    }

    // Bu karenin gerçek süresi. Motor sabit kare süresiyle çalıştırılıyorsa (Time.captureDeltaTime: kayıt ve batch ölçümü) kare o
    // kadar gerçek süreyi temsil eder; aksi halde ölçeklenmemiş kare süresidir.
    public static double FrameRealSeconds => Time.captureDeltaTime > 0f ? Time.captureDeltaTime : Time.unscaledDeltaTime;

    // Bir karenin gerçek süresini o anki ekranın sayacına yazar. Test edilebilsin diye durum ve süre dışarıdan verilir; Update aynı
    // yolu kullanır. Aktif round'un süresi buradan yazılmaz (RoundManager, AddActive ile bildirir).
    public void Advance(GameStates state, bool roundActive, bool awaitingLevels, double realSeconds)
    {
        if (state == GameStates.Round && roundActive) return;
        realSeconds = WithoutPause(realSeconds);
        switch (state)
        {
            case GameStates.Round:
                if (awaitingLevels) LevelWorkSeconds += realSeconds; else OtherSeconds += realSeconds;
                break;
            case GameStates.CardSelection: CardSelectionSeconds += realSeconds; break;
            case GameStates.RoundChoice: RoundChoiceSeconds += realSeconds; break;
            case GameStates.Shop:
            case GameStates.Placing:
            case GameStates.Selling: ShopSeconds += realSeconds; break;
            case GameStates.RunSetup:
            case GameStates.RoundEnd: PreparationSeconds += realSeconds; break;
            default: OtherSeconds += realSeconds; break;
        }
    }

    // Askıdan dönüşten sonraki ilk kare, motorun o kareye yazdığı duraklama süresini taşıyorsa o pay düşülür (yalnız gerçek askıda).
    private double WithoutPause(double realSeconds)
    {
        if (pendingGap <= 0d) return realSeconds;
        if (realSeconds >= pendingGap * 0.9d) realSeconds = System.Math.Max(0d, realSeconds - pendingGap);
        pendingGap = 0d;
        return realSeconds;
    }

    private void OnApplicationFocus(bool focused) { if (focused) Resume(Time.realtimeSinceStartupAsDouble); else Suspend(Time.realtimeSinceStartupAsDouble); }
    private void OnApplicationPause(bool paused) { if (paused) Suspend(Time.realtimeSinceStartupAsDouble); else Resume(Time.realtimeSinceStartupAsDouble); }

    // Uygulama odağını kaybetti / askıya alındı (gerçek saat). Test için açık.
    public void Suspend(double realtime)
    {
        if (suspended) return;
        suspended = true;
        tickedWhileSuspended = false;
        suspendedAt = realtime;
    }

    // Geri dönüş: arada hiç kare işlenmediyse geçen gerçek süre duraklamadır. Kare işlendiyse (editör, arka planda çalışan build)
    // oyun durmamıştır; süre zaten ilgili sayaçlara yazıldı.
    public void Resume(double realtime)
    {
        if (!suspended) return;
        suspended = false;
        if (tickedWhileSuspended) return;
        double gap = System.Math.Max(0d, realtime - suspendedAt);
        PausedSeconds += gap;
        pendingGap = gap;
    }

    // Test: askı sırasında kare işlendi (oyun durmadı) durumunu kurar. Oyunda bunu Update kendisi işaretler.
    public void NoteFrameWhileSuspended() { if (suspended) tickedWhileSuspended = true; }

    // "51:40" (dakika:saniye); bir saati aşarsa "1:02:05".
    public static string Format(double seconds)
    {
        long total = (long)System.Math.Round(System.Math.Max(0d, seconds));
        long hours = total / 3600, minutes = total % 3600 / 60, secs = total % 60;
        return hours > 0 ? $"{hours}:{minutes:00}:{secs:00}" : $"{minutes}:{secs:00}";
    }
}
