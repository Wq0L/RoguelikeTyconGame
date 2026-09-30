using System;
using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameStates CurrentState { get; private set; }
    public event Action<GameStates> OnGameStateChanged;

    [Tooltip("Round bitince dünya bir anda donmaz; bu sürede (gerçek zaman) yavaşlayarak durur.")]
    [SerializeField, Min(0f)] private float roundEndStopDuration = 0.9f;
    private Coroutine timeStop;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetState(GameStates.MainMenu);
    }

    // Diagnostic only: Debug.Log($"Current Game State: {CurrentState}");

    public void SetState(GameStates newState)
    {
        if (CurrentState == newState) return;

        GameStates previous = CurrentState;
        float previousTimeScale = Time.timeScale;
        CurrentState = newState;
        HandleStateEnter(newState);
        SoftenRoundEnd(previous, newState, previousTimeScale);
        OnGameStateChanged?.Invoke(CurrentState);

        // Debug.Log($"GameManager: State changed to {CurrentState}");
    }

    public void ReturnToMenu() => SetState(GameStates.MainMenu);
    public void StartRunSetup() => SetState(GameStates.RunSetup);
    public void StartGame() => SetState(GameStates.Round);
    public void ShowRoundEnd() => SetState(GameStates.RoundEnd);   
    public void StartCardSelection() => SetState(GameStates.CardSelection);
    public void OpenShop() => SetState(GameStates.Shop);
    public void StartPlacement() => SetState(GameStates.Placing);
    public void EnterSellMode() => SetState(GameStates.Selling);
    public void CompleteRun() => SetState(GameStates.RunComplete);
    public void StartRoundChoice() => SetState(GameStates.RoundChoice);

    private void HandleStateEnter(GameStates state)
    {
        switch (state)
        {
            case GameStates.MainMenu:
                EnterMainMenu();
                break;

            case GameStates.RunSetup:
                EnterRunSetup();
                break;

            case GameStates.Round:
                EnterRound();
                break;

            case GameStates.RoundEnd:
                EnterRoundEnd();
                break;

            case GameStates.CardSelection:
                EnterCardSelection();
                break;

            case GameStates.Shop:
                EnterShop();
                break;

            case GameStates.Placing:
                EnterPlacing();
                break;

            case GameStates.Selling:
                EnterSelling();
                break;

            case GameStates.RunComplete:
                EnterRunComplete();
                break;

            case GameStates.RoundChoice:
                EnterRoundChoice();
                break;

            default:
                EnterRound();
                break;
        }
    }

    // Round'dan duraklatılan bir ekrana geçerken zaman anında 0 olmaz: parçacıklar, yazılar ve bitkiler
    // yavaşlayarak durur. Round bittiği için hasar kaynağı yok (oyuncu pasif, davranışlar temizlendi);
    // ekonomi etkilenmez. Başka bir state'e geçilirse yarıda kesilir.
    private void SoftenRoundEnd(GameStates previous, GameStates state, float fromScale)
    {
        if (timeStop != null) { StopCoroutine(timeStop); timeStop = null; }
        bool roundEnded = previous == GameStates.Round &&
            (state == GameStates.RoundEnd || state == GameStates.CardSelection || state == GameStates.RunComplete || state == GameStates.RoundChoice);
        if (!roundEnded || roundEndStopDuration <= 0f || fromScale <= 0f) return;
        Time.timeScale = fromScale;
        timeStop = StartCoroutine(EaseTimeToStop(fromScale));
    }

    private IEnumerator EaseTimeToStop(float fromScale)
    {
        for (float t = 0f; t < roundEndStopDuration; t += Time.unscaledDeltaTime)
        {
            float remaining = 1f - t / roundEndStopDuration;
            Time.timeScale = fromScale * remaining * remaining;
            yield return null;
        }
        Time.timeScale = 0f;
        timeStop = null;
    }

    private void EnterMainMenu() => Time.timeScale = 0f;
    private void EnterRound() => Time.timeScale = 1f;
    private void EnterRoundEnd() => Time.timeScale = 0f;
    private void EnterCardSelection() => Time.timeScale = 0f;
    private void EnterShop() => Time.timeScale = 0f;
    private void EnterPlacing() => Time.timeScale = 0f;
    private void EnterSelling() => Time.timeScale = 0f;
    private void EnterRunSetup() => Time.timeScale = 0f;
    private void EnterRunComplete() => Time.timeScale = 0f;
    private void EnterRoundChoice() => Time.timeScale = 0f;
}
