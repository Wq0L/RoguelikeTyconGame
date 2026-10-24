using UnityEngine;

// Music uses its own random stream, so track selection cannot affect rewards.

public sealed partial class SoundManager
{
    public const string DefaultResourcePath = "Music/RoundMusic";

    [System.Serializable]
    public sealed class Entry
    {
        public string resourcePath;
        public float weight = 1f;
        public AudioClip clip;
        [Tooltip("Bu puandan itibaren seçilebilir (dahil). Boss ödülleri puanı artırır.")]
        [Min(0)] public int minPower;
        [Tooltip("Bu puana kadar seçilebilir (dahil). 0: üst sınır yok.")]
        [Min(0)] public int maxPower;
    }
    [System.Serializable]
    private sealed class Pool { public Entry[] entries = null; }
    [Header("Round Music Pool")]
    [SerializeField] private Entry[] entries = new Entry[0];
    private readonly System.Random random = new System.Random();
    [SerializeField, Range(0f, 1f)] private float volume = 0.65f;

    private Voice musicSource;
    private bool started;
    private bool paused;
    public int MusicPower => BossRewardManager.Instance != null ? BossRewardManager.Instance.MusicPower : 0;

    private void InitializeMusic()
    {
        musicSource = CreateVoice(SoundChannel.Music, gameObject);
        var config = Resources.Load<TextAsset>("Music/RoundMusicPool");
        if ((entries == null || entries.Length == 0) && config != null)
        {
            try { entries = JsonUtility.FromJson<Pool>(config.text)?.entries; }
            catch (System.ArgumentException) { Debug.LogWarning("Invalid round music pool.", this); }
        }
        if (entries == null)
            entries = new[] { new Entry { resourcePath = DefaultResourcePath } };
        foreach (var entry in entries)
            if (entry != null && entry.clip == null && !string.IsNullOrEmpty(entry.resourcePath))
                entry.clip = Resources.Load<AudioClip>(entry.resourcePath);
    }

    private void TickMusic()
    {
        if (musicSource == null) return;


        if (game == null || game.CurrentState == GameStates.MainMenu ||
            game.CurrentState == GameStates.RunSetup || game.CurrentState == GameStates.RunComplete)
        {
            ResetMusic();
            return;
        }

        bool active = rounds != null && rounds.IsRoundActive &&
            game.CurrentState == GameStates.Round && Time.timeScale > 0f;
        if (active)
        {
            if (!started) PlayNext();
            else if (paused) musicSource.UnPause();
            else if (!musicSource.IsPlaying && !AudioListener.pause) PlayNext();
            paused = false;
        }
        else if (started && !paused)
        {
            musicSource.Pause();
            paused = true;
        }
    }

    private static bool Eligible(Entry entry) => entry != null && entry.clip != null &&
        entry.weight > 0f && !float.IsNaN(entry.weight) && !float.IsInfinity(entry.weight) &&
        entry.minPower >= 0 && (entry.maxPower == 0 || entry.maxPower >= entry.minPower);

    public static bool MatchesPower(Entry entry, int power) => Eligible(entry) &&
        power >= entry.minPower && (entry.maxPower == 0 || power <= entry.maxPower);

    // Pure selection entry point also used by verification; an uncovered range stays silent.
    public static Entry SelectMusic(Entry[] pool, int power, System.Random rng)
    {
        double total = 0;
        if (pool == null) return null;
        foreach (var entry in pool)
            if (MatchesPower(entry, power)) total += entry.weight;
        if (total <= 0) return null;
        double roll = rng.NextDouble() * total;
        Entry chosen = null;
        foreach (var entry in pool)
        {
            if (!MatchesPower(entry, power)) continue;
            chosen = entry;
            roll -= entry.weight;
            if (roll < 0) break;
        }
        return chosen;
    }

    private void PlayNext()
    {
        Entry chosen = SelectMusic(entries, MusicPower, random);
        if (chosen == null) return;
        int count = 0;
        foreach (var entry in entries) if (Eligible(entry)) count++;
        // Native looping is safe only for a sole, unrestricted track. Otherwise even
        // a single current candidate must finish so the next selection sees new power.
        bool loop = count == 1 && chosen.minPower == 0 && chosen.maxPower == 0;
        musicSource.Play(chosen.clip, volume, 1f, loop);
        started = true;
    }

    private void ResetMusic()
    {
        if (musicSource != null && started) musicSource.Stop();
        started = false;
        paused = false;
    }

    private void PauseMusic()
    {
        if (!started || paused) return;
        musicSource.Pause();
        paused = true;
    }
}

