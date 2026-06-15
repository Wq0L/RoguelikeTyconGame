using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    public GameStates CurrentState { get; private set; }
    public event Action<GameStates> OnGameStateChanged;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SetState(GameStates.Round);
    }

    private void Update()
    {
        Debug.Log($"Current Game State: {CurrentState}");
    }

    public void SetState(GameStates newState)
    {
        if (CurrentState == newState) return;

        CurrentState = newState;
        HandleStateEnter(newState);
        OnGameStateChanged?.Invoke(CurrentState);

        Debug.Log($"GameManager: State changed to {CurrentState}");
    }

    public void ReturnToMenu() => SetState(GameStates.MainMenu);
    public void StartRunSetup() => SetState(GameStates.RunSetup);
    public void StartGame() => SetState(GameStates.Round);
    public void ShowRoundEnd() => SetState(GameStates.RoundEnd);   // ← yeni
    public void OpenShop() => SetState(GameStates.Shop);
    public void StartPlacement() => SetState(GameStates.Placing);
    public void EnterSellMode() => SetState(GameStates.Selling);
    public void CompleteRun() => SetState(GameStates.RunComplete); // ← isim değişti

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

            default:
                EnterRound();
                break;
        }
    }

    private void EnterMainMenu() => Time.timeScale = 0f;
    private void EnterRound() => Time.timeScale = 1f;
    private void EnterRoundEnd() => Time.timeScale = 0f;
    private void EnterShop() => Time.timeScale = 0f;
    private void EnterPlacing() => Time.timeScale = 0f;
    private void EnterSelling() => Time.timeScale = 0f;
    private void EnterRunSetup() => Time.timeScale = 0f;
    private void EnterRunComplete() => Time.timeScale = 0f;
}