using System;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    public event Action OnRoundEnded;
    public event Action<int> OnTimeChanged;
    public event Action<int> OnRoundChanged;


    [SerializeField] private float roundDuration = 30f;
    [SerializeField] private int maxRounds = 150;
    private int lastDisplayedSecond = -1;

    public int CurrentRound { get; private set; } = 1;
    public float RemainingTime { get; private set; }
    public bool IsRoundActive { get; private set; }

    private int pendingCardSelections = 0;
    private int skipUsesRemaining;
    private bool awaitingFirstRound = true;

    public bool IsPreparingFirstRound => awaitingFirstRound;

    public int SkipUsesRemaining => skipUsesRemaining;
    public int MaxRounds => maxRounds;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ProgressionManager.Instance.OnLevelUp += HandleLevelUp; // Start'ta güvenli
        BeginRun();
    }

    private void OnDestroy()
    {
        if (ProgressionManager.Instance != null)
            ProgressionManager.Instance.OnLevelUp -= HandleLevelUp;
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Round)
            return;

        if (!IsRoundActive)
            return;

        TickRound();
    }

    private void TickRound()
    {
        RemainingTime -= Time.deltaTime;
        RemainingTime = Mathf.Max(RemainingTime, 0f);

        int currentSecond = Mathf.CeilToInt(RemainingTime);

        if (currentSecond != lastDisplayedSecond)
        {
            lastDisplayedSecond = currentSecond;
            OnTimeChanged?.Invoke(currentSecond);
        }

        if (RemainingTime <= 0f)
        {
            EndRound();
        }
    }

    public void BeginRun()
    {
        CurrentRound = 1;
        pendingCardSelections = 0;
        awaitingFirstRound = true;
        IsRoundActive = false;
        RemainingTime = roundDuration;
        skipUsesRemaining = 0;
        lastDisplayedSecond = -1;

        SkillTreeManager.Instance.ResetTree();
        UnlockManager.Instance.ResetUnlocks();

        GameManager.Instance.StartRunSetup();
    }

    public void StartRound()
    {
        awaitingFirstRound = false;
        CurrentRound = Mathf.Max(CurrentRound, 1);
        RemainingTime = roundDuration;
        IsRoundActive = true;
        lastDisplayedSecond = -1;

        skipUsesRemaining = 1 + Mathf.RoundToInt(
        StatManager.Instance.GetFinalStat(StatType.CardSkip, StatTarget.All));

        OnRoundChanged?.Invoke(CurrentRound);
        GameManager.Instance.StartGame();
    }

    private void EndRound()
    {
        IsRoundActive = false;
        RemainingTime = 0f;
        OnRoundEnded?.Invoke();

        // Son round ise direkt bitir
        if (CurrentRound >= maxRounds)
        {
            pendingCardSelections = 0; // kart seçimini atla
            GameManager.Instance.CompleteRun();
            return;
        }


        if (pendingCardSelections > 0)
            GameManager.Instance.StartCardSelection();
        else
            GameManager.Instance.ShowRoundEnd();
    }
    
    private void HandleLevelUp(int newLevel)
    {
        pendingCardSelections++;
    }

    public bool OnCardSelectionComplete()
    {
        pendingCardSelections--;

        if (pendingCardSelections > 0)
            return true;  // daha seçim var, kartları yenile

        GameManager.Instance.ShowRoundEnd();
        return false;  // bitti, RoundEnd'e geç
    }

    public bool TryUseSkip()
    {
        if(skipUsesRemaining <= 0)
        {
            return false;
        }

        skipUsesRemaining--;
        return true;
    }

    public void StartNextRound()
    {
        if (GameManager.Instance.CurrentState != GameStates.Shop &&
            GameManager.Instance.CurrentState != GameStates.RoundEnd &&
            GameManager.Instance.CurrentState != GameStates.RunSetup)
            return;

        if (!awaitingFirstRound) CurrentRound++;

        if (CurrentRound > maxRounds)
        {
            GameManager.Instance.CompleteRun();
            return;
        }
        StartRound();
    }

    public void ResetRounds()
    {
        awaitingFirstRound = true;
        CurrentRound = 1;
        RemainingTime = roundDuration;
        IsRoundActive = false;
    }

}
