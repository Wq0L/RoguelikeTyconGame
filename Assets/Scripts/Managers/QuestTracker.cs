using System.Collections.Generic;
using UnityEngine;

// Kalıcı görevlerin run içi sayacı. RoundManager kurar.
// Kaynak olay: PlantHealth.AnyHarvested — Die içinde bitki başına bir kez (ölü bitki yeniden ölemez). Doğrudan ve davranış
// öldürmelerini kapsar; çifte ödül yalnız kaynağı ikiler, ayrı hasat olayı değildir.
// - Tek-run sayaçları yeni run'da (OnRunStarted) sıfırlanır; ana menüde sayım durur.
// - Olaya yalnız etkin tracker (Instance) tepki verir: sahne değişirken iki kopya aynı hasadı iki kez sayamaz.
// - Hedefe ulaşınca MetaSave'e hemen yazılır (kalıcı; kaybetmek ya da menüye dönmek geri almaz). Tamamlanmış görev yeniden sayılmaz.
[DisallowMultipleComponent]
public sealed class QuestTracker : MonoBehaviour
{
    public static QuestTracker Instance { get; private set; }

    private readonly Dictionary<QuestSO, int> counts = new();
    private readonly List<StartOptionSO> unlockedThisRun = new();
    private RoundManager rounds;
    private GameManager game;
    private bool counting;

    public IReadOnlyList<StartOptionSO> UnlockedThisRun => unlockedThisRun;
    public int Progress(QuestSO quest) => quest != null && counts.TryGetValue(quest, out int n) ? n : 0;

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        PlantHealth.AnyHarvested += HandleHarvest;
        if (rounds != null) rounds.OnRunStarted += ResetRun;
        if (rounds != null) rounds.OnRoundEnded += FlushSave;
    }

    private void OnDisable()
    {
        PlantHealth.AnyHarvested -= HandleHarvest;
        if (rounds != null) rounds.OnRunStarted -= ResetRun;
        if (rounds != null) rounds.OnRoundEnded -= FlushSave;
        FlushSave();
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
        counting = false;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        MetaSave.RetryPending();
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu) { counting = false; FlushSave(); }
    }

    private void FlushSave() => MetaSave.FlushPending();
    private void OnApplicationQuit() => FlushSave();
    private void OnApplicationPause(bool paused) { if (paused) FlushSave(); }

    private void ResetRun()
    {
        counts.Clear();
        unlockedThisRun.Clear();
        counting = true;
    }

    private void HandleHarvest(PlantHealth plant)
    {
        if (Instance != this || !counting || plant == null || plant.Data == null) return;
        StartCatalogSO catalog = StartCatalogSO.Active;
        if (catalog == null) return;
        foreach (QuestSO quest in catalog.quests)
        {
            if (quest == null || !Counts(quest, plant) || MetaSave.IsQuestDone(quest)) continue;
            int value = Progress(quest) + 1;
            counts[quest] = value;
            if (value >= quest.target)
            {
                if (MetaSave.CompleteQuest(quest, catalog.UnlockedBy(quest)))
                    unlockedThisRun.AddRange(catalog.UnlockedBy(quest));
            }
            else MetaSave.ReportProgress(quest, value);
        }
    }

    private static bool Counts(QuestSO quest, PlantHealth plant) => quest.counter switch
    {
        QuestCounter.LegendaryHarvestInRun => plant.Data.rarity == PlantRarity.Legendary,
        _ => false
    };
}
