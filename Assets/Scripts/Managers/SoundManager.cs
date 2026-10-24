using System.Collections.Generic;
using UnityEngine;

public enum SoundChannel { Music, Feedback, Hit, Gameplay }

// Owns every runtime audio source. Callers keep cues/cooldowns, never sources.
[DisallowMultipleComponent]
public sealed partial class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }
    private readonly List<Voice> voices = new();
    private readonly Voice[] feedback = new Voice[8];
    private readonly Voice[] hits = new Voice[8];
    private int nextFeedback, nextHit;
    private RoundManager rounds;
    private GameManager game;
    private bool runtimeFallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instance = null;

    public static SoundManager Ensure()
    {
        if (Instance == null)
        {
            Instance = Object.FindFirstObjectByType<SoundManager>();
            if (Instance == null)
            {
                var fallback = new GameObject("Sound Manager (Menu Fallback)").AddComponent<SoundManager>();
                fallback.runtimeFallback = true;
                DontDestroyOnLoad(fallback.gameObject);
            }
        }
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            if (!Instance.runtimeFallback) { Destroy(gameObject); return; }
            Instance.enabled = false;
            Destroy(Instance.gameObject);
        }
        Instance = this;
        InitializeMusic();
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        GameSettings.Changed += ApplySettings;
        Application.focusChanged += OnFocusChanged;
        SoundEvents.EffectRequested += OnEffectRequested;
        ApplySettings();
    }

    private void OnEffectRequested(SoundEvents.EffectRequest request) =>
        PlayEffect(request.Clip, request.Volume, request.Pitch, request.Hit);

    private void OnFocusChanged(bool _) => ApplySettings();

    private void LateUpdate()
    {
        // Managers may appear later or be replaced when a scene is loaded.
        if (rounds != RoundManager.Instance || game != GameManager.Instance) BindEvents();
        for (int i = voices.Count - 1; i >= 0; i--)
        {
            if (!voices[i].OwnerAlive) { voices[i].Dispose(); voices.RemoveAt(i); }
            else voices[i].ApplyVolume();
        }
        // Track completion and a direct timeScale pause have no game event.
        TickMusic();
    }

    private void BindEvents()
    {
        UnbindEvents();
        StopEffects();
        rounds = RoundManager.Instance;
        game = GameManager.Instance;
        if (rounds != null)
        {
            rounds.OnRunStarted += OnRunStarted;
            rounds.OnRoundChanged += OnRoundStarted;
            rounds.OnRoundEnded += OnRoundEnded;
        }
        if (game != null) game.OnGameStateChanged += OnStateChanged;
        ResetMusic();
    }

    private void UnbindEvents()
    {
        if (rounds != null)
        {
            rounds.OnRunStarted -= OnRunStarted;
            rounds.OnRoundChanged -= OnRoundStarted;
            rounds.OnRoundEnded -= OnRoundEnded;
        }
        if (game != null) game.OnGameStateChanged -= OnStateChanged;
        rounds = null;
        game = null;
    }

    private void OnRunStarted() { StopEffects(); ResetMusic(); }
    private void OnRoundStarted(int _) => TickMusic();
    private void OnRoundEnded() => PauseMusic();
    private void OnStateChanged(GameStates state)
    {
        if (state == GameStates.MainMenu || state == GameStates.RunSetup || state == GameStates.RunComplete)
        { StopEffects(); ResetMusic(); }
        else TickMusic();
    }

    public Voice CreateVoice(SoundChannel channel, GameObject owner)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        var voice = new Voice(source, channel, owner != null ? owner : gameObject);
        voices.Add(voice);
        return voice;
    }

    // Feedback continues in menus/round summaries. Gameplay callers control pause.
    public void PlayEffect(AudioClip clip, float volume = 1f, float pitch = 1f, bool hit = false)
    {
        if (clip == null || volume <= 0f || Application.isBatchMode) return;
        var pool = hit ? hits : feedback;
        int index = hit ? nextHit : nextFeedback;
        if (hit) nextHit = (nextHit + 1) % hits.Length;
        else nextFeedback = (nextFeedback + 1) % feedback.Length;
        pool[index] ??= CreateVoice(hit ? SoundChannel.Hit : SoundChannel.Feedback, gameObject);
        pool[index].Play(clip, volume, pitch);
    }

    public void PlayFeel(FeelSound sound, float volume = 1f, float pitch = 1f) =>
        FeelAudio.Play(sound, volume, pitch);

    public void StopEffects()
    {
        foreach (var voice in voices) if (voice.Channel != SoundChannel.Music) voice.Stop();
    }

    public void ApplySettings()
    {
        AudioListener.volume = Mathf.Clamp01(GameSettings.MasterVolume);
        AudioListener.pause = GameSettings.MuteInBackground && !Application.isFocused;
        foreach (var voice in voices) voice.ApplyVolume();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= ApplySettings;
        Application.focusChanged -= OnFocusChanged;
        SoundEvents.EffectRequested -= OnEffectRequested;
        UnbindEvents();
        foreach (var voice in voices) voice.Stop();
        ResetMusic();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public sealed class Voice
    {
        private readonly AudioSource source;
        private readonly GameObject owner;
        private float gain;
        public SoundChannel Channel { get; }
        public AudioClip Clip => source != null ? source.clip : null;
        public bool IsPlaying => source != null && source.isPlaying;
        internal bool OwnerAlive => owner != null;
        internal Voice(AudioSource source, SoundChannel channel, GameObject owner)
        { this.source = source; Channel = channel; this.owner = owner; }
        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f, bool loop = false)
        {
            if (source == null || clip == null) return;
            source.Stop();
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch, 0.01f, 3f);
            source.loop = loop;
            gain = Mathf.Clamp01(volume);
            ApplyVolume();
            source.Play();
        }
        internal void ApplyVolume()
        {
            if (source != null) source.volume = gain * Mathf.Clamp01(
                Channel == SoundChannel.Music ? GameSettings.MusicVolume : GameSettings.EffectsVolume);
        }
        public void Pause() { if (source != null) source.Pause(); }
        public void UnPause() { if (source != null) source.UnPause(); }
        public void Stop() { if (source != null) source.Stop(); }
        internal void Dispose() { if (source != null) Object.Destroy(source); }
    }
}
