using System.Collections.Generic;
using UnityEngine;

// Boss sonrası bedelli uzmanlaşma (run başına bir seçim). RoundManager kurar; ayarlar aktif run profilinden gelir.
// - Profilin segmenti kotayla kapanınca (run'ın son round'u değilse) seçim bekler; kart seçimlerinden sonra açılır.
// - Seçim tek seferdir: bekleyen seçim yokken ya da seçilmişken istek reddedilir (çift tıklama, panelin yeniden açılması).
// - Etki yalnız hasar katsayılarıdır; yeni run'da (OnRunStarted), ana menüde ve yok edilmede aynı ClearAll ile temizlenir.
// Arayüz seçenekleri gösterir ve Choose ister; kural burada.
[DisallowMultipleComponent]
public sealed class SpecializationManager : MonoBehaviour, IRoundChoice
{
    public static SpecializationManager Instance { get; private set; }

    private RoundManager rounds;
    private GameManager game;
    private bool resolved;

    public SpecializationSO Chosen { get; private set; }
    public bool IsPending { get; private set; }
    public int Version { get; private set; }

    public IReadOnlyList<SpecializationSO> Options =>
        rounds != null && rounds.Profile != null ? rounds.Profile.specializationOptions : (IReadOnlyList<SpecializationSO>)System.Array.Empty<SpecializationSO>();

    public static float DirectMultiplier => Instance != null && Instance.Chosen != null ? Instance.Chosen.directDamageMultiplier : 1f;
    public static float BehaviorMultiplier => Instance != null && Instance.Chosen != null ? Instance.Chosen.behaviorDamageMultiplier : 1f;

    private void Awake()
    {
        Instance = this;
        rounds = GetComponent<RoundManager>();
    }

    private void OnEnable()
    {
        if (rounds == null) return;
        rounds.OnRunStarted += ClearAll;
        rounds.OnRoundEnded += HandleRoundEnded;
        rounds.RegisterRoundChoice(this);
    }

    private void OnDisable()
    {
        if (rounds != null)
        {
            rounds.OnRunStarted -= ClearAll;
            rounds.OnRoundEnded -= HandleRoundEnded;
            rounds.UnregisterRoundChoice(this);
        }
        if (game != null) game.OnGameStateChanged -= HandleStateChanged;
        game = null;
    }

    private void OnDestroy()
    {
        ClearAll();
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (game != null || GameManager.Instance == null) return;
        game = GameManager.Instance;
        game.OnGameStateChanged += HandleStateChanged;
    }

    private void HandleStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu) ClearAll();
    }

    // Kota değerlendirmesi OnRoundEnded'den önce yapılır; burada sonucu okunur.
    private void HandleRoundEnded()
    {
        RunProfileSO profile = rounds.Profile;
        if (resolved || IsPending || profile == null || profile.specializationAfterSegment <= 0 || profile.specializationOptions.Count == 0) return;
        int round = rounds.CurrentRound;
        if (!rounds.IsQuotaSegmentEnd(round) || round / rounds.QuotaSegmentRounds != profile.specializationAfterSegment) return;
        // Kota tutmadıysa ödül yok; run'ın son round'uysa kullanılamayacak seçim açılmaz.
        if (rounds.EndedByQuota || round >= rounds.MaxRounds) return;
        IsPending = true;
        Version++;
    }

    public bool Choose(SpecializationSO option)
    {
        if (!IsPending || resolved || option == null || !ContainsOption(option)) return false;
        Chosen = option;
        IsPending = false;
        resolved = true;
        Version++;
        rounds.ContinueAfterRoundChoice();
        return true;
    }

    private bool ContainsOption(SpecializationSO option)
    {
        foreach (SpecializationSO o in Options) if (o == option) return true;
        return false;
    }

    // Tek temizlik yolu: yeni run, ana menü, yok edilme (yeniden başlatma ve sahne değişimi).
    public void ClearAll()
    {
        Chosen = null;
        IsPending = false;
        resolved = false;
        Version++;
    }
}
