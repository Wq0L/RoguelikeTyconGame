using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// Biten round'un özeti. RoundSummaryUI okur.
public sealed class RoundSummaryData
{
    public int Round;
    public int Harvests;
    public int Gold, Iron, Stone;
    public int Score;
    public int Xp;
    public int Levels;
}

// Oyunun büyük anlarını sahneler: round intro damgası, level up anı (damga + ada dalgası + ses)
// ve round özeti için istatistik takibi. Oynanışa dokunmaz; sadece event dinler.
public class GameFeelDirector : MonoBehaviour
{
    public static GameFeelDirector Instance { get; private set; }

    [Header("Round Intro")]
    [SerializeField] private bool showRoundIntro = true;
    [SerializeField] private Color roundColor = new Color(1f, 0.95f, 0.82f);
    [Tooltip("Kota duyurusu ve son round uyarısında round intro rengi (kırmızı değil).")]
    [FormerlySerializedAs("exhaustColor")]
    [SerializeField] private Color quotaColor = new Color(1f, 0.72f, 0.3f);
    [SerializeField] private float roundStampY = 120f;

    [Header("Level Up")]
    [SerializeField] private Color levelColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private float levelStampY = 300f;
    [Tooltip("Adadaki parlama dalgasının halka başına gecikmesi (sn).")]
    [SerializeField, Min(0f)] private float tileWaveStep = 0.035f;
    [Tooltip("Aynı anda gelen level up'larda efekt tekrarlanmaz, sadece damga güncellenir.")]
    [SerializeField, Min(0f)] private float levelEffectCooldown = 0.6f;

    private struct WaveHit
    {
        public float time;
        public GridObject cell;
    }

    private readonly List<WaveHit> wave = new();
    private float nextLevelEffectTime;
    private float pendingThumpTime = -1f;

    private GameManager game;
    private ProgressionManager progression;
    private ResourceManager resources;
    private GameStates lastState;

    private RoundSummaryData current;
    private long scoreAtStart;
    private int levelAtStart;
    private double xpAtStart;
    public RoundSummaryData LastRound { get; private set; }
    public int SummaryVersion { get; private set; }

    private void OnEnable()
    {
        Instance = this;
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        if (progression != null) progression.OnLevelUp -= HandleLevelUp;
        if (resources != null) resources.OnHarvestResourceAdded -= HandleHarvest;
        game = null;
        progression = null;
        resources = null;
        if (Instance == this) Instance = null;
    }

    private void TrySubscribe()
    {
        if (game == null && GameManager.Instance != null)
        {
            game = GameManager.Instance;
            game.OnGameStateChanged += HandleStateChanged;
            lastState = game.CurrentState;
        }
        if (progression == null && ProgressionManager.Instance != null)
        {
            progression = ProgressionManager.Instance;
            progression.OnLevelUp += HandleLevelUp;
        }
        if (resources == null && ResourceManager.Instance != null)
        {
            resources = ResourceManager.Instance;
            resources.OnHarvestResourceAdded += HandleHarvest;
        }
    }

    private void HandleStateChanged(GameStates state)
    {
        GameStates previous = lastState;
        lastState = state;

        if (state == GameStates.Round)
        {
            BeginRoundStats();
            if (showRoundIntro) PlayRoundIntro();
            return;
        }

        if (state == GameStates.RunSetup) LastRound = null;
        if (previous == GameStates.Round && current != null) FinishRoundStats();
        wave.Clear();
    }

    private void PlayRoundIntro()
    {
        RoundManager rounds = RoundManager.Instance;
        if (rounds == null) return;
        // Öncelik: olay başladı/bitti > kota (yeni segment, son round uyarısı) > yaklaşan olay > süre.
        // Run'ın son round'unda kota uyarısı "SON ROUND!" yerine geçer.
        SegmentEventDirector events = SegmentEventDirector.Instance;
        string quota = QuotaIntro(rounds);
        string eventIntro = SegmentEventText.Intro(events, rounds);
        bool last = rounds.CurrentRound >= rounds.MaxRounds;
        string subtitle; Color color;
        Color bossColor = BossTheme.Bright(SegmentEventText.IntroSubject(events, rounds));
        if (eventIntro != null) { subtitle = eventIntro; color = bossColor; }
        else if (quota != null) { subtitle = quota; color = quotaColor; }
        else if (last) { subtitle = "SON ROUND!"; color = roundColor; }
        else if (SegmentEventText.HeadsUp(events, rounds) is string headsUp) { subtitle = headsUp; color = bossColor; }
        else { subtitle = $"{Mathf.RoundToInt(rounds.RemainingTime)} SANİYE"; color = roundColor; }
        ScreenStamp.Show("round", $"ROUND {rounds.CurrentRound}", subtitle, color, roundStampY, 0.55f);
        FeelAudio.Play(FeelSound.Whoosh, 0.55f);
        pendingThumpTime = Time.unscaledTime + 0.2f;
    }

