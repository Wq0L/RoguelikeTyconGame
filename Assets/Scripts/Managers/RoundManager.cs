using System;
using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    public event Action OnRoundEnded;
    public event Action<int> OnTimeChanged;
    public event Action<int> OnRoundChanged;


    [SerializeField] private float roundDuration = 30f;
    private int lastDisplayedSecond = -1;

    public int CurrentRound { get; private set; } = 1;
    public float RemainingTime { get; private set; }
    public bool IsRoundActive { get; private set; }

    private int pendingCardSelections = 0;

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
        StartRound();
        ProgressionManager.Instance.OnLevelUp += HandleLevelUp; // Start'ta güvenli
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

    public void StartRound()
    {
        CurrentRound = Mathf.Max(CurrentRound, 1);
        RemainingTime = roundDuration;
        IsRoundActive = true;

        OnRoundChanged?.Invoke(CurrentRound);
        GameManager.Instance.StartGame();
    }

    private void EndRound()
    {
        IsRoundActive = false;
        RemainingTime = 0f;
        OnRoundEnded?.Invoke();

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

    public void StartNextRound()
    {
        if (GameManager.Instance.CurrentState != GameStates.Shop &&
            GameManager.Instance.CurrentState != GameStates.RoundEnd)
            return;

        CurrentRound++;

        if (CurrentRound > 150)
        {
            GameManager.Instance.CompleteRun();
            return;
        }
        StartRound();
    }

    public void ResetRounds()
    {
        CurrentRound = 1;
        RemainingTime = roundDuration;
        IsRoundActive = false;
    }

}