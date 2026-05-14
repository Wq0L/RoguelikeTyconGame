using UnityEngine;

public class RoundManager : MonoBehaviour
{
    public static RoundManager Instance { get; private set; }

    [SerializeField] private float roundDuration = 30f;

    public int CurrentRound { get; private set; } = 1;
    public float RemainingTime { get; private set; }
    public bool IsRoundActive { get; private set; }

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
    }

    private void Update()
    {
        if (GameManager.Instance.CurrentState != GameStates.Playing)
            return;

        if (!IsRoundActive)
            return;

        TickRound();
    }

    private void TickRound()
    {
        RemainingTime -= Time.deltaTime;
        RemainingTime = Mathf.Max(RemainingTime, 0f);

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

        GameManager.Instance.StartGame();
    }

    private void EndRound()
    {
        IsRoundActive = false;
        RemainingTime = 0f;

        GameManager.Instance.OpenShop();
    }

    public void StartNextRound()
    {
        if (GameManager.Instance.CurrentState != GameStates.Shop)
            return;

        CurrentRound++;
        StartRound();
    }

    public void ResetRounds()
    {
        CurrentRound = 1;
        RemainingTime = roundDuration;
        IsRoundActive = false;
    }
}