    // Hasat Kotası: dönemin ilk round'unda yeni kota duyurulur; son round'unda kota tutmadıysa kalan miktar uyarılır.
    // Dönem uzunluğu run takviminden gelir (eşit segment ya da açık boss takvimi).
    public static string QuotaIntro(RoundManager rounds)
    {
        if (rounds == null || !rounds.QuotaEnabled) return null;
        int round = rounds.CurrentRound;
        if (rounds.Calendar.IsPeriodStart(round))
            return $"KOTA {HarvestQuota.Format(rounds.QuotaTarget)} · {rounds.Calendar.PeriodLength(rounds.QuotaSegment)} ROUND";
        long missing = rounds.QuotaTarget - rounds.QuotaProgress;
        return rounds.IsQuotaSegmentEnd(round) && missing > 0 ? $"SON ROUND · KOTAYA {HarvestQuota.Format(missing)} KALDI" : null;
    }

    private void HandleLevelUp(int level)
    {
        ScreenStamp.Show("level", "LEVEL UP!", $"Lv. {level}", levelColor, levelStampY, 0.5f);
        float now = Time.unscaledTime;
        if (now < nextLevelEffectTime) return;
        nextLevelEffectTime = now + levelEffectCooldown;

        FeelAudio.Play(FeelSound.Chime, 0.8f);
        CameraFeel.PunchZoom(-0.03f);
        StartTileWave();
    }

    // Grid merkezinden dışa doğru her tile (ve üstündeki bitki) sırayla altın rengi parlar.
    private void StartTileWave()
    {
        GridManager grid = GridManager.Instance;
        if (grid == null || VFXManager.Instance == null) return;
        GridSystem system = grid.GetGridSystem();
        if (system == null) return;

        int width = grid.GetWidth(), height = grid.GetHeight();
        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float now = Time.unscaledTime;
        wave.Clear();
        for (int x = 0; x < width; x++)
        for (int z = 0; z < height; z++)
        {
            GridObject cell = system.GetGridObject(new GridPosition(x, z));
            if (cell == null) continue;
            float ring = Vector2.Distance(new Vector2(x, z), center);
            wave.Add(new WaveHit { time = now + ring * tileWaveStep, cell = cell });
        }
    }

    private void Update()
    {
        if (game == null || progression == null || resources == null) TrySubscribe();

        float now = Time.unscaledTime;
        if (pendingThumpTime > 0f && now >= pendingThumpTime)
        {
            pendingThumpTime = -1f;
            FeelAudio.Play(FeelSound.Thump, 0.6f);
        }

        for (int i = wave.Count - 1; i >= 0; i--)
        {
            if (wave[i].time > now) continue;
            FlashCell(wave[i].cell);
            wave.RemoveAt(i);
        }
    }

    private void FlashCell(GridObject cell)
    {
        VFXManager vfx = VFXManager.Instance;
        if (vfx == null || cell == null) return;
        GroundCell ground = cell.GetGroundCellCached();
        if (ground != null) vfx.PlayHitFlash(ground.GroundRenderer, levelColor);
        GameObject plant = cell.GetPlantObject();
        if (plant != null) vfx.PlayHitFlash(plant.GetComponentInChildren<Renderer>(), Color.white);
    }

    private void BeginRoundStats()
    {
        current = new RoundSummaryData { Round = RoundManager.Instance != null ? RoundManager.Instance.CurrentRound : 0 };
        scoreAtStart = HarvestScoreManager.Instance != null ? HarvestScoreManager.Instance.TotalScore : 0;
        levelAtStart = progression != null ? progression.CurrentLevel : 0;
        xpAtStart = progression != null ? progression.TotalXPEarned : 0;
    }

    private void FinishRoundStats()
    {
        long score = (HarvestScoreManager.Instance != null ? HarvestScoreManager.Instance.TotalScore : 0) - scoreAtStart;
        current.Score = (int)System.Math.Min(int.MaxValue, System.Math.Max(0L, score));
        current.Levels = (progression != null ? progression.CurrentLevel : 0) - levelAtStart;
        current.Xp = progression != null ? (int)System.Math.Min(int.MaxValue, progression.TotalXPEarned - xpAtStart) : 0;
        LastRound = current;
        current = null;
        SummaryVersion++;
    }

    // Sadece hasat kaynaklı eklemeler sayılır (kart skip ödülleri pozisyonsuz gelir, sayılmaz).
    private void HandleHarvest(ResourceType type, int amount, Vector3 position)
    {
        if (current == null) return;
        current.Harvests++;
        switch (type)
        {
            case ResourceType.Gold: current.Gold += amount; break;
            case ResourceType.Iron: current.Iron += amount; break;
            case ResourceType.Stone: current.Stone += amount; break;
        }
    }
}
