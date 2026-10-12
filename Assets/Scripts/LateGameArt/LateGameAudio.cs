using UnityEngine;

// Bounded, non-stealing voice pool. Scaled time freezes both clips and repeat windows during pause.
public sealed class LateGameAudio
{
    private const int Capacity = 8;
    private readonly AudioSource[] sources = new AudioSource[Capacity];
    private readonly float[] remaining = new float[Capacity];
    private readonly float[] cooldown = new float[1024];
    private readonly GameObject owner;
    private bool paused;
    public int ActiveVoices { get; private set; }
    public int Skipped { get; private set; }
    public LateGameAudio(GameObject owner) => this.owner = owner;

    public void Play(LateGameArtSO.SoundBinding binding)
    {
        if (binding.clip == null || Time.timeScale <= 0f) return;
        int key = (int)binding.variant * 32 + (int)binding.sound;
        if (key >= cooldown.Length || cooldown[key] > 0f) { Skipped++; return; }
        int count = 0, free = -1;
        for (int i = 0; i < Capacity; i++)
        {
            if (remaining[i] <= 0f) { if (free < 0) free = i; }
            else if (sources[i].clip == binding.clip) count++;
        }
        if (free < 0 || count >= Mathf.Clamp(binding.voices == 0 ? 2 : binding.voices, 1, Capacity)) { Skipped++; return; }
        if (sources[free] == null)
        {
            sources[free] = owner.AddComponent<AudioSource>();
            sources[free].playOnAwake = false;
            sources[free].spatialBlend = 0f;
        }
        sources[free].clip = binding.clip;
        sources[free].volume = Mathf.Clamp01(binding.volume);
        sources[free].Play();
        remaining[free] = binding.clip.length;
        cooldown[key] = binding.repeatSeconds > 0f ? binding.repeatSeconds : .06f;
        ActiveVoices++;
    }

    public void Step(float delta, bool playing)
    {
        for (int i = 0; i < cooldown.Length; i++) cooldown[i] = Mathf.Max(0f, cooldown[i] - delta);
        ActiveVoices = 0;
        for (int i = 0; i < Capacity; i++)
        {
            if (remaining[i] <= 0f) continue;
            if (!playing && !paused) sources[i].Pause();
            if (playing && paused) sources[i].UnPause();
            remaining[i] = Mathf.Max(0f, remaining[i] - delta);
            if (remaining[i] <= 0f) sources[i].Stop(); else ActiveVoices++;
        }
        paused = !playing;
    }

    public void Clear()
    {
        for (int i = 0; i < Capacity; i++) { if (sources[i] != null) sources[i].Stop(); remaining[i] = 0f; }
        System.Array.Clear(cooldown, 0, cooldown.Length);
        ActiveVoices = 0;
        paused = false;
    }
}
