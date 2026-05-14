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
        SetState(GameStates.Playing);
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

    public void StartGame() => SetState(GameStates.Playing);
    public void OpenShop() => SetState(GameStates.Shop);
    public void PauseGame() => SetState(GameStates.Paused);
    public void GameOver() => SetState(GameStates.GameOver);
    public void ReturnToMenu() => SetState(GameStates.MainMenu);

    private void HandleStateEnter(GameStates state)
    {
        switch (state)
        {
            case GameStates.MainMenu:
                EnterMainMenu();
                break;
            case GameStates.Playing:
                EnterPlaying();
                break;
            case GameStates.Shop:
                EnterShop();
                break;
            case GameStates.Paused:
                EnterPaused();
                break;
            case GameStates.GameOver:
                EnterGameOver();
                break;
            default:
                EnterPlaying();
                break;
        }
    }

    private void EnterMainMenu()
    {
        Time.timeScale = 0f;
    }

    private void EnterPlaying()
    {
        Time.timeScale = 1f;
    }

    private void EnterShop()
    {
        Time.timeScale = 0f;
    }

    private void EnterPaused()
    {
        Time.timeScale = 0f;
    }

    private void EnterGameOver()
    {
        Time.timeScale = 0f;
    }
}